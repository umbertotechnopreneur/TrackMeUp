#region Using directives
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
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
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Input;
using TrackMeUp.Application;
using TrackMeUp.Controls;
using TrackMeUp.Presentation;
using TrackMeUp.Runtime;
using TrackMeUp.Services;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
#endregion

namespace TrackMeUp;

/// <summary>Displays the compact player and forwards user intent to UI-neutral view models.</summary>
public sealed partial class MainWindow : Window
{
    #region Fields

    private const int LogicalWindowWidth = 450;
    private const int LogicalScreenMargin = 22;
    private const int WindowResizeAnimationDurationMilliseconds = 180;
    private const int DwmWindowAttributeBorderColor = 34;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private readonly ITrackMeUpApplication _application;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly DispatcherQueueTimer _windowResizeAnimationTimer;
    private readonly AppWindow _appWindow;
    private readonly MainWindowLayoutState _layoutState = new();
    private readonly MicaDialogService _dialogs;
    private readonly TrayIconService _trayIcon;
    private readonly TaskCompletionSource _rootLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RectInt32 _currentWorkArea;
    private LocalizationService _strings = new("system");
    private bool _updatingMenuState;
    private double _rasterizationScale = 1d;
    private DateTimeOffset _windowResizeAnimationStartedAt;
    private SizeInt32 _windowResizeAnimationStartSize;
    private SizeInt32 _windowResizeAnimationTargetSize;
    private string _theme = "system";
    private string _position = FlyoutPositions.BottomCenter;
    private AppSettings? _menuSettings;
    private AboutWindow? _aboutWindow;
    private ScheduleWindow? _scheduleWindow;
    private XamlRoot? _xamlRoot;
    private string? _latestScreenshotPath;
    private DateTimeOffset? _latestScreenshotCapturedAt;
    private bool _screenshotsEnabled;
    private const int PendingSnapshotDeleteSeconds = 30;
    private bool _pendingSnapshotDeleteInProgress;
    private bool _startupAiWarningShown;
    private int _notificationDrainInProgress;
    private DateTimeOffset _nextAiSpendRefreshAt = DateTimeOffset.MinValue;
    private int _aiSpendRefreshInProgress;
    private MainWindowSurface _operationsReturnSurface = MainWindowSurface.Player;
    #endregion

    /// <summary>Gets the single observable AI state shared by the player menu and options surface.</summary>
    public AiApplicationState AiState { get; }

    #region Events

    /// <summary>Occurs when a fully persisted settings snapshot has been applied to the player surface.</summary>
    public event Action<AppSettings>? SettingsApplied;

    /// <summary>Occurs when the user requests the dedicated reports surface.</summary>
    public event EventHandler? ReportsRequested;

    /// <summary>Occurs when the user requests the floating local-search surface.</summary>
    public event EventHandler? SearchRequested;

    /// <summary>Occurs when the user requests the retained screenshot gallery surface.</summary>
    public event EventHandler? ScreenshotGalleryRequested;

    /// <summary>Occurs when the user requests the retained screenshot gallery.</summary>
    public event EventHandler<ScreenshotPreviewRequestedEventArgs>? ScreenshotsRequested;

    /// <summary>Occurs when the notification-area context menu requests an orderly application exit.</summary>
    public event EventHandler? ExitRequested;

    #endregion

    #region Initialization

    /// <summary>Creates the player view with the shared application facade supplied by the composition root.</summary>
    internal MainWindow(ITrackMeUpApplication application, LaunchOptions options, MicaDialogService dialogs, TrayIconService trayIcon)
    {
        _application = application;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
        _viewModel = new MainViewModel(application);
        AiState = new AiApplicationState(application);
        InitializeComponent();
        AiState.PropertyChanged += AiState_PropertyChanged;
        _trayIcon.ExitRequested += TrayIcon_ExitRequested;
        UpdateOpenAiMenuAccessibility();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragRegion);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _currentWorkArea = CurrentWorkArea();
        _appWindow.Changed += AppWindow_Changed;
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
        ApplyBorderlessPlayerWindow();
        ResizeForLogicalContent(_layoutState.LogicalHeight);

        OptionsControl.Initialize(application, AiState);
        OptionsControl.SettingsSaved += ApplySettings;
        OptionsControl.LayoutChanged += OptionsControl_LayoutChanged;
        OptionsControl.AiConnectionTestRequested += OptionsControl_AiConnectionTestRequested;
        OptionsControl.OperationsSectionRequested += OptionsControl_OperationsSectionRequested;
        OperationsControl.Initialize(application, _dialogs, this);
        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(1);
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        _refreshTimer.Start();

        _windowResizeAnimationTimer = DispatcherQueue.CreateTimer();
        _windowResizeAnimationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _windowResizeAnimationTimer.Tick += WindowResizeAnimationTimer_Tick;

        _ = InitializeAsync(options);
        Closed += MainWindow_Closed;
    }

    private void ApplyBorderlessPlayerWindow()
    {
        var color = DwmColorNone;
        _ = DwmSetWindowAttribute(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            DwmWindowAttributeBorderColor,
            ref color,
            Marshal.SizeOf<uint>());
    }

    private async Task InitializeAsync(LaunchOptions options)
    {
        var initialization = await _viewModel.InitializeAsync(options, CancellationToken.None);
        if (initialization.Succeeded && initialization.Value is not null)
        {
            ApplySettings(initialization.Value.Settings);
            UpdatePlayer(initialization.Value.Dashboard);
        }
        else
        {
            // If launch initialization fails, keep the player usable and let the normal dashboard read expose runtime state.
            await RefreshDashboardAsync();
        }

        await _rootLoaded.Task;
        await ShowStartupAiWarningAsync();
        await DrainApplicationNotificationsAsync();
    }

    #endregion

    private async Task RefreshDashboardAsync()
    {
        var state = await _viewModel.RefreshAsync(CancellationToken.None);
        if (state.Succeeded && state.Value is not null)
        {
            UpdatePlayer(state.Value);
        }

        await RefreshAiMonthlySpendAsync();
        await DrainApplicationNotificationsAsync();
    }

    /// <summary>Refreshes the month-to-date AI spend at a bounded cadence while the integration is active.</summary>
    private async Task RefreshAiMonthlySpendAsync()
    {
        if (!AiState.Enabled)
        {
            AiMonthlySpendPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (DateTimeOffset.Now < _nextAiSpendRefreshAt || Interlocked.Exchange(ref _aiSpendRefreshInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            var result = await _application.GetAiPricingOverviewAsync(CancellationToken.None);
            if (result.Succeeded && result.Value is not null)
            {
                UpdateAiMonthlySpend(result.Value);
                _nextAiSpendRefreshAt = DateTimeOffset.Now.AddMinutes(1);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _aiSpendRefreshInProgress, 0);
        }
    }

    /// <summary>Renders the current local calendar-month AI spend using actual provider cost when available.</summary>
    private void UpdateAiMonthlySpend(AiPricingOverview overview)
    {
        var cost = overview.ActualCostCurrentMonthUsd ?? overview.EstimatedCostCurrentMonthUsd ?? 0m;
        AiMonthlySpendText.Text = "$" + cost.ToString("0.0", CultureInfo.InvariantCulture);
        var range = $"{overview.CurrentMonthStart:dd/MM}-{overview.CurrentMonthEnd:dd/MM}";
        AiMonthlySpendRangeText.Text = range;
        AiMonthlySpendPanel.Visibility = Visibility.Visible;
    }

    private async Task ShowStartupAiWarningAsync()
    {
        if (_startupAiWarningShown)
        {
            return;
        }

        var status = await AiState.LoadAsync(CancellationToken.None);
        if (status is not { Succeeded: true, Value: { Enabled: true, HasKey: false } aiStatus })
        {
            return;
        }

        _startupAiWarningShown = true;
        await _dialogs.ShowInformativeAsync(
            _application,
            this,
            MicaDialogRequest.Informative(
                T("Dialog.AiKeyMissing.Title"),
                string.Format(CultureInfo.CurrentCulture, T("Dialog.AiKeyMissing.Message"), aiStatus.KeyVariable),
                MicaDialogSeverity.Warning,
                T("Dialog.Ok")),
            RootGrid.RequestedTheme);
    }

    private async Task DrainApplicationNotificationsAsync()
    {
        if (Interlocked.Exchange(ref _notificationDrainInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            var result = await _application.DrainApplicationNotificationsAsync(CancellationToken.None);
            if (!result.Succeeded || result.Value is null)
            {
                return;
            }

            foreach (var notification in result.Value)
            {
                var severity = notification.Severity switch
                {
                    ApplicationNotificationSeverity.Error => MicaDialogSeverity.Error,
                    ApplicationNotificationSeverity.Warning => MicaDialogSeverity.Warning,
                    _ => MicaDialogSeverity.Information
                };
                await _dialogs.ShowInformativeAsync(
                    _application,
                    this,
                    MicaDialogRequest.Informative(
                        T(notification.TitleKey),
                        FormatNotificationMessage(notification),
                        severity,
                        T("Dialog.Ok")),
                    RootGrid.RequestedTheme);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _notificationDrainInProgress, 0);
        }
    }

    /// <summary>Captures a screenshot manually when the user clicks the "Take snapshot" button.</summary>
    private async void TakeScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TakeScreenshotButton.IsEnabled)
        {
            return;
        }

        TakeScreenshotButton.IsEnabled = false;
        var result = await _application.CaptureManualScreenshotAsync(CancellationToken.None);
        if (!result.Succeeded)
        {
            await RefreshDashboardAsync();
            return;
        }

        await RefreshDashboardAsync();

        // Refresh the last session to show the newly captured screenshot.
        var lastSession = await _viewModel.RefreshLastSessionAsync(CancellationToken.None);
        if (lastSession.Succeeded)
        {
            UpdateLastSession(lastSession.Value);
        }
    }

    /// <summary>Opens the detached screenshot scheduling window.</summary>
    private async void ScheduleScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
        => await OpenScheduleWindowAsync();

    private async void OutsideActiveHoursSettingsLink_Click(object sender, RoutedEventArgs e)
        => await OpenScheduleWindowAsync();

    private async Task OpenScheduleWindowAsync()
    {
        if (MoreButton.Flyout is Flyout flyout)
        {
            flyout.Hide();
        }

        if (_scheduleWindow is not null)
        {
            _scheduleWindow.Activate();
            return;
        }

        var settingsResult = await _application.GetSettingsAsync(CancellationToken.None);
        if (!settingsResult.Succeeded || settingsResult.Value is null)
        {
            return;
        }

        var scheduleWindow = new ScheduleWindow(
            settingsResult.Value.ActiveHours,
            settingsResult.Value.ScreenshotIntervalMinutes,
            _theme,
            settingsResult.Value.UiLanguage,
            _application,
            _dialogs);
        scheduleWindow.ScheduleConfirmed += ScheduleWindow_ScheduleConfirmed;
        scheduleWindow.Closed += ScheduleWindow_Closed;
        _scheduleWindow = scheduleWindow;
        scheduleWindow.Activate();
    }

    /// <summary>Persists a confirmed schedule and starts or stops its timer from the main runtime owner.</summary>
    private async void ScheduleWindow_ScheduleConfirmed(object? sender, ScheduleConfigurationEventArgs eventArgs)
    {
        var patch = new SettingsPatch(new Dictionary<string, string?>
        {
            ["active_hours.monday.active"] = eventArgs.ActiveHours.Single(day => day.Day == "monday").ActivePeriod,
            ["active_hours.monday.breaks"] = eventArgs.ActiveHours.Single(day => day.Day == "monday").BreakPeriods,
            ["active_hours.tuesday.active"] = eventArgs.ActiveHours.Single(day => day.Day == "tuesday").ActivePeriod,
            ["active_hours.tuesday.breaks"] = eventArgs.ActiveHours.Single(day => day.Day == "tuesday").BreakPeriods,
            ["active_hours.wednesday.active"] = eventArgs.ActiveHours.Single(day => day.Day == "wednesday").ActivePeriod,
            ["active_hours.wednesday.breaks"] = eventArgs.ActiveHours.Single(day => day.Day == "wednesday").BreakPeriods,
            ["active_hours.thursday.active"] = eventArgs.ActiveHours.Single(day => day.Day == "thursday").ActivePeriod,
            ["active_hours.thursday.breaks"] = eventArgs.ActiveHours.Single(day => day.Day == "thursday").BreakPeriods,
            ["active_hours.friday.active"] = eventArgs.ActiveHours.Single(day => day.Day == "friday").ActivePeriod,
            ["active_hours.friday.breaks"] = eventArgs.ActiveHours.Single(day => day.Day == "friday").BreakPeriods,
            ["active_hours.saturday.active"] = eventArgs.ActiveHours.Single(day => day.Day == "saturday").ActivePeriod,
            ["active_hours.saturday.breaks"] = eventArgs.ActiveHours.Single(day => day.Day == "saturday").BreakPeriods,
            ["active_hours.sunday.active"] = eventArgs.ActiveHours.Single(day => day.Day == "sunday").ActivePeriod,
            ["active_hours.sunday.breaks"] = eventArgs.ActiveHours.Single(day => day.Day == "sunday").BreakPeriods,
            ["screenshots.interval_minutes"] = eventArgs.IntervalMinutes.ToString(CultureInfo.InvariantCulture)
        });
        var saveResult = await _application.PatchSettingsAsync(patch, CancellationToken.None);
        if (!saveResult.Succeeded || saveResult.Value is null)
        {
            return;
        }

        ApplySettings(saveResult.Value);
        await RefreshDashboardAsync();

        if (sender is ScheduleWindow scheduleWindow)
        {
            scheduleWindow.Close();
        }
    }

    /// <summary>Releases the detached scheduling window reference after it closes.</summary>
    private void ScheduleWindow_Closed(object sender, WindowEventArgs args)
    {
        if (sender is ScheduleWindow scheduleWindow)
        {
            scheduleWindow.ScheduleConfirmed -= ScheduleWindow_ScheduleConfirmed;
            scheduleWindow.Closed -= ScheduleWindow_Closed;
        }

        _scheduleWindow = null;
    }

    /// <summary>Shows the shared overflow flyout from its title-bar command.</summary>
    private void TitleBarMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.Flyout is MenuFlyout flyout)
        {
            flyout.ShowAt(TitleBarMoreButton);
        }
    }

    /// <summary>Delegates compact settings navigation to the active passive view.</summary>
    private void TitleBarBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (OperationsPanel.Visibility == Visibility.Visible)
        {
            OperationsControl.NavigateBack();
            return;
        }

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

        var passthroughRects = new List<RectInt32>
        {
            ElementRect(TitleBarMoreButton, scale),
            ElementRect(TitleBarSearchButton, scale),
            ElementRect(TitleBarReportButton, scale)
        };
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
        ApplyMainMenuLabels();
        var settingsTask = _application.GetSettingsAsync(CancellationToken.None);
        var aiStateTask = AiState.LoadAsync(CancellationToken.None);
        await Task.WhenAll(settingsTask, aiStateTask);
        AiPricingMenuItem.IsEnabled = false;
        var result = await settingsTask;
        if (!result.Succeeded || result.Value is null)
        {
            return;
        }

        _menuSettings = result.Value;
        AiPricingMenuItem.IsEnabled = IsOpenAiPricingAvailable(result.Value);
        _screenshotsEnabled = result.Value.ScreenshotsEnabled;
        UpdateScreenshotCaptureStatus();
        _updatingMenuState = true;
        ScreenshotsMenuToggle.IsChecked = result.Value.ScreenshotsEnabled;
        _updatingMenuState = false;

    }

    /// <summary>Shows the options view.</summary>
    private void OptionsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        ShowOptionsPanel();
    }

    private void ShowOptionsPanel()
    {
        ShowPanel(OptionsPanel, MainWindowSurface.Options);
    }

    /// <summary>Forwards report-window activation to the application composition root.</summary>
    private void ReportsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        RequestReports();
    }

    /// <summary>Opens reports directly from the first-class title-bar command.</summary>
    private void TitleBarReportButton_Click(object sender, RoutedEventArgs e) => RequestReports();

    private void RequestReports() => ReportsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Forwards search-window activation to the application composition root.</summary>
    private void SearchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        RequestSearch();
    }

    /// <summary>Opens local search directly from the title bar.</summary>
    private void TitleBarSearchButton_Click(object sender, RoutedEventArgs e) => RequestSearch();

    private void RequestSearch() => SearchRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Routes the small set of primary window shortcuts to the same passive commands as the menu.</summary>
    private void MainKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        switch (sender.Key)
        {
            case Windows.System.VirtualKey.P:
                RequestSearch();
                break;
            case Windows.System.VirtualKey.R:
                RequestReports();
                break;
            case Windows.System.VirtualKey.G:
                RequestScreenshotGallery();
                break;
            case Windows.System.VirtualKey.O:
                ShowOptionsPanel();
                break;
        }
    }

    private void RequestScreenshotGallery() => ScreenshotGalleryRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Forwards gallery-window activation to the application composition root.</summary>
    private void ScreenshotsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        ScreenshotGalleryRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the operational and diagnostic facade surface.</summary>
    private void OperationsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        _operationsReturnSurface = MainWindowSurface.Player;
        OperationsControl.ShowOverview();
        ShowPanel(OperationsPanel, MainWindowSurface.Operations);
    }

    /// <summary>Hides the player from the taskbar while retaining its notification-area activation icon.</summary>
    private async void MinimizeToTrayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        try
        {
            HideToNotificationArea();
        }
        catch (Exception)
        {
            await _dialogs.ShowInformativeAsync(
                _application,
                this,
                MicaDialogRequest.Informative(
                    T("Tray.UnavailableTitle"),
                    T("Tray.UnavailableMessage"),
                    MicaDialogSeverity.Error,
                    T("Dialog.Ok")),
                RootGrid.RequestedTheme);
        }
    }

    /// <summary>Shows simplified OpenAI pricing and local estimated usage costs.</summary>
    private async void AiPricingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        AiPricingMenuItem.IsEnabled = false;
        try
        {
            var result = await _application.GetAiPricingOverviewAsync(CancellationToken.None);
            if (result.Succeeded && result.Value is not null)
            {
                await _dialogs.ShowPricingAsync(_application, this, result.Value, RootGrid.RequestedTheme, _strings);
                return;
            }

            await _dialogs.ShowInformativeAsync(
                _application,
                this,
                MicaDialogRequest.Informative(
                    T("AiPricing.UnavailableTitle"),
                    T("AiPricing.UnavailableMessage"),
                    MicaDialogSeverity.Warning,
                    T("Dialog.Ok")),
                RootGrid.RequestedTheme);
        }
        finally
        {
            AiPricingMenuItem.IsEnabled = _menuSettings is not null && IsOpenAiPricingAvailable(_menuSettings);
        }
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
    private async void OpenAiMenuToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_updatingMenuState || OpenAiMenuToggle.IsChecked == AiState.Enabled)
        {
            return;
        }

        var result = await AiState.SetEnabledAsync(OpenAiMenuToggle.IsChecked, CancellationToken.None);
        if (!result.Succeeded)
        {
            _updatingMenuState = true;
            OpenAiMenuToggle.IsChecked = AiState.Enabled;
            _updatingMenuState = false;
            AiPricingMenuItem.IsEnabled = _menuSettings is not null && IsOpenAiPricingAvailable(_menuSettings);
            return;
        }

        if (_menuSettings is not null)
        {
            _menuSettings = _menuSettings with { OpenAiEnabled = AiState.Enabled };
        }

        AiPricingMenuItem.IsEnabled = _menuSettings is not null && IsOpenAiPricingAvailable(_menuSettings);
    }

    /// <summary>Forwards the screenshot-capture toggle to the validated settings application service.</summary>
    private async void ScreenshotsMenuToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_updatingMenuState)
        {
            return;
        }

        var requestedValue = ScreenshotsMenuToggle.IsChecked;
        var result = await _application.PatchSettingsAsync(
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["screenshots.enabled"] = requestedValue.ToString()
            }),
            CancellationToken.None);

        if (result.Succeeded && result.Value is not null)
        {
            _menuSettings = result.Value;
            ApplySettings(result.Value);
            return;
        }

        _updatingMenuState = true;
        ScreenshotsMenuToggle.IsChecked = !requestedValue;
        _updatingMenuState = false;
    }

    /// <summary>Localizes the logical menu groups and commands without coupling them to one AI vendor.</summary>
    private void ApplyMainMenuLabels()
    {
        ActivityMenu.Text = T("Main.Menu.Activity");
        CaptureMenu.Text = T("Main.Menu.Capture");
        SettingsMenu.Text = T("Main.Menu.Settings");
        AiProviderMenu.Text = T("Main.Menu.AiProvider");
        SearchMenuItem.Text = T("Search.Title");
        ReportsMenuItem.Text = T("Reports.Title");
        ScreenshotsMenuItem.Text = T("Screenshots.Caption");
        ScheduleMenuItem.Text = T("Schedule.Snapshots");
        ScreenshotsMenuToggle.Text = T("MenuToggleScreenshot");
        OptionsMenuItem.Text = T("MenuTitleOptions");
        OperationsMenuItem.Text = T("Main.Menu.Operations");
        OpenAiMenuToggle.Text = T("MenuToggleOpenAi");
        AiPricingMenuItem.Text = T("AiPricing.MenuTitle");
        MinimizeToTrayMenuItem.Text = T("Main.Menu.MinimizeToTray");
        AboutMenuItem.Text = T("MenuTitleAbout");
    }

    private static bool IsOpenAiPricingAvailable(AppSettings settings) =>
        settings.OpenAiEnabled &&
        string.Equals(settings.AiProvider, "openai", StringComparison.OrdinalIgnoreCase);

    /// <summary>Shows or hides the presentation-only latest-session panel.</summary>
    private async void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = TogglePlayerSection(MainWindowLayoutSection.LastSession, DetailsPanel);
        DetailsChevron.Glyph = isVisible ? "\uE70E" : "\uE70D";
        UpdateDetailsAccessibility();
        if (isVisible)
        {
            var lastSession = await _viewModel.RefreshLastSessionAsync(CancellationToken.None);
            if (lastSession.Succeeded)
            {
                UpdateLastSession(lastSession.Value);
            }
            FadeIn(DetailsPanel);
        }
        if (!_windowResizeAnimationTimer.IsRunning)
        {
            ApplyFlyoutPosition(_position);
        }
    }

    /// <summary>Shows or hides the presentation-only live activity-score histogram.</summary>
    private void ActivityScoreButton_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = TogglePlayerSection(MainWindowLayoutSection.ActivityScore, ActivityScorePanel);
        ActivityScoreChevron.Glyph = isVisible ? "\uE70E" : "\uE70D";
        UpdateActivityScoreAccessibility();
        if (isVisible)
        {
            FadeIn(ActivityScorePanel);
        }

        if (!_windowResizeAnimationTimer.IsRunning)
        {
            ApplyFlyoutPosition(_position);
        }
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

    /// <summary>Opens one operational detail requested from the settings overview.</summary>
    private void OptionsControl_OperationsSectionRequested(OperationsSection section)
    {
        _operationsReturnSurface = MainWindowSurface.Options;
        OperationsControl.NavigateTo(section, returnToOverview: false);
        ShowPanel(OperationsPanel, MainWindowSurface.Operations);
    }

    /// <summary>Returns from the operations landing page to the surface that opened it.</summary>
    private void OperationsControl_BackRequested(object sender, EventArgs e)
    {
        var returnSurface = _operationsReturnSurface;
        _operationsReturnSurface = MainWindowSurface.Player;
        if (returnSurface == MainWindowSurface.Options)
        {
            ShowPanel(OptionsPanel, MainWindowSurface.Options);
            return;
        }

        ShowPlayer();
    }

    /// <summary>Re-measures the options surface after one of its nested sections changes visibility.</summary>
    private void OptionsControl_LayoutChanged(object? sender, EventArgs e)
    {
        if (_layoutState.Surface == MainWindowSurface.Options)
        {
            ResizeForCurrentLayout(animate: true);
        }
    }

    /// <summary>Re-measures the operations surface after its landing or detail view changes.</summary>
    private void OperationsControl_LayoutChanged(object? sender, EventArgs e)
    {
        if (_layoutState.Surface == MainWindowSurface.Operations)
        {
            ResizeForCurrentLayout(animate: true);
        }
    }

    /// <summary>Shows one top-level panel and measures the visible XAML content before resizing.</summary>
    private void ShowPanel(FrameworkElement panel, MainWindowSurface surface)
    {
        PlayerPanel.Visibility = Visibility.Collapsed;
        OptionsPanel.Visibility = Visibility.Collapsed;
        OperationsPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
        _layoutState.ShowSurface(surface);
        TitleBarBackButton.Visibility = Visibility.Visible;
        ResizeForCurrentLayout(animate: false);
        ApplyFlyoutPosition(_position);
        FadeIn(panel);
        DispatcherQueue.TryEnqueue(UpdateTitleBarLayout);
    }

    /// <summary>Restores the player panel.</summary>
    private void ShowPlayer()
    {
        OptionsPanel.Visibility = Visibility.Collapsed;
        OperationsPanel.Visibility = Visibility.Collapsed;
        PlayerPanel.Visibility = Visibility.Visible;
        _layoutState.ShowSurface(MainWindowSurface.Player);
        TitleBarBackButton.Visibility = Visibility.Collapsed;
        ResizeForCurrentLayout(animate: false);
        ApplyFlyoutPosition(_position);
        FadeIn(PlayerPanel);
        DispatcherQueue.TryEnqueue(UpdateTitleBarLayout);
    }

    /// <summary>Toggles one player section and resizes from the XAML content currently visible.</summary>
    private bool TogglePlayerSection(MainWindowLayoutSection section, FrameworkElement element)
    {
        var isVisible = _layoutState.ToggleSection(section);
        element.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ResizeForCurrentLayout(animate: RootGrid.IsLoaded);
        return isVisible;
    }

    /// <summary>Applies one player section visibility and re-measures only when the layout changed.</summary>
    private void SetPlayerSectionVisibility(MainWindowLayoutSection section, FrameworkElement element, bool isVisible)
    {
        var changed = _layoutState.SetSectionVisibility(section, isVisible);
        element.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        if (changed && _layoutState.Surface == MainWindowSurface.Player)
        {
            ResizeForCurrentLayout(animate: RootGrid.IsLoaded);
        }
    }

    /// <summary>Measures the active XAML surface at the flyout width and applies the resulting window height.</summary>
    private void ResizeForCurrentLayout(bool animate)
    {
        RootGrid.Measure(new Size(LogicalWindowWidth, double.PositiveInfinity));
        var logicalHeight = _layoutState.RecordMeasuredHeight(RootGrid.DesiredSize.Height);
        if (animate && RootGrid.IsLoaded)
        {
            AnimateResizeForLogicalContent(logicalHeight);
            return;
        }

        ResizeForLogicalContent(logicalHeight);
    }

    /// <summary>Renders current dashboard values without making application calls.</summary>
    private void UpdatePlayer(DashboardState state)
    {
        UpdateActiveHoursAvailability(state.IsWithinActiveHours);
        var currentContext = state.CurrentContext is "STATE_READY"
            ? T("StateReady")
            : state.CurrentContext is "STATE_IDLE"
                ? T("StateIdleContext")
                : state.CurrentContext;
        CurrentContextText.Text = FormatCurrentContext(currentContext);
        KeyCountText.Text = state.TotalKeyPresses.ToString("N0");
        ClickCountText.Text = state.TotalMouseClicks.ToString("N0");
        ActiveTimeText.Text = TimeSpan.FromSeconds(state.ActiveSeconds).ToString(@"hh\:mm\:ss");
        RenderActivityScore(state.ActivityScore);
        TrackingStateText.Text = T(state.IsTracking ? "StateRunning" : "StatePaused");
        PlayPauseIcon.Glyph = state.IsTracking ? "\uE769" : "\uE768";
        LocalTimeText.Text = $"Local time {state.LocalTime:HH:mm:ss}";
        UtcTimeText.Text = $"UTC {state.UtcTime:HH:mm:ss}";
        AutomationProperties.SetName(TrackingButton, state.IsTracking ? "Metti in pausa il monitoraggio" : "Avvia il monitoraggio");

        // The runtime owns the deadline and freezes this value while tracking is paused.
        if (state.ScheduledSnapshotRemaining is { } scheduledSnapshotRemaining)
        {
            var remainingSeconds = Math.Max(0, (int)Math.Ceiling(scheduledSnapshotRemaining.TotalSeconds));
            var remaining = TimeSpan.FromSeconds(remainingSeconds);
            ElapsedText.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        }
        else
        {
            ElapsedText.Text = "00:00";
        }

        UpdatePendingSnapshotDeleteUi(state.PendingManualScreenshot);
    }

    private static string FormatCurrentContext(string context)
    {
        var parts = context.Split(" · ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && string.Equals(parts[0], parts[1], StringComparison.OrdinalIgnoreCase)
            ? parts[0]
            : context;
    }

    /// <summary>Updates the temporary delete action and its 30-second countdown.</summary>
    private void UpdatePendingSnapshotDeleteUi(PendingManualScreenshotState? pendingSnapshot)
    {
        if (_pendingSnapshotDeleteInProgress)
        {
            SetPlayerSectionVisibility(MainWindowLayoutSection.PendingSnapshot, PendingSnapshotPanel, isVisible: false);
            DeleteSnapshotButton.Visibility = Visibility.Collapsed;
            TakeScreenshotButton.IsEnabled = false;
            return;
        }

        if (pendingSnapshot is not { } pending)
        {
            SetPlayerSectionVisibility(MainWindowLayoutSection.PendingSnapshot, PendingSnapshotPanel, isVisible: false);
            DeleteSnapshotButton.Visibility = Visibility.Collapsed;
            TakeScreenshotButton.IsEnabled = true;
            return;
        }

        var remaining = pending.ExpiresAt - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            SetPlayerSectionVisibility(MainWindowLayoutSection.PendingSnapshot, PendingSnapshotPanel, isVisible: false);
            DeleteSnapshotButton.Visibility = Visibility.Collapsed;
            TakeScreenshotButton.IsEnabled = true;
            return;
        }

        var remainingSeconds = Math.Clamp(remaining.TotalSeconds, 0, PendingSnapshotDeleteSeconds);
        SnapshotDeleteCountdownText.Text = $"00:{Math.Max(1, (int)Math.Ceiling(remainingSeconds)):00}";
        DeleteSnapshotButton.IsEnabled = true;
        DeleteSnapshotButton.Visibility = Visibility.Visible;
        TakeScreenshotButton.IsEnabled = false;
        SetPlayerSectionVisibility(MainWindowLayoutSection.PendingSnapshot, PendingSnapshotPanel, isVisible: true);
    }

    /// <summary>Renders the bounded one-minute score series supplied by the application facade.</summary>
    private void RenderActivityScore(ActivityScoreState? state)
    {
        if (state is not { Minutes.Count: > 0 })
        {
            ActivityScoreValueText.Text = "0";
            ActivityScoreTelemetryText.Text = "CPU -- · GPU --";
            ActivityScorePreviousIntervalText.Text = T("Activity.Previous") + ": --";
            ActivityScoreLatestIntervalText.Text = T("Activity.Latest") + ": --";
            ActivityScoreBarHost.Children.Clear();
            ActivityScoreBarHost.ColumnDefinitions.Clear();
            return;
        }

        var latestMinute = state.Minutes[^1];
        ActivityScoreValueText.Text = state.CurrentScore.ToString(CultureInfo.CurrentCulture);
        ActivityScoreTelemetryText.Text = $"CPU {latestMinute.CpuUsagePercent}% · GPU {(latestMinute.GpuUsagePercent is { } gpu ? $"{gpu}%" : "--")}";
        ActivityScorePreviousIntervalText.Text = FormatInterval(T("Activity.Previous"), state.PreviousSnapshotInterval);
        ActivityScoreLatestIntervalText.Text = FormatInterval(T("Activity.Latest"), state.LatestSnapshotInterval);

        var maximumKeys = Math.Max(1L, state.Minutes.Max(minute => minute.KeyPresses));
        var maximumClicks = Math.Max(1L, state.Minutes.Max(minute => minute.MouseClicks));
        var inputStartIndex = Math.Max(0, state.Minutes.Count - (state.SnapshotIntervalMinutes * 2));
        var latestSnapshotStartIndex = Math.Max(0, state.Minutes.Count - state.SnapshotIntervalMinutes);
        var scoreBrush = (Brush)PlayerPanel.Resources["PlayerAccentTextBrush"];
        var inputBrush = GetActivityInputBrush();
        ActivityScoreBarHost.Children.Clear();
        ActivityScoreBarHost.ColumnDefinitions.Clear();
        for (var index = 0; index < state.Minutes.Count; index++)
        {
            var minute = state.Minutes[index];
            ActivityScoreBarHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            var cell = new Grid { Margin = new Thickness(1, 0, 1, 0) };
            Grid.SetColumn(cell, index);
            cell.Children.Add(new Rectangle
            {
                Height = Math.Max(2d, minute.Score / 100d * 78d),
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = scoreBrush,
                Opacity = 0.25,
                RadiusX = 1,
                RadiusY = 1
            });
            if (index >= inputStartIndex)
            {
                cell.Children.Add(new Rectangle
                {
                    Width = 2,
                    Height = Math.Max(2d, minute.KeyPresses / (double)maximumKeys * 42d),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Fill = inputBrush,
                    RadiusX = 1,
                    RadiusY = 1
                });
                cell.Children.Add(new Rectangle
                {
                    Width = 2,
                    Height = Math.Max(2d, minute.MouseClicks / (double)maximumClicks * 42d),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Fill = scoreBrush,
                    RadiusX = 1,
                    RadiusY = 1
                });
            }

            if (index == latestSnapshotStartIndex)
            {
                cell.Children.Add(new Rectangle
                {
                    Width = 1,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Fill = scoreBrush,
                    Opacity = 0.6
                });
            }

            ActivityScoreBarHost.Children.Add(cell);
        }
    }

    private string FormatInterval(string intervalName, ActivityScoreInterval interval) =>
        $"{intervalName}: {interval.MouseClicks:N0} {T("Activity.Clicks")} · {interval.KeyPresses:N0} {T("Activity.Keys")}";

    private Brush GetActivityInputBrush() =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SystemAccentColor", out var accent)
        && accent is Color accentColor
            ? new SolidColorBrush(accentColor)
            : (Brush)PlayerPanel.Resources["PlayerAccentTextBrush"];

    /// <summary>Deletes the most recent manual capture while its temporary countdown is active.</summary>
    private async void DeleteSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingSnapshotDeleteInProgress = true;
        SetPlayerSectionVisibility(MainWindowLayoutSection.PendingSnapshot, PendingSnapshotPanel, isVisible: false);
        DeleteSnapshotButton.Visibility = Visibility.Collapsed;
        TakeScreenshotButton.IsEnabled = false;
        DeleteSnapshotButton.IsEnabled = false;
        try
        {
            var result = await _application.DeletePendingManualScreenshotAsync(CancellationToken.None);
            if (!result.Succeeded)
            {
                return;
            }

            var lastSession = await _viewModel.RefreshLastSessionAsync(CancellationToken.None);
            if (lastSession.Succeeded)
            {
                UpdateLastSession(lastSession.Value);
            }
        }
        finally
        {
            _pendingSnapshotDeleteInProgress = false;
            await RefreshDashboardAsync();
        }
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
        UpdateScreenshotCaptureStatus();
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
        _scheduleWindow?.ApplyTheme(_theme);
        _scheduleWindow?.ApplyLanguage(settings.UiLanguage);
        UiLocalization.Apply(RootGrid, _strings);
        var reportsLabel = T("Reports.Title");
        var searchLabel = T("Search.Title");
        AutomationProperties.SetName(TitleBarSearchButton, searchLabel);
        ToolTipService.SetToolTip(TitleBarSearchButton, searchLabel);
        AutomationProperties.SetName(TitleBarReportButton, reportsLabel);
        ToolTipService.SetToolTip(TitleBarReportButton, reportsLabel);
        UpdateActivityScoreAccessibility();
        UpdateDetailsAccessibility();
        UpdateOpenAiMenuAccessibility();
        UpdateScreenshotCaptureStatus();
        OptionsControl.ApplyLanguage(settings.UiLanguage);
        OperationsControl.ApplyLanguage(settings.UiLanguage);
        ResizeForCurrentLayout(animate: false);
        ApplyFlyoutPosition(_position);

        SettingsApplied?.Invoke(settings);
    }

    private void UpdateScreenshotCaptureStatus() =>
        ScreenshotStatusText.Text = T(_screenshotsEnabled ? "Screenshot.Status.On" : "Screenshot.Status.Off");

    private void UpdateDetailsAccessibility()
    {
        var label = T(_layoutState.IsLastSessionVisible ? "LastSession.Hide" : "LastSession.Show");
        AutomationProperties.SetName(DetailsButton, label);
        ToolTipService.SetToolTip(DetailsButton, label);
    }

    private void UpdateActivityScoreAccessibility()
    {
        var label = T(_layoutState.IsActivityScoreVisible ? "Activity.Hide" : "Activity.Show");
        AutomationProperties.SetName(ActivityScoreButton, label);
        ToolTipService.SetToolTip(ActivityScoreButton, label);
    }

    private void AiState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AiApplicationState.CanEnable)
            or nameof(AiApplicationState.Enabled)
            or nameof(AiApplicationState.IsStatusUnavailable))
        {
            UpdateOpenAiMenuAccessibility();
            if (e.PropertyName == nameof(AiApplicationState.Enabled))
            {
                _nextAiSpendRefreshAt = DateTimeOffset.MinValue;
                if (!AiState.Enabled)
                {
                    AiMonthlySpendPanel.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    /// <summary>Shows the queued topmost connection-test dialog requested by the passive options surface.</summary>
    private async void OptionsControl_AiConnectionTestRequested(object? sender, EventArgs e)
    {
        await _dialogs.ShowAiConnectionTestAsync(_application, this, RootGrid.RequestedTheme);
        _nextAiSpendRefreshAt = DateTimeOffset.MinValue;
        await RefreshAiMonthlySpendAsync();
    }

    private void UpdateOpenAiMenuAccessibility()
    {
        AutomationProperties.SetName(OpenAiMenuToggle, T("Options.OpenAi.Header"));
        AutomationProperties.SetHelpText(
            OpenAiMenuToggle,
            AiState.IsStatusUnavailable
                ? T("Options.ApiKeyStatus.Unavailable")
                : !AiState.CanEnable && !AiState.Enabled
                    ? T("Options.OpenAi.KeyRequired")
                    : string.Empty);
        AutomationProperties.SetName(AiPricingMenuItem, T("AiPricing.MenuTitle"));
        AutomationProperties.SetHelpText(
            AiPricingMenuItem,
            AiState.Enabled
                ? string.Empty
                : T("AiPricing.DisabledHint"));
    }

    private void UpdateActiveHoursAvailability(bool isWithinActiveHours)
    {
        var warningVisible = !isWithinActiveHours;
        OutsideActiveHoursMessageRun.Text = T("Schedule.OutsideActiveHoursBanner");
        OutsideActiveHoursLinkRun.Text = T("Schedule.ConfigureHours");
        AutomationProperties.SetName(OutsideActiveHoursBanner, $"{OutsideActiveHoursMessageRun.Text} {OutsideActiveHoursLinkRun.Text}");
        SetPlayerSectionVisibility(MainWindowLayoutSection.OutsideActiveHoursWarning, OutsideActiveHoursBanner, warningVisible);
    }

    private string T(string key) => _strings.Translate(key);

    private string FormatNotificationMessage(ApplicationNotification notification)
    {
        var message = T(notification.MessageKey);
        return string.IsNullOrWhiteSpace(notification.Detail)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}{notification.Detail}";
    }

    /// <summary>Shows and positions the player when requested from the taskbar control.</summary>
    public void ShowFlyout()
    {
        ApplyFlyoutPosition(_position);
        Activate();
    }

    /// <summary>Starts the Windows-sign-in instance in the notification area without first creating a taskbar button.</summary>
    internal void StartMinimizedToNotificationArea() => HideToNotificationArea();

    private void HideToNotificationArea()
    {
        _trayIcon.HideToNotificationArea(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "TrackMeUpIcon.ico"),
            "TrackMeUp",
            new TrayIconMenuLabels(
                T("Tray.ShowMainWindow"),
                T("Tray.HideMainWindow"),
                T("Tray.CloseApplication")));
    }

    /// <summary>Forwards the native context-menu exit request to the application composition root after the current window callback completes.</summary>
    private void TrayIcon_ExitRequested(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>Starts the player entrance fade.</summary>
    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _rootLoaded.TrySetResult();
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        ResizeForCurrentLayout(animate: false);
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

        ResizeForCurrentLayout(animate: false);
        ApplyFlyoutPosition(_position);
    }

    /// <summary>Reapplies the smart height limit when the flyout crosses onto another display.</summary>
    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPositionChange)
        {
            return;
        }

        var workArea = CurrentWorkArea();
        if (workArea.Equals(_currentWorkArea))
        {
            return;
        }

        _currentWorkArea = workArea;
        ResizeForCurrentLayout(animate: false);
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
        _appWindow.Resize(GetPhysicalWindowSize(logicalHeight));
    }

    /// <summary>Interpolates the compact player height after a visible layout change.</summary>
    private void AnimateResizeForLogicalContent(int logicalHeight)
    {
        _windowResizeAnimationStartSize = _appWindow.Size;
        _windowResizeAnimationTargetSize = GetPhysicalWindowSize(logicalHeight);
        if (_windowResizeAnimationStartSize.Height == _windowResizeAnimationTargetSize.Height)
        {
            _appWindow.Resize(_windowResizeAnimationTargetSize);
            ApplyFlyoutPosition(_position);
            return;
        }

        _windowResizeAnimationStartedAt = DateTimeOffset.UtcNow;
        _windowResizeAnimationTimer.Start();
    }

    /// <summary>Advances the AppWindow height animation while preserving the chosen flyout anchor.</summary>
    private void WindowResizeAnimationTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        var elapsedMilliseconds = (DateTimeOffset.UtcNow - _windowResizeAnimationStartedAt).TotalMilliseconds;
        var progress = Math.Clamp(elapsedMilliseconds / WindowResizeAnimationDurationMilliseconds, 0d, 1d);
        var easedProgress = 1d - Math.Pow(1d - progress, 3d);
        var height = (int)Math.Round(
            _windowResizeAnimationStartSize.Height
            + ((_windowResizeAnimationTargetSize.Height - _windowResizeAnimationStartSize.Height) * easedProgress));
        _appWindow.Resize(new SizeInt32(_windowResizeAnimationTargetSize.Width, height));
        ApplyFlyoutPosition(_position);

        if (progress >= 1d)
        {
            _windowResizeAnimationTimer.Stop();
        }
    }

    /// <summary>Calculates the physical AppWindow size for one logical content height.</summary>
    private SizeInt32 GetPhysicalWindowSize(int logicalHeight)
    {
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? _rasterizationScale);
        _rasterizationScale = scale;

        var workArea = CurrentWorkArea();
        _currentWorkArea = workArea;
        var physicalMargin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var availableWidth = Math.Max(1, workArea.Width - (physicalMargin * 2));
        var availableHeight = Math.Max(1, workArea.Height - (physicalMargin * 2));
        var boundedLogicalHeight = Math.Min(logicalHeight, _layoutState.ResolveLogicalHeight(availableHeight / scale));
        var physicalWidth = Math.Min(availableWidth, (int)Math.Ceiling(LogicalWindowWidth * scale));
        var physicalHeight = Math.Min(availableHeight, (int)Math.Ceiling(boundedLogicalHeight * scale));
        return new SizeInt32(physicalWidth, physicalHeight);
    }

    /// <summary>Places the player at the selected visual anchor.</summary>
    private void ApplyFlyoutPosition(string position)
    {
        var area = CurrentWorkArea();
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

    private RectInt32 CurrentWorkArea() =>
        DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr window,
        int attribute,
        ref uint attributeValue,
        int attributeSize);

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        _refreshTimer.Stop();
        _windowResizeAnimationTimer.Stop();
        _dialogs.CloseActive();
        _appWindow.Changed -= AppWindow_Changed;
        _trayIcon.ExitRequested -= TrayIcon_ExitRequested;
        _trayIcon.Dispose();

        if (_scheduleWindow is not null)
        {
            _scheduleWindow.Close();
            _scheduleWindow = null;
        }
    }
}

/// <summary>Identifies the retained capture that the screenshot inspector should select.</summary>
public sealed class ScreenshotPreviewRequestedEventArgs(string screenshotPath, DateTimeOffset capturedAt) : EventArgs
{
    public string ScreenshotPath { get; } = screenshotPath;

    public DateTimeOffset CapturedAt { get; } = capturedAt;
}
