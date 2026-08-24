using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Data.Sqlite;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class DataArchiveServiceTests
{
    [Fact]
    public void ExportPreviewAndMerge_RoundTripsSqlAndScreenshotsIdempotently()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourceDirectory = Path.Combine(root, "source");
            var targetDirectory = Path.Combine(root, "target");
            var sourceScreenshots = Path.Combine(sourceDirectory, "screenshots");
            var targetScreenshots = Path.Combine(targetDirectory, "screenshots");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(targetDirectory);

            var source = new LocalStore(sourceDirectory);
            source.SaveSettings(source.LoadSettings() with { ScreenshotDirectory = sourceScreenshots });
            var sourceSettings = source.LoadSettings();
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            source.AppendSample(new ActivitySample(
                capturedAt,
                5,
                "active",
                "test",
                "Archive test",
                "round trip",
                "Archive window",
                sourceSettings.InstallationId,
                2,
                1,
                new Dictionary<string, string> { [ActivityAttributeKeys.SpanLabel] = "Portable work" }));

            var captureId = Guid.NewGuid().ToString("N");
            var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(sourceScreenshots, capturedAt);
            Directory.CreateDirectory(dayDirectory);
            var screenshotPath = Path.Combine(dayDirectory, $"{captureId}_1.0.0_manual_monitor-1.webp");
            File.WriteAllBytes(screenshotPath, [1, 2, 3, 4, 5]);
            File.SetLastWriteTimeUtc(screenshotPath, capturedAt.UtcDateTime);
            source.UpsertScreenshotIntervalTelemetry(
                captureId,
                [screenshotPath],
                new ScreenshotIntervalTelemetry(capturedAt.AddMinutes(-5), capturedAt, 12, 4));

            var archivePath = Path.Combine(root, "history.tmuarchive");
            var exporter = new DataArchiveService(source);
            var exported = exporter.Export(new DataArchiveExportRequest(archivePath), CancellationToken.None);

            Assert.Equal(1, exported.ActivitySampleCount);
            Assert.Equal(1, exported.ScreenshotFileCount);
            Assert.True(File.Exists(archivePath));
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                Assert.Contains(zip.Entries, entry => entry.FullName == "manifest.json");
                Assert.Contains(zip.Entries, entry => entry.FullName == "data.sqlite3");
                Assert.DoesNotContain(zip.Entries, entry => entry.FullName.Contains(sourceDirectory, StringComparison.OrdinalIgnoreCase));
            }

            var target = new LocalStore(targetDirectory);
            target.SaveSettings(target.LoadSettings() with { ScreenshotDirectory = targetScreenshots });
            var importer = new DataArchiveService(target);
            var preview = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None);
            Assert.False(preview.AlreadyImported);
            Assert.Single(preview.Installations);

            var imported = importer.Import(preview.PlanId, CancellationToken.None);
            Assert.Equal(1, imported.AddedInstallationCount);
            Assert.Equal(1, imported.AddedActivitySampleCount);
            Assert.Equal(1, imported.AddedScreenshotFileCount);
            Assert.Equal(2, target.GetInstallationProfiles().Count);
            Assert.Equal(1, ReadCount(targetDirectory, "activity_samples"));

            var importedScreenshot = Path.Combine(
                ScreenshotStorageLayout.GetDayDirectory(targetScreenshots, capturedAt),
                Path.GetFileName(screenshotPath));
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, File.ReadAllBytes(importedScreenshot));
            var gallery = target.GetScreenshotGallery(DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));
            var galleryItem = Assert.Single(gallery.Items);
            Assert.Equal(sourceSettings.InstallationId, galleryItem.Installation?.InstallationId);

            var secondPreview = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None);
            Assert.True(secondPreview.AlreadyImported);
            var secondImport = importer.Import(secondPreview.PlanId, CancellationToken.None);
            Assert.Equal(0, secondImport.AddedActivitySampleCount);
            Assert.Equal(1, secondImport.SkippedActivitySampleCount);
            Assert.Equal(0, secondImport.AddedScreenshotFileCount);
            Assert.Equal(1, secondImport.SkippedScreenshotFileCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreviewImport_RejectsCaseInsensitiveArchiveEntryCollisions()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var archivePath = CreateMinimalArchive(root);
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
            {
                var alias = archive.CreateEntry("MANIFEST.JSON");
                using var destination = alias.Open();
                destination.Write([1]);
            }

            var importer = CreateImporter(root);
            var exception = Assert.Throws<InvalidDataException>(() =>
                importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None));

            Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreviewImport_RejectsDeclaredEntriesOutsideTheArchiveContract()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var archivePath = CreateMinimalArchive(root);
            AddDeclaredUnexpectedEntry(archivePath, "notes.txt", Encoding.UTF8.GetBytes("not TrackMeUp data"));

            var importer = CreateImporter(root);
            Assert.Throws<InvalidDataException>(() =>
                importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PreviewImport_RejectsTraversalEntryNames()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var archivePath = CreateMinimalArchive(root);
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
            {
                var traversal = archive.CreateEntry("../outside.webp");
                using var destination = traversal.Open();
                destination.Write([1]);
            }

            var importer = CreateImporter(root);
            Assert.Throws<InvalidDataException>(() =>
                importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ConstructorRecovery_UsesImportLedgerAsTheCommitAuthority()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var targetDirectory = Path.Combine(root, "recovery-target");
            var screenshotRoot = Path.Combine(targetDirectory, "screenshots");
            Directory.CreateDirectory(targetDirectory);
            var target = new LocalStore(targetDirectory);
            target.SaveSettings(target.LoadSettings() with { ScreenshotDirectory = screenshotRoot });

            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var uncommittedPath = CreateRecoveryScreenshot(screenshotRoot, capturedAt, [1, 2, 3]);
            var uncommittedArchiveId = Guid.NewGuid();
            var uncommittedFingerprint = new string('a', 64);
            WriteRecoveryJournal(targetDirectory, uncommittedArchiveId, uncommittedFingerprint, uncommittedPath);

            _ = new DataArchiveService(target);

            Assert.False(File.Exists(uncommittedPath));
            Assert.False(File.Exists(Path.Combine(targetDirectory, "archive-import-journal.json")));

            var committedPath = CreateRecoveryScreenshot(screenshotRoot, capturedAt, [4, 5, 6]);
            var committedArchiveId = Guid.NewGuid();
            var committedFingerprint = new string('b', 64);
            InsertImportLedger(targetDirectory, committedArchiveId, committedFingerprint);
            WriteRecoveryJournal(targetDirectory, committedArchiveId, committedFingerprint, committedPath);

            _ = new DataArchiveService(target);

            Assert.True(File.Exists(committedPath));
            Assert.False(File.Exists(Path.Combine(targetDirectory, "archive-import-journal.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static int ReadCount(string dataDirectory, string table)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, SqliteActivityStore.DatabaseFileName),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string CreateMinimalArchive(string root)
    {
        var sourceDirectory = Path.Combine(root, "minimal-source");
        Directory.CreateDirectory(sourceDirectory);
        var source = new LocalStore(sourceDirectory);
        var archivePath = Path.Combine(root, Guid.NewGuid().ToString("N") + ".tmuarchive");
        _ = new DataArchiveService(source).Export(
            new DataArchiveExportRequest(archivePath, IncludeScreenshots: false),
            CancellationToken.None);
        return archivePath;
    }

    private static DataArchiveService CreateImporter(string root)
    {
        var targetDirectory = Path.Combine(root, "import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);
        return new DataArchiveService(new LocalStore(targetDirectory));
    }

    private static void AddDeclaredUnexpectedEntry(string archivePath, string entryName, byte[] content)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("The test archive manifest is missing.");
        JsonObject manifest;
        using (var manifestStream = manifestEntry.Open())
        {
            manifest = JsonNode.Parse(manifestStream)?.AsObject()
                ?? throw new InvalidOperationException("The test archive manifest is invalid.");
        }

        manifestEntry.Delete();
        var entries = manifest["entries"]?.AsArray()
            ?? throw new InvalidOperationException("The test archive entry list is missing.");
        entries.Add(new JsonObject
        {
            ["path"] = entryName,
            ["length"] = content.LongLength,
            ["sha256"] = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
        });

        var unexpected = archive.CreateEntry(entryName);
        using (var destination = unexpected.Open())
        {
            destination.Write(content);
        }

        var replacementManifest = archive.CreateEntry("manifest.json");
        using var replacementStream = replacementManifest.Open();
        JsonSerializer.Serialize(replacementStream, manifest);
    }

    private static string CreateRecoveryScreenshot(
        string screenshotRoot,
        DateTimeOffset capturedAt,
        byte[] content)
    {
        var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(screenshotRoot, capturedAt);
        Directory.CreateDirectory(dayDirectory);
        var path = Path.Combine(
            dayDirectory,
            $"{Guid.NewGuid():N}_1.0.0_manual_monitor-1.webp");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static void WriteRecoveryJournal(
        string dataDirectory,
        Guid archiveId,
        string fingerprint,
        string screenshotPath)
    {
        var content = File.ReadAllBytes(screenshotPath);
        var journal = new
        {
            archiveId,
            fingerprint,
            files = new[]
            {
                new
                {
                    path = screenshotPath,
                    sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
                }
            }
        };
        File.WriteAllText(
            Path.Combine(dataDirectory, "archive-import-journal.json"),
            JsonSerializer.Serialize(journal, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static void InsertImportLedger(string dataDirectory, Guid archiveId, string fingerprint)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(dataDirectory, SqliteActivityStore.DatabaseFileName),
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO archive_imports (archive_id, archive_fingerprint, imported_utc_ticks)
            VALUES ($archiveId, $fingerprint, $importedAt);
            """;
        command.Parameters.AddWithValue("$archiveId", archiveId.ToString("N"));
        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        command.Parameters.AddWithValue("$importedAt", DateTimeOffset.UtcNow.UtcDateTime.Ticks);
        command.ExecuteNonQuery();
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrackMeUp.Archive.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
