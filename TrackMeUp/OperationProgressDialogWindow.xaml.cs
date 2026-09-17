// SPDX-License-Identifier: MIT

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Shows one facade operation behind a non-dismissible, indeterminate Mica progress surface.</summary>
internal sealed partial class OperationProgressDialogWindow : Window
{
    private const int LogicalWidth = 520;
    private const int LogicalHeight = 240;
    private const int LogicalScreenMargin = 24;
    private readonly Func<CancellationToken, Task> _operation;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly WindowSurfaceLifecycle _lifecycle = new();
    private readonly AppWindow _appWindow;
    private readonly CustomTitleBarController _titleBar;
    private readonly WindowPlacementService _placement;
    private bool _loaded;
    private bool _allowClose;
    private bool _closed;

    /// <summary>Creates an owned progress surface whose supplied operation uses the shared application facade.</summary>
    internal OperationProgressDialogWindow(
        ITrackMeUpApplication application,
        ElementTheme theme,
        string title,
        string description,
        string language,
        AppWindow ownerAppWindow,
        IntPtr ownerHandle,
        Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(ownerAppWindow);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        if (ownerHandle == IntPtr.Zero)
        {
            throw new ArgumentException("The progress window requires a valid owner handle.", nameof(ownerHandle));
        }

        InitializeComponent();
        Title = title;
        RootGrid.RequestedTheme = theme;
        RootGrid.Language = language;
        DialogTitleText.Text = title;
        DescriptionText.Text = description;
        AutomationProperties.SetName(RootGrid, title);
        AutomationProperties.SetName(DescriptionText, description);
        AutomationProperties.SetName(OperationProgress, title);
        WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowHandle));
        _titleBar = new CustomTitleBarController(
            this,
            _appWindow,
            RootGrid,
            TitleDragRegion,
            TitleBarLeftInsetColumn,
            TitleBarRightInsetColumn,
            static () => Array.Empty<FrameworkElement>());
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.Dialog,
            LogicalWidth,
            LogicalHeight,
            LogicalScreenMargin,
            ownerAppWindow.Id,
            deferNativeClose: false);
        WindowInteropService.SetOwner(WindowHandle, ownerHandle);
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        _lifecycle.InitializationFailed += CompleteWithException;
        _appWindow.Closing += AppWindow_Closing;
        Closed += OperationProgressDialogWindow_Closed;
    }

    /// <summary>Gets the native handle used by the shared modal queue.</summary>
    internal IntPtr WindowHandle { get; }

    /// <summary>Shows the surface and observes the operation until completion, failure, or shutdown.</summary>
    internal Task ShowAsync()
    {
        _lifecycle.StartInitialization(RunOperationAsync);
        WindowInteropService.MakeTopmostWithoutActivation(WindowHandle);
        Activate();
        return _completion.Task;
    }

    /// <summary>Allows owner or application shutdown to cancel the request and release the modal surface.</summary>
    internal void CloseForShutdown()
    {
        if (_closed)
        {
            return;
        }

        _allowClose = true;
        _lifecycle.Cancel();
        Close();
    }

    /// <summary>Releases placement and any remaining window after the modal session ends.</summary>
    internal void DisposePlacement()
    {
        if (!_closed)
        {
            CloseForShutdown();
        }
        _placement.Dispose();
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded || _closed)
        {
            return;
        }

        _loaded = true;
        try
        {
            // Progress is transient: use shared DPI-aware bounds without reading or writing another dialog's geometry.
            _placement.ApplyDefaultBounds(RootGrid);
            _lifecycle.SignalLoaded();
        }
        catch (Exception exception)
        {
            // Failed placement is reported to the caller; the operation must not run on an unavailable surface.
            CompleteWithException(exception);
        }
    }

    private async Task RunOperationAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitUntilLoadedAsync(cancellationToken);
        var visibleFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Defer the facade call until Loaded has returned and WinUI has had a chance to compose progress.
        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => visibleFrame.TrySetResult()))
        {
            throw new InvalidOperationException("The progress operation could not be queued.");
        }
        await visibleFrame.Task.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await _operation(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        _allowClose = true;
        _completion.TrySetResult();
        Close();
    }

    private void CompleteWithException(Exception exception)
    {
        if (_closed)
        {
            return;
        }

        // The caller owns the error message; close progress and propagate the observed failure unchanged.
        _allowClose = true;
        _completion.TrySetException(exception);
        Close();
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args) =>
        args.Cancel = !_allowClose;

    private void OperationProgressDialogWindow_Closed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _appWindow.Closing -= AppWindow_Closing;
        Closed -= OperationProgressDialogWindow_Closed;
        _lifecycle.Cancel();
        _completion.TrySetCanceled(_lifecycle.Token);
        _titleBar.Dispose();
        _lifecycle.Dispose();
    }
}
