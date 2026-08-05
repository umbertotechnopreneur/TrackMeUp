using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Taskbar;

/// <summary>Renders the alpha-capable compact taskbar controls and forwards actions to the shared application facade.</summary>
public sealed partial class TaskbarWidgetWindow : Window
{
    private readonly ITrackMeUpApplication _application;
    private readonly DispatcherTimer _refreshTimer;
    private bool _refreshInProgress;
    private LocalizationService _strings = new("system");

    /// <summary>Initializes the transparent taskbar control.</summary>
    public TaskbarWidgetWindow(ITrackMeUpApplication application)
    {
        _application = application;
        InitializeComponent();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += RefreshTimer_Tick;
        Loaded += TaskbarWidgetWindow_Loaded;
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            RecordingGlow.BeginAnimation(OpacityProperty, null);
        };
    }

    /// <summary>Occurs when the user wants to open the full TrackMeUp flyout.</summary>
    public event EventHandler? FlyoutRequested;

    /// <summary>Gets the native transparent HWND used by the shared Core taskbar host.</summary>
    internal IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>Creates the hidden WPF HWND at the logical taskbar size before Core reparents it into Explorer.</summary>
    internal void PrepareForTaskbar(TaskbarWidgetHost.TaskbarWidgetBounds bounds)
    {
        // Size and position the hidden window in WPF DIPs so its HWND adopts the taskbar monitor's DPI.
        Width = bounds.Width / bounds.Scale;
        Height = bounds.Height / bounds.Scale;
        Left = bounds.ScreenX / bounds.Scale;
        Top = bounds.ScreenY / bounds.Scale;
        Opacity = 1;
        _ = Handle;
    }

    /// <summary>Applies presentation colors from the persisted app theme without accessing external state.</summary>
    internal void ApplySettings(AppSettings settings)
    {
        _strings = new LocalizationService(settings.UiLanguage);
        var isLight = settings.Theme == "light";
        var foreground = new SolidColorBrush(isLight
            ? Color.FromRgb(23, 59, 63)
            : Color.FromRgb(244, 245, 241));
        PlayPauseIcon.Foreground = foreground;
    }

    private async void TaskbarWidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDashboardAsync();
        _refreshTimer.Start();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e) => await RefreshDashboardAsync();

    private void OpenFlyoutButton_Click(object sender, RoutedEventArgs e) => FlyoutRequested?.Invoke(this, EventArgs.Empty);

    private async void TrackingButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await _application.ToggleTrackingAsync(CancellationToken.None);
        if (result.Succeeded && result.Value is not null)
        {
            UpdateWidget(result.Value);
        }
    }

    private async Task RefreshDashboardAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        try
        {
            var result = await _application.GetDashboardAsync(CancellationToken.None);
            if (result.Succeeded && result.Value is not null)
            {
                UpdateWidget(result.Value);
            }
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void UpdateWidget(DashboardState state)
    {
        PlayPauseIcon.Text = state.IsTracking ? "\uE769" : "\uE768";
        var actionName = _strings.Translate(state.IsTracking ? "TrackingActionPause" : "TrackingActionStart");
        TrackingButton.ToolTip = actionName;
        AutomationProperties.SetName(TrackingButton, actionName);
        SetRecordingVisual(state.IsTracking);
    }

    private void SetRecordingVisual(bool isTracking)
    {
        RecordingGlow.BeginAnimation(OpacityProperty, null);
        if (!isTracking)
        {
            RecordingLed.Fill = new SolidColorBrush(Color.FromRgb(119, 128, 141));
            RecordingGlow.Opacity = 0;
            RecordingIndicator.ToolTip = _strings.Translate("StatePaused");
            AutomationProperties.SetName(RecordingIndicator, _strings.Translate("StatePaused"));
            return;
        }

        RecordingLed.Fill = new SolidColorBrush(Color.FromRgb(244, 61, 75));
        RecordingIndicator.ToolTip = _strings.Translate("StateRunning");
        AutomationProperties.SetName(RecordingIndicator, _strings.Translate("StateRunning"));
        RecordingGlow.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = 0.16,
                To = 0.48,
                Duration = TimeSpan.FromMilliseconds(1100),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            });
    }
}
