// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Microsoft.Data.Sqlite;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class InstallationProfilePersistenceTests
{
    [Fact]
    public void MissingProfile_IsBackfilledFromEarliestDurableActivityStart()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var store = new LocalStore(dataDirectory);
            var installationId = store.LoadSettings().InstallationId;
            var firstSeenAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
            store.AppendSample(new ActivitySample(
                firstSeenAt.AddSeconds(5),
                5,
                "active",
                "test",
                "Test",
                "Test",
                "Test",
                installationId,
                1,
                1));

            using (var connection = new SqliteConnection(
                $"Data Source={Path.Combine(dataDirectory, SqliteActivityStore.DatabaseFileName)};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM installation_profiles WHERE installation_id = $installationId;";
                command.Parameters.AddWithValue("$installationId", installationId);
                Assert.Equal(1, command.ExecuteNonQuery());
            }

            var reloaded = new LocalStore(dataDirectory);

            var profile = Assert.Single(reloaded.GetInstallationProfiles());
            Assert.Equal(installationId, profile.InstallationId);
            Assert.Equal(firstSeenAt, profile.FirstSeenAt);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public void ScreenshotBackfill_UsesCaptureLevelTelemetryWhenOneArtifactHasNoTelemetryRow()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var screenshotDirectory = Path.Combine(dataDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDirectory);
            var captureId = Guid.NewGuid().ToString("N");
            var firstPath = Path.Combine(
                screenshotDirectory,
                $"{captureId}_1.0.0_manual_monitor-1.webp");
            var secondPath = Path.Combine(
                screenshotDirectory,
                $"{captureId}_1.0.0_manual_monitor-2.webp");

            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with { ScreenshotDirectory = screenshotDirectory });
            File.WriteAllBytes(firstPath, [1]);
            File.WriteAllBytes(secondPath, [2]);
            var capturedAt = new DateTimeOffset(2026, 8, 11, 19, 15, 54, TimeSpan.Zero);
            store.UpsertScreenshotIntervalTelemetry(
                captureId,
                [secondPath],
                new ScreenshotIntervalTelemetry(
                    capturedAt.AddMinutes(-15),
                    capturedAt,
                    25,
                    10));

            var databasePath = Path.Combine(dataDirectory, SqliteActivityStore.DatabaseFileName);
            using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM screenshot_captures;
                    DELETE FROM store_metadata WHERE key = 'installation.capture_backfill.v1';
                    """;
                Assert.Equal(2, command.ExecuteNonQuery());
            }

            _ = new LocalStore(dataDirectory);

            using var verification = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            verification.Open();
            using var select = verification.CreateCommand();
            select.CommandText = """
                SELECT capture_id, captured_utc_ticks, origin
                FROM screenshot_captures;
                """;
            using var reader = select.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(captureId, reader.GetString(0));
            Assert.Equal(capturedAt.UtcDateTime.Ticks, reader.GetInt64(1));
            Assert.Equal(ScreenshotCaptureOrigins.Manual, reader.GetString(2));
            Assert.False(reader.Read());
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrackMeUpInstallationProfileTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
