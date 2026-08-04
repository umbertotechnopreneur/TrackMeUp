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

    /// <summary>
    /// Creates a new AI analysis service.
    /// </summary>
    /// <param name="store">Application data store used for settings and persistence.</param>
    /// <param name="capture">Screenshot capture service for the selected capture mode.</param>
    /// <param name="snapshotService">Optional system snapshot provider.</param>
    /// <param name="decoder">Optional decoder override for testing.</param>
    public OpenAiAnalysisService(LocalStore store, ScreenCaptureService capture, SystemSnapshotService? snapshotService = null, IAIDecoder? decoder = null)
    {
        _store = store;
        _capture = capture;
        _snapshotService = snapshotService ?? new SystemSnapshotService();
        _decoder = decoder;
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
    public async Task<AiAnalysis> AnalyzeCurrentScreenAsync(AnalysisContextSnapshot? activity, bool allowCapture = true)
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
            ? _capture.CaptureByMode(settings.ScreenshotDirectory, settings.ScreenshotCaptureMode, settings.WatermarkScreenshots)
            : new ScreenshotCaptureResult(Guid.NewGuid().ToString("N"), Array.Empty<string>(), Array.Empty<string>());

        try
        {
            var snapshot = _snapshotService.Capture();
            var context = (activity is null ? null : activity with { Snapshot = snapshot }) ?? new AnalysisContextSnapshot(
                "not available",
                "not available",
                "not available",
                "active",
                null,
                snapshot);

            var prompt = AiPromptCatalog.RenderScreenshotAnalysis(settings.AiOutputDetail, context);
            var decoder = _decoder ?? AIDecoderFactory.Create(settings);
            // Route un-watermarked capture to model, and keep watermarked files only for local history UX.
            var summary = await decoder.DecodeAsync(prompt, captureResult.AnalysisScreenshotPaths, settings, apiKey);

            var result = new AiAnalysis(
                DateTimeOffset.Now,
                context.Application,
                context.Context,
                summary,
                settings.InstallationId,
                settings.KeepScreenshots ? string.Join(";", captureResult.StoredScreenshotPaths) : null,
                context.Snapshot);

            _store.AppendAnalysis(result);
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

}
