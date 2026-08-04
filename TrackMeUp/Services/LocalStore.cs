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
/// Handles local JSONL persistence for samples, analyses and settings.
/// </summary>
public sealed class LocalStore
{
    private readonly UtilityService _utilities = new();
    private readonly string _samplesPath;
    private readonly string _analysesPath;
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
        _samplesPath = Path.Combine(resolvedDataDirectory, "activity.jsonl");
        _analysesPath = Path.Combine(resolvedDataDirectory, "analyses.jsonl");
        _settingsPath = Path.Combine(resolvedDataDirectory, "appsettings.json");
        var settingsFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_settingsPath.ToUpperInvariant())))[..32];
        _settingsBootstrapMutexName = $"Local\\TrackMeUp.Settings.{settingsFingerprint}";
    }

    /// <summary>
    /// Appends one activity sample to the rolling JSONL store.
    /// </summary>
    public void AppendSample(ActivitySample sample) => AppendLine(_samplesPath, sample);

    /// <summary>
    /// Appends one AI analysis entry to the rolling JSONL store.
    /// </summary>
    public void AppendAnalysis(AiAnalysis analysis) => AppendLine(_analysesPath, analysis);

    /// <summary>
    /// Loads application settings, normalizing screenshot path and falling back to defaults on corruption.
    /// </summary>
    public AppSettings LoadSettings()
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(_settingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _json) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            PreserveCorruptSettings();
            settings = new AppSettings(ScreenshotDirectory: _utilities.GetDefaultScreenshotDirectory());
        }

        var normalized = SettingsCatalog.NormalizePersisted(settings, _utilities.GetDefaultScreenshotDirectory());
        return EnsureInstallationId(normalized);
    }

    /// <summary>
    /// Reads the persisted installation identifier without creating or rewriting settings.
    /// </summary>
    /// <returns>The existing installation identifier, or null when settings have not been initialized by the runtime.</returns>
    public string? TryLoadInstallationId()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            var installationId = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _json)?.InstallationId;
            return IsValidInstallationId(installationId) ? installationId : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Persists application settings to JSON file.
    /// </summary>
    /// <param name="settings">Settings payload.</param>
    public void SaveSettings(AppSettings settings)
    {
        var normalized = SettingsCatalog.NormalizePersisted(settings, _utilities.GetDefaultScreenshotDirectory());
        var payload = EnsureInstallationId(normalized);
        WriteSettingsFile(payload);
    }

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

    /// <summary>
    /// Loads the latest AI analysis record, if available.
    /// </summary>
    public AiAnalysis? LoadLatestAnalysis() => ReadLines<AiAnalysis>(_analysesPath).LastOrDefault();

    /// <summary>
    /// Returns today's AI analysis records for policy and cost calculations.
    /// </summary>
    public IReadOnlyList<AiAnalysis> GetTodayAnalyses() => ReadLines<AiAnalysis>(_analysesPath).Where(x => x.Timestamp.LocalDateTime.Date == DateTime.Today).ToList();

    /// <summary>
    /// Lists data files that contain at least one expired, parseable record without deleting them.
    /// </summary>
    /// <param name="cutoffUtc">Oldest allowed last-write timestamp.</param>
    /// <returns>Files that would be compacted by the requested cutoff.</returns>
    public IReadOnlyList<string> GetRetentionCandidates(DateTimeOffset cutoffUtc)
    {
        return GetRetentionPreview(cutoffUtc).Paths;
    }

    /// <summary>Counts expired JSONL records and their encoded size without changing the data files.</summary>
    /// <param name="cutoffUtc">Records older than this instant are counted.</param>
    /// <returns>Record-level retention preview with affected file paths.</returns>
    public DataRetentionPreview GetRetentionPreview(DateTimeOffset cutoffUtc)
    {
        var samples = InspectExpiredLines<ActivitySample>(_samplesPath, cutoffUtc, sample => sample.Timestamp);
        var analyses = InspectExpiredLines<AiAnalysis>(_analysesPath, cutoffUtc, analysis => analysis.Timestamp);
        var paths = new[] { samples.Path, analyses.Path }.Where(path => path is not null).Cast<string>().ToArray();
        return new DataRetentionPreview(samples.Count + analyses.Count, samples.Bytes + analyses.Bytes, paths);
    }

    /// <summary>Removes only expired JSONL records and preserves newer or malformed lines.</summary>
    /// <param name="cutoffUtc">Records older than this instant are removed.</param>
    /// <returns>Number of expired records removed across local data files.</returns>
    public int ApplyRetention(DateTimeOffset cutoffUtc) =>
        CompactLines<ActivitySample>(_samplesPath, cutoffUtc, sample => sample.Timestamp)
        + CompactLines<AiAnalysis>(_analysesPath, cutoffUtc, analysis => analysis.Timestamp);

    /// <summary>
    /// Returns today samples from local telemetry log.
    /// </summary>
    public IReadOnlyList<ActivitySample> GetTodaySamples() => ReadLines<ActivitySample>(_samplesPath).Where(x => x.Timestamp.LocalDateTime.Date == DateTime.Today).ToList();

    /// <summary>
    /// Returns latest sample, if any.
    /// </summary>
    public ActivitySample? LoadLatestSample() => ReadLines<ActivitySample>(_samplesPath).LastOrDefault();

    /// <summary>
    /// Returns the newest screenshot path referenced by the latest AI analysis.
    /// </summary>
    public string? LoadLatestPrimaryScreenshot()
    {
        var latest = LoadLatestAnalysis()?.ScreenshotPaths;
        return string.IsNullOrWhiteSpace(latest) ? null : latest.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

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
        var samples = ReadLines<ActivitySample>(_samplesPath)
            .Where(x => DateOnly.FromDateTime(x.Timestamp.LocalDateTime.Date) == date)
            .ToList();
        var applications = samples.Where(x => x.State == "active").GroupBy(x => x.Application)
            .Select(x => new ApplicationSummary(x.Key, x.Sum(y => (long)y.DurationSeconds))).OrderByDescending(x => x.ActiveSeconds).ToList();
        return new DailySummary(samples.Where(x => x.State == "active").Sum(x => (long)x.DurationSeconds), samples.Where(x => x.State == "idle").Sum(x => (long)x.DurationSeconds), samples.Sum(x => x.KeyPresses), samples.Sum(x => x.MouseClicks), applications);
    }

    /// <summary>
    /// Serializes and appends one JSONL line using a lock to avoid concurrent file corruption.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    /// <param name="path">Target JSONL file path.</param>
    /// <param name="value">Value to append.</param>
    private void AppendLine<T>(string path, T value)
    {
        lock (_fileLock)
        {
            File.AppendAllText(path, JsonSerializer.Serialize(value, _json) + Environment.NewLine);
        }
    }

    /// <summary>
    /// Reads non-empty JSONL lines and ignores malformed lines.
    /// </summary>
    /// <typeparam name="T">Target record type.</typeparam>
    /// <param name="path">Source path.</param>
    /// <returns>Enumerable of successfully deserialized records.</returns>
    private IEnumerable<T> ReadLines<T>(string path)
    {
        if (!File.Exists(path)) yield break;
        string[] lines;
        // Lock around the full file read to avoid torn lines while another writer is active.
        lock (_fileLock) lines = File.ReadAllLines(path);
        foreach (var line in lines)
        {
            T? value;
            try { value = JsonSerializer.Deserialize<T>(line, _json); } catch { continue; }
            if (value is not null) yield return value;
        }
    }

    private int CompactLines<T>(string path, DateTimeOffset cutoffUtc, Func<T, DateTimeOffset> timestamp)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        lock (_fileLock)
        {
            var retained = new List<string>();
            var removed = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    retained.Add(line);
                    continue;
                }

                try
                {
                    var value = JsonSerializer.Deserialize<T>(line, _json);
                    if (value is not null && timestamp(value) < cutoffUtc)
                    {
                        removed++;
                        continue;
                    }
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException)
                {
                    // Unknown lines are retained fail-closed instead of being classified as expired data.
                }

                retained.Add(line);
            }

            if (removed == 0)
            {
                return 0;
            }

            if (retained.Count == 0)
            {
                File.Delete(path);
                return removed;
            }

            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    foreach (var line in retained)
                    {
                        writer.WriteLine(line);
                    }

                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                File.Replace(temporaryPath, path, null, ignoreMetadataErrors: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            return removed;
        }
    }

    private (string? Path, int Count, long Bytes) InspectExpiredLines<T>(string path, DateTimeOffset cutoffUtc, Func<T, DateTimeOffset> timestamp)
    {
        if (!File.Exists(path))
        {
            return (null, 0, 0);
        }

        lock (_fileLock)
        {
            var count = 0;
            long bytes = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                try
                {
                    var value = JsonSerializer.Deserialize<T>(line, _json);
                    if (value is null || timestamp(value) >= cutoffUtc)
                    {
                        continue;
                    }

                    count++;
                    bytes += Encoding.UTF8.GetByteCount(line + Environment.NewLine);
                }
                catch (Exception exception) when (exception is JsonException or NotSupportedException)
                {
                    // Malformed records are excluded from deletion estimates and retained during execution.
                }
            }

            return count == 0 ? (null, 0, 0) : (path, count, bytes);
        }
    }

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

    private string? TryReadPersistedInstallationId()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            var installationId = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _json)?.InstallationId;
            return IsValidInstallationId(installationId) ? installationId : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsValidInstallationId(string? value) =>
        value is { Length: >= 16 and <= 160 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');

    /// <summary>
    /// Preserves malformed settings for manual recovery before the runtime recreates defaults.
    /// </summary>
    private void PreserveCorruptSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        var corruptPath = _settingsPath + "." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + ".corrupt";
        try
        {
            // A move keeps the original invalid data intact and prevents the recovery save from overwriting it.
            File.Move(_settingsPath, corruptPath);
        }
        catch
        {
            // If another process already recovered the file, default loading remains the safe fallback.
        }
    }
}

/// <summary>Describes record-level JSONL retention impact without exposing record contents.</summary>
public sealed record DataRetentionPreview(int RecordCount, long TotalBytes, IReadOnlyList<string> Paths);
