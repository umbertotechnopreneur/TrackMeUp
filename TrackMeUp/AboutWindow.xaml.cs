using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Displays immutable about information and uses declarative links for external navigation.</summary>
public sealed partial class AboutWindow : Window
{
    private const int LogicalWindowWidth = 440;
    private const int LogicalWindowHeight = 360;
    private const int LogicalScreenMargin = 22;
    private readonly AppWindow _appWindow;
    private double _rasterizationScale = 1d;
    private XamlRoot? _xamlRoot;

    /// <summary>Creates and sizes the compact about window.</summary>
    public AboutWindow(string theme)
    {
        InitializeComponent();
        RootGrid.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        ResizeForLogicalContent();
        Closed += (_, _) =>
        {
            if (_xamlRoot is not null)
            {
                _xamlRoot.Changed -= XamlRoot_Changed;
            }
        };
    }

    /// <summary>Forwards the close interaction to the window framework.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        ResizeForLogicalContent();
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _rasterizationScale) >= 0.001d)
        {
            ResizeForLogicalContent();
        }
    }

    private void ResizeForLogicalContent()
    {
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? _rasterizationScale);
        _rasterizationScale = scale;

        var workArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var physicalMargin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var availableWidth = Math.Max(1, workArea.Width - (physicalMargin * 2));
        var availableHeight = Math.Max(1, workArea.Height - (physicalMargin * 2));
        var physicalWidth = Math.Min(availableWidth, (int)Math.Ceiling(LogicalWindowWidth * scale));
        var physicalHeight = Math.Min(availableHeight, (int)Math.Ceiling(LogicalWindowHeight * scale));
        _appWindow.Resize(new SizeInt32(physicalWidth, physicalHeight));
    }
}
