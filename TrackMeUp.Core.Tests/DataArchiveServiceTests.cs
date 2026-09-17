// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class DataArchiveServiceTests
{
    /// <summary>Publishes an export into a destination whose parent directories do not exist yet.</summary>
    [Fact]
    public void Export_CreatesDestinationDirectoryBeforeWritingArchive()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = new LocalStore(Path.Combine(root, "source"));
            var destination = Path.Combine(root, "new", "nested", "history.tmuarchive");

            var result = new DataArchiveService(source).Export(
                new DataArchiveExportRequest(destination, IncludeScreenshots: false), CancellationToken.None);

            Assert.Equal(destination, result.Path);
            using var archive = ZipFile.OpenRead(destination);
            Assert.NotNull(archive.GetEntry("data.sqlite3"));
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>A failed search invalidation must roll back the imported rows and the import ledger together.</summary>
    [Fact]
    public void Import_SearchMarkerFailureRollsBackTheMerge()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var archivePath = CreateMinimalArchive(root);
            var targetDirectory = Path.Combine(root, "target");
            var target = new LocalStore(targetDirectory);
            var importer = new DataArchiveService(target);
            var plan = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = target.ActivityDatabasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TRIGGER fail_archive_search_marker BEFORE INSERT ON search_change_log
                    WHEN NEW.kind = 'rebuild' AND NEW.entity_id = 'archive-import'
                    BEGIN
                        SELECT RAISE(ABORT, 'test search marker failure');
                    END;
                    """;
                command.ExecuteNonQuery();
            }

            var exception = Assert.Throws<SqliteException>(() => importer.Import(plan.PlanId, CancellationToken.None));

            Assert.Contains("test search marker failure", exception.Message, StringComparison.Ordinal);
            Assert.Single(target.GetInstallationProfiles());
            Assert.Equal(0, ReadCount(targetDirectory, "archive_imports"));
            Assert.Equal(0, target.ActivityRevision);
            Assert.DoesNotContain(target.LoadSearchSourceChanges(0, 100), change => change.EntityId == "archive-import");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Exercises the current SQLite AI path contract and OCR paths across archive and destination roots.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExportAndImport_WithAiAndOcrRecords_RemapsEveryScreenshotPath(bool includeScreenshots)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourceDirectory = Path.Combine(root, "source");
            var targetDirectory = Path.Combine(root, "target");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(targetDirectory);
            var source = new LocalStore(sourceDirectory);
            var sourceScreenshots = Path.Combine(sourceDirectory, "screenshots");
            source.SaveSettings(source.LoadSettings() with { ScreenshotDirectory = sourceScreenshots });
            var capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            var captureId = Guid.NewGuid().ToString("N");
            var dayDirectory = ScreenshotStorageLayout.GetDayDirectory(sourceScreenshots, capturedAt);
            Directory.CreateDirectory(dayDirectory);
            var sourcePaths = Enumerable.Range(1, 2)
                .Select(monitor => Path.Combine(dayDirectory, $"{captureId}_1.0.0_manual_monitor-{monitor}.webp"))
                .ToArray();
            foreach (var path in sourcePaths)
            {
                File.WriteAllBytes(path, [1, 2, 3]);
                File.SetLastWriteTimeUtc(path, capturedAt.UtcDateTime);
                source.UpsertScreenshotTextSnapshot(captureId, new ScreenshotTextSnapshot(
                    path,
                    new OcrRawSnapshot(ScreenshotTextExtractionStatus.Succeeded, "Archive test text", "en-US",
                        null, capturedAt, "test-ocr", 100, 100, [])));
            }

            var sourcePathList = string.Join(';', sourcePaths);
            source.UpsertScreenshotIntervalTelemetry(captureId, sourcePaths,
                new ScreenshotIntervalTelemetry(capturedAt.AddMinutes(-5), capturedAt, 12, 4));
            source.UpsertCaptureHardwareSnapshot(captureId, new SystemSnapshot(capturedAt, "partial",
            [
                new HardwareDeviceSnapshot("/gpu/0", "Archive GPU", "GpuNvidia", capturedAt,
                [new HardwareSensorSnapshot("/gpu/0/power/0", "GPU Power", "Power", "W", 37.5)])
            ]));
            AppendAnalysis(source, captureId, capturedAt, sourcePathList, sourcePaths.Length);
            var archivePath = Path.Combine(root, "ai-history.tmuarchive");
            var exporter = new DataArchiveService(source);
            var exported = exporter.Export(
                new DataArchiveExportRequest(archivePath, IncludeScreenshots: includeScreenshots),
                CancellationToken.None);

            Assert.Equal(1, exported.AiAnalysisCount);
            Assert.Equal(1, exported.AiRequestCount);
            Assert.Equal(includeScreenshots ? 2 : 0, exported.ScreenshotFileCount);
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                var snapshotPath = Path.Combine(root, "exported.sqlite3");
                archive.GetEntry("data.sqlite3")!.ExtractToFile(snapshotPath);
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = snapshotPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT screenshot_paths FROM ai_analysis_results;";
                var archivedPaths = Assert.IsType<string>(command.ExecuteScalar()).Split(';');
                Assert.Equal(2, archivedPaths.Length);
                Assert.All(archivedPaths, path =>
                {
                    Assert.StartsWith("screenshots/", path, StringComparison.Ordinal);
                    Assert.False(Path.IsPathFullyQualified(path));
                    Assert.DoesNotContain(sourceDirectory, path, StringComparison.OrdinalIgnoreCase);
                });
            }

            var target = new LocalStore(targetDirectory);
            var targetScreenshots = Path.Combine(targetDirectory, "screenshots");
            target.SaveSettings(target.LoadSettings() with { ScreenshotDirectory = targetScreenshots });
            var importer = new DataArchiveService(target);
            var preview = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None);
            var imported = importer.Import(preview.PlanId, CancellationToken.None);
            Assert.Equal(1, imported.AddedAiAnalysisCount);
            Assert.Equal(1, imported.AddedAiRequestCount);
            var importedHardware = Assert.IsType<SystemSnapshot>(target.LoadCaptureHardwareSnapshot(captureId));
            Assert.Equal(capturedAt, importedHardware.Timestamp);
            Assert.Equal(37.5d, Assert.Single(Assert.Single(importedHardware.Devices).Sensors).Value);

            var targetPaths = sourcePaths.Select(path => Path.Combine(
                ScreenshotStorageLayout.GetDayDirectory(targetScreenshots, capturedAt), Path.GetFileName(path))).ToArray();
            var importedAnalysis = Assert.IsType<AiAnalysis>(target.LoadAiAnalysis(captureId));
            Assert.Equal(string.Join(';', targetPaths), importedAnalysis.ScreenshotPaths);
            foreach (var path in targetPaths)
            {
                var snapshot = Assert.IsType<ScreenshotTextSnapshot>(target.LoadScreenshotTextSnapshot(path));
                Assert.Equal(path, snapshot.SourceScreenshotPath);
                Assert.Equal("Archive test text", snapshot.Ocr.RawText);
                Assert.Equal(includeScreenshots, File.Exists(path));
            }

            Assert.Equal(sourcePathList, source.LoadAiAnalysis(captureId)!.ScreenshotPaths);
            Assert.Equal(sourcePaths[0], source.LoadScreenshotTextSnapshot(sourcePaths[0])!.SourceScreenshotPath);
            var repeat = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None);
            Assert.True(repeat.AlreadyImported);
            Assert.Equal(1, importer.Import(repeat.PlanId, CancellationToken.None).SkippedAiAnalysisCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Preserves analysis records that have no retained screenshot references.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExportAndImport_WithNoAnalysisScreenshotPaths_PreservesTheRecord(string? screenshotPaths)
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var sourceDirectory = Path.Combine(root, "source");
            var targetDirectory = Path.Combine(root, "target");
            Directory.CreateDirectory(sourceDirectory);
            Directory.CreateDirectory(targetDirectory);
            var source = new LocalStore(sourceDirectory);
            var captureId = Guid.NewGuid().ToString("N");
            AppendAnalysis(source, captureId, DateTimeOffset.UtcNow.AddMinutes(-1), screenshotPaths, 0);
            var archivePath = Path.Combine(root, "no-screenshots.tmuarchive");
            _ = new DataArchiveService(source).Export(
                new DataArchiveExportRequest(archivePath, IncludeScreenshots: false), CancellationToken.None);
            var target = new LocalStore(targetDirectory);
            var importer = new DataArchiveService(target);
            var preview = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None);
            _ = importer.Import(preview.PlanId, CancellationToken.None);

            Assert.Equal(screenshotPaths, target.LoadAiAnalysis(captureId)!.ScreenshotPaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Preserves the committed import result even when post-commit journal or staging cleanup fails.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("journal")]
    [InlineData("staging")]
    public void ExportPreviewAndMerge_RoundTripsSqlAndScreenshotsIdempotently(string? cleanupFailure)
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
            var logger = new RecordingLogger();
            var journalPath = Path.Combine(targetDirectory, "archive-import-journal.json");
            var failureInjected = false;
            var importer = new DataArchiveService(target, logger, deleteFile: path =>
            {
                if (!failureInjected
                    && ((cleanupFailure == "journal" && path == journalPath)
                        || (cleanupFailure == "staging" && path.EndsWith(".importing", StringComparison.Ordinal))))
                {
                    failureInjected = true;
                    throw new IOException("Simulated post-commit cleanup failure.");
                }

                File.Delete(path);
            });
            var preview = importer.PreviewImport(new DataArchiveImportPreviewRequest(archivePath), CancellationToken.None);
            Assert.False(preview.AlreadyImported);
            Assert.Single(preview.Installations);

            var imported = importer.Import(preview.PlanId, CancellationToken.None);
            Assert.Equal(1, imported.AddedInstallationCount);
            Assert.Equal(1, imported.AddedActivitySampleCount);
            Assert.Equal(1, imported.AddedScreenshotFileCount);
            Assert.Equal(2, target.GetInstallationProfiles().Count);
            Assert.Equal(1, ReadCount(targetDirectory, "activity_samples"));
            Assert.Equal(1, target.ActivityRevision);
            Assert.Contains(target.LoadSearchSourceChanges(0, 100), change => change.Kind == "rebuild" && change.EntityId == "archive-import");
            Assert.Equal(cleanupFailure is not null, failureInjected);
            if (cleanupFailure is not null)
            {
                var warning = Assert.Single(logger.Entries);
                Assert.Equal(LogLevel.Warning, warning.Level);
                Assert.Contains("import committed", warning.Message, StringComparison.Ordinal);
            }
            else
            {
                Assert.Empty(logger.Entries);
            }

            if (cleanupFailure == "journal")
            {
                Assert.True(File.Exists(journalPath));
                _ = new DataArchiveService(target);
                Assert.False(File.Exists(journalPath));
            }

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

    private static void AppendAnalysis(
        LocalStore store,
        string captureId,
        DateTimeOffset capturedAt,
        string? screenshotPaths,
        int imageCount)
    {
        var usage = new AiRequestUsageRecord(
            Guid.NewGuid().ToString("N"), captureId, capturedAt, capturedAt.AddMilliseconds(10),
            "snapshot.manual", "screen_analysis", "test-provider", "provider.invalid", "test-model", "test-model",
            null, null, 200, 10, null, imageCount, 10, 100, new AiUsageMetrics(10, 5, 15), "stop", true, null);
        var analysis = new AiAnalysis(
            capturedAt, "Test", "Archive", "Portable AI analysis", store.LoadSettings().InstallationId,
            screenshotPaths, CorrelationId: captureId, Origin: "snapshot.manual");
        store.AppendAiAnalysisAndUsage(usage, analysis);
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

    private sealed class RecordingLogger : ILogger
    {
        internal List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
