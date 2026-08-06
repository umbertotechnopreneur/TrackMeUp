using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class ScheduledSnapshotStateTests
{
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
                new TrackingDomainService(store, utilities),
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
}