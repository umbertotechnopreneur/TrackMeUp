using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TrackMeUp.Services;

/// <summary>
/// Orchestrates screenshot capture, prompt assembly, AI provider call, and persistence.
/// </summary>
public sealed class OpenAiAnalysisService
{
    private readonly LocalStore _store;
    private readonly ScreenCaptureService _capture;
    private readonly SystemSnapshotService _snapshotService;
    private readonly IAIDecoder? _decoder;
    private readonly DeviceContextService _deviceContext;

    /// <summary>
    /// Creates a new AI analysis service.
    /// </summary>
    /// <param name="store">Application data store used for settings and persistence.</param>
    /// <param name="capture">Screenshot capture service for the selected capture mode.</param>
    /// <param name="snapshotService">Optional system snapshot provider.</param>
    /// <param name="decoder">Optional decoder override for testing.</param>
    /// <param name="deviceContext">Optional device-context provider for time zone, language, and Windows location metadata.</param>
    public OpenAiAnalysisService(
        LocalStore store,
        ScreenCaptureService capture,
        SystemSnapshotService? snapshotService = null,
        IAIDecoder? decoder = null,
        DeviceContextService? deviceContext = null)
    {
        _store = store;
        _capture = capture;
        _snapshotService = snapshotService ?? new SystemSnapshotService();
        _decoder = decoder;
        _deviceContext = deviceContext ?? new DeviceContextService();
    }

    /// <summary>
    /// Runs a single AI analysis for the current foreground context and saves the result locally.
    /// </summary>
    /// <param name="activity">Current context sample used to build the analysis prompt.</param>
    /// <param name="allowCapture">Whether this invocation may capture screenshots when globally enabled.</param>
    /// <returns>The AI summary record persisted in the local history store.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when OpenAI integration is disabled or the API key is missing.
    /// </exception>
    public async Task<AiAnalysis> AnalyzeCurrentScreenAsync(
        AnalysisContextSnapshot? activity,
        bool allowCapture = true,
        string origin = "manual")
    {
        var settings = _store.LoadSettings();
        if (!settings.OpenAiEnabled)
        {
            throw new InvalidOperationException("Enable AI integration from the app options before running analysis.");
        }

        var apiKey = _store.LoadApiKey(settings.AiApiKeyName);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Set the environment variable {settings.AiApiKeyName} for {settings.AiProvider}.");
        }

        // Keep analysis possible even when screenshots are disabled. In that case, run with empty image context.
        var captureResult = allowCapture && settings.ScreenshotsEnabled
            ? _capture.CaptureByMode(
                settings.ScreenshotDirectory,
                settings.ScreenshotCaptureMode,
                settings.WatermarkScreenshots,
                origin == "automatic.timer" ? ScreenshotCaptureOrigins.Scheduled : ScreenshotCaptureOrigins.Manual)
            : new ScreenshotCaptureResult(
                Guid.NewGuid().ToString("N"),
                Array.Empty<string>(),
                Array.Empty<string>(),
                origin == "automatic.timer" ? ScreenshotCaptureOrigins.Scheduled : ScreenshotCaptureOrigins.Manual);

        try
        {
            var deviceContext = await _deviceContext.CaptureAsync(settings.IncludeDeviceLocation);
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
            // Route un-watermarked capture to model, and keep watermarked files only for local history UX.
            AiProviderResult providerResult;
            try
            {
                providerResult = await decoder.DecodeAsync(
                    prompt,
                    captureResult.AnalysisScreenshotPaths,
                    settings,
                    apiKey,
                    captureResult.CaptureId);
            }
            catch (AiProviderRequestException exception)
            {
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
                    new AiProviderFailure("unexpected", null, (long)Math.Max(0, (DateTimeOffset.UtcNow - attemptedAt).TotalMilliseconds))),
                    exception);
                throw;
            }

            var result = new AiAnalysis(
                DateTimeOffset.Now,
                context.Application,
                context.Context,
                providerResult.Text,
                settings.InstallationId,
                settings.KeepScreenshots ? string.Join(";", captureResult.StoredScreenshotPaths) : null,
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
            _store.AppendAiAnalysisAndUsage(usage, result);

            return result;
        }
        finally
        {
            // Remove transient screenshots only when the user chose not to retain them.
            if (!settings.KeepScreenshots)
            {
                foreach (var screenshot in captureResult.AllScreenshotPaths.Where(File.Exists))
                {
                    try
                    {
                        File.Delete(screenshot);
                    }
                    catch
                    {
                        // Keep analysis flow resilient: cleanup failures should not block result persistence.
                    }
                }
            }
        }
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
