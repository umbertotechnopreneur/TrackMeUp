using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TrackMeUp;

/// <summary>Dialog for configuring automatic screenshot scheduling interval.</summary>
public sealed partial class ScheduleScreenshotDialog : ContentDialog
{
    /// <summary>Gets the interval in minutes selected by the user.</summary>
    public int IntervalMinutes { get; private set; }

    /// <summary>Gets the weekly working-hours schedule selected by the user.</summary>
    public IReadOnlyList<ActiveHoursDay> ActiveHours { get; private set; } = Array.Empty<ActiveHoursDay>();

    /// <summary>Creates the screenshot scheduling dialog.</summary>
    public ScheduleScreenshotDialog(IReadOnlyList<ActiveHoursDay>? activeHours)
    {
        InitializeComponent();
        WorkingHoursEditor.LoadSchedule(activeHours);
    }

    private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        IntervalMinutes = (int)IntervalNumberBox.Value;
        ActiveHours = WorkingHoursEditor.GetSchedule();
    }
}
