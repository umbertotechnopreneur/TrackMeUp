using Microsoft.UI.Xaml.Controls;

namespace TrackMeUp.Controls;

/// <summary>Displays the screenshot page title, counter, and date selector.</summary>
public sealed partial class ScreenshotHeaderControl : UserControl
{
    /// <summary>Creates the header control.</summary>
    public ScreenshotHeaderControl() => InitializeComponent();

    /// <summary>Gets the date picker used by the host window.</summary>
    public CalendarDatePicker DatePicker => SelectedDatePicker;

    /// <summary>Gets the text element that displays the screenshot count.</summary>
    public TextBlock CountText => GalleryCountText;
}
