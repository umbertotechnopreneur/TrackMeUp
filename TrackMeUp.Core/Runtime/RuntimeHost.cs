// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackMeUp.Application;
using TrackMeUp.Search;
using TrackMeUp.Services;

namespace TrackMeUp.Runtime;

/// <summary>Owns the single local runtime and serves versioned requests over a same-user named pipe.</summary>
public sealed class RuntimeHost : IAsyncDisposable
{
    private ITrackMeUpApplication _application;
    private readonly Func<ITrackMeUpApplication>? _applicationFactory;
    private readonly bool _ownsApplication;
    private readonly RuntimeEndpoint _endpoint;
    private readonly ILogger<RuntimeHost> _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentDictionary<int, Task> _activeRequests = new();
    private RuntimeMutexLease? _mutexLease;
    private Task? _serverTask;
    private bool _disposed;
    private int _requestSequence;

    /// <summary>Initializes a runtime host for a single application installation.</summary>
    public RuntimeHost(ITrackMeUpApplication application, string installationId, ILogger<RuntimeHost>? logger = null)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _endpoint = RuntimeProtocol.CreateEndpoint(installationId);
        _logger = logger ?? NullLogger<RuntimeHost>.Instance;
    }

    /// <summary>
    /// Initializes a host that creates its local application only after runtime ownership is acquired.
    /// </summary>
    public RuntimeHost(
        Func<ITrackMeUpApplication> applicationFactory,
        string installationId,
        ILogger<RuntimeHost>? logger = null)
    {
        _application = null!;
        _applicationFactory = applicationFactory ?? throw new ArgumentNullException(nameof(applicationFactory));
        _ownsApplication = true;
        _endpoint = RuntimeProtocol.CreateEndpoint(installationId);
        _logger = logger ?? NullLogger<RuntimeHost>.Instance;
    }

    /// <summary>Gets the endpoint used by this host.</summary>
    public RuntimeEndpoint Endpoint => _endpoint;

    /// <summary>Gets the local application after this host has acquired runtime ownership.</summary>
    public ITrackMeUpApplication Application =>
        _application ?? throw new InvalidOperationException("The runtime host does not own a local application.");

    /// <summary>Occurs after a successful reset-preparation response has been flushed to its caller.</summary>
    public event Action<AtomicResetPlan>? AtomicResetPrepared;

    /// <summary>Starts the host when this process successfully acquires runtime ownership.</summary>
    public bool TryStart()
    {
        ThrowIfDisposed();
        _mutexLease = new RuntimeMutexLease(_endpoint.MutexName);
        if (!_mutexLease.Acquired)
        {
            _logger.LogDebug("Runtime mutex is already owned by another process.");
            _mutexLease.Dispose();
            _mutexLease = null;
            return false;
        }

        try
        {
            _application ??= _applicationFactory?.Invoke()
                ?? throw new InvalidOperationException("The runtime application factory returned no application.");
        }
        catch
        {
            _mutexLease.Dispose();
            _mutexLease = null;
            throw;
        }

        // One background loop owns pipe acceptance; mutations themselves remain serialized in the facade.
        _serverTask = Task.Run(() => ServeAsync(_shutdown.Token));
        _logger.LogInformation("Runtime host started. Pipe={PipeName}", _endpoint.PipeName);
        return true;
    }

    /// <summary>
    /// Stops the named-pipe server, drains requests and disposes a factory-owned application before
    /// releasing the runtime mutex.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Exception? shutdownFailure = null;
        try
        {
            _shutdown.Cancel();
            if (_serverTask is not null)
            {
                try
                {
                    await _serverTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown; no client-visible error is produced.
                }
                catch (Exception exception)
                {
                    shutdownFailure = exception;
                }
            }

            var activeRequests = _activeRequests.Values.ToArray();
            if (activeRequests.Length > 0)
            {
                try
                {
                    await Task.WhenAll(activeRequests);
                }
                catch (Exception exception)
                {
                    shutdownFailure ??= exception;
                }
            }

            if (_ownsApplication && _application is not null)
            {
                try
                {
                    await _application.DisposeAsync();
                }
                catch (Exception exception)
                {
                    shutdownFailure ??= exception;
                }
            }
        }
        finally
        {
            // Runtime ownership is the last resource released. A successor cannot start while any
            // factory-owned writer from this host is still shutting down.
            _mutexLease?.Dispose();
            _mutexLease = null;
            _shutdown.Dispose();
        }

        if (shutdownFailure is not null)
        {
            ExceptionDispatchInfo.Capture(shutdownFailure).Throw();
        }
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _endpoint.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                TrackRequest(HandleConnectionAsync(pipe, cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await pipe.DisposeAsync();
                throw;
            }
            catch (Exception exception)
            {
                await pipe.DisposeAsync();
                _logger.LogWarning("Runtime pipe acceptance failed; continuing to serve requests. ExceptionType={ExceptionType}", exception.GetType().Name);
            }
        }
    }

    private void TrackRequest(Task requestTask)
    {
        var requestId = Interlocked.Increment(ref _requestSequence);
        _activeRequests[requestId] = requestTask;
        _ = requestTask.ContinueWith(
            completedTask =>
            {
                _activeRequests.TryRemove(requestId, out _);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken shutdownToken)
    {
        await using (pipe)
        {
            try
            {
                var request = await RuntimeProtocol.ReadAsync<RuntimeRequestEnvelope>(pipe, shutdownToken);
                using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                var disconnectMonitor = MonitorDisconnectAsync(pipe, requestCancellation);
                RuntimeResponseEnvelope response;
                try
                {
                    response = await DispatchAsync(request, requestCancellation.Token);
                }
                finally
                {
                    requestCancellation.Cancel();
                    await disconnectMonitor;
                }

                await RuntimeProtocol.WriteAsync(pipe, response, shutdownToken);
                if (request.Operation == RuntimeOperationCatalog.GetWireName(RuntimeOperation.AppAtomicResetV1)
                    && response.Succeeded
                    && response.Payload is AtomicResetPlan resetPlan)
                {
                    // The runtime owner begins shutdown only after the frontend has received the destructive-operation result.
                    AtomicResetPrepared?.Invoke(resetPlan);
                }
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // Host shutdown cancels every active request before releasing runtime ownership.
            }
            catch (OperationCanceledException)
            {
                // Client disconnect/timeout cancellation stops long-running reads without affecting the host.
            }
            catch (Exception exception)
            {
                // Invalid/disconnected clients are isolated so the long-lived local runtime remains available.
                _logger.LogWarning("Runtime pipe request failed; continuing to serve requests. ExceptionType={ExceptionType}", exception.GetType().Name);
            }
        }
    }

    private static async Task MonitorDisconnectAsync(NamedPipeServerStream pipe, CancellationTokenSource requestCancellation)
    {
        var probe = new byte[1];
        try
        {
            _ = await pipe.ReadAsync(probe, requestCancellation.Token);
            requestCancellation.Cancel();
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
            // Normal completion cancels the pending disconnect read before the response is written.
        }
        catch (IOException)
        {
            requestCancellation.Cancel();
        }
    }

    private async Task<RuntimeResponseEnvelope> DispatchAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        if (request.ProtocolVersion != RuntimeProtocol.ProtocolVersion)
        {
            return Failure(request, "ipc.protocol.unsupported", "IpcProtocolUnsupported");
        }

        if (!RuntimeOperationCatalog.TryResolve(request.Operation, out var operation))
        {
            return Failure(request, "command.invalid", "CommandInvalid");
        }

        try
        {
            return operation switch
            {
                RuntimeOperation.RuntimeHealth => ToResponse(request, await _application.GetRuntimeHealthAsync(cancellationToken)),
                RuntimeOperation.TrackingStart => ToResponse(request, await _application.StartTrackingAsync(Read<StartTrackingRequest>(request.Payload) ?? new StartTrackingRequest(), cancellationToken)),
                RuntimeOperation.TrackingPause => ToResponse(request, await _application.PauseTrackingAsync(cancellationToken)),
                RuntimeOperation.TrackingToggle => ToResponse(request, await _application.ToggleTrackingAsync(cancellationToken)),
                RuntimeOperation.DashboardGet => ToResponse(request, await _application.GetDashboardAsync(cancellationToken)),
                RuntimeOperation.WorldClocksGetV2 => ToResponse(request, await _application.GetWorldClocksAsync(cancellationToken)),
                RuntimeOperation.WorldClocksConvertV1 => ToResponse(request, await _application.ConvertWorldClocksAsync(
                    Read<WorldClockConversionRequest>(request.Payload)
                        ?? throw new InvalidDataException("A world-clock conversion request is required."),
                    cancellationToken)),
                RuntimeOperation.WorldClocksCatalogV1 => ToResponse(request, await _application.GetWorldClockCityCatalogAsync(cancellationToken)),
                RuntimeOperation.WorldClocksAddV3 => ToResponse(request, await _application.AddWorldClockAsync(ReadString(request.Payload, "cityId"), cancellationToken)),
                RuntimeOperation.WorldClocksRemoveV3 => ToResponse(request, await _application.RemoveWorldClockAsync(ReadString(request.Payload, "cityId"), cancellationToken)),
                RuntimeOperation.WorldClocksWeatherKeySetV1 => ToResponse(request, await _application.SetWorldClockWeatherKeyAsync(ReadString(request.Payload, "secret"), cancellationToken)),
                RuntimeOperation.SessionLast => ToResponse(request, await _application.GetLastSessionAsync(cancellationToken)),
                RuntimeOperation.SessionToday => ToResponse(request, await _application.GetTodaySummaryAsync(cancellationToken)),
                RuntimeOperation.SearchQueryV1 => await DispatchSearchAsync(request, cancellationToken),
                RuntimeOperation.SearchSuggestV2 => await DispatchSearchSuggestionsAsync(request, cancellationToken),
                RuntimeOperation.SearchAvailabilityV1 => ToResponse(request, await _application.GetSearchAvailabilityAsync(cancellationToken)),
                RuntimeOperation.SearchRebuildV1 => ToResponse(request, await _application.RebuildSearchIndexAsync(cancellationToken)),
                RuntimeOperation.SystemSnapshot => ToResponse(request, await _application.CaptureSystemSnapshotAsync(cancellationToken)),
                RuntimeOperation.ScreenshotCapture => await DispatchScreenshotCaptureAsync(request, cancellationToken),
                RuntimeOperation.ScreenshotManualCapture => ToResponse(request, await _application.CaptureManualScreenshotAsync(cancellationToken)),
                RuntimeOperation.ScreenshotManualDelete => ToResponse(request, await _application.DeletePendingManualScreenshotAsync(cancellationToken)),
                RuntimeOperation.ScreenshotAnalyze => await DispatchScreenshotAnalysisAsync(request, cancellationToken),
                RuntimeOperation.ScreenshotLatest => ToResponse(request, await _application.GetLatestScreenshotAsync(cancellationToken)),
                RuntimeOperation.ScreenshotGallery => ToResponse(request, await DispatchScreenshotGalleryAsync(request, cancellationToken)),
                RuntimeOperation.ScreenshotGalleryLatest => ToResponse(request, await _application.GetLatestScreenshotGalleryAsync(cancellationToken)),
                RuntimeOperation.ScreenshotStorageMigrationStatusV1 => ToResponse(request, await _application.GetScreenshotStorageMigrationStatusAsync(cancellationToken)),
                RuntimeOperation.ScreenshotStorageMigrationRunV1 => ToResponse(request, await _application.MigrateScreenshotStorageAsync(cancellationToken)),
                RuntimeOperation.InstallationsListV1 => ToResponse(request, await _application.GetInstallationProfilesAsync(cancellationToken)),
                RuntimeOperation.InstallationsUpdateV1 => ToResponse(request, await _application.UpdateInstallationProfileAsync(
                    Read<UpdateInstallationProfileRequest>(request.Payload)
                        ?? throw new InvalidDataException("An installation profile update payload is required."),
                    cancellationToken)),
                RuntimeOperation.ArchiveExportV1 => ToResponse(request, await _application.ExportDataArchiveAsync(
                    Read<DataArchiveExportRequest>(request.Payload)
                        ?? throw new InvalidDataException("An archive export payload is required."),
                    cancellationToken)),
                RuntimeOperation.ArchiveImportPreviewV1 => ToResponse(request, await _application.PreviewDataArchiveImportAsync(
                    Read<DataArchiveImportPreviewRequest>(request.Payload)
                        ?? throw new InvalidDataException("An archive import preview payload is required."),
                    cancellationToken)),
                RuntimeOperation.ArchiveImportMergeV1 => ToResponse(request, await _application.ImportDataArchiveAsync(
                    Read<DataArchiveImportRequest>(request.Payload)
                        ?? throw new InvalidDataException("An archive import payload is required."),
                    cancellationToken)),
                RuntimeOperation.ScreenshotDelete => ToResponse(request, await _application.DeleteScreenshotAsync(ReadString(request.Payload, "screenshotPath"), cancellationToken)),
                RuntimeOperation.ScreenshotAnalysisDeleteV1 => ToResponse(request, await _application.DeleteScreenshotAnalysisAsync(ReadString(request.Payload, "screenshotPath"), cancellationToken)),
                RuntimeOperation.ScreenshotSave => ToResponse(request, await _application.SaveScreenshotAsync(ReadString(request.Payload, "screenshotPath"), ReadString(request.Payload, "destinationPath"), cancellationToken)),
                RuntimeOperation.ScreenshotShare => ToResponse(request, await _application.ShareScreenshotAsync(ReadString(request.Payload, "screenshotPath"), ReadInt64(request.Payload, "windowHandle"), cancellationToken)),
                RuntimeOperation.DiagnosticsLogOpen => ToResponse(request, await _application.OpenApplicationLogAsync(cancellationToken)),
                RuntimeOperation.DiagnosticsLogOpenFolder => ToResponse(request, await _application.OpenApplicationLogFolderAsync(cancellationToken)),
                RuntimeOperation.DiagnosticsLogShare => ToResponse(request, await _application.ShareApplicationLogAsync(ReadInt64(request.Payload, "windowHandle"), cancellationToken)),
                RuntimeOperation.ScreenshotOpenFolder => ToResponse(request, await DispatchOpenScreenshotFolderAsync(request, cancellationToken)),
                RuntimeOperation.NotificationsDrain => ToResponse(request, await _application.DrainApplicationNotificationsAsync(cancellationToken)),
                RuntimeOperation.AiStatus => ToResponse(request, await _application.GetAiStatusAsync(cancellationToken)),
                RuntimeOperation.AiPricingOverview => ToResponse(request, await _application.GetAiPricingOverviewAsync(cancellationToken)),
                RuntimeOperation.AiConnectionTest => ToResponse(request, await _application.TestAiConnectionAsync(cancellationToken)),
                RuntimeOperation.AiScreenshotReprocessPreviewV1 => await DispatchAiScreenshotReprocessPreviewAsync(request, cancellationToken),
                RuntimeOperation.AiScreenshotReprocessStartV1 => ToResponse(request, await _application.StartAiScreenshotReprocessingAsync(ReadGuid(request.Payload, "planId"), cancellationToken)),
                RuntimeOperation.AiScreenshotReprocessStatusV1 => ToResponse(request, await _application.GetAiScreenshotReprocessingJobAsync(ReadGuid(request.Payload, "jobId"), cancellationToken)),
                RuntimeOperation.AiScreenshotReprocessPauseV1 => ToResponse(request, await _application.PauseAiScreenshotReprocessingAsync(ReadGuid(request.Payload, "jobId"), cancellationToken)),
                RuntimeOperation.AiScreenshotReprocessResumeV1 => ToResponse(request, await _application.ResumeAiScreenshotReprocessingAsync(ReadGuid(request.Payload, "jobId"), cancellationToken)),
                RuntimeOperation.AiModels => ToResponse(request, await _application.GetAiModelCatalogAsync(cancellationToken)),
                RuntimeOperation.AiEnable => ToResponse(request, await _application.SetAiEnabledAsync(true, cancellationToken)),
                RuntimeOperation.AiDisable => ToResponse(request, await _application.SetAiEnabledAsync(false, cancellationToken)),
                RuntimeOperation.AiConfigure => ToResponse(request, await _application.ConfigureAiAsync(Read<SettingsPatch>(request.Payload) ?? new SettingsPatch(new Dictionary<string, string?>()), cancellationToken)),
                RuntimeOperation.AiKeySet => ToResponse(request, await _application.SetAiKeyAsync(ReadString(request.Payload, "keyVariable"), ReadString(request.Payload, "secret"), cancellationToken)),
                RuntimeOperation.AiAnalyze => ToResponse(request, await _application.AnalyzeCurrentActivityAsync(Read<AnalyzeCurrentActivityRequest>(request.Payload) ?? new AnalyzeCurrentActivityRequest(), cancellationToken)),
                RuntimeOperation.ReportQueryV1 => await DispatchReportQueryAsync(request, cancellationToken),
                RuntimeOperation.ReportToday => ToResponse(request, await _application.GenerateTodayReportAsync(ReadStringOrNull(request.Payload, "outputDirectory"), ReadBool(request.Payload, "open"), cancellationToken)),
                RuntimeOperation.ReportDigest => await DispatchDailyDigestAsync(request, cancellationToken),
                RuntimeOperation.ReportOpenFolder => ToResponse(request, await _application.OpenReportsFolderAsync(cancellationToken)),
                RuntimeOperation.UiOpen => ToResponse(request, await _application.OpenUserInterfaceAsync(cancellationToken)),
                RuntimeOperation.PrivacyList => ToResponse(request, await _application.GetPrivacyRulesAsync(cancellationToken)),
                RuntimeOperation.PrivacyAdd => ToResponse(request, await _application.AddPrivacyRuleAsync(ReadString(request.Payload, "type"), ReadString(request.Payload, "value"), cancellationToken)),
                RuntimeOperation.PrivacyRemove => ToResponse(request, await _application.RemovePrivacyRuleAsync(ReadString(request.Payload, "id"), cancellationToken)),
                RuntimeOperation.PrivacyTestCurrent => ToResponse(request, await _application.TestCurrentPrivacyAsync(cancellationToken)),
                RuntimeOperation.RetentionStatus => ToResponse(request, await _application.GetRetentionStatusAsync(cancellationToken)),
                RuntimeOperation.RetentionPreview => ToResponse(request, await _application.PreviewRetentionAsync(cancellationToken)),
                RuntimeOperation.RetentionRun => ToResponse(request, await _application.RunRetentionAsync(Read<RetentionRequest>(request.Payload) ?? new RetentionRequest(false, false), cancellationToken)),
                RuntimeOperation.AppAtomicResetV1 => ToResponse(request, await _application.PrepareAtomicResetAsync(
                    Read<AtomicResetRequest>(request.Payload) ?? new AtomicResetRequest(false, false),
                    cancellationToken)),
                RuntimeOperation.PluginsList => ToResponse(request, await _application.GetPluginsAsync(cancellationToken)),
                RuntimeOperation.PluginsShow => ToResponse(request, await _application.GetPluginAsync(ReadString(request.Payload, "id"), cancellationToken)),
                RuntimeOperation.PluginsEnable => ToResponse(request, await _application.SetPluginEnabledAsync(ReadString(request.Payload, "id"), true, cancellationToken)),
                RuntimeOperation.PluginsDisable => ToResponse(request, await _application.SetPluginEnabledAsync(ReadString(request.Payload, "id"), false, cancellationToken)),
                RuntimeOperation.SettingsGet => ToResponse(request, await _application.GetSettingsAsync(cancellationToken)),
                RuntimeOperation.QuickSetupApplyV1 => ToResponse(request, await _application.ApplyQuickSetupProfileAsync(
                    Read<QuickSetupProfileRequest>(request.Payload) ?? new QuickSetupProfileRequest(string.Empty, false),
                    cancellationToken)),
                RuntimeOperation.SettingsPatch => ToResponse(request, await _application.PatchSettingsAsync(Read<SettingsPatch>(request.Payload) ?? new SettingsPatch(new Dictionary<string, string?>()), cancellationToken)),
                RuntimeOperation.WindowStateRestore => ToResponse(request, await _application.RestoreWindowStateAsync(ReadString(request.Payload, "windowKey"), ReadInt64(request.Payload, "windowHandle"), cancellationToken)),
                RuntimeOperation.WindowStateSave => ToResponse(request, await _application.SaveWindowStateAsync(ReadString(request.Payload, "windowKey"), ReadInt64(request.Payload, "windowHandle"), cancellationToken)),
                RuntimeOperation.StartupStatus => ToResponse(request, await _application.GetStartupStatusAsync(cancellationToken)),
                RuntimeOperation.StartupEnable => ToResponse(request, await _application.SetStartupEnabledAsync(true, cancellationToken)),
                RuntimeOperation.StartupDisable => ToResponse(request, await _application.SetStartupEnabledAsync(false, cancellationToken)),
                RuntimeOperation.ProductGet => ToResponse(request, await _application.GetProductInformationAsync(cancellationToken)),
                RuntimeOperation.ProductLinkOpen => ToResponse(request, await _application.OpenProductLinkAsync(ReadString(request.Payload, "linkKey"), cancellationToken)),
                _ => throw new InvalidOperationException($"Runtime operation '{operation}' has no host dispatch handler.")
            };
        }
        catch (OperationCanceledException)
        {
            return Failure(request, "operation.cancelled", "OperationCancelled");
        }
        catch (Exception exception)
        {
            // Never serialize exception messages: they can disclose local paths or external provider details.
            _logger.LogWarning("Runtime operation failed. Operation={Operation} ExceptionType={ExceptionType}", request.Operation, exception.GetType().Name);
            return Failure(request, "runtime.operation.failed", "RuntimeOperationFailed");
        }
    }

    private static RuntimeResponseEnvelope ToResponse<T>(RuntimeRequestEnvelope request, OperationResult<T> result) => new(RuntimeProtocol.ProtocolVersion, request.RequestId, result.Succeeded, result.Code, result.MessageKey, result.Value, result.Issues);

    private async Task<OperationResult<ScreenshotGallery>> DispatchScreenshotGalleryAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var galleryRequest = Read<ScreenshotGalleryRequest>(request.Payload);
        return galleryRequest is null
            ? OperationResult<ScreenshotGallery>.Failure("screenshot.gallery.invalid", "ScreenshotGalleryRequestInvalid")
            : await _application.GetScreenshotGalleryAsync(galleryRequest.Date, cancellationToken);
    }

    private async Task<RuntimeResponseEnvelope> DispatchSearchAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var searchRequest = Read<SearchRequest>(request.Payload);
        return searchRequest is null
            ? Failure(request, "search.query.invalid", "SearchQueryInvalid")
            : ToResponse(request, await _application.SearchAsync(searchRequest, cancellationToken));
    }

    private async Task<RuntimeResponseEnvelope> DispatchSearchSuggestionsAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var suggestionRequest = Read<SearchSuggestionRequest>(request.Payload);
        return suggestionRequest is null
            ? Failure(request, "search.suggestions.invalid", "SearchQueryInvalid")
            : ToResponse(request, await _application.GetSearchSuggestionsAsync(suggestionRequest, cancellationToken));
    }

    private async Task<RuntimeResponseEnvelope> DispatchScreenshotCaptureAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var captureRequest = Read<CaptureScreenshotRequest>(request.Payload);
        return captureRequest is null
            ? Failure(request, "screenshot.capture.invalid", "ScreenshotCaptureRequestInvalid")
            : ToResponse(request, await _application.CaptureScreenshotAsync(captureRequest, cancellationToken));
    }

    private async Task<RuntimeResponseEnvelope> DispatchScreenshotAnalysisAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var analysisRequest = Read<AnalyzeCapturedScreenshotRequest>(request.Payload);
        return analysisRequest is null
            ? Failure(request, "screenshot.analysis.invalid", "AiConfigurationInvalid")
            : ToResponse(request, await _application.AnalyzeCapturedScreenshotAsync(analysisRequest, cancellationToken));
    }

    private async Task<RuntimeResponseEnvelope> DispatchAiScreenshotReprocessPreviewAsync(
        RuntimeRequestEnvelope request,
        CancellationToken cancellationToken)
    {
        var previewRequest = Read<AiScreenshotReprocessRequest>(request.Payload);
        return previewRequest is null
            ? Failure(request, "ai.screenshot_reprocess.preview.invalid", "AiScreenshotReprocessInvalid")
            : ToResponse(request, await _application.PreviewAiScreenshotReprocessingAsync(previewRequest, cancellationToken));
    }

    private async Task<RuntimeResponseEnvelope> DispatchDailyDigestAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var digest = Read<GenerateDailyDigestRequest>(request.Payload);
        if (digest is null)
        {
            return Failure(request, "command.arguments.invalid", "InvalidDigestDate");
        }

        return ToResponse(request, await _application.GenerateDailyDigestAsync(digest.Date, digest.Open, cancellationToken));
    }

    private async Task<RuntimeResponseEnvelope> DispatchReportQueryAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var query = Read<ReportQuery>(request.Payload);
        if (query is null)
        {
            return Failure(request, "command.arguments.invalid", "InvalidReportQuery");
        }

        return ToResponse(request, await _application.GetReportAsync(query, cancellationToken));
    }

    private static RuntimeResponseEnvelope Failure(RuntimeRequestEnvelope request, string code, string messageKey) => new(RuntimeProtocol.ProtocolVersion, request.RequestId, false, code, messageKey, null, Array.Empty<ValidationIssue>());

    private static T? Read<T>(JsonElement value) => value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? default : value.Deserialize<T>(RuntimeProtocol.SerializerOptions);

    private static string ReadString(JsonElement value, string name) => ReadStringOrNull(value, name) ?? string.Empty;

    private static string? ReadStringOrNull(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static Guid ReadGuid(JsonElement value, string name) =>
        Guid.TryParse(ReadStringOrNull(value, name), out var parsed) ? parsed : Guid.Empty;

    private Task<OperationResult<string>> DispatchOpenScreenshotFolderAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
        => ReadStringOrNull(request.Payload, "directory") is { } directory
            ? _application.OpenScreenshotFolderAsync(directory, cancellationToken)
            : _application.OpenScreenshotFolderAsync(cancellationToken);

    private static bool ReadBool(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False && property.GetBoolean();

    private static long ReadInt64(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var result) ? result : 0L;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RuntimeHost));
        }
    }

    /// <summary>
    /// Keeps named-mutex acquisition and release on one dedicated thread because Windows mutex ownership is thread-affine.
    /// </summary>
    private sealed class RuntimeMutexLease : IDisposable
    {
        private readonly ManualResetEventSlim _ready = new(false);
        private readonly ManualResetEventSlim _release = new(false);
        private readonly Thread _ownerThread;
        private Exception? _failure;
        private bool _disposed;

        internal RuntimeMutexLease(string mutexName)
        {
            _ownerThread = new Thread(() => Own(mutexName))
            {
                IsBackground = true,
                Name = "TrackMeUp runtime mutex"
            };
            _ownerThread.Start();
            _ready.Wait();
            if (_failure is not null)
            {
                Dispose();
                throw new InvalidOperationException("Unable to acquire the TrackMeUp runtime mutex.", _failure);
            }
        }

        internal bool Acquired { get; private set; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _release.Set();
            _ownerThread.Join();
            _release.Dispose();
            _ready.Dispose();
        }

        private void Own(string mutexName)
        {
            try
            {
                using var mutex = new Mutex(false, mutexName);
                try
                {
                    Acquired = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    Acquired = true;
                }

                _ready.Set();
                if (!Acquired)
                {
                    return;
                }

                _release.Wait();
                mutex.ReleaseMutex();
            }
            catch (Exception exception)
            {
                _failure = exception;
                _ready.Set();
            }
        }
    }
}

/// <summary>Provides a typed application facade backed by the local runtime pipe.</summary>
public sealed class RuntimeClient : ITrackMeUpApplication
{
    private static readonly TimeSpan ReportQueryTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ScreenshotAnalysisTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ScreenshotReprocessPreviewTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ScreenshotStorageMigrationTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DataArchiveTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SearchTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StartupMutationTimeout = TimeSpan.FromMinutes(2);
    internal static readonly TimeSpan WorldClockQueryTimeout = TimeSpan.FromSeconds(15);
    private readonly RuntimeEndpoint _endpoint;
    private readonly TimeSpan _timeout;
    private readonly ILogger<RuntimeClient> _logger;

    /// <summary>Initializes a client for the supplied installation identifier.</summary>
    public RuntimeClient(string installationId, TimeSpan timeout, ILogger<RuntimeClient>? logger = null)
    {
        _endpoint = RuntimeProtocol.CreateEndpoint(installationId);
        _timeout = timeout;
        _logger = logger ?? NullLogger<RuntimeClient>.Instance;
    }

    /// <inheritdoc />
    public event EventHandler<RuntimeStateChangedEventArgs>? RuntimeStateChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public Task<OperationResult<RuntimeHealth>> GetRuntimeHealthAsync(CancellationToken cancellationToken) => SendAsync<RuntimeHealth>(RuntimeOperation.RuntimeHealth, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> StartTrackingAsync(StartTrackingRequest request, CancellationToken cancellationToken) => SendAsync<DashboardState>(RuntimeOperation.TrackingStart, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> PauseTrackingAsync(CancellationToken cancellationToken) => SendAsync<DashboardState>(RuntimeOperation.TrackingPause, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> ToggleTrackingAsync(CancellationToken cancellationToken) => SendAsync<DashboardState>(RuntimeOperation.TrackingToggle, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> GetDashboardAsync(CancellationToken cancellationToken) => SendAsync<DashboardState>(RuntimeOperation.DashboardGet, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<WorldClockSnapshot>> GetWorldClocksAsync(CancellationToken cancellationToken) =>
        SendAsync<WorldClockSnapshot>(RuntimeOperation.WorldClocksGetV2, null, cancellationToken, WorldClockQueryTimeout);
    /// <inheritdoc />
    public Task<OperationResult<WorldClockSnapshot>> ConvertWorldClocksAsync(WorldClockConversionRequest request, CancellationToken cancellationToken) =>
        SendAsync<WorldClockSnapshot>(RuntimeOperation.WorldClocksConvertV1, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<WorldClockCityCatalog>> GetWorldClockCityCatalogAsync(CancellationToken cancellationToken) => SendAsync<WorldClockCityCatalog>(RuntimeOperation.WorldClocksCatalogV1, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<WorldClockSelectionState>> AddWorldClockAsync(string cityId, CancellationToken cancellationToken) =>
        SendAsync<WorldClockSelectionState>(RuntimeOperation.WorldClocksAddV3, new { cityId }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<WorldClockSelectionState>> RemoveWorldClockAsync(string cityId, CancellationToken cancellationToken) =>
        SendAsync<WorldClockSelectionState>(RuntimeOperation.WorldClocksRemoveV3, new { cityId }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> SetWorldClockWeatherKeyAsync(string secret, CancellationToken cancellationToken) =>
        SendAsync<string>(RuntimeOperation.WorldClocksWeatherKeySetV1, new { secret }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<LastSessionState?>> GetLastSessionAsync(CancellationToken cancellationToken) => SendAsync<LastSessionState?>(RuntimeOperation.SessionLast, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DailySummary>> GetTodaySummaryAsync(CancellationToken cancellationToken) => SendAsync<DailySummary>(RuntimeOperation.SessionToday, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<SearchResponse>> SearchAsync(SearchRequest request, CancellationToken cancellationToken) =>
        SendAsync<SearchResponse>(RuntimeOperation.SearchQueryV1, request, cancellationToken, SearchTimeout);
    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<SearchSuggestion>>> GetSearchSuggestionsAsync(SearchSuggestionRequest request, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<SearchSuggestion>>(RuntimeOperation.SearchSuggestV2, request, cancellationToken, SearchTimeout);
    /// <inheritdoc />
    public Task<OperationResult<SearchAvailability>> GetSearchAvailabilityAsync(CancellationToken cancellationToken) =>
        SendAsync<SearchAvailability>(RuntimeOperation.SearchAvailabilityV1, null, cancellationToken, SearchTimeout);
    /// <inheritdoc />
    public Task<OperationResult<int>> RebuildSearchIndexAsync(CancellationToken cancellationToken) =>
        SendAsync<int>(RuntimeOperation.SearchRebuildV1, null, cancellationToken, SearchTimeout);
    /// <inheritdoc />
    public Task<OperationResult<ReportSnapshot>> GetReportAsync(ReportQuery query, CancellationToken cancellationToken) =>
        SendAsync<ReportSnapshot>(RuntimeOperation.ReportQueryV1, query, cancellationToken, ReportQueryTimeout);
    /// <inheritdoc />
    public Task<OperationResult<SystemSnapshot>> CaptureSystemSnapshotAsync(CancellationToken cancellationToken) => SendAsync<SystemSnapshot>(RuntimeOperation.SystemSnapshot, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ScreenshotCaptureResult>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken) => SendAsync<ScreenshotCaptureResult>(RuntimeOperation.ScreenshotCapture, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<PendingManualScreenshotState>> CaptureManualScreenshotAsync(CancellationToken cancellationToken) => SendAsync<PendingManualScreenshotState>(RuntimeOperation.ScreenshotManualCapture, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> DeletePendingManualScreenshotAsync(CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.ScreenshotManualDelete, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiAnalysis>> AnalyzeCapturedScreenshotAsync(AnalyzeCapturedScreenshotRequest request, CancellationToken cancellationToken) => SendAsync<AiAnalysis>(RuntimeOperation.ScreenshotAnalyze, request, cancellationToken, ScreenshotAnalysisTimeout);
    /// <inheritdoc />
    public Task<OperationResult<string?>> GetLatestScreenshotAsync(CancellationToken cancellationToken) => SendAsync<string?>(RuntimeOperation.ScreenshotLatest, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ScreenshotGallery>> GetScreenshotGalleryAsync(DateOnly date, CancellationToken cancellationToken) => SendAsync<ScreenshotGallery>(RuntimeOperation.ScreenshotGallery, new ScreenshotGalleryRequest(date), cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ScreenshotGallery>> GetLatestScreenshotGalleryAsync(CancellationToken cancellationToken) => SendAsync<ScreenshotGallery>(RuntimeOperation.ScreenshotGalleryLatest, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ScreenshotStorageMigrationStatus>> GetScreenshotStorageMigrationStatusAsync(CancellationToken cancellationToken) =>
        SendAsync<ScreenshotStorageMigrationStatus>(RuntimeOperation.ScreenshotStorageMigrationStatusV1, null, cancellationToken, ScreenshotStorageMigrationTimeout);
    /// <inheritdoc />
    public Task<OperationResult<ScreenshotStorageMigrationResult>> MigrateScreenshotStorageAsync(CancellationToken cancellationToken) =>
        SendAsync<ScreenshotStorageMigrationResult>(RuntimeOperation.ScreenshotStorageMigrationRunV1, null, cancellationToken, ScreenshotStorageMigrationTimeout);
    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<InstallationProfile>>> GetInstallationProfilesAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<InstallationProfile>>(RuntimeOperation.InstallationsListV1, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<InstallationProfile>> UpdateInstallationProfileAsync(
        UpdateInstallationProfileRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<InstallationProfile>(RuntimeOperation.InstallationsUpdateV1, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DataArchiveExportResult>> ExportDataArchiveAsync(
        DataArchiveExportRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<DataArchiveExportResult>(RuntimeOperation.ArchiveExportV1, request, cancellationToken, DataArchiveTimeout);
    /// <inheritdoc />
    public Task<OperationResult<DataArchiveImportPlan>> PreviewDataArchiveImportAsync(
        DataArchiveImportPreviewRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<DataArchiveImportPlan>(RuntimeOperation.ArchiveImportPreviewV1, request, cancellationToken, DataArchiveTimeout);
    /// <inheritdoc />
    public Task<OperationResult<DataArchiveImportResult>> ImportDataArchiveAsync(
        DataArchiveImportRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<DataArchiveImportResult>(RuntimeOperation.ArchiveImportMergeV1, request, cancellationToken, DataArchiveTimeout);
    /// <inheritdoc />
    public Task<OperationResult<string>> DeleteScreenshotAsync(string screenshotPath, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ScreenshotDelete, new { screenshotPath }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> DeleteScreenshotAnalysisAsync(string screenshotPath, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ScreenshotAnalysisDeleteV1, new { screenshotPath }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> SaveScreenshotAsync(string screenshotPath, string destinationPath, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ScreenshotSave, new { screenshotPath, destinationPath }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> ShareScreenshotAsync(string screenshotPath, long windowHandle, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ScreenshotShare, new { screenshotPath, windowHandle }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> OpenApplicationLogAsync(CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.DiagnosticsLogOpen, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> OpenApplicationLogFolderAsync(CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.DiagnosticsLogOpenFolder, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> ShareApplicationLogAsync(long windowHandle, CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.DiagnosticsLogShare, new { windowHandle }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> OpenScreenshotFolderAsync(CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ScreenshotOpenFolder, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> OpenScreenshotFolderAsync(string directory, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ScreenshotOpenFolder, new { directory }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<ApplicationNotification>>> DrainApplicationNotificationsAsync(CancellationToken cancellationToken) => SendAsync<IReadOnlyList<ApplicationNotification>>(RuntimeOperation.NotificationsDrain, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiStatus>> GetAiStatusAsync(CancellationToken cancellationToken) => SendAsync<AiStatus>(RuntimeOperation.AiStatus, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiPricingOverview>> GetAiPricingOverviewAsync(CancellationToken cancellationToken) => SendAsync<AiPricingOverview>(RuntimeOperation.AiPricingOverview, null, cancellationToken, ReportQueryTimeout);
    /// <inheritdoc />
    public Task<OperationResult<AiConnectionTestResult>> TestAiConnectionAsync(CancellationToken cancellationToken) => SendAsync<AiConnectionTestResult>(RuntimeOperation.AiConnectionTest, null, cancellationToken, TimeSpan.FromSeconds(35));
    /// <inheritdoc />
    public Task<OperationResult<AiScreenshotReprocessPlan>> PreviewAiScreenshotReprocessingAsync(
        AiScreenshotReprocessRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiScreenshotReprocessPlan>(RuntimeOperation.AiScreenshotReprocessPreviewV1, request, cancellationToken, ScreenshotReprocessPreviewTimeout);
    /// <inheritdoc />
    public Task<OperationResult<AiScreenshotReprocessJobSnapshot>> StartAiScreenshotReprocessingAsync(Guid planId, CancellationToken cancellationToken) =>
        SendAsync<AiScreenshotReprocessJobSnapshot>(RuntimeOperation.AiScreenshotReprocessStartV1, new { planId }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiScreenshotReprocessJobSnapshot>> GetAiScreenshotReprocessingJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        SendAsync<AiScreenshotReprocessJobSnapshot>(RuntimeOperation.AiScreenshotReprocessStatusV1, new { jobId }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiScreenshotReprocessJobSnapshot>> PauseAiScreenshotReprocessingAsync(Guid jobId, CancellationToken cancellationToken) =>
        SendAsync<AiScreenshotReprocessJobSnapshot>(RuntimeOperation.AiScreenshotReprocessPauseV1, new { jobId }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiScreenshotReprocessJobSnapshot>> ResumeAiScreenshotReprocessingAsync(Guid jobId, CancellationToken cancellationToken) =>
        SendAsync<AiScreenshotReprocessJobSnapshot>(RuntimeOperation.AiScreenshotReprocessResumeV1, new { jobId }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiModelCatalogSnapshot>> GetAiModelCatalogAsync(CancellationToken cancellationToken) => SendAsync<AiModelCatalogSnapshot>(RuntimeOperation.AiModels, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiStatus>> SetAiEnabledAsync(bool enabled, CancellationToken cancellationToken) => SendAsync<AiStatus>(enabled ? RuntimeOperation.AiEnable : RuntimeOperation.AiDisable, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> ConfigureAiAsync(SettingsPatch patch, CancellationToken cancellationToken) => SendAsync<AppSettings>(RuntimeOperation.AiConfigure, patch, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> SetAiKeyAsync(string keyVariable, string secret, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.AiKeySet, new { keyVariable, secret }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiAnalysis>> AnalyzeCurrentActivityAsync(AnalyzeCurrentActivityRequest request, CancellationToken cancellationToken) => SendAsync<AiAnalysis>(RuntimeOperation.AiAnalyze, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> GenerateTodayReportAsync(string? outputDirectory, bool open, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ReportToday, new { outputDirectory, open }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> GenerateDailyDigestAsync(DateOnly date, bool open, CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ReportDigest, new GenerateDailyDigestRequest(date, open), cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> OpenReportsFolderAsync(CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.ReportOpenFolder, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> OpenUserInterfaceAsync(CancellationToken cancellationToken) => SendAsync<string>(RuntimeOperation.UiOpen, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<PrivacyRule>>> GetPrivacyRulesAsync(CancellationToken cancellationToken) => SendAsync<IReadOnlyList<PrivacyRule>>(RuntimeOperation.PrivacyList, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<PrivacyRule>> AddPrivacyRuleAsync(string type, string value, CancellationToken cancellationToken) => SendAsync<PrivacyRule>(RuntimeOperation.PrivacyAdd, new { type, value }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> RemovePrivacyRuleAsync(string id, CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.PrivacyRemove, new { id }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> TestCurrentPrivacyAsync(CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.PrivacyTestCurrent, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<RetentionStatus>> GetRetentionStatusAsync(CancellationToken cancellationToken) => SendAsync<RetentionStatus>(RuntimeOperation.RetentionStatus, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<RetentionPreview>> PreviewRetentionAsync(CancellationToken cancellationToken) => SendAsync<RetentionPreview>(RuntimeOperation.RetentionPreview, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<RetentionPreview>> RunRetentionAsync(RetentionRequest request, CancellationToken cancellationToken) => SendAsync<RetentionPreview>(RuntimeOperation.RetentionRun, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AtomicResetPlan>> PrepareAtomicResetAsync(AtomicResetRequest request, CancellationToken cancellationToken) =>
        SendAsync<AtomicResetPlan>(RuntimeOperation.AppAtomicResetV1, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<PluginInfo>>> GetPluginsAsync(CancellationToken cancellationToken) => SendAsync<IReadOnlyList<PluginInfo>>(RuntimeOperation.PluginsList, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<PluginInfo>> GetPluginAsync(string id, CancellationToken cancellationToken) => SendAsync<PluginInfo>(RuntimeOperation.PluginsShow, new { id }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<PluginInfo>> SetPluginEnabledAsync(string id, bool enabled, CancellationToken cancellationToken) => SendAsync<PluginInfo>(enabled ? RuntimeOperation.PluginsEnable : RuntimeOperation.PluginsDisable, new { id }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> GetSettingsAsync(CancellationToken cancellationToken) => SendAsync<AppSettings>(RuntimeOperation.SettingsGet, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> ApplyQuickSetupProfileAsync(QuickSetupProfileRequest request, CancellationToken cancellationToken) => SendAsync<AppSettings>(RuntimeOperation.QuickSetupApplyV1, request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> PatchSettingsAsync(SettingsPatch patch, CancellationToken cancellationToken) => SendAsync<AppSettings>(RuntimeOperation.SettingsPatch, patch, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<WindowState?>> RestoreWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken) => SendAsync<WindowState?>(RuntimeOperation.WindowStateRestore, new { windowKey, windowHandle }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<WindowState>> SaveWindowStateAsync(string windowKey, long windowHandle, CancellationToken cancellationToken) => SendAsync<WindowState>(RuntimeOperation.WindowStateSave, new { windowKey, windowHandle }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> GetStartupStatusAsync(CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.StartupStatus, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> SetStartupEnabledAsync(bool enabled, CancellationToken cancellationToken) =>
        SendAsync<bool>(enabled ? RuntimeOperation.StartupEnable : RuntimeOperation.StartupDisable, null, cancellationToken, StartupMutationTimeout);
    /// <inheritdoc />
    public Task<OperationResult<ProductInformation>> GetProductInformationAsync(CancellationToken cancellationToken) => SendAsync<ProductInformation>(RuntimeOperation.ProductGet, null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> OpenProductLinkAsync(string linkKey, CancellationToken cancellationToken) => SendAsync<bool>(RuntimeOperation.ProductLinkOpen, new { linkKey }, cancellationToken);
    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<OperationResult<T>> SendAsync<T>(
        RuntimeOperation operation,
        object? payload,
        CancellationToken cancellationToken,
        TimeSpan? requestTimeout = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout ?? _timeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(".", _endpoint.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var wireName = RuntimeOperationCatalog.GetWireName(operation);
            var request = new RuntimeRequestEnvelope(RuntimeProtocol.ProtocolVersion, Guid.NewGuid(), wireName, JsonSerializer.SerializeToElement(payload, RuntimeProtocol.SerializerOptions), null, null);
            await RuntimeProtocol.WriteAsync(pipe, request, timeout.Token).ConfigureAwait(false);
            var response = await RuntimeProtocol.ReadAsync<RuntimeResponseEnvelope>(pipe, timeout.Token).ConfigureAwait(false);
            if (response.ProtocolVersion != RuntimeProtocol.ProtocolVersion)
            {
                return OperationResult<T>.Failure("ipc.protocol.unsupported", "IpcProtocolUnsupported");
            }

            var value = response.Payload is null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(response.Payload, RuntimeProtocol.SerializerOptions), RuntimeProtocol.SerializerOptions);
            return new OperationResult<T>(response.Succeeded, response.Code, response.MessageKey, value, response.Issues);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<T>.Failure(cancellationToken.IsCancellationRequested ? "operation.cancelled" : "runtime.unavailable", cancellationToken.IsCancellationRequested ? "OperationCancelled" : "RuntimeUnavailable");
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Runtime pipe request was unavailable. Operation={Operation}",
                RuntimeOperationCatalog.GetWireName(operation));
            return OperationResult<T>.Failure("runtime.unavailable", "RuntimeUnavailable");
        }
    }
}
