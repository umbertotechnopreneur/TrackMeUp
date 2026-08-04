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

namespace TrackMeUp;

/// <summary>Provides the Windows composition root and selects a launch mode before any view is created.</summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly ServiceProvider _services;
    private readonly ILogger<App> _logger;
    private MainWindow? _window;
    private TaskbarWidgetWindow? _taskbarWidgetWindow;
    private TaskbarWidgetHost? _taskbarWidgetHost;
    private RuntimeHost? _runtimeHost;
    private ITrackMeUpApplication? _runtimeApplication;

    /// <summary>Initializes the WinUI application object and its logging composition root.</summary>
    public App()
    {
        _services = LoggingBootstrapper.CreateServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();
        InitializeComponent();
        UnhandledException += (_, eventArgs) => _logger.LogCritical(eventArgs.Exception, "Unhandled WinUI exception.");
        _logger.LogInformation("TrackMeUp process started. Architecture={Architecture}", RuntimeInformation.ProcessArchitecture);
    }

    /// <summary>Routes launch modes to the CLI, background runtime, or WinUI player.</summary>
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
        var application = StartOrConnectRuntime();
        _window = new MainWindow(application, options);
        _window.SettingsApplied += ApplyTaskbarWidgetSettings;
        _window.Closed += (_, _) => DisposeTaskbarWidget();
        _taskbarWidgetWindow = new TaskbarWidgetWindow(application);
        _taskbarWidgetWindow.FlyoutRequested += (_, _) => _window?.ShowFlyout();
        _window.Activate();
        _taskbarWidgetWindow.Activate();
        _taskbarWidgetWindow.PrepareForTaskbar();

        var settings = application.GetSettingsAsync(CancellationToken.None).GetAwaiter().GetResult().Value;
        if (settings is null)
        {
            DisposeTaskbarWidget();
            return;
        }

        _taskbarWidgetWindow.ApplySettings(settings);
        _taskbarWidgetHost = new TaskbarWidgetHost();
        var widgetHandle = WinRT.Interop.WindowNative.GetWindowHandle(_taskbarWidgetWindow);
        if (_taskbarWidgetHost.Attach(widgetHandle, settings.TaskbarWidgetPosition))
        {
            _taskbarWidgetHost.HideTopLevelWindow(WinRT.Interop.WindowNative.GetWindowHandle(_window));
        }
        else
        {
            // If a custom shell rejects parenting, keep the normal player usable rather than leaving an orphaned top-level control.
            DisposeTaskbarWidget();
        }
    }

    private void ApplyTaskbarWidgetSettings(AppSettings settings)
    {
        _taskbarWidgetWindow?.ApplySettings(settings);
        _taskbarWidgetHost?.Configure(settings.TaskbarWidgetPosition);
    }

    private void DisposeTaskbarWidget()
    {
        _taskbarWidgetHost?.Dispose();
        _taskbarWidgetHost = null;
        _taskbarWidgetWindow?.Close();
        _taskbarWidgetWindow = null;
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
            return localApplication;
        }

        // A separate process owns hooks and persistence; this frontend uses the same facade through its pipe.
        _ = localApplication.DisposeAsync();
        _ = host.DisposeAsync();
        _logger.LogInformation("Runtime ownership is held by another process; connecting through the named pipe.");
        return new RuntimeClient(settings.InstallationId, TimeSpan.FromSeconds(5), loggerFactory.CreateLogger<RuntimeClient>());
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
