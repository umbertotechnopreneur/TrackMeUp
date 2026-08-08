using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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
    private MicaDialogWindow? _activeWindow;
    private bool _isShuttingDown;

    /// <summary>Shows a one-button informative dialog and waits for acknowledgement or dismissal.</summary>
    internal async Task ShowInformativeAsync(Window owner, MicaDialogRequest request, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CancelButtonText is not null)
        {
            throw new ArgumentException("An informative dialog cannot define a cancel button.", nameof(request));
        }

        _ = await ShowAsync(owner, request, theme);
    }

    /// <summary>Shows a confirmation whose close path and secondary action both return <see langword="false"/>.</summary>
    internal async Task<bool> ConfirmAsync(Window owner, MicaDialogRequest request, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.CancelButtonText))
        {
            throw new ArgumentException("A confirmation dialog requires cancel text.", nameof(request));
        }

        return await ShowAsync(owner, request, theme) == MicaDialogResult.Primary;
    }

    /// <summary>Closes the current dialog during application shutdown.</summary>
    internal void CloseActive()
    {
        _isShuttingDown = true;
        _activeWindow?.Close();
    }

    private async Task<MicaDialogResult> ShowAsync(Window owner, MicaDialogRequest request, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Validate(request);
        await _queue.WaitAsync();
        var ownerContent = owner.Content as UIElement;
        var ownerWasInteractive = ownerContent?.IsHitTestVisible ?? false;
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

            var ownerWindowId = Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(owner));
            var ownerAppWindow = AppWindow.GetFromWindowId(ownerWindowId);
            var dialog = new MicaDialogWindow(request, theme, ownerAppWindow);
            _activeWindow = dialog;
            return await dialog.ShowAsync();
        }
        finally
        {
            _activeWindow = null;
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
}

internal enum MicaDialogResult
{
    Cancel,
    Primary
}
