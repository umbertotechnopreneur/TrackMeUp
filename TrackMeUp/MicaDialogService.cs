// SPDX-License-Identifier: MIT

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using TrackMeUp.Application;
using TrackMeUp.Controls;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Serializes native system messages and dedicated rich windows while keeping views passive.</summary>
internal sealed class MicaDialogService
{
    private static readonly TimeSpan BannerProgressInterval = TimeSpan.FromMilliseconds(50);
    private readonly SemaphoreSlim _queue = new(1, 1);
    private readonly Dictionary<TimedInfoBar, BannerCountdown> _bannerCountdowns = [];
    private Window? _activeWindow;
    private bool _isShuttingDown;
    private TimeSpan _defaultBannerTimeout = TimeSpan.FromSeconds(10);
    private long _nextBannerGeneration;

    /// <summary>Gets or sets the timeout used when a banner call does not provide an override.</summary>
    internal TimeSpan DefaultBannerTimeout
    {
        get => _defaultBannerTimeout;
        set => _defaultBannerTimeout = ValidateBannerTimeout(value, nameof(value));
    }

    /// <summary>Displays an informational banner in an existing passive host.</summary>
    internal void ShowInfoBanner(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        ShowBanner(host, title, message, InfoBarSeverity.Informational, timeout);

    /// <summary>Displays a success banner in an existing passive host.</summary>
    internal void ShowSuccessBanner(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        ShowBanner(host, title, message, InfoBarSeverity.Success, timeout);

    /// <summary>Displays a warning banner in an existing passive host.</summary>
    internal void ShowWarningBanner(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        ShowBanner(host, title, message, InfoBarSeverity.Warning, timeout);

    /// <summary>Displays an error banner in an existing passive host.</summary>
    internal void ShowErrorBanner(TimedInfoBar host, string title, string message, TimeSpan? timeout = null) =>
        ShowBanner(host, title, message, InfoBarSeverity.Error, timeout);

    private void ShowBanner(TimedInfoBar host, string title, string message, InfoBarSeverity severity, TimeSpan? timeout)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!host.DispatcherQueue.HasThreadAccess)
        {
            // Banner state is UI-thread owned; cross-thread calls fail fast because there is no safe visual fallback.
            throw new InvalidOperationException("Banners must be shown from their host UI thread.");
        }

        var duration = ValidateBannerTimeout(timeout ?? DefaultBannerTimeout, nameof(timeout));
        StopBannerCountdown(host);
        host.Dismissed -= BannerHost_Dismissed;
        host.Dismissed += BannerHost_Dismissed;
        host.Present(title, message, severity);

        var timer = host.DispatcherQueue.CreateTimer();
        timer.Interval = BannerProgressInterval;
        timer.IsRepeating = true;
        var generation = ++_nextBannerGeneration;
        _bannerCountdowns[host] = new BannerCountdown(timer, Stopwatch.GetTimestamp(), duration, generation);
        timer.Tick += (_, _) => UpdateBannerCountdown(host, generation);
        timer.Start();
    }

    private void UpdateBannerCountdown(TimedInfoBar host, long generation)
    {
        if (!_bannerCountdowns.TryGetValue(host, out var countdown) || countdown.Generation != generation)
        {
            return;
        }

        // Monotonic elapsed time ignores wall-clock corrections; replaced generations make queued ticks no-ops.
        var elapsed = Stopwatch.GetElapsedTime(countdown.StartedTimestamp);
        var remainingRatio = Math.Clamp(1d - (elapsed.TotalMilliseconds / countdown.Duration.TotalMilliseconds), 0d, 1d);
        host.CountdownIndicator.Value = host.CountdownIndicator.Maximum * remainingRatio;
        if (remainingRatio > 0d)
        {
            return;
        }

        StopBannerCountdown(host);
        host.Dismiss();
    }

    private void BannerHost_Dismissed(object? sender, EventArgs e)
    {
        if (sender is TimedInfoBar host)
        {
            StopBannerCountdown(host);
        }
    }

    private void StopBannerCountdown(TimedInfoBar host)
    {
        if (_bannerCountdowns.Remove(host, out var countdown))
        {
            countdown.Timer.Stop();
        }
    }

    private static TimeSpan ValidateBannerTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "Banner timeout must be greater than zero.");
        }

        return timeout;
    }

    private sealed record BannerCountdown(
        DispatcherQueueTimer Timer,
        long StartedTimestamp,
        TimeSpan Duration,
        long Generation);

    /// <summary>Shows one queued owner-modal Windows message with the native localized OK action.</summary>
    internal async Task ShowInformativeAsync(Window owner, SystemMessageBoxRequest request)
    {
        ValidateSystemMessageRequest(request);
        _ = await RunSystemMessageSessionAsync(owner, false, ownerHandle =>
        {
            WindowInteropService.ShowInformativeMessage(ownerHandle, request);
            return true;
        });
    }

    /// <summary>Shows one queued owner-modal Windows confirmation with native localized actions.</summary>
    /// <returns><see langword="true"/> only when the user explicitly chooses OK; dismissal safely cancels.</returns>
    internal async Task<bool> ConfirmAsync(Window owner, SystemMessageBoxRequest request)
    {
        ValidateSystemMessageRequest(request);
        return await RunSystemMessageSessionAsync(
            owner,
            false,
            ownerHandle => WindowInteropService.ShowConfirmationMessage(ownerHandle, request));
    }

    /// <summary>Shows provider-specific pricing and locally estimated costs in the shared acrylic dialog queue.</summary>
    internal async Task ShowPricingAsync(
        ITrackMeUpApplication application,
        Window owner,
        AiPricingOverview overview,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(overview);
        ArgumentNullException.ThrowIfNull(strings);
        await RunModalSessionAsync(owner, async (ownerAppWindow, ownerHandle) =>
        {
            var dialog = new AiPricingDialogWindow(application, overview, theme, strings, ownerAppWindow, ownerHandle);
            await ShowDialogWindowAsync(dialog, dialog.WindowHandle, dialog.ShowAsync, dialog.DisposePlacement);
        });
    }

    /// <summary>Shows the native rolling activity calendar and returns a day requested for screenshot exploration.</summary>
    internal async Task<DateOnly?> ShowActivityCalendarAsync(
        ITrackMeUpApplication application,
        Window owner,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(strings);
        return await RunModalSessionAsync<DateOnly?>(owner, null, async (ownerAppWindow, ownerHandle) =>
        {
            var dialog = new ActivityCalendarDialogWindow(application, theme, strings, ownerAppWindow, ownerHandle);
            var result = await ShowDialogWindowAsync(dialog, dialog.WindowHandle, dialog.ShowAsync, dialog.DisposePlacement);
            if (result is null || _isShuttingDown)
            {
                return null;
            }

            if (result.Action == ActivityCalendarAction.OpenScreenshots)
            {
                return result.Date;
            }

            if (result.Action != ActivityCalendarAction.ReprocessDescriptions)
            {
                throw new InvalidOperationException($"Unsupported activity-calendar action: {result.Action}.");
            }

            // Both surfaces retain the same queue lease, so the chained modal flow cannot interleave with another dialog.
            var reprocessingDialog = new AiScreenshotReprocessingDialogWindow(
                application,
                result.Date,
                theme,
                strings,
                ownerAppWindow,
                ownerHandle);
            await ShowDialogWindowAsync(
                reprocessingDialog,
                reprocessingDialog.WindowHandle,
                reprocessingDialog.ShowAsync,
                reprocessingDialog.DisposePlacement);
            return null;
        });
    }

    /// <summary>Shows the searchable world-clock catalog and returns one selected city identifier.</summary>
    internal async Task<string?> ShowWorldClockCityPickerAsync(
        ITrackMeUpApplication application,
        Window owner,
        IReadOnlyList<WorldClockCitySummary> cities,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(strings);
        return await RunModalSessionAsync<string?>(owner, null, async (ownerAppWindow, ownerHandle) =>
        {
            var dialog = new WorldClockCityPickerDialogWindow(
                application,
                cities,
                theme,
                strings,
                ownerAppWindow,
                ownerHandle);
            return await ShowDialogWindowAsync(
                dialog,
                dialog.WindowHandle,
                dialog.ShowAsync,
                dialog.DisposePlacement);
        });
    }

    /// <summary>Shows the non-dismissible screenshot-storage migration progress surface.</summary>
    internal async Task<OperationResult<ScreenshotStorageMigrationResult>> ShowScreenshotStorageMigrationAsync(
        ITrackMeUpApplication application,
        Window owner,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(strings);
        return await RunModalSessionAsync(
            owner,
            OperationResult<ScreenshotStorageMigrationResult>.Failure(
                "operation.cancelled",
                "ScreenshotStorageMigrationFailed"),
            async (ownerAppWindow, ownerHandle) =>
            {
                var dialog = new ScreenshotStorageMigrationDialogWindow(
                    application,
                    theme,
                    strings,
                    ownerAppWindow,
                    ownerHandle);
                return await ShowDialogWindowAsync(
                    dialog,
                    dialog.WindowHandle,
                    dialog.ShowAsync,
                    dialog.DisposePlacement);
            });
    }

    /// <summary>Shows screenshot-storage migration when a launch mode has no owner window yet.</summary>
    internal async Task<OperationResult<ScreenshotStorageMigrationResult>> ShowStandaloneScreenshotStorageMigrationAsync(
        ITrackMeUpApplication application,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(strings);
        await _queue.WaitAsync();
        try
        {
            if (_isShuttingDown)
            {
                return OperationResult<ScreenshotStorageMigrationResult>.Failure(
                    "operation.cancelled",
                    "ScreenshotStorageMigrationFailed");
            }

            var dialog = new ScreenshotStorageMigrationDialogWindow(
                application,
                theme,
                strings,
                ownerAppWindow: null,
                ownerHandle: IntPtr.Zero);
            return await ShowDialogWindowAsync(
                dialog,
                dialog.WindowHandle,
                dialog.ShowAsync,
                dialog.DisposePlacement);
        }
        finally
        {
            _queue.Release();
        }
    }

    /// <summary>Shows the dedicated topmost acrylic surface for a bounded AI provider connection check.</summary>
    internal async Task ShowAiConnectionTestAsync(ITrackMeUpApplication application, Window owner, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(owner);
        var settings = await application.GetSettingsAsync(CancellationToken.None);
        // If settings cannot be read, system language is the safe presentation-only fallback.
        var strings = new LocalizationService(settings is { Succeeded: true, Value: { } value } ? value.UiLanguage : "system");
        await RunModalSessionAsync(owner, async (ownerAppWindow, ownerHandle) =>
        {
            var dialog = new AiConnectionTestDialogWindow(application, theme, ownerAppWindow, ownerHandle, strings);
            await ShowDialogWindowAsync(dialog, dialog.WindowHandle, dialog.ShowAsync, dialog.DisposePlacement);
        });
    }

    /// <summary>Stops new modal work and closes the current dedicated rich window during shutdown.</summary>
    internal void CloseActive()
    {
        _isShuttingDown = true;
        if (_activeWindow is ScreenshotStorageMigrationDialogWindow migrationWindow)
        {
            migrationWindow.CloseForShutdown();
            return;
        }

        _activeWindow?.Close();
    }

    private async Task<TResult> RunSystemMessageSessionAsync<TResult>(
        Window owner,
        TResult shutdownResult,
        Func<IntPtr, TResult> show)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(show);
        if (!owner.DispatcherQueue.HasThreadAccess)
        {
            throw new InvalidOperationException("Windows system messages must be shown from their owner UI thread.");
        }

        await _queue.WaitAsync();
        IReadOnlyList<IntPtr>? disabledPeerWindows = null;
        try
        {
            if (_isShuttingDown)
            {
                return shutdownResult;
            }

            if (!owner.DispatcherQueue.HasThreadAccess)
            {
                throw new InvalidOperationException("Windows system messages must be shown from their owner UI thread.");
            }

            var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
            disabledPeerWindows = WindowInteropService.DisableCurrentThreadPeerWindows(ownerHandle);
            return show(ownerHandle);
        }
        finally
        {
            if (disabledPeerWindows is not null)
            {
                WindowInteropService.RestoreWindows(disabledPeerWindows);
            }

            _queue.Release();
        }
    }

    private async Task RunModalSessionAsync(
        Window owner,
        Func<AppWindow, IntPtr, Task> showAsync)
    {
        ArgumentNullException.ThrowIfNull(showAsync);
        _ = await RunModalSessionAsync(owner, false, async (ownerAppWindow, ownerHandle) =>
        {
            await showAsync(ownerAppWindow, ownerHandle);
            return true;
        });
    }

    private async Task<TResult> RunModalSessionAsync<TResult>(
        Window owner,
        TResult shutdownResult,
        Func<AppWindow, IntPtr, Task<TResult>> showAsync)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(showAsync);
        await _queue.WaitAsync();
        var ownerContent = owner.Content as UIElement;
        var ownerWasInteractive = ownerContent?.IsHitTestVisible ?? false;
        try
        {
            if (_isShuttingDown)
            {
                return shutdownResult;
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = false;
            }

            var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
            var ownerWindowId = Win32Interop.GetWindowIdFromWindow(ownerHandle);
            var ownerAppWindow = AppWindow.GetFromWindowId(ownerWindowId);
            return await showAsync(ownerAppWindow, ownerHandle);
        }
        finally
        {
            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = ownerWasInteractive;
            }

            _queue.Release();
        }
    }

    private async Task ShowDialogWindowAsync(
        Window dialog,
        IntPtr dialogHandle,
        Func<Task> showAsync,
        Action disposePlacement)
    {
        _ = await ShowDialogWindowAsync(dialog, dialogHandle, async () =>
        {
            await showAsync();
            return true;
        }, disposePlacement);
    }

    private async Task<TResult> ShowDialogWindowAsync<TResult>(
        Window dialog,
        IntPtr dialogHandle,
        Func<Task<TResult>> showAsync,
        Action disposePlacement)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(showAsync);
        ArgumentNullException.ThrowIfNull(disposePlacement);
        IReadOnlyList<IntPtr>? disabledPeerWindows = null;
        try
        {
            _activeWindow = dialog;
            disabledPeerWindows = WindowInteropService.DisableCurrentThreadPeerWindows(dialogHandle);
            return await showAsync();
        }
        finally
        {
            _activeWindow = null;
            try
            {
                disposePlacement();
            }
            finally
            {
                if (disabledPeerWindows is not null)
                {
                    WindowInteropService.RestoreWindows(disabledPeerWindows);
                }
            }
        }
    }

    private static void ValidateSystemMessageRequest(SystemMessageBoxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("System message title and message are required.", nameof(request));
        }
    }

}
