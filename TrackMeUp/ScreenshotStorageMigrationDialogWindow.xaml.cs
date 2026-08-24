using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Displays a non-dismissible progress surface while the application facade migrates screenshot storage.</summary>
internal sealed partial class ScreenshotStorageMigrationDialogWindow : Window
{
    private const int LogicalWidth = 500;
    private const int LogicalHeight = 220;
    private const int LogicalScreenMargin = 24;
    private readonly ITrackMeUpApplication _application;
    private readonly TaskCompletionSource<OperationResult<ScreenshotStorageMigrationResult>> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly IntPtr _windowHandle;
    private bool _started;
    private bool _allowClose;

    /// <summary>Creates an owned progress window that delegates the migration to the shared application facade.</summary>
    internal ScreenshotStorageMigrationDialogWindow(
        ITrackMeUpApplication application,
        ElementTheme theme,
        LocalizationService strings,
        AppWindow? ownerAppWindow,
        IntPtr ownerHandle)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        ArgumentNullException.ThrowIfNull(strings);
        InitializeComponent();
        RootGrid.RequestedTheme = theme;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.Dialog,
            LogicalWidth,
            LogicalHeight,
            LogicalScreenMargin,
            ownerAppWindow?.Id);
        if (ownerHandle != IntPtr.Zero)
        {
            WindowInteropService.SetOwner(_windowHandle, ownerHandle);
        }
        ConfigureWindowBehavior();

        Title = strings.Translate("Dialog.DataMigration.Title");
        MigrationTitleText.Text = Title;
        MigrationMessageText.Text = strings.Translate("Dialog.DataMigration.Message");
        AutomationProperties.SetName(RootGrid, Title);
        AutomationProperties.SetName(MigrationTitleText, Title);
        AutomationProperties.SetName(MigrationMessageText, MigrationMessageText.Text);

        _appWindow.Closing += AppWindow_Closing;
        Closed += ScreenshotStorageMigrationDialogWindow_Closed;
    }

    internal IntPtr WindowHandle => _windowHandle;

    /// <summary>Activates the progress surface and completes with the facade migration result.</summary>
    internal Task<OperationResult<ScreenshotStorageMigrationResult>> ShowAsync()
    {
        WindowInteropService.MakeTopmostWithoutActivation(_windowHandle);
        Activate();
        return _completion.Task;
    }

    /// <summary>Allows application shutdown to close the otherwise non-dismissible migration window.</summary>
    internal void CloseForShutdown()
    {
        _allowClose = true;
        _lifetimeCancellation.Cancel();
        Close();
    }

    internal void DisposePlacement() => _placement.Dispose();

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _placement.ApplyDefaultBounds(RootGrid);
        try
        {
            await _placement.RestoreAndCenterAsync(RootGrid, _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_allowClose)
        {
            return;
        }
        catch (Exception)
        {
            Complete(OperationResult<ScreenshotStorageMigrationResult>.Failure(
                "screenshot.storage_migration.window_failed",
                "ScreenshotStorageMigrationFailed"));
            return;
        }

        if (_started || _allowClose)
        {
            return;
        }

        _started = true;
        // A low-priority dispatch gives WinUI a frame to compose the progress surface before file migration begins.
        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, RunMigrationFromVisibleWindow))
        {
            Complete(OperationResult<ScreenshotStorageMigrationResult>.Failure(
                "screenshot.storage_migration.dispatch_failed",
                "ScreenshotStorageMigrationFailed"));
        }
    }

    private async void RunMigrationFromVisibleWindow()
    {
        OperationResult<ScreenshotStorageMigrationResult> result;
        try
        {
            // Storage and metadata mutation remain serialized behind the application facade.
            result = await _application.MigrateScreenshotStorageAsync(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_allowClose)
        {
            return;
        }
        catch (Exception)
        {
            result = OperationResult<ScreenshotStorageMigrationResult>.Failure(
                "screenshot.storage_migration.unexpected",
                "ScreenshotStorageMigrationFailed");
        }

        Complete(result);
    }

    private void Complete(OperationResult<ScreenshotStorageMigrationResult> result)
    {
        if (_allowClose)
        {
            return;
        }

        _allowClose = true;
        _completion.TrySetResult(result);
        Close();
    }

    private void ConfigureWindowBehavior()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _appWindow.TitleBar.BackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.InactiveBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = !_allowClose;
    }

    private void ScreenshotStorageMigrationDialogWindow_Closed(object sender, WindowEventArgs args)
    {
        _appWindow.Closing -= AppWindow_Closing;
        _lifetimeCancellation.Cancel();
        _completion.TrySetResult(OperationResult<ScreenshotStorageMigrationResult>.Failure(
            "operation.cancelled",
            "ScreenshotStorageMigrationFailed"));
    }
}
