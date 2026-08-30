// SPDX-License-Identifier: MIT

using System.Globalization;

namespace TrackMeUp.Services;

/// <summary>Defines and validates the calendar-based directory layout for TrackMeUp screenshot artifacts.</summary>
internal static class ScreenshotStorageLayout
{
    private static readonly EnumerationOptions RecursiveEnumeration = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    /// <summary>Resolves the year-month, ISO-week, and local-day directory for one capture.</summary>
    internal static string GetDayDirectory(string rootDirectory, DateTimeOffset capturedAt) =>
        GetDayDirectory(rootDirectory, DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime));

    /// <summary>Resolves the year-month, ISO-week, and local-day directory for one local date.</summary>
    internal static string GetDayDirectory(string rootDirectory, DateOnly date)
    {
        var root = NormalizeRoot(rootDirectory);
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var month = dateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var week = $"week-{ISOWeek.GetYear(dateTime):0000}-{ISOWeek.GetWeekOfYear(dateTime):00}";
        var day = dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Path.Combine(root, month, week, day);
    }

    /// <summary>Reads the authoritative local day from an artifact stored directly in a canonical layout leaf.</summary>
    internal static DateOnly GetDay(string rootDirectory, string artifactPath)
    {
        if (TryGetDay(rootDirectory, artifactPath, out var day))
        {
            return day;
        }

        throw new InvalidDataException($"Screenshot artifact is not inside a canonical day directory: '{artifactPath}'.");
    }

    /// <summary>Tries to read the local day from a canonical year-month, ISO-week, and day artifact path.</summary>
    internal static bool TryGetDay(string rootDirectory, string artifactPath, out DateOnly day)
    {
        var root = NormalizeRoot(rootDirectory);
        day = default;
        if (string.IsNullOrWhiteSpace(artifactPath) || !Path.IsPathFullyQualified(artifactPath))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(artifactPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }

        var directory = Path.GetDirectoryName(fullPath);
        var daySegment = directory is null ? null : Path.GetFileName(directory);
        if (daySegment is null
            || !DateOnly.TryParseExact(
                daySegment,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDay))
        {
            return false;
        }

        // Comparing the complete leaf validates the month, ISO week-year/week, day, and direct file placement.
        var expectedDirectory = GetDayDirectory(root, parsedDay);
        if (!string.Equals(directory, expectedDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        day = parsedDay;
        return true;
    }

    /// <summary>Enumerates every TrackMeUp-owned artifact below the configured screenshot root.</summary>
    internal static IEnumerable<string> EnumerateOwnedArtifacts(string rootDirectory)
    {
        var root = NormalizeRoot(rootDirectory);
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*", RecursiveEnumeration))
        {
            if (ScreenCaptureService.IsOwnedArtifact(path))
            {
                yield return path;
            }
        }
    }

    /// <summary>Enumerates TrackMeUp-owned artifacts directly inside one validated layout directory.</summary>
    internal static IEnumerable<string> EnumerateOwnedArtifactsInDirectory(string directory)
    {
        var fullDirectory = NormalizeRoot(directory);
        if (!Directory.Exists(fullDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(fullDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            if (ScreenCaptureService.IsOwnedArtifact(path))
            {
                yield return path;
            }
        }
    }

    /// <summary>Builds the complete set of file moves required to enforce the current directory layout.</summary>
    internal static IReadOnlyList<ScreenshotStorageMove> BuildMigrationPlan(string rootDirectory)
    {
        var root = NormalizeRoot(rootDirectory);
        var artifacts = EnumerateOwnedArtifacts(root)
            .Select(path => new FileInfo(path))
            .ToArray();

        foreach (var artifact in artifacts)
        {
            var artifactDirectory = artifact.DirectoryName is { } directory
                ? NormalizeRoot(directory)
                : throw new InvalidDataException($"Screenshot artifact has no parent directory: '{artifact.FullName}'.");
            if (!string.Equals(artifactDirectory, root, StringComparison.OrdinalIgnoreCase)
                && !TryGetDay(root, artifact.FullName, out _))
            {
                // The only superseded contract is the former flat root. Unknown nested layouts are
                // rejected before any move so crash recovery never has to guess an original path.
                throw new InvalidDataException(
                    $"Screenshot artifact uses an unsupported directory layout: '{artifact.FullName}'.");
            }
        }

        var duplicateName = artifacts
            .GroupBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new IOException($"Multiple screenshot artifacts share the file name '{duplicateName.Key}'.");
        }

        var moves = artifacts
            .GroupBy(GetCaptureId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group =>
            {
                var canonicalDays = group
                    .Select(file => TryGetDay(root, file.FullName, out var day) ? day : (DateOnly?)null)
                    .Where(day => day.HasValue)
                    .Select(day => day!.Value)
                    .Distinct()
                    .ToArray();
                if (canonicalDays.Length > 1)
                {
                    throw new InvalidDataException(
                        $"Screenshot capture '{group.Key}' is split across canonical day directories.");
                }

                DateOnly authoritativeDay;
                if (canonicalDays.Length == 1)
                {
                    // Once migrated, the canonical path is authoritative even if file metadata changes timezone or date.
                    authoritativeDay = canonicalDays[0];
                }
                else
                {
                    // The latest retained artifact is the authoritative legacy timestamp. Every monitor
                    // and raw/stored variant from the same capture pass must land in the same day folder.
                    var timestampSource = group
                        .OrderByDescending(file => !Path.GetFileNameWithoutExtension(file.Name).EndsWith("-raw", StringComparison.OrdinalIgnoreCase))
                        .ThenByDescending(file => file.LastWriteTimeUtc)
                        .First();
                    var capturedAt = new DateTimeOffset(timestampSource.LastWriteTimeUtc, TimeSpan.Zero);
                    authoritativeDay = DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime);
                }

                var dayDirectory = GetDayDirectory(root, authoritativeDay);
                return group.Select(file => new ScreenshotStorageMove(
                    file.FullName,
                    Path.Combine(dayDirectory, file.Name)));
            })
            .Where(move => !string.Equals(move.SourcePath, move.DestinationPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var duplicateDestination = moves
            .GroupBy(move => move.DestinationPath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDestination is not null)
        {
            throw new IOException($"Multiple screenshot artifacts resolve to '{duplicateDestination.Key}'.");
        }

        foreach (var move in moves)
        {
            // Migration never overwrites an existing artifact: a collision needs explicit recovery.
            if (File.Exists(move.DestinationPath))
            {
                throw new IOException($"Screenshot migration destination already exists: '{move.DestinationPath}'.");
            }
        }

        return moves;
    }

    private static string GetCaptureId(FileInfo file)
    {
        var separator = file.Name.IndexOf('_');
        if (separator <= 0)
        {
            throw new InvalidDataException($"Screenshot artifact has no capture identifier: '{file.FullName}'.");
        }

        var captureId = file.Name[..separator];
        if (!Guid.TryParseExact(captureId, "N", out var parsedCaptureId))
        {
            throw new InvalidDataException($"Screenshot artifact has an invalid capture identifier: '{file.FullName}'.");
        }

        return parsedCaptureId.ToString("N");
    }

    /// <summary>Returns whether a fully qualified path is the root itself or one of its descendants.</summary>
    internal static bool IsSameOrDescendant(string path, string rootDirectory)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = NormalizeRoot(rootDirectory);
        var rootPrefix = Path.EndsInDirectorySeparator(root)
            ? root
            : root + Path.DirectorySeparatorChar;
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeRoot(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Path.IsPathFullyQualified(rootDirectory))
        {
            throw new ArgumentException("A fully qualified screenshot directory is required.", nameof(rootDirectory));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
    }
}

/// <summary>Describes one fail-fast screenshot artifact move during layout migration.</summary>
internal sealed record ScreenshotStorageMove(string SourcePath, string DestinationPath);
