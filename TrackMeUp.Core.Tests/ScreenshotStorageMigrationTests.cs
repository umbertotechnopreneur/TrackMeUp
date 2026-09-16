// SPDX-License-Identifier: MIT

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
    /// <summary>Repairs durable references to missing artifacts before exporting and importing their retained history.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void Migration_WithMissingArtifacts_RoundTripsAiAndOcrHistory(int siblingLocation, bool includeScreenshots)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourceDirectory = Path.Combine(root, "source");
            var screenshotRoot = Path.Combine(sourceDirectory, "screenshots");
            var source = new LocalStore(sourceDirectory);
            source.SaveSettings(source.LoadSettings() with { ScreenshotDirectory = screenshotRoot });
            var capturedAt = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
            // An existing canonical day wins over telemetry and mutable file timestamps.
            var layoutDay = siblingLocation == 2 ? capturedAt.AddDays(1) : capturedAt;
            var captureId = Guid.NewGuid().ToString("N");
            source.RegisterScreenshotCapture(captureId, source.LoadSettings().InstallationId,
                capturedAt, ScreenshotCaptureOrigins.Manual);
            var storedName = $"{captureId}_1.0.0_manual_monitor-1.webp";
            var rawPath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-1-raw.webp");
            var secondPath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-2.webp");
            var siblingDirectory = siblingLocation == 2
                ? ScreenshotStorageLayout.GetDayDirectory(screenshotRoot, layoutDay)
                : screenshotRoot;
            var storedPath = Path.Combine(siblingDirectory, storedName);
            if (siblingLocation != 0)
            {
                Directory.CreateDirectory(siblingDirectory);
                File.WriteAllBytes(storedPath, [1, 2, 3]);
                File.SetLastWriteTimeUtc(storedPath, capturedAt.UtcDateTime);
            }

            source.UpsertScreenshotTextSnapshot(captureId, CreateTextSnapshot(rawPath, capturedAt));
            AppendAnalysis(source, captureId, capturedAt, $"{storedPath};{secondPath}");
            var identity = LocalStore.ScreenshotIdentity(Path.GetFileName(rawPath));
            var updatedTicks = ReadSnapshotUpdatedTicks(sourceDirectory, identity);
            var revision = source.GetSearchSourceRevision();
            var archivePath = Path.Combine(root, "history.tmuarchive");
            var exporter = new DataArchiveService(source);
            Assert.Throws<InvalidDataException>(() => exporter.Export(
                new DataArchiveExportRequest(archivePath, IncludeScreenshots: includeScreenshots), default));

            var status = source.GetScreenshotStorageMigrationStatus(default);
            Assert.True(status.Required);
            Assert.Equal(siblingLocation == 1 ? 1 : 0, status.ArtifactCount);
            Assert.Equal(status.ArtifactCount, source.MigrateScreenshotStorage(default).MovedArtifactCount);

            var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(screenshotRoot, layoutDay);
            var migratedRaw = Path.Combine(dayDirectory, Path.GetFileName(rawPath));
            var migratedStored = Path.Combine(dayDirectory, storedName);
            var migratedSecond = Path.Combine(dayDirectory, Path.GetFileName(secondPath));
            Assert.Equal(migratedRaw, source.LoadScreenshotTextSnapshot(migratedRaw)!.SourceScreenshotPath);
            Assert.Equal($"{migratedStored};{migratedSecond}", source.LoadAiAnalysis(captureId)!.ScreenshotPaths);
            Assert.Equal(capturedAt, source.LoadAiAnalysis(captureId)!.Timestamp);
            Assert.Equal(updatedTicks, ReadSnapshotUpdatedTicks(sourceDirectory, identity));
            Assert.False(File.Exists(migratedRaw));
            Assert.False(File.Exists(migratedSecond));
            Assert.True(source.GetSearchSourceRevision() > revision);
            var migratedRevision = source.GetSearchSourceRevision();
            Assert.False(source.GetScreenshotStorageMigrationStatus(default).Required);
            Assert.Equal(0, source.MigrateScreenshotStorage(default).MovedArtifactCount);
            Assert.Equal(migratedRevision, source.GetSearchSourceRevision());

            var exported = exporter.Export(
                new DataArchiveExportRequest(archivePath, IncludeScreenshots: includeScreenshots), default);
            Assert.Equal(includeScreenshots && siblingLocation != 0 ? 1 : 0, exported.ScreenshotFileCount);
            var targetDirectory = Path.Combine(root, "target");
            var target = new LocalStore(targetDirectory);
            var targetRoot = Path.Combine(targetDirectory, "screenshots");
            target.SaveSettings(target.LoadSettings() with { ScreenshotDirectory = targetRoot });
            var importer = new DataArchiveService(target);
            var preview = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), default);
            Assert.Equal(1, importer.Import(preview.PlanId, default).AddedAiAnalysisCount);
            var targetDay = ScreenshotStorageLayout.GetDayDirectory(targetRoot, layoutDay);
            var importedRaw = Path.Combine(targetDay, Path.GetFileName(rawPath));
            var importedSnapshot = target.LoadScreenshotTextSnapshot(importedRaw)!;
            Assert.Equal(importedRaw, importedSnapshot.SourceScreenshotPath);
            Assert.Equal("migration text", importedSnapshot.Ocr.RawText);
            Assert.Equal(updatedTicks, ReadSnapshotUpdatedTicks(targetDirectory, identity));
            Assert.Equal($"{Path.Combine(targetDay, storedName)};{Path.Combine(targetDay, Path.GetFileName(secondPath))}",
                target.LoadAiAnalysis(captureId)!.ScreenshotPaths);
            Assert.Equal(includeScreenshots && siblingLocation != 0, File.Exists(Path.Combine(targetDay, storedName)));
            Assert.False(File.Exists(importedRaw));
            Assert.False(target.GetScreenshotStorageMigrationStatus(default).Required);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Does not invent a date or adopt unsupported paths when a missing artifact cannot be migrated safely.</summary>
    [Theory]
    [InlineData("outside-root")]
    [InlineData("nested-layout")]
    [InlineData("missing-provenance")]
    public void Migration_WithInvalidMissingArtifact_FailsWithoutChangingHistory(string invalidState)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new LocalStore(root);
            var screenshotRoot = Path.Combine(root, "screenshots");
            store.SaveSettings(store.LoadSettings() with { ScreenshotDirectory = screenshotRoot });
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var captureId = Guid.NewGuid().ToString("N");
            if (invalidState != "missing-provenance")
            {
                store.RegisterScreenshotCapture(captureId, store.LoadSettings().InstallationId,
                    capturedAt, ScreenshotCaptureOrigins.Manual);
            }

            var parent = invalidState switch
            {
                "outside-root" => Path.Combine(root, "elsewhere"),
                "nested-layout" => Path.Combine(screenshotRoot, "unsupported"),
                _ => screenshotRoot
            };
            var sourcePath = Path.Combine(parent, $"{captureId}_1.0.0_manual_monitor-1-raw.webp");
            store.UpsertScreenshotTextSnapshot(captureId, CreateTextSnapshot(sourcePath, capturedAt));
            var revision = store.GetSearchSourceRevision();

            Assert.Throws<InvalidDataException>(() => store.GetScreenshotStorageMigrationStatus(default));
            Assert.Throws<InvalidDataException>(() => store.MigrateScreenshotStorage(default));

            Assert.Equal(sourcePath, store.LoadScreenshotTextSnapshot(sourcePath)!.SourceScreenshotPath);
            Assert.Equal(revision, store.GetSearchSourceRevision());
            Assert.False(Directory.Exists(screenshotRoot));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
            var sourceRevisionBefore = store.GetSearchSourceRevision();

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
            Assert.True(store.GetSearchSourceRevision() > sourceRevisionBefore);

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

    /// <summary>Rejects inconsistent OCR path representations before changing the filesystem.</summary>
    [Fact]
    public void Migration_RejectsInconsistentPathRepresentationsBeforeFileMoves()
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

    /// <summary>Restores moved files and all durable references when a database update fails.</summary>
    [Fact]
    public void Migration_DatabaseWriteFailureRollsBackFilesAndHistory()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var store = new LocalStore(root);
            var screenshotRoot = Path.Combine(root, "screenshots");
            Directory.CreateDirectory(screenshotRoot);
            store.SaveSettings(store.LoadSettings() with { ScreenshotDirectory = screenshotRoot });
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var captureId = Guid.NewGuid().ToString("N");
            store.RegisterScreenshotCapture(captureId, store.LoadSettings().InstallationId,
                capturedAt, ScreenshotCaptureOrigins.Manual);
            var sourcePath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-1.webp");
            var missingPath = Path.Combine(screenshotRoot, $"{captureId}_1.0.0_manual_monitor-2.webp");
            File.WriteAllBytes(sourcePath, [4, 5, 6]);
            File.SetLastWriteTimeUtc(sourcePath, capturedAt.UtcDateTime);
            store.UpsertScreenshotTextSnapshot(captureId, CreateTextSnapshot(sourcePath, capturedAt));
            var analysisPaths = $"{sourcePath};{missingPath}";
            AppendAnalysis(store, captureId, capturedAt, analysisPaths);
            var revision = store.GetSearchSourceRevision();
            var identity = LocalStore.ScreenshotIdentity(Path.GetFileName(sourcePath));
            var updatedTicks = ReadSnapshotUpdatedTicks(root, identity);
            using (var connection = OpenDatabase(root))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    CREATE TRIGGER fail_migration_ocr BEFORE UPDATE OF source_path ON screenshot_text_snapshots
                    BEGIN
                        SELECT RAISE(ABORT, 'test migration write failure');
                    END;
                    """;
                command.ExecuteNonQuery();
            }

            var exception = Assert.Throws<SqliteException>(() => store.MigrateScreenshotStorage(default));

            Assert.Contains("test migration write failure", exception.Message, StringComparison.Ordinal);
            Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(sourcePath));
            Assert.False(File.Exists(Path.Combine(
                ScreenshotStorageLayout.GetDayDirectory(screenshotRoot, capturedAt), Path.GetFileName(sourcePath))));
            Assert.Equal(analysisPaths, store.LoadAiAnalysis(captureId)!.ScreenshotPaths);
            Assert.Equal(sourcePath, store.LoadScreenshotTextSnapshot(sourcePath)!.SourceScreenshotPath);
            Assert.Equal(updatedTicks, ReadSnapshotUpdatedTicks(root, identity));
            Assert.Equal(revision, store.GetSearchSourceRevision());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
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
