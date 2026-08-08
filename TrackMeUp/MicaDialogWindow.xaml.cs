using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace TrackMeUp;

/// <summary>Renders one queued TrackMeUp Mica dialog without owning product behavior.</summary>
internal sealed partial class MicaDialogWindow : Window
{
    private const int LogicalWidth = 430;
    private const int LogicalInformationHeight = 238;
    private const int LogicalConfirmationHeight = 258;
    private readonly TaskCompletionSource<MicaDialogResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AppWindow _appWindow;
    private readonly AppWindow _ownerAppWindow;
    private readonly bool _isConfirmation;

    /// <summary>Creates a passive dialog surface from a validated request.</summary>
    internal MicaDialogWindow(MicaDialogRequest request, ElementTheme theme, AppWindow ownerAppWindow)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ownerAppWindow = ownerAppWindow ?? throw new ArgumentNullException(nameof(ownerAppWindow));
        InitializeComponent();
        RootGrid.RequestedTheme = theme;
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        var accent = request.AccentColor ?? DefaultAccent(request.Severity);
        var accentBrush = new SolidColorBrush(accent);
        AccentIconSurface.Background = new SolidColorBrush(Color.FromArgb(38, accent.R, accent.G, accent.B));
        SeverityIcon.Foreground = accentBrush;
        SeverityIcon.Glyph = request.Severity switch
        {
            MicaDialogSeverity.Error => "\uE783",
            MicaDialogSeverity.Warning => "\uE7BA",
            _ => "\uE946"
        };
        DialogTitleText.Text = request.Title;
        DialogMessageText.Text = request.Message;
        AutomationProperties.SetName(RootGrid, $"{request.Title}. {request.Message}");
        AutomationProperties.SetName(DialogTitleText, request.Title);
        AutomationProperties.SetName(DialogMessageText, request.Message);
        PrimaryButton.Content = request.PrimaryButtonText;
        AutomationProperties.SetName(PrimaryButton, request.PrimaryButtonText);
        PrimaryButton.Background = accentBrush;
        PrimaryButton.Foreground = new SolidColorBrush(Colors.White);
        _isConfirmation = request.CancelButtonText is not null;
        if (_isConfirmation)
        {
            CancelButton.Content = request.CancelButtonText;
            CancelButton.Visibility = Visibility.Visible;
            AutomationProperties.SetName(CancelButton, request.CancelButtonText);
        }

        Closed += (_, _) => _completion.TrySetResult(MicaDialogResult.Cancel);
    }

    /// <summary>Activates the detached Mica surface and completes after its explicit action or closure.</summary>
    internal Task<MicaDialogResult> ShowAsync()
    {
        Activate();
        return _completion.Task;
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? 1d);
        var area = DisplayArea.GetFromWindowId(_ownerAppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var margin = (int)Math.Ceiling(24 * scale);
        var width = Math.Min(area.Width - (margin * 2), (int)Math.Ceiling(LogicalWidth * scale));
        var height = Math.Min(area.Height - (margin * 2), (int)Math.Ceiling((_isConfirmation ? LogicalConfirmationHeight : LogicalInformationHeight) * scale));
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        _appWindow.Resize(new SizeInt32(width, height));

        var ownerBounds = new RectInt32(
            _ownerAppWindow.Position.X,
            _ownerAppWindow.Position.Y,
            _ownerAppWindow.Size.Width,
            _ownerAppWindow.Size.Height);
        var x = Math.Clamp(ownerBounds.X + ((ownerBounds.Width - width) / 2), area.X, Math.Max(area.X, area.X + area.Width - width));
        var y = Math.Clamp(ownerBounds.Y + ((ownerBounds.Height - height) / 2), area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        _appWindow.Move(new PointInt32(x, y));
        (_isConfirmation ? CancelButton : PrimaryButton).Focus(FocusState.Programmatic);
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(MicaDialogResult.Primary);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(MicaDialogResult.Cancel);
        Close();
    }

    private static Color DefaultAccent(MicaDialogSeverity severity) => severity switch
    {
        MicaDialogSeverity.Error => Color.FromArgb(255, 224, 76, 62),
        MicaDialogSeverity.Warning => Color.FromArgb(255, 217, 152, 18),
        _ => Color.FromArgb(255, 91, 111, 214)
    };
}
