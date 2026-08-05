using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class SnapshotAnalysisFlowTests
{
    private const string TestApiKeyVariable = "TRACKMEUP_OPENAI_APIKEY";

    [Fact]
    public async Task CaptureScreenshot_AnalyzesTheSameSnapshot_WhenOpenAiIsEnabled()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);

        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new RecordingAnalysisService(store.LoadSettings().InstallationId);
            await using var application = CreateApplication(store, capture, analysis);

            var result = await application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Value);
            Assert.Equal(1, capture.CallCount);
            Assert.Equal(1, analysis.CallCount);
            Assert.Same(capture.Result, analysis.Capture);
            Assert.Equal("snapshot.manual", analysis.Origin);
            Assert.True(analysis.KeepCapture);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DeferredCapture_AnalyzesTheSameSnapshotOnlyAfterExplicitRequest_WhenOpenAiIsEnabled()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);

        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new RecordingAnalysisService(store.LoadSettings().InstallationId);
            await using var application = CreateApplication(store, capture, analysis);

            var captureResult = await application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual, DeferAiAnalysis: true),
                CancellationToken.None);

            Assert.True(captureResult.Succeeded);
            Assert.Equal(1, capture.CallCount);
            Assert.Equal(0, analysis.CallCount);

            var analysisResult = await application.AnalyzeCapturedScreenshotAsync(
                new AnalyzeCapturedScreenshotRequest(capture.Result, KeepCapture: true),
                CancellationToken.None);

            Assert.True(analysisResult.Succeeded);
            Assert.Equal(1, analysis.CallCount);
            Assert.Same(capture.Result, analysis.Capture);
            Assert.Equal("snapshot.manual", analysis.Origin);
            Assert.True(analysis.KeepCapture);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureScreenshot_DoesNotAnalyze_WhenOpenAiIsDisabled()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = false,
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new RecordingAnalysisService(store.LoadSettings().InstallationId);
            await using var application = CreateApplication(store, capture, analysis);

            var result = await application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(1, capture.CallCount);
            Assert.Equal(0, analysis.CallCount);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureScreenshot_RejectsUnsupportedPersistedModelBeforeCapture()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);

        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                Model = "unsupported-model",
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new RecordingAnalysisService(store.LoadSettings().InstallationId);
            await using var application = CreateApplication(store, capture, analysis);

            var result = await application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("ai.configuration.invalid", result.Code);
            Assert.Contains(result.Issues, issue => issue.Field == "ai.model" && issue.Code == "unsupported");
            Assert.Equal(0, capture.CallCount);
            Assert.Equal(0, analysis.CallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureScreenshot_RejectsUnsupportedPersistedThinkingEffortBeforeCapture()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);

        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                Model = "gpt-5.5",
                AiReasoningEffort = "max",
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new RecordingAnalysisService(store.LoadSettings().InstallationId);
            await using var application = CreateApplication(store, capture, analysis);

            var result = await application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("ai.configuration.invalid", result.Code);
            Assert.Contains(result.Issues, issue => issue.Field == "ai.reasoning_effort" && issue.Code == "unsupported");
            Assert.Equal(0, capture.CallCount);
            Assert.Equal(0, analysis.CallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureScreenshot_RejectsTextOnlyPreviewModelBeforeCapture()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);

        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                Model = "gpt-5.3-codex-spark",
                AiReasoningEffort = "auto",
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new RecordingAnalysisService(store.LoadSettings().InstallationId);
            await using var application = CreateApplication(store, capture, analysis);

            var result = await application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual),
                CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("ai.configuration.invalid", result.Code);
            Assert.Contains(result.Issues, issue => issue.Field == "ai.model" && issue.Code == "image_input_unsupported");
            Assert.Equal(0, capture.CallCount);
            Assert.Equal(0, analysis.CallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureScreenshot_CanonicalizesPersistedModelAliasBeforeAnalysis()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);

        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                Model = "gpt-5.6",
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new RecordingAnalysisService(store.LoadSettings().InstallationId);
            await using var application = CreateApplication(store, capture, analysis);

            var result = await application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal("gpt-5.6-sol", store.LoadSettings().Model);
            Assert.Equal(1, analysis.CallCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureScreenshot_PropagatesCancellationIntoAnalysis()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "test-only-key", EnvironmentVariableTarget.Process);

        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                ScreenshotDirectory = dataDirectory
            });

            var capture = new RecordingCaptureService(dataDirectory);
            var analysis = new CancellationAwareAnalysisService();
            await using var application = CreateApplication(store, capture, analysis);
            using var cancellation = new CancellationTokenSource();

            var captureTask = application.CaptureScreenshotAsync(
                new CaptureScreenshotRequest("all-screens", Keep: true, Watermark: false, ScreenshotCaptureOrigins.Manual),
                cancellation.Token);
            await analysis.Entered;
            cancellation.Cancel();
            var result = await captureTask;

            Assert.False(result.Succeeded);
            Assert.Equal("operation.cancelled", result.Code);
            Assert.True(analysis.CancellationObserved);
            Assert.True(analysis.ReceivedToken.CanBeCanceled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static TrackMeUpApplication CreateApplication(
        LocalStore store,
        IScreenCaptureService capture,
        IAiAnalysisService analysis)
    {
        var utilities = new UtilityService();
        return new TrackMeUpApplication(
            store,
            utilities,
            new TrackingDomainService(store, utilities),
            capture,
            new SystemSnapshotService(),
            analysis,
            new StartupService(),
            new BuildInformationService());
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingCaptureService : IScreenCaptureService
    {
        public RecordingCaptureService(string directory)
        {
            var path = Path.Combine(directory, $"{Guid.NewGuid():N}_1.0.0_manual_monitor-1.webp");
            Result = new ScreenshotCaptureResult(
                Guid.NewGuid().ToString("N"),
                [path],
                [path],
                ScreenshotCaptureOrigins.Manual);
        }

        public int CallCount { get; private set; }

        public ScreenshotCaptureResult Result { get; }

        public ScreenshotCaptureResult CaptureByMode(string directory, string captureMode, bool includeWatermark, string captureOrigin)
        {
            CallCount++;
            return Result;
        }
    }

    private sealed class RecordingAnalysisService(string installationId) : IAiAnalysisService
    {
        public int CallCount { get; private set; }

        public ScreenshotCaptureResult? Capture { get; private set; }

        public bool KeepCapture { get; private set; }

        public string? Origin { get; private set; }

        public Task<AiAnalysis> AnalyzeCurrentScreenAsync(
            AnalysisContextSnapshot? activity,
            bool allowCapture = true,
            string origin = "manual",
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AiAnalysis> AnalyzeCapturedScreenAsync(
            AnalysisContextSnapshot? activity,
            ScreenshotCaptureResult captureResult,
            bool keepCapture,
            string origin,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Capture = captureResult;
            KeepCapture = keepCapture;
            Origin = origin;
            return Task.FromResult(new AiAnalysis(
                DateTimeOffset.UtcNow,
                "TrackMeUp",
                "test",
                "analyzed",
                installationId,
                keepCapture ? string.Join(';', captureResult.StoredScreenshotPaths) : null,
                CorrelationId: captureResult.CaptureId,
                Origin: origin));
        }
    }

    private sealed class CancellationAwareAnalysisService : IAiAnalysisService
    {
        private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public CancellationToken ReceivedToken { get; private set; }

        public bool CancellationObserved { get; private set; }

        public Task<AiAnalysis> AnalyzeCurrentScreenAsync(
            AnalysisContextSnapshot? activity,
            bool allowCapture = true,
            string origin = "manual",
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<AiAnalysis> AnalyzeCapturedScreenAsync(
            AnalysisContextSnapshot? activity,
            ScreenshotCaptureResult captureResult,
            bool keepCapture,
            string origin,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            _entered.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Cancellation-aware analysis unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }
}
