// SPDX-License-Identifier: MIT

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Serializes standard WinUI dialogs and dedicated rich windows while keeping views passive.</summary>
internal sealed class MicaDialogService
{
    private readonly DialogSessionQueue _queue = new();
    private Window? _activeWindow;
    private ContentDialog? _activeContentDialog;

    /// <summary>Gets the service that shows and hides reusable toast UI components.</summary>
    internal ToastNotificationService Notifications { get; } = new();

    /// <summary>Shows one queued standard WinUI acknowledgement dialog.</summary>
    internal async Task ShowInformativeAsync(Window owner, DialogRequest request)
    {
        ValidateDialogRequest(request, requiresCloseButton: false);
        _ = await RunContentDialogSessionAsync(owner, request, ContentDialogButton.Primary);
    }

    /// <summary>Shows one queued standard WinUI OK/Cancel confirmation.</summary>
    /// <returns><see langword="true"/> only when the user explicitly chooses OK; dismissal safely cancels.</returns>
    internal async Task<bool> ConfirmAsync(Window owner, DialogRequest request)
    {
        ValidateDialogRequest(request, requiresCloseButton: true);
        return await RunContentDialogSessionAsync(owner, request, ContentDialogButton.Close) == ContentDialogResult.Primary;
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
            if (result is null || _queue.IsShuttingDown)
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

    /// <summary>Shows the searchable world-clock catalog and reports whether it added at least one city.</summary>
    internal async Task<bool> ShowWorldClockCityPickerAsync(
        ITrackMeUpApplication application,
        Window owner,
        IReadOnlyList<WorldClockCitySummary> cities,
        ElementTheme theme,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(strings);
        return await RunModalSessionAsync(owner, false, async (ownerAppWindow, ownerHandle) =>
        {
            var dialog = new WorldClockCityPickerDialogWindow(
                application,
                cities,
                theme,
                strings,
                ownerAppWindow,
                ownerHandle,
                Notifications);
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
        using var session = await _queue.EnterAsync();
        if (session is null || _queue.IsShuttingDown)
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

    /// <summary>Cancels queued requests and closes active WinUI dialogs, rich windows, and toasts during shutdown.</summary>
    internal void CloseActive()
    {
        _queue.Shutdown();
        _activeContentDialog?.Hide();
        CloseActiveWindow();
        Notifications.HideAll();
    }

    private void CloseActiveWindow()
    {
        switch (_activeWindow)
        {
            case ScreenshotStorageMigrationDialogWindow migrationWindow:
                migrationWindow.CloseForShutdown();
                break;
            case WorldClockCityPickerDialogWindow pickerWindow:
                pickerWindow.CloseForShutdown();
                break;
            default:
                _activeWindow?.Close();
                break;
        }
    }

    private async Task<ContentDialogResult> RunContentDialogSessionAsync(
        Window owner,
        DialogRequest request,
        ContentDialogButton defaultButton)
    {
        ValidateOwnerThread(owner);
        using var ownerLifetime = new CancellationTokenSource();
        ContentDialog? dialog = null;
        void OwnerClosed(object sender, WindowEventArgs args)
        {
            ownerLifetime.Cancel();
            dialog?.Hide();
        }

        owner.Closed += OwnerClosed;
        try
        {
            using var session = await _queue.EnterAsync(ownerLifetime.Token);
            if (session is null || _queue.IsShuttingDown || ownerLifetime.IsCancellationRequested)
            {
                return ContentDialogResult.None;
            }

            ValidateOwnerThread(owner);
            if (owner.Content is not FrameworkElement { XamlRoot: { } xamlRoot } ownerContent)
            {
                // A standard ContentDialog has no valid visual root before its owner content is loaded.
                throw new InvalidOperationException("Content dialogs require a loaded owner XamlRoot.");
            }

            dialog = CreateContentDialog(xamlRoot, ownerContent, request, defaultButton);
            _activeContentDialog = dialog;
            try
            {
                // ContentDialog is an overlay: restore a hidden tray owner so the request is reachable.
                owner.Activate();
                var result = await dialog.ShowAsync();
                return _queue.IsShuttingDown || ownerLifetime.IsCancellationRequested
                    ? ContentDialogResult.None
                    : result;
            }
            finally
            {
                _activeContentDialog = null;
                dialog = null;
            }
        }
        finally
        {
            owner.Closed -= OwnerClosed;
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
        ValidateOwnerThread(owner);
        ArgumentNullException.ThrowIfNull(showAsync);
        using var ownerLifetime = new CancellationTokenSource();
        var presenting = false;
        void OwnerClosed(object sender, WindowEventArgs args)
        {
            ownerLifetime.Cancel();
            if (presenting)
            {
                CloseActiveWindow();
            }
        }

        owner.Closed += OwnerClosed;
        try
        {
            using var session = await _queue.EnterAsync(ownerLifetime.Token);
            if (session is null || _queue.IsShuttingDown || ownerLifetime.IsCancellationRequested)
            {
                return shutdownResult;
            }

            ValidateOwnerThread(owner);
            var ownerContent = owner.Content as UIElement;
            var ownerWasInteractive = ownerContent?.IsHitTestVisible ?? false;
            presenting = true;
            try
            {
                if (ownerContent is not null)
                {
                    ownerContent.IsHitTestVisible = false;
                }

                var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(owner);
                var ownerWindowId = Win32Interop.GetWindowIdFromWindow(ownerHandle);
                var ownerAppWindow = AppWindow.GetFromWindowId(ownerWindowId);
                var result = await showAsync(ownerAppWindow, ownerHandle);
                return _queue.IsShuttingDown || ownerLifetime.IsCancellationRequested ? shutdownResult : result;
            }
            finally
            {
                presenting = false;
                if (!ownerLifetime.IsCancellationRequested && ownerContent is not null)
                {
                    ownerContent.IsHitTestVisible = ownerWasInteractive;
                }
            }
        }
        finally
        {
            owner.Closed -= OwnerClosed;
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

    private static ContentDialog CreateContentDialog(
        XamlRoot xamlRoot,
        FrameworkElement ownerContent,
        DialogRequest request,
        ContentDialogButton defaultButton) =>
        new()
        {
            XamlRoot = xamlRoot,
            RequestedTheme = ownerContent.ActualTheme,
            Language = ownerContent.Language,
            Title = request.Title,
            Content = request.Message,
            PrimaryButtonText = request.PrimaryButtonText,
            CloseButtonText = request.CloseButtonText,
            DefaultButton = defaultButton
        };

    private static void ValidateOwnerThread(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.DispatcherQueue.HasThreadAccess)
        {
            // Dialog windows share their owner's dispatcher; no background-thread UI fallback is supported.
            throw new InvalidOperationException("Dialogs must be controlled from their owner UI thread.");
        }
    }

    private static void ValidateDialogRequest(DialogRequest request, bool requiresCloseButton)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Message) ||
            string.IsNullOrWhiteSpace(request.PrimaryButtonText) ||
            (requiresCloseButton && string.IsNullOrWhiteSpace(request.CloseButtonText)))
        {
            throw new ArgumentException("Dialog title, message, and required button labels must be supplied.", nameof(request));
        }
    }

}
