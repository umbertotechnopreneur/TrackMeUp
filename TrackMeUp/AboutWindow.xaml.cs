using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TrackMeUp.Application;
using Windows.Graphics;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Displays immutable about information and uses declarative links for external navigation.</summary>
public sealed partial class AboutWindow : Window
{
    private const int LogicalWindowWidth = 440;
    private const int LogicalWindowHeight = 590;
    private const int LogicalScreenMargin = 22;
    private readonly AppWindow _appWindow;
    private readonly ITrackMeUpApplication _application;
    private readonly LocalizationService _strings;
    private double _rasterizationScale = 1d;
    private XamlRoot? _xamlRoot;

    /// <summary>Creates and sizes the compact about window.</summary>
    public AboutWindow(ITrackMeUpApplication application, string theme, string language)
    {
        _application = application;
        _strings = new LocalizationService(language);
        InitializeComponent();
        RootGrid.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        UiLocalization.Apply(RootGrid, _strings);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        ConfigureWindowBehavior();
        ResizeForLogicalContent();
        Closed += AboutWindow_Closed;
    }

    /// <summary>Forwards the close interaction to the window framework.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void AboutWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        ResizeForLogicalContent();

        var result = await _application.GetProductInformationAsync(CancellationToken.None);
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException($"Build information is unavailable ({result.Code}).");
        }

        var product = result.Value;
        var build = product.Build;
        VersionText.Text = build.SemVer;
        BuiltText.Text = build.BuiltAtLocal.ToString("yyyy-MM-dd HH:mm:ss zzz");
        MachineText.Text = build.MachineName;
        CommitLink.Content = build.GitCommitShort;
        CommitLink.NavigateUri = new Uri($"{product.RepositoryUrl}/commit/{build.GitCommit}");
        BuildContextText.Text = $"{build.Configuration} · {build.Platform} · {build.RuntimeIdentifier}" +
            (build.GitDirty ? $" · {_strings.Translate("About.Dirty")}" : string.Empty);
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
        CenterWindowInWorkArea(workArea);
    }

    private void ConfigureWindowBehavior()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }
    }

    private void CenterWindowInWorkArea(RectInt32 workArea)
    {
        var x = workArea.X + Math.Max(0, (workArea.Width - _appWindow.Size.Width) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - _appWindow.Size.Height) / 2);
        _appWindow.Move(new PointInt32(x, y));
    }
}
