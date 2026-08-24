using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "sk-test-only-key-1234567890", EnvironmentVariableTarget.Process);
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

    [Fact]
    public void LoadLatestPrimaryScreenshot_UsesRetainedFilesWhenAiDatabaseIsEmpty()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            var older = CreateCapture(dataDirectory);
            var latest = CreateCapture(dataDirectory);
            foreach (var path in older.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
            }

            foreach (var path in latest.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            }

            var reloadedStore = new LocalStore(dataDirectory);

            Assert.Null(reloadedStore.LoadLatestAnalysis());
            Assert.Equal(latest.StoredScreenshotPaths[0], reloadedStore.LoadLatestPrimaryScreenshot());
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetLatestScreenshotGallery_UsesMostRecentRetainedDayAfterRestart()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            var olderLocalTime = DateTime.Today.AddDays(-2).AddHours(12);
            var latestLocalTime = DateTime.Today.AddDays(-1).AddHours(16);
            var older = CreateCapture(dataDirectory, new DateTimeOffset(olderLocalTime));
            var latest = CreateCapture(dataDirectory, new DateTimeOffset(latestLocalTime));
            foreach (var path in older.AllScreenshotPaths)
            {
                File.SetLastWriteTime(path, olderLocalTime);
            }

            foreach (var path in latest.AllScreenshotPaths)
            {
                File.SetLastWriteTime(path, latestLocalTime);
            }

            var reloadedStore = new LocalStore(dataDirectory);

            var gallery = reloadedStore.GetLatestScreenshotGallery();

            Assert.Equal(DateOnly.FromDateTime(latestLocalTime), gallery.Date);
            var item = Assert.Single(gallery.Items);
            Assert.Equal(latest.StoredScreenshotPaths[0], item.Path);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScreenshotGallery_ProjectsStableCaptureKindIdentifiers()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var monitorCapture = CreateCapture(dataDirectory, capturedAt);
            var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(dataDirectory, capturedAt);
            var activeWindowPath = Path.Combine(
                dayDirectory,
                $"{Guid.NewGuid():N}_1.0.0_scheduled_active-window.webp");
            File.WriteAllBytes(activeWindowPath, [7, 8, 9]);
            foreach (var path in monitorCapture.AllScreenshotPaths.Append(activeWindowPath))
            {
                File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
            }

            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));

            var monitorItem = Assert.Single(gallery.Items, item => item.Path == monitorCapture.StoredScreenshotPaths[0]);
            var activeWindowItem = Assert.Single(gallery.Items, item => item.Path == activeWindowPath);
            Assert.Equal("monitor", monitorItem.CaptureKind);
            Assert.Equal(1, monitorItem.ScreenIndex);
            Assert.Equal("Monitor 1", monitorItem.ScreenName);
            Assert.Equal("active-window", activeWindowItem.CaptureKind);
            Assert.Null(activeWindowItem.ScreenIndex);
            Assert.Null(activeWindowItem.ScreenName);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScreenshotGallery_ShowsDistinctSpanLabelsSampledDuringItsInterval()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with { ScreenshotIntervalMinutes = 15 });
            var capture = CreateCapture(dataDirectory);
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            foreach (var path in capture.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
            }

            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-14), "Planning"));
            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-8), "Planning"));
            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-4), "Implementation"));
            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-1), "Implementation"));

            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));

            var item = Assert.Single(gallery.Items);
            var labels = item.SpanLabels;
            Assert.Collection(
                labels!,
                label => Assert.Equal("Planning", label.Label),
                label => Assert.Equal("Implementation", label.Label));
            Assert.Equal("Test", item.ForegroundApplication);
            Assert.Equal("Test", item.ForegroundWindowTitle);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScreenshotGallery_BatchesDailyActivityWithoutCrossingCaptureIntervals()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with { ScreenshotIntervalMinutes = 5 });
            var earlierCapture = CreateCapture(dataDirectory);
            var laterCapture = CreateCapture(dataDirectory);
            var laterCapturedAt = new DateTimeOffset(DateTime.Today.AddHours(12)).ToUniversalTime();
            var earlierCapturedAt = laterCapturedAt.AddMinutes(-20);
            foreach (var path in earlierCapture.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, earlierCapturedAt.UtcDateTime);
            }

            foreach (var path in laterCapture.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, laterCapturedAt.UtcDateTime);
            }

            store.AppendSample(CreateActivitySample(earlierCapturedAt.AddMinutes(-1), "Earlier work"));
            store.AppendSample(CreateActivitySample(laterCapturedAt.AddMinutes(-1), "Later work"));

            var gallery = store.GetScreenshotGallery(
                DateOnly.FromDateTime(laterCapturedAt.ToLocalTime().DateTime),
                CancellationToken.None);

            var earlierItem = Assert.Single(gallery.Items, item => item.Path == earlierCapture.StoredScreenshotPaths[0]);
            var laterItem = Assert.Single(gallery.Items, item => item.Path == laterCapture.StoredScreenshotPaths[0]);
            Assert.Equal("Earlier work", Assert.Single(earlierItem.SpanLabels!).Label);
            Assert.Equal("Later work", Assert.Single(laterItem.SpanLabels!).Label);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ScreenshotGallery_EnrichesOnlyTheExactCaptureWithPersistedAiMarkdownAndActivityIndex()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "sk-test-only-key-1234567890", EnvironmentVariableTarget.Process);
        try
        {
            var store = CreateStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable,
                ScreenshotIntervalMinutes = 15
            });
            var analyzedCapture = CreateCapture(dataDirectory);
            var unrelatedCapture = CreateCapture(dataDirectory);
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            foreach (var path in analyzedCapture.AllScreenshotPaths.Concat(unrelatedCapture.AllScreenshotPaths))
            {
                File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
            }

            store.AppendSample(CreateActivitySample(
                capturedAt.AddMinutes(-1),
                "Implementation",
                keyPresses: 300,
                mouseClicks: 30));
            var analysisService = new OpenAiAnalysisService(
                store,
                new UnexpectedCaptureService(),
                decoder: new SuccessfulDecoder());
            var analysis = await analysisService.AnalyzeCapturedScreenAsync(
                activity: null,
                analyzedCapture,
                keepCapture: true,
                origin: "snapshot.scheduled",
                CancellationToken.None);

            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));

            var analyzedItem = Assert.Single(gallery.Items, item =>
                string.Equals(item.Path, analyzedCapture.StoredScreenshotPaths[0], StringComparison.OrdinalIgnoreCase));
            var unrelatedItem = Assert.Single(gallery.Items, item =>
                string.Equals(item.Path, unrelatedCapture.StoredScreenshotPaths[0], StringComparison.OrdinalIgnoreCase));
            Assert.Equal("## Activity\n\n- Coding.", analyzedItem.AiDescriptionMarkdown);
            Assert.Equal(analysis.Timestamp, analyzedItem.AiAnalyzedAt);
            Assert.True(analyzedItem.ActivityIndex > 0);
            Assert.Null(unrelatedItem.AiDescriptionMarkdown);
            Assert.Null(unrelatedItem.AiAnalyzedAt);
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

    private static ScreenshotCaptureResult CreateCapture(string directory, DateTimeOffset? capturedAt = null)
    {
        var timestamp = capturedAt ?? DateTimeOffset.Now;
        var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(directory, timestamp);
        Directory.CreateDirectory(dayDirectory);
        var captureId = Guid.NewGuid().ToString("N");
        var rawPath = Path.Combine(dayDirectory, $"{captureId}_1.0.0_manual_monitor-1-raw.webp");
        var storedPath = Path.Combine(dayDirectory, $"{captureId}_1.0.0_manual_monitor-1.webp");
        File.WriteAllBytes(rawPath, [1, 2, 3]);
        File.WriteAllBytes(storedPath, [4, 5, 6]);
        File.SetLastWriteTimeUtc(rawPath, timestamp.UtcDateTime);
        File.SetLastWriteTimeUtc(storedPath, timestamp.UtcDateTime);
        return new ScreenshotCaptureResult(
            captureId,
            [rawPath],
            [storedPath],
            ScreenshotCaptureOrigins.Manual);
    }

    private static ActivitySample CreateActivitySample(
        DateTimeOffset timestamp,
        string spanLabel,
        long keyPresses = 1,
        long mouseClicks = 1) => new(
        timestamp,
        5,
        "active",
        "test",
        "Test",
        "Test",
        "Test",
        "installation",
        keyPresses,
        mouseClicks,
        new Dictionary<string, string> { [ActivityAttributeKeys.SpanLabel] = spanLabel });

    private static TrackMeUpApplication CreateApplication(LocalStore store, IAiAnalysisService? analysis = null) =>
        new(
            store,
            new UtilityService(),
            new TrackingDomainService(store),
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
        public ScreenshotCaptureResult CaptureByMode(string directory, string captureMode, string captureOrigin) =>
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

        public Task<AiAnalysis> AnalyzeHistoricalCapturedScreenAsync(
            AnalysisContextSnapshot activity,
            ScreenshotCaptureResult captureResult,
            string origin,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The deletion tests must not analyze a historical snapshot.");
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
            AiProviderRequestOptions? requestOptions = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiProviderResult(
                "## Activity\n\n- Coding.",
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
