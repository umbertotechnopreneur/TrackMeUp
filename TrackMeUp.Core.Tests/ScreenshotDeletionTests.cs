// SPDX-License-Identifier: MIT

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

[Collection(ProcessEnvironmentCollection.Name)]
public sealed class ScreenshotDeletionTests
{
    private const string TestApiKeyVariable = "TRACKMEUP_OPENAI_APIKEY";

    [Fact]
    public async Task DeleteScreenshot_RemovesStoredAndRawArtifactsForCapture()
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
            var capture = CreateCapture(store, dataDirectory);
            var unrelatedPath = Path.Combine(dataDirectory, "unrelated.webp");
            File.WriteAllBytes(unrelatedPath, [7, 8, 9]);
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

            var result = await application.DeleteScreenshotAsync(capture.StoredScreenshotPaths[0], CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.False(File.Exists(capture.StoredScreenshotPaths[0]));
            Assert.False(File.Exists(capture.AnalysisScreenshotPaths[0]));
            Assert.Null(store.LoadLatestAnalysis());
            Assert.Empty(store.LoadScreenshotCaptureTimes(capture.StoredScreenshotPaths, CancellationToken.None));
            Assert.True(File.Exists(unrelatedPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>Ensures deleting one monitor artifact retains capture provenance while a sibling monitor remains.</summary>
    [Fact]
    public async Task DeleteScreenshot_KeepsCaptureProvenanceWhileAnotherMonitorArtifactRemains()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            var capture = CreateCapture(store, dataDirectory);
            var siblingPath = Path.Combine(
                Path.GetDirectoryName(capture.StoredScreenshotPaths[0])!,
                $"{capture.CaptureId}_1.0.0_manual_monitor-2.webp");
            File.WriteAllBytes(siblingPath, [7, 8, 9]);
            await using var application = CreateApplication(store);

            var result = await application.DeleteScreenshotAsync(capture.StoredScreenshotPaths[0], CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.True(File.Exists(siblingPath));
            Assert.Contains(
                siblingPath,
                store.LoadScreenshotCaptureTimes([siblingPath], CancellationToken.None).Keys);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>Ensures analysis-only deletion removes persisted enrichment while retaining the image artifact.</summary>
    [Fact]
    public async Task DeleteScreenshotAnalysis_RemovesAnalysisButKeepsTheImage()
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
            var capture = CreateCapture(store, dataDirectory);
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

            var result = await application.DeleteScreenshotAnalysisAsync(capture.StoredScreenshotPaths[0], CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Null(store.LoadLatestAnalysis());
            Assert.True(File.Exists(capture.StoredScreenshotPaths[0]));
            // Deletion now commits the derived projection before returning (the log keeps its last anchor).
            using var directory = Lucene.Net.Store.FSDirectory.Open(new DirectoryInfo(Path.Combine(
                store.SearchIndexRootDirectory, TrackMeUp.Search.LocalSearchService.IndexDirectoryName)));
            using var reader = Lucene.Net.Index.DirectoryReader.Open(directory);
            Assert.Equal(store.GetSearchSourceRevision().ToString(System.Globalization.CultureInfo.InvariantCulture),
                reader.IndexCommit.UserData["trackmeup.search.source_revision"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>Ensures an owned-looking path outside the configured store cannot delete retained analysis data.</summary>
    [Fact]
    public async Task DeleteScreenshotAnalysis_RejectsOwnedLookingPathOutsideConfiguredStoreWithoutDeletingData()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var outsideDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var capture = CreateCapture(store, dataDirectory, capturedAt);
            var genuinePath = capture.StoredScreenshotPaths[0];
            store.UpsertScreenshotTextSnapshot(capture.CaptureId, CreateTextSnapshot(genuinePath, capturedAt));
            store.UpsertScreenshotIntervalTelemetry(
                capture.CaptureId,
                capture.StoredScreenshotPaths,
                new ScreenshotIntervalTelemetry(capturedAt.AddMinutes(-15), capturedAt, 30, 20));
            var outsidePath = Path.Combine(outsideDirectory, Path.GetFileName(genuinePath));
            File.WriteAllBytes(outsidePath, [7, 8, 9]);
            await using var application = CreateApplication(store);

            var result = await application.DeleteScreenshotAnalysisAsync(outsidePath, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal("screenshot.analysis.not_found", result.Code);
            Assert.NotNull(store.LoadScreenshotTextSnapshot(genuinePath));
            Assert.NotNull(store.LoadScreenshotIntervalTelemetry(genuinePath));
            Assert.True(File.Exists(genuinePath));
        }
        finally
        {
            Directory.Delete(outsideDirectory, recursive: true);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>Ensures analysis deletion detaches only the selected monitor from a shared AI result.</summary>
    [Fact]
    public async Task DeleteScreenshotAnalysis_DisassociatesOnlySelectedMonitorFromSharedAiResult()
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
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var capture = CreateMultiMonitorCapture(store, dataDirectory, capturedAt);
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
            await using var application = CreateApplication(store, analysis);

            var result = await application.DeleteScreenshotAnalysisAsync(capture.StoredScreenshotPaths[0], CancellationToken.None);

            Assert.True(result.Succeeded);
            var retainedAnalysis = Assert.IsType<AiAnalysis>(store.LoadLatestAnalysis());
            Assert.Equal(capture.StoredScreenshotPaths[1], retainedAnalysis.ScreenshotPaths);
            Assert.True(File.Exists(capture.StoredScreenshotPaths[0]));
            Assert.True(File.Exists(capture.StoredScreenshotPaths[1]));
            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));
            var selectedMonitor = Assert.Single(gallery.Items, item => item.Path == capture.StoredScreenshotPaths[0]);
            var siblingMonitor = Assert.Single(gallery.Items, item => item.Path == capture.StoredScreenshotPaths[1]);
            Assert.Null(selectedMonitor.AiDescriptionMarkdown);
            Assert.Equal("## Activity\n\n- Coding.", siblingMonitor.AiDescriptionMarkdown);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>Ensures deleting one monitor retains the shared AI result for its surviving sibling.</summary>
    [Fact]
    public async Task DeleteScreenshot_DisassociatesDeletedMonitorButKeepsSiblingAiResult()
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
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var capture = CreateMultiMonitorCapture(store, dataDirectory, capturedAt);
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
            await using var application = CreateApplication(store, analysis);

            var result = await application.DeleteScreenshotAsync(capture.StoredScreenshotPaths[0], CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.False(File.Exists(capture.StoredScreenshotPaths[0]));
            Assert.True(File.Exists(capture.StoredScreenshotPaths[1]));
            var retainedAnalysis = Assert.IsType<AiAnalysis>(store.LoadLatestAnalysis());
            Assert.Equal(capture.StoredScreenshotPaths[1], retainedAnalysis.ScreenshotPaths);
            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));
            var siblingMonitor = Assert.Single(gallery.Items, item => item.Path == capture.StoredScreenshotPaths[1]);
            Assert.Equal("## Activity\n\n- Coding.", siblingMonitor.AiDescriptionMarkdown);
            Assert.Contains(
                capture.StoredScreenshotPaths[1],
                store.LoadScreenshotCaptureTimes([capture.StoredScreenshotPaths[1]], CancellationToken.None).Keys);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>Ensures runtime health advertises the screenshot-analysis deletion capability.</summary>
    [Fact]
    public async Task RuntimeHealth_AdvertisesScreenshotAnalysisDeletionCapability()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            await using var application = CreateApplication(store);

            var result = await application.GetRuntimeHealthAsync(CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Contains("screenshots.analysis.delete.v1", result.Value!.Capabilities);
        }
        finally
        {
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
            var older = CreateCapture(store, dataDirectory);
            var latest = CreateCapture(store, dataDirectory);
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
            var older = CreateCapture(store, dataDirectory, new DateTimeOffset(olderLocalTime));
            var latest = CreateCapture(store, dataDirectory, new DateTimeOffset(latestLocalTime));
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
            var monitorCapture = CreateCapture(store, dataDirectory, capturedAt);
            var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(dataDirectory, capturedAt);
            var activeWindowCaptureId = Guid.NewGuid().ToString("N");
            var activeWindowPath = Path.Combine(
                dayDirectory,
                $"{activeWindowCaptureId}_1.0.0_scheduled_active-window.webp");
            File.WriteAllBytes(activeWindowPath, [7, 8, 9]);
            foreach (var path in monitorCapture.AllScreenshotPaths.Append(activeWindowPath))
            {
                File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
            }
            store.RegisterScreenshotCapture(
                activeWindowCaptureId,
                store.LoadSettings().InstallationId,
                capturedAt,
                ScreenshotCaptureOrigins.Scheduled);

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
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var capture = CreateCapture(store, dataDirectory, capturedAt);
            foreach (var path in capture.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
            }

            var installationId = store.LoadSettings().InstallationId;
            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-14), "Planning", installationId));
            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-8), "Planning", installationId));
            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-4), "Implementation", installationId));
            store.AppendSample(CreateActivitySample(capturedAt.AddMinutes(-1), "Implementation", installationId));

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

    /// <summary>Ensures stored interval telemetry remains removable even when both sampled metrics are unavailable.</summary>
    [Fact]
    public void ScreenshotGallery_ReportsStoredIntervalTelemetryEvenWhenMetricsAreUnavailable()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = CreateStore(dataDirectory);
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var capture = CreateCapture(store, dataDirectory, capturedAt);
            store.UpsertScreenshotIntervalTelemetry(
                capture.CaptureId,
                capture.StoredScreenshotPaths,
                new ScreenshotIntervalTelemetry(capturedAt.AddMinutes(-15), capturedAt, null, null));

            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));

            var item = Assert.Single(gallery.Items);
            Assert.True(item.HasRemovableAnalysisData);
            Assert.Null(item.CpuUsagePercent);
            Assert.Null(item.GpuUsagePercent);
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
            var laterCapturedAt = new DateTimeOffset(DateTime.Today.AddHours(12)).ToUniversalTime();
            var earlierCapturedAt = laterCapturedAt.AddMinutes(-20);
            var earlierCapture = CreateCapture(store, dataDirectory, earlierCapturedAt);
            var laterCapture = CreateCapture(store, dataDirectory, laterCapturedAt);
            foreach (var path in earlierCapture.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, earlierCapturedAt.UtcDateTime);
            }

            foreach (var path in laterCapture.AllScreenshotPaths)
            {
                File.SetLastWriteTimeUtc(path, laterCapturedAt.UtcDateTime);
            }

            var installationId = store.LoadSettings().InstallationId;
            store.AppendSample(CreateActivitySample(earlierCapturedAt.AddMinutes(-1), "Earlier work", installationId));
            store.AppendSample(CreateActivitySample(laterCapturedAt.AddMinutes(-1), "Later work", installationId));

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
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var analyzedCapture = CreateCapture(store, dataDirectory, capturedAt);
            var unrelatedCapture = CreateCapture(store, dataDirectory, capturedAt);
            foreach (var path in analyzedCapture.AllScreenshotPaths.Concat(unrelatedCapture.AllScreenshotPaths))
            {
                File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
            }

            store.AppendSample(CreateActivitySample(
                capturedAt.AddMinutes(-1),
                "Implementation",
                store.LoadSettings().InstallationId,
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

    private static ScreenshotCaptureResult CreateCapture(
        LocalStore store,
        string directory,
        DateTimeOffset? capturedAt = null)
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
        var result = new ScreenshotCaptureResult(
            captureId,
            [rawPath],
            [storedPath],
            ScreenshotCaptureOrigins.Manual,
            CapturedAt: timestamp);
        store.RegisterScreenshotCapture(
            captureId,
            store.LoadSettings().InstallationId,
            timestamp,
            ScreenshotCaptureOrigins.Manual);
        return result;
    }

    private static ScreenshotCaptureResult CreateMultiMonitorCapture(
        LocalStore store,
        string directory,
        DateTimeOffset capturedAt)
    {
        var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(directory, capturedAt);
        Directory.CreateDirectory(dayDirectory);
        var captureId = Guid.NewGuid().ToString("N");
        var rawPaths = new List<string>();
        var storedPaths = new List<string>();
        for (var monitor = 1; monitor <= 2; monitor++)
        {
            var rawPath = Path.Combine(dayDirectory, $"{captureId}_1.0.0_manual_monitor-{monitor}-raw.webp");
            var storedPath = Path.Combine(dayDirectory, $"{captureId}_1.0.0_manual_monitor-{monitor}.webp");
            File.WriteAllBytes(rawPath, [1, 2, 3]);
            File.WriteAllBytes(storedPath, [4, 5, 6]);
            File.SetLastWriteTimeUtc(rawPath, capturedAt.UtcDateTime);
            File.SetLastWriteTimeUtc(storedPath, capturedAt.UtcDateTime);
            rawPaths.Add(rawPath);
            storedPaths.Add(storedPath);
        }

        var result = new ScreenshotCaptureResult(
            captureId,
            rawPaths,
            storedPaths,
            ScreenshotCaptureOrigins.Manual,
            CapturedAt: capturedAt);
        store.RegisterScreenshotCapture(
            captureId,
            store.LoadSettings().InstallationId,
            capturedAt,
            ScreenshotCaptureOrigins.Manual);
        return result;
    }

    private static ScreenshotTextSnapshot CreateTextSnapshot(string screenshotPath, DateTimeOffset capturedAt) =>
        new(
            screenshotPath,
            new OcrRawSnapshot(
                ScreenshotTextExtractionStatus.Succeeded,
                "retained OCR text",
                "en-US",
                null,
                capturedAt,
                "test",
                1,
                1,
                []));

    private static ActivitySample CreateActivitySample(
        DateTimeOffset timestamp,
        string spanLabel,
        string installationId,
        long keyPresses = 1,
        long mouseClicks = 1) => new(
        timestamp,
        5,
        "active",
        "test",
        "Test",
        "Test",
        "Test",
        installationId,
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
        /// <inheritdoc />
        public ScreenshotCaptureResult CaptureByMode(
            string directory,
            string captureMode,
            string captureOrigin,
            Func<ScreenshotCaptureContext, ScreenshotCaptureDecision> authorizeCapture) =>
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
