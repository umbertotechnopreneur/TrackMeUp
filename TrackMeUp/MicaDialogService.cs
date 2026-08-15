using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TrackMeUp.Application;
using TrackMeUp.Controls;
using TrackMeUp.Services;
using Windows.UI;

namespace TrackMeUp;

/// <summary>Defines the visual meaning of a reusable TrackMeUp dialog.</summary>
internal enum MicaDialogSeverity
{
    Information,
    Warning,
    Error
}

/// <summary>Describes localized text, buttons and optional accent for one reusable dialog.</summary>
internal sealed record MicaDialogRequest(
    string Title,
    string Message,
    MicaDialogSeverity Severity,
    string PrimaryButtonText,
    string? CancelButtonText = null,
    Color? AccentColor = null)
{
    /// <summary>Creates an informative dialog with one acknowledgement button.</summary>
    internal static MicaDialogRequest Informative(
        string title,
        string message,
        MicaDialogSeverity severity,
        string okText,
        Color? accentColor = null) =>
        new(title, message, severity, okText, AccentColor: accentColor);

    /// <summary>Creates a safe-default confirmation with explicit primary and cancel actions.</summary>
    internal static MicaDialogRequest Confirmation(
        string title,
        string message,
        string primaryText,
        string cancelText,
        Color? accentColor = null) =>
        new(title, message, MicaDialogSeverity.Warning, primaryText, cancelText, accentColor);
}

/// <summary>Serializes custom Mica dialogs and keeps window views free of ad-hoc dialog construction.</summary>
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

    /// <summary>Shows a one-button informative dialog and waits for acknowledgement or dismissal.</summary>
    internal async Task ShowInformativeAsync(ITrackMeUpApplication application, Window owner, MicaDialogRequest request, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(request);
        if (request.CancelButtonText is not null)
        {
            throw new ArgumentException("An informative dialog cannot define a cancel button.", nameof(request));
        }

        _ = await ShowAsync(application, owner, request, theme);
    }

    /// <summary>Shows a confirmation whose close path and secondary action both return <see langword="false"/>.</summary>
    internal async Task<bool> ConfirmAsync(ITrackMeUpApplication application, Window owner, MicaDialogRequest request, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CancelButtonText))
        {
            throw new ArgumentException("A confirmation dialog requires cancel text.", nameof(request));
        }

        return await ShowAsync(application, owner, request, theme) == MicaDialogResult.Primary;
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
        await ShowPricingWindowAsync(application, owner, overview, theme, strings);
    }

    /// <summary>Shows the native rolling activity calendar in the shared acrylic dialog queue.</summary>
    internal async Task ShowActivityCalendarAsync(
        ITrackMeUpApplication application,
        Window owner,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(strings);
        await ShowActivityCalendarWindowAsync(application, owner, theme, strings);
    }

    /// <summary>Shows the dedicated topmost acrylic surface for a bounded AI provider connection check.</summary>
    internal async Task ShowAiConnectionTestAsync(ITrackMeUpApplication application, Window owner, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(owner);
        var settings = await application.GetSettingsAsync(CancellationToken.None);
        // If settings cannot be read, system language is the safe presentation-only fallback.
        var strings = new LocalizationService(settings is { Succeeded: true, Value: { } value } ? value.UiLanguage : "system");
        await _queue.WaitAsync();
        var ownerContent = owner.Content as UIElement;
        var ownerWasInteractive = ownerContent?.IsHitTestVisible ?? false;
        var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        AiConnectionTestDialogWindow? dialog = null;
        List<IntPtr>? disabledPeerWindows = null;
        try
        {
            if (_isShuttingDown)
            {
                return;
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = false;
            }

            var ownerAppWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(ownerHandle));
            dialog = new AiConnectionTestDialogWindow(application, theme, ownerAppWindow, ownerHandle, strings);
            _activeWindow = dialog;
            disabledPeerWindows = DisableDialogPeerWindows(dialog.WindowHandle);
            await dialog.ShowAsync();
        }
        finally
        {
            _activeWindow = null;
            dialog?.DisposePlacement();
            if (disabledPeerWindows is not null)
            {
                RestoreDialogPeerWindows(disabledPeerWindows);
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = ownerWasInteractive;
            }

            _queue.Release();
        }
    }

    /// <summary>Closes the current dialog during application shutdown.</summary>
    internal void CloseActive()
    {
        _isShuttingDown = true;
        _activeWindow?.Close();
    }

    private async Task<MicaDialogResult> ShowAsync(ITrackMeUpApplication application, Window owner, MicaDialogRequest request, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Validate(request);
        await _queue.WaitAsync();
        var ownerContent = owner.Content as UIElement;
        var ownerWasInteractive = ownerContent?.IsHitTestVisible ?? false;
        var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        MicaDialogWindow? dialog = null;
        List<IntPtr>? disabledPeerWindows = null;
        try
        {
            if (_isShuttingDown)
            {
                return MicaDialogResult.Cancel;
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = false;
            }

            var ownerWindowId = Win32Interop.GetWindowIdFromWindow(ownerHandle);
            var ownerAppWindow = AppWindow.GetFromWindowId(ownerWindowId);
            dialog = new MicaDialogWindow(application, request, theme, ownerAppWindow, ownerHandle);
            _activeWindow = dialog;
            disabledPeerWindows = DisableDialogPeerWindows(dialog.WindowHandle);

            var result = await dialog.ShowAsync();
            return result;
        }
        finally
        {
            _activeWindow = null;
            dialog?.DisposePlacement();
            if (disabledPeerWindows is not null)
            {
                RestoreDialogPeerWindows(disabledPeerWindows);
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = ownerWasInteractive;
            }

            _queue.Release();
        }
    }

    private async Task ShowPricingWindowAsync(
        ITrackMeUpApplication application,
        Window owner,
        AiPricingOverview overview,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(owner);
        await _queue.WaitAsync();
        var ownerContent = owner.Content as UIElement;
        var ownerWasInteractive = ownerContent?.IsHitTestVisible ?? false;
        var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        AiPricingDialogWindow? dialog = null;
        List<IntPtr>? disabledPeerWindows = null;
        try
        {
            if (_isShuttingDown)
            {
                return;
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = false;
            }

            var ownerWindowId = Win32Interop.GetWindowIdFromWindow(ownerHandle);
            var ownerAppWindow = AppWindow.GetFromWindowId(ownerWindowId);
            dialog = new AiPricingDialogWindow(application, overview, theme, strings, ownerAppWindow, ownerHandle);
            _activeWindow = dialog;
            disabledPeerWindows = DisableDialogPeerWindows(dialog.WindowHandle);
            await dialog.ShowAsync();
        }
        finally
        {
            _activeWindow = null;
            dialog?.DisposePlacement();
            if (disabledPeerWindows is not null)
            {
                RestoreDialogPeerWindows(disabledPeerWindows);
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = ownerWasInteractive;
            }

            _queue.Release();
        }
    }

    private async Task ShowActivityCalendarWindowAsync(
        ITrackMeUpApplication application,
        Window owner,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(owner);
        await _queue.WaitAsync();
        var ownerContent = owner.Content as UIElement;
        var ownerWasInteractive = ownerContent?.IsHitTestVisible ?? false;
        var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
        ActivityCalendarDialogWindow? dialog = null;
        AiScreenshotReprocessingDialogWindow? reprocessingDialog = null;
        List<IntPtr>? disabledPeerWindows = null;
        try
        {
            if (_isShuttingDown)
            {
                return;
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = false;
            }

            var ownerWindowId = Win32Interop.GetWindowIdFromWindow(ownerHandle);
            var ownerAppWindow = AppWindow.GetFromWindowId(ownerWindowId);
            dialog = new ActivityCalendarDialogWindow(application, theme, strings, ownerAppWindow, ownerHandle);
            _activeWindow = dialog;
            disabledPeerWindows = DisableDialogPeerWindows(dialog.WindowHandle);
            var result = await dialog.ShowAsync();
            if (result is not null && !_isShuttingDown)
            {
                // The calendar and reprocessing surfaces share this queue acquisition so no nested modal wait can deadlock.
                reprocessingDialog = new AiScreenshotReprocessingDialogWindow(
                    application,
                    result.Date,
                    theme,
                    strings,
                    ownerAppWindow,
                    ownerHandle);
                _activeWindow = reprocessingDialog;
                await reprocessingDialog.ShowAsync();
            }
        }
        finally
        {
            _activeWindow = null;
            dialog?.DisposePlacement();
            reprocessingDialog?.DisposePlacement();
            if (disabledPeerWindows is not null)
            {
                RestoreDialogPeerWindows(disabledPeerWindows);
            }

            if (ownerContent is not null)
            {
                ownerContent.IsHitTestVisible = ownerWasInteractive;
            }

            _queue.Release();
        }
    }

    private static void Validate(MicaDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Message) ||
            string.IsNullOrWhiteSpace(request.PrimaryButtonText))
        {
            throw new ArgumentException("Dialog title, message and primary button text are required.", nameof(request));
        }
    }

    private static List<IntPtr> DisableDialogPeerWindows(IntPtr dialogHandle)
    {
        var disabled = new List<IntPtr>();
        EnumThreadWindows(GetCurrentThreadId(), (windowHandle, _) =>
        {
            if (windowHandle == dialogHandle || !IsWindowEnabled(windowHandle))
            {
                return true;
            }

            EnableWindow(windowHandle, false);
            disabled.Add(windowHandle);
            return true;
        }, IntPtr.Zero);
        return disabled;
    }

    private static void RestoreDialogPeerWindows(IEnumerable<IntPtr> disabledPeerWindows)
    {
        foreach (var windowHandle in disabledPeerWindows)
        {
            if (IsWindow(windowHandle))
            {
                EnableWindow(windowHandle, true);
            }
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hWnd);

    private delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
}

internal enum MicaDialogResult
{
    Cancel,
    Primary
}
