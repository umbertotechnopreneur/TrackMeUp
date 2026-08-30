// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

[Collection(ProcessEnvironmentCollection.Name)]
public sealed class ScheduledSnapshotStateTests
{
    private const string TestApiKeyVariable = "TRACKMEUP_OPENAI_APIKEY";

    [Fact]
    public async Task EmptyActiveHours_DisableCountdownUntilAWorkingPeriodIsConfigured()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotIntervalMinutes = 1,
                ActiveHours = [.. ActiveHoursSchedule.Days.Select(day => new ActiveHoursDay(day))]
            });
            var utilities = new UtilityService();
            await using var application = new TrackMeUpApplication(
                store,
                utilities,
                new TrackingDomainService(store),
                new ScreenCaptureService(utilities.GetAppVersion()),
                new SystemSnapshotService(),
                new OpenAiAnalysisService(store, new ScreenCaptureService(utilities.GetAppVersion()), new SystemSnapshotService()),
                new StartupService(),
                new BuildInformationService());

            var started = await application.StartTrackingAsync(new StartTrackingRequest(), CancellationToken.None);
            var enabled = await application.PatchSettingsAsync(
                new SettingsPatch(new Dictionary<string, string?>
                {
                    ["active_hours.monday.active"] = "09:00-18:00"
                }),
                CancellationToken.None);
            var dashboard = await application.GetDashboardAsync(CancellationToken.None);

            Assert.True(started.Succeeded);
            Assert.Null(started.Value?.ScheduledSnapshotRemaining);
            Assert.False(started.Value?.IsWithinActiveHours);
            Assert.True(enabled.Succeeded);
            Assert.NotNull(dashboard.Value?.ScheduledSnapshotRemaining);
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(dataDirectory);
        }
    }

    [Fact]
    public async Task PauseTracking_FreezesScheduledSnapshotCountdown_UntilTrackingResumes()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with { ScreenshotIntervalMinutes = 1 });
            var utilities = new UtilityService();
            await using var application = new TrackMeUpApplication(
                store,
                utilities,
                new TrackingDomainService(store),
                new ScreenCaptureService(utilities.GetAppVersion()),
                new SystemSnapshotService(),
                new OpenAiAnalysisService(store, new ScreenCaptureService(utilities.GetAppVersion()), new SystemSnapshotService()),
                new StartupService(),
                new BuildInformationService());

            var started = await application.StartTrackingAsync(new StartTrackingRequest(), CancellationToken.None);
            var paused = await application.PauseTrackingAsync(CancellationToken.None);
            var frozenCountdown = paused.Value?.ScheduledSnapshotRemaining;

            await Task.Delay(TimeSpan.FromMilliseconds(50));
            var stillPaused = await application.GetDashboardAsync(CancellationToken.None);
            var resumed = await application.StartTrackingAsync(new StartTrackingRequest(), CancellationToken.None);

            Assert.True(started.Succeeded);
            Assert.True(paused.Succeeded);
            Assert.NotNull(frozenCountdown);
            Assert.Equal(frozenCountdown, stillPaused.Value?.ScheduledSnapshotRemaining);
            Assert.True(resumed.Succeeded);
            Assert.NotNull(resumed.Value?.ScheduledSnapshotRemaining);
            Assert.True(resumed.Value!.ScheduledSnapshotRemaining <= frozenCountdown);
        }
        finally
        {
            await DeleteTemporaryDirectoryAsync(dataDirectory);
        }
    }

    [Fact]
    public async Task DueTimer_CapturesAnalyzesPersistsAndProjectsTheSameScheduledSnapshot()
    {
        var dataDirectory = CreateTemporaryDirectory();
        var previousApiKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "sk-test-only-key-1234567890", EnvironmentVariableTarget.Process);
        try
        {
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with
            {
                ScreenshotIntervalMinutes = 1,
                ScreenshotDirectory = dataDirectory,
                ScreenshotsEnabled = true,
                OpenAiEnabled = true,
                AiApiKeyName = TestApiKeyVariable
            });
            var utilities = new UtilityService();
            var capture = new ScheduledCaptureService(dataDirectory);
            var analysis = new OpenAiAnalysisService(
                store,
                capture,
                decoder: new SuccessfulDecoder());
            await using var application = new TrackMeUpApplication(
                store,
                utilities,
                new TrackingDomainService(store),
                capture,
                new SystemSnapshotService(),
                analysis,
                new StartupService(),
                new BuildInformationService(),
                // This test drives the due processor directly, so disarm the background callback that could
                // otherwise claim the same deadline and still be enriching the capture when assertions run.
                startScheduledSnapshotTimer: false);
            var started = await application.StartTrackingAsync(new StartTrackingRequest(), CancellationToken.None);
            Assert.True(started.Succeeded);

            var deadline = typeof(TrackMeUpApplication).GetField(
                "_nextScheduledSnapshotAt",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Scheduled snapshot deadline field was not found.");
            deadline.SetValue(application, DateTimeOffset.Now.AddSeconds(-1));
            var timerProcessor = typeof(TrackMeUpApplication).GetMethod(
                "ProcessScheduledSnapshotAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Scheduled snapshot processor was not found.");
            await (Task)(timerProcessor.Invoke(application, null)
                ?? throw new InvalidOperationException("Scheduled snapshot processor returned no task."));

            var persisted = store.LoadLatestAnalysis();
            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(DateTime.Today));
            var galleryItem = Assert.Single(gallery.Items);
            Assert.Equal(1, capture.CallCount);
            Assert.Equal(ScreenshotCaptureOrigins.Scheduled, capture.LastOrigin);
            Assert.NotNull(persisted);
            Assert.Equal(capture.Result.CaptureId, persisted!.CorrelationId);
            Assert.Equal("snapshot.scheduled", persisted.Origin);
            Assert.Equal(capture.Result.StoredScreenshotPaths[0], persisted.ScreenshotPaths);
            Assert.Equal("## Activity\n\n- Scheduled work.", galleryItem.AiDescriptionMarkdown);
            Assert.Equal(persisted.Timestamp, galleryItem.AiAnalyzedAt);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousApiKey, EnvironmentVariableTarget.Process);
            await DeleteTemporaryDirectoryAsync(dataDirectory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task DeleteTemporaryDirectoryAsync(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
        }
    }

    private sealed class ScheduledCaptureService : IScreenCaptureService
    {
        public ScheduledCaptureService(string directory)
        {
            var capturedAt = DateTimeOffset.Now;
            var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(directory, capturedAt);
            Directory.CreateDirectory(dayDirectory);
            var captureId = Guid.NewGuid().ToString("N");
            var rawPath = Path.Combine(dayDirectory, $"{captureId}_1.0.0_scheduled_monitor-1-raw.webp");
            var storedPath = Path.Combine(dayDirectory, $"{captureId}_1.0.0_scheduled_monitor-1.webp");
            File.WriteAllBytes(rawPath, [1, 2, 3]);
            File.WriteAllBytes(storedPath, [4, 5, 6]);
            Result = new ScreenshotCaptureResult(
                captureId,
                [rawPath],
                [storedPath],
                ScreenshotCaptureOrigins.Scheduled);
        }

        public int CallCount { get; private set; }

        public string? LastOrigin { get; private set; }

        public ScreenshotCaptureResult Result { get; }

        public ScreenshotCaptureResult CaptureByMode(string directory, string captureMode, string captureOrigin)
        {
            CallCount++;
            LastOrigin = captureOrigin;
            return Result;
        }
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
                "## Activity\n\n- Scheduled work.",
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
