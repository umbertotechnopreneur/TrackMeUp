// SPDX-License-Identifier: MIT

#region Using directives
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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

    private const int LogicalWindowWidth = 576;
    private const int LogicalExpandedWindowWidth = 760;
    private const int LogicalWindowHeightPadding = 20;
    private const int LogicalScreenMargin = 22;
    private const int WindowResizeAnimationDurationMilliseconds = 180;
    private const int ScreenshotPreviewDecodePixelWidth = 384;
    private static readonly TimeSpan LastSessionRefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AiSpendRefreshInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AiSpendFailureRetryInterval = TimeSpan.FromMinutes(5);
    private readonly ITrackMeUpApplication _application;
    private readonly ScreenshotBitmapSourceLoader _screenshotBitmapLoader;
    private readonly MainViewModel _viewModel;
    private readonly DashboardRefreshCoordinator _dashboardRefreshCoordinator;
    private readonly WindowSurfaceLifecycle _lifecycle = new();
    private readonly DispatcherQueueTimer _windowResizeAnimationTimer;
    private readonly AppWindow _appWindow;
    private readonly CustomTitleBarController _titleBar;
    private readonly WindowPlacementService _placement;
    private readonly MainWindowLayoutState _layoutState = new();
    private readonly MicaDialogService _dialogs;
    private readonly TrayIconService _trayIcon;
    private readonly IWindowsToastNotificationService _windowsNotifications;
    private RectInt32 _currentWorkArea;
    private LocalizationService _strings = new("system");
    private bool _updatingMenuState;
    private double _rasterizationScale = 1d;
    private DateTimeOffset _windowResizeAnimationStartedAt;
    private SizeInt32 _windowResizeAnimationStartSize;
    private SizeInt32 _windowResizeAnimationTargetSize;
    private string _theme = "system";
    private string _position = FlyoutPositions.BottomCenter;
    private bool _hasAppliedSettings;
    private bool _mainPlacementRestored;
    private bool _windowSizingReady;
    private SizeInt32 _requestedWindowSize;
    private AppSettings? _menuSettings;
    private AboutWindow? _aboutWindow;
    private ScheduleWindow? _scheduleWindow;
    private SearchIndexingWindow? _searchIndexingWindow;
    private XamlRoot? _xamlRoot;
    private string? _latestScreenshotPath;
    private DateTimeOffset? _latestScreenshotCapturedAt;
    private CancellationTokenSource? _latestScreenshotLoadCancellation;
    private int _latestScreenshotLoadGeneration;
    private bool _screenshotsEnabled;
    private bool _isTracking;
    private const int PendingSnapshotDeleteSeconds = 30;
    private bool _pendingSnapshotDeleteInProgress;
    private int _lastSessionRefreshInProgress;
    private DateTimeOffset _nextLastSessionRefreshAt = DateTimeOffset.MinValue;
    private bool _startupAiWarningShown;
    private bool _screenshotStorageReady;
    private int _notificationDrainInProgress;
    private DateTimeOffset _nextAiSpendRefreshAt = DateTimeOffset.MinValue;
    private int _aiSpendRefreshInProgress;
    private bool _showAiMonthlySpend;
    private IDisposable? _dashboardSubscription;
    private bool _dashboardRefreshReady;
    private bool _dashboardSurfaceClosed;
    private bool _allowClose;
    private bool _closeConfirmationInProgress;
    private OptionsControl? _optionsControl;
    private OperationsControl? _operationsControl;
    private Task? _optionsInitializationTask;
    private Task? _operationsInitializationTask;

    private OptionsControl OptionsControl => _optionsControl
        ?? throw new InvalidOperationException("OptionsControl has not been initialized.");

    private OperationsControl OperationsControl => _operationsControl
        ?? throw new InvalidOperationException("OperationsControl has not been initialized.");
    private MainWindowSurface _operationsReturnSurface = MainWindowSurface.Player;
    #endregion

    /// <summary>Gets the single observable AI state shared by the player menu and options surface.</summary>
    public AiApplicationState AiState { get; }

    #region Events

    /// <summary>Occurs when a fully persisted settings snapshot has been applied to the player surface.</summary>
    public event Action<AppSettings>? SettingsApplied;

    /// <summary>Occurs when the user requests the dedicated reports surface.</summary>
    public event EventHandler? ReportsRequested;

    /// <summary>Occurs when the user requests the independent world-clock window.</summary>
    public event EventHandler? WorldClocksRequested;

    /// <summary>Occurs when the user requests the floating local-search surface.</summary>
    public event EventHandler? SearchRequested;

    /// <summary>Occurs when the user requests the reusable Quick Setup surface.</summary>
    public event EventHandler? QuickSetupRequested;

    /// <summary>Occurs when the user requests the retained screenshot gallery surface.</summary>
    public event EventHandler? ScreenshotGalleryRequested;

    /// <summary>Occurs when the user requests the retained screenshot gallery for a specific day.</summary>
    public event EventHandler<ScreenshotGalleryDateRequestedEventArgs>? ScreenshotGalleryDateRequested;

    /// <summary>Occurs when the user requests the retained screenshot gallery.</summary>
    public event EventHandler<ScreenshotPreviewRequestedEventArgs>? ScreenshotsRequested;

    /// <summary>Occurs when the notification-area context menu requests an orderly application exit.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Occurs after the user confirms and the runtime prepares a complete local-data reset.</summary>
    internal event EventHandler<AtomicResetPreparedEventArgs>? AtomicResetPrepared;

    #endregion

    #region Initialization

    /// <summary>Creates the player view with the shared application facade supplied by the composition root.</summary>
    internal MainWindow(
        ITrackMeUpApplication application,
        LaunchOptions options,
        MicaDialogService dialogs,
        TrayIconService trayIcon,
        IWindowsToastNotificationService windowsNotifications,
        DashboardRefreshCoordinator dashboardRefreshCoordinator)
    {
        _application = application;
        _screenshotBitmapLoader = new ScreenshotBitmapSourceLoader(application);
        _dashboardRefreshCoordinator = dashboardRefreshCoordinator ?? throw new ArgumentNullException(nameof(dashboardRefreshCoordinator));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
        _windowsNotifications = windowsNotifications ?? throw new ArgumentNullException(nameof(windowsNotifications));
        _viewModel = new MainViewModel(application);
        AiState = new AiApplicationState(application);
        InitializeComponent();
        SetScreenshotStorageReady(false);
        UiLocalization.Apply(RootGrid, _strings);
        ApplyMainAccessibility();
        AiState.PropertyChanged += AiState_PropertyChanged;
        _trayIcon.ExitRequested += TrayIcon_ExitRequested;
        UpdateOpenAiMenuAccessibility();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _titleBar = new CustomTitleBarController(
            this,
            _appWindow,
            RootGrid,
            DragRegion,
            TitleBarLeftInsetColumn,
            TitleBarRightInsetColumn,
            () =>
            [
                TitleBarBackButton,
                WorldClockButton,
                TitleBarMoreButton,
                TitleBarSearchButton,
                TitleBarReportButton,
                TitleBarMinimizeToTrayButton,
                TitleBarCloseButton
            ]);
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.Main,
            LogicalWindowWidth,
            _layoutState.LogicalHeight,
            LogicalScreenMargin);
        _currentWorkArea = CurrentWorkArea();
        _appWindow.Changed += AppWindow_Changed;
        _appWindow.Closing += AppWindow_Closing;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        }
        ApplyBorderlessPlayerWindow();
        ResizeForLogicalContent(_layoutState.LogicalHeight);

        _windowResizeAnimationTimer = DispatcherQueue.CreateTimer();
        _windowResizeAnimationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _windowResizeAnimationTimer.Tick += WindowResizeAnimationTimer_Tick;

        _lifecycle.InitializationFailed += Lifecycle_InitializationFailed;
        _lifecycle.StartInitialization(cancellationToken => InitializeAsync(options, cancellationToken));
        Closed += MainWindow_Closed;
    }

    private void ApplyBorderlessPlayerWindow()
    {
        WindowInteropService.ApplyPlayerWindowChrome(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }

    private async Task InitializeAsync(LaunchOptions options, CancellationToken cancellationToken)
    {
        await _lifecycle.WaitUntilLoadedAsync(cancellationToken);
        var startupRegistrationFailureCode = await ReconcileWindowsStartupAsync(options, cancellationToken);
        if (!await EnsureScreenshotStorageMigratedAsync(cancellationToken))
        {
            // Tracking and periodic refresh stay stopped until the explicit storage migration succeeds.
            return;
        }

        var initialization = await _viewModel.InitializeAsync(options, cancellationToken);
        if (initialization.Succeeded && initialization.Value is not null)
        {
            ApplySettings(initialization.Value.Settings);
            UpdatePlayer(initialization.Value.Dashboard);
            UpdateLastSession(initialization.Value.LastSession);
        }
        else
        {
            // If launch initialization fails, keep the player usable and let the normal dashboard read expose runtime state.
            await RefreshDashboardAsync(cancellationToken);
        }

        SetScreenshotStorageReady(true);
        _dashboardRefreshReady = true;
        UpdateDashboardSubscriptionForVisibility();
        if (startupRegistrationFailureCode is not null)
        {
            await _dialogs.ShowInformativeAsync(
                this,
                DialogRequest.Informative(
                    T("Notification.WindowsStartupFailed.Title"),
                    $"{T("Notification.WindowsStartupFailed.Message")}{Environment.NewLine}{Environment.NewLine}{startupRegistrationFailureCode}",
                    T("Dialog.Ok")));
        }

        if (initialization.Succeeded && initialization.Value?.StartedPaused == true)
        {
            _windowsNotifications.TryShow(T("Notification.TrackingPaused.Title"), T("Notification.TrackingPaused.Message"));
        }

        await ShowStartupAiWarningAsync(cancellationToken);
        await RefreshAiMonthlySpendAsync();
        await DrainApplicationNotificationsAsync(cancellationToken);
    }

    private void Lifecycle_InitializationFailed(Exception exception)
    {
        if (_lifecycle.IsCancellationRequested)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"Main-window initialization failed: {exception}");
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            SetScreenshotStorageReady(false);
            _ = ShowLazySurfaceFailureAsync();
        });
    }

    private async Task<string?> ReconcileWindowsStartupAsync(
        LaunchOptions options,
        CancellationToken cancellationToken)
    {
        var settingsResult = await _application.GetSettingsAsync(cancellationToken);
        if (!settingsResult.Succeeded || settingsResult.Value is null)
        {
            return null;
        }

        var effectiveLanguage = options.Language ?? settingsResult.Value.UiLanguage;
        var effectiveTheme = options.Theme ?? settingsResult.Value.Theme;
        _strings = new LocalizationService(effectiveLanguage);
        RootGrid.RequestedTheme = effectiveTheme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        _titleBar.ApplyTheme(RootGrid.RequestedTheme == ElementTheme.Default ? RootGrid.ActualTheme : RootGrid.RequestedTheme);

        // Reapplying the persisted choice repairs stale paths and removes stale disabled registrations.
        var startupResult = await _application.SetStartupEnabledAsync(
            settingsResult.Value.StartWithWindows,
            cancellationToken);
        return startupResult.Succeeded ? null : startupResult.Code;
    }

    private async Task<bool> EnsureScreenshotStorageMigratedAsync(CancellationToken cancellationToken)
    {
        var status = await _application.GetScreenshotStorageMigrationStatusAsync(cancellationToken);
        if (!status.Succeeded || status.Value is null)
        {
            await ShowScreenshotStorageMigrationFailureAsync(status.Code);
            return false;
        }

        OperationResult<ScreenshotStorageMigrationResult> migration;
        if (status.Value.Required)
        {
            migration = await _dialogs.ShowScreenshotStorageMigrationAsync(
                _application,
                this,
                RootGrid.RequestedTheme,
                _strings);
            cancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            // An idempotent silent run repairs metadata after a process interruption even when every file was already moved.
            migration = await _application.MigrateScreenshotStorageAsync(cancellationToken);
        }

        if (migration.Succeeded)
        {
            return true;
        }

        await ShowScreenshotStorageMigrationFailureAsync(migration.Code);
        return false;
    }

    private async Task ShowScreenshotStorageMigrationFailureAsync(string code)
    {
        await _dialogs.ShowInformativeAsync(
            this,
            DialogRequest.Informative(
                T("Dialog.DataMigration.Failed.Title"),
                _strings.Format("Dialog.DataMigration.Failed.Message", code),
                T("Dialog.Ok")));
    }

    private void SetScreenshotStorageReady(bool isReady)
    {
        _screenshotStorageReady = isReady;
        TrackingButton.IsEnabled = isReady;
        MoreButton.IsEnabled = isReady;
        TitleBarMoreButton.IsEnabled = isReady;
        TitleBarSearchButton.IsEnabled = isReady;
        TitleBarReportButton.IsEnabled = isReady;
        ScreenshotPreviewButton.IsEnabled = isReady;
        CaptureMenu.IsEnabled = isReady;
        OperationsMenuItem.IsEnabled = isReady;
        TakeScreenshotButton.IsEnabled = isReady
            && !_pendingSnapshotDeleteInProgress
            && DeleteSnapshotButton.Visibility != Visibility.Visible;
        DeleteSnapshotButton.IsEnabled = isReady && DeleteSnapshotButton.Visibility == Visibility.Visible;
    }

    #endregion

    private async Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (_dashboardSubscription is not null)
        {
            _dashboardRefreshCoordinator.RequestRefresh();
            return;
        }

        var state = await _viewModel.RefreshAsync(cancellationToken);
        if (state.Succeeded && state.Value is not null)
        {
            UpdatePlayer(state.Value);
        }

        await RefreshLastSessionIfDueAsync();

        await RefreshAiMonthlySpendAsync();
        await DrainApplicationNotificationsAsync(cancellationToken);
    }

    private void OnDashboardStateChanged(DashboardState state)
    {
        if (_dashboardSurfaceClosed)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (_dashboardSurfaceClosed)
            {
                return;
            }

            UpdatePlayer(state);
            _ = RefreshLastSessionIfDueAsync();
            _ = RefreshAiMonthlySpendAsync();
            _ = DrainApplicationNotificationsAsync();
        });
    }

    private void UpdateDashboardSubscriptionForVisibility()
    {
        var shouldSubscribe = _dashboardRefreshReady
            && !_dashboardSurfaceClosed
            && _appWindow.IsVisible;
        if (shouldSubscribe && _dashboardSubscription is null)
        {
            _dashboardSubscription = _dashboardRefreshCoordinator.Subscribe(OnDashboardStateChanged);
        }
        else if (!shouldSubscribe && _dashboardSubscription is not null)
        {
            _dashboardSubscription.Dispose();
            _dashboardSubscription = null;
        }
    }

    /// <summary>Refreshes the month-to-date AI spend at a bounded cadence while the integration is active.</summary>
    private async Task RefreshAiMonthlySpendAsync()
    {
        if (!_showAiMonthlySpend || !AiState.Enabled)
        {
            AiMonthlySpendPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (_lifecycle.IsCancellationRequested ||
            DateTimeOffset.UtcNow < _nextAiSpendRefreshAt ||
            Interlocked.Exchange(ref _aiSpendRefreshInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            // Reserve a bounded failure retry before issuing the call so provider/runtime outages do
            // not turn the one-second dashboard cadence into a request loop.
            _nextAiSpendRefreshAt = DateTimeOffset.UtcNow.Add(AiSpendFailureRetryInterval);
            var result = await _application.GetAiPricingOverviewAsync(_lifecycle.Token);
            if (result.Succeeded && result.Value is not null)
            {
                UpdateAiMonthlySpend(result.Value);
                _nextAiSpendRefreshAt = DateTimeOffset.UtcNow.Add(AiSpendRefreshInterval);
            }
        }
        catch (OperationCanceledException) when (_lifecycle.IsCancellationRequested)
        {
            // The window lifetime owns this refresh; closing it cancels pending IPC/aggregation.
        }
        finally
        {
            Interlocked.Exchange(ref _aiSpendRefreshInProgress, 0);
        }
    }

    /// <summary>Renders the current local calendar-month AI spend using actual provider cost when available.</summary>
    private void UpdateAiMonthlySpend(AiPricingOverview overview)
    {
        if (!_showAiMonthlySpend || !AiState.Enabled)
        {
            AiMonthlySpendPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var cost = overview.ActualCostCurrentMonthUsd ?? overview.EstimatedCostCurrentMonthUsd;
        if (cost is null)
        {
            AiMonthlySpendPanel.Visibility = Visibility.Collapsed;
            return;
        }

        AiMonthlySpendText.Text = _strings.Format("AiPricing.UsdShort", cost.Value);
        AiMonthlySpendRangeText.Text = _strings.Format(
            "AiPricing.DateRange",
            overview.CurrentMonthStart,
            overview.CurrentMonthEnd);
        AiMonthlySpendPanel.Visibility = Visibility.Visible;
    }

    private async Task ShowStartupAiWarningAsync(CancellationToken cancellationToken)
    {
        if (_startupAiWarningShown)
        {
            return;
        }

        var status = await AiState.LoadAsync(cancellationToken);
        if (status is not { Succeeded: true, Value: { Enabled: true, HasKey: false } aiStatus })
        {
            return;
        }

        _startupAiWarningShown = true;
        await _dialogs.ShowInformativeAsync(
            this,
            DialogRequest.Informative(
                T("Dialog.AiKeyMissing.Title"),
                _strings.Format("Dialog.AiKeyMissing.Message", aiStatus.KeyVariable),
                T("Dialog.Ok")));
    }

    private async Task DrainApplicationNotificationsAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _notificationDrainInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            var result = await _application.DrainApplicationNotificationsAsync(cancellationToken);
            if (!result.Succeeded || result.Value is null)
            {
                return;
            }

            foreach (var notification in result.Value)
            {
                if (IsWindowsToastNotification(notification))
                {
                    _windowsNotifications.TryShow(
                        T(notification.TitleKey),
                        FormatNotificationMessage(notification));
                    continue;
                }

                if (IsFrameAnalysisNotification(notification))
                {
                    ShowNotificationBanner(notification);
                    continue;
                }

                await _dialogs.ShowInformativeAsync(
                    this,
                    DialogRequest.Informative(
                        T(notification.TitleKey),
                        FormatNotificationMessage(notification),
                        T("Dialog.Ok")));
            }
        }
        finally
        {
            Interlocked.Exchange(ref _notificationDrainInProgress, 0);
        }
    }

    private static bool IsFrameAnalysisNotification(ApplicationNotification notification) =>
        notification.TitleKey is
            "Notification.AiAnalysisFailed.Title" or
            "Notification.AiDailyLimitReached.Title";

    private static bool IsWindowsToastNotification(ApplicationNotification notification) =>
        notification.TitleKey is
            "Notification.ScreenshotCaptureFailed.Title" or
            "Notification.ScreenshotStorageLow.Title" or
            "Notification.TrackingUnavailable.Title";

    private void ShowNotificationBanner(ApplicationNotification notification)
    {
        var title = T(notification.TitleKey);
        var message = FormatNotificationMessage(notification);
        switch (notification.Severity)
        {
            case ApplicationNotificationSeverity.Error:
                _dialogs.Notifications.ShowError(MainNotificationBanner, title, message);
                break;
            case ApplicationNotificationSeverity.Warning:
                _dialogs.Notifications.ShowWarning(MainNotificationBanner, title, message);
                break;
            default:
                _dialogs.Notifications.ShowInfo(MainNotificationBanner, title, message);
                break;
        }
    }

    /// <summary>Shows a localized warning when the independent world-clock surface cannot open.</summary>
    internal void ShowWorldClockOpenFailure() =>
        _dialogs.Notifications.ShowWarning(
            MainNotificationBanner,
            T("WorldClock.ErrorTitle"),
            T("WorldClock.OpenFailed"));

    /// <summary>Captures a screenshot manually when the user clicks the "Take snapshot" button.</summary>
    private async void TakeScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_screenshotStorageReady || !TakeScreenshotButton.IsEnabled)
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
        await RefreshLastSessionIfDueAsync(force: true);
    }

    /// <summary>Opens the detached screenshot scheduling window.</summary>
    private async void ScheduleScreenshotMenuItem_Click(object sender, RoutedEventArgs e)
        => await OpenScheduleWindowAsync();

    private async void OutsideActiveHoursSettingsLink_Click(object sender, RoutedEventArgs e)
        => await OpenScheduleWindowAsync();

    private async Task OpenScheduleWindowAsync()
    {
        if (!_screenshotStorageReady)
        {
            return;
        }

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
        var activeHoursByDay = eventArgs.ActiveHours.ToDictionary(day => day.Day, StringComparer.Ordinal);
        var patchValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var day in ActiveHoursSchedule.Days)
        {
            var configuredDay = activeHoursByDay[day];
            patchValues.Add($"active_hours.{day}.active", configuredDay.ActivePeriod);
            patchValues.Add($"active_hours.{day}.breaks", configuredDay.BreakPeriods);
        }

        patchValues.Add("screenshots.interval_minutes", eventArgs.IntervalMinutes.ToString(CultureInfo.InvariantCulture));
        var patch = new SettingsPatch(patchValues);
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

    private void TitleBarCloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Forwards the play/pause action to the player view model.</summary>
    private async void TrackingButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_screenshotStorageReady)
        {
            return;
        }

        var state = await _viewModel.ToggleTrackingAsync(CancellationToken.None);
        if (state.Succeeded && state.Value is not null)
        {
            UpdatePlayer(state.Value);
            return;
        }

        // Tracking failures are reported through the non-blocking Windows toast queue.
        await DrainApplicationNotificationsAsync();
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
        _ = ShowOptionsPanelAsync();
    }

    private void ShowOptionsPanel()
    {
        _ = ShowOptionsPanelAsync();
    }

    private async Task ShowOptionsPanelAsync()
    {
        if (!await TryEnsureOptionsAsync())
        {
            return;
        }

        ShowPanel(OptionsPanel, MainWindowSurface.Options);
    }

    private async Task<bool> TryEnsureOptionsAsync()
    {
        try
        {
            await EnsureOptionsAsync();
            return true;
        }
        catch (OperationCanceledException) when (_lifecycle.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            await ShowLazySurfaceFailureAsync();
            return false;
        }
    }

    private Task EnsureOptionsAsync()
    {
        if (_optionsInitializationTask is { } existing)
        {
            return existing;
        }

        var options = new OptionsControl();
        _optionsControl = options;
        OptionsHost.Content = options;
        options.BackRequested += OptionsControl_BackRequested;
        options.SettingsSaved += ApplySettings;
        options.LayoutChanged += OptionsControl_LayoutChanged;
        options.AiConnectionTestRequested += OptionsControl_AiConnectionTestRequested;
        options.OperationsSectionRequested += OptionsControl_OperationsSectionRequested;
        options.SearchIndexingRequested += OptionsControl_SearchIndexingRequested;
        var initialization = options.InitializeAsync(_application, AiState, _lifecycle.Token);
        _optionsInitializationTask = CompleteOptionsInitializationAsync(options, initialization);
        return _optionsInitializationTask;
    }

    private async Task CompleteOptionsInitializationAsync(OptionsControl options, Task initialization)
    {
        try
        {
            await initialization;
        }
        catch
        {
            if (ReferenceEquals(_optionsControl, options))
            {
                options.BackRequested -= OptionsControl_BackRequested;
                options.SettingsSaved -= ApplySettings;
                options.LayoutChanged -= OptionsControl_LayoutChanged;
                options.AiConnectionTestRequested -= OptionsControl_AiConnectionTestRequested;
                options.OperationsSectionRequested -= OptionsControl_OperationsSectionRequested;
                options.SearchIndexingRequested -= OptionsControl_SearchIndexingRequested;
                OptionsHost.Content = null;
                _optionsControl = null;
                _optionsInitializationTask = null;
            }

            throw;
        }
    }

    private Task EnsureOperationsAsync()
    {
        if (_operationsInitializationTask is { } existing)
        {
            return existing;
        }

        var operations = new OperationsControl();
        _operationsControl = operations;
        OperationsHost.Content = operations;
        operations.BackRequested += OperationsControl_BackRequested;
        operations.LayoutChanged += OperationsControl_LayoutChanged;
        operations.AtomicResetPrepared += OperationsControl_AtomicResetPrepared;
        operations.Initialize(_application, _dialogs, this, MainNotificationBanner);
        operations.ApplyLanguage(_strings.Language);
        _operationsInitializationTask = Task.CompletedTask;
        return _operationsInitializationTask;
    }

    private Task ShowLazySurfaceFailureAsync() => _dialogs.ShowInformativeAsync(
        this,
        DialogRequest.Informative(
            T("Operations.Status.RuntimeUnavailable.Title"),
            T("Operations.Status.RuntimeUnavailable.Message"),
            T("Dialog.Ok")));

    /// <summary>Forwards Quick Setup activation to the application composition root.</summary>
    private void QuickSetupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        QuickSetupRequested?.Invoke(this, EventArgs.Empty);
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

    /// <summary>Forwards world-clock activation to the application composition root.</summary>
    private void WorldClockButton_Click(object sender, RoutedEventArgs e) =>
        WorldClocksRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Shows the native aggregate activity calendar through the shared dialog coordinator.</summary>
    private async void ActivityCalendarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        var selectedDate = await _dialogs.ShowActivityCalendarAsync(_application, this, RootGrid.RequestedTheme, _strings);
        if (selectedDate is { } date)
        {
            ScreenshotGalleryDateRequested?.Invoke(this, new ScreenshotGalleryDateRequestedEventArgs(date));
        }
    }

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
        if (!_screenshotStorageReady)
        {
            return;
        }

        switch (sender.Key)
        {
            case Windows.System.VirtualKey.F3:
            case Windows.System.VirtualKey.P:
                RequestSearch();
                break;
            case Windows.System.VirtualKey.R:
                RequestReports();
                break;
            case Windows.System.VirtualKey.G:
                RequestScreenshotGallery();
                break;
            case Windows.System.VirtualKey.S:
                _ = OpenScheduleWindowAsync();
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
    private async void OperationsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        _operationsReturnSurface = MainWindowSurface.Player;
        if (!await TryEnsureOperationsAsync())
        {
            return;
        }

        OperationsControl.ShowOverview();
        ShowPanel(OperationsPanel, MainWindowSurface.Operations);
    }

    private async Task<bool> TryEnsureOperationsAsync()
    {
        try
        {
            await EnsureOperationsAsync();
            return true;
        }
        catch (OperationCanceledException) when (_lifecycle.IsCancellationRequested)
        {
            return false;
        }
        catch
        {
            await ShowLazySurfaceFailureAsync();
            return false;
        }
    }

    /// <summary>Hides the player from the taskbar while retaining its notification-area activation icon.</summary>
    private async void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        MoreButton.Flyout.Hide();
        try
        {
            HideToNotificationArea();
        }
        catch (Exception)
        {
            await _dialogs.ShowInformativeAsync(
                this,
                DialogRequest.Informative(
                    T("Tray.UnavailableTitle"),
                    T("Tray.UnavailableMessage"),
                    T("Dialog.Ok")));
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
                this,
                DialogRequest.Informative(
                    T("AiPricing.UnavailableTitle"),
                    T("AiPricing.UnavailableMessage"),
                    T("Dialog.Ok")));
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
        if (_aboutWindow is null)
        {
            _aboutWindow = new AboutWindow(
                _application,
                _theme,
                _strings.RequestedLanguage,
                _appWindow,
                WinRT.Interop.WindowNative.GetWindowHandle(this));
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        }

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
        if (!_screenshotStorageReady || _updatingMenuState)
        {
            return;
        }

        var requestedValue = ScreenshotsMenuToggle.IsChecked;
        var result = await _application.PatchSettingsAsync(
            new SettingsPatch(new Dictionary<string, string?>
            {
                ["screenshots.enabled"] = requestedValue ? "true" : "false"
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
        ActivityCalendarMenuItem.Text = T("ActivityCalendar.MenuTitle");
        ScreenshotsMenuItem.Text = T("Screenshots.Caption");
        ScheduleMenuItem.Text = T("Schedule.Snapshots");
        ScreenshotsMenuToggle.Text = T("MenuToggleScreenshot");
        QuickSetupMenuItem.Text = T("QuickSetup.MenuTitle");
        OptionsMenuItem.Text = T("MenuTitleOptions");
        OperationsMenuItem.Text = T("Main.Menu.Operations");
        OpenAiMenuToggle.Text = T("MenuToggleOpenAi");
        AiPricingMenuItem.Text = T("AiPricing.MenuTitle");
        MinimizeToTrayMenuItem.Text = T("Main.Menu.MinimizeToTray");
        AboutMenuItem.Text = T("MenuTitleAbout");

        ApplyMenuAccessibility(ActivityMenu, "Main.Menu.Activity", "Main.Menu.Activity.Tooltip");
        ApplyMenuAccessibility(SearchMenuItem, "Search.Title", "Main.Menu.Search.Tooltip");
        ApplyMenuAccessibility(ReportsMenuItem, "Reports.Title", "Main.Menu.Reports.Tooltip");
        ApplyMenuAccessibility(ActivityCalendarMenuItem, "ActivityCalendar.MenuTitle", "Main.Menu.ActivityCalendar.Tooltip");
        ApplyMenuAccessibility(ScreenshotsMenuItem, "Screenshots.Caption", "Main.Menu.Screenshots.Tooltip");
        ApplyMenuAccessibility(CaptureMenu, "Main.Menu.Capture", "Main.Menu.Capture.Tooltip");
        ApplyMenuAccessibility(ScheduleMenuItem, "Schedule.Snapshots", "Main.Menu.Schedule.Tooltip");
        ApplyMenuAccessibility(ScreenshotsMenuToggle, "MenuToggleScreenshot", "Main.Menu.ScreenshotToggle.Tooltip");
        ApplyMenuAccessibility(SettingsMenu, "Main.Menu.Settings", "Main.Menu.Settings.Tooltip");
        ApplyMenuAccessibility(QuickSetupMenuItem, "QuickSetup.MenuTitle", "Main.Menu.QuickSetup.Tooltip");
        ApplyMenuAccessibility(OptionsMenuItem, "MenuTitleOptions", "Main.Menu.Options.Tooltip");
        ApplyMenuAccessibility(OperationsMenuItem, "Main.Menu.Operations", "Main.Menu.Operations.Tooltip");
        ApplyMenuAccessibility(AiProviderMenu, "Main.Menu.AiProvider", "Main.Menu.AiProvider.Tooltip");
        ApplyMenuAccessibility(OpenAiMenuToggle, "MenuToggleOpenAi", "Main.Menu.AiToggle.Tooltip");
        ApplyMenuAccessibility(AiPricingMenuItem, "AiPricing.MenuTitle", "Main.Menu.AiPricing.Tooltip");
        ApplyMenuAccessibility(MinimizeToTrayMenuItem, "Main.Menu.MinimizeToTray", "Main.Menu.MinimizeToTray");
        ApplyMenuAccessibility(AboutMenuItem, "MenuTitleAbout", "Main.Menu.About.Tooltip");
    }

    private void ApplyMenuAccessibility(DependencyObject item, string labelKey, string tooltipKey)
    {
        var label = T(labelKey);
        var tooltip = T(tooltipKey);
        AutomationProperties.SetName(item, label);
        AutomationProperties.SetHelpText(item, tooltip);
        ToolTipService.SetToolTip(item, tooltip);
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
            await RefreshLastSessionIfDueAsync(force: true);
            FadeIn(DetailsPanel);
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
    private void OptionsControl_BackRequested(object? sender, EventArgs e) => ShowPlayer();

    /// <summary>Opens one operational detail requested from the settings overview.</summary>
    private void OptionsControl_OperationsSectionRequested(OperationsSection section) => _ = ShowOperationsSectionAsync(section);

    private async Task ShowOperationsSectionAsync(OperationsSection section)
    {
        if (!await TryEnsureOperationsAsync())
        {
            return;
        }

        _operationsReturnSurface = MainWindowSurface.Options;
        OperationsControl.NavigateTo(section, returnToOverview: false);
        ShowPanel(OperationsPanel, MainWindowSurface.Operations);
    }

    /// <summary>Returns from the operations landing page to the surface that opened it.</summary>
    private void OperationsControl_BackRequested(object? sender, EventArgs e)
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

    private void OperationsControl_AtomicResetPrepared(object? sender, AtomicResetPreparedEventArgs e) =>
        AtomicResetPrepared?.Invoke(this, e);

    /// <summary>Shows one top-level panel and measures the visible XAML content before resizing.</summary>
    private void ShowPanel(FrameworkElement panel, MainWindowSurface surface)
    {
        PlayerPanel.Visibility = Visibility.Collapsed;
        PlayerBackgroundSurface.Visibility = Visibility.Collapsed;
        WorldClockButton.Visibility = Visibility.Collapsed;
        OptionsPanel.Visibility = Visibility.Collapsed;
        OperationsPanel.Visibility = Visibility.Collapsed;
        panel.Visibility = Visibility.Visible;
        _layoutState.ShowSurface(surface);
        TitleBarBackButton.Visibility = Visibility.Visible;
        TitleBarLogo.Visibility = Visibility.Collapsed;
        TitleBarTitleText.Text = T(surface == MainWindowSurface.Options
            ? "Options.Title"
            : "Main.Menu.Operations").ToUpper(_strings.Culture);
        TitleBarSearchButton.Visibility = Visibility.Collapsed;
        ResizeForCurrentLayout(animate: false);
        _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        FadeIn(panel);
        _titleBar.QueueLayoutUpdate();
    }

    /// <summary>Restores the player panel.</summary>
    private void ShowPlayer()
    {
        OptionsPanel.Visibility = Visibility.Collapsed;
        OperationsPanel.Visibility = Visibility.Collapsed;
        PlayerBackgroundSurface.Visibility = Visibility.Visible;
        PlayerPanel.Visibility = Visibility.Visible;
        _layoutState.ShowSurface(MainWindowSurface.Player);
        WorldClockButton.Visibility = Visibility.Visible;
        TitleBarBackButton.Visibility = Visibility.Collapsed;
        TitleBarLogo.Visibility = Visibility.Visible;
        TitleBarTitleText.Text = "TRACK ME UP";
        TitleBarSearchButton.Visibility = Visibility.Visible;
        ResizeForCurrentLayout(animate: false);
        _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        FadeIn(PlayerPanel);
        _titleBar.QueueLayoutUpdate();
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

    /// <summary>Measures the active XAML surface at its presentation width and applies the resulting window height.</summary>
    private void ResizeForCurrentLayout(bool animate)
    {
        RootGrid.Measure(new Size(CurrentLogicalWindowWidth, double.PositiveInfinity));
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
        _isTracking = state.IsTracking;
        UpdateActiveHoursAvailability(state.IsWithinActiveHours);
        var currentContext = state.CurrentContext is "STATE_READY"
            ? T("StateReady")
            : state.CurrentContext is "STATE_IDLE"
                ? T("StateIdleContext")
                : state.CurrentContext;
        CurrentContextText.Text = FormatCurrentContext(currentContext);
        KeyCountText.Text = state.TotalKeyPresses.ToString("N0", _strings.Culture);
        ClickCountText.Text = state.TotalMouseClicks.ToString("N0", _strings.Culture);
        ActiveTimeText.Text = TimeSpan.FromSeconds(state.ActiveSeconds).ToString(@"hh\:mm\:ss");
        RenderActivityScore(state.ActivityScore);
        TrackingStateText.Text = T(state.IsTracking ? "StateRunning" : "StatePaused");
        PlayPauseIcon.Glyph = state.IsTracking ? "\uE769" : "\uE768";
        LocalTimeText.Text = _strings.Format("Main.Time.Local", state.LocalTime);
        UtcTimeText.Text = _strings.Format("Main.Time.Utc", state.UtcTime);
        AutomationProperties.SetName(LocalTimeText, LocalTimeText.Text);
        AutomationProperties.SetName(UtcTimeText, UtcTimeText.Text);
        var trackingAction = T(state.IsTracking ? "TrackingActionPause" : "TrackingActionStart");
        AutomationProperties.SetName(TrackingButton, trackingAction);
        ToolTipService.SetToolTip(TrackingButton, trackingAction);

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
            HidePendingSnapshotDeleteUi(enableCapture: false);
            return;
        }

        if (pendingSnapshot is not { } pending)
        {
            HidePendingSnapshotDeleteUi(enableCapture: true);
            return;
        }

        var remaining = pending.ExpiresAt - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            HidePendingSnapshotDeleteUi(enableCapture: true);
            return;
        }

        var countdown = FormatPendingSnapshotCountdown(remaining);
        var deleteAvailableLabel = T("Snapshot.DeleteAvailable");
        var deleteLabel = T("Snapshot.Delete");
        var accessibleStatus = $"{deleteAvailableLabel}, {countdown}";
        SnapshotDeleteAvailableText.Text = deleteAvailableLabel;
        SnapshotDeleteCountdownText.Text = countdown;
        AutomationProperties.SetName(PendingSnapshotPanel, accessibleStatus);
        AutomationProperties.SetHelpText(DeleteSnapshotButton, accessibleStatus);
        AutomationProperties.SetName(DeleteSnapshotButton, deleteLabel);
        ToolTipService.SetToolTip(DeleteSnapshotButton, deleteLabel);
        DeleteSnapshotButton.IsEnabled = _screenshotStorageReady;
        DeleteSnapshotButton.Visibility = Visibility.Visible;
        TakeScreenshotButton.IsEnabled = false;
        SetPlayerSectionVisibility(MainWindowLayoutSection.PendingSnapshot, PendingSnapshotPanel, isVisible: true);
    }

    private void HidePendingSnapshotDeleteUi(bool enableCapture)
    {
        SetPlayerSectionVisibility(MainWindowLayoutSection.PendingSnapshot, PendingSnapshotPanel, isVisible: false);
        DeleteSnapshotButton.Visibility = Visibility.Collapsed;
        TakeScreenshotButton.IsEnabled = _screenshotStorageReady && enableCapture;
        AutomationProperties.SetName(PendingSnapshotPanel, string.Empty);
        AutomationProperties.SetHelpText(DeleteSnapshotButton, string.Empty);
    }

    private static string FormatPendingSnapshotCountdown(TimeSpan remaining)
    {
        var boundedSeconds = Math.Clamp(remaining.TotalSeconds, 1d, PendingSnapshotDeleteSeconds);
        return TimeSpan
            .FromSeconds(Math.Ceiling(boundedSeconds))
            .ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>Renders the bounded one-minute score series supplied by the application facade.</summary>
    private void RenderActivityScore(ActivityScoreState? state)
    {
        if (state is not { Minutes.Count: > 0 })
        {
            ActivityScoreValueText.Text = "0";
            ActivityScoreTelemetryText.Text = T("Activity.Telemetry.Empty");
            ActivityScorePreviousIntervalText.Text = _strings.Format("Activity.Interval.Empty", T("Activity.Previous"));
            ActivityScoreLatestIntervalText.Text = _strings.Format("Activity.Interval.Empty", T("Activity.Latest"));
            ActivityScoreBarHost.Children.Clear();
            ActivityScoreBarHost.ColumnDefinitions.Clear();
            return;
        }

        var latestMinute = state.Minutes[^1];
        ActivityScoreValueText.Text = state.CurrentScore.ToString(_strings.Culture);
        var cpuUsage = latestMinute.CpuUsagePercent is { } cpu
            ? _strings.Format("Activity.Percent", cpu)
            : "--";
        var gpuUsage = latestMinute.GpuUsagePercent is { } gpu
            ? _strings.Format("Activity.Percent", gpu)
            : "--";
        ActivityScoreTelemetryText.Text = _strings.Format("Activity.Telemetry", cpuUsage, gpuUsage);
        ActivityScorePreviousIntervalText.Text = FormatInterval(T("Activity.Previous"), state.PreviousSnapshotInterval);
        ActivityScoreLatestIntervalText.Text = FormatInterval(T("Activity.Latest"), state.LatestSnapshotInterval);

        var maximumKeys = Math.Max(1L, state.Minutes.Max(minute => minute.KeyPresses));
        var maximumClicks = Math.Max(1L, state.Minutes.Max(minute => minute.MouseClicks));
        var inputStartIndex = Math.Max(0, state.Minutes.Count - (state.SnapshotIntervalMinutes * 2));
        var latestSnapshotStartIndex = Math.Max(0, state.Minutes.Count - state.SnapshotIntervalMinutes);
        var scoreBrush = GetPlayerAccentBrush();
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
        _strings.Format(
            "Activity.Interval",
            intervalName,
            interval.MouseClicks,
            T("Activity.Clicks"),
            interval.KeyPresses,
            T("Activity.Keys"));

    private Brush GetPlayerAccentBrush() =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("PlayerAccentTextBrush", out var brush) && brush is Brush playerAccentBrush
            ? playerAccentBrush
            : new SolidColorBrush(Colors.Transparent);

    private Brush GetActivityInputBrush() =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SystemAccentColor", out var accent)
        && accent is Color accentColor
            ? new SolidColorBrush(accentColor)
            : GetPlayerAccentBrush();

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

            await RefreshLastSessionIfDueAsync(force: true);
        }
        finally
        {
            _pendingSnapshotDeleteInProgress = false;
            await RefreshDashboardAsync();
        }
    }

    /// <summary>Renders the latest-session labels and requests the retained screenshot through the application facade.</summary>
    private void UpdateLastSession(LastSessionState? session)
    {
        LastSessionAppText.Text = session?.Application ?? T("NoSession");
        LastSessionDetailText.Text = session?.Timestamp is { } timestamp
            ? _strings.Format("LastSession.Detail", timestamp, session.Context)
            : string.Empty;
        var screenshotPath = session?.ScreenshotPath;
        if (session?.ScreenshotCapturedAt is { } capturedAt
            && !string.IsNullOrWhiteSpace(screenshotPath)
            && System.IO.Path.IsPathFullyQualified(screenshotPath))
        {
            if (string.Equals(_latestScreenshotPath, screenshotPath, StringComparison.OrdinalIgnoreCase)
                && _latestScreenshotCapturedAt == capturedAt)
            {
                return;
            }

            BeginLatestScreenshotLoad(screenshotPath, capturedAt);
            return;
        }

        ShowScreenshotPlaceholder();
    }

    /// <summary>Refreshes the visible latest-session projection at a bounded cadence.</summary>
    private async Task RefreshLastSessionIfDueAsync(bool force = false)
    {
        if (!force && (DetailsPanel.Visibility != Visibility.Visible || DateTimeOffset.Now < _nextLastSessionRefreshAt))
        {
            return;
        }

        if (Interlocked.Exchange(ref _lastSessionRefreshInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            var lastSession = await _viewModel.RefreshLastSessionAsync(CancellationToken.None);
            if (lastSession.Succeeded)
            {
                UpdateLastSession(lastSession.Value);
            }
        }
        finally
        {
            _nextLastSessionRefreshAt = DateTimeOffset.Now.Add(LastSessionRefreshInterval);
            Interlocked.Exchange(ref _lastSessionRefreshInProgress, 0);
        }
    }

    private void BeginLatestScreenshotLoad(string screenshotPath, DateTimeOffset capturedAt)
    {
        CancelLatestScreenshotLoad();
        _latestScreenshotPath = screenshotPath;
        _latestScreenshotCapturedAt = capturedAt;
        RenderScreenshotPlaceholder();

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifecycle.Token);
        _latestScreenshotLoadCancellation = cancellation;
        var generation = ++_latestScreenshotLoadGeneration;
        _ = LoadLatestScreenshotAsync(screenshotPath, capturedAt, generation, cancellation);
    }

    private async Task LoadLatestScreenshotAsync(
        string screenshotPath,
        DateTimeOffset capturedAt,
        int generation,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _screenshotBitmapLoader.LoadAsync(
                screenshotPath,
                ScreenshotPreviewDecodePixelWidth,
                cancellation.Token);
            if (!IsCurrentLatestScreenshotLoad(screenshotPath, capturedAt, generation, cancellation))
            {
                return;
            }

            if (!result.Succeeded || result.Bitmap is null)
            {
                ShowScreenshotPlaceholder();
                return;
            }

            LastScreenshotImage.Source = result.Bitmap;
            LastScreenshotImage.Visibility = Visibility.Visible;
            ScreenshotPlaceholderImage.Visibility = Visibility.Collapsed;
            ScreenshotPreviewButton.IsHitTestVisible = true;
            UpdateScreenshotPreviewAccessibility();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer retained capture or the closing window owns the next preview state.
        }
        catch (Exception)
        {
            if (IsCurrentLatestScreenshotLoad(screenshotPath, capturedAt, generation, cancellation))
            {
                ShowScreenshotPlaceholder();
            }
        }
        finally
        {
            if (ReferenceEquals(_latestScreenshotLoadCancellation, cancellation))
            {
                _latestScreenshotLoadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private bool IsCurrentLatestScreenshotLoad(
        string screenshotPath,
        DateTimeOffset capturedAt,
        int generation,
        CancellationTokenSource cancellation) =>
        !cancellation.IsCancellationRequested
        && generation == _latestScreenshotLoadGeneration
        && ReferenceEquals(_latestScreenshotLoadCancellation, cancellation)
        && string.Equals(_latestScreenshotPath, screenshotPath, StringComparison.OrdinalIgnoreCase)
        && _latestScreenshotCapturedAt == capturedAt;

    private void CancelLatestScreenshotLoad()
    {
        _latestScreenshotLoadGeneration++;
        if (_latestScreenshotLoadCancellation is not { } cancellation)
        {
            return;
        }

        _latestScreenshotLoadCancellation = null;
        cancellation.Cancel();
    }

    /// <summary>Falls back to the packaged pastoral placeholder when an artifact cannot be rendered.</summary>
    private void ShowScreenshotPlaceholder()
    {
        CancelLatestScreenshotLoad();
        _latestScreenshotPath = null;
        _latestScreenshotCapturedAt = null;
        RenderScreenshotPlaceholder();
    }

    private void RenderScreenshotPlaceholder()
    {
        LastScreenshotImage.Source = null;
        LastScreenshotImage.Visibility = Visibility.Collapsed;
        ScreenshotPlaceholderImage.Visibility = Visibility.Visible;
        UpdateScreenshotCaptureStatus();
        ScreenshotOpenOverlay.Opacity = 0;
        ScreenshotPreviewButton.IsHitTestVisible = false;
        UpdateScreenshotPreviewAccessibility();
    }

    /// <summary>Applies presentation settings already validated and persisted by the application layer.</summary>
    private void ApplySettings(AppSettings settings)
    {
        var isInitialSettings = !_hasAppliedSettings;
        var showAiMonthlySpendChanged = _showAiMonthlySpend != settings.ShowAiMonthlySpend;
        var positionChangedByUser = _hasAppliedSettings
            && !string.Equals(_position, settings.FlyoutPosition, StringComparison.Ordinal);
        _strings = new LocalizationService(settings.UiLanguage);
        _theme = settings.Theme;
        _position = settings.FlyoutPosition;
        _hasAppliedSettings = true;
        _screenshotsEnabled = settings.ScreenshotsEnabled;
        _showAiMonthlySpend = settings.ShowAiMonthlySpend;
        WindowContent.Opacity = settings.MainWindowOpacityPercent / 100d;
        _appWindow.IsShownInSwitchers = settings.MainWindowShowInTaskbar;
        if (!_showAiMonthlySpend)
        {
            AiMonthlySpendPanel.Visibility = Visibility.Collapsed;
        }
        else if (showAiMonthlySpendChanged)
        {
            _nextAiSpendRefreshAt = DateTimeOffset.MinValue;
            _ = RefreshAiMonthlySpendAsync();
        }

        RootGrid.RequestedTheme = _theme switch { "light" => ElementTheme.Light, "dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        _titleBar.ApplyTheme(RootGrid.RequestedTheme == ElementTheme.Default ? RootGrid.ActualTheme : RootGrid.RequestedTheme);
        _scheduleWindow?.ApplyTheme(_theme);
        _scheduleWindow?.ApplyLanguage(settings.UiLanguage);
        var indexingTheme = RootGrid.RequestedTheme == ElementTheme.Default
            ? RootGrid.ActualTheme
            : RootGrid.RequestedTheme;
        _searchIndexingWindow?.ApplyTheme(indexingTheme);
        _searchIndexingWindow?.ApplyLanguage(settings.UiLanguage);
        UiLocalization.Apply(RootGrid, _strings);
        ApplyMainAccessibility();
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
        _optionsControl?.ApplyLanguage(settings.UiLanguage);
        _operationsControl?.ApplyLanguage(settings.UiLanguage);
        if (!isInitialSettings || !_mainPlacementRestored)
        {
            ResizeForCurrentLayout(animate: false);
        }
        if (positionChangedByUser)
        {
            // A newly selected anchor is explicit user intent; routine layout changes never reposition the window.
            ApplyFlyoutPosition(_position);
        }

        SettingsApplied?.Invoke(settings);
    }

    private void ApplyMainAccessibility()
    {
        SetIconButtonLabel(TitleBarBackButton, "QuickSetup.Back");
        SetIconButtonLabel(MoreButton, "Main.Menu.Open");
        SetIconButtonLabel(TitleBarMoreButton, "Main.Menu.Open");
        SetIconButtonLabel(TitleBarSearchButton, "Search.Title");
        SetIconButtonLabel(TitleBarReportButton, "Reports.Title");
        SetIconButtonLabel(TitleBarMinimizeToTrayButton, "Main.Menu.MinimizeToTray");
        SetIconButtonLabel(TitleBarCloseButton, "Tray.CloseApplication");
        SetIconButtonLabel(WorldClockButton, "WorldClock.OpenWindow");
        SetIconButtonLabel(TrackingButton, _isTracking ? "TrackingActionPause" : "TrackingActionStart");
        SetIconButtonLabel(TakeScreenshotButton, "Snapshot.Take");
        AutomationProperties.SetName(TrackingStatusToast, T("Main.TrackingStatus"));
        AutomationProperties.SetName(ActivityScoreBarHost, T("Activity.LastThirtyMinutes"));
        UpdateScreenshotPreviewAccessibility();
    }

    private void SetIconButtonLabel(Button button, string key)
    {
        var label = T(key);
        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, label);
    }

    private void UpdateScreenshotPreviewAccessibility()
    {
        var label = T(_latestScreenshotPath is null
            ? "ScreenshotUnavailableHint"
            : "Screenshots.OpenLatest");
        AutomationProperties.SetName(ScreenshotPreviewButton, label);
        ToolTipService.SetToolTip(ScreenshotPreviewButton, label);
    }

    /// <summary>Refreshes every visible settings projection after another application surface changes them.</summary>
    internal async Task ApplyExternalSettingsAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _menuSettings = settings;
        _updatingMenuState = true;
        ScreenshotsMenuToggle.IsChecked = settings.ScreenshotsEnabled;
        _updatingMenuState = false;
        _optionsControl?.ApplyExternalSettings(settings);
        await AiState.LoadAsync(CancellationToken.None);
        ApplySettings(settings);
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

    /// <summary>Opens or reactivates the owned progress window for the two local search indexes.</summary>
    private void OptionsControl_SearchIndexingRequested(object? sender, EventArgs e)
    {
        if (_searchIndexingWindow is not null)
        {
            _searchIndexingWindow.Activate();
            return;
        }

        var effectiveTheme = RootGrid.RequestedTheme == ElementTheme.Default
            ? RootGrid.ActualTheme
            : RootGrid.RequestedTheme;
        var indexingWindow = new SearchIndexingWindow(
            _application,
            effectiveTheme,
            _strings.RequestedLanguage,
            _appWindow,
            WinRT.Interop.WindowNative.GetWindowHandle(this));
        indexingWindow.Closed += SearchIndexingWindow_Closed;
        _searchIndexingWindow = indexingWindow;
        indexingWindow.Activate();
    }

    private void SearchIndexingWindow_Closed(object sender, WindowEventArgs args)
    {
        if (sender is SearchIndexingWindow indexingWindow)
        {
            indexingWindow.Closed -= SearchIndexingWindow_Closed;
        }

        _searchIndexingWindow = null;
    }

    private void UpdateOpenAiMenuAccessibility()
    {
        AutomationProperties.SetName(OpenAiMenuToggle, T("MenuToggleOpenAi"));
        var toggleTooltip = T("Main.Menu.AiToggle.Tooltip");
        AutomationProperties.SetHelpText(
            OpenAiMenuToggle,
            AiState.IsStatusUnavailable
                ? $"{toggleTooltip} {T("Options.ApiKeyStatus.Unavailable")}"
                : !AiState.CanEnable && !AiState.Enabled
                    ? $"{toggleTooltip} {T("Options.OpenAi.KeyRequired")}"
                    : toggleTooltip);
        AutomationProperties.SetName(AiPricingMenuItem, T("AiPricing.MenuTitle"));
        var pricingTooltip = T("Main.Menu.AiPricing.Tooltip");
        AutomationProperties.SetHelpText(
            AiPricingMenuItem,
            AiState.Enabled
                ? pricingTooltip
                : $"{pricingTooltip} {T("AiPricing.DisabledHint")}");
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
        var detail = notification.LocalizedDetail is { } localizedDetail
            ? _strings.Format(
                localizedDetail.MessageKey,
                localizedDetail.Arguments.Select(static value => (object?)value).ToArray())
            : notification.Detail;
        return string.IsNullOrWhiteSpace(detail)
            ? message
            : $"{message}{Environment.NewLine}{Environment.NewLine}{detail}";
    }

    /// <summary>Shows the player at its current user-controlled position.</summary>
    public void ShowFlyout()
    {
        Activate();
    }

    /// <summary>Closes the player for an application-owned shutdown that must not prompt the user.</summary>
    internal void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    /// <summary>Starts the Windows-sign-in instance in the notification area without first creating a taskbar button.</summary>
    internal void StartMinimizedToNotificationArea()
    {
        // Activating and hiding in the same dispatcher turn composes Loaded without leaving a taskbar window visible.
        Activate();
        HideToNotificationArea();
    }

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
    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_xamlRoot is null && RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        ResizeForCurrentLayout(animate: false);
        try
        {
            _mainPlacementRestored = await _placement.RestoreAsync(RootGrid, _lifecycle.Token);
        }
        catch (OperationCanceledException) when (_lifecycle.IsCancellationRequested)
        {
            return;
        }

        if (_mainPlacementRestored)
        {
            var scale = RootGrid.XamlRoot!.RasterizationScale;
            _layoutState.RecordManualSize(_appWindow.Size.Width / scale, _appWindow.Size.Height / scale);
        }

        ResizeForCurrentLayout(animate: false);
        _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        _currentWorkArea = CurrentWorkArea();
        _windowSizingReady = true;
        _lifecycle.SignalLoaded();
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
        _placement.KeepCurrentBoundsInWorkArea(RootGrid);
    }

    /// <summary>Reapplies the smart height limit when the flyout crosses onto another display.</summary>
    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange && _windowSizingReady)
        {
            if (sender.Size.Width != _requestedWindowSize.Width || sender.Size.Height != _requestedWindowSize.Height)
            {
                var scale = RootGrid.XamlRoot!.RasterizationScale;
                _windowResizeAnimationTimer.Stop();
                _layoutState.RecordManualSize(sender.Size.Width / scale, sender.Size.Height / scale);
            }

            _titleBar.QueueLayoutUpdate();
        }

        if (args.DidVisibilityChange)
        {
            UpdateDashboardSubscriptionForVisibility();
        }

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
        _placement.KeepCurrentBoundsInWorkArea(RootGrid);
    }

    /// <summary>Confirms native close requests before the application suspends tracking and exits.</summary>
    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || _dashboardSurfaceClosed)
        {
            return;
        }

        args.Cancel = true;
        if (_closeConfirmationInProgress)
        {
            return;
        }

        _closeConfirmationInProgress = true;
        try
        {
            var confirmed = await _dialogs.ConfirmAsync(
                this,
                DialogRequest.Confirmation(
                    T("Dialog.CloseTracking.Title"),
                    T("Dialog.CloseTracking.Message"),
                    T("Dialog.Ok"),
                    T("Dialog.Cancel")));
            if (!confirmed)
            {
                return;
            }

            await _placement.TrySaveForCloseAsync(CancellationToken.None);
            _allowClose = true;
            Close();
        }
        finally
        {
            _closeConfirmationInProgress = false;
        }
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
        ApplyWindowSize(GetPhysicalWindowSize(logicalHeight));
        _titleBar.QueueLayoutUpdate();
    }

    /// <summary>Interpolates the compact player height after a visible layout change.</summary>
    private void AnimateResizeForLogicalContent(int logicalHeight)
    {
        _windowResizeAnimationStartSize = _appWindow.Size;
        _windowResizeAnimationTargetSize = GetPhysicalWindowSize(logicalHeight);
        if (_windowResizeAnimationStartSize.Height == _windowResizeAnimationTargetSize.Height)
        {
            ApplyWindowSize(_windowResizeAnimationTargetSize);
            _titleBar.QueueLayoutUpdate();
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
        ApplyWindowSize(new SizeInt32(_windowResizeAnimationTargetSize.Width, height));

        if (progress >= 1d)
        {
            _windowResizeAnimationTimer.Stop();
            _titleBar.QueueLayoutUpdate();
        }
    }

    /// <summary>Marks application-driven bounds so their native notification cannot replace a user's preferred size.</summary>
    private void ApplyWindowSize(SizeInt32 size)
    {
        _requestedWindowSize = size;
        _appWindow.Resize(size);
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
        var boundedLogicalHeight = _layoutState.ResolveLogicalHeight(availableHeight / scale, LogicalWindowHeightPadding);
        var physicalWidth = Math.Min(availableWidth, (int)Math.Ceiling(CurrentLogicalWindowWidth * scale));
        var physicalHeight = Math.Min(availableHeight, (int)Math.Ceiling(boundedLogicalHeight * scale));
        return new SizeInt32(physicalWidth, physicalHeight);
    }

    /// <summary>Keeps the live player compact while giving layered options and operations enough room to reflow.</summary>
    private int CurrentLogicalWindowWidth
    {
        get
        {
            var scale = RootGrid.XamlRoot?.RasterizationScale ?? _rasterizationScale;
            var availableWidth = Math.Max(1d, CurrentWorkArea().Width / scale - LogicalScreenMargin * 2);
            var preferredWidth = _layoutState.Surface == MainWindowSurface.Player
                ? LogicalWindowWidth
                : LogicalExpandedWindowWidth;
            return _layoutState.ResolveLogicalWidth(availableWidth, preferredWidth);
        }
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

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        _dashboardSurfaceClosed = true;
        _dashboardRefreshReady = false;
        CancelLatestScreenshotLoad();
        _lifecycle.Cancel();
        _dashboardSubscription?.Dispose();
        _dashboardSubscription = null;
        _windowResizeAnimationTimer.Stop();
        _dialogs.CloseActive();
        _appWindow.Changed -= AppWindow_Changed;
        _appWindow.Closing -= AppWindow_Closing;
        _titleBar.Dispose();
        _placement.Dispose();
        _trayIcon.ExitRequested -= TrayIcon_ExitRequested;
        _trayIcon.Dispose();
        if (_optionsControl is not null)
        {
            _optionsControl.BackRequested -= OptionsControl_BackRequested;
            _optionsControl.SettingsSaved -= ApplySettings;
            _optionsControl.LayoutChanged -= OptionsControl_LayoutChanged;
            _optionsControl.AiConnectionTestRequested -= OptionsControl_AiConnectionTestRequested;
            _optionsControl.OperationsSectionRequested -= OptionsControl_OperationsSectionRequested;
            _optionsControl.SearchIndexingRequested -= OptionsControl_SearchIndexingRequested;
        }

        if (_operationsControl is not null)
        {
            _operationsControl.BackRequested -= OperationsControl_BackRequested;
            _operationsControl.LayoutChanged -= OperationsControl_LayoutChanged;
            _operationsControl.AtomicResetPrepared -= OperationsControl_AtomicResetPrepared;
        }

        if (_scheduleWindow is not null)
        {
            _scheduleWindow.Close();
            _scheduleWindow = null;
        }

        if (_searchIndexingWindow is not null)
        {
            _searchIndexingWindow.Close();
            _searchIndexingWindow = null;
        }

        _lifecycle.InitializationFailed -= Lifecycle_InitializationFailed;
        _lifecycle.Dispose();
    }
}

/// <summary>Identifies the retained capture that the screenshot inspector should select.</summary>
public sealed class ScreenshotPreviewRequestedEventArgs(string screenshotPath, DateTimeOffset capturedAt) : EventArgs
{
    public string ScreenshotPath { get; } = screenshotPath;

    public DateTimeOffset CapturedAt { get; } = capturedAt;
}

/// <summary>Identifies the day that the screenshot inspector should explore.</summary>
public sealed class ScreenshotGalleryDateRequestedEventArgs(DateOnly date) : EventArgs
{
    public DateOnly Date { get; } = date;
}
