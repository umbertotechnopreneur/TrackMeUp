using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>
/// Handles current-format SQLite activity history and local settings persistence.
/// </summary>
public sealed class LocalStore
{
    private readonly UtilityService _utilities = new();
    private readonly SqliteActivityStore _activity;
    private readonly string _settingsPath;
    private readonly string _settingsBootstrapMutexName;
    private readonly object _fileLock = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes persistence file paths in the user app data folder.
    /// </summary>
    /// <param name="dataDirectory">Optional data directory override intended for isolated hosts and tests.</param>
    public LocalStore(string? dataDirectory = null)
    {
        var resolvedDataDirectory = string.IsNullOrWhiteSpace(dataDirectory)
            ? _utilities.AppDataDirectory
            : Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(resolvedDataDirectory);
        var unsupportedLegacyPath = new[]
        {
            Path.Combine(resolvedDataDirectory, "activity.jsonl"),
            Path.Combine(resolvedDataDirectory, "analyses.jsonl")
        }.FirstOrDefault(File.Exists);
        if (unsupportedLegacyPath is not null)
        {
            // Greenfield storage is intentional: never import, read, delete, or silently ignore legacy history.
            throw new InvalidOperationException($"Legacy storage '{Path.GetFileName(unsupportedLegacyPath)}' is not supported; remove it before starting TrackMeUp.");
        }

        _activity = new SqliteActivityStore(Path.Combine(resolvedDataDirectory, SqliteActivityStore.DatabaseFileName));
        _settingsPath = Path.Combine(resolvedDataDirectory, "appsettings.json");
        var settingsFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_settingsPath.ToUpperInvariant())))[..32];
        _settingsBootstrapMutexName = $"Local\\TrackMeUp.Settings.{settingsFingerprint}";
    }

    /// <summary>
    /// Appends one activity sample to the SQLite activity store.
    /// </summary>
    public void AppendSample(ActivitySample sample) => _activity.Append(sample);

    /// <summary>Persists one sanitized AI request-usage record in SQLite.</summary>
    internal void AppendAiUsage(AiRequestUsageRecord usage) => _activity.AppendFailedAiRequest(usage);

    /// <summary>Persists provider usage and the corresponding analysis in one SQLite transaction.</summary>
    internal void AppendAiAnalysisAndUsage(AiRequestUsageRecord usage, AiAnalysis analysis)
    {
        // The current SQLite schema owns the correlated request/result pair.
        _activity.AppendSuccessfulAiRequestAndAnalysis(usage, analysis);
    }

    /// <summary>
    /// Loads application settings and rejects malformed persisted configuration.
    /// </summary>
    public AppSettings LoadSettings() => WithSettingsMutex(() =>
    {
        var settings = File.Exists(_settingsPath)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _json)
                ?? throw new InvalidOperationException("The TrackMeUp settings file must contain a JSON object.")
            : new AppSettings();

        var normalized = SettingsCatalog.NormalizePersisted(settings, _utilities.GetDefaultScreenshotDirectory());
        return EnsureInstallationId(normalized);
    });

    /// <summary>
    /// Reads the persisted installation identifier without creating or rewriting settings.
    /// </summary>
    /// <returns>The existing installation identifier, or null when settings have not been initialized by the runtime.</returns>
    public string? TryLoadInstallationId()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _json)
            ?? throw new InvalidOperationException("The TrackMeUp settings file must contain a JSON object.");
        if (!IsValidInstallationId(settings.InstallationId))
        {
            throw new InvalidOperationException("The TrackMeUp settings file has an invalid installation identity.");
        }

        return settings.InstallationId;
    }

    /// <summary>
    /// Persists application settings to JSON file.
    /// </summary>
    /// <param name="settings">Settings payload.</param>
    public void SaveSettings(AppSettings settings) => WithSettingsMutex(() =>
    {
        var normalized = SettingsCatalog.NormalizePersisted(settings, _utilities.GetDefaultScreenshotDirectory());
        var payload = EnsureInstallationId(normalized);
        WriteSettingsFile(payload);
        return true;
    });

    /// <summary>Writes a fully normalized settings snapshot using an atomic same-directory replacement.</summary>
    private void WriteSettingsFile(AppSettings payload)
    {
        var temporaryPath = _settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        lock (_fileLock)
        {
            // Write and flush in the target directory so a completed replacement is never a torn JSON file.
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonSerializer.Serialize(payload, _json));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Replace(temporaryPath, _settingsPath, null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporaryPath, _settingsPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    /// <summary>
    /// Reads an environment variable from process/user/machine scopes.
    /// </summary>
    /// <param name="keyName">Environment variable key.</param>
    /// <returns>Value when found, null otherwise.</returns>
    public string? LoadApiKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return null;
        }

        var trimmedKeyName = keyName.Trim();
        return Environment.GetEnvironmentVariable(trimmedKeyName, EnvironmentVariableTarget.Process)
            ?? Environment.GetEnvironmentVariable(trimmedKeyName, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(trimmedKeyName, EnvironmentVariableTarget.Machine);
    }
    /// <summary>
    /// Loads the default API key variable using configured provider name.
    /// </summary>
    public string? LoadApiKey() => LoadApiKey("OPENAI_API_KEY");

    /// <summary>Counts today's persisted AI analyses using the current SQLite schema.</summary>
    public int GetTodayAnalysisCount()
    {
        var localStart = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local));
        var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, TimeZoneInfo.Local));
        return _activity.CountAiAnalysisResults(startUtc, endUtc);
    }

    /// <summary>Loads the most recent analysis from the current SQLite store.</summary>
    public AiAnalysis? LoadLatestAnalysis() => _activity.LoadLatestAiAnalysis();

    /// <summary>Deletes local snapshot-analysis records that reference one retained screenshot.</summary>
    internal int DeleteAiAnalysesReferencingScreenshot(string screenshotPath)
        => _activity.DeleteAiAnalysesReferencingScreenshot(screenshotPath);

    /// <summary>Returns the first screenshot path referenced by the current latest analysis.</summary>
    public string? LoadLatestPrimaryScreenshot()
    {
        var latest = LoadLatestAnalysis()?.ScreenshotPaths;
        return string.IsNullOrWhiteSpace(latest) ? null : latest.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    /// <summary>Finds every owned image artifact generated by the same screenshot capture.</summary>
    /// <param name="screenshotPath">Absolute path to one retained capture artifact.</param>
    /// <returns>The stored and raw artifacts for the capture, or an empty list for an invalid path.</returns>
    public IReadOnlyList<string> FindScreenshotArtifacts(string screenshotPath)
    {
        if (!ScreenCaptureService.IsOwnedArtifact(screenshotPath) || !Path.IsPathFullyQualified(screenshotPath))
        {
            return Array.Empty<string>();
        }

        var fullPath = Path.GetFullPath(screenshotPath);
        if (!File.Exists(fullPath))
        {
            return Array.Empty<string>();
        }

        var settings = LoadSettings();
        var directory = string.IsNullOrWhiteSpace(settings.ScreenshotDirectory)
            ? _utilities.GetDefaultScreenshotDirectory()
            : settings.ScreenshotDirectory;
        var configuredDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var artifactDirectory = Path.GetDirectoryName(fullPath)?
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(configuredDirectory, artifactDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        var identity = ScreenshotIdentity(Path.GetFileName(fullPath));
        return Directory.EnumerateFiles(configuredDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(ScreenCaptureService.IsOwnedArtifact)
            .Where(path => string.Equals(ScreenshotIdentity(Path.GetFileName(path)), identity, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Lists owned screenshot artifacts for one local calendar date without exposing unrelated files.
    /// </summary>
    /// <param name="date">The local date represented by the gallery.</param>
    /// <returns>A presentation-neutral screenshot projection ordered newest first.</returns>
    public ScreenshotGallery GetScreenshotGallery(DateOnly date)
    {
        var settings = LoadSettings();
        var directory = string.IsNullOrWhiteSpace(settings.ScreenshotDirectory)
            ? _utilities.GetDefaultScreenshotDirectory()
            : settings.ScreenshotDirectory;
        if (!Directory.Exists(directory))
        {
            return new ScreenshotGallery(date, Array.Empty<ScreenshotGalleryItem>());
        }

        var files = new List<FileInfo>();
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            if (!ScreenCaptureService.IsOwnedArtifact(path))
            {
                continue;
            }

            var file = new FileInfo(path);
            var capturedAt = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
            if (DateOnly.FromDateTime(capturedAt.ToLocalTime().DateTime) == date)
            {
                files.Add(file);
            }
        }

        var items = files
            .GroupBy(file => ScreenshotIdentity(file.Name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(file => IsPreferredStoredArtifact(file.Name))
                .ThenByDescending(file => file.LastWriteTimeUtc)
                .First())
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file =>
            {
                var capturedAt = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
                return new ScreenshotGalleryItem(
                    capturedAt,
                    file.FullName,
                    FindForegroundApplication(capturedAt),
                    GetCaptureKind(file.Name),
                    GetCaptureOrigin(file.Name));
            })
            .ToArray();

        return new ScreenshotGallery(date, items);
    }

    private string FindForegroundApplication(DateTimeOffset capturedAt)
    {
        var samples = new List<ActivitySample>();
        var fromUtc = capturedAt.ToUniversalTime().AddMinutes(-2);
        var toUtc = capturedAt.ToUniversalTime().AddMinutes(2);
        _activity.VisitOverlapping(fromUtc, toUtc, CancellationToken.None, samples.Add);
        return samples
            .OrderBy(sample => Math.Abs((sample.Timestamp - capturedAt).TotalMilliseconds))
            .Select(sample => sample.Application)
            .FirstOrDefault(application => !string.IsNullOrWhiteSpace(application))
            ?? "Desktop";
    }

    private static string ScreenshotIdentity(string fileName)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return withoutExtension.EndsWith("-raw", StringComparison.OrdinalIgnoreCase)
            ? withoutExtension[..^4]
            : withoutExtension;
    }

    private static bool IsPreferredStoredArtifact(string fileName) =>
        !Path.GetFileNameWithoutExtension(fileName).EndsWith("-raw", StringComparison.OrdinalIgnoreCase);

    private static string GetCaptureKind(string fileName) =>
        fileName.Contains("_active-window", StringComparison.OrdinalIgnoreCase)
            ? "Active window"
            : "Monitor";

    private static string GetCaptureOrigin(string fileName)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (withoutExtension.Contains("_manual_", StringComparison.OrdinalIgnoreCase))
        {
            return ScreenshotCaptureOrigins.Manual;
        }

        if (withoutExtension.Contains("_scheduled_", StringComparison.OrdinalIgnoreCase))
        {
            return ScreenshotCaptureOrigins.Scheduled;
        }

        throw new InvalidDataException($"Screenshot artifact has no valid capture origin: {fileName}");
    }

    /// <summary>
    /// Lists data files that contain at least one expired, parseable record without deleting them.
    /// </summary>
    /// <param name="cutoffUtc">Oldest allowed last-write timestamp.</param>
    /// <returns>Files that would be compacted by the requested cutoff.</returns>
    public IReadOnlyList<string> GetRetentionCandidates(DateTimeOffset cutoffUtc)
    {
        return GetRetentionPreview(cutoffUtc).Paths;
    }

    /// <summary>Counts expired local records and their estimated or encoded size without changing the data files.</summary>
    /// <param name="cutoffUtc">Records older than this instant are counted.</param>
    /// <returns>Record-level retention preview with affected file paths.</returns>
    public DataRetentionPreview GetRetentionPreview(DateTimeOffset cutoffUtc)
    {
        var samples = _activity.GetRetentionPreview(cutoffUtc);
        var paths = samples.Count > 0 ? new[] { _activity.DatabasePath } : Array.Empty<string>();
        return new DataRetentionPreview(samples.Count, samples.Bytes, paths);
    }

    /// <summary>Removes expired records from the current SQLite store.</summary>
    /// <param name="cutoffUtc">Records older than this instant are removed.</param>
    /// <returns>Number of expired records removed across local data files.</returns>
    public int ApplyRetention(DateTimeOffset cutoffUtc) => _activity.ApplyRetention(cutoffUtc);

    /// <summary>
    /// Returns samples that overlap today's local interval from SQLite activity history.
    /// </summary>
    public IReadOnlyList<ActivitySample> GetTodaySamples()
    {
        var localStart = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
        var localEnd = localStart.AddDays(1);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local));
        var endUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, TimeZoneInfo.Local));
        var samples = new List<ActivitySample>();
        _activity.VisitOverlapping(startUtc, endUtc, CancellationToken.None, samples.Add);
        return samples;
    }

    /// <summary>
    /// Builds hourly active-time levels for the trailing 24-hour window when persisted samples cover that entire window.
    /// </summary>
    /// <param name="windowEndUtc">Optional inclusive-end instant for deterministic callers and tests; defaults to the current UTC time.</param>
    /// <returns>A 24-point trend whose levels are percentages of active seconds per hour.</returns>
    public ActivityTrendState Get24HourActivityTrend(DateTimeOffset? windowEndUtc = null)
    {
        const int hourCount = 24;
        var windowEnd = (windowEndUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var windowStart = windowEnd.AddHours(-hourCount);
        var samples = new List<ActivitySample>();
        _activity.VisitOverlapping(windowStart, windowEnd, CancellationToken.None, samples.Add);

        var activeSecondsByHour = new double[hourCount];
        var coveredSeconds = 0d;
        var coveredUntil = windowStart;
        foreach (var sample in samples.OrderBy(sample => sample.Timestamp))
        {
            var sampleEnd = sample.Timestamp.ToUniversalTime();
            var sampleStart = sampleEnd.AddSeconds(-sample.DurationSeconds);
            var overlapStart = sampleStart < windowStart ? windowStart : sampleStart;
            var overlapEnd = sampleEnd > windowEnd ? windowEnd : sampleEnd;
            if (overlapEnd <= overlapStart)
            {
                continue;
            }

            if (overlapEnd > coveredUntil)
            {
                var uncoveredStart = overlapStart > coveredUntil ? overlapStart : coveredUntil;
                if (overlapEnd > uncoveredStart)
                {
                    coveredSeconds += (overlapEnd - uncoveredStart).TotalSeconds;
                    coveredUntil = overlapEnd;
                }
            }

            if (!string.Equals(sample.State, "active", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var activeCursor = overlapStart;
            while (activeCursor < overlapEnd)
            {
                var hourIndex = Math.Clamp((int)(activeCursor - windowStart).TotalHours, 0, hourCount - 1);
                var bucketEnd = windowStart.AddHours(hourIndex + 1);
                var activeSegmentEnd = overlapEnd < bucketEnd ? overlapEnd : bucketEnd;
                activeSecondsByHour[hourIndex] += (activeSegmentEnd - activeCursor).TotalSeconds;
                activeCursor = activeSegmentEnd;
            }
        }

        var hasCompleteCoverage = coveredSeconds >= TimeSpan.FromHours(hourCount).TotalSeconds;
        var hourlyLevels = activeSecondsByHour
            .Select(seconds => Math.Min(100d, seconds / TimeSpan.FromHours(1).TotalSeconds * 100d))
            .ToArray();
        return new ActivityTrendState(windowStart, windowEnd, hasCompleteCoverage, hourlyLevels);
    }

    /// <summary>
    /// Returns latest sample, if any.
    /// </summary>
    public ActivitySample? LoadLatestSample() => _activity.LoadLatest();


    /// <summary>
    /// Calculates summary counters for today.
    /// </summary>
    public DailySummary GetTodaySummary()
    {
        return GetSummary(DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>
    /// Calculates summary counters for one local calendar date.
    /// </summary>
    /// <param name="date">The local date represented by the summary.</param>
    /// <returns>Aggregated activity values for the requested date.</returns>
    public DailySummary GetSummary(DateOnly date)
    {
        var report = new ReportAggregationService(this).Build(
            new ReportQuery(date, date, string.Empty),
            CancellationToken.None,
            applicationLimit: int.MaxValue);
        if (!report.Succeeded || report.Value is null)
        {
            throw new InvalidOperationException("The local daily activity query was rejected.");
        }

        return new DailySummary(
            report.Value.Totals.ActiveSeconds,
            report.Value.Totals.IdleSeconds,
            report.Value.Totals.KeyPresses,
            report.Value.Totals.MouseClicks,
            report.Value.Applications
                .Select(application => new ApplicationSummary(application.Application, application.ActiveSeconds))
                .ToArray());
    }

    /// <summary>Streams privacy-minimized activity and AI usage from one consistent SQLite snapshot.</summary>
    internal void VisitReportData(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken,
        Action<ReportSourceSample> activityVisitor,
        Action<AiRequestUsageRecord> aiUsageVisitor) =>
        _activity.VisitReportData(fromUtc, toUtc, cancellationToken, activityVisitor, aiUsageVisitor);

    private AppSettings EnsureInstallationId(AppSettings settings)
    {
        if (IsValidInstallationId(settings.InstallationId))
        {
            return settings;
        }

        using var bootstrapMutex = new Mutex(false, _settingsBootstrapMutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = bootstrapMutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                throw new TimeoutException("Timed out while initializing the TrackMeUp installation identity.");
            }

            var persistedInstallationId = TryReadPersistedInstallationId();
            if (persistedInstallationId is not null)
            {
                return settings with { InstallationId = persistedInstallationId };
            }

            var initialized = settings with { InstallationId = _utilities.GenerateInstallationId() };
            // The fixed per-settings-file mutex makes first-launch identity initialization process-safe.
            WriteSettingsFile(initialized);
            return initialized;
        }
        finally
        {
            if (acquired)
            {
                bootstrapMutex.ReleaseMutex();
            }
        }
    }

    private T WithSettingsMutex<T>(Func<T> operation)
    {
        using var settingsMutex = new Mutex(false, _settingsBootstrapMutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = settingsMutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }

            if (!acquired)
            {
                throw new TimeoutException("Timed out while accessing TrackMeUp settings.");
            }

            // Read/replace operations share one process-safe mutex so readers never observe an in-progress settings write.
            return operation();
        }
        finally
        {
            if (acquired)
            {
                settingsMutex.ReleaseMutex();
            }
        }
    }

    private string? TryReadPersistedInstallationId()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _json)
            ?? throw new InvalidOperationException("The TrackMeUp settings file must contain a JSON object.");
        if (!IsValidInstallationId(settings.InstallationId))
        {
            throw new InvalidOperationException("The TrackMeUp settings file has an invalid installation identity.");
        }

        return settings.InstallationId;
    }

    private static bool IsValidInstallationId(string? value) =>
        value is { Length: >= 16 and <= 160 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');

}

/// <summary>Describes record-level local-data retention impact without exposing record contents.</summary>
public sealed record DataRetentionPreview(int RecordCount, long TotalBytes, IReadOnlyList<string> Paths);
