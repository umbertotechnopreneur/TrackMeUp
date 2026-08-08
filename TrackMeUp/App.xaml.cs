using Microsoft.UI.Xaml;
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
using TrackMeUp.Runtime;
using TrackMeUp.Services;
using TaskbarWidgetSurface = TrackMeUp.Taskbar.TaskbarWidgetSurface;

namespace TrackMeUp;

/// <summary>Provides the Windows composition root and selects a launch mode before any view is created.</summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly ServiceProvider _services;
    private readonly ILogger<App> _logger;
    private MainWindow? _window;
    private ReportsWindow? _reportsWindow;
    private ScreenshotWindow? _screenshotsWindow;
    private TaskbarWidgetSurface? _taskbarWidgetSurface;
    private RuntimeHost? _runtimeHost;
    private ITrackMeUpApplication? _runtimeApplication;
    private ITrackMeUpApplication? _applicationFacade;
    private bool _reportsOnly;
    private int _shutdownStarted;

    /// <summary>Initializes the WinUI application object and its logging composition root.</summary>
    public App()
    {
        _services = LoggingBootstrapper.CreateServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();
        InitializeComponent();
        UnhandledException += (_, eventArgs) => _logger.LogCritical(eventArgs.Exception, "Unhandled WinUI exception.");
        _logger.LogInformation("TrackMeUp process started. Architecture={Architecture}", RuntimeInformation.ProcessArchitecture);
    }

    /// <summary>Routes launch modes to the CLI, background runtime, reports, or WinUI player.</summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var options = LaunchOptions.Parse(Environment.GetCommandLineArgs().Skip(1).ToArray());
            _logger.LogInformation("Launch requested. Mode={Mode}", options.Mode);
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

    private void StartUi(LaunchOptions options)
    {
        _reportsOnly = false;
        var application = StartOrConnectRuntime();
        _window = new MainWindow(application, options);
        _window.SettingsApplied += ApplyTaskbarWidgetSettings;
        _window.ReportsRequested += MainWindow_ReportsRequested;
        _window.ScreenshotGalleryRequested += MainWindow_ScreenshotGalleryRequested;
        _window.ScreenshotsRequested += MainWindow_ScreenshotsRequested;
        _window.Closed += MainWindow_Closed;
        _window.Activate();

        var settings = application.GetSettingsAsync(CancellationToken.None).GetAwaiter().GetResult().Value;
        if (settings is null)
        {
            DisposeTaskbarWidget();
            return;
        }

        ApplyTaskbarWidgetSettings(settings);
    }

    private void StartReports(LaunchOptions options)
    {
        _reportsOnly = true;
        ShowReportsWindow(StartOrConnectRuntime(), options.Theme);
    }

    private void MainWindow_ReportsRequested(object? sender, EventArgs eventArgs) => ShowReportsWindow(StartOrConnectRuntime(), null);

    private async void MainWindow_ScreenshotGalleryRequested(object? sender, EventArgs eventArgs)
        => await ShowScreenshotWindowAsync(StartOrConnectRuntime(), null);

    private async void MainWindow_ScreenshotsRequested(object? sender, ScreenshotPreviewRequestedEventArgs eventArgs)
        => await ShowScreenshotWindowAsync(StartOrConnectRuntime(), null, eventArgs.ScreenshotPath, eventArgs.CapturedAt);

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
            _window.ReportsRequested -= MainWindow_ReportsRequested;
            _window.ScreenshotGalleryRequested -= MainWindow_ScreenshotGalleryRequested;
            _window.ScreenshotsRequested -= MainWindow_ScreenshotsRequested;
            _window.Closed -= MainWindow_Closed;
            _window = null;
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
            _screenshotsWindow.Close();
            _screenshotsWindow = null;
        }

        DisposeTaskbarWidget();
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

    private void StartBackgroundRuntime(LaunchOptions options)
    {
        var application = StartOrConnectRuntime();
        if (ReferenceEquals(application, _runtimeApplication) && options.StartTracking && !options.Paused)
        {
            _ = application.StartTrackingAsync(new StartTrackingRequest(options.SafeMode, "background"), CancellationToken.None);
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
        var localApplication = TrackMeUpApplicationFactory.Create(loggerFactory, observability);
        var settings = localApplication.GetSettingsAsync(CancellationToken.None).GetAwaiter().GetResult().Value ?? new AppSettings();
        var host = new RuntimeHost(localApplication, settings.InstallationId, loggerFactory.CreateLogger<RuntimeHost>());
        if (host.TryStart())
        {
            _logger.LogInformation("Runtime ownership acquired for this installation.");
            _runtimeHost = host;
            _runtimeApplication = localApplication;
            _applicationFacade = localApplication;
            return _applicationFacade;
        }

        // A separate process owns hooks and persistence; this frontend uses the same facade through its pipe.
        _ = localApplication.DisposeAsync();
        _ = host.DisposeAsync();
        _logger.LogInformation("Runtime ownership is held by another process; connecting through the named pipe.");
        _applicationFacade = new RuntimeClient(settings.InstallationId, TimeSpan.FromSeconds(5), loggerFactory.CreateLogger<RuntimeClient>());
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
