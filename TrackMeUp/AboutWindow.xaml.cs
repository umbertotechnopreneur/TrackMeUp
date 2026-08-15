using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using Windows.Graphics;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Displays product information and delegates diagnostics actions to the application facade.</summary>
public sealed partial class AboutWindow : Window
{
    private const int LogicalWindowWidth = 940;
    private const int LogicalWindowHeight = 650;
    private const int LogicalScreenMargin = 22;
    private const string DarkHeroAsset = "ms-appx:///Assets/TrackMeUpAboutHero.theme-dark.png";
    private const string LightHeroAsset = "ms-appx:///Assets/TrackMeUpAboutHero.theme-light.png";
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly ITrackMeUpApplication _application;
    private readonly LocalizationService _strings;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private ThirdPartyLicensesWindow? _licensesWindow;
    private XamlRoot? _xamlRoot;
    private ElementTheme? _heroTheme;

    /// <summary>Creates and sizes the compact about window.</summary>
    public AboutWindow(ITrackMeUpApplication application, string theme, string language)
    {
        _application = application;
        _strings = new LocalizationService(language);
        InitializeComponent();
        RootGrid.RequestedTheme = theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
        UiLocalization.Apply(RootGrid, _strings);
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _placement = new WindowPlacementService(_application, this, _appWindow, WindowStateKeys.About, LogicalWindowWidth, LogicalWindowHeight, LogicalScreenMargin);
        ConfigureWindowBehavior();
        _placement.ApplyDefaultBounds(RootGrid);
        Closed += AboutWindow_Closed;
    }

    /// <summary>Forwards the close interaction to the window framework.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void AboutWindow_Closed(object sender, WindowEventArgs args)
    {
        await _placement.SaveAsync(CancellationToken.None);
        _placement.Dispose();
        _lifetimeCancellation.Cancel();
        _licensesWindow?.Close();
        RootGrid.ActualThemeChanged -= RootGrid_ActualThemeChanged;
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

        _placement.ApplyDefaultBounds(RootGrid);
        await _placement.RestoreAndCenterAsync(RootGrid, _lifetimeCancellation.Token);
        UpdateTitleBarInsets();
        UpdateThemeAssets();

        try
        {
            var result = await _application.GetProductInformationAsync(_lifetimeCancellation.Token);
            if (!result.Succeeded || result.Value is null)
            {
                throw new InvalidOperationException($"Build information is unavailable ({result.Code}).");
            }

            VersionText.Text = result.Value.Build.SemVer;
            BuiltAtText.Text = result.Value.Build.BuiltAtLocal.ToString("d", _strings.Culture);
            CommitText.Text = result.Value.Build.GitCommitShort;
            DirtyIndicator.Visibility = result.Value.Build.GitDirty ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The window is closing; no presentation update is required.
        }
    }

    private async void ShowLogButton_Click(object sender, RoutedEventArgs e)
    {
        await RunDiagnosticsActionAsync(
            cancellationToken => _application.OpenApplicationLogFolderAsync(cancellationToken),
            "About.LogFolderOpened");
    }

    private async void ShareLogButton_Click(object sender, RoutedEventArgs e)
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64();
        await RunDiagnosticsActionAsync(
            cancellationToken => _application.ShareApplicationLogAsync(windowHandle, cancellationToken),
            "About.LogShared");
    }

    private async void WebsiteButton_Click(object sender, RoutedEventArgs e) =>
        await RunProductLinkActionAsync("author");

    private async void IssuesButton_Click(object sender, RoutedEventArgs e) =>
        await RunProductLinkActionAsync("issues");

    private async void RepositoryButton_Click(object sender, RoutedEventArgs e) =>
        await RunProductLinkActionAsync("repository");

    private void LicensesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_licensesWindow is not null)
        {
            _licensesWindow.Activate();
            return;
        }

        _licensesWindow = new ThirdPartyLicensesWindow(_application, RootGrid.ActualTheme, _strings.RequestedLanguage, _appWindow);
        _licensesWindow.Closed += (_, _) => _licensesWindow = null;
        _licensesWindow.Activate();
    }

    private async Task RunProductLinkActionAsync(string linkKey)
    {
        IssuesButton.IsEnabled = false;
        RepositoryFooterButton.IsEnabled = false;
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
                IssuesButton.IsEnabled = true;
                RepositoryFooterButton.IsEnabled = true;
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
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        }
    }

    private void RootGrid_ActualThemeChanged(FrameworkElement sender, object args) => UpdateThemeAssets();

    private void UpdateThemeAssets()
    {
        var actualTheme = RootGrid.ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        if (_heroTheme == actualTheme)
        {
            return;
        }

        _heroTheme = actualTheme;
        HeroImage.Source = new BitmapImage(new Uri(actualTheme == ElementTheme.Dark ? DarkHeroAsset : LightHeroAsset));
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
}
