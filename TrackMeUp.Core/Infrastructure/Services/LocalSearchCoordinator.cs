using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using TrackMeUp.Application;
using TrackMeUp.Search;

namespace TrackMeUp.Services;

/// <summary>Builds the mandatory Lucene projection from durable TrackMeUp sources and executes local queries.</summary>
internal sealed class LocalSearchCoordinator : IAsyncDisposable
{
    private const int MaximumIncrementalChanges = 2_000;
    private readonly LocalStore _store;
    private readonly ILocalSearchService _search;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private bool _disposed;

    /// <summary>Creates the runtime-owned local search coordinator.</summary>
    internal LocalSearchCoordinator(LocalStore store, ILocalSearchService search)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _search = search ?? throw new ArgumentNullException(nameof(search));
    }

    /// <summary>Ensures current durable data is indexed, then executes a ranked local query.</summary>
    internal Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        // Store inspection, index refresh, and Lucene reads contain synchronous work. Enter the
        // thread pool before any of it can continue inline on a WinUI dispatcher.
        return Task.Run(() => SearchCoreAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<SearchResponse> SearchCoreAsync(
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCurrentAsync(cancellationToken).ConfigureAwait(false);
        var settings = _store.LoadSettings();
        var query = request with
        {
            QueryLanguage = string.IsNullOrWhiteSpace(request.QueryLanguage)
                ? ResolveDefaultLanguage(settings)
                : request.QueryLanguage,
            EnableSynonyms = settings.SearchSynonymsEnabled,
            EnableFuzzyMatching = settings.SearchTypoToleranceEnabled
        };
        return await _search.SearchAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ensures current durable data is indexed, then returns local query suggestions.</summary>
    internal Task<IReadOnlyList<SearchSuggestion>> SuggestAsync(
        SearchSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        // Suggestions share the same synchronous index-refresh path and must never run inline
        // on the caller's dispatcher, even when every semaphore is immediately available.
        return Task.Run(() => SuggestCoreAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyList<SearchSuggestion>> SuggestCoreAsync(
        SearchSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCurrentAsync(cancellationToken).ConfigureAwait(false);
        return await _search.SuggestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replaces the complete derived index and returns the number of indexed documents.</summary>
    internal async Task<int> RebuildAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _indexGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RebuildCurrentSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _indexGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _search.DisposeAsync().ConfigureAwait(false);
        _indexGate.Dispose();
    }

    private async Task EnsureCurrentAsync(CancellationToken cancellationToken)
    {
        var sourceRevision = _store.GetSearchSourceRevision();
        if (sourceRevision == _search.CommittedSourceRevision)
        {
            return;
        }

        await _indexGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            sourceRevision = _store.GetSearchSourceRevision();
            var committedRevision = _search.CommittedSourceRevision;
            if (sourceRevision == committedRevision)
            {
                return;
            }

            if (committedRevision > sourceRevision)
            {
                _ = await RebuildCurrentSnapshotAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var changes = _store.LoadSearchSourceChanges(committedRevision, MaximumIncrementalChanges + 1);
            if (!CanReplayIncrementally(changes, committedRevision, sourceRevision))
            {
                _ = await RebuildCurrentSnapshotAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var mutations = BuildIncrementalMutations(changes, cancellationToken);
            if (mutations is null)
            {
                _ = await RebuildCurrentSnapshotAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await _search.ApplyBatchAsync(mutations, sourceRevision, cancellationToken).ConfigureAwait(false);
            _store.PruneSearchSourceChanges(sourceRevision);
        }
        finally
        {
            _indexGate.Release();
        }
    }

    private async Task<int> RebuildCurrentSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceRevision = _store.GetSearchSourceRevision();
        var documents = BuildDocuments(cancellationToken);
        await _search.RebuildAsync(documents, sourceRevision, cancellationToken).ConfigureAwait(false);
        _store.PruneSearchSourceChanges(sourceRevision);
        return documents.Count;
    }

    private static bool CanReplayIncrementally(
        IReadOnlyList<SearchSourceChange> changes,
        long committedRevision,
        long sourceRevision)
    {
        if (changes.Count == 0 || changes.Count > MaximumIncrementalChanges)
        {
            return false;
        }

        var expectedRevision = committedRevision + 1;
        foreach (var change in changes)
        {
            if (change.Revision != expectedRevision++)
            {
                return false;
            }
        }

        return changes[^1].Revision == sourceRevision
            && changes.All(change => !string.Equals(change.Kind, "rebuild", StringComparison.Ordinal));
    }

    private IReadOnlyCollection<SearchIndexMutation>? BuildIncrementalMutations(
        IReadOnlyList<SearchSourceChange> changes,
        CancellationToken cancellationToken)
    {
        var settings = _store.LoadSettings();
        var defaultLanguage = ResolveDefaultLanguage(settings);
        var installationProfiles = _store.GetInstallationProfiles()
            .ToDictionary(profile => profile.InstallationId, StringComparer.Ordinal);
        InstallationProfile RequireInstallation(string installationId) =>
            installationProfiles.TryGetValue(installationId, out var profile)
                ? profile
                : throw new InvalidDataException($"Search source references an unknown installation '{installationId}'.");

        var mutations = new Dictionary<string, SearchIndexMutation>(StringComparer.Ordinal);
        void Set(SearchIndexMutation mutation) => mutations[mutation.Id] = mutation;

        foreach (var change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (change.Kind)
            {
                case "activity":
                    if (!long.TryParse(change.EntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var activityId)
                        || activityId <= 0)
                    {
                        return null;
                    }

                    var activityDocumentId = $"activity:{activityId}";
                    var sample = string.Equals(change.Operation, "delete", StringComparison.Ordinal)
                        ? null
                        : _store.LoadActivitySample(activityId);
                    Set(sample is null
                        ? SearchIndexMutation.Delete(activityDocumentId)
                        : SearchIndexMutation.Upsert(BuildActivityDocument(
                            activityId,
                            sample,
                            defaultLanguage,
                            RequireInstallation(sample.InstallationId))));
                    break;

                case "analysis":
                    if (string.IsNullOrWhiteSpace(change.EntityId))
                    {
                        return null;
                    }

                    var analysisDocumentId = $"analysis:{change.EntityId}";
                    var analysis = string.Equals(change.Operation, "delete", StringComparison.Ordinal)
                        ? null
                        : _store.LoadAiAnalysis(change.EntityId);
                    Set(analysis is null
                        ? SearchIndexMutation.Delete(analysisDocumentId)
                        : SearchIndexMutation.Upsert(BuildAnalysisDocument(
                            analysis,
                            defaultLanguage,
                            RequireInstallation(analysis.InstallationId))));
                    break;

                case "screenshot":
                    if (string.IsNullOrWhiteSpace(change.EntityId))
                    {
                        return null;
                    }

                    Set(SearchIndexMutation.Delete($"screenshot:{change.EntityId}"));
                    Set(SearchIndexMutation.Delete($"screenshot-text:{change.EntityId}"));
                    if (!string.Equals(change.Operation, "delete", StringComparison.Ordinal))
                    {
                        var current = _store.GetScreenshotGalleryItem(change.EntityId, cancellationToken);
                        if (current is not null)
                        {
                            Set(SearchIndexMutation.Upsert(BuildScreenshotDocument(current, defaultLanguage)));
                        }
                        else if (_store.LoadScreenshotTextSnapshotByIdentity(change.EntityId) is { } text)
                        {
                            Set(SearchIndexMutation.Upsert(BuildOrphanTextDocument(change.EntityId, null, text, defaultLanguage)));
                        }
                    }
                    break;

                // A capture deletion does not contain the former artifact identities. Rebuild is
                // the only deterministic way to remove every derived document in that case.
                case "capture":
                    if (string.Equals(change.Operation, "delete", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    foreach (var item in _store.GetScreenshotGalleryItemsForCapture(change.EntityId, cancellationToken))
                    {
                        Set(SearchIndexMutation.Upsert(BuildScreenshotDocument(item, defaultLanguage)));
                    }

                    foreach (var pair in _store.LoadScreenshotTextSnapshotsForCapture(change.EntityId, cancellationToken))
                    {
                        if (!mutations.ContainsKey($"screenshot:{pair.Key}"))
                        {
                            Set(SearchIndexMutation.Upsert(BuildOrphanTextDocument(
                                pair.Key,
                                change.EntityId,
                                pair.Value,
                                defaultLanguage)));
                        }
                    }
                    break;

                default:
                    return null;
            }
        }

        return mutations.Values.OrderBy(mutation => mutation.Id, StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<SearchDocument> BuildDocuments(CancellationToken cancellationToken)
    {
        var documents = new List<SearchDocument>();
        var settings = _store.LoadSettings();
        var defaultLanguage = ResolveDefaultLanguage(settings);
        var installationProfiles = _store.GetInstallationProfiles()
            .ToDictionary(profile => profile.InstallationId, StringComparer.Ordinal);
        InstallationProfile RequireInstallation(string installationId) =>
            installationProfiles.TryGetValue(installationId, out var profile)
                ? profile
                : throw new InvalidDataException($"Search source references an unknown installation '{installationId}'.");

        _store.VisitAllActivitySamples((id, sample) => documents.Add(BuildActivityDocument(
            id,
            sample,
            defaultLanguage,
            RequireInstallation(sample.InstallationId))), cancellationToken);

        var screenshotItems = _store.GetAllScreenshotGalleryItems();
        var retainedScreenshotIdentities = screenshotItems
            .Select(item => ScreenshotIdentity(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in screenshotItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            documents.Add(BuildScreenshotDocument(item, defaultLanguage));
        }

        _store.VisitScreenshotTextSnapshots((artifactIdentity, captureId, text) =>
        {
            if (retainedScreenshotIdentities.Contains(artifactIdentity))
            {
                return;
            }

            documents.Add(BuildOrphanTextDocument(artifactIdentity, captureId, text, defaultLanguage));
        }, cancellationToken);

        _store.VisitAllAiAnalyses(analysis => documents.Add(BuildAnalysisDocument(
            analysis,
            defaultLanguage,
            RequireInstallation(analysis.InstallationId))), cancellationToken);
        return documents;
    }

    private static SearchDocument BuildActivityDocument(
        long id,
        ActivitySample sample,
        string defaultLanguage,
        InstallationProfile installation) => new()
    {
        Id = $"activity:{id}",
        Kind = "activity",
        Timestamp = sample.Timestamp,
        Language = defaultLanguage,
        Application = sample.Application,
        ProcessName = sample.ProcessName,
        Context = sample.Context,
        WindowTitle = sample.WindowTitle,
        AttributesRaw = AddInstallationAttributes(
            sample.Attributes?.ToImmutableDictionary(
                    pair => pair.Key,
                    pair => (string?)pair.Value,
                    StringComparer.OrdinalIgnoreCase)
                ?? ImmutableDictionary<string, string?>.Empty,
            installation),
        SpanLabels = sample.Attributes is not null
            && sample.Attributes.TryGetValue(ActivityAttributeKeys.SpanLabel, out var label)
            && !string.IsNullOrWhiteSpace(label)
                ? [label.Trim()]
                : []
    };

    private static SearchDocument BuildScreenshotDocument(
        ScreenshotGalleryItem item,
        string defaultLanguage)
    {
        var text = item.TextSnapshot;
        return new SearchDocument
        {
            Id = $"screenshot:{ScreenshotIdentity(item.Path)}",
            Kind = "screenshot",
            Timestamp = item.CapturedAt,
            Language = text?.Ocr.LanguageTag ?? defaultLanguage,
            Application = item.ForegroundApplication,
            WindowTitle = item.ForegroundWindowTitle,
            AttributesRaw = BuildOcrAttributes(
                null,
                text,
                item.ActivityIndex,
                item.MouseClicks,
                item.CpuUsagePercent,
                item.GpuUsagePercent,
                item.Installation ?? throw new InvalidDataException("Screenshot search source has no installation provenance.")),
            SpanLabels = (item.SpanLabels ?? Array.Empty<ActivityLabelSample>())
                .Select(label => label.Label)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToImmutableArray(),
            CaptureKind = item.CaptureKind,
            CaptureOrigin = item.CaptureOrigin,
            CapturePath = item.Path,
            OcrRawText = text?.Ocr.RawText,
            OcrCorrectedText = text?.AiRefinement?.CorrectedText,
            OcrStructuredSummary = StructuredSummary(text),
            AiDescription = item.AiDescriptionMarkdown
        };
    }

    private static SearchDocument BuildOrphanTextDocument(
        string artifactIdentity,
        string? captureId,
        ScreenshotTextSnapshot text,
        string defaultLanguage) => new()
    {
        Id = $"screenshot-text:{artifactIdentity}",
        Kind = "screenshot-text",
        Timestamp = text.Ocr.ExtractedAt,
        Language = text.AiRefinement?.LanguageTag ?? text.Ocr.LanguageTag ?? defaultLanguage,
        AttributesRaw = BuildOcrAttributes(captureId, text, null, null, null, null, null),
        CapturePath = text.SourceScreenshotPath,
        OcrRawText = text.Ocr.RawText,
        OcrCorrectedText = text.AiRefinement?.CorrectedText,
        OcrStructuredSummary = StructuredSummary(text)
    };

    private static SearchDocument BuildAnalysisDocument(
        AiAnalysis analysis,
        string defaultLanguage,
        InstallationProfile installation) => new()
    {
        Id = $"analysis:{analysis.CorrelationId}",
        Kind = "analysis",
        Timestamp = analysis.Timestamp,
        Language = defaultLanguage,
        Application = analysis.Application,
        Context = analysis.Context,
        CaptureOrigin = analysis.Origin,
        AttributesRaw = AddInstallationAttributes(ImmutableDictionary<string, string?>.Empty, installation),
        AiDescription = analysis.Summary
    };

    private static string ResolveDefaultLanguage(AppSettings settings) =>
        ProductLanguageCatalog.ResolveSearchLanguage(settings.SearchLanguage, CultureInfo.CurrentUICulture);

    private static ImmutableDictionary<string, string?> BuildOcrAttributes(
        string? captureId,
        ScreenshotTextSnapshot? text,
        int? activityIndex,
        long? mouseClicks,
        int? cpuUsagePercent,
        int? gpuUsagePercent,
        InstallationProfile? installation)
    {
        var attributes = ImmutableDictionary.CreateBuilder<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(captureId))
        {
            attributes["capture.id"] = captureId;
        }

        if (activityIndex is not null)
        {
            attributes["activity.index"] = activityIndex.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (mouseClicks is not null)
        {
            attributes[SearchAttributeKeys.MouseClicks] = mouseClicks.Value.ToString(CultureInfo.InvariantCulture);
        }


        if (cpuUsagePercent is not null)
        {
            attributes[SearchAttributeKeys.CpuUsagePercent] = cpuUsagePercent.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (gpuUsagePercent is not null)
        {
            attributes[SearchAttributeKeys.GpuUsagePercent] = gpuUsagePercent.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (installation is not null)
        {
            AddInstallationAttributes(attributes, installation);
        }

        if (text is null)
        {
            return attributes.ToImmutable();
        }

        attributes["ocr.status"] = text.Ocr.Status.ToString();
        attributes["ocr.engine"] = text.Ocr.Engine;
        attributes["ocr.width"] = text.Ocr.PixelWidth is { } width
            ? width.ToString(CultureInfo.InvariantCulture)
            : null;
        attributes["ocr.height"] = text.Ocr.PixelHeight is { } height
            ? height.ToString(CultureInfo.InvariantCulture)
            : null;
        attributes["ocr.angle"] = text.Ocr.TextAngleDegrees?.ToString(CultureInfo.InvariantCulture);
        attributes["ocr.failure"] = text.Ocr.FailureCode;
        return attributes.ToImmutable();
    }

    private static ImmutableDictionary<string, string?> AddInstallationAttributes(
        ImmutableDictionary<string, string?> attributes,
        InstallationProfile installation)
    {
        var builder = attributes.ToBuilder();
        AddInstallationAttributes(builder, installation);
        return builder.ToImmutable();
    }

    private static void AddInstallationAttributes(
        ImmutableDictionary<string, string?>.Builder attributes,
        InstallationProfile installation)
    {
        var validated = InstallationProfileCatalog.ValidatePersisted(installation);
        attributes[SearchAttributeKeys.InstallationId] = validated.InstallationId;
        attributes[SearchAttributeKeys.InstallationFriendlyName] = validated.FriendlyName;
        attributes[SearchAttributeKeys.InstallationMachineName] = validated.MachineName;
        attributes[SearchAttributeKeys.InstallationColor] = validated.Color;
        attributes[SearchAttributeKeys.InstallationIcon] = validated.Icon;
    }

    private static string? StructuredSummary(ScreenshotTextSnapshot? text) =>
        text?.AiRefinement is null
            ? null
            : JsonSerializer.Serialize(text.AiRefinement.Summary);

    private static string ScreenshotIdentity(string path)
    {
        var identity = Path.GetFileNameWithoutExtension(path);
        return identity.EndsWith("-raw", StringComparison.OrdinalIgnoreCase)
            ? identity[..^4]
            : identity;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LocalSearchCoordinator));
        }
    }
}
