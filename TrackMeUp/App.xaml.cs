using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Cli;
using TrackMeUp.Controls;
using TrackMeUp.Runtime;
using TrackMeUp.Services;
using TaskbarWidgetSurface = TrackMeUp.Taskbar.TaskbarWidgetSurface;

namespace TrackMeUp;

/// <summary>Provides the Windows composition root and selects a launch mode before any view is created.</summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly ServiceProvider _services;
    private readonly ILogger<App> _logger;
    private readonly MicaDialogService _dialogs = new();
    private readonly IWindowsToastNotificationService _windowsNotifications;
    private readonly AtomicResetService _atomicReset = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private MainWindow? _window;
    private ReportsWindow? _reportsWindow;
    private ScreenshotWindow? _screenshotsWindow;
    private SearchWindow? _searchWindow;
    private QuickSetupWindow? _quickSetupWindow;
    private TaskbarWidgetSurface? _taskbarWidgetSurface;
    private RuntimeHost? _runtimeHost;
    private ITrackMeUpApplication? _runtimeApplication;
    private ITrackMeUpApplication? _applicationFacade;
    private bool _reportsOnly;
    private bool _searchWindowOpening;
    private bool _quickSetupOwnerWasInteractive;
    private int _shutdownStarted;
    private int _atomicResetStarted;

    /// <summary>Initializes the WinUI application object and its logging composition root.</summary>
    public App()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("The WinUI dispatcher queue is unavailable.");
        _services = LoggingBootstrapper.CreateServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();
        _windowsNotifications = new WindowsToastNotificationService(
            _services.GetRequiredService<ILoggerFactory>().CreateLogger<WindowsToastNotificationService>());
        InitializeComponent();
        UnhandledException += (_, eventArgs) => _logger.LogCritical(eventArgs.Exception, "Unhandled WinUI exception.");
        _logger.LogInformation("TrackMeUp process started. Architecture={Architecture}", RuntimeInformation.ProcessArchitecture);
    }

    /// <summary>Routes launch modes to the CLI, background runtime, reports, or WinUI player.</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var activationKind = ReadActivationKind();
            var options = StartupActivationPolicy.Apply(
                LaunchOptions.Parse(Environment.GetCommandLineArgs().Skip(1).ToArray()),
                activationKind);
            _logger.LogInformation(
                "Launch requested. Mode={Mode} ActivationKind={ActivationKind}",
                options.Mode,
                activationKind);
            switch (options.Mode)
            {
                case LaunchMode.Cli:
                    _ = RunCliAndExitAsync(Environment.GetCommandLineArgs().Skip(1).ToArray());
                    return;
                case LaunchMode.Help:
                    _ = RunCliAndExitAsync(["--help"]);
                    return;
                case LaunchMode.Version:
                    _ = RunCliAndExitAsync(["--version"]);
                    return;
                case LaunchMode.Background:
                    StartBackgroundRuntime(options);
                    return;
                case LaunchMode.Reports:
                    StartReports(options);
                    return;
                default:
                    StartUi(options);
                    return;
            }
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Launch failed before the main window was created.");
            throw;
        }
    }

    private static ExtendedActivationKind ReadActivationKind()
    {
        try
        {
            return AppInstance.GetCurrent().GetActivatedEventArgs()?.Kind ?? ExtendedActivationKind.Launch;
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            // Plain command-line launches still have complete bootstrap arguments when rich activation is unavailable.
            return ExtendedActivationKind.Launch;
        }
    }

    private void StartUi(LaunchOptions options)
    {
        _reportsOnly = false;
        var application = StartOrConnectRuntime();
        var trayIcon = new TrayIconService(_services.GetRequiredService<ILoggerFactory>().CreateLogger<TrayIconService>());
        _window = new MainWindow(application, options, _dialogs, trayIcon, _windowsNotifications);
        _window.SettingsApplied += ApplyTaskbarWidgetSettings;
        _window.QuickSetupRequested += MainWindow_QuickSetupRequested;
        _window.ReportsRequested += MainWindow_ReportsRequested;
        _window.SearchRequested += MainWindow_SearchRequested;
        _window.ScreenshotGalleryRequested += MainWindow_ScreenshotGalleryRequested;
        _window.ScreenshotsRequested += MainWindow_ScreenshotsRequested;
        _window.ExitRequested += MainWindow_ExitRequested;
        _window.AtomicResetPrepared += MainWindow_AtomicResetPrepared;
        _window.Closed += MainWindow_Closed;
        if (options.StartWithWindows)
        {
            try
            {
                _window.StartMinimizedToNotificationArea();
            }
            catch (Exception exception)
            {
                // If Explorer rejects the tray icon at sign-in, keep the application reachable instead of leaving a hidden window without an activation path.
                _logger.LogError(exception, "Windows-sign-in startup could not initialize the notification-area icon.");
                var strings = new LocalizationService(options.Language ?? "system");
                _windowsNotifications.TryShow(
                    strings.Translate("Notification.WindowsStartupFailed.Title"),
                    $"{strings.Translate("Notification.WindowsStartupFailed.Message")}{Environment.NewLine}{Environment.NewLine}{exception.GetType().Name}: {exception.Message}");
                _window.Activate();
            }
        }
        else
        {
            _window.Activate();
        }

        _ = CompleteUiStartupAsync(application, options);
    }

    private async Task CompleteUiStartupAsync(ITrackMeUpApplication application, LaunchOptions options)
    {
        try
        {
            var settingsResult = await application.GetSettingsAsync(CancellationToken.None);
            if (!settingsResult.Succeeded || settingsResult.Value is null)
            {
                _logger.LogWarning("UI startup settings could not be loaded. Code={Code}", settingsResult.Code);
                DisposeTaskbarWidget();
                return;
            }

            var settings = settingsResult.Value;
            ApplyTaskbarWidgetSettings(settings);
            if (!settings.QuickSetupCompleted && !options.StartWithWindows)
            {
                ShowQuickSetupWindow(application, settings, firstRun: true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "UI startup preparation failed after the main window was activated.");
            DisposeTaskbarWidget();
        }
    }

    private void StartReports(LaunchOptions options) => _ = StartReportsAsync(options);

    private async Task StartReportsAsync(LaunchOptions options)
    {
        _reportsOnly = true;
        var application = StartOrConnectRuntime();
        try
        {
            var settings = await application.GetSettingsAsync(CancellationToken.None);
            if (!settings.Succeeded || settings.Value is null)
            {
                _logger.LogWarning("Reports startup settings could not be loaded. Code={Code}", settings.Code);
                await ShutdownRuntimeAsync();
                Exit();
                return;
            }

            var startup = await application.SetStartupEnabledAsync(
                settings.Value.StartWithWindows,
                CancellationToken.None);
            if (!startup.Succeeded)
            {
                _logger.LogWarning("Windows startup registration reconciliation failed. Code={Code}", startup.Code);
            }

            var strings = new LocalizationService(settings.Value.UiLanguage);
            var migrationTheme = (options.Theme ?? settings.Value.Theme) switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            var migrationStatus = await application.GetScreenshotStorageMigrationStatusAsync(CancellationToken.None);
            if (!migrationStatus.Succeeded || migrationStatus.Value is null)
            {
                NotifyScreenshotStorageMigrationFailure(strings, migrationStatus.Code);
                await ShutdownRuntimeAsync();
                Exit();
                return;
            }

            // A reports-only launch has no owner window yet, but still exposes required file moves visibly.
            var migration = migrationStatus.Value.Required
                ? await _dialogs.ShowStandaloneScreenshotStorageMigrationAsync(application, migrationTheme, strings)
                : await application.MigrateScreenshotStorageAsync(CancellationToken.None);
            if (!migration.Succeeded)
            {
                _logger.LogError("Reports startup screenshot migration failed. Code={Code}", migration.Code);
                NotifyScreenshotStorageMigrationFailure(strings, migration.Code);
                await ShutdownRuntimeAsync();
                Exit();
                return;
            }

            ShowReportsWindow(application, options.Theme);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Reports startup preparation failed; no report window was opened.");
            await ShutdownRuntimeAsync();
            Exit();
        }
    }

    private void NotifyScreenshotStorageMigrationFailure(LocalizationService strings, string code) =>
        _windowsNotifications.TryShow(
            strings.Translate("Dialog.DataMigration.Failed.Title"),
            strings.Format("Dialog.DataMigration.Failed.Message", code));

    private void MainWindow_ReportsRequested(object? sender, EventArgs eventArgs) => ShowReportsWindow(StartOrConnectRuntime(), null);

    private async void MainWindow_QuickSetupRequested(object? sender, EventArgs eventArgs)
    {
        var application = StartOrConnectRuntime();
        var result = await application.GetSettingsAsync(CancellationToken.None);
        if (!result.Succeeded || result.Value is null)
        {
            if (_window is not null)
            {
                var strings = new LocalizationService("system");
                await _dialogs.ShowInformativeAsync(
                    application,
                    _window,
                    MicaDialogRequest.Informative(
                        strings.Translate("QuickSetup.Unavailable.Title"),
                        strings.Translate("QuickSetup.Unavailable.Message"),
                        MicaDialogSeverity.Error,
                        strings.Translate("Dialog.Ok")),
                    ElementTheme.Default);
            }

            return;
        }

        ShowQuickSetupWindow(application, result.Value, firstRun: false);
    }

    private void ShowQuickSetupWindow(ITrackMeUpApplication application, AppSettings settings, bool firstRun)
    {
        if (_quickSetupWindow is not null)
        {
            _quickSetupWindow.Activate();
            return;
        }

        if (_window is null)
        {
            throw new InvalidOperationException("Quick Setup requires the main TrackMeUp window.");
        }

        var ownerHandle = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var ownerAppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(ownerHandle));
        if (_window.Content is UIElement ownerContent)
        {
            _quickSetupOwnerWasInteractive = ownerContent.IsHitTestVisible;
            ownerContent.IsHitTestVisible = false;
        }

        _quickSetupWindow = new QuickSetupWindow(application, settings, firstRun, ownerAppWindow, ownerHandle);
        _quickSetupWindow.ProfileApplied += QuickSetupWindow_ProfileApplied;
        _quickSetupWindow.Closed += QuickSetupWindow_Closed;
        _quickSetupWindow.Activate();
    }

    private async void QuickSetupWindow_ProfileApplied(AppSettings settings)
    {
        if (_window is not null)
        {
            await _window.ApplyExternalSettingsAsync(settings);
        }
        else
        {
            ApplyTaskbarWidgetSettings(settings);
        }
    }

    private void QuickSetupWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_quickSetupWindow is not null)
        {
            _quickSetupWindow.ProfileApplied -= QuickSetupWindow_ProfileApplied;
            _quickSetupWindow.Closed -= QuickSetupWindow_Closed;
            _quickSetupWindow = null;
        }

        if (_window?.Content is UIElement ownerContent)
        {
            ownerContent.IsHitTestVisible = _quickSetupOwnerWasInteractive;
        }
    }

    private async void MainWindow_SearchRequested(object? sender, EventArgs eventArgs) =>
        await ShowSearchWindowAsync(StartOrConnectRuntime());

    private async void MainWindow_ScreenshotGalleryRequested(object? sender, EventArgs eventArgs)
        => await ShowScreenshotWindowAsync(StartOrConnectRuntime(), null);

    private async void MainWindow_ScreenshotsRequested(object? sender, ScreenshotPreviewRequestedEventArgs eventArgs)
        => await ShowScreenshotWindowAsync(StartOrConnectRuntime(), null, eventArgs.ScreenshotPath, eventArgs.CapturedAt);

    private void MainWindow_ExitRequested(object? sender, EventArgs eventArgs) => _window?.Close();

    private void ShowReportsWindow(ITrackMeUpApplication application, string? launchTheme)
    {
        if (_reportsWindow is not null)
        {
            _reportsWindow.SelectToday();
            _reportsWindow.Activate();
            return;
        }

        _reportsWindow = new ReportsWindow(application, launchTheme);
        _reportsWindow.Closed += ReportsWindow_Closed;
        _reportsWindow.Activate();
    }

    private async Task ShowSearchWindowAsync(ITrackMeUpApplication application)
    {
        if (_searchWindow is not null)
        {
            _searchWindow.ActivateAtCursor();
            return;
        }

        if (_searchWindowOpening)
        {
            return;
        }

        _searchWindowOpening = true;
        try
        {
            if (_window is null)
            {
                throw new InvalidOperationException("Search requires the main TrackMeUp window.");
            }

            var settingsTask = application.GetSettingsAsync(CancellationToken.None);
            var availabilityTask = application.GetSearchAvailabilityAsync(CancellationToken.None);
            await Task.WhenAll(settingsTask, availabilityTask);
            var settings = await settingsTask;
            var availability = await availabilityTask;
            if (!settings.Succeeded || settings.Value is null || !availability.Succeeded || availability.Value is null)
            {
                throw new InvalidOperationException($"Search availability is unavailable ({settings.Code}, {availability.Code}).");
            }

            if (availability.Value.TotalSnapshotCount == 0)
            {
                var strings = new LocalizationService(settings.Value.UiLanguage);
                await _dialogs.ShowInformativeAsync(
                    application,
                    _window,
                    MicaDialogRequest.Informative(
                        strings.Translate("Search.Empty.Title"),
                        strings.Translate("Search.Empty.Message"),
                        MicaDialogSeverity.Information,
                        strings.Translate("Dialog.Ok")),
                    ElementTheme.Default);
                return;
            }

            _searchWindow = new SearchWindow(application, settings.Value.UiLanguage, availability.Value);
            _searchWindow.ScreenshotRequested += SearchWindow_ScreenshotRequested;
            _searchWindow.Closed += SearchWindow_Closed;
            _searchWindow.Activate();
        }
        finally
        {
            _searchWindowOpening = false;
        }
    }

    private async void SearchWindow_ScreenshotRequested(object? sender, ScreenshotPreviewRequestedEventArgs eventArgs) =>
        await ShowScreenshotWindowAsync(
            StartOrConnectRuntime(),
            null,
            eventArgs.ScreenshotPath,
            eventArgs.CapturedAt);

    private async Task ShowScreenshotWindowAsync(
        ITrackMeUpApplication application,
        string? launchTheme,
        string screenshotPath,
        DateTimeOffset capturedAt)
    {
        if (_screenshotsWindow is not null)
        {
            await _screenshotsWindow.FocusScreenshotAsync(screenshotPath, capturedAt);
            _screenshotsWindow.Activate();
            return;
        }

        _screenshotsWindow = new ScreenshotWindow(application, launchTheme, screenshotPath, capturedAt);
        _screenshotsWindow.Closed += ScreenshotsWindow_Closed;
        _screenshotsWindow.Activate();
    }

    private async Task ShowScreenshotWindowAsync(ITrackMeUpApplication application, string? launchTheme)
    {
        if (_screenshotsWindow is not null)
        {
            await _screenshotsWindow.FocusLatestAsync();
            _screenshotsWindow.Activate();
            return;
        }

        _screenshotsWindow = new ScreenshotWindow(application, launchTheme);
        _screenshotsWindow.Closed += ScreenshotsWindow_Closed;
        _screenshotsWindow.Activate();
    }

    private async void ReportsWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_reportsWindow is not null)
        {
            _reportsWindow.Closed -= ReportsWindow_Closed;
            _reportsWindow = null;
        }

        if (_reportsOnly && _window is null)
        {
            await ShutdownRuntimeAsync();
            Exit();
        }
    }

    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_window is not null)
        {
            _window.SettingsApplied -= ApplyTaskbarWidgetSettings;
            _window.QuickSetupRequested -= MainWindow_QuickSetupRequested;
            _window.ReportsRequested -= MainWindow_ReportsRequested;
            _window.SearchRequested -= MainWindow_SearchRequested;
            _window.ScreenshotGalleryRequested -= MainWindow_ScreenshotGalleryRequested;
            _window.ScreenshotsRequested -= MainWindow_ScreenshotsRequested;
            _window.ExitRequested -= MainWindow_ExitRequested;
            _window.AtomicResetPrepared -= MainWindow_AtomicResetPrepared;
            _window.Closed -= MainWindow_Closed;
            _window = null;
        }

        if (_quickSetupWindow is not null)
        {
            _quickSetupWindow.ProfileApplied -= QuickSetupWindow_ProfileApplied;
            _quickSetupWindow.Closed -= QuickSetupWindow_Closed;
            _quickSetupWindow.Close();
            _quickSetupWindow = null;
        }

        if (_reportsWindow is not null)
        {
            _reportsWindow.Closed -= ReportsWindow_Closed;
            _reportsWindow.Close();
            _reportsWindow = null;
        }

        if (_screenshotsWindow is not null)
        {
            _screenshotsWindow.Closed -= ScreenshotsWindow_Closed;
            _screenshotsWindow.CloseForShutdown();
            _screenshotsWindow = null;
        }

        if (_searchWindow is not null)
        {
            _searchWindow.ScreenshotRequested -= SearchWindow_ScreenshotRequested;
            _searchWindow.Closed -= SearchWindow_Closed;
            _searchWindow.Close();
            _searchWindow = null;
        }

        DisposeTaskbarWidget();
        if (Volatile.Read(ref _atomicResetStarted) != 0)
        {
            return;
        }

        await ShutdownRuntimeAsync();
        Exit();
    }

    private void ScreenshotsWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_screenshotsWindow is not null)
        {
            _screenshotsWindow.Closed -= ScreenshotsWindow_Closed;
            _screenshotsWindow = null;
        }
    }

    private void SearchWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_searchWindow is not null)
        {
            _searchWindow.ScreenshotRequested -= SearchWindow_ScreenshotRequested;
            _searchWindow.Closed -= SearchWindow_Closed;
            _searchWindow = null;
        }
    }

    private void ApplyTaskbarWidgetSettings(AppSettings settings)
    {
        if (!settings.TaskbarWidgetVisible)
        {
            DisposeTaskbarWidget();
            return;
        }

        if (_taskbarWidgetSurface is not null)
        {
            _taskbarWidgetSurface.ApplySettings(settings);
            _taskbarWidgetSurface.Configure(settings.TaskbarWidgetPosition);
            return;
        }

        try
        {
            var application = _applicationFacade ?? throw new InvalidOperationException("The taskbar widget requires an initialized application facade.");
            var taskbarWidgetSurface = new TaskbarWidgetSurface(application, new TaskbarWidgetHost(_services.GetRequiredService<ILogger<TaskbarWidgetHost>>()), _services.GetRequiredService<ILogger<TaskbarWidgetSurface>>());
            _taskbarWidgetSurface = taskbarWidgetSurface;
            taskbarWidgetSurface.FlyoutRequested += (_, _) => _window?.DispatcherQueue.TryEnqueue(() => _window?.ShowFlyout());
            taskbarWidgetSurface.ApplySettings(settings);
            if (!taskbarWidgetSurface.Attach(settings.TaskbarWidgetPosition))
            {
                // If a custom shell rejects parenting, keep the normal player usable rather than leaving an orphaned top-level control.
                DisposeTaskbarWidget();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Taskbar widget initialization failed; the main window remains available.");
            DisposeTaskbarWidget();
        }
    }

    private void DisposeTaskbarWidget()
    {
        _taskbarWidgetSurface?.Dispose();
        _taskbarWidgetSurface = null;
    }

    private void StartBackgroundRuntime(LaunchOptions options) => _ = StartBackgroundRuntimeAsync(options);

    private async Task StartBackgroundRuntimeAsync(LaunchOptions options)
    {
        try
        {
            var application = StartOrConnectRuntime();
            if (!ReferenceEquals(application, _runtimeApplication))
            {
                return;
            }

            var settings = await application.GetSettingsAsync(CancellationToken.None);
            if (!settings.Succeeded || settings.Value is null)
            {
                // A headless launch cannot present recovery UI, so leave tracking paused and record the explicit failure.
                _logger.LogWarning("Background startup settings could not be loaded. Code={Code}", settings.Code);
                return;
            }

            var startup = await application.SetStartupEnabledAsync(
                settings.Value.StartWithWindows,
                CancellationToken.None);
            if (!startup.Succeeded)
            {
                // Startup reconciliation is recoverable: the runtime may track, but diagnostics retain the OS integration failure.
                _logger.LogWarning("Background Windows startup registration reconciliation failed. Code={Code}", startup.Code);
            }

            var migration = await application.MigrateScreenshotStorageAsync(CancellationToken.None);
            if (!migration.Succeeded)
            {
                // A headless process cannot ask for recovery; fail paused so captures never mix old and new layouts.
                _logger.LogError("Background screenshot storage migration failed. Code={Code}", migration.Code);
                return;
            }

            if (TrackingStartupPolicy.ShouldStart(options, settings.Value))
            {
                var started = await application.StartTrackingAsync(
                    new StartTrackingRequest(options.SafeMode, "background"),
                    CancellationToken.None);
                if (!started.Succeeded)
                {
                    _logger.LogError("Background tracking startup failed. Code={Code}", started.Code);
                }
            }
        }
        catch (Exception exception)
        {
            // Fire-and-forget launch tasks must retain an explicit paused failure path instead of surfacing an unobserved exception.
            _logger.LogError(exception, "Background startup preparation failed; tracking remains paused.");
        }
    }

    private ITrackMeUpApplication StartOrConnectRuntime()
    {
        if (_applicationFacade is not null)
        {
            return _applicationFacade;
        }

        var loggerFactory = _services.GetRequiredService<ILoggerFactory>();
        var observability = _services.GetRequiredService<ObservabilityHealth>();
        var installationId = TrackMeUpApplicationFactory.LoadInstallationId();
        var host = new RuntimeHost(
            () => TrackMeUpApplicationFactory.Create(loggerFactory, observability),
            installationId,
            loggerFactory.CreateLogger<RuntimeHost>());
        if (host.TryStart())
        {
            var localApplication = host.Application;
            _logger.LogInformation("Runtime ownership acquired for this installation.");
            host.AtomicResetPrepared += RuntimeHost_AtomicResetPrepared;
            _runtimeHost = host;
            _runtimeApplication = localApplication;
            _applicationFacade = localApplication;
            return _applicationFacade;
        }

        // A separate process owns hooks and persistence; this frontend uses the same facade through its pipe.
        _ = host.DisposeAsync();
        _logger.LogInformation("Runtime ownership is held by another process; connecting through the named pipe.");
        _applicationFacade = new RuntimeClient(installationId, TimeSpan.FromSeconds(5), loggerFactory.CreateLogger<RuntimeClient>());
        return _applicationFacade;
    }

    private async Task ShutdownRuntimeAsync()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
        {
            return;
        }

        // Stop accepting IPC before disposing the one facade that owns tracking and persistence.
        if (_runtimeHost is not null)
        {
            _runtimeHost.AtomicResetPrepared -= RuntimeHost_AtomicResetPrepared;
            await _runtimeHost.DisposeAsync();
            _runtimeHost = null;
        }

        if (_applicationFacade is not null)
        {
            await _applicationFacade.DisposeAsync();
            _applicationFacade = null;
        }

        _runtimeApplication = null;
    }

    private void MainWindow_AtomicResetPrepared(object? sender, AtomicResetPreparedEventArgs e) =>
        BeginAtomicReset(e.Plan);

    private void RuntimeHost_AtomicResetPrepared(AtomicResetPlan plan)
    {
        _ = _dispatcherQueue.TryEnqueue(() => BeginAtomicReset(plan));
    }

    private void BeginAtomicReset(AtomicResetPlan plan)
    {
        if (Interlocked.Exchange(ref _atomicResetStarted, 1) != 0)
        {
            return;
        }

        _ = CompleteAtomicResetAsync(plan, ownsRuntime: _runtimeHost is not null);
    }

    private async Task CompleteAtomicResetAsync(AtomicResetPlan plan, bool ownsRuntime)
    {
        try
        {
            _window?.Close();
            if (ownsRuntime)
            {
                // A remote frontend receives the response first and gets a short window to release its log sink.
                await Task.Delay(TimeSpan.FromMilliseconds(750));
            }

            await ShutdownRuntimeAsync();
            await LoggingBootstrapper.ShutdownAsync(_services);
            if (ownsRuntime)
            {
                _atomicReset.ExecuteAndRelaunch(plan);
            }
        }
        catch (Exception exception)
        {
            // Logging may already be closed because deletion must run without open handles.
            System.Diagnostics.Debug.WriteLine($"TrackMeUp atomic reset failed: {exception.GetType().Name}");
            Environment.ExitCode = 1;
        }
        finally
        {
            Exit();
        }
    }

    private async Task RunCliAndExitAsync(string[] arguments)
    {
        try
        {
            var executable = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "TrackMeUp.exe");
            Environment.ExitCode = await CliBootstrap.RunAsync(arguments, executable);
        }
        finally
        {
            // Short-lived CLI processes explicitly flush and dispose providers before WinUI exits.
            await LoggingBootstrapper.ShutdownAsync(_services);
            Exit();
        }
    }
}

internal static class StartupActivationPolicy
{
    internal static LaunchOptions Apply(LaunchOptions options, ExtendedActivationKind activationKind)
    {
        ArgumentNullException.ThrowIfNull(options);
        return activationKind == ExtendedActivationKind.StartupTask
            ? options with { Mode = LaunchMode.Ui, StartWithWindows = true }
            : options;
    }
}
