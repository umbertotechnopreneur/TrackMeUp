using System;
using System.IO;
using System.Linq;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class ScreenshotStorageLayoutTests
{
    [Fact]
    public void GetDayDirectory_UsesMonthIsoWeekAndDaySegments()
    {
        var root = CreateAbsoluteRoot();

        var directory = ScreenshotStorageLayout.GetDayDirectory(root, new DateOnly(2026, 8, 23));

        Assert.Equal(
            Path.Combine(root, "2026-08", "week-2026-34", "2026-08-23"),
            directory);
    }

    [Fact]
    public void GetDayDirectory_UsesIsoWeekYearAtCalendarYearBoundary()
    {
        var root = CreateAbsoluteRoot();

        var directory = ScreenshotStorageLayout.GetDayDirectory(root, new DateOnly(2021, 1, 1));

        Assert.Equal(
            Path.Combine(root, "2021-01", "week-2020-53", "2021-01-01"),
            directory);
    }

    [Fact]
    public void NormalizeRoot_PreservesAWindowsDriveRoot()
    {
        var driveRoot = Path.GetPathRoot(Path.GetTempPath())
            ?? throw new InvalidOperationException("The test drive root is unavailable.");

        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(driveRoot)),
            ScreenshotStorageLayout.NormalizeRoot(driveRoot),
            ignoreCase: true);
    }

    [Theory]
    [InlineData("")]
    [InlineData("screenshots")]
    public void GetDayDirectory_RejectsNonAbsoluteRoot(string root)
    {
        Assert.Throws<ArgumentException>(() =>
            ScreenshotStorageLayout.GetDayDirectory(root, new DateOnly(2026, 8, 23)));
    }

    [Fact]
    public void TryGetDay_AcceptsOnlyAnArtifactDirectlyInsideItsCanonicalLeaf()
    {
        var root = CreateAbsoluteRoot();
        var day = new DateOnly(2021, 1, 1);
        var canonicalPath = Path.Combine(
            ScreenshotStorageLayout.GetDayDirectory(root, day),
            $"{Guid.NewGuid():N}_1.0.0_manual_monitor-1.webp");
        var wrongWeekPath = Path.Combine(
            root,
            "2021-01",
            "week-2021-01",
            "2021-01-01",
            Path.GetFileName(canonicalPath));
        var nestedPath = Path.Combine(
            ScreenshotStorageLayout.GetDayDirectory(root, day),
            "extra",
            Path.GetFileName(canonicalPath));

        Assert.True(ScreenshotStorageLayout.TryGetDay(root, canonicalPath, out var parsedDay));
        Assert.Equal(day, parsedDay);
        Assert.Equal(day, ScreenshotStorageLayout.GetDay(root, canonicalPath));
        Assert.False(ScreenshotStorageLayout.TryGetDay(root, wrongWeekPath, out _));
        Assert.False(ScreenshotStorageLayout.TryGetDay(root, nestedPath, out _));
        Assert.False(ScreenshotStorageLayout.TryGetDay(root, "relative.webp", out _));
        Assert.Throws<InvalidDataException>(() => ScreenshotStorageLayout.GetDay(root, wrongWeekPath));
    }

    [Fact]
    public void EnumerateOwnedArtifacts_IsRecursiveAndExcludesUnownedFiles()
    {
        var root = CreateAbsoluteRoot();
        var nestedDirectory = Path.Combine(root, "2026-08", "week-2026-34", "2026-08-23");
        Directory.CreateDirectory(nestedDirectory);
        var rootArtifact = CreateOwnedArtifact(root, Guid.NewGuid(), "monitor-1");
        var nestedArtifact = CreateOwnedArtifact(nestedDirectory, Guid.NewGuid(), "active-window");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not a screenshot");
        File.WriteAllText(Path.Combine(nestedDirectory, "image.webp"), "not owned");

        try
        {
            var artifacts = ScreenshotStorageLayout.EnumerateOwnedArtifacts(root)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(
                new[] { rootArtifact, nestedArtifact }.Order(StringComparer.OrdinalIgnoreCase),
                artifacts,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildMigrationPlan_PutsEveryArtifactFromOneCaptureInTheAuthoritativeDayDirectory()
    {
        var root = CreateAbsoluteRoot();
        Directory.CreateDirectory(root);
        var captureId = Guid.NewGuid();
        var rawTimestamp = new DateTimeOffset(2026, 4, 30, 23, 58, 0, TimeSpan.Zero);
        var olderStoredTimestamp = new DateTimeOffset(2026, 5, 1, 0, 1, 0, TimeSpan.Zero);
        var authoritativeTimestamp = new DateTimeOffset(2026, 5, 1, 0, 2, 0, TimeSpan.Zero);
        var artifacts = new[]
        {
            CreateOwnedArtifact(root, captureId, "monitor-1", raw: true, rawTimestamp),
            CreateOwnedArtifact(root, captureId, "monitor-1", raw: false, olderStoredTimestamp),
            CreateOwnedArtifact(root, captureId, "monitor-2", raw: true, rawTimestamp.AddDays(1)),
            CreateOwnedArtifact(root, captureId, "monitor-2", raw: false, authoritativeTimestamp)
        };

        try
        {
            var expectedDirectory = ScreenshotStorageLayout.GetDayDirectory(root, authoritativeTimestamp);

            var plan = ScreenshotStorageLayout.BuildMigrationPlan(root);

            Assert.Equal(artifacts.Length, plan.Count);
            Assert.All(plan, move => Assert.Equal(
                expectedDirectory,
                Path.GetDirectoryName(move.DestinationPath),
                ignoreCase: true));
            Assert.Equal(
                artifacts.Order(StringComparer.OrdinalIgnoreCase),
                plan.Select(move => move.SourcePath).Order(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildMigrationPlan_KeepsCanonicalLeafAndUsesItsDayForCaptureSiblings()
    {
        var root = CreateAbsoluteRoot();
        Directory.CreateDirectory(root);
        var captureId = Guid.NewGuid();
        var canonicalDay = new DateOnly(2026, 8, 23);
        var canonicalDirectory = ScreenshotStorageLayout.GetDayDirectory(root, canonicalDay);
        var canonicalArtifact = CreateOwnedArtifact(
            canonicalDirectory,
            captureId,
            "monitor-1",
            raw: false,
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        var legacyArtifact = CreateOwnedArtifact(
            root,
            captureId,
            "monitor-2",
            raw: true,
            new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));

        try
        {
            var plan = ScreenshotStorageLayout.BuildMigrationPlan(root);

            var move = Assert.Single(plan);
            Assert.Equal(legacyArtifact, move.SourcePath, ignoreCase: true);
            Assert.Equal(
                Path.Combine(canonicalDirectory, Path.GetFileName(legacyArtifact)),
                move.DestinationPath,
                ignoreCase: true);
            Assert.DoesNotContain(
                plan,
                candidate => string.Equals(candidate.SourcePath, canonicalArtifact, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildMigrationPlan_LeavesCanonicalArtifactInPlaceWhenTimestampHasAnotherLocalDay()
    {
        var root = CreateAbsoluteRoot();
        var canonicalDirectory = ScreenshotStorageLayout.GetDayDirectory(root, new DateOnly(2026, 8, 23));
        var artifact = CreateOwnedArtifact(
            canonicalDirectory,
            Guid.NewGuid(),
            "monitor-1",
            raw: false,
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));

        try
        {
            Assert.Empty(ScreenshotStorageLayout.BuildMigrationPlan(root));
            Assert.True(File.Exists(artifact));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildMigrationPlan_RejectsUnknownNestedLegacyLayoutBeforeMovingFiles()
    {
        var root = CreateAbsoluteRoot();
        var unsupportedDirectory = Path.Combine(root, "legacy", "nested");
        var artifact = CreateOwnedArtifact(unsupportedDirectory, Guid.NewGuid(), "monitor-1");

        try
        {
            var exception = Assert.Throws<InvalidDataException>(() =>
                ScreenshotStorageLayout.BuildMigrationPlan(root));

            Assert.Contains("unsupported directory layout", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(artifact));
            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildMigrationPlan_RejectsDestinationCollisionBeforeReturningAnyMoves()
    {
        var root = CreateAbsoluteRoot();
        Directory.CreateDirectory(root);
        var captureId = Guid.NewGuid();
        var capturedAt = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
        var source = CreateOwnedArtifact(root, captureId, "monitor-1", raw: false, capturedAt);
        var destinationDirectory = ScreenshotStorageLayout.GetDayDirectory(root, capturedAt);
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(destinationDirectory, Path.GetFileName(source));
        File.WriteAllText(destination, "existing");
        File.SetLastWriteTimeUtc(destination, capturedAt.UtcDateTime);

        try
        {
            var exception = Assert.Throws<IOException>(() => ScreenshotStorageLayout.BuildMigrationPlan(root));

            Assert.Contains("artifact", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(source));
            Assert.True(File.Exists(destination));
            Assert.Equal("source", File.ReadAllText(source));
            Assert.Equal("existing", File.ReadAllText(destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildMigrationPlan_RejectsDuplicateArtifactNamesEvenWhenBothHaveNoMove()
    {
        var root = CreateAbsoluteRoot();
        var captureId = Guid.NewGuid();
        var firstDirectory = ScreenshotStorageLayout.GetDayDirectory(root, new DateOnly(2026, 8, 23));
        var secondDirectory = ScreenshotStorageLayout.GetDayDirectory(root, new DateOnly(2026, 8, 24));
        var first = CreateOwnedArtifact(firstDirectory, captureId, "monitor-1");
        var second = CreateOwnedArtifact(secondDirectory, captureId, "monitor-1");

        try
        {
            var exception = Assert.Throws<IOException>(() => ScreenshotStorageLayout.BuildMigrationPlan(root));

            Assert.Contains(Path.GetFileName(first), exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateAbsoluteRoot() =>
        Path.Combine(Path.GetTempPath(), $"TrackMeUp-storage-layout-{Guid.NewGuid():N}");

    private static string CreateOwnedArtifact(
        string directory,
        Guid captureId,
        string target,
        bool raw = false,
        DateTimeOffset? capturedAt = null)
    {
        Directory.CreateDirectory(directory);
        var rawSuffix = raw ? "-raw" : string.Empty;
        var path = Path.Combine(
            directory,
            $"{captureId:N}_1.0.0_manual_{target}{rawSuffix}.webp");
        File.WriteAllText(path, "source");
        if (capturedAt is { } timestamp)
        {
            File.SetLastWriteTimeUtc(path, timestamp.UtcDateTime);
        }

        return path;
    }
}
