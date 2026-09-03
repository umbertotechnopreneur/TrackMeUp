// SPDX-License-Identifier: MIT

using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Controls;
using Windows.Foundation;

namespace TrackMeUp;

/// <summary>Owns the lifecycle of timed toast components without coupling notifications to dialog presentation.</summary>
internal sealed class ToastNotificationService
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(50);
    private readonly Dictionary<TimedInfoBar, ToastCountdown> _countdowns = [];
    private TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private long _nextGeneration;

    /// <summary>Gets or sets the timeout used when a toast does not provide an override.</summary>
    internal TimeSpan DefaultTimeout
    {
        get => _defaultTimeout;
        set => _defaultTimeout = ValidateTimeout(value, nameof(value));
    }

    /// <summary>Shows an informational toast in its existing UI component.</summary>
    internal void ShowInfo(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        Show(host, title, message, InfoBarSeverity.Informational, timeout);

    /// <summary>Shows a successful-operation toast in its existing UI component.</summary>
    internal void ShowSuccess(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        Show(host, title, message, InfoBarSeverity.Success, timeout);

    /// <summary>Shows a warning toast in its existing UI component.</summary>
    internal void ShowWarning(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        Show(host, title, message, InfoBarSeverity.Warning, timeout);

    /// <summary>Shows an error toast in its existing UI component.</summary>
    internal void ShowError(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        Show(host, title, message, InfoBarSeverity.Error, timeout);

    /// <summary>Hides the toast component and stops its timeout lifecycle.</summary>
    internal void Hide(TimedInfoBar host)
    {
        ValidateHostThread(host);
        StopCountdown(host);
        host.Dismiss();
    }

    /// <summary>Stops all active timers and dismisses their hosts before application shutdown.</summary>
    internal void HideAll()
    {
        var hosts = _countdowns.Keys.ToArray();
        foreach (var host in hosts)
        {
            ValidateHostThread(host);
        }

        foreach (var host in hosts)
        {
            Hide(host);
        }
    }

    private void Show(TimedInfoBar host, string title, string message, InfoBarSeverity severity, TimeSpan? timeout)
    {
        ValidateHostThread(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(message);
        var duration = ValidateTimeout(timeout ?? DefaultTimeout, nameof(timeout));
        StopCountdown(host);
        host.Dismissed += ToastHost_Dismissed;
        host.Present(title, message, severity);

        var timer = host.DispatcherQueue.CreateTimer();
        timer.Interval = ProgressInterval;
        timer.IsRepeating = true;
        var generation = ++_nextGeneration;
        TypedEventHandler<DispatcherQueueTimer, object> tick = (_, _) => UpdateCountdown(host, generation);
        _countdowns[host] = new ToastCountdown(timer, tick, Stopwatch.GetTimestamp(), duration, generation);
        timer.Tick += tick;
        timer.Start();
    }

    private void UpdateCountdown(TimedInfoBar host, long generation)
    {
        if (!_countdowns.TryGetValue(host, out var countdown) || countdown.Generation != generation)
        {
            return;
        }

        // Monotonic time keeps the timeout stable even when Windows clock time changes.
        var elapsed = Stopwatch.GetElapsedTime(countdown.StartedTimestamp);
        var remainingRatio = Math.Clamp(1d - (elapsed.TotalMilliseconds / countdown.Duration.TotalMilliseconds), 0d, 1d);
        host.CountdownIndicator.Value = host.CountdownIndicator.Maximum * remainingRatio;
        if (remainingRatio > 0d)
        {
            return;
        }

        Hide(host);
    }

    private void ToastHost_Dismissed(object? sender, EventArgs e)
    {
        if (sender is TimedInfoBar host)
        {
            StopCountdown(host);
        }
    }

    private void StopCountdown(TimedInfoBar host)
    {
        host.Dismissed -= ToastHost_Dismissed;
        if (_countdowns.Remove(host, out var countdown))
        {
            countdown.Timer.Stop();
            countdown.Timer.Tick -= countdown.Tick;
        }
    }

    private static void ValidateHostThread(TimedInfoBar host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!host.DispatcherQueue.HasThreadAccess)
        {
            // Presentation has no cross-dispatcher fallback: show and hide must use the host UI thread.
            throw new InvalidOperationException("Toasts must be controlled from their host UI thread.");
        }
    }

    private static TimeSpan ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "Toast timeout must be greater than zero.");
        }

        return timeout;
    }

    private sealed record ToastCountdown(
        DispatcherQueueTimer Timer,
        TypedEventHandler<DispatcherQueueTimer, object> Tick,
        long StartedTimestamp,
        TimeSpan Duration,
        long Generation);
}
