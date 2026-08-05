using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Collects screenshot scheduling input in a detached themed window.</summary>
public sealed partial class ScheduleWindow : Window
{
    private const int LogicalWindowWidth = 860;
    private const int LogicalWindowHeight = 700;
    private const int LogicalScreenMargin = 24;
    private readonly AppWindow _appWindow;

    /// <summary>Occurs after the user confirms a valid screenshot schedule.</summary>
    public event EventHandler<ScheduleConfigurationEventArgs>? ScheduleConfirmed;

    /// <summary>Creates a detached schedule editor with the supplied working hours, interval and theme.</summary>
    public ScheduleWindow(IReadOnlyList<ActiveHoursDay>? activeHours, int intervalMinutes, string theme)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        ApplyTheme(theme);
        IntervalNumberBox.Value = intervalMinutes is >= 1 and <= 1440 ? intervalMinutes : 5;
        WorkingHoursEditor.LoadSchedule(activeHours);
        ResizeAndCenter();
    }

    /// <summary>Applies the active application theme to the detached schedule editor.</summary>
    public void ApplyTheme(string theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e) => ResizeAndCenter();

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var intervalMinutes = (int)IntervalNumberBox.Value;
        ScheduleConfirmed?.Invoke(this, new ScheduleConfigurationEventArgs(intervalMinutes, WorkingHoursEditor.GetSchedule()));
    }

    private void ResizeAndCenter()
    {
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? 1d);
        var workArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var margin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var width = Math.Min(workArea.Width - (margin * 2), (int)Math.Ceiling(LogicalWindowWidth * scale));
        var height = Math.Min(workArea.Height - (margin * 2), (int)Math.Ceiling(LogicalWindowHeight * scale));
        _appWindow.Resize(new SizeInt32(Math.Max(1, width), Math.Max(1, height)));
        _appWindow.Move(new PointInt32(
            workArea.X + Math.Max(0, (workArea.Width - _appWindow.Size.Width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - _appWindow.Size.Height) / 2)));
    }
}

/// <summary>Provides the screenshot scheduling values confirmed by the detached editor.</summary>
public sealed class ScheduleConfigurationEventArgs(int intervalMinutes, IReadOnlyList<ActiveHoursDay> activeHours) : EventArgs
{
    /// <summary>Gets the interval in minutes between eligible snapshots.</summary>
    public int IntervalMinutes { get; } = intervalMinutes;

    /// <summary>Gets the selected active periods and breaks for each day.</summary>
    public IReadOnlyList<ActiveHoursDay> ActiveHours { get; } = activeHours;
}
