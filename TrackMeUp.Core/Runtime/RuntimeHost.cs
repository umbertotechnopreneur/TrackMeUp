using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Runtime;

/// <summary>Owns the single local runtime and serves versioned requests over a same-user named pipe.</summary>
public sealed class RuntimeHost : IAsyncDisposable
{
    private readonly ITrackMeUpApplication _application;
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
        _application = application;
        _endpoint = RuntimeProtocol.CreateEndpoint(installationId);
        _logger = logger ?? NullLogger<RuntimeHost>.Instance;
    }

    /// <summary>Gets the endpoint used by this host.</summary>
    public RuntimeEndpoint Endpoint => _endpoint;

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

        // One background loop owns pipe acceptance; mutations themselves remain serialized in the facade.
        _serverTask = Task.Run(() => ServeAsync(_shutdown.Token));
        _logger.LogInformation("Runtime host started. Pipe={PipeName}", _endpoint.PipeName);
        return true;
    }

    /// <summary>Stops the named-pipe server and releases the runtime mutex.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
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
        }

        var activeRequests = _activeRequests.Values.ToArray();
        if (activeRequests.Length > 0)
        {
            await Task.WhenAll(activeRequests);
        }

        _mutexLease?.Dispose();
        _mutexLease = null;

        _shutdown.Dispose();
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

        try
        {
            return request.Operation switch
            {
                "runtime.health" => ToResponse(request, await _application.GetRuntimeHealthAsync(cancellationToken)),
                "tracking.start" => ToResponse(request, await _application.StartTrackingAsync(Read<StartTrackingRequest>(request.Payload) ?? new StartTrackingRequest(), cancellationToken)),
                "tracking.pause" => ToResponse(request, await _application.PauseTrackingAsync(cancellationToken)),
                "tracking.toggle" => ToResponse(request, await _application.ToggleTrackingAsync(cancellationToken)),
                "dashboard.get" => ToResponse(request, await _application.GetDashboardAsync(cancellationToken)),
                "session.last" => ToResponse(request, await _application.GetLastSessionAsync(cancellationToken)),
                "session.today" => ToResponse(request, await _application.GetTodaySummaryAsync(cancellationToken)),
                "focus.start" => ToResponse(request, await _application.StartFocusSessionAsync(Read<StartFocusSessionRequest>(request.Payload) ?? new StartFocusSessionRequest(string.Empty), cancellationToken)),
                "focus.status" => ToResponse(request, await _application.GetFocusSessionAsync(cancellationToken)),
                "focus.stop" => ToResponse(request, await _application.StopFocusSessionAsync(ReadBool(request.Payload, "summarize"), cancellationToken)),
                "system.snapshot" => ToResponse(request, await _application.CaptureSystemSnapshotAsync(cancellationToken)),
                "screenshot.capture" => await DispatchScreenshotCaptureAsync(request, cancellationToken),
                "screenshot.latest" => ToResponse(request, await _application.GetLatestScreenshotAsync(cancellationToken)),
                "screenshot.gallery" => ToResponse(request, await DispatchScreenshotGalleryAsync(request, cancellationToken)),
                "screenshot.save" => ToResponse(request, await _application.SaveScreenshotAsync(ReadString(request.Payload, "screenshotPath"), ReadString(request.Payload, "destinationPath"), cancellationToken)),
                "screenshot.share" => ToResponse(request, await _application.ShareScreenshotAsync(ReadString(request.Payload, "screenshotPath"), ReadInt64(request.Payload, "windowHandle"), cancellationToken)),
                "screenshot.open_folder" => ToResponse(request, await _application.OpenScreenshotFolderAsync(cancellationToken)),
                "ai.status" => ToResponse(request, await _application.GetAiStatusAsync(cancellationToken)),
                "ai.enable" => ToResponse(request, await _application.SetAiEnabledAsync(true, cancellationToken)),
                "ai.disable" => ToResponse(request, await _application.SetAiEnabledAsync(false, cancellationToken)),
                "ai.configure" => ToResponse(request, await _application.ConfigureAiAsync(Read<SettingsPatch>(request.Payload) ?? new SettingsPatch(new Dictionary<string, string?>()), cancellationToken)),
                "ai.key.set" => ToResponse(request, await _application.SetAiKeyAsync(ReadString(request.Payload, "keyVariable"), ReadString(request.Payload, "secret"), cancellationToken)),
                "ai.analyze" => ToResponse(request, await _application.AnalyzeCurrentActivityAsync(Read<AnalyzeCurrentActivityRequest>(request.Payload) ?? new AnalyzeCurrentActivityRequest(), cancellationToken)),
                "report.query.v1" => await DispatchReportQueryAsync(request, cancellationToken),
                "report.today" => ToResponse(request, await _application.GenerateTodayReportAsync(ReadStringOrNull(request.Payload, "outputDirectory"), ReadBool(request.Payload, "open"), cancellationToken)),
                "report.digest" => await DispatchDailyDigestAsync(request, cancellationToken),
                "report.open_folder" => ToResponse(request, await _application.OpenReportsFolderAsync(cancellationToken)),
                "ui.open" => ToResponse(request, await _application.OpenUserInterfaceAsync(cancellationToken)),
                "privacy.list" => ToResponse(request, await _application.GetPrivacyRulesAsync(cancellationToken)),
                "privacy.add" => ToResponse(request, await _application.AddPrivacyRuleAsync(ReadString(request.Payload, "type"), ReadString(request.Payload, "value"), cancellationToken)),
                "privacy.remove" => ToResponse(request, await _application.RemovePrivacyRuleAsync(ReadString(request.Payload, "id"), cancellationToken)),
                "privacy.test_current" => ToResponse(request, await _application.TestCurrentPrivacyAsync(cancellationToken)),
                "retention.status" => ToResponse(request, await _application.GetRetentionStatusAsync(cancellationToken)),
                "retention.preview" => ToResponse(request, await _application.PreviewRetentionAsync(cancellationToken)),
                "retention.run" => ToResponse(request, await _application.RunRetentionAsync(Read<RetentionRequest>(request.Payload) ?? new RetentionRequest(false, false), cancellationToken)),
                "plugins.list" => ToResponse(request, await _application.GetPluginsAsync(cancellationToken)),
                "plugins.show" => ToResponse(request, await _application.GetPluginAsync(ReadString(request.Payload, "id"), cancellationToken)),
                "plugins.enable" => ToResponse(request, await _application.SetPluginEnabledAsync(ReadString(request.Payload, "id"), true, cancellationToken)),
                "plugins.disable" => ToResponse(request, await _application.SetPluginEnabledAsync(ReadString(request.Payload, "id"), false, cancellationToken)),
                "settings.get" => ToResponse(request, await _application.GetSettingsAsync(cancellationToken)),
                "settings.patch" => ToResponse(request, await _application.PatchSettingsAsync(Read<SettingsPatch>(request.Payload) ?? new SettingsPatch(new Dictionary<string, string?>()), cancellationToken)),
                "startup.status" => ToResponse(request, await _application.GetStartupStatusAsync(cancellationToken)),
                "startup.enable" => ToResponse(request, await _application.SetStartupEnabledAsync(true, cancellationToken)),
                "startup.disable" => ToResponse(request, await _application.SetStartupEnabledAsync(false, cancellationToken)),
                "product.get" => ToResponse(request, await _application.GetProductInformationAsync(cancellationToken)),
                _ => Failure(request, "command.invalid", "CommandInvalid")
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

    private async Task<RuntimeResponseEnvelope> DispatchScreenshotCaptureAsync(RuntimeRequestEnvelope request, CancellationToken cancellationToken)
    {
        var captureRequest = Read<CaptureScreenshotRequest>(request.Payload);
        return captureRequest is null
            ? Failure(request, "screenshot.capture.invalid", "ScreenshotCaptureRequestInvalid")
            : ToResponse(request, await _application.CaptureScreenshotAsync(captureRequest, cancellationToken));
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
    public Task<OperationResult<RuntimeHealth>> GetRuntimeHealthAsync(CancellationToken cancellationToken) => SendAsync<RuntimeHealth>("runtime.health", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> StartTrackingAsync(StartTrackingRequest request, CancellationToken cancellationToken) => SendAsync<DashboardState>("tracking.start", request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> PauseTrackingAsync(CancellationToken cancellationToken) => SendAsync<DashboardState>("tracking.pause", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> ToggleTrackingAsync(CancellationToken cancellationToken) => SendAsync<DashboardState>("tracking.toggle", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DashboardState>> GetDashboardAsync(CancellationToken cancellationToken) => SendAsync<DashboardState>("dashboard.get", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<LastSessionState?>> GetLastSessionAsync(CancellationToken cancellationToken) => SendAsync<LastSessionState?>("session.last", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<DailySummary>> GetTodaySummaryAsync(CancellationToken cancellationToken) => SendAsync<DailySummary>("session.today", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ReportSnapshot>> GetReportAsync(ReportQuery query, CancellationToken cancellationToken) =>
        SendAsync<ReportSnapshot>("report.query.v1", query, cancellationToken, ReportQueryTimeout);
    /// <inheritdoc />
    public Task<OperationResult<FocusSessionState>> StartFocusSessionAsync(StartFocusSessionRequest request, CancellationToken cancellationToken) => SendAsync<FocusSessionState>("focus.start", request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<FocusSessionState>> GetFocusSessionAsync(CancellationToken cancellationToken) => SendAsync<FocusSessionState>("focus.status", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<FocusSessionSummary?>> StopFocusSessionAsync(bool summarize, CancellationToken cancellationToken) => SendAsync<FocusSessionSummary?>("focus.stop", new { summarize }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<SystemSnapshot>> CaptureSystemSnapshotAsync(CancellationToken cancellationToken) => SendAsync<SystemSnapshot>("system.snapshot", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ScreenshotCaptureResult>> CaptureScreenshotAsync(CaptureScreenshotRequest request, CancellationToken cancellationToken) => SendAsync<ScreenshotCaptureResult>("screenshot.capture", request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string?>> GetLatestScreenshotAsync(CancellationToken cancellationToken) => SendAsync<string?>("screenshot.latest", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ScreenshotGallery>> GetScreenshotGalleryAsync(DateOnly date, CancellationToken cancellationToken) => SendAsync<ScreenshotGallery>("screenshot.gallery", new ScreenshotGalleryRequest(date), cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> SaveScreenshotAsync(string screenshotPath, string destinationPath, CancellationToken cancellationToken) => SendAsync<string>("screenshot.save", new { screenshotPath, destinationPath }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> ShareScreenshotAsync(string screenshotPath, long windowHandle, CancellationToken cancellationToken) => SendAsync<string>("screenshot.share", new { screenshotPath, windowHandle }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> OpenScreenshotFolderAsync(CancellationToken cancellationToken) => SendAsync<string>("screenshot.open_folder", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiStatus>> GetAiStatusAsync(CancellationToken cancellationToken) => SendAsync<AiStatus>("ai.status", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiStatus>> SetAiEnabledAsync(bool enabled, CancellationToken cancellationToken) => SendAsync<AiStatus>(enabled ? "ai.enable" : "ai.disable", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> ConfigureAiAsync(SettingsPatch patch, CancellationToken cancellationToken) => SendAsync<AppSettings>("ai.configure", patch, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> SetAiKeyAsync(string keyVariable, string secret, CancellationToken cancellationToken) => SendAsync<string>("ai.key.set", new { keyVariable, secret }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AiAnalysis>> AnalyzeCurrentActivityAsync(AnalyzeCurrentActivityRequest request, CancellationToken cancellationToken) => SendAsync<AiAnalysis>("ai.analyze", request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> GenerateTodayReportAsync(string? outputDirectory, bool open, CancellationToken cancellationToken) => SendAsync<string>("report.today", new { outputDirectory, open }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> GenerateDailyDigestAsync(DateOnly date, bool open, CancellationToken cancellationToken) => SendAsync<string>("report.digest", new GenerateDailyDigestRequest(date, open), cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> OpenReportsFolderAsync(CancellationToken cancellationToken) => SendAsync<string>("report.open_folder", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<string>> OpenUserInterfaceAsync(CancellationToken cancellationToken) => SendAsync<string>("ui.open", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<PrivacyRule>>> GetPrivacyRulesAsync(CancellationToken cancellationToken) => SendAsync<IReadOnlyList<PrivacyRule>>("privacy.list", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<PrivacyRule>> AddPrivacyRuleAsync(string type, string value, CancellationToken cancellationToken) => SendAsync<PrivacyRule>("privacy.add", new { type, value }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> RemovePrivacyRuleAsync(string id, CancellationToken cancellationToken) => SendAsync<bool>("privacy.remove", new { id }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> TestCurrentPrivacyAsync(CancellationToken cancellationToken) => SendAsync<bool>("privacy.test_current", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<RetentionStatus>> GetRetentionStatusAsync(CancellationToken cancellationToken) => SendAsync<RetentionStatus>("retention.status", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<RetentionPreview>> PreviewRetentionAsync(CancellationToken cancellationToken) => SendAsync<RetentionPreview>("retention.preview", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<RetentionPreview>> RunRetentionAsync(RetentionRequest request, CancellationToken cancellationToken) => SendAsync<RetentionPreview>("retention.run", request, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<IReadOnlyList<PluginInfo>>> GetPluginsAsync(CancellationToken cancellationToken) => SendAsync<IReadOnlyList<PluginInfo>>("plugins.list", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<PluginInfo>> GetPluginAsync(string id, CancellationToken cancellationToken) => SendAsync<PluginInfo>("plugins.show", new { id }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<PluginInfo>> SetPluginEnabledAsync(string id, bool enabled, CancellationToken cancellationToken) => SendAsync<PluginInfo>(enabled ? "plugins.enable" : "plugins.disable", new { id }, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> GetSettingsAsync(CancellationToken cancellationToken) => SendAsync<AppSettings>("settings.get", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<AppSettings>> PatchSettingsAsync(SettingsPatch patch, CancellationToken cancellationToken) => SendAsync<AppSettings>("settings.patch", patch, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> GetStartupStatusAsync(CancellationToken cancellationToken) => SendAsync<bool>("startup.status", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<bool>> SetStartupEnabledAsync(bool enabled, CancellationToken cancellationToken) => SendAsync<bool>(enabled ? "startup.enable" : "startup.disable", null, cancellationToken);
    /// <inheritdoc />
    public Task<OperationResult<ProductInformation>> GetProductInformationAsync(CancellationToken cancellationToken) => SendAsync<ProductInformation>("product.get", null, cancellationToken);
    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<OperationResult<T>> SendAsync<T>(
        string operation,
        object? payload,
        CancellationToken cancellationToken,
        TimeSpan? requestTimeout = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout ?? _timeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(".", _endpoint.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token);
            var request = new RuntimeRequestEnvelope(RuntimeProtocol.ProtocolVersion, Guid.NewGuid(), operation, JsonSerializer.SerializeToElement(payload, RuntimeProtocol.SerializerOptions), null, null);
            await RuntimeProtocol.WriteAsync(pipe, request, timeout.Token);
            var response = await RuntimeProtocol.ReadAsync<RuntimeResponseEnvelope>(pipe, timeout.Token);
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
            _logger.LogDebug(exception, "Runtime pipe request was unavailable. Operation={Operation}", operation);
            return OperationResult<T>.Failure("runtime.unavailable", "RuntimeUnavailable");
        }
    }
}
