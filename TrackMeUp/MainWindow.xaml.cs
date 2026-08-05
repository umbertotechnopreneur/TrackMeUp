using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Runtime;
using TrackMeUp.Services;
using Windows.Foundation;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Displays the compact player and forwards user intent to UI-neutral view models.</summary>
public sealed partial class MainWindow : Window
{
    private const int LogicalWindowWidth = 430;
    private const int PlayerHeight = 342;
    private const int ExpandedPlayerHeight = 492;
    private const int OptionsHeight = 650;
    private const int OperationsHeight = 768;
    private const int LogicalScreenMargin = 22;
    private readonly ITrackMeUpApplication _application;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly AppWindow _appWindow;
    private LocalizationService _strings = new("system");
    private bool _detailsExpanded;
    private bool _updatingMenuState;
    private int _logicalHeight = PlayerHeight;
    private double _rasterizationScale = 1d;
    private string _theme = "system";
    private string _position = FlyoutPositions.BottomCenter;
    private AppSettings? _menuSettings;
    private AboutWindow? _aboutWindow;
    private XamlRoot? _xamlRoot;
    private string? _latestScreenshotPath;
    private DateTimeOffset? _latestScreenshotCapturedAt;
    private bool _screenshotsEnabled;

    /// <summary>Occurs when a fully persisted settings snapshot has been applied to the player surface.</summary>
    public event Action<AppSettings>? SettingsApplied;

    /// <summary>Occurs when the user requests the dedicated reports surface.</summary>
    public event EventHandler? ReportsRequested;

    /// <summary>Occurs when the user requests the retained screenshot gallery.</summary>
    public event EventHandler<ScreenshotPreviewRequestedEventArgs>? ScreenshotsRequested;

    /// <summary>Creates the player view with the shared application facade supplied by the composition root.</summary>
    public MainWindow(ITrackMeUpApplication application, LaunchOptions options)
    {
        _application = application;
        _viewModel = new MainViewModel(application);
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
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
        ResizeForLogicalContent(PlayerHeight);

        OptionsControl.Initialize(application);
        OptionsControl.SettingsSaved += ApplySettings;
        OperationsControl.Initialize(application);
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(1);
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        _refreshTimer.Start();
        _ = InitializeAsync(options);
        Closed += MainWindow_Closed;
    }

    private async Task InitializeAsync(LaunchOptions options)
    {
        var settings = await _application.GetSettingsAsync(CancellationToken.None);
        if (settings.Succeeded && settings.Value is not null)
        {
            ApplySettings(settings.Value with
            {
                UiLanguage = options.Language ?? settings.Value.UiLanguage,
                Theme = options.Theme ?? settings.Value.Theme,
                FlyoutPosition = options.Position ?? settings.Value.FlyoutPosition
            });
        }

        if (options.StartTracking && !options.Paused)
        {
            await _viewModel.ToggleTrackingAsync(CancellationToken.None);
        }

        await RefreshDashboardAsync();
    }

    private async Task RefreshDashboardAsync()
    {
        var state = await _viewModel.RefreshAsync(CancellationToken.None);
        if (state.Succeeded && state.Value is not null)
        {
            UpdatePlayer(state.Value);
        }
    }

    /// <summary>Shows the shared overflow flyout from its title-bar command.</summary>
    private void TitleBarMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.Flyout is Flyout flyout)
        {
            flyout.ShowAt(TitleBarMoreButton);
        }
    }

    /// <summary>Delegates compact settings navigation to the active passive view.</summary>
    private void TitleBarBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (OptionsPanel.Visibility == Visibility.Visible)
        {
            OptionsControl.NavigateBack();
            return;
        }

        ShowPlayer();
    }

    /// <summary>Initializes caption insets and the title-bar button passthrough region after layout.</summary>
    private void DragRegion_Loaded(object sender, RoutedEventArgs e) => UpdateTitleBarLayout();

    /// <summary>Keeps caption insets and the title-bar button passthrough region aligned after resizing.</summary>
    private void DragRegion_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTitleBarLayout();

    /// <summary>Reserves the native caption area and makes only title-bar commands interactive.</summary>
    private void UpdateTitleBarLayout()
    {
        if (!ExtendsContentIntoTitleBar || DragRegion.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        var scale = xamlRoot.RasterizationScale;
        TitleBarLeftInsetColumn.Width = new GridLength(_appWindow.TitleBar.LeftInset / scale);
        TitleBarRightInsetColumn.Width = new GridLength(_appWindow.TitleBar.RightInset / scale);

        var passthroughRects = new List<RectInt32> { ElementRect(TitleBarMoreButton, scale) };
        if (TitleBarBackButton.Visibility == Visibility.Visible)
        {
            passthroughRects.Add(ElementRect(TitleBarBackButton, scale));
        }

        // The system keeps the rest of the title bar draggable and owns the caption buttons.
        InputNonClientPointerSource
            .GetForWindowId(_appWindow.Id)
            .SetRegionRects(NonClientRegionKind.Passthrough, passthroughRects.ToArray());
    }

    private static RectInt32 ElementRect(FrameworkElement element, double scale)
    {
        var transform = element.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new RectInt32(
            (int)Math.Round(bounds.X * scale),
            (int)Math.Round(bounds.Y * scale),
            (int)Math.Round(bounds.Width * scale),
            (int)Math.Round(bounds.Height * scale));
    }

    /// <summary>Forwards the play/pause action to the player view model.</summary>
    private async void TrackingButton_Click(object sender, RoutedEventArgs e)
    {
        var state = await _viewModel.ToggleTrackingAsync(CancellationToken.None);
        if (state.Succeeded && state.Value is not null)
        {
            UpdatePlayer(state.Value);
        }
    }

    /// <summary>Loads persisted settings into presentation-only flyout controls.</summary>
    private async void MoreMenu_Opened(object sender, object e)
    {
        if (MoreButton.Flyout is Flyout flyout && flyout.Content is DependencyObject content)
        {
            UiLocalization.Apply(content, _strings);
        }

        var result = await _application.GetSettingsAsync(CancellationToken.None);
        if (!result.Succeeded || result.Value is null)
        {
            return;
        }

        _menuSettings = result.Value;
        _updatingMenuState = true;
        OpenAiMenuToggle.IsOn = result.Value.OpenAiEnabled;
        ScreenshotsMenuToggle.IsOn = result.Value.ScreenshotsEnabled;
        _updatingMenuState = false;

    }

    /// <summary>Shows the options view.</summary>
    private void OptionsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        ShowPanel(OptionsPanel, OptionsHeight);
    }

    /// <summary>Forwards report-window activation to the application composition root.</summary>
    private void ReportsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        ReportsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the operational and diagnostic facade surface.</summary>
    private void OperationsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        ShowPanel(OperationsPanel, OperationsHeight);
    }

    /// <summary>Shows the passive About view.</summary>
    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        _aboutWindow ??= new AboutWindow(_application, _theme, _strings.RequestedLanguage);
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Activate();
    }

    /// <summary>Forwards the AI-toggle value to the application facade.</summary>
    private async void OpenAiMenuToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingMenuState)
        {
            return;
        }

        var result = await _application.SetAiEnabledAsync(OpenAiMenuToggle.IsOn, CancellationToken.None);
        if (!result.Succeeded)
        {
            _updatingMenuState = true;
            OpenAiMenuToggle.IsOn = !OpenAiMenuToggle.IsOn;
            _updatingMenuState = false;
        }
    }

    /// <summary>Forwards the screenshot-capture toggle to the validated settings application service.</summary>
    private async void ScreenshotsMenuToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingMenuState)
        {
            return;
        }

        var requestedValue = ScreenshotsMenuToggle.IsOn;
        var result = await _application.PatchSettingsAsync(
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["screenshots.enabled"] = requestedValue.ToString()
            }),
            CancellationToken.None);

        if (result.Succeeded && result.Value is not null)
        {
            _menuSettings = result.Value;
            SettingsApplied?.Invoke(result.Value);
            return;
        }

        _updatingMenuState = true;
        ScreenshotsMenuToggle.IsOn = !requestedValue;
        _updatingMenuState = false;
    }

    /// <summary>Localizes a compact icon command without replacing its visual content.</summary>
    private static void ApplyOverflowCommandLabel(Button button, string label)
    {
        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
    }

    /// <summary>Shows or hides the presentation-only latest-session panel.</summary>
    private async void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        _detailsExpanded = !_detailsExpanded;
        DetailsPanel.Visibility = _detailsExpanded ? Visibility.Visible : Visibility.Collapsed;
        DetailsChevron.Glyph = _detailsExpanded ? "\uE70E" : "\uE70D";
        AutomationProperties.SetName(DetailsButton, _detailsExpanded ? "Hide last session" : "Show last session");
        ResizeForLogicalContent(_detailsExpanded ? ExpandedPlayerHeight : PlayerHeight);
        if (_detailsExpanded)
        {
            var lastSession = await _viewModel.RefreshLastSessionAsync(CancellationToken.None);
            if (lastSession.Succeeded)
            {
                UpdateLastSession(lastSession.Value);
            }
            FadeIn(DetailsPanel);
        }
        ApplyFlyoutPosition(_position);
    }

    /// <summary>Forwards screenshot-gallery activation to the application composition root.</summary>
    private void ScreenshotPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestScreenshotPath is not { } screenshotPath || _latestScreenshotCapturedAt is not { } capturedAt)
        {
            return;
        }

        ScreenshotsRequested?.Invoke(this, new ScreenshotPreviewRequestedEventArgs(screenshotPath, capturedAt));
    }

    private void ScreenshotPreviewButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        => ScreenshotOpenOverlay.Opacity = _latestScreenshotPath is null ? 0 : 1;

    private void ScreenshotPreviewButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        => ScreenshotOpenOverlay.Opacity = 0;

    /// <summary>Returns from options to the player panel.</summary>
    private void OptionsControl_BackRequested(object sender, EventArgs e) => ShowPlayer();

    /// <summary>Returns from operational tools to the player panel.</summary>
    private void OperationsControl_BackRequested(object sender, EventArgs e) => ShowPlayer();

    /// <summary>Returns from the inline about panel to the player panel.</summary>
    private void AboutBackButton_Click(object sender, RoutedEventArgs e) => ShowPlayer();

    /// <summary>Shows one view panel and applies its expected compact size.</summary>
    private void ShowPanel(FrameworkElement panel, int height)
    {
        PlayerPanel.Visibility = Visibility.Collapsed;
        OptionsPanel.Visibility = Visibility.Collapsed;
        OperationsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
        TitleBarBackButton.Visibility = ReferenceEquals(panel, OptionsPanel) ? Visibility.Visible : Visibility.Collapsed;
        ResizeForLogicalContent(height);
        ApplyFlyoutPosition(_position);
        FadeIn(panel);
        DispatcherQueue.TryEnqueue(UpdateTitleBarLayout);
    }

    /// <summary>Restores the player panel.</summary>
    private void ShowPlayer()
    {
        OptionsPanel.Visibility = Visibility.Collapsed;
        OperationsPanel.Visibility = Visibility.Collapsed;
        AboutPanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
        TitleBarBackButton.Visibility = Visibility.Collapsed;
        ResizeForLogicalContent(_detailsExpanded ? ExpandedPlayerHeight : PlayerHeight);
        ApplyFlyoutPosition(_position);
        FadeIn(PlayerPanel);
        DispatcherQueue.TryEnqueue(UpdateTitleBarLayout);
    }

    /// <summary>Renders current dashboard values without making application calls.</summary>
    private void UpdatePlayer(DashboardState state)
    {
        CurrentContextText.Text = state.CurrentContext is "STATE_READY" ? T("StateReady") : state.CurrentContext is "STATE_IDLE" ? T("StateIdleContext") : state.CurrentContext;
        KeyCountText.Text = state.TotalKeyPresses.ToString("N0");
        ClickCountText.Text = state.TotalMouseClicks.ToString("N0");
        ActiveTimeText.Text = TimeSpan.FromSeconds(state.ActiveSeconds).ToString(@"hh\:mm\:ss");
        ActivityLine.Value = state.Intensity;
        TrackingStateText.Text = T(state.IsTracking ? "StateRunning" : "StatePaused");
        PlayPauseIcon.Glyph = state.IsTracking ? "\uE769" : "\uE768";
        LocalTimeText.Text = $"Local time {state.LocalTime:HH:mm:ss}";
        UtcTimeText.Text = $"UTC {state.UtcTime:HH:mm:ss}";
        AutomationProperties.SetName(TrackingButton, state.IsTracking ? "Metti in pausa il monitoraggio" : "Avvia il monitoraggio");
    }

    /// <summary>Renders the latest-session labels and the screenshot URI supplied by the application facade.</summary>
    private void UpdateLastSession(LastSessionState? session)
    {
        LastSessionAppText.Text = session?.Application ?? T("NoSession");
        LastSessionDetailText.Text = session?.Timestamp is null ? string.Empty : $"{session.Timestamp:HH:mm} · {session.Context}";
        if (session?.ScreenshotCapturedAt is { } capturedAt && Uri.TryCreate(session.ScreenshotPath, UriKind.Absolute, out var screenshotUri))
        {
            _latestScreenshotPath = session.ScreenshotPath;
            _latestScreenshotCapturedAt = capturedAt;
            LastScreenshotImage.Source = new BitmapImage(screenshotUri);
            LastScreenshotImage.Visibility = Visibility.Visible;
            ScreenshotPlaceholderImage.Visibility = Visibility.Collapsed;
            ScreenshotStatusText.Visibility = Visibility.Collapsed;
            ScreenshotPreviewButton.IsHitTestVisible = true;
            AutomationProperties.SetName(ScreenshotPreviewButton, "Open latest screenshot");
            ToolTipService.SetToolTip(ScreenshotPreviewButton, "Open latest screenshot");
            return;
        }

        ShowScreenshotPlaceholder();
    }

    /// <summary>Falls back to the packaged pastoral placeholder when an artifact cannot be rendered.</summary>
    private void LastScreenshotImage_ImageFailed(object sender, ExceptionRoutedEventArgs e) => ShowScreenshotPlaceholder();

    private void ShowScreenshotPlaceholder()
    {
        _latestScreenshotPath = null;
        _latestScreenshotCapturedAt = null;
        LastScreenshotImage.Source = null;
        LastScreenshotImage.Visibility = Visibility.Collapsed;
        ScreenshotPlaceholderImage.Visibility = Visibility.Visible;
        ScreenshotStatusText.Visibility = Visibility.Visible;
        ScreenshotStatusText.Text = T(_screenshotsEnabled ? "ScreenshotUnavailableHint" : "ScreenshotDisabledHint");
        ScreenshotOpenOverlay.Opacity = 0;
        ScreenshotPreviewButton.IsHitTestVisible = false;
        AutomationProperties.SetName(ScreenshotPreviewButton, "No captured screenshot");
        ToolTipService.SetToolTip(ScreenshotPreviewButton, "No captured screenshot");
    }

    /// <summary>Applies presentation settings already validated and persisted by the application layer.</summary>
    private void ApplySettings(AppSettings settings)
    {
        _strings = new LocalizationService(settings.UiLanguage);
        _theme = settings.Theme;
        _position = settings.FlyoutPosition;
        _screenshotsEnabled = settings.ScreenshotsEnabled;
        RootGrid.RequestedTheme = _theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        UiLocalization.Apply(RootGrid, _strings);
        if (ScreenshotPlaceholderImage.Visibility == Visibility.Visible)
        {
            ScreenshotStatusText.Text = T(_screenshotsEnabled ? "ScreenshotUnavailableHint" : "ScreenshotDisabledHint");
        }
        OptionsControl.ApplyLanguage(settings.UiLanguage);
        OperationsControl.ApplyLanguage(settings.UiLanguage);
        ApplyFlyoutPosition(_position);

        SettingsApplied?.Invoke(settings);
    }

    private string T(string key) => _strings.Translate(key);

    /// <summary>Shows and positions the player when requested from the taskbar control.</summary>
    public void ShowFlyout()
    {
        ApplyFlyoutPosition(_position);
        Activate();
    }

    /// <summary>Starts the player entrance fade.</summary>
    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        ResizeForLogicalContent(_logicalHeight);
        ApplyFlyoutPosition(_position);
        FadeIn(PlayerPanel);
    }

    /// <summary>Keeps the requested WinUI logical size stable when the window crosses displays with different DPI.</summary>
    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _rasterizationScale) < 0.001d)
        {
            return;
        }

        ResizeForLogicalContent(_logicalHeight);
        ApplyFlyoutPosition(_position);
    }

    /// <summary>Fades a view into the compact player without changing geometry on pointer interaction.</summary>
    private static void FadeIn(FrameworkElement element)
    {
        element.Opacity = 0;
        var animation = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(180)) };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    /// <summary>Converts the compact surface size from WinUI DIPs to the physical pixels required by AppWindow.</summary>
    private void ResizeForLogicalContent(int logicalHeight)
    {
        _logicalHeight = logicalHeight;
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? _rasterizationScale);
        _rasterizationScale = scale;

        var workArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var physicalMargin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var availableWidth = Math.Max(1, workArea.Width - (physicalMargin * 2));
        var availableHeight = Math.Max(1, workArea.Height - (physicalMargin * 2));
        var physicalWidth = Math.Min(availableWidth, (int)Math.Ceiling(LogicalWindowWidth * scale));
        var physicalHeight = Math.Min(availableHeight, (int)Math.Ceiling(logicalHeight * scale));
        _appWindow.Resize(new SizeInt32(physicalWidth, physicalHeight));
    }

    /// <summary>Places the player at the selected visual anchor.</summary>
    private void ApplyFlyoutPosition(string position)
    {
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var margin = (int)Math.Ceiling(LogicalScreenMargin * _rasterizationScale);
        var x = position switch
        {
            FlyoutPositions.BottomLeft or FlyoutPositions.TopLeft => area.X + margin,
            FlyoutPositions.TopRight or FlyoutPositions.BottomRight => area.X + area.Width - _appWindow.Size.Width - margin,
            _ => area.X + (area.Width - _appWindow.Size.Width) / 2
        };
        var y = position is FlyoutPositions.TopLeft or FlyoutPositions.TopRight ? area.Y + margin : area.Y + area.Height - _appWindow.Size.Height - margin;
        _appWindow.Move(new PointInt32(x, y));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        _refreshTimer.Stop();
    }
}

/// <summary>Identifies the retained capture that the screenshot inspector should select.</summary>
public sealed class ScreenshotPreviewRequestedEventArgs(string screenshotPath, DateTimeOffset capturedAt) : EventArgs
{
    public string ScreenshotPath { get; } = screenshotPath;

    public DateTimeOffset CapturedAt { get; } = capturedAt;
}
