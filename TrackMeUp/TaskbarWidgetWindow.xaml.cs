using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Renders the compact taskbar controls and forwards all tracking actions to the shared application facade.</summary>
public sealed partial class TaskbarWidgetWindow : Window
{
    private readonly ITrackMeUpApplication _application;
    private readonly DispatcherQueueTimer _refreshTimer;
    private Storyboard? _recordingPulse;
    private bool _refreshInProgress;

    /// <summary>Initializes the compact taskbar control.</summary>
    public TaskbarWidgetWindow(ITrackMeUpApplication application)
    {
        _application = application;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarRegion);

        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(1);
        _refreshTimer.Tick += async (_, _) => await RefreshDashboardAsync();
        RootGrid.Loaded += TaskbarWidgetWindow_Loaded;
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _recordingPulse?.Stop();
        };
    }

    /// <summary>Occurs when the user wants to open the full TrackMeUp flyout.</summary>
    public event EventHandler? FlyoutRequested;

    /// <summary>Sizes this UI surface before the Core host reparents it into Explorer.</summary>
    public void PrepareForTaskbar()
    {
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        appWindow.Resize(new SizeInt32(TaskbarWidgetHost.LogicalWidth, TaskbarWidgetHost.LogicalHeight));
    }

    /// <summary>Applies visual settings already validated and persisted by the application layer.</summary>
    public void ApplySettings(AppSettings settings) => RootGrid.RequestedTheme = settings.Theme switch
    {
        "light" => ElementTheme.Light,
        "dark" => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    private async void TaskbarWidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshDashboardAsync();
        _refreshTimer.Start();
    }

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
        PlayPauseIcon.Glyph = state.IsTracking ? "\uE769" : "\uE768";
        var actionName = state.IsTracking ? "Metti in pausa la registrazione" : "Avvia registrazione sessione";
        ToolTipService.SetToolTip(TrackingButton, actionName);
        AutomationProperties.SetName(TrackingButton, actionName);
        SetRecordingVisual(state.IsTracking);
    }

    private void SetRecordingVisual(bool isTracking)
    {
        _recordingPulse?.Stop();
        if (!isTracking)
        {
            RecordingLed.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 119, 128, 141));
            RecordingGlow.Opacity = 0;
            ToolTipService.SetToolTip(RecordingIndicator, "Registrazione in pausa");
            AutomationProperties.SetName(RecordingIndicator, "Registrazione in pausa");
            return;
        }

        RecordingLed.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 61, 75));
        ToolTipService.SetToolTip(RecordingIndicator, "Registrazione sessione in corso");
        AutomationProperties.SetName(RecordingIndicator, "Registrazione sessione in corso");
        var glowAnimation = new DoubleAnimation
        {
            From = 0.16,
            To = 0.48,
            Duration = new Duration(TimeSpan.FromMilliseconds(1100)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(glowAnimation, RecordingGlow);
        Storyboard.SetTargetProperty(glowAnimation, "Opacity");
        _recordingPulse = new Storyboard();
        _recordingPulse.Children.Add(glowAnimation);
        _recordingPulse.Begin();
    }
}
