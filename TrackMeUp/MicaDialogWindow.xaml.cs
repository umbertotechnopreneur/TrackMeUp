using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using Windows.UI;

namespace TrackMeUp;

/// <summary>Renders one queued TrackMeUp Mica dialog without owning product behavior.</summary>
internal sealed partial class MicaDialogWindow : Window
{
    private const int LogicalWidth = 430;
    private const int LogicalMinimumHeight = 196;
    private const int LogicalMaximumHeight = 620;
    private const int LogicalTitleBarHeight = 44;
    private const int LogicalScreenMargin = 24;
    private const int LogicalWindowChromeAllowance = 8;
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
        Title = request.Title;
        RootGrid.RequestedTheme = theme;
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
        AccentVeil.Fill = CreateAccentVeil(accent, theme);
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
        AutomationProperties.SetName(RootGrid, $"{request.Title} dialog");
        AutomationProperties.SetName(DialogTitleText, request.Title);
        AutomationProperties.SetName(DialogMessageText, request.Message);
        PrimaryButton.Content = request.PrimaryButtonText;
        AutomationProperties.SetName(PrimaryButton, request.PrimaryButtonText);
        PrimaryButton.Background = accentBrush;
        PrimaryButton.BorderBrush = accentBrush;
        PrimaryButton.Foreground = new SolidColorBrush(GetContrastingForeground(accent));
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
        // Resolve against the owner's current monitor; Windows falls back to the primary work area if that owner is unavailable.
        var area = DisplayArea.GetFromWindowId(_ownerAppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var margin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var width = Math.Min(area.Width - (margin * 2), (int)Math.Ceiling(LogicalWidth * scale));
        width = Math.Max(1, width);
        var maximumPhysicalHeight = Math.Max(
            1,
            Math.Min(area.Height - (margin * 2), (int)Math.Ceiling(LogicalMaximumHeight * scale)));
        var logicalContentWidth = Math.Max(1d, (width / scale) - LogicalWindowChromeAllowance);
        var maximumLogicalHeight = Math.Max(1d, maximumPhysicalHeight / scale);
        var desiredLogicalHeight = MeasureDesiredWindowHeight(logicalContentWidth, maximumLogicalHeight);
        var height = Math.Clamp(
            (int)Math.Ceiling(desiredLogicalHeight * scale),
            1,
            maximumPhysicalHeight);
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

    private double MeasureDesiredWindowHeight(double logicalContentWidth, double maximumLogicalHeight)
    {
        MessageScrollViewer.MaxHeight = double.PositiveInfinity;
        MessageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        MessageScrollViewer.VerticalScrollMode = ScrollMode.Disabled;
        DialogBody.Measure(new Windows.Foundation.Size(logicalContentWidth, double.PositiveInfinity));

        var naturalBodyHeight = DialogBody.DesiredSize.Height;
        var naturalWindowHeight = LogicalTitleBarHeight + naturalBodyHeight + LogicalWindowChromeAllowance;
        if (naturalWindowHeight <= maximumLogicalHeight)
        {
            return Math.Max(LogicalMinimumHeight, naturalWindowHeight);
        }

        var naturalMessageHeight = Math.Max(1d, DialogMessageText.DesiredSize.Height);
        var nonMessageHeight = naturalWindowHeight - naturalMessageHeight;
        MessageScrollViewer.MaxHeight = Math.Max(1d, maximumLogicalHeight - nonMessageHeight);
        MessageScrollViewer.VerticalScrollMode = ScrollMode.Enabled;
        MessageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        DialogBody.Measure(new Windows.Foundation.Size(logicalContentWidth, double.PositiveInfinity));
        return Math.Min(
            maximumLogicalHeight,
            Math.Max(
                LogicalMinimumHeight,
                LogicalTitleBarHeight + DialogBody.DesiredSize.Height + LogicalWindowChromeAllowance));
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Complete(MicaDialogResult.Primary);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Complete(MicaDialogResult.Cancel);
    }

    private void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;
        Complete(MicaDialogResult.Cancel);
    }

    private void Complete(MicaDialogResult result)
    {
        _completion.TrySetResult(result);
        Close();
    }

    private static Color GetContrastingForeground(Color background)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045d
                ? value / 12.92d
                : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
        }

        var luminance = (0.2126d * Linearize(background.R))
            + (0.7152d * Linearize(background.G))
            + (0.0722d * Linearize(background.B));
        var whiteContrast = 1.05d / (luminance + 0.05d);
        var blackContrast = (luminance + 0.05d) / 0.05d;
        return blackContrast >= whiteContrast ? Colors.Black : Colors.White;
    }

    private static RadialGradientBrush CreateAccentVeil(Color accent, ElementTheme theme)
    {
        var centerAlpha = theme switch
        {
            ElementTheme.Dark => (byte)30,
            ElementTheme.Light => (byte)20,
            _ => (byte)24
        };
        var middleAlpha = (byte)(centerAlpha / 3);
        var brush = new RadialGradientBrush
        {
            Center = new Windows.Foundation.Point(0.12d, 0.18d),
            GradientOrigin = new Windows.Foundation.Point(0.08d, 0.12d),
            RadiusX = 0.92d,
            RadiusY = 1.18d
        };
        brush.GradientStops.Add(new GradientStop
        {
            Color = Color.FromArgb(centerAlpha, accent.R, accent.G, accent.B),
            Offset = 0d
        });
        brush.GradientStops.Add(new GradientStop
        {
            Color = Color.FromArgb(middleAlpha, accent.R, accent.G, accent.B),
            Offset = 0.56d
        });
        brush.GradientStops.Add(new GradientStop
        {
            Color = Color.FromArgb(0, accent.R, accent.G, accent.B),
            Offset = 1d
        });
        return brush;
    }

    private static Color DefaultAccent(MicaDialogSeverity severity) => severity switch
    {
        MicaDialogSeverity.Error => Color.FromArgb(255, 224, 76, 62),
        MicaDialogSeverity.Warning => Color.FromArgb(255, 217, 152, 18),
        _ => Color.FromArgb(255, 91, 111, 214)
    };
}
