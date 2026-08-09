using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackMeUp.Ocr;
using TrackMeUp.Runtime;
using TrackMeUp.Search;
using TrackMeUp.Services;

namespace TrackMeUp.Application;

/// <summary>Creates the application facade and keeps concrete infrastructure construction in one composition boundary.</summary>
public static class TrackMeUpApplicationFactory
{
    /// <summary>Creates the in-process runtime application used by the Windows host.</summary>
    public static ITrackMeUpApplication Create(ILoggerFactory? loggerFactory = null, ObservabilityHealth? observability = null)
    {
        var logger = loggerFactory?.CreateLogger<TrackMeUpApplication>() ?? NullLogger<TrackMeUpApplication>.Instance;
        var utilities = new UtilityService();
        var store = new LocalStore();
        var tracking = new TrackingDomainService(store, utilities);
        var capture = new ScreenCaptureService(utilities.GetAppVersion());
        var snapshot = new SystemSnapshotService();
        var deviceContext = new DeviceContextService();
        var buildInformation = new BuildInformationService();
        var aiModelCatalog = AiModelCatalog.LoadDefault();
        var fileShare = new WindowsFileShareService();
        var settings = store.LoadSettings();
        var screenshotOcr = new WindowsScreenshotOcrService(new OcrOptions
        {
            Enabled = settings.OcrEnabled,
            PreferredLanguageTag = string.Equals(settings.OcrLanguage, "system", StringComparison.OrdinalIgnoreCase)
                ? null
                : settings.OcrLanguage
        });
        var ocrRefinement = new OpenAiOcrRefinementService(
            store,
            logger: loggerFactory?.CreateLogger<OpenAiOcrRefinementService>());
        var pricingRefresh = new OpenAiPricingRefreshService(
            store,
            loggerFactory?.CreateLogger<OpenAiPricingRefreshService>());
        var localSearch = CreateLocalSearchService(store);
        var analysis = new OpenAiAnalysisService(
            store,
            capture,
            snapshot,
            deviceContext: deviceContext,
            logger: loggerFactory?.CreateLogger<OpenAiAnalysisService>());
        return new TrackMeUpApplication(
            store,
            utilities,
            tracking,
            capture,
            snapshot,
            analysis,
            new StartupService(),
            buildInformation,
            logger,
            observability,
            deviceContext,
            new ScreenshotShareService(fileShare),
            new WindowStateService(store),
            aiModelCatalog,
            new ApplicationLogService(fileShare: fileShare),
            screenshotOcr,
            ocrRefinement,
            loggerFactory?.CreateLogger<ScreenshotTextExtractionCoordinator>(),
            localSearch,
            pricingRefresh);
    }

    private static ILocalSearchService CreateLocalSearchService(LocalStore store)
    {
        var options = new SearchOptions
        {
            IndexRootPath = store.SearchIndexRootDirectory,
            SynonymSets = SearchSynonymConfiguration.Load(Path.Combine(AppContext.BaseDirectory, "search-synonyms.json"))
        };
        try
        {
            return new LocalSearchService(options);
        }
        catch (InvalidDataException)
        {
            var root = Path.GetFullPath(options.IndexRootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var indexPath = Path.GetFullPath(Path.Combine(root, LocalSearchService.IndexDirectoryName));
            var requiredPrefix = root + Path.DirectorySeparatorChar;
            if (!indexPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The derived search index path escaped its configured root.");
            }

            // A schema-mismatched Lucene directory contains derived data only and is rebuilt from SQLite and screenshots.
            if (Directory.Exists(indexPath))
            {
                Directory.Delete(indexPath, recursive: true);
            }

            return new LocalSearchService(options);
        }
    }
}

/// <summary>Implements UI-independent use cases over the existing local infrastructure services.</summary>
public sealed class TrackMeUpApplication : ITrackMeUpApplication
{
    private readonly LocalStore _store;
    private readonly UtilityService _utilities;
    private readonly TrackingDomainService _tracking;
    private readonly IScreenCaptureService _capture;
    private readonly SystemSnapshotService _snapshot;
    private readonly DeviceContextService _deviceContext;
    private readonly IAiAnalysisService _analysis;
    private readonly IAiOcrRefinementService _ocrRefinement;
    private readonly ScreenshotTextExtractionCoordinator _textExtraction;
    private readonly LocalSearchCoordinator _search;
    private readonly ScreenshotShareService _screenshotShare;
    private readonly ApplicationLogService _applicationLogs;
    private readonly WindowStateService _windowState;
    private readonly StartupService _startup;
    private readonly BuildInformationService _buildInformation;
    private readonly AiModelCatalogSnapshot _aiModelCatalog;
    private readonly ReportAggregationService _reports;
    private readonly OpenAiPricingRefreshService? _pricingRefresh;
    private readonly ILogger<TrackMeUpApplication> _logger;
    private readonly ObservabilityHealth _observability;
    private readonly SemaphoreSlim _mutations = new(1, 1);
    private readonly ConcurrentQueue<ApplicationNotification> _notifications = new();
    private readonly Timer _scheduledSnapshotTimer;
    private DateTimeOffset? _nextScheduledSnapshotAt;
    private TimeSpan? _pausedScheduledSnapshotRemaining;
    private ScreenshotCaptureResult? _pendingManualScreenshotCapture;
    private DateTimeOffset? _pendingManualScreenshotExpiresAt;
    private int _scheduledSnapshotIntervalMinutes;
    private bool _scheduledSnapshotsEnabled;
    private readonly object _activityScoreTelemetryGate = new();
    private DateTimeOffset? _nextActivityScoreTelemetryAt;
    private const int ManualScreenshotDeletionWindowSeconds = 30;
    private const int MaximumPendingNotifications = 32;
    private const string ProductRepositoryUrl = "https://github.com/umbertotechnopreneur/TrackMeUp";
    private const string ProductAuthorUrl = "https://umbertogiacobbi.com";
    private bool _disposed;

    /// <summary>Initializes an application facade with infrastructure owned by the runtime host.</summary>
    public TrackMeUpApplication(
        LocalStore store,
        UtilityService utilities,
        TrackingDomainService tracking,
        IScreenCaptureService capture,
        SystemSnapshotService snapshot,
        IAiAnalysisService analysis,
        StartupService startup,
        BuildInformationService buildInformation,
        ILogger<TrackMeUpApplication>? logger = null,
        ObservabilityHealth? observability = null,
        DeviceContextService? deviceContext = null,
        ScreenshotShareService? screenshotShare = null,
        WindowStateService? windowState = null,
        AiModelCatalog? aiModelCatalog = null,
        ApplicationLogService? applicationLogs = null,
        IScreenshotOcrService? screenshotOcr = null,
        IAiOcrRefinementService? ocrRefinement = null,
        ILogger<ScreenshotTextExtractionCoordinator>? screenshotTextLogger = null,
        ILocalSearchService? localSearch = null,
        OpenAiPricingRefreshService? pricingRefresh = null)
    {
        _store = store;
        _utilities = utilities;
        _tracking = tracking;
        _capture = capture;
        _snapshot = snapshot;
        _deviceContext = deviceContext ?? new DeviceContextService();
        _screenshotShare = screenshotShare ?? new ScreenshotShareService();
        _applicationLogs = applicationLogs ?? new ApplicationLogService();
        _windowState = windowState ?? new WindowStateService(store);
        _analysis = analysis;
        _ocrRefinement = ocrRefinement ?? new OpenAiOcrRefinementService(store);
        _textExtraction = new ScreenshotTextExtractionCoordinator(
            store,
            screenshotOcr ?? new WindowsScreenshotOcrService(new OcrOptions { Enabled = false }),
            screenshotTextLogger);
        _search = new LocalSearchCoordinator(
            store,
            localSearch ?? new LocalSearchService(new SearchOptions { IndexRootPath = store.SearchIndexRootDirectory }));
        _startup = startup;
        _buildInformation = buildInformation;
        _aiModelCatalog = (aiModelCatalog ?? AiModelCatalog.LoadDefault()).Snapshot;
        _reports = new ReportAggregationService(store);
        _pricingRefresh = pricingRefresh;
        _pricingRefresh?.Start();
        _logger = logger ?? NullLogger<TrackMeUpApplication>.Instance;
        _observability = observability ?? new ObservabilityHealth(false, false, "unknown", false);
        _tracking.DashboardStateChanged += OnDashboardStateChanged;
        _tracking.TrackingStateChanged += OnTrackingStateChanged;
        ConfigureScheduledSnapshots(_store.LoadSettings(), restartCountdown: true);
        _scheduledSnapshotTimer = new Timer(HandleScheduledSnapshotTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        _logger.LogInformation("Application facade initialized.");
    }

    /// <inheritdoc />
    public event EventHandler<RuntimeStateChangedEventArgs>? RuntimeStateChanged;

    /// <inheritdoc />
    public Task<OperationResult<RuntimeHealth>> GetRuntimeHealthAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var installationFingerprint = RuntimeProtocol.CreateEndpoint(_store.LoadSettings().InstallationId)
            .PipeName["TrackMeUp.Runtime.".Length..];
        var health = new RuntimeHealth(
            _utilities.GetAppVersion(),
            RuntimeProtocol.ProtocolVersion,
            installationFingerprint,
            true,
            ["tracking", "sessions", "system", "screenshots", "screenshots.save", "screenshots.share", "screenshots.delete", "snapshots.delete", "screenshots.analyze", "ocr", "search", "search.suggest.v1", "search.rebuild.v1", "notifications", "window.state", "ai", "ai.models", "ai.pricing", "ai.pricing.overview", "reports", "reports.query.v1", "privacy", "retention", "plugins", "settings", "startup", "links", "observability", "diagnostics.logs"],
            _observability);
        return Task.FromResult(OperationResult<RuntimeHealth>.Success("runtime.healthy", "RuntimeHealthy", health));
    }

    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> StartTrackingAsync(StartTrackingRequest request, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        if (request.SafeMode)
        {
            return OperationResult<DashboardState>.Failure("tracking.safe_mode", "TrackingBlockedSafeMode", new ValidationIssue("safeMode", "safe_mode", "SafeModeBlocksTracking"));
        }

        ResumeScheduledSnapshots();
        _tracking.Start();
        _logger.LogInformation("Tracking started. SafeMode={SafeMode}", request.SafeMode);
        var state = LoadDashboardState();
        await Task.CompletedTask;
        return OperationResult<DashboardState>.Success("tracking.started", "TrackingStarted", state);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> PauseTrackingAsync(CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        PauseScheduledSnapshots();
        _tracking.Stop();
        _logger.LogInformation("Tracking paused.");
        var state = LoadDashboardState();
        await Task.CompletedTask;
        return OperationResult<DashboardState>.Success("tracking.paused", "TrackingPaused", state);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> ToggleTrackingAsync(CancellationToken cancellationToken) => _tracking.IsTracking
        ? PauseTrackingAsync(cancellationToken)
        : StartTrackingAsync(new StartTrackingRequest(), cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> GetDashboardAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<DashboardState>.Success("dashboard.loaded", "DashboardLoaded", LoadDashboardState()));
    }

    /// <inheritdoc />
    public Task<OperationResult<LastSessionState?>> GetLastSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<LastSessionState?>.Success("session.last.loaded", "LastSessionLoaded", _tracking.LoadLastSessionState()));
    }

    /// <inheritdoc />
    public Task<OperationResult<DailySummary>> GetTodaySummaryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<DailySummary>.Success("session.today.loaded", "TodaySummaryLoaded", _store.GetTodaySummary()));
    }

    /// <inheritdoc />
    public async Task<OperationResult<ReportSnapshot>> GetReportAsync(ReportQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Large SQLite scans run off the presentation/pipe thread and observe cancellation once per row.
        return await Task.Run(() => _reports.Build(query, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult<SystemSnapshot>> CaptureSystemSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var settings = _store.LoadSettings();
            var deviceContext = await _deviceContext.CaptureAsync(settings.IncludeDeviceLocation, cancellationToken);
            var snapshot = _snapshot.Capture();
            var scheduleNote = ActiveHoursSchedule.BuildInformationalNote(settings.ActiveHours, snapshot.Timestamp);
            _tracking.RecordSystemSnapshot(snapshot);
            return OperationResult<SystemSnapshot>.Success(
                "system.snapshot.captured",
                "SystemSnapshotCaptured",
                snapshot with { DeviceContext = deviceContext, InformationalSchedule = scheduleNote });
        }
        catch (Exception exception)
        {
            // OS telemetry can be unavailable; surface a stable failure without leaking host details.
            _logger.LogWarning("System snapshot capture failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return OperationResult<SystemSnapshot>.Failure("system.snapshot.failed", "SystemSnapshotFailed");
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<ScreenshotCaptureResult>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var settings = _store.LoadSettings();
        var mode = request.Mode switch
        {
            null => settings.ScreenshotCaptureMode,
            "all-screens" => "all-screens",
            "active-window" => "active-window",
            _ => null
        };
        if (mode is null)
        {
            return OperationResult<ScreenshotCaptureResult>.Failure(
                "screenshot.mode.invalid",
                "ScreenshotModeUnsupported",
                new ValidationIssue("mode", "unsupported", "ScreenshotModeUnsupported"));
        }

        if (!settings.ScreenshotsEnabled)
        {
            return OperationResult<ScreenshotCaptureResult>.Failure("screenshot.disabled", "ScreenshotsDisabled");
        }

        if (IsCurrentContextPrivate(settings))
        {
            return OperationResult<ScreenshotCaptureResult>.Failure("privacy.blocked", "PrivacyBlocked");
        }

        if (settings.OpenAiEnabled && !request.DeferAiAnalysis)
        {
            if (!TryValidateOpenAiConfiguration(settings, requireImageInput: true, out var validatedSettings, out var validationIssue))
            {
                return OperationResult<ScreenshotCaptureResult>.Failure(
                    "ai.configuration.invalid",
                    "AiConfigurationInvalid",
                    validationIssue!);
            }

            if (!string.Equals(settings.Model, validatedSettings.Model, StringComparison.Ordinal))
            {
                _store.SaveSettings(validatedSettings);
            }

            settings = validatedSettings;
            if (string.IsNullOrWhiteSpace(_store.LoadApiKey(settings.AiApiKeyName)))
            {
                return OperationResult<ScreenshotCaptureResult>.Failure("ai.configuration.invalid", "AiConfigurationInvalid");
            }

            if (!BuildCostGate(settings).Allowed)
            {
                return OperationResult<ScreenshotCaptureResult>.Failure("ai.cost_guardrail", "AiCostGuardrail");
            }
        }

        // Capture happens only after the privacy and enabled-state gates above have succeeded.
        var result = _capture.CaptureByMode(
            settings.ScreenshotDirectory,
            mode,
            request.Watermark && settings.WatermarkScreenshots,
            request.CaptureOrigin);
        result = await _textExtraction.AttachAsync(result, cancellationToken);

        if (settings.OpenAiEnabled && !request.DeferAiAnalysis)
        {
            try
            {
                result = await _ocrRefinement.RefineAsync(result, settings, cancellationToken);
                var analysisOrigin = result.CaptureOrigin == ScreenshotCaptureOrigins.Scheduled
                    ? "snapshot.scheduled"
                    : "snapshot.manual";
                await _analysis.AnalyzeCapturedScreenAsync(
                    _tracking.LatestAnalysisContext,
                    result,
                    request.Keep,
                    analysisOrigin,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return OperationResult<ScreenshotCaptureResult>.Failure("operation.cancelled", "OperationCancelled");
            }
            catch (AiProviderRequestException exception)
            {
                LogAiProviderFailure("screenshot.capture", exception);
                EnqueueAiAnalysisFailure("ai.provider.failed", BuildAiProviderFailureDetail(exception));
                return OperationResult<ScreenshotCaptureResult>.Failure("ai.provider.failed", "AiProviderFailed");
            }
            catch (InvalidOperationException)
            {
                EnqueueAiAnalysisFailure("ai.configuration.invalid");
                return OperationResult<ScreenshotCaptureResult>.Failure("ai.configuration.invalid", "AiConfigurationInvalid");
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Snapshot AI analysis failed. ExceptionType={ExceptionType}", exception.GetType().Name);
                EnqueueAiAnalysisFailure("ai.provider.failed");
                return OperationResult<ScreenshotCaptureResult>.Failure("ai.provider.failed", "AiProviderFailed");
            }
            finally
            {
                // Refinement runs before normal analysis, so Core must still clean raw files if refinement itself fails.
                CleanupCaptureArtifacts(result, request.Keep);
            }
        }
        else if (!request.Keep)
        {
            foreach (var file in result.AllScreenshotPaths.Where(File.Exists))
            {
                File.Delete(file);
            }
        }

        await Task.CompletedTask;
        return OperationResult<ScreenshotCaptureResult>.Success("screenshot.captured", "ScreenshotCaptured", result);
    }, cancellationToken);

    /// <inheritdoc />
    public async Task<OperationResult<PendingManualScreenshotState>> CaptureManualScreenshotAsync(CancellationToken cancellationToken)
    {
        if (GetPendingManualScreenshotState() is not null)
        {
            return OperationResult<PendingManualScreenshotState>.Failure("snapshot.pending.exists", "PendingManualSnapshotExists");
        }

        var capture = await CaptureScreenshotAsync(
            new CaptureScreenshotRequest(
                // The player is foreground when its capture button runs, so active-window would capture TrackMeUp itself.
                Mode: "all-screens",
                Keep: true,
                Watermark: true,
                CaptureOrigin: ScreenshotCaptureOrigins.Manual,
                DeferAiAnalysis: true),
            cancellationToken);
        if (!capture.Succeeded || capture.Value is not { } captured || captured.StoredScreenshotPaths.FirstOrDefault() is not { } screenshotPath)
        {
            return OperationResult<PendingManualScreenshotState>.Failure(capture.Code, capture.MessageKey, capture.Issues.ToArray());
        }

        return await MutateAsync(async () =>
        {
            _pendingManualScreenshotCapture = captured;
            _pendingManualScreenshotExpiresAt = DateTimeOffset.Now.AddSeconds(ManualScreenshotDeletionWindowSeconds);
            var pending = new PendingManualScreenshotState(screenshotPath, _pendingManualScreenshotExpiresAt.Value);
            await Task.CompletedTask;
            return OperationResult<PendingManualScreenshotState>.Success("snapshot.pending.created", "PendingManualSnapshotCreated", pending);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OperationResult<bool>> DeletePendingManualScreenshotAsync(CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        if (_pendingManualScreenshotCapture is not { } capture || GetPendingManualScreenshotState() is null)
        {
            return OperationResult<bool>.Failure("snapshot.pending.not_found", "PendingManualSnapshotNotFound");
        }

        try
        {
            foreach (var artifact in capture.AllScreenshotPaths.Where(File.Exists))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(artifact);
            }
        }
        catch (IOException)
        {
            return OperationResult<bool>.Failure("screenshot.delete.failed", "ScreenshotDeleteFailed");
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult<bool>.Failure("screenshot.delete.failed", "ScreenshotDeleteFailed");
        }

        foreach (var sourcePath in (capture.TextSnapshots ?? Array.Empty<ScreenshotTextSnapshot>())
                     .Select(snapshot => snapshot.SourceScreenshotPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _store.DeleteScreenshotTextSnapshot(sourcePath);
        }

        _pendingManualScreenshotCapture = null;
        _pendingManualScreenshotExpiresAt = null;
        await Task.CompletedTask;
        return OperationResult<bool>.Success("snapshot.pending.deleted", "PendingManualSnapshotDeleted", true);
    }, cancellationToken);

    /// <inheritdoc />
    public async Task<OperationResult<AiAnalysis>> AnalyzeCapturedScreenshotAsync(AnalyzeCapturedScreenshotRequest request, CancellationToken cancellationToken)
    {
        var aiEnabledForAnalysis = false;
        string? aiFailureDetail = null;
        var operation = await MutateAsync(async () =>
        {
            if (request.Capture is null)
            {
                return OperationResult<AiAnalysis>.Failure("ai.capture.invalid", "AiConfigurationInvalid");
            }

            var settings = _store.LoadSettings();
            if (!settings.OpenAiEnabled)
            {
                return OperationResult<AiAnalysis>.Failure("ai.disabled", "AiDisabled");
            }

            aiEnabledForAnalysis = true;

            if (IsCurrentContextPrivate(settings))
            {
                return OperationResult<AiAnalysis>.Failure("privacy.blocked", "PrivacyBlocked");
            }

            if (!TryValidateOpenAiConfiguration(settings, requireImageInput: true, out var validatedSettings, out var validationIssue))
            {
                return OperationResult<AiAnalysis>.Failure(
                    "ai.configuration.invalid",
                    "AiConfigurationInvalid",
                    validationIssue!);
            }

            if (!string.Equals(settings.Model, validatedSettings.Model, StringComparison.Ordinal))
            {
                _store.SaveSettings(validatedSettings);
            }

            settings = validatedSettings;
            var gate = BuildCostGate(settings);
            if (!gate.Allowed)
            {
                return OperationResult<AiAnalysis>.Failure("ai.cost_guardrail", "AiCostGuardrail");
            }

            try
            {
                var origin = NormalizeAnalysisOrigin(request.Origin);
                var refinedCapture = await _ocrRefinement.RefineAsync(request.Capture, settings, cancellationToken);
                var result = await _analysis.AnalyzeCapturedScreenAsync(
                    _tracking.LatestAnalysisContext,
                    refinedCapture,
                    request.KeepCapture,
                    origin,
                    cancellationToken);
                return OperationResult<AiAnalysis>.Success("ai.analyzed", "AiAnalyzed", result);
            }
            catch (OperationCanceledException)
            {
                return OperationResult<AiAnalysis>.Failure("operation.cancelled", "OperationCancelled");
            }
            catch (AiProviderRequestException exception)
            {
                LogAiProviderFailure("screenshot.analyze", exception);
                aiFailureDetail = BuildAiProviderFailureDetail(exception);
                return OperationResult<AiAnalysis>.Failure("ai.provider.failed", "AiProviderFailed");
            }
            catch (InvalidOperationException)
            {
                return OperationResult<AiAnalysis>.Failure("ai.configuration.invalid", "AiConfigurationInvalid");
            }
            catch (Exception exception)
            {
                // Provider/network errors remain stable to callers; the capture service owns cleanup behavior.
                _logger.LogWarning("Deferred snapshot AI analysis failed. ExceptionType={ExceptionType}", exception.GetType().Name);
                return OperationResult<AiAnalysis>.Failure("ai.provider.failed", "AiProviderFailed");
            }
            finally
            {
                // Refinement may fail before the analysis service assumes cleanup ownership.
                CleanupCaptureArtifacts(request.Capture, request.KeepCapture);
            }
        }, cancellationToken);

        if (!operation.Succeeded && aiEnabledForAnalysis && ShouldNotifyAiAnalysisFailure(operation.Code))
        {
            // The queue is the cross-process fallback: a connected UI drains it, while headless runtimes only retain bounded notices.
            EnqueueAiAnalysisFailure(operation.Code, aiFailureDetail);
        }

        return operation;
    }

    /// <inheritdoc />
    public async Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _search.SearchAsync(request, cancellationToken).ConfigureAwait(false);
            return OperationResult<SearchResponse>.Success("search.completed", "SearchCompleted", response);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<SearchResponse>.Failure("operation.cancelled", "OperationCancelled");
        }
        catch (ArgumentException)
        {
            return OperationResult<SearchResponse>.Failure(
                "search.query.invalid",
                "SearchQueryInvalid",
                new ValidationIssue("query", "invalid", "SearchQueryInvalid"));
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Local search failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return OperationResult<SearchResponse>.Failure("search.failed", "SearchFailed");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<string>>> GetSearchSuggestionsAsync(
        SearchSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var suggestions = await _search.SuggestAsync(request, cancellationToken).ConfigureAwait(false);
            return OperationResult<IReadOnlyList<string>>.Success(
                "search.suggestions.completed",
                "SearchSuggestionsCompleted",
                suggestions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return OperationResult<IReadOnlyList<string>>.Failure(
                "search.suggestions.invalid",
                "SearchQueryInvalid",
                new ValidationIssue("query", "invalid", "SearchQueryInvalid"));
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Local search suggestions failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return OperationResult<IReadOnlyList<string>>.Failure("search.suggestions.failed", "SearchFailed");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult<int>> RebuildSearchIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _search.RebuildAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult<int>.Success("search.index.rebuilt", "SearchIndexRebuilt", count);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<int>.Failure("operation.cancelled", "OperationCancelled");
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Local search index rebuild failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return OperationResult<int>.Failure("search.index.failed", "SearchIndexFailed");
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<ApplicationNotification>>> DrainApplicationNotificationsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var drained = new List<ApplicationNotification>(MaximumPendingNotifications);
        while (drained.Count < MaximumPendingNotifications && _notifications.TryDequeue(out var notification))
        {
            drained.Add(notification);
        }

        return Task.FromResult(OperationResult<IReadOnlyList<ApplicationNotification>>.Success(
            "notifications.drained",
            "ApplicationNotificationsDrained",
            drained));
    }

    /// <inheritdoc />
    public Task<OperationResult<string?>> GetLatestScreenshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<string?>.Success("screenshot.latest.loaded", "LatestScreenshotLoaded", _store.LoadLatestPrimaryScreenshot()));
    }

    /// <inheritdoc />
    public Task<OperationResult<string>> DeleteScreenshotAsync(string screenshotPath, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifacts = _store.FindScreenshotArtifacts(screenshotPath);
        if (artifacts.Count == 0)
        {
            return OperationResult<string>.Failure("screenshot.not_found", "ScreenshotNotFound", new ValidationIssue("screenshotPath", "not_found", "ScreenshotNotFound"));
        }

        try
        {
            foreach (var artifact in artifacts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(artifact);
            }
        }
        catch (IOException)
        {
            // A locked or unavailable artifact is reported without hiding a partial deletion from the caller.
            return OperationResult<string>.Failure("screenshot.delete.failed", "ScreenshotDeleteFailed");
        }
        catch (UnauthorizedAccessException)
        {
            // Permission failures remain explicit so the user can retry after the file becomes writable.
            return OperationResult<string>.Failure("screenshot.delete.failed", "ScreenshotDeleteFailed");
        }

        _store.DeleteScreenshotTextSnapshot(screenshotPath);

        await Task.CompletedTask;
        return OperationResult<string>.Success("screenshot.deleted", "ScreenshotDeleted", screenshotPath);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<string>> DeleteSnapshotAsync(string screenshotPath, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ScreenCaptureService.IsOwnedArtifact(screenshotPath) || !Path.IsPathFullyQualified(screenshotPath))
        {
            return OperationResult<string>.Failure("snapshot.invalid", "SnapshotInvalid", new ValidationIssue("screenshotPath", "invalid", "SnapshotInvalid"));
        }

        var deletedCount = checked(
            _store.DeleteAiAnalysesReferencingScreenshot(screenshotPath)
            + _store.DeleteScreenshotTextSnapshot(screenshotPath));
        if (deletedCount == 0)
        {
            return OperationResult<string>.Failure("snapshot.not_found", "SnapshotNotFound", new ValidationIssue("screenshotPath", "not_found", "SnapshotNotFound"));
        }

        await Task.CompletedTask;
        return OperationResult<string>.Success("snapshot.deleted", "SnapshotDeleted", screenshotPath);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<ScreenshotGallery>> GetScreenshotGalleryAsync(DateOnly date, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<ScreenshotGallery>.Success(
            "screenshot.gallery.loaded",
            "ScreenshotGalleryLoaded",
            _store.GetScreenshotGallery(date)));
    }

    /// <inheritdoc />
    public Task<OperationResult<ScreenshotGallery>> GetLatestScreenshotGalleryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<ScreenshotGallery>.Success(
            "screenshot.gallery.latest.loaded",
            "LatestScreenshotGalleryLoaded",
            _store.GetLatestScreenshotGallery()));
    }

    /// <inheritdoc />
    public Task<OperationResult<string>> SaveScreenshotAsync(string screenshotPath, string destinationPath, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ScreenCaptureService.IsOwnedArtifact(screenshotPath) || !File.Exists(screenshotPath))
        {
            return OperationResult<string>.Failure("screenshot.invalid", "ScreenshotInvalid");
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return OperationResult<string>.Failure("screenshot.destination.invalid", "ScreenshotDestinationInvalid");
        }

        var destination = Path.GetFullPath(destinationPath);
        File.Copy(screenshotPath, destination, overwrite: true);
        await Task.CompletedTask;
        return OperationResult<string>.Success("screenshot.saved", "ScreenshotSaved", destination);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<string>> ShareScreenshotAsync(string screenshotPath, long windowHandle, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ScreenCaptureService.IsOwnedArtifact(screenshotPath) || !File.Exists(screenshotPath))
        {
            return OperationResult<string>.Failure("screenshot.invalid", "ScreenshotInvalid");
        }

        if (windowHandle == 0)
        {
            return OperationResult<string>.Failure("screenshot.share.window.invalid", "ScreenshotShareWindowInvalid");
        }

        var result = _screenshotShare.Share(screenshotPath, new IntPtr(windowHandle));
        await Task.CompletedTask;
        return OperationResult<string>.Success("screenshot.share.opened", "ScreenshotShareOpened", result);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<bool>> OpenApplicationLogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _ = _applicationLogs.OpenLatestLog();
            _logger.LogInformation("Application log opened through the diagnostics facade.");
            return Task.FromResult(OperationResult<bool>.Success("diagnostics.log.opened", "ApplicationLogOpened", true));
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                InvalidOperationException or
                NotSupportedException or
                System.Runtime.InteropServices.COMException or
                System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning("Application log could not be opened. ExceptionType={ExceptionType}", exception.GetType().Name);
            return Task.FromResult(OperationResult<bool>.Failure("diagnostics.log.unavailable", "ApplicationLogUnavailable"));
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<bool>> ShareApplicationLogAsync(long windowHandle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (windowHandle == 0)
        {
            return Task.FromResult(OperationResult<bool>.Failure("diagnostics.log.share.window.invalid", "ApplicationLogShareWindowInvalid"));
        }

        try
        {
            _ = _applicationLogs.ShareLatestRedactedLog(new IntPtr(windowHandle));
            _logger.LogInformation("Redacted application log prepared for sharing.");
            return Task.FromResult(OperationResult<bool>.Success("diagnostics.log.share.opened", "ApplicationLogShareOpened", true));
        }
        catch (Exception exception) when (
            exception is IOException or
                UnauthorizedAccessException or
                InvalidOperationException or
                ArgumentException or
                NotSupportedException or
                System.Runtime.InteropServices.COMException or
                System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning("Redacted application log could not be shared. ExceptionType={ExceptionType}", exception.GetType().Name);
            return Task.FromResult(OperationResult<bool>.Failure("diagnostics.log.unavailable", "ApplicationLogUnavailable"));
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<string>> OpenScreenshotFolderAsync(CancellationToken cancellationToken) => OpenFolderAsync(_store.LoadSettings().ScreenshotDirectory, "screenshot.folder.opened", "ScreenshotFolderOpened", cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<string>> OpenScreenshotFolderAsync(string directory, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(directory)
            ? Task.FromResult(OperationResult<string>.Failure("screenshot.folder.path.required", "ScreenshotFolderPathRequired", new ValidationIssue("directory", "required", "ScreenshotFolderPathRequired")))
            : OpenFolderAsync(directory, "screenshot.folder.opened", "ScreenshotFolderOpened", cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<AiStatus>> GetAiStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<AiStatus>.Success("ai.status.loaded", "AiStatusLoaded", BuildAiStatus()));
    }

    /// <inheritdoc />
    public async Task<OperationResult<AiPricingOverview>> GetAiPricingOverviewAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() =>
        {
            var settings = _store.LoadSettings();
            if (!settings.OpenAiEnabled ||
                !string.Equals(settings.AiProvider, AiPricingProviders.OpenAi, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult<AiPricingOverview>.Failure(
                    "ai.pricing.disabled",
                    "AiPricingDisabled",
                    new ValidationIssue("ai.provider", "openai_required", "AiPricingOpenAiRequired"));
            }

            var prices = _store.ListAiModelPricing(AiPricingProviders.OpenAi);
            var displayedRows = prices
                .Where(price =>
                    string.Equals(price.ServiceTier, AiPricingServiceTiers.Standard, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(price.ContextWindow, AiPricingContextWindows.Short, StringComparison.OrdinalIgnoreCase))
                .GroupBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(price => price.Model, StringComparer.OrdinalIgnoreCase)
                .Select(price => new AiPricingCostRow(
                    price.Model,
                    price.InputUsdPerMillionTokens,
                    price.OutputUsdPerMillionTokens))
                .ToArray();

            var today = DateOnly.FromDateTime(DateTime.Now);
            var report = _reports.Build(new ReportQuery(today, today, TimeZoneInfo.Local.Id), cancellationToken);
            if (!report.Succeeded || report.Value is null)
            {
                return OperationResult<AiPricingOverview>.Failure(
                    report.Code,
                    report.MessageKey,
                    report.Issues.ToArray());
            }

            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            var monthReport = _reports.Build(new ReportQuery(monthStart, today, TimeZoneInfo.Local.Id), cancellationToken);
            if (!monthReport.Succeeded || monthReport.Value is null)
            {
                return OperationResult<AiPricingOverview>.Failure(
                    monthReport.Code,
                    monthReport.MessageKey,
                    monthReport.Issues.ToArray());
            }

            var usage = report.Value.AiUsage;
            var monthUsage = monthReport.Value.AiUsage;
            var overview = new AiPricingOverview(
                _store.GetLatestAiModelPricingRetrievedAt(AiPricingProviders.OpenAi),
                prices.Count,
                displayedRows.Length,
                usage.EstimatedCostUsd,
                usage.EstimatedCostRequestCount,
                usage.ActualCostUsd,
                usage.ActualCostRequestCount,
                usage.InputTokens,
                usage.OutputTokens,
                usage.TotalTokens,
                monthStart,
                monthEnd,
                monthUsage.EstimatedCostUsd,
                monthUsage.ActualCostUsd,
                displayedRows);
            return OperationResult<AiPricingOverview>.Success("ai.pricing.loaded", "AiPricingLoaded", overview);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OperationResult<AiConnectionTestResult>> TestAiConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = _store.LoadSettings();
        if (!TryValidateOpenAiConfiguration(settings, requireImageInput: false, out var validatedSettings, out var validationIssue))
        {
            return OperationResult<AiConnectionTestResult>.Failure("ai.connection.configuration.invalid", "AiConnectionTestConfigurationInvalid", validationIssue!);
        }

        var apiKey = _store.LoadApiKey(validatedSettings.AiApiKeyName);
        if (!AiApiKeyPolicy.LooksPlausible(validatedSettings.AiProvider, validatedSettings.AiApiKeyName, apiKey))
        {
            return OperationResult<AiConnectionTestResult>.Failure(
                "ai.connection.key.missing",
                "AiConnectionTestKeyMissing",
                new ValidationIssue("ai.key_variable", "required", "AiConnectionTestKeyMissing"));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // This intentionally uses a tiny text-only request: no screen or device data leaves this PC during a connection check.
            var providerResult = await AIDecoderFactory.Create(validatedSettings).DecodeAsync(
                AiConnectionTestProtocol.Prompt,
                Array.Empty<string>(),
                validatedSettings,
                apiKey!,
                Guid.NewGuid().ToString("N"),
                cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return OperationResult<AiConnectionTestResult>.Success(
                "ai.connection.succeeded",
                "AiConnectionTestSucceeded",
                new AiConnectionTestResult(validatedSettings.AiProvider, validatedSettings.Model, providerResult.Text, stopwatch.ElapsedMilliseconds));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AiProviderRequestException exception)
        {
            stopwatch.Stop();
            _logger.LogWarning("AI connection test failed. Provider={Provider} Model={Model} FailureCategory={FailureCategory} HttpStatus={HttpStatus}", validatedSettings.AiProvider, validatedSettings.Model, exception.Failure.FailureCode, exception.Failure.HttpStatusCode);
            return OperationResult<AiConnectionTestResult>.Failure("ai.connection.failed", "AiConnectionTestFailed");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            _logger.LogWarning("AI connection test failed unexpectedly. Provider={Provider} Model={Model} ExceptionType={ExceptionType}", validatedSettings.AiProvider, validatedSettings.Model, exception.GetType().Name);
            return OperationResult<AiConnectionTestResult>.Failure("ai.connection.failed", "AiConnectionTestFailed");
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<AiModelCatalogSnapshot>> GetAiModelCatalogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<AiModelCatalogSnapshot>.Success("ai.models.loaded", "AiModelsLoaded", _aiModelCatalog));
    }

    /// <inheritdoc />
    public Task<OperationResult<AiStatus>> SetAiEnabledAsync(bool enabled, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var settings = _store.LoadSettings() with { OpenAiEnabled = enabled };
        var validatedSettings = settings;
        if (enabled
            && !TryValidateOpenAiConfiguration(settings, requireImageInput: false, out validatedSettings, out var validationIssue))
        {
            return OperationResult<AiStatus>.Failure("ai.configuration.invalid", "AiConfigurationInvalid", validationIssue!);
        }

        _store.SaveSettings(validatedSettings);
        await Task.CompletedTask;
        return OperationResult<AiStatus>.Success(enabled ? "ai.enabled" : "ai.disabled", enabled ? "AiEnabled" : "AiDisabled", BuildAiStatus());
    }, cancellationToken);

    /// <inheritdoc />
    public async Task<OperationResult<AppSettings>> ConfigureAiAsync(SettingsPatch patch, CancellationToken cancellationToken)
    {
        var aiKeys = SettingsCatalog.Definitions
            .Select(definition => definition.Key)
            .Where(key => key.StartsWith("ai.", StringComparison.OrdinalIgnoreCase) && !key.Equals("ai.key_variable", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values = patch.Values
            .Where(pair => aiKeys.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        return await PatchSettingsAsync(new SettingsPatch(values), cancellationToken);
    }

    /// <inheritdoc />
    public Task<OperationResult<string>> SetAiKeyAsync(string keyVariable, string secret, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var normalizedKeyVariable = keyVariable ?? string.Empty;
        var secretValue = secret ?? string.Empty;
        if (!SettingsCatalog.IsAllowedApiKeyVariable(normalizedKeyVariable)
            || !AiApiKeyPolicy.LooksPlausibleForVariable(normalizedKeyVariable, secretValue))
        {
            return OperationResult<string>.Failure("ai.key.invalid", "AiKeyInvalid", new ValidationIssue("key", "invalid", "AiKeyInvalid"));
        }

        // The secret is immediately delegated to the user environment store and never persisted or logged.
        _utilities.SetApiKey(normalizedKeyVariable, secretValue);
        secretValue = string.Empty;
        var settings = _store.LoadSettings();
        _store.SaveSettings(settings with { AiApiKeyName = normalizedKeyVariable });
        await Task.CompletedTask;
        return OperationResult<string>.Success("ai.key.stored", "AiKeyStored", normalizedKeyVariable);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<AiAnalysis>> AnalyzeCurrentActivityAsync(AnalyzeCurrentActivityRequest request, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var settings = _store.LoadSettings();
        if (!settings.OpenAiEnabled)
        {
            return OperationResult<AiAnalysis>.Failure("ai.disabled", "AiDisabled");
        }

        if (IsCurrentContextPrivate(settings))
        {
            return OperationResult<AiAnalysis>.Failure("privacy.blocked", "PrivacyBlocked");
        }

        if (!TryValidateOpenAiConfiguration(
                settings,
                request.AllowCapture && settings.ScreenshotsEnabled,
                out var validatedSettings,
                out var validationIssue))
        {
            return OperationResult<AiAnalysis>.Failure(
                "ai.configuration.invalid",
                "AiConfigurationInvalid",
                validationIssue!);
        }

        if (!string.Equals(settings.Model, validatedSettings.Model, StringComparison.Ordinal))
        {
            _store.SaveSettings(validatedSettings);
        }

        settings = validatedSettings;

        var gate = BuildCostGate(settings);
        if (!gate.Allowed)
        {
            return OperationResult<AiAnalysis>.Failure("ai.cost_guardrail", "AiCostGuardrail");
        }

        try
        {
            var origin = NormalizeAnalysisOrigin(request.Origin);
            ScreenshotCaptureResult? capture = null;
            try
            {
                AiAnalysis result;
                if (request.AllowCapture && settings.ScreenshotsEnabled)
                {
                    capture = _capture.CaptureByMode(
                        settings.ScreenshotDirectory,
                        settings.ScreenshotCaptureMode,
                        settings.WatermarkScreenshots,
                        string.Equals(origin, "snapshot.scheduled", StringComparison.Ordinal)
                            ? ScreenshotCaptureOrigins.Scheduled
                            : ScreenshotCaptureOrigins.Manual);
                    capture = await _textExtraction.AttachAsync(capture, cancellationToken);
                    capture = await _ocrRefinement.RefineAsync(capture, settings, cancellationToken);
                    result = await _analysis.AnalyzeCapturedScreenAsync(
                        _tracking.LatestAnalysisContext,
                        capture,
                        settings.KeepScreenshots,
                        origin,
                        cancellationToken);
                }
                else
                {
                    result = await _analysis.AnalyzeCurrentScreenAsync(
                        _tracking.LatestAnalysisContext,
                        allowCapture: false,
                        origin,
                        cancellationToken);
                }

                return OperationResult<AiAnalysis>.Success("ai.analyzed", "AiAnalyzed", result);
            }
            finally
            {
                if (capture is not null)
                {
                    CleanupCaptureArtifacts(capture, settings.KeepScreenshots);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return OperationResult<AiAnalysis>.Failure("operation.cancelled", "OperationCancelled");
        }
        catch (AiProviderRequestException exception)
        {
            LogAiProviderFailure("ai.analyze", exception);
            return OperationResult<AiAnalysis>.Failure("ai.provider.failed", "AiProviderFailed");
        }
        catch (InvalidOperationException)
        {
            return OperationResult<AiAnalysis>.Failure("ai.configuration.invalid", "AiConfigurationInvalid");
        }
        catch (Exception exception)
        {
            // Provider/network errors are intentionally not serialized into diagnostics or IPC payloads.
            _logger.LogWarning("AI analysis failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return OperationResult<AiAnalysis>.Failure("ai.provider.failed", "AiProviderFailed");
        }
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<string>> GenerateTodayReportAsync(string? outputDirectory, bool open, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var report = new HtmlReportService(_store, _utilities).ExportToday();
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            var target = Path.Combine(_utilities.NormalizeDirectory(outputDirectory), Path.GetFileName(report));
            File.Copy(report, target, true);
            report = target;
        }

        if (open)
        {
            Process.Start(new ProcessStartInfo { FileName = report, UseShellExecute = true });
        }

        await Task.CompletedTask;
        return OperationResult<string>.Success("report.today.generated", "TodayReportGenerated", report);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<string>> GenerateDailyDigestAsync(DateOnly date, bool open, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var report = new HtmlReportService(_store, _utilities).ExportDailyDigest(date);
        var settings = _store.LoadSettings() with { LastDailyDigestDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        _store.SaveSettings(settings);
        if (open)
        {
            Process.Start(new ProcessStartInfo { FileName = report, UseShellExecute = true });
        }

        await Task.CompletedTask;
        return OperationResult<string>.Success("report.digest.generated", "DailyDigestGenerated", report);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<string>> OpenReportsFolderAsync(CancellationToken cancellationToken) => OpenFolderAsync(_utilities.ReportsDirectory, "report.folder.opened", "ReportFolderOpened", cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<string>> OpenUserInterfaceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var executable = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "TrackMeUp.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--ui");
            Process.Start(startInfo);
            return Task.FromResult(OperationResult<string>.Success("ui.opened", "UiOpened", "TrackMeUp UI"));
        }
        catch (Exception exception)
        {
            _logger.LogWarning("WinUI frontend launch failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            return Task.FromResult(OperationResult<string>.Failure("shell.open.failed", "UiOpenFailed"));
        }
    }

    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<PrivacyRule>>> GetPrivacyRulesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<IReadOnlyList<PrivacyRule>>.Success("privacy.list.loaded", "PrivacyRulesLoaded", ReadPrivacyRules(_store.LoadSettings())));
    }

    /// <inheritdoc />
    public Task<OperationResult<PrivacyRule>> AddPrivacyRuleAsync(string type, string value, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        if (type is not ("process" or "title" or "hint") || string.IsNullOrWhiteSpace(value))
        {
            return OperationResult<PrivacyRule>.Failure("privacy.rule.invalid", "PrivacyRuleInvalid", new ValidationIssue("rule", "invalid", "PrivacyRuleInvalid"));
        }

        var rule = new PrivacyRule(Guid.NewGuid().ToString("N"), type, value.Trim());
        var all = ReadPrivacyRules(_store.LoadSettings()).Append(rule).ToArray();
        SavePrivacyRules(_store.LoadSettings(), all);
        await Task.CompletedTask;
        return OperationResult<PrivacyRule>.Success("privacy.rule.added", "PrivacyRuleAdded", rule);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<bool>> RemovePrivacyRuleAsync(string id, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var rules = ReadPrivacyRules(_store.LoadSettings());
        var filtered = rules.Where(x => !string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (filtered.Length == rules.Count)
        {
            return OperationResult<bool>.Failure("privacy.rule.not_found", "PrivacyRuleNotFound", new ValidationIssue("id", "not_found", "PrivacyRuleNotFound"));
        }

        SavePrivacyRules(_store.LoadSettings(), filtered);
        await Task.CompletedTask;
        return OperationResult<bool>.Success("privacy.rule.removed", "PrivacyRuleRemoved", true);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<bool>> TestCurrentPrivacyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<bool>.Success("privacy.test.completed", "PrivacyTestCompleted", IsCurrentContextPrivate(_store.LoadSettings())));
    }

    /// <inheritdoc />
    public Task<OperationResult<RetentionStatus>> GetRetentionStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = _store.LoadSettings();
        return Task.FromResult(OperationResult<RetentionStatus>.Success("retention.status.loaded", "RetentionStatusLoaded", new RetentionStatus(settings.DataRetentionDays, settings.ScreenshotRetentionDays, settings.ScreenshotDirectory)));
    }

    /// <inheritdoc />
    public Task<OperationResult<RetentionPreview>> PreviewRetentionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<RetentionPreview>.Success("retention.preview.loaded", "RetentionPreviewLoaded", BuildRetentionPreview()));
    }

    /// <inheritdoc />
    public Task<OperationResult<RetentionPreview>> RunRetentionAsync(RetentionRequest request, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        if (!request.Execute || !request.Confirmed)
        {
            return OperationResult<RetentionPreview>.Failure("retention.confirmation.required", "RetentionConfirmationRequired", new ValidationIssue("confirmation", "required", "RetentionConfirmationRequired"));
        }

        var preview = BuildRetentionPreview();
        foreach (var path in preview.Paths.Where(ScreenCaptureService.IsOwnedArtifact))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(path);
            _store.DeleteScreenshotTextSnapshot(path);
        }

        var settings = _store.LoadSettings();
        _store.ApplyRetention(DateTimeOffset.Now.AddDays(-settings.DataRetentionDays));

        await Task.CompletedTask;
        return OperationResult<RetentionPreview>.Success("retention.completed", "RetentionCompleted", preview);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<PluginInfo>>> GetPluginsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<IReadOnlyList<PluginInfo>>.Success("plugins.list.loaded", "PluginsLoaded", BuildPlugins(_store.LoadSettings())));
    }

    /// <inheritdoc />
    public Task<OperationResult<PluginInfo>> GetPluginAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plugin = BuildPlugins(_store.LoadSettings()).SingleOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(plugin is null
            ? OperationResult<PluginInfo>.Failure("plugins.not_found", "PluginNotFound", new ValidationIssue("id", "not_found", "PluginNotFound"))
            : OperationResult<PluginInfo>.Success("plugins.loaded", "PluginLoaded", plugin));
    }

    /// <inheritdoc />
    public Task<OperationResult<PluginInfo>> SetPluginEnabledAsync(string id, bool enabled, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var settings = _store.LoadSettings();
        var updated = id.ToLowerInvariant() switch
        {
            "word" => settings with { EnableWordDetailPlugin = enabled },
            "excel" => settings with { EnableExcelDetailPlugin = enabled },
            "vscode" => settings with { EnableVsCodeDetailPlugin = enabled },
            "browser" => settings with { EnableBrowserDetailPlugin = enabled },
            _ => null
        };
        if (updated is null)
        {
            return OperationResult<PluginInfo>.Failure("plugins.not_found", "PluginNotFound", new ValidationIssue("id", "not_found", "PluginNotFound"));
        }

        _store.SaveSettings(updated);
        var plugin = BuildPlugins(updated).Single(x => x.Id == id.ToLowerInvariant());
        await Task.CompletedTask;
        return OperationResult<PluginInfo>.Success(enabled ? "plugins.enabled" : "plugins.disabled", enabled ? "PluginEnabled" : "PluginDisabled", plugin);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<AppSettings>.Success("settings.loaded", "SettingsLoaded", _store.LoadSettings()));
    }

    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> PatchSettingsAsync(SettingsPatch patch, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var settings = _store.LoadSettings();
        var validation = SettingsCatalog.Apply(settings, patch);
        if (!validation.Succeeded || validation.Value is null)
        {
            return validation;
        }

        var current = validation.Value;
        if (!TryValidateOpenAiConfiguration(current, requireImageInput: false, out var validatedSettings, out var validationIssue))
        {
            return OperationResult<AppSettings>.Failure(
                "settings.validation.failed",
                "SettingsValidationFailed",
                validationIssue!);
        }

        current = validatedSettings;
        var startupChanged = current.StartWithWindows != settings.StartWithWindows;
        if (startupChanged && !_startup.SetEnabled(current.StartWithWindows))
        {
            return OperationResult<AppSettings>.Failure(
                "startup.update.failed",
                "StartupUpdateFailed",
                new ValidationIssue("startup.enabled", "os_update_failed", "StartupUpdateFailed"));
        }

        try
        {
            _store.SaveSettings(current);
        }
        catch
        {
            if (startupChanged && !_startup.SetEnabled(settings.StartWithWindows))
            {
                _logger.LogError("Startup state rollback failed after settings persistence error.");
            }

            throw;
        }

        if (current.StartWithWindows != settings.StartWithWindows)
        {
            _logger.LogInformation("Windows startup state updated. Enabled={Enabled}", current.StartWithWindows);
        }
        ConfigureScheduledSnapshots(current, restartCountdown: current.ScreenshotIntervalMinutes != settings.ScreenshotIntervalMinutes);
        await Task.CompletedTask;
        return OperationResult<AppSettings>.Success("settings.saved", "SettingsSaved", current);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<WindowState?>> RestoreWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var state = _windowState.Restore(windowKey, windowHandle);
        await Task.CompletedTask;
        return OperationResult<WindowState?>.Success(
            state is null ? "window.state.not_found" : "window.state.restored",
            state is null ? "WindowStateNotFound" : "WindowStateRestored",
            state);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<WindowState>> SaveWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var state = _windowState.Save(windowKey, windowHandle);
        await Task.CompletedTask;
        return OperationResult<WindowState>.Success("window.state.saved", "WindowStateSaved", state);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<bool>> GetStartupStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<bool>.Success("startup.status.loaded", "StartupStatusLoaded", _startup.IsEnabled()));
    }

    /// <inheritdoc />
    public Task<OperationResult<bool>> SetStartupEnabledAsync(bool enabled, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var settings = _store.LoadSettings();
        var success = _startup.SetEnabled(enabled);
        if (!success)
        {
            return OperationResult<bool>.Failure("startup.failed", "StartupUpdateFailed");
        }

        try
        {
            _store.SaveSettings(settings with { StartWithWindows = enabled });
        }
        catch
        {
            if (!_startup.SetEnabled(settings.StartWithWindows))
            {
                _logger.LogError("Startup state rollback failed after settings persistence error.");
            }

            throw;
        }
        await Task.CompletedTask;
        return OperationResult<bool>.Success(enabled ? "startup.enabled" : "startup.disabled", enabled ? "StartupEnabled" : "StartupDisabled", enabled);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<ProductInformation>> GetProductInformationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new ProductInformation(
            "TrackMeUp",
            "MIT",
            ProductRepositoryUrl,
            ProductAuthorUrl,
            _buildInformation.Load());
        return Task.FromResult(OperationResult<ProductInformation>.Success("product.loaded", "ProductInformationLoaded", info));
    }

    /// <inheritdoc />
    public Task<OperationResult<bool>> OpenProductLinkAsync(string linkKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = linkKey switch
        {
            "author" => ProductAuthorUrl,
            "repository" => ProductRepositoryUrl,
            _ => null
        };
        if (target is null)
        {
            return Task.FromResult(OperationResult<bool>.Failure(
                "product.link.invalid",
                "ProductLinkInvalid",
                new ValidationIssue("linkKey", "unsupported", "ProductLinkInvalid")));
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true })
                ?? throw new InvalidOperationException("Windows did not open the product link.");
            _logger.LogInformation("Product link opened. Link={Link}", linkKey);
            return Task.FromResult(OperationResult<bool>.Success("product.link.opened", "ProductLinkOpened", true));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
                NotSupportedException or
                System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning("Product link could not be opened. Link={Link} ExceptionType={ExceptionType}", linkKey, exception.GetType().Name);
            return Task.FromResult(OperationResult<bool>.Failure("product.link.unavailable", "ProductLinkUnavailable"));
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
        _scheduledSnapshotTimer.Dispose();
        _tracking.DashboardStateChanged -= OnDashboardStateChanged;
        _tracking.TrackingStateChanged -= OnTrackingStateChanged;
        _pricingRefresh?.Dispose();
        _tracking.Dispose();
        await _search.DisposeAsync().ConfigureAwait(false);
        _mutations.Dispose();
    }

    private async Task<OperationResult<T>> MutateAsync<T>(Func<Task<OperationResult<T>>> operation, CancellationToken cancellationToken)
    {
        await _mutations.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _mutations.Release();
        }
    }

    private DashboardState LoadDashboardState()
    {
        var settings = _store.LoadSettings();
        return _tracking.LoadCurrentDashboardState() with
        {
            ScheduledSnapshotRemaining = GetScheduledSnapshotRemaining(),
            PendingManualScreenshot = GetPendingManualScreenshotState(),
            IsWithinActiveHours = ActiveHoursSchedule.IsWithinActiveHours(settings.ActiveHours, DateTimeOffset.Now)
        };
    }

    private PendingManualScreenshotState? GetPendingManualScreenshotState() =>
        _pendingManualScreenshotCapture?.StoredScreenshotPaths.FirstOrDefault() is { } screenshotPath &&
        _pendingManualScreenshotExpiresAt is { } expiresAt && expiresAt > DateTimeOffset.Now
            ? new PendingManualScreenshotState(screenshotPath, expiresAt)
            : null;

    private TimeSpan? GetScheduledSnapshotRemaining()
    {
        if (_tracking.IsTracking && _nextScheduledSnapshotAt is { } nextSnapshotAt)
        {
            return nextSnapshotAt > DateTimeOffset.Now
                ? nextSnapshotAt - DateTimeOffset.Now
                : TimeSpan.Zero;
        }

        return _pausedScheduledSnapshotRemaining;
    }

    private void ConfigureScheduledSnapshots(AppSettings settings, bool restartCountdown)
    {
        _scheduledSnapshotIntervalMinutes = settings.ScreenshotIntervalMinutes;
        _scheduledSnapshotsEnabled = _scheduledSnapshotIntervalMinutes > 0
            && ActiveHoursSchedule.HasAnyActivePeriod(settings.ActiveHours);
        if (!_scheduledSnapshotsEnabled)
        {
            // With no eligible hours there is no countdown to display and no silent timer loop to keep resetting.
            _nextScheduledSnapshotAt = null;
            _pausedScheduledSnapshotRemaining = null;
            return;
        }

        if (_tracking.IsTracking)
        {
            if (restartCountdown || _nextScheduledSnapshotAt is null)
            {
                _nextScheduledSnapshotAt = DateTimeOffset.Now.AddMinutes(_scheduledSnapshotIntervalMinutes);
            }

            _pausedScheduledSnapshotRemaining = null;
            return;
        }

        if (restartCountdown || _pausedScheduledSnapshotRemaining is null)
        {
            _pausedScheduledSnapshotRemaining = TimeSpan.FromMinutes(_scheduledSnapshotIntervalMinutes);
        }

        _nextScheduledSnapshotAt = null;
    }

    private void PauseScheduledSnapshots()
    {
        _pausedScheduledSnapshotRemaining = GetScheduledSnapshotRemaining();
        _nextScheduledSnapshotAt = null;
    }

    private void ResumeScheduledSnapshots()
    {
        if (!_scheduledSnapshotsEnabled)
        {
            return;
        }

        var remaining = _pausedScheduledSnapshotRemaining ?? TimeSpan.FromMinutes(_scheduledSnapshotIntervalMinutes);
        _nextScheduledSnapshotAt = DateTimeOffset.Now.Add(remaining);
        _pausedScheduledSnapshotRemaining = null;
    }

    private void HandleScheduledSnapshotTimerTick(object? state) => _ = ProcessRuntimeTimerAsync();

    private async Task ProcessRuntimeTimerAsync()
    {
        await CaptureActivityScoreTelemetryIfDueAsync();
        await ProcessScheduledSnapshotAsync();
    }

    private void LogAiProviderFailure(string operation, AiProviderRequestException exception)
    {
        _logger.LogWarning(
            "AI provider request failed at the application boundary. Operation={Operation} HttpStatus={HttpStatus} FailureCategory={FailureCategory} LatencyMs={LatencyMs} ProviderRequestId={ProviderRequestId}",
            operation,
            exception.Failure.HttpStatusCode,
            exception.Failure.FailureCode,
            exception.Failure.ElapsedMilliseconds,
            AiProviderTelemetry.SafeToken(exception.Failure.ProviderRequestId, 80));
    }

    private void CleanupCaptureArtifacts(ScreenshotCaptureResult capture, bool keepStoredArtifacts)
    {
        var storedPaths = capture.StoredScreenshotPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cleanupPaths = keepStoredArtifacts
            ? capture.AnalysisScreenshotPaths.Where(path => !storedPaths.Contains(path))
            : capture.AllScreenshotPaths;
        foreach (var path in cleanupPaths.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Image cleanup is best effort; OCR and provider failures remain the primary operation outcome.
                _logger.LogWarning("Screenshot cleanup failed. ExceptionType={ExceptionType}", exception.GetType().Name);
            }
        }
    }

    /// <summary>Captures one telemetry point per minute while tracking so the live score includes CPU and GPU activity.</summary>
    private async Task CaptureActivityScoreTelemetryIfDueAsync()
    {
        if (!_tracking.IsTracking)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        lock (_activityScoreTelemetryGate)
        {
            if (_nextActivityScoreTelemetryAt is { } nextTelemetryAt && now < nextTelemetryAt)
            {
                return;
            }

            _nextActivityScoreTelemetryAt = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero)
                .AddMinutes(1);
        }

        var result = await CaptureSystemSnapshotAsync(CancellationToken.None);
        if (!result.Succeeded)
        {
            // The score keeps its input-only data for this minute when optional telemetry is unavailable.
            _logger.LogWarning("Activity score telemetry sample failed. Code={Code}", result.Code);
        }
    }

    private async Task ProcessScheduledSnapshotAsync()
    {
        try
        {
            var expiredManualCapture = await MutateAsync(async () =>
            {
                if (_pendingManualScreenshotCapture is not { } capture ||
                    _pendingManualScreenshotExpiresAt is not { } expiresAt || expiresAt > DateTimeOffset.Now)
                {
                    return OperationResult<ScreenshotCaptureResult?>.Success("snapshot.pending.not_due", "PendingManualSnapshotNotDue");
                }

                _pendingManualScreenshotCapture = null;
                _pendingManualScreenshotExpiresAt = null;
                await Task.CompletedTask;
                return OperationResult<ScreenshotCaptureResult?>.Success("snapshot.pending.expired", "PendingManualSnapshotExpired", capture);
            }, CancellationToken.None);

            if (expiredManualCapture.Succeeded && expiredManualCapture.Value is { } capture)
            {
                // Analysis begins only after the runtime closes the deletion window.
                await AnalyzeCapturedScreenshotAsync(
                    new AnalyzeCapturedScreenshotRequest(capture, KeepCapture: true, Origin: "snapshot.manual"),
                    CancellationToken.None);
            }

            var due = await MutateAsync(async () =>
            {
                if (!_tracking.IsTracking || _nextScheduledSnapshotAt is not { } nextSnapshotAt || DateTimeOffset.Now < nextSnapshotAt)
                {
                    return OperationResult<bool>.Success("snapshot.schedule.not_due", "SnapshotScheduleNotDue", false);
                }

                _nextScheduledSnapshotAt = DateTimeOffset.Now.AddMinutes(_scheduledSnapshotIntervalMinutes);
                await Task.CompletedTask;
                return OperationResult<bool>.Success("snapshot.schedule.due", "SnapshotScheduleDue", true);
            }, CancellationToken.None);

            if (!due.Succeeded || due.Value != true)
            {
                return;
            }

            var settings = _store.LoadSettings();
            if (!ActiveHoursSchedule.IsWithinActiveHours(settings.ActiveHours, DateTimeOffset.Now))
            {
                return;
            }

            // Scheduled capture is runtime-owned; pause clears its deadline before another tick can become due.
            var systemSnapshot = await CaptureSystemSnapshotAsync(CancellationToken.None);
            if (!systemSnapshot.Succeeded)
            {
                _logger.LogWarning("Scheduled system snapshot failed. Code={Code}", systemSnapshot.Code);
            }

            if (settings.ScreenshotsEnabled)
            {
                // The retained image is the primary result. AI enrichment is attempted only after local capture succeeds.
                var scheduledCapture = await CaptureScreenshotAsync(
                    new CaptureScreenshotRequest(
                        Mode: null,
                        Keep: true,
                        Watermark: true,
                        CaptureOrigin: ScreenshotCaptureOrigins.Scheduled,
                        DeferAiAnalysis: true),
                    CancellationToken.None);
                if (!scheduledCapture.Succeeded || scheduledCapture.Value is not { } retainedCapture)
                {
                    _logger.LogWarning("Scheduled screen capture failed. Code={Code}", scheduledCapture.Code);
                    return;
                }

                if (settings.OpenAiEnabled)
                {
                    var analysis = await AnalyzeCapturedScreenshotAsync(
                        new AnalyzeCapturedScreenshotRequest(retainedCapture, KeepCapture: true, Origin: "snapshot.scheduled"),
                        CancellationToken.None);
                    if (!analysis.Succeeded)
                    {
                        // Missing keys, cost limits, and provider failures never remove the already retained snapshot.
                        _logger.LogWarning("Scheduled snapshot retained without AI analysis. Code={Code}", analysis.Code);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            // Timer failures are logged and the next configured interval remains available for a later attempt.
            _logger.LogWarning("Scheduled snapshot processing failed. ExceptionType={ExceptionType}", exception.GetType().Name);
        }
    }

    private Task<OperationResult<string>> OpenFolderAsync(string directory, string code, string messageKey, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        try
        {
            var normalized = _utilities.NormalizeDirectory(directory);
            // Shell invocation remains behind the application boundary, never in a UI or CLI renderer.
            Process.Start(new ProcessStartInfo { FileName = normalized, UseShellExecute = true });
            await Task.CompletedTask;
            return OperationResult<string>.Success(code, messageKey, normalized);
        }
        catch (Exception)
        {
            return OperationResult<string>.Failure("shell.open.failed", "FolderOpenFailed");
        }
    }, cancellationToken);

    private AiStatus BuildAiStatus()
    {
        var settings = _store.LoadSettings();
        var key = _store.LoadApiKey(settings.AiApiKeyName);
        var hasKey = !string.IsNullOrWhiteSpace(key);
        var canEnable = AiApiKeyPolicy.LooksPlausible(settings.AiProvider, settings.AiApiKeyName, key);
        key = string.Empty;
        return new AiStatus(settings.OpenAiEnabled, settings.AiProvider, settings.Model, settings.AiEndpoint, settings.AiApiKeyName, hasKey, canEnable, BuildCostGate(settings));
    }

    private static bool ShouldNotifyAiAnalysisFailure(string code) =>
        code is not "operation.cancelled" and not "privacy.blocked" and not "ai.disabled";

    private void EnqueueAiAnalysisFailure(string code, string? detail = null)
    {
        while (_notifications.Count >= MaximumPendingNotifications && _notifications.TryDequeue(out _))
        {
            // Oldest notifications are discarded first so a headless runtime remains memory-bounded.
        }

        _notifications.Enqueue(new ApplicationNotification(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            ApplicationNotificationSeverity.Error,
            "Dialog.AiAnalysisFailed.Title",
            "Dialog.AiAnalysisFailed.Message",
            code,
            detail));
    }

    private static string? BuildAiProviderFailureDetail(AiProviderRequestException exception)
    {
        var lines = new List<string>();
        if (exception.Failure.HttpStatusCode is { } statusCode)
        {
            lines.Add($"HTTP status: {statusCode}");
        }

        if (!string.IsNullOrWhiteSpace(exception.Failure.FailureCode))
        {
            lines.Add($"Failure: {exception.Failure.FailureCode}");
        }

        lines.Add($"Latency: {exception.Failure.ElapsedMilliseconds} ms");
        if (exception.Failure.ProviderProcessingMilliseconds is { } providerProcessingMilliseconds)
        {
            lines.Add($"Provider processing: {providerProcessingMilliseconds} ms");
        }

        var providerRequestId = AiProviderTelemetry.SafeToken(exception.Failure.ProviderRequestId, 80);
        if (providerRequestId is not null)
        {
            lines.Add($"Provider request id: {providerRequestId}");
        }

        var providerResponseId = AiProviderTelemetry.SafeToken(exception.Failure.ProviderResponseId, 80);
        if (providerResponseId is not null)
        {
            lines.Add($"Provider response id: {providerResponseId}");
        }

        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }

    private AnalysisCostGate BuildCostGate(AppSettings settings)
    {
        var count = _store.GetTodayAnalysisCount();
        var projected = settings.OpenAiDailyCostUsd + settings.EstimatedCostPerAnalysisUsd;
        var allowed = count < settings.OpenAiDailyLimit;
        return new AnalysisCostGate(allowed, allowed ? null : "daily_limit", settings.EstimatedCostPerAnalysisUsd, count, projected);
    }

    private bool IsCurrentContextPrivate(AppSettings settings)
    {
        var context = _tracking.LatestAnalysisContext;
        if (context is null)
        {
            return false;
        }

        return ReadPrivacyRules(settings).Any(rule => rule.Type switch
        {
            "process" => context.Application.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
            "title" => context.WindowTitle.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
            "hint" => context.Context.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        });
    }

    private static IReadOnlyList<PrivacyRule> ReadPrivacyRules(AppSettings settings) =>
        ReadPrivacyRules("process", settings.PrivacyProcessNames)
            .Concat(ReadPrivacyRules("title", settings.PrivacyWindowTitles))
            .Concat(ReadPrivacyRules("hint", settings.PrivacyWindowHints))
            .ToArray();

    private static IEnumerable<PrivacyRule> ReadPrivacyRules(string type, string raw) => raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => value.Split('|', 2))
        .Where(parts => parts.Length == 2)
        .Select(parts => new PrivacyRule(parts[0], type, parts[1]));

    private void SavePrivacyRules(AppSettings settings, IReadOnlyCollection<PrivacyRule> rules)
    {
        static string Serialize(IEnumerable<PrivacyRule> values) => string.Join('\n', values.Select(x => $"{x.Id}|{x.Value.Replace("|", "", StringComparison.Ordinal)}"));
        _store.SaveSettings(settings with
        {
            PrivacyProcessNames = Serialize(rules.Where(x => x.Type == "process")),
            PrivacyWindowTitles = Serialize(rules.Where(x => x.Type == "title")),
            PrivacyWindowHints = Serialize(rules.Where(x => x.Type == "hint"))
        });
    }

    private RetentionPreview BuildRetentionPreview()
    {
        var settings = _store.LoadSettings();
        var screenshotCutoff = DateTimeOffset.Now.AddDays(-settings.ScreenshotRetentionDays);
        var dataCutoff = DateTimeOffset.Now.AddDays(-settings.DataRetentionDays);
        var screenshotPaths = Directory.Exists(settings.ScreenshotDirectory)
            ? Directory.EnumerateFiles(settings.ScreenshotDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(ScreenCaptureService.IsOwnedArtifact)
                .Where(path => File.GetLastWriteTimeUtc(path) < screenshotCutoff.UtcDateTime)
                .ToArray()
            : Array.Empty<string>();
        var dataPreview = _store.GetRetentionPreview(dataCutoff);
        var paths = screenshotPaths.Concat(dataPreview.Paths).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var screenshotBytes = screenshotPaths.Sum(path => new FileInfo(path).Length);
        return new RetentionPreview(screenshotPaths.Length + dataPreview.RecordCount, screenshotBytes + dataPreview.TotalBytes, paths);
    }

    private static IReadOnlyList<PluginInfo> BuildPlugins(AppSettings settings) =>
    [
        new PluginInfo("word", "Microsoft Word", settings.EnableWordDetailPlugin, "Extracts safe Word activity context."),
        new PluginInfo("excel", "Microsoft Excel", settings.EnableExcelDetailPlugin, "Extracts safe Excel activity context."),
        new PluginInfo("vscode", "Visual Studio Code", settings.EnableVsCodeDetailPlugin, "Extracts safe IDE activity context."),
        new PluginInfo("browser", "Browser", settings.EnableBrowserDetailPlugin, "Extracts safe browser activity context.")
    ];

    private AiModelDescriptor? ResolveAiModel(string identifier) => _aiModelCatalog.Models.FirstOrDefault(model =>
        string.Equals(model.Key, identifier, StringComparison.OrdinalIgnoreCase) ||
        model.Aliases.Contains(identifier, StringComparer.OrdinalIgnoreCase));

    private bool TryValidateOpenAiConfiguration(
        AppSettings settings,
        bool requireImageInput,
        out AppSettings validatedSettings,
        out ValidationIssue? issue)
    {
        validatedSettings = settings;
        issue = null;
        if (settings.OpenAiEnabled
            && !AiApiKeyPolicy.LooksPlausible(
                settings.AiProvider,
                settings.AiApiKeyName,
                _store.LoadApiKey(settings.AiApiKeyName)))
        {
            issue = new ValidationIssue("ai.enabled", "api_key_required", "AiKeyInvalid");
            return false;
        }

        if (!string.Equals(settings.AiProvider, "openai", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var selectedModel = ResolveAiModel(settings.Model);
        if (selectedModel is null)
        {
            issue = new ValidationIssue("ai.model", "unsupported", "AiModelUnsupported");
            return false;
        }

        if (requireImageInput && !selectedModel.SupportsImageInput)
        {
            issue = new ValidationIssue("ai.model", "image_input_unsupported", "AiModelImageInputUnsupported");
            return false;
        }

        if (!selectedModel.SupportedThinkingEfforts.Contains(settings.AiReasoningEffort, StringComparer.Ordinal))
        {
            issue = new ValidationIssue("ai.reasoning_effort", "unsupported", "AiThinkingEffortUnsupported");
            return false;
        }

        validatedSettings = settings with { Model = selectedModel.Key };
        return true;
    }

    private void OnDashboardStateChanged(DashboardState state) => RuntimeStateChanged?.Invoke(this, new RuntimeStateChangedEventArgs(LoadDashboardState(), "runtime.dashboard.changed"));

    private void OnTrackingStateChanged(bool isTracking) => RuntimeStateChanged?.Invoke(this, new RuntimeStateChangedEventArgs(LoadDashboardState(), isTracking ? "tracking.started" : "tracking.paused"));

    private static string NormalizeAnalysisOrigin(string? origin) => origin?.Trim().ToLowerInvariant() switch
    {
        "winui.operations" => "winui.operations",
        "cli.ai" => "cli.ai",
        "snapshot.manual" => "snapshot.manual",
        "snapshot.scheduled" => "snapshot.scheduled",
        "runtime.ai" => "runtime.ai",
        _ => "manual"
    };
}
