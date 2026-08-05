using System.Windows;
using System.Windows.Threading;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Taskbar;

/// <summary>Owns the dedicated WPF dispatcher that renders the transparent taskbar surface while Core retains taskbar interop ownership.</summary>
public sealed class TaskbarWidgetSurface : IDisposable
{
    private readonly ITrackMeUpApplication _application;
    private readonly TaskbarWidgetHost _host;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private System.Windows.Application? _wpfApplication;
    private Dispatcher? _dispatcher;
    private TaskbarWidgetWindow? _window;
    private Exception? _startupException;
    private int _disposed;

    /// <summary>Starts the dedicated WPF dispatcher and creates the alpha-capable taskbar surface.</summary>
    public TaskbarWidgetSurface(ITrackMeUpApplication application, TaskbarWidgetHost host)
    {
        _application = application;
        _host = host;
        _thread = new Thread(DispatcherThreadMain)
        {
            IsBackground = true,
            Name = "TrackMeUp taskbar surface"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Timed out while creating the TrackMeUp taskbar surface.");
        }

        if (_startupException is not null)
        {
            throw new InvalidOperationException("Unable to create the TrackMeUp taskbar surface.", _startupException);
        }
    }

    /// <summary>Occurs on the WPF dispatcher when the user asks to open the WinUI player.</summary>
    public event EventHandler? FlyoutRequested;

    /// <summary>Applies persisted presentation settings on the WPF dispatcher.</summary>
    public void ApplySettings(AppSettings settings) => Invoke(window => window.ApplySettings(settings));

    /// <summary>Parents the already-rendered transparent HWND into the taskbar through the shared Core host.</summary>
    public bool Attach(string position) => Invoke(window =>
    {
        var attached = _host.Attach(window.Handle, position);
        if (attached)
        {
            window.RevealInTaskbar();
        }

        return attached;
    });

    /// <summary>Updates only the Core host placement while preserving the WPF surface and shared runtime.</summary>
    public void Configure(string position) => Invoke(_ => _host.Configure(position));

    /// <summary>Hides the WinUI player after the transparent taskbar surface has attached successfully.</summary>
    public void HideTopLevelWindow(IntPtr windowHandle) => _host.HideTopLevelWindow(windowHandle);

    /// <summary>Closes the alpha surface and stops its WPF dispatcher after Core removes taskbar parenting.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var dispatcher = _dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted)
        {
            dispatcher.Invoke(() =>
            {
                _host.Dispose();
                _window?.Close();
                _wpfApplication?.Shutdown();
            });
        }
        else
        {
            _host.Dispose();
        }

        _ = _thread.Join(TimeSpan.FromSeconds(5));
        _ready.Dispose();
    }

    private void DispatcherThreadMain()
    {
        try
        {
            _wpfApplication = new System.Windows.Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            _dispatcher = Dispatcher.CurrentDispatcher;
            _window = new TaskbarWidgetWindow(_application);
            _window.FlyoutRequested += Window_FlyoutRequested;
            _window.PrepareForTaskbar();
            _ready.Set();
            Dispatcher.Run();
        }
        catch (Exception exception)
        {
            _startupException = exception;
            _ready.Set();
        }
    }

    private void Window_FlyoutRequested(object? sender, EventArgs e) => FlyoutRequested?.Invoke(this, EventArgs.Empty);

    private void Invoke(Action<TaskbarWidgetWindow> action)
    {
        var dispatcher = GetDispatcher();
        if (dispatcher.CheckAccess())
        {
            action(GetWindow());
            return;
        }

        dispatcher.Invoke(() => action(GetWindow()));
    }

    private TResult Invoke<TResult>(Func<TaskbarWidgetWindow, TResult> action)
    {
        var dispatcher = GetDispatcher();
        return dispatcher.CheckAccess()
            ? action(GetWindow())
            : dispatcher.Invoke(() => action(GetWindow()));
    }

    private Dispatcher GetDispatcher() => _dispatcher is { HasShutdownStarted: false } dispatcher
        ? dispatcher
        : throw new ObjectDisposedException(nameof(TaskbarWidgetSurface));

    private TaskbarWidgetWindow GetWindow() => _window ?? throw new InvalidOperationException("The taskbar surface has not been initialized.");
}
