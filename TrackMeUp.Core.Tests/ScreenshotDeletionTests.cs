using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class ScreenshotDeletionTests
{
    private const string TestApiKeyVariable = "TRACKMEUP_OPENAI_APIKEY";

    [Fact]
    public async Task DeleteScreenshot_RemovesStoredAndRawArtifactsForCapture()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            var capture = CreateCapture(dataDirectory);
            var unrelatedPath = Path.Combine(dataDirectory, "unrelated.webp");
            File.WriteAllBytes(unrelatedPath, [7, 8, 9]);
            await using var application = CreateApplication(store);

            var result = await application.DeleteScreenshotAsync(capture.StoredScreenshotPaths[0], CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.False(File.Exists(capture.StoredScreenshotPaths[0]));
            Assert.False(File.Exists(capture.AnalysisScreenshotPaths[0]));
            Assert.True(File.Exists(unrelatedPath));
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteSnapshot_RemovesAnalysisReferencingCapture()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);
        try
        {
            var store = CreateStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable
            });
            var capture = CreateCapture(dataDirectory);
            var analysis = new OpenAiAnalysisService(
                store,
                new UnexpectedCaptureService(),
                decoder: new SuccessfulDecoder());
            await analysis.AnalyzeCapturedScreenAsync(
                activity: null,
                capture,
                keepCapture: true,
                origin: "snapshot.manual",
                CancellationToken.None);
            Assert.NotNull(store.LoadLatestAnalysis());
            await using var application = CreateApplication(store, analysis);

            var result = await application.DeleteSnapshotAsync(capture.StoredScreenshotPaths[0], CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Null(store.LoadLatestAnalysis());
            Assert.True(File.Exists(capture.StoredScreenshotPaths[0]));
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static LocalStore CreateStore(string dataDirectory)
    {
        var store = new LocalStore(dataDirectory);
        store.SaveSettings(store.LoadSettings() with
        {
            ScreenshotDirectory = dataDirectory,
            IncludeDeviceLocation = false
        });
        return store;
    }

    private static ScreenshotCaptureResult CreateCapture(string directory)
    {
        var captureId = Guid.NewGuid().ToString("N");
        var rawPath = Path.Combine(directory, $"{captureId}_1.0.0_manual_monitor-1-raw.webp");
        var storedPath = Path.Combine(directory, $"{captureId}_1.0.0_manual_monitor-1.webp");
        File.WriteAllBytes(rawPath, [1, 2, 3]);
        File.WriteAllBytes(storedPath, [4, 5, 6]);
        return new ScreenshotCaptureResult(
            captureId,
            [rawPath],
            [storedPath],
            ScreenshotCaptureOrigins.Manual);
    }

    private static TrackMeUpApplication CreateApplication(LocalStore store, IAiAnalysisService? analysis = null) =>
        new(
            store,
            new UtilityService(),
            new TrackingDomainService(store, new UtilityService()),
            new UnexpectedCaptureService(),
            new SystemSnapshotService(),
            analysis ?? new UnexpectedAnalysisService(),
            new StartupService(),
            new BuildInformationService());

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class UnexpectedCaptureService : IScreenCaptureService
    {
        public ScreenshotCaptureResult CaptureByMode(string directory, string captureMode, bool includeWatermark, string captureOrigin) =>
            throw new InvalidOperationException("The deletion tests must not capture a new screenshot.");
    }

    private sealed class UnexpectedAnalysisService : IAiAnalysisService
    {
        public Task<AiAnalysis> AnalyzeCurrentScreenAsync(
            AnalysisContextSnapshot? activity,
            bool allowCapture = true,
            string origin = "manual",
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The deletion tests must not analyze a new snapshot.");

        public Task<AiAnalysis> AnalyzeCapturedScreenAsync(
            AnalysisContextSnapshot? activity,
            ScreenshotCaptureResult captureResult,
            bool keepCapture,
            string origin,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The deletion tests must not analyze a new snapshot.");
    }

    private sealed class SuccessfulDecoder : IAIDecoder
    {
        public string Provider => "openai";

        public Task<AiProviderResult> DecodeAsync(
            string prompt,
            IReadOnlyList<string> screenshotPaths,
            AppSettings settings,
            string apiKey,
            string correlationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiProviderResult(
                "analyzed",
                new AiUsageMetrics(),
                "response-id",
                "request-id",
                settings.Model,
                "completed",
                200,
                1,
                null));
    }
}