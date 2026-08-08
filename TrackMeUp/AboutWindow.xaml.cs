using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using Windows.Graphics;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Displays product information and delegates diagnostics actions to the application facade.</summary>
public sealed partial class AboutWindow : Window
{
    private const int LogicalWindowWidth = 430;
    private const int LogicalWindowHeight = 520;
    private const int LogicalScreenMargin = 22;
    private readonly AppWindow _appWindow;
    private readonly ITrackMeUpApplication _application;
    private readonly LocalizationService _strings;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
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
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        ConfigureWindowBehavior();
        ResizeForLogicalContent();
        Closed += AboutWindow_Closed;
    }

    /// <summary>Forwards the close interaction to the window framework.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void AboutWindow_Closed(object sender, WindowEventArgs args)
    {
        _lifetimeCancellation.Cancel();
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
        UpdateTitleBarInsets();

        try
        {
            var result = await _application.GetProductInformationAsync(_lifetimeCancellation.Token);
            if (!result.Succeeded || result.Value is null)
            {
                throw new InvalidOperationException($"Build information is unavailable ({result.Code}).");
            }

            VersionText.Text = result.Value.Build.SemVer;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The window is closing; no presentation update is required.
        }
    }

    private async void ShowLogButton_Click(object sender, RoutedEventArgs e)
    {
        await RunDiagnosticsActionAsync(
            cancellationToken => _application.OpenApplicationLogAsync(cancellationToken),
            "About.LogOpened");
    }

    private async void ShareLogButton_Click(object sender, RoutedEventArgs e)
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64();
        await RunDiagnosticsActionAsync(
            cancellationToken => _application.ShareApplicationLogAsync(windowHandle, cancellationToken),
            "About.LogShared");
    }

    private async void ContactButton_Click(object sender, RoutedEventArgs e) =>
        await RunProductLinkActionAsync("author");

    private async void RepositoryButton_Click(object sender, RoutedEventArgs e) =>
        await RunProductLinkActionAsync("repository");

    private async Task RunProductLinkActionAsync(string linkKey)
    {
        ContactButton.IsEnabled = false;
        RepositoryButton.IsEnabled = false;
        CreatedByButton.IsEnabled = false;
        try
        {
            var result = await _application.OpenProductLinkAsync(linkKey, _lifetimeCancellation.Token);
            if (!result.Succeeded)
            {
                DiagnosticsInfoBar.Title = _strings.Translate("About.LinkFailed");
                DiagnosticsInfoBar.Message = _strings.Translate("About.LinkFailed.Description");
                DiagnosticsInfoBar.Severity = InfoBarSeverity.Error;
                DiagnosticsInfoBar.IsOpen = true;
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The window is closing; no presentation update is required.
        }
        catch (Exception)
        {
            DiagnosticsInfoBar.Title = _strings.Translate("About.LinkFailed");
            DiagnosticsInfoBar.Message = _strings.Translate("About.LinkFailed.Description");
            DiagnosticsInfoBar.Severity = InfoBarSeverity.Error;
            DiagnosticsInfoBar.IsOpen = true;
        }
        finally
        {
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                ContactButton.IsEnabled = true;
                RepositoryButton.IsEnabled = true;
                CreatedByButton.IsEnabled = true;
            }
        }
    }

    private async Task RunDiagnosticsActionAsync(
        Func<CancellationToken, Task<OperationResult<bool>>> action,
        string successKey)
    {
        ShowLogButton.IsEnabled = false;
        ShareLogButton.IsEnabled = false;
        try
        {
            var result = await action(_lifetimeCancellation.Token);
            DiagnosticsInfoBar.Title = _strings.Translate(result.Succeeded ? successKey : "About.LogFailed");
            DiagnosticsInfoBar.Message = result.Succeeded ? string.Empty : _strings.Translate("About.LogFailed.Description");
            DiagnosticsInfoBar.Severity = result.Succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            DiagnosticsInfoBar.IsOpen = true;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The window is closing; no presentation update is required.
        }
        catch (Exception)
        {
            DiagnosticsInfoBar.Title = _strings.Translate("About.LogFailed");
            DiagnosticsInfoBar.Message = _strings.Translate("About.LogFailed.Description");
            DiagnosticsInfoBar.Severity = InfoBarSeverity.Error;
            DiagnosticsInfoBar.IsOpen = true;
        }
        finally
        {
            if (!_lifetimeCancellation.IsCancellationRequested)
            {
                ShowLogButton.IsEnabled = true;
                ShareLogButton.IsEnabled = true;
            }
        }
    }

    private void TitleBarDragRegion_Loaded(object sender, RoutedEventArgs e) => UpdateTitleBarInsets();

    private void TitleBarDragRegion_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarInsets();

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

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _appWindow.TitleBar.BackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void UpdateTitleBarInsets()
    {
        if (!ExtendsContentIntoTitleBar || TitleBarDragRegion.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        var scale = Math.Max(0.1d, xamlRoot.RasterizationScale);
        TitleBarLeftInsetColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scale);
        TitleBarRightInsetColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scale);
    }

    private void CenterWindowInWorkArea(RectInt32 workArea)
    {
        var x = workArea.X + Math.Max(0, (workArea.Width - _appWindow.Size.Width) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - _appWindow.Size.Height) / 2);
        _appWindow.Move(new PointInt32(x, y));
    }
}
