using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using TrackMeUp.Application;
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
    private readonly SemaphoreSlim _queue = new(1, 1);
    private Window? _activeWindow;
    private bool _isShuttingDown;

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

    /// <summary>Shows simplified OpenAI pricing and locally estimated costs in the shared Mica dialog queue.</summary>
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
