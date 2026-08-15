using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using TrackMeUp.Application;
using TrackMeUp.Search;

namespace TrackMeUp.Services;

/// <summary>Builds the mandatory Lucene projection from durable TrackMeUp sources and executes local queries.</summary>
internal sealed class LocalSearchCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan AutomaticRefreshInterval = TimeSpan.FromMinutes(1);
    private readonly LocalStore _store;
    private readonly ILocalSearchService _search;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private string? _indexedSourceStamp;
    private DateTimeOffset _lastIndexedAtUtc = DateTimeOffset.MinValue;
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
        var sourceStamp = _store.GetSearchSourceStamp();
        if (CanUseCurrentIndex(sourceStamp))
        {
            return;
        }

        await _indexGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            sourceStamp = _store.GetSearchSourceStamp();
            if (!CanUseCurrentIndex(sourceStamp))
            {
                _ = await RebuildCurrentSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _indexGate.Release();
        }
    }

    private bool CanUseCurrentIndex(string sourceStamp) =>
        string.Equals(sourceStamp, _indexedSourceStamp, StringComparison.Ordinal)
        || _indexedSourceStamp is not null
        && DateTimeOffset.UtcNow - _lastIndexedAtUtc < AutomaticRefreshInterval;

    private async Task<int> RebuildCurrentSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceStamp = _store.GetSearchSourceStamp();
        var documents = BuildDocuments(cancellationToken);
        await _search.RebuildAsync(documents, cancellationToken).ConfigureAwait(false);

        // Tracking writes continuously, so requiring a byte-for-byte stable database stamp would
        // rebuild forever under normal use. Commit one coherent projection, remember the stamp it
        // started from, and coalesce subsequent mutations into the next bounded refresh window.
        _indexedSourceStamp = sourceStamp;
        _lastIndexedAtUtc = DateTimeOffset.UtcNow;
        return documents.Count;
    }

    private IReadOnlyList<SearchDocument> BuildDocuments(CancellationToken cancellationToken)
    {
        var documents = new List<SearchDocument>();
        var settings = _store.LoadSettings();
        var defaultLanguage = ResolveDefaultLanguage(settings);
        _store.VisitAllActivitySamples((id, sample) => documents.Add(new SearchDocument
        {
            Id = $"activity:{id}",
            Kind = "activity",
            Timestamp = sample.Timestamp,
            Language = defaultLanguage,
            Application = sample.Application,
            ProcessName = sample.ProcessName,
            Context = sample.Context,
            WindowTitle = sample.WindowTitle,
            AttributesRaw = sample.Attributes?.ToImmutableDictionary(
                    pair => pair.Key,
                    pair => (string?)pair.Value,
                    StringComparer.OrdinalIgnoreCase)
                ?? ImmutableDictionary<string, string?>.Empty,
            SpanLabels = sample.Attributes is not null
                && sample.Attributes.TryGetValue(ActivityAttributeKeys.SpanLabel, out var label)
                && !string.IsNullOrWhiteSpace(label)
                    ? [label.Trim()]
                    : []
        }), cancellationToken);

        var screenshotItems = _store.GetAllScreenshotGalleryItems();
        var retainedScreenshotIdentities = screenshotItems
            .Select(item => ScreenshotIdentity(item.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in screenshotItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = item.TextSnapshot;
            documents.Add(new SearchDocument
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
                    item.GpuUsagePercent),
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
            });
        }

        _store.VisitScreenshotTextSnapshots((artifactIdentity, captureId, text) =>
        {
            if (retainedScreenshotIdentities.Contains(artifactIdentity))
            {
                return;
            }

            documents.Add(new SearchDocument
            {
                Id = $"screenshot-text:{artifactIdentity}",
                Kind = "screenshot-text",
                Timestamp = text.Ocr.ExtractedAt,
                Language = text.AiRefinement?.LanguageTag ?? text.Ocr.LanguageTag ?? defaultLanguage,
                AttributesRaw = BuildOcrAttributes(captureId, text, null, null, null, null),
                CapturePath = text.SourceScreenshotPath,
                OcrRawText = text.Ocr.RawText,
                OcrCorrectedText = text.AiRefinement?.CorrectedText,
                OcrStructuredSummary = StructuredSummary(text)
            });
        }, cancellationToken);

        _store.VisitAllAiAnalyses(analysis => documents.Add(new SearchDocument
        {
            Id = $"analysis:{analysis.CorrelationId}",
            Kind = "analysis",
            Timestamp = analysis.Timestamp,
            Language = defaultLanguage,
            Application = analysis.Application,
            Context = analysis.Context,
            CaptureOrigin = analysis.Origin,
            AiDescription = analysis.Summary
        }), cancellationToken);
        return documents;
    }

    private static string ResolveDefaultLanguage(AppSettings settings) =>
        string.Equals(settings.SearchLanguage, "system", StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.CurrentUICulture.Name
            : settings.SearchLanguage;

    private static ImmutableDictionary<string, string?> BuildOcrAttributes(
        string? captureId,
        ScreenshotTextSnapshot? text,
        int? activityIndex,
        long? mouseClicks,
        int? cpuUsagePercent,
        int? gpuUsagePercent)
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
