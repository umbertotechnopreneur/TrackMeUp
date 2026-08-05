using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackMeUp.Runtime;
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
        var analysis = new OpenAiAnalysisService(store, capture, snapshot, deviceContext: deviceContext);
        return new TrackMeUpApplication(store, utilities, tracking, capture, snapshot, analysis, new StartupService(), buildInformation, logger, observability, deviceContext, new ScreenshotShareService(), new WindowStateService(store), aiModelCatalog);
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
    private readonly ScreenshotShareService _screenshotShare;
    private readonly WindowStateService _windowState;
    private readonly StartupService _startup;
    private readonly BuildInformationService _buildInformation;
    private readonly AiModelCatalogSnapshot _aiModelCatalog;
    private readonly ReportAggregationService _reports;
    private readonly ILogger<TrackMeUpApplication> _logger;
    private readonly ObservabilityHealth _observability;
    private readonly SemaphoreSlim _mutations = new(1, 1);
    private FocusSessionState _focus = new(null, false, null, TimeSpan.Zero, 0, 0, 0, 0, null);
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
        AiModelCatalog? aiModelCatalog = null)
    {
        _store = store;
        _utilities = utilities;
        _tracking = tracking;
        _capture = capture;
        _snapshot = snapshot;
        _deviceContext = deviceContext ?? new DeviceContextService();
        _screenshotShare = screenshotShare ?? new ScreenshotShareService();
        _windowState = windowState ?? new WindowStateService(store);
        _analysis = analysis;
        _startup = startup;
        _buildInformation = buildInformation;
        _aiModelCatalog = (aiModelCatalog ?? AiModelCatalog.LoadDefault()).Snapshot;
        _reports = new ReportAggregationService(store);
        _logger = logger ?? NullLogger<TrackMeUpApplication>.Instance;
        _observability = observability ?? new ObservabilityHealth(false, false, "unknown", false);
        _tracking.DashboardStateChanged += OnDashboardStateChanged;
        _tracking.TrackingStateChanged += OnTrackingStateChanged;
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
            ["tracking", "sessions", "focus", "system", "screenshots", "screenshots.save", "screenshots.share", "screenshots.delete", "snapshots.delete", "screenshots.analyze", "window.state", "ai", "ai.models", "reports", "reports.query.v1", "privacy", "retention", "plugins", "settings", "startup", "links", "observability"],
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

        _tracking.Start();
        _logger.LogInformation("Tracking started. SafeMode={SafeMode}", request.SafeMode);
        var state = _tracking.LoadCurrentDashboardState();
        await Task.CompletedTask;
        return OperationResult<DashboardState>.Success("tracking.started", "TrackingStarted", state);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> PauseTrackingAsync(CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        _tracking.Stop();
        _logger.LogInformation("Tracking paused.");
        var state = _tracking.LoadCurrentDashboardState();
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
        return Task.FromResult(OperationResult<DashboardState>.Success("dashboard.loaded", "DashboardLoaded", _tracking.LoadCurrentDashboardState()));
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
    public Task<OperationResult<FocusSessionState>> StartFocusSessionAsync(StartFocusSessionRequest request, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var objective = request.Objective?.Trim();
        if (string.IsNullOrWhiteSpace(objective))
        {
            return OperationResult<FocusSessionState>.Failure("focus.objective.required", "FocusObjectiveRequired", new ValidationIssue("objective", "required", "FocusObjectiveRequired"));
        }

        if (_focus.IsActive)
        {
            return OperationResult<FocusSessionState>.Failure("focus.already_active", "FocusAlreadyActive", new ValidationIssue("focus", "already_active", "FocusAlreadyActive"));
        }

        _focus = new FocusSessionState(objective, true, DateTimeOffset.Now, TimeSpan.Zero, 0, 0, 0, 0, null);
        await Task.CompletedTask;
        return OperationResult<FocusSessionState>.Success("focus.started", "FocusStarted", _focus);
    }, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<FocusSessionState>> GetFocusSessionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<FocusSessionState>.Success("focus.status.loaded", "FocusStatusLoaded", UpdateFocusState()));
    }

    /// <inheritdoc />
    public Task<OperationResult<FocusSessionSummary?>> StopFocusSessionAsync(bool summarize, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var state = UpdateFocusState();
        if (!state.IsActive || state.StartedAt is null || string.IsNullOrWhiteSpace(state.Objective))
        {
            return OperationResult<FocusSessionSummary?>.Failure("focus.not_active", "FocusNotActive", new ValidationIssue("focus", "not_active", "FocusNotActive"));
        }

        var summary = new FocusSessionSummary(state.StartedAt.Value, DateTimeOffset.Now, state.Objective, state.ActiveSeconds, state.IdleSeconds, state.KeyPresses, state.MouseClicks, state.PrimaryApplication);
        _focus = new FocusSessionState(null, false, null, TimeSpan.Zero, 0, 0, 0, 0, null);
        await Task.CompletedTask;
        return OperationResult<FocusSessionSummary?>.Success("focus.stopped", summarize ? "FocusStoppedWithSummary" : "FocusStopped", summarize ? summary : null);
    }, cancellationToken);

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

        var mode = request.Mode is "all-screens" or "active-window" ? request.Mode : settings.ScreenshotCaptureMode;
        // Capture happens only after the privacy and enabled-state gates above have succeeded.
        var result = _capture.CaptureByMode(
            settings.ScreenshotDirectory,
            mode,
            request.Watermark && settings.WatermarkScreenshots,
            request.CaptureOrigin);

        if (settings.OpenAiEnabled && !request.DeferAiAnalysis)
        {
            try
            {
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
            catch (InvalidOperationException)
            {
                return OperationResult<ScreenshotCaptureResult>.Failure("ai.configuration.invalid", "AiConfigurationInvalid");
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Snapshot AI analysis failed. ExceptionType={ExceptionType}", exception.GetType().Name);
                return OperationResult<ScreenshotCaptureResult>.Failure("ai.provider.failed", "AiProviderFailed");
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
    public Task<OperationResult<AiAnalysis>> AnalyzeCapturedScreenshotAsync(AnalyzeCapturedScreenshotRequest request, CancellationToken cancellationToken) => MutateAsync(async () =>
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
            var result = await _analysis.AnalyzeCapturedScreenAsync(
                _tracking.LatestAnalysisContext,
                request.Capture,
                request.KeepCapture,
                origin,
                cancellationToken);
            return OperationResult<AiAnalysis>.Success("ai.analyzed", "AiAnalyzed", result);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<AiAnalysis>.Failure("operation.cancelled", "OperationCancelled");
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
    }, cancellationToken);

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

        var deletedCount = _store.DeleteAiAnalysesReferencingScreenshot(screenshotPath);
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
    public Task<OperationResult<AiModelCatalogSnapshot>> GetAiModelCatalogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OperationResult<AiModelCatalogSnapshot>.Success("ai.models.loaded", "AiModelsLoaded", _aiModelCatalog));
    }

    /// <inheritdoc />
    public Task<OperationResult<AiStatus>> SetAiEnabledAsync(bool enabled, CancellationToken cancellationToken) => MutateAsync(async () =>
    {
        var settings = _store.LoadSettings() with { OpenAiEnabled = enabled };
        _store.SaveSettings(settings);
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
        if (!SettingsCatalog.IsAllowedApiKeyVariable(keyVariable) || string.IsNullOrWhiteSpace(secret))
        {
            return OperationResult<string>.Failure("ai.key.invalid", "AiKeyInvalid", new ValidationIssue("key", "invalid", "AiKeyInvalid"));
        }

        // The secret is immediately delegated to the user environment store and never persisted or logged.
        var normalizedKeyVariable = keyVariable.Trim();
        _utilities.SetApiKey(normalizedKeyVariable, secret.Trim());
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
            var result = await _analysis.AnalyzeCurrentScreenAsync(
                _tracking.LatestAnalysisContext,
                request.AllowCapture,
                origin,
                cancellationToken);
            return OperationResult<AiAnalysis>.Success("ai.analyzed", "AiAnalyzed", result);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<AiAnalysis>.Failure("operation.cancelled", "OperationCancelled");
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
            "https://github.com/umbertotechnopreneur/TrackMeUp",
            "https://umbertogiacobbi.com",
            _buildInformation.Load());
        return Task.FromResult(OperationResult<ProductInformation>.Success("product.loaded", "ProductInformationLoaded", info));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _tracking.DashboardStateChanged -= OnDashboardStateChanged;
        _tracking.TrackingStateChanged -= OnTrackingStateChanged;
        _tracking.Dispose();
        _mutations.Dispose();
        return ValueTask.CompletedTask;
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
        return new AiStatus(settings.OpenAiEnabled, settings.AiProvider, settings.Model, settings.AiEndpoint, settings.AiApiKeyName, !string.IsNullOrWhiteSpace(_store.LoadApiKey(settings.AiApiKeyName)), BuildCostGate(settings));
    }

    private AnalysisCostGate BuildCostGate(AppSettings settings)
    {
        var count = _store.GetTodayAnalysisCount();
        var projected = settings.OpenAiDailyCostUsd + settings.EstimatedCostPerAnalysisUsd;
        var allowed = count < settings.OpenAiDailyLimit;
        return new AnalysisCostGate(allowed, allowed ? null : "daily_limit", settings.EstimatedCostPerAnalysisUsd, count, projected);
    }

    private FocusSessionState UpdateFocusState()
    {
        if (!_focus.IsActive || _focus.StartedAt is null)
        {
            return _focus;
        }

        var today = _store.GetTodaySummary();
        var last = _tracking.LoadLastSessionState();
        _focus = _focus with
        {
            Elapsed = DateTimeOffset.Now - _focus.StartedAt.Value,
            ActiveSeconds = today.ActiveSeconds,
            IdleSeconds = today.IdleSeconds,
            KeyPresses = today.KeyPresses,
            MouseClicks = today.MouseClicks,
            PrimaryApplication = last?.Application
        };
        return _focus;
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

    private void OnDashboardStateChanged(DashboardState state) => RuntimeStateChanged?.Invoke(this, new RuntimeStateChangedEventArgs(state, "runtime.dashboard.changed"));

    private void OnTrackingStateChanged(bool isTracking) => RuntimeStateChanged?.Invoke(this, new RuntimeStateChangedEventArgs(_tracking.LoadCurrentDashboardState(), isTracking ? "tracking.started" : "tracking.paused"));

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
