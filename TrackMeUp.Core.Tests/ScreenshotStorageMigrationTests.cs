using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class ScreenshotStorageMigrationTests
{
    [Fact]
    public void Migration_MovesArtifactsAndRemapsEveryDurablePathWithoutChangingSemanticTimestamps()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var screenshotRoot = Path.Combine(dataDirectory, "screenshots");
            Directory.CreateDirectory(screenshotRoot);
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with { ScreenshotDirectory = screenshotRoot });

            var captureId = Guid.NewGuid().ToString("N");
            var capturedAt = new DateTimeOffset(2026, 8, 23, 12, 34, 56, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 8, 23, 12, 34, 56)));
            var rawPath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-1-raw.webp");
            var storedPath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-1.webp");
            var secondStoredPath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-2.webp");
            File.WriteAllBytes(rawPath, [1, 2, 3]);
            File.WriteAllBytes(storedPath, [4, 5, 6]);
            File.WriteAllBytes(secondStoredPath, [7, 8, 9]);
            File.SetLastWriteTimeUtc(rawPath, capturedAt.AddDays(-1).UtcDateTime);
            File.SetLastWriteTimeUtc(storedPath, capturedAt.UtcDateTime);
            File.SetLastWriteTimeUtc(secondStoredPath, capturedAt.UtcDateTime);
            store.RegisterScreenshotCapture(
                captureId,
                store.LoadSettings().InstallationId,
                capturedAt,
                ScreenshotCaptureOrigins.Manual);
            var storedLastWrite = File.GetLastWriteTimeUtc(storedPath);
            var unrelatedPath = Path.Combine(screenshotRoot, "notes.txt");
            File.WriteAllText(unrelatedPath, "keep");

            var artifactIdentity = Path.GetFileNameWithoutExtension(storedPath);
            var textSnapshot = CreateTextSnapshot(storedPath, capturedAt);
            store.UpsertScreenshotTextSnapshot(captureId, textSnapshot);
            var updatedTicksBefore = ReadSnapshotUpdatedTicks(dataDirectory, artifactIdentity);
            AppendAnalysis(store, captureId, capturedAt, $"{storedPath};{secondStoredPath}");
            var sourceStampBefore = store.GetSearchSourceStamp();

            var status = store.GetScreenshotStorageMigrationStatus(default);
            Assert.True(status.Required);
            Assert.Equal(3, status.ArtifactCount);

            var migration = store.MigrateScreenshotStorage(default);

            var expectedDirectory = ScreenshotStorageLayout.GetDayDirectory(screenshotRoot, capturedAt);
            var migratedStoredPath = Path.Combine(expectedDirectory, Path.GetFileName(storedPath));
            var migratedRawPath = Path.Combine(expectedDirectory, Path.GetFileName(rawPath));
            var migratedSecondStoredPath = Path.Combine(expectedDirectory, Path.GetFileName(secondStoredPath));
            Assert.Equal(3, migration.MovedArtifactCount);
            Assert.False(File.Exists(storedPath));
            Assert.False(File.Exists(rawPath));
            Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(migratedStoredPath));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(migratedRawPath));
            Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(migratedSecondStoredPath));
            Assert.Equal(storedLastWrite, File.GetLastWriteTimeUtc(migratedStoredPath));
            Assert.True(File.Exists(unrelatedPath));
            Assert.Equal(updatedTicksBefore, ReadSnapshotUpdatedTicks(dataDirectory, artifactIdentity));
            Assert.Equal(migratedStoredPath, store.LoadScreenshotTextSnapshot(migratedStoredPath)?.SourceScreenshotPath);
            Assert.Equal($"{migratedStoredPath};{migratedSecondStoredPath}", store.LoadLatestAnalysis()?.ScreenshotPaths);
            Assert.NotEqual(sourceStampBefore, store.GetSearchSourceStamp());

            var gallery = store.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.Date));
            Assert.Contains(gallery.Items, item => item.Path == migratedStoredPath);
            Assert.Contains(gallery.Items, item => item.Path == migratedSecondStoredPath);
            Assert.Equal(0, store.MigrateScreenshotStorage(default).MovedArtifactCount);
            Assert.False(store.GetScreenshotStorageMigrationStatus(default).Required);
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    [Fact]
    public void Migration_RollsBackFileMovesWhenPersistedPathRepresentationsDisagree()
    {
        var dataDirectory = CreateTemporaryDirectory();
        try
        {
            var screenshotRoot = Path.Combine(dataDirectory, "screenshots");
            Directory.CreateDirectory(screenshotRoot);
            var store = new LocalStore(dataDirectory);
            store.SaveSettings(store.LoadSettings() with { ScreenshotDirectory = screenshotRoot });
            var capturedAt = DateTimeOffset.Now.AddMinutes(-1);
            var captureId = Guid.NewGuid().ToString("N");
            var sourcePath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-1.webp");
            File.WriteAllBytes(sourcePath, [7, 8, 9]);
            File.SetLastWriteTimeUtc(sourcePath, capturedAt.UtcDateTime);
            var artifactIdentity = Path.GetFileNameWithoutExtension(sourcePath);
            var snapshot = CreateTextSnapshot(sourcePath, capturedAt);
            store.UpsertScreenshotTextSnapshot(captureId, snapshot);

            var inconsistentPath = Path.Combine(screenshotRoot, $"{Guid.NewGuid():N}_1.0.0_manual_monitor-1.webp");
            using (var connection = OpenDatabase(dataDirectory))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE screenshot_text_snapshots SET snapshot_json = $json WHERE artifact_identity = $identity;";
                command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(
                    snapshot with { SourceScreenshotPath = inconsistentPath },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                command.Parameters.AddWithValue("$identity", artifactIdentity);
                command.ExecuteNonQuery();
            }

            Assert.Throws<InvalidDataException>(() => store.MigrateScreenshotStorage(default));

            Assert.True(File.Exists(sourcePath));
            var destinationPath = Path.Combine(
                ScreenshotStorageLayout.GetDayDirectory(screenshotRoot, capturedAt),
                Path.GetFileName(sourcePath));
            Assert.False(File.Exists(destinationPath));
            Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(sourcePath));
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private static ScreenshotTextSnapshot CreateTextSnapshot(string path, DateTimeOffset capturedAt) =>
        new(
            path,
            new OcrRawSnapshot(
                ScreenshotTextExtractionStatus.Succeeded,
                "migration text",
                "en-US",
                null,
                capturedAt,
                "test-ocr",
                100,
                100,
                []));

    private static void AppendAnalysis(LocalStore store, string correlationId, DateTimeOffset capturedAt, string paths)
    {
        var usage = new AiRequestUsageRecord(
            Guid.NewGuid().ToString("N"),
            correlationId,
            capturedAt,
            capturedAt.AddMilliseconds(10),
            "snapshot.manual",
            "screen_analysis",
            "test-provider",
            "provider.invalid",
            "test-model",
            "test-model",
            null,
            null,
            200,
            10,
            null,
            2,
            10,
            100,
            new AiUsageMetrics(10, 5, 15),
            "stop",
            true,
            null);
        var analysis = new AiAnalysis(
            capturedAt,
            "Test",
            "Migration",
            "Migrated analysis",
            store.LoadSettings().InstallationId,
            paths,
            CorrelationId: correlationId,
            Origin: "snapshot.manual");
        store.AppendAiAnalysisAndUsage(usage, analysis);
    }

    private static long ReadSnapshotUpdatedTicks(string dataDirectory, string artifactIdentity)
    {
        using var connection = OpenDatabase(dataDirectory);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT updated_utc_ticks FROM screenshot_text_snapshots WHERE artifact_identity = $identity;";
        command.Parameters.AddWithValue("$identity", artifactIdentity);
        return (long)(command.ExecuteScalar() ?? throw new InvalidDataException("Snapshot timestamp was not found."));
    }

    private static SqliteConnection OpenDatabase(string dataDirectory)
    {
        var connection = new SqliteConnection($"Data Source={Path.Combine(dataDirectory, "activity.sqlite3")};Pooling=False");
        connection.Open();
        return connection;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"TrackMeUp-storage-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
