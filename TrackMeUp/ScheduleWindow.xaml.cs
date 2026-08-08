using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Services;
using Windows.Graphics;

namespace TrackMeUp;

/// <summary>Collects screenshot scheduling input in a detached themed window.</summary>
public sealed partial class ScheduleWindow : Window
{
    private const int LogicalWindowWidth = 860;
    private const int LogicalWindowHeight = 700;
    private const int LogicalScreenMargin = 24;
    private readonly AppWindow _appWindow;
    private readonly MicaDialogService _dialogs;
    private LocalizationService _strings;

    /// <summary>Occurs after the user confirms a valid screenshot schedule.</summary>
    public event EventHandler<ScheduleConfigurationEventArgs>? ScheduleConfirmed;

    /// <summary>Creates a detached schedule editor with the supplied working hours, interval and theme.</summary>
    internal ScheduleWindow(
        IReadOnlyList<ActiveHoursDay>? activeHours,
        int intervalMinutes,
        string theme,
        string uiLanguage,
        MicaDialogService dialogs)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _strings = new LocalizationService(uiLanguage);
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        ApplyTheme(theme);
        ApplyLanguage(uiLanguage);
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

    /// <summary>Applies localized labels to schedule-specific commands and confirmations.</summary>
    public void ApplyLanguage(string uiLanguage)
    {
        _strings = new LocalizationService(uiLanguage);
        StandardWorkWeekButton.Content = _strings.Translate("Schedule.Preset.WorkWeek");
        ClearAllHoursButton.Content = _strings.Translate("Schedule.ClearAll");
        AutomationProperties.SetName(StandardWorkWeekButton, _strings.Translate("Schedule.Preset.WorkWeek.Title"));
        AutomationProperties.SetName(ClearAllHoursButton, _strings.Translate("Schedule.ClearAll.Title"));
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e) => ResizeAndCenter();

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    private async void StandardWorkWeekButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmReplacementAsync("Schedule.Preset.WorkWeek.Title", "Schedule.Preset.WorkWeek.Message"))
        {
            WorkingHoursEditor.ApplyStandardWorkWeek();
        }
    }

    private async void ClearAllHoursButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ConfirmReplacementAsync("Schedule.ClearAll.Title", "Schedule.ClearAll.Message"))
        {
            WorkingHoursEditor.ClearAll();
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var intervalMinutes = (int)IntervalNumberBox.Value;
        ScheduleConfirmed?.Invoke(this, new ScheduleConfigurationEventArgs(intervalMinutes, WorkingHoursEditor.GetSchedule()));
    }

    private async Task<bool> ConfirmReplacementAsync(string titleKey, string messageKey)
    {
        return await _dialogs.ConfirmAsync(
            this,
            MicaDialogRequest.Confirmation(
                _strings.Translate(titleKey),
                _strings.Translate(messageKey),
                _strings.Translate("Schedule.Apply"),
                _strings.Translate("Schedule.Cancel")),
            RootGrid.RequestedTheme);
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
