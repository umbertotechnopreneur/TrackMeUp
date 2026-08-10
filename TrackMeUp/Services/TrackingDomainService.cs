using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>
/// Coordinates monitoring sessions and translates sample events into UI/dashboard state.
/// </summary>
public sealed class TrackingDomainService : IDisposable
{
    private readonly LocalStore _store;
    private readonly InputHookService _inputHooks = new();
    private readonly ActivityMonitorService _monitor;
    private readonly ActivityScoreService _activityScore = new();
    private ActivitySample? _latestSample;
    private DateTimeOffset? _trackingStartedAt;

    /// <summary>
    /// Initializes a new tracking domain service.
    /// </summary>
    /// <param name="store">Persistent store used for summaries and settings.</param>
    /// <param name="utilities">Shared utility service (required for compatibility with future persistence strategies).</param>
    public TrackingDomainService(LocalStore store, UtilityService utilities)
    {
        _store = store;
        _monitor = new ActivityMonitorService(_store, _inputHooks);
        _monitor.SampleRecorded += HandleSampleRecorded;
    }

    public event Action<DashboardState>? DashboardStateChanged;

    /// <summary>
    /// Raised after tracking starts or stops. The value is true when tracking is active.
    /// </summary>
    public event Action<bool>? TrackingStateChanged;

    public bool IsTracking => _trackingStartedAt is not null;
    public DateTimeOffset? TrackingStartedAt => _trackingStartedAt;
    public AnalysisContextSnapshot? LatestAnalysisContext => _latestSample is null
        ? null
        : ToAnalysisContext(_latestSample);

    /// <summary>
    /// Starts the input hooks and periodic sample collection when not already running.
    /// </summary>
    public void Start()
    {
        if (IsTracking)
        {
            // Keep start idempotent: callers may trigger Start multiple times without side effects.
            return;
        }

        _trackingStartedAt = DateTimeOffset.Now;
        _inputHooks.Start();
        _monitor.Start();
        TrackingStateChanged?.Invoke(true);
        DashboardStateChanged?.Invoke(LoadCurrentDashboardState());
    }

    /// <summary>
    /// Stops input hooks and sample collection safely.
    /// </summary>
    public void Stop()
    {
        if (!IsTracking)
        {
            return;
        }

        // Stop all collectors before clearing the session marker, so stale UI updates are avoided.
        _monitor.Stop();
        _inputHooks.Stop();
        _trackingStartedAt = null;
        TrackingStateChanged?.Invoke(false);
        DashboardStateChanged?.Invoke(LoadCurrentDashboardState());
    }

    /// <summary>
    /// Reads current values used by the main UI panel.
    /// </summary>
    public DashboardState LoadCurrentDashboardState()
    {
        var summary = _store.GetTodaySummary();
        var settings = _store.LoadSettings();
        var sample = _latestSample;
        var status = IsTracking && sample?.State == "active" ? "RUNNING" : "PAUSED";
        var context = sample is null ? "STATE_READY" : sample.State == "idle" ? "STATE_IDLE" : $"{sample.Application} · {sample.Context}";
        var intensity = sample?.State == "active"
            ? Math.Min(100, 30 + sample.KeyPresses + sample.MouseClicks * 2)
            : IsTracking ? 5 : 5;
        var utcNow = DateTimeOffset.UtcNow;

        return new DashboardState(
            status,
            context,
            summary.KeyPresses,
            summary.MouseClicks,
            summary.ActiveSeconds,
            intensity,
            IsTracking,
            sample?.Timestamp,
            utcNow.ToLocalTime(),
            utcNow,
            _store.Get24HourActivityTrend(utcNow),
            ActivityScore: _activityScore.GetState(settings.ScreenshotIntervalMinutes, utcNow),
            SpanLabel: settings.SpanLabel);
    }

    /// <summary>
    /// Loads the latest completed sample and its latest screenshot path from local history.
    /// </summary>
    public LastSessionState? LoadLastSessionState()
    {
        var sample = _store.LoadLatestSample();
        if (sample is null)
        {
            return null;
        }

        var screenshotPath = _store.LoadLatestPrimaryScreenshot();
        DateTimeOffset? screenshotCapturedAt = screenshotPath is not null && File.Exists(screenshotPath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(screenshotPath), TimeSpan.Zero)
            : null;
        if (screenshotCapturedAt is null)
        {
            screenshotPath = null;
        }

        return new LastSessionState(sample.Timestamp, sample.Application, sample.Context, sample.InstallationId, sample.Attributes, screenshotPath, screenshotCapturedAt);
    }

    /// <summary>
    /// Returns a compact elapsed timer label for the running tracking session.
    /// </summary>
    public string GetElapsedLabel()
    {
        if (_trackingStartedAt is null)
        {
            return "00:00";
        }

        var elapsed = DateTimeOffset.Now - _trackingStartedAt.Value;
        return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    /// <summary>
    /// Loads application settings from local storage.
    /// </summary>
    public AppSettings LoadSettings() => _store.LoadSettings();

    /// <summary>
    /// Persists provided application settings to local storage.
    /// </summary>
    /// <param name="settings">Settings instance to persist.</param>
    public void SaveSettings(AppSettings settings) => _store.SaveSettings(settings);

    /// <summary>Records application-owned telemetry for the live score without exposing OS access to the UI.</summary>
    public void RecordSystemSnapshot(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _activityScore.RecordSystemSnapshot(snapshot);
        DashboardStateChanged?.Invoke(LoadCurrentDashboardState());
    }

    /// <summary>Calculates the telemetry averages that must be persisted with one screenshot.</summary>
    public ScreenshotIntervalTelemetry BuildScreenshotIntervalTelemetry(
        DateTimeOffset intervalStartedAt,
        DateTimeOffset capturedAt) =>
        _activityScore.BuildScreenshotIntervalTelemetry(intervalStartedAt, capturedAt);

    /// <summary>
    /// Handles each new sample and publishes updated UI state.
    /// </summary>
    /// <param name="sample">New sample to publish.</param>
    private void HandleSampleRecorded(ActivitySample sample)
    {
        _latestSample = sample;
        _activityScore.RecordSample(sample);
        DashboardStateChanged?.Invoke(BuildDashboardState(sample));
    }

    /// <summary>
    /// Builds dashboard state from the latest sample and today's counters.
    /// </summary>
    /// <param name="sample">Sample currently in focus.</param>
    /// <returns>Computed dashboard representation.</returns>
    private DashboardState BuildDashboardState(ActivitySample sample)
    {
        var summary = _store.GetTodaySummary();
        var settings = _store.LoadSettings();
        var status = sample.State == "active" ? "RUNNING" : "PAUSED";
        var context = sample.State == "idle" ? "STATE_IDLE" : $"{sample.Application} · {sample.Context}";
        var intensity = sample.State == "active" ? Math.Min(100, 30 + sample.KeyPresses + sample.MouseClicks * 2) : 5;
        var utcNow = DateTimeOffset.UtcNow;

        return new DashboardState(
            status,
            context,
            summary.KeyPresses,
            summary.MouseClicks,
            summary.ActiveSeconds,
            intensity,
            true,
            sample.Timestamp,
            utcNow.ToLocalTime(),
            utcNow,
            _store.Get24HourActivityTrend(utcNow),
            ActivityScore: _activityScore.GetState(settings.ScreenshotIntervalMinutes, utcNow),
            SpanLabel: settings.SpanLabel);
    }

    /// <summary>
    /// Converts the activity sample into a context object used by AI analysis.
    /// </summary>
    /// <param name="sample">Source sample.</param>
    /// <returns>Normalized AI input context.</returns>
    private static AnalysisContextSnapshot ToAnalysisContext(ActivitySample sample) => new(
        sample.Application,
        sample.Context,
        sample.WindowTitle,
        sample.State,
        FilterAnalysisAttributes(sample.Attributes));

    private static IReadOnlyDictionary<string, string>? FilterAnalysisAttributes(IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is null)
        {
            return null;
        }

        var safeAttributes = attributes
            .Where(attribute => !string.Equals(attribute.Key, ActivityAttributeKeys.SpanLabel, StringComparison.Ordinal))
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
        return safeAttributes.Count == 0 ? null : safeAttributes;
    }

    /// <summary>
    /// Disposes active hooks and unsubscribes events.
    /// </summary>
    public void Dispose()
    {
        _monitor.Stop();
        _inputHooks.Stop();
        _monitor.SampleRecorded -= HandleSampleRecorded;
    }
}
