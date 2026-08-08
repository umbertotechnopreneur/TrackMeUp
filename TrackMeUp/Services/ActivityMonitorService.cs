using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using TrackMeUp.Providers;

namespace TrackMeUp.Services;

/// <summary>
/// Samples foreground activity, keypresses and mouse clicks at a fixed interval.
/// </summary>
public sealed class ActivityMonitorService : IDisposable
{
    private const int SampleSeconds = 5;
    private const int IdleThresholdSeconds = 60;
    private readonly LocalStore _store;
    private readonly InputHookService _inputHooks;
    private readonly ActivityContextProviderRegistry _providers = new();
    private string _installationId = string.Empty;
    private Timer? _timer;

    /// <summary>
    /// Creates a monitor service bound to a store and input counter source.
    /// </summary>
    public ActivityMonitorService(LocalStore store, InputHookService inputHooks)
    {
        _store = store;
        _inputHooks = inputHooks;
        _installationId = store.LoadSettings().InstallationId;
    }

    public event Action<ActivitySample>? SampleRecorded;
    public ActivitySample? CurrentSample { get; private set; }

    /// <summary>
    /// Starts periodic sampling. If already running, it keeps existing cadence.
    /// </summary>
    public void Start() => _timer ??= new Timer(_ => Sample(), null, TimeSpan.Zero, TimeSpan.FromSeconds(SampleSeconds));

    /// <summary>
    /// Stops periodic sampling and releases the timer.
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Captures one activity point and stores it. Failures are isolated to one sample.
    /// </summary>
    private void Sample()
    {
        ActivitySample sample;
        try
        {
            // Capture failures from protected/transient OS state skip only this observation.
            var window = ReadForegroundWindow();
            var context = _providers.Resolve(window);
            var counts = _inputHooks.TakeCounts();
            var state = GetIdleSeconds() >= IdleThresholdSeconds ? "idle" : "active";
            var attributes = context.Attributes is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(context.Attributes, StringComparer.Ordinal);
            var spanLabel = _store.LoadSettings().SpanLabel;
            if (!string.IsNullOrWhiteSpace(spanLabel))
            {
                attributes[ActivityAttributeKeys.SpanLabel] = spanLabel;
            }

            sample = new ActivitySample(DateTimeOffset.Now, SampleSeconds, state, window.ProcessName,
                context.Application, context.Context, window.WindowTitle, _installationId, counts.Keys, counts.Clicks,
                attributes.Count == 0 ? null : attributes);
        }
        catch
        {
            return;
        }

        PersistSample(sample);
    }

    /// <summary>Persists a captured sample; storage failures propagate because tracking has no secondary store.</summary>
    internal void PersistSample(ActivitySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        _store.AppendSample(sample);
        CurrentSample = sample;
        try
        {
            SampleRecorded?.Invoke(sample);
        }
        catch
        {
            // A presentation subscriber cannot invalidate an already durable activity sample.
        }
    }

    /// <summary>
    /// Reads foreground window title and process from OS APIs.
    /// </summary>
    /// <returns>Window metadata for enrichment of activity context.</returns>
    private static ForegroundWindowInfo ReadForegroundWindow()
    {
        var handle = NativeMethods.GetForegroundWindow();
        var title = new StringBuilder(1024);
        NativeMethods.GetWindowText(handle, title, title.Capacity);
        NativeMethods.GetWindowThreadProcessId(handle, out var processId);

        try
        {
            return new ForegroundWindowInfo(Process.GetProcessById((int)processId).ProcessName, title.ToString());
        }
        catch
        {
            return new ForegroundWindowInfo("System", title.ToString());
        }
    }

    /// <summary>
    /// Returns the time elapsed since last user input in seconds.
    /// </summary>
    private static long GetIdleSeconds()
    {
        var info = new NativeMethods.LastInputInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LastInputInfo>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return 0;
        }

        return unchecked((uint)Environment.TickCount - info.Time) / 1000;
    }

    public void Dispose() => Stop();
}
