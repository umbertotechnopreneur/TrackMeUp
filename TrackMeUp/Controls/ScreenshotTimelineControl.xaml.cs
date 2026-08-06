using Microsoft.UI.Xaml.Controls;

namespace TrackMeUp.Controls;

/// <summary>Displays the screenshot timeline strip and thumbnail host.</summary>
public sealed partial class ScreenshotTimelineControl : UserControl
{
    /// <summary>Creates the timeline control.</summary>
    public ScreenshotTimelineControl() => InitializeComponent();

    /// <summary>Gets the root timeline container.</summary>
    public StackPanel TimelineRoot => FilmstripStrip;

    /// <summary>Gets the horizontal thumbnail host.</summary>
    public ScrollViewer FilmstripHost => FilmstripPanelHost;

    /// <summary>Gets the panel used to append timeline items.</summary>
    public StackPanel ItemsHost => FilmstripPanel;

    /// <summary>Gets the optional strip toggle button.</summary>
    public Button ToggleButton => FilmstripToggleButton;

    /// <summary>Gets the icon used by the optional strip toggle button.</summary>
    public FontIcon ToggleChevronIcon => FilmstripChevronIcon;
}
