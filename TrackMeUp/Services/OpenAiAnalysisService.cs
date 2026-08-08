using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrackMeUp.Services;

/// <summary>Runs AI analysis for either a newly captured or caller-supplied snapshot.</summary>
public interface IAiAnalysisService
{
    /// <summary>Captures when allowed, then analyzes the current screen context.</summary>
    /// <param name="activity">Current context sample used to build the analysis prompt.</param>
    /// <param name="allowCapture">Whether this invocation may capture screenshots when globally enabled.</param>
    /// <param name="origin">Stable analysis origin recorded with local usage and history.</param>
    /// <param name="cancellationToken">Cancels capture, device-context collection, and the provider request.</param>
    /// <returns>The AI summary record persisted in the local history store.</returns>
    Task<AiAnalysis> AnalyzeCurrentScreenAsync(
        AnalysisContextSnapshot? activity,
        bool allowCapture = true,
        string origin = "manual",
        CancellationToken cancellationToken = default);

    /// <summary>Analyzes an already captured snapshot without creating a second capture.</summary>
    /// <param name="activity">Current context sample used to build the analysis prompt.</param>
    /// <param name="captureResult">The completed snapshot capture to submit to the configured provider.</param>
    /// <param name="keepCapture">Whether the retained snapshot files should remain on disk after analysis.</param>
    /// <param name="origin">Stable analysis origin recorded with local usage and history.</param>
    /// <param name="cancellationToken">Cancels device-context collection and the provider request.</param>
    /// <returns>The AI summary record persisted in the local history store.</returns>
    Task<AiAnalysis> AnalyzeCapturedScreenAsync(
        AnalysisContextSnapshot? activity,
        ScreenshotCaptureResult captureResult,
        bool keepCapture,
        string origin,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates screenshot capture, prompt assembly, AI provider call, and persistence.
/// </summary>
public sealed class OpenAiAnalysisService : IAiAnalysisService
{
    private readonly LocalStore _store;
    private readonly IScreenCaptureService _capture;
    private readonly SystemSnapshotService _snapshotService;
    private readonly IAIDecoder? _decoder;
    private readonly DeviceContextService _deviceContext;
    private readonly ILogger<OpenAiAnalysisService> _logger;

    /// <summary>
    /// Creates a new AI analysis service.
    /// </summary>
    /// <param name="store">Application data store used for settings and persistence.</param>
    /// <param name="capture">Screenshot capture service for the selected capture mode.</param>
    /// <param name="snapshotService">Optional system snapshot provider.</param>
    /// <param name="decoder">Optional decoder override for testing.</param>
    /// <param name="deviceContext">Optional device-context provider for time zone, language, and Windows location metadata.</param>
    /// <param name="logger">Optional structured application logger.</param>
    public OpenAiAnalysisService(
        LocalStore store,
        IScreenCaptureService capture,
        SystemSnapshotService? snapshotService = null,
        IAIDecoder? decoder = null,
        DeviceContextService? deviceContext = null,
        ILogger<OpenAiAnalysisService>? logger = null)
    {
        _store = store;
        _capture = capture;
        _snapshotService = snapshotService ?? new SystemSnapshotService();
        _decoder = decoder;
        _deviceContext = deviceContext ?? new DeviceContextService();
        _logger = logger ?? NullLogger<OpenAiAnalysisService>.Instance;
    }

    /// <summary>
    /// Runs a single AI analysis for the current foreground context and saves the result locally.
    /// </summary>
    /// <param name="activity">Current context sample used to build the analysis prompt.</param>
    /// <param name="allowCapture">Whether this invocation may capture screenshots when globally enabled.</param>
    /// <param name="origin">Stable analysis origin recorded with local usage and history.</param>
    /// <param name="cancellationToken">Cancels capture, device-context collection, and the provider request.</param>
    /// <returns>The AI summary record persisted in the local history store.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when OpenAI integration is disabled or the API key is missing.
    /// </exception>
    public async Task<AiAnalysis> AnalyzeCurrentScreenAsync(
        AnalysisContextSnapshot? activity,
        bool allowCapture = true,
        string origin = "manual",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = _store.LoadSettings();
        var apiKey = LoadRequiredApiKey(settings);

        // Keep analysis possible even when screenshots are disabled. In that case, run with empty image context.
        var captureResult = allowCapture && settings.ScreenshotsEnabled
            ? _capture.CaptureByMode(
                settings.ScreenshotDirectory,
                settings.ScreenshotCaptureMode,
                settings.WatermarkScreenshots,
                origin == "snapshot.scheduled" ? ScreenshotCaptureOrigins.Scheduled : ScreenshotCaptureOrigins.Manual)
            : new ScreenshotCaptureResult(
                Guid.NewGuid().ToString("N"),
                Array.Empty<string>(),
                Array.Empty<string>(),
                origin == "snapshot.scheduled" ? ScreenshotCaptureOrigins.Scheduled : ScreenshotCaptureOrigins.Manual);

        return await AnalyzeCapturedScreenCoreAsync(
            settings,
            apiKey,
            activity,
            captureResult,
            settings.KeepScreenshots,
            origin,
            cancellationToken);
    }

    /// <summary>
    /// Analyzes an already captured snapshot so capture and AI processing share the same correlation ID and files.
    /// </summary>
    /// <param name="activity">Current context sample used to build the analysis prompt.</param>
    /// <param name="captureResult">The completed snapshot capture to submit to the configured provider.</param>
    /// <param name="keepCapture">Whether the captured files should remain on disk after analysis.</param>
    /// <param name="origin">Stable analysis origin recorded with local usage and history.</param>
    /// <param name="cancellationToken">Cancels device-context collection and the provider request.</param>
    /// <returns>The AI summary record persisted in the local history store.</returns>
    public async Task<AiAnalysis> AnalyzeCapturedScreenAsync(
        AnalysisContextSnapshot? activity,
        ScreenshotCaptureResult captureResult,
        bool keepCapture,
        string origin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captureResult);
        var coreOwnsCleanup = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (captureResult.AnalysisScreenshotPaths.Count == 0 ||
                captureResult.AnalysisScreenshotPaths.Any(path => !ScreenCaptureService.IsOwnedArtifact(path) || !File.Exists(path)))
            {
                throw new InvalidOperationException("The captured snapshot does not contain valid analysis files.");
            }

            var settings = _store.LoadSettings();
            var apiKey = LoadRequiredApiKey(settings);
            coreOwnsCleanup = true;
            return await AnalyzeCapturedScreenCoreAsync(
                settings,
                apiKey,
                activity,
                captureResult,
                keepCapture,
                origin,
                cancellationToken);
        }
        finally
        {
            if (!coreOwnsCleanup)
            {
                CleanupCapture(captureResult, keepCapture);
            }
        }
    }

    private async Task<AiAnalysis> AnalyzeCapturedScreenCoreAsync(
        AppSettings settings,
        string apiKey,
        AnalysisContextSnapshot? activity,
        ScreenshotCaptureResult captureResult,
        bool keepCapture,
        string origin,
        CancellationToken cancellationToken)
    {
        try
        {
            var deviceContext = await _deviceContext.CaptureAsync(settings.IncludeDeviceLocation, cancellationToken);
            var capturedSnapshot = _snapshotService.Capture();
            var scheduleNote = ActiveHoursSchedule.BuildInformationalNote(settings.ActiveHours, capturedSnapshot.Timestamp);
            var snapshot = capturedSnapshot with
            {
                DeviceContext = deviceContext,
                InformationalSchedule = scheduleNote
            };
            var context = (activity is null ? null : activity with
            {
                Snapshot = snapshot,
                InformationalSchedule = scheduleNote
            }) ?? new AnalysisContextSnapshot(
                "not available",
                "not available",
                "not available",
                "active",
                null,
                snapshot,
                scheduleNote);

            var prompt = AiPromptCatalog.RenderScreenshotAnalysis(
                settings.AiOutputDetail,
                context,
                customPrompt: settings.AiCustomPrompt);
            var decoder = _decoder ?? AIDecoderFactory.Create(settings);
            var attemptId = Guid.NewGuid().ToString("N");
            var attemptedAt = DateTimeOffset.UtcNow;
            var profile = AiAnalysisProfileCatalog.Resolve(settings.AiOutputDetail);
            var attemptToken = CorrelationToken(attemptId);
            var captureToken = CorrelationToken(captureResult.CaptureId);
            var endpointHost = AiProviderTelemetry.EndpointHost(settings.AiEndpoint);
            _logger.LogInformation(
                "AI analysis attempt started. Attempt={Attempt} Correlation={Correlation} Origin={Origin} Provider={Provider} Model={Model} EndpointHost={EndpointHost} ImageCount={ImageCount}",
                attemptToken,
                captureToken,
                origin,
                decoder.Provider,
                settings.Model,
                endpointHost,
                captureResult.AnalysisScreenshotPaths.Count);
            // Route un-watermarked capture to model, and keep watermarked files only for local history UX.
            AiProviderResult providerResult;
            try
            {
                providerResult = await decoder.DecodeAsync(
                    prompt,
                    captureResult.AnalysisScreenshotPaths,
                    settings,
                    apiKey,
                    captureResult.CaptureId,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "AI analysis attempt completed. Attempt={Attempt} Correlation={Correlation} Origin={Origin} Provider={Provider} Model={Model} EndpointHost={EndpointHost} Outcome={Outcome}",
                    attemptToken,
                    captureToken,
                    origin,
                    decoder.Provider,
                    settings.Model,
                    endpointHost,
                    "cancelled");
                throw;
            }
            catch (AiProviderRequestException exception)
            {
                _logger.LogWarning(
                    "AI analysis attempt completed. Attempt={Attempt} Correlation={Correlation} Origin={Origin} Provider={Provider} Model={Model} EndpointHost={EndpointHost} HttpStatus={HttpStatus} FailureCategory={FailureCategory} LatencyMs={LatencyMs} ProviderRequestId={ProviderRequestId} Outcome={Outcome}",
                    attemptToken,
                    captureToken,
                    origin,
                    decoder.Provider,
                    settings.Model,
                    endpointHost,
                    exception.Failure.HttpStatusCode,
                    exception.Failure.FailureCode,
                    exception.Failure.ElapsedMilliseconds,
                    AiProviderTelemetry.SafeToken(exception.Failure.ProviderRequestId, 80),
                    "provider_failed");
                AppendFailedUsageOrThrow(CreateUsageRecord(
                    attemptId,
                    captureResult.CaptureId,
                    attemptedAt,
                    origin,
                    decoder.Provider,
                    settings,
                    captureResult.AnalysisScreenshotPaths.Count,
                    prompt.Length,
                    profile.MaxOutputTokens,
                    null,
                    exception.Failure),
                    exception);
                throw;
            }
            catch (Exception exception)
            {
                var elapsed = (long)Math.Max(0, (DateTimeOffset.UtcNow - attemptedAt).TotalMilliseconds);
                _logger.LogWarning(
                    "AI analysis attempt completed. Attempt={Attempt} Correlation={Correlation} Origin={Origin} Provider={Provider} Model={Model} EndpointHost={EndpointHost} FailureCategory={FailureCategory} LatencyMs={LatencyMs} Outcome={Outcome} ExceptionType={ExceptionType}",
                    attemptToken,
                    captureToken,
                    origin,
                    decoder.Provider,
                    settings.Model,
                    endpointHost,
                    "unexpected",
                    elapsed,
                    "unexpected_failed",
                    exception.GetType().Name);
                AppendFailedUsageOrThrow(CreateUsageRecord(
                    attemptId,
                    captureResult.CaptureId,
                    attemptedAt,
                    origin,
                    decoder.Provider,
                    settings,
                    captureResult.AnalysisScreenshotPaths.Count,
                    prompt.Length,
                    profile.MaxOutputTokens,
                    null,
                    new AiProviderFailure("unexpected", null, elapsed)),
                    exception);
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = new AiAnalysis(
                DateTimeOffset.Now,
                context.Application,
                context.Context,
                providerResult.Text,
                settings.InstallationId,
                keepCapture ? string.Join(";", captureResult.StoredScreenshotPaths) : null,
                context.Snapshot,
                captureResult.CaptureId,
                origin,
                context.InformationalSchedule);
            var usage = CreateUsageRecord(
                attemptId,
                captureResult.CaptureId,
                attemptedAt,
                origin,
                decoder.Provider,
                settings,
                captureResult.AnalysisScreenshotPaths.Count,
                prompt.Length,
                profile.MaxOutputTokens,
                providerResult,
                null);
            try
            {
                _store.AppendAiAnalysisAndUsage(usage, result);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "AI analysis attempt persistence failed. Attempt={Attempt} Correlation={Correlation} Origin={Origin} Provider={Provider} Model={Model} EndpointHost={EndpointHost} HttpStatus={HttpStatus} LatencyMs={LatencyMs} Outcome={Outcome} ExceptionType={ExceptionType}",
                    attemptToken,
                    captureToken,
                    origin,
                    decoder.Provider,
                    settings.Model,
                    endpointHost,
                    providerResult.HttpStatusCode,
                    providerResult.ElapsedMilliseconds,
                    "persistence_failed",
                    exception.GetType().Name);
                throw;
            }

            _logger.LogInformation(
                "AI analysis attempt completed. Attempt={Attempt} Correlation={Correlation} Origin={Origin} Provider={Provider} Model={Model} EndpointHost={EndpointHost} HttpStatus={HttpStatus} LatencyMs={LatencyMs} ProviderRequestId={ProviderRequestId} Outcome={Outcome}",
                attemptToken,
                captureToken,
                origin,
                decoder.Provider,
                settings.Model,
                endpointHost,
                providerResult.HttpStatusCode,
                providerResult.ElapsedMilliseconds,
                AiProviderTelemetry.SafeToken(providerResult.ProviderRequestId, 80),
                "succeeded");

            return result;
        }
        finally
        {
            CleanupCapture(captureResult, keepCapture);
        }
    }

    private static void CleanupCapture(ScreenshotCaptureResult captureResult, bool keepCapture)
    {
        var retainedPaths = captureResult.StoredScreenshotPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cleanupPaths = keepCapture
            ? captureResult.AnalysisScreenshotPaths.Where(path => !retainedPaths.Contains(path))
            : captureResult.AllScreenshotPaths;

        foreach (var screenshot in cleanupPaths
                     .Where(ScreenCaptureService.IsOwnedArtifact)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Where(File.Exists))
        {
            try
            {
                File.Delete(screenshot);
            }
            catch
            {
                // Cleanup is best effort: retained history remains valid and provider/persistence errors stay primary.
            }
        }
    }

    private string LoadRequiredApiKey(AppSettings settings)
    {
        if (!settings.OpenAiEnabled)
        {
            throw new InvalidOperationException("Enable AI integration from the app options before running analysis.");
        }

        var apiKey = _store.LoadApiKey(settings.AiApiKeyName);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Set the environment variable {settings.AiApiKeyName} for {settings.AiProvider}.");
        }

        return apiKey;
    }

    private void AppendFailedUsageOrThrow(AiRequestUsageRecord usage, Exception providerException)
    {
        try
        {
            _store.AppendAiUsage(usage);
        }
        catch (Exception persistenceException)
        {
            throw new AggregateException(
                "The AI provider request and its local usage persistence both failed.",
                providerException,
                persistenceException);
        }
    }

    private static string CorrelationToken(string value) =>
        string.IsNullOrEmpty(value) ? "unavailable" : value[..Math.Min(12, value.Length)];

    private static AiRequestUsageRecord CreateUsageRecord(
        string attemptId,
        string correlationId,
        DateTimeOffset attemptedAt,
        string origin,
        string provider,
        AppSettings settings,
        int imageCount,
        int promptCharacters,
        int maxOutputTokens,
        AiProviderResult? result,
        AiProviderFailure? failure)
    {
        var completedAt = DateTimeOffset.UtcNow;
        return new AiRequestUsageRecord(
            attemptId,
            correlationId,
            attemptedAt,
            completedAt,
            origin,
            "screen_analysis",
            provider,
            AiProviderTelemetry.EndpointHost(settings.AiEndpoint),
            settings.Model,
            result?.ReturnedModel,
            result?.ProviderResponseId ?? failure?.ProviderResponseId,
            result?.ProviderRequestId ?? failure?.ProviderRequestId,
            result?.HttpStatusCode ?? failure?.HttpStatusCode,
            result?.ElapsedMilliseconds ?? failure?.ElapsedMilliseconds,
            result?.ProviderProcessingMilliseconds ?? failure?.ProviderProcessingMilliseconds,
            imageCount,
            promptCharacters,
            maxOutputTokens,
            result?.Usage ?? new AiUsageMetrics(),
            result?.FinishReason,
            result is not null,
            failure?.FailureCode);
    }

}
