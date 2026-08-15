using System;
using System.IO;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class DashboardActivityCacheTests
{
    [Fact]
    public void GetSummary_PreservesDailyCountersWithTheMinimalProjection()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        try
        {
            var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
            var localNoon = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Unspecified);
            var noon = new DateTimeOffset(localNoon, TimeZoneInfo.Local.GetUtcOffset(localNoon));
            var store = new LocalStore(dataDirectory);
            store.AppendSample(Sample(noon, keyPresses: 4, mouseClicks: 1));
            store.AppendSample(Sample(noon.AddSeconds(5), keyPresses: 1, mouseClicks: 2) with
            {
                State = "idle",
                Application = "Desktop"
            });

            var summary = store.GetSummary(date);

            Assert.Equal(5, summary.ActiveSeconds);
            Assert.Equal(5, summary.IdleSeconds);
            Assert.Equal(5, summary.KeyPresses);
            Assert.Equal(3, summary.MouseClicks);
            var application = Assert.Single(summary.Applications);
            Assert.Equal("Editor", application.Application);
            Assert.Equal(5, application.ActiveSeconds);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadCurrentDashboardState_ReusesHistoryUntilDurableActivityChanges()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        try
        {
            var store = new LocalStore(dataDirectory);
            store.AppendSample(Sample(DateTimeOffset.Now, keyPresses: 2, mouseClicks: 1));
            using var tracking = new TrackingDomainService(store, new UtilityService());

            var first = tracking.LoadCurrentDashboardState();
            var databasePath = Path.Combine(dataDirectory, SqliteActivityStore.DatabaseFileName);
            var detachedDatabasePath = databasePath + ".detached";
            File.Move(databasePath, detachedDatabasePath);
            try
            {
                // A repeated player refresh must be served from the seeded in-memory projection.
                var repeated = tracking.LoadCurrentDashboardState();
                Assert.Equal(first.TotalKeyPresses, repeated.TotalKeyPresses);
                Assert.Equal(first.TotalMouseClicks, repeated.TotalMouseClicks);
                Assert.Equal(first.ActiveSeconds, repeated.ActiveSeconds);
            }
            finally
            {
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }

                File.Move(detachedDatabasePath, databasePath);
            }

            store.AppendSample(Sample(DateTimeOffset.Now, keyPresses: 3, mouseClicks: 2));

            var refreshed = tracking.LoadCurrentDashboardState();
            Assert.Equal(first.TotalKeyPresses + 3, refreshed.TotalKeyPresses);
            Assert.Equal(first.TotalMouseClicks + 2, refreshed.TotalMouseClicks);
            Assert.Equal(first.ActiveSeconds + 5, refreshed.ActiveSeconds);
        }
        finally
        {
            if (Directory.Exists(dataDirectory))
            {
                Directory.Delete(dataDirectory, recursive: true);
            }
        }
    }

    private static ActivitySample Sample(DateTimeOffset timestamp, long keyPresses, long mouseClicks) => new(
        timestamp,
        5,
        "active",
        "editor",
        "Editor",
        "Document",
        "Cache test",
        "dashboard-cache-test",
        keyPresses,
        mouseClicks,
        null);
}
