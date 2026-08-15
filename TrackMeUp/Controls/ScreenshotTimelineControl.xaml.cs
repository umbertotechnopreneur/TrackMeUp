using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Displays a virtualized screenshot timeline and synchronizes its passive selection state.</summary>
public sealed partial class ScreenshotTimelineControl : UserControl
{
    private IReadOnlyList<ScreenshotTimelineEntry> _entries = Array.Empty<ScreenshotTimelineEntry>();
    private ScrollViewer? _timelineScroller;
    private bool _updatingSelection;
    private LocalizationService _strings = new("system");

    /// <summary>Creates the timeline control.</summary>
    public ScreenshotTimelineControl() => InitializeComponent();

    /// <summary>Occurs when the user chooses a different retained screenshot.</summary>
    public event Action<int>? SelectedIndexChanged;

    /// <summary>Gets the root timeline container.</summary>
    public StackPanel TimelineRoot => FilmstripStrip;

    /// <summary>Gets the virtualizing horizontal screenshot list.</summary>
    public ListView ItemsView => FilmstripList;

    /// <summary>Gets the optional strip toggle button.</summary>
    public Button ToggleButton => FilmstripToggleButton;

    /// <summary>Gets the icon used by the optional strip toggle button.</summary>
    public FontIcon ToggleChevronIcon => FilmstripChevronIcon;

    /// <summary>Replaces the lightweight timeline projection without eagerly decoding screenshot files.</summary>
    public void SetItems(IReadOnlyList<ScreenshotGalleryItem> items, int selectedIndex, string language)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _strings = new LocalizationService(language);
        if (items.Count == 0 && selectedIndex != -1)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex), selectedIndex, "An empty timeline must use selection index -1.");
        }

        if (items.Count > 0 && (selectedIndex < 0 || selectedIndex >= items.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex), selectedIndex, "The selected screenshot must exist in the timeline.");
        }

        _entries = items
            .Select(CreateEntry)
            .ToArray();

        _updatingSelection = true;
        try
        {
            FilmstripList.ItemsSource = _entries;
            FilmstripList.SelectedIndex = selectedIndex;
        }
        finally
        {
            _updatingSelection = false;
        }

        BringSelectionIntoView();
        QueueNavigationAvailabilityUpdate();
    }

    /// <summary>Synchronizes the selected thumbnail with another gallery surface.</summary>
    public void SetSelectedIndex(int selectedIndex)
    {
        if (_entries.Count == 0 && selectedIndex == -1)
        {
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= _entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex), selectedIndex, "The selected screenshot must exist in the timeline.");
        }

        if (FilmstripList.SelectedIndex == selectedIndex)
        {
            return;
        }

        _updatingSelection = true;
        try
        {
            FilmstripList.SelectedIndex = selectedIndex;
        }
        finally
        {
            _updatingSelection = false;
        }

        BringSelectionIntoView();
    }

    private ScreenshotTimelineEntry CreateEntry(ScreenshotGalleryItem item, int index)
    {
        if (!Uri.TryCreate(item.Path, UriKind.Absolute, out _))
        {
            throw new InvalidDataException($"Screenshot path is not an absolute URI: {item.Path}");
        }

        var localTime = item.CapturedAt.ToLocalTime();
        return new ScreenshotTimelineEntry(
            item.Path,
            _strings.Format("Screenshots.Timeline.Date", localTime),
            _strings.Format("Screenshots.Timeline.Time", localTime),
            _strings.Format("Screenshots.Timeline.ItemAccessible", index + 1, localTime));
    }

    private void FilmstripList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingSelection || FilmstripList.SelectedIndex < 0)
        {
            return;
        }

        SelectedIndexChanged?.Invoke(FilmstripList.SelectedIndex);
    }

    private void BringSelectionIntoView()
    {
        if (FilmstripList.SelectedItem is not { } selectedItem)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            FilmstripList.ScrollIntoView(selectedItem, ScrollIntoViewAlignment.Default);
            QueueNavigationAvailabilityUpdate();
        });
    }

    private void FilmstripList_Loaded(object sender, RoutedEventArgs e)
    {
        AttachTimelineScroller();
        QueueNavigationAvailabilityUpdate();
    }

    private void FilmstripList_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_timelineScroller is not null)
        {
            _timelineScroller.ViewChanged -= TimelineScroller_ViewChanged;
            _timelineScroller = null;
        }
    }

    private void FilmstripList_SizeChanged(object sender, SizeChangedEventArgs e)
        => QueueNavigationAvailabilityUpdate();

    private void PreviousTimelineButton_Click(object sender, RoutedEventArgs e) => ScrollTimeline(-1);

    private void NextTimelineButton_Click(object sender, RoutedEventArgs e) => ScrollTimeline(1);

    private void ScrollTimeline(int direction)
    {
        if (direction is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        AttachTimelineScroller();
        if (_timelineScroller is not { } scroller)
        {
            return;
        }

        var page = Math.Max(118d, scroller.ViewportWidth * 0.72d);
        var targetOffset = Math.Clamp(
            scroller.HorizontalOffset + (direction * page),
            0d,
            scroller.ScrollableWidth);
        scroller.ChangeView(targetOffset, null, null, disableAnimation: false);
    }

    private void AttachTimelineScroller()
    {
        var scroller = FindDescendant<ScrollViewer>(FilmstripList);
        if (ReferenceEquals(scroller, _timelineScroller))
        {
            return;
        }

        if (_timelineScroller is not null)
        {
            _timelineScroller.ViewChanged -= TimelineScroller_ViewChanged;
        }

        _timelineScroller = scroller;
        if (_timelineScroller is not null)
        {
            _timelineScroller.ViewChanged += TimelineScroller_ViewChanged;
        }
    }

    private void TimelineScroller_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        => UpdateNavigationAvailability();

    private void QueueNavigationAvailabilityUpdate()
    {
        if (!DispatcherQueue.TryEnqueue(() =>
            {
                AttachTimelineScroller();
                UpdateNavigationAvailability();
            }))
        {
            UpdateNavigationAvailability();
        }
    }

    private void UpdateNavigationAvailability()
    {
        var scroller = _timelineScroller;
        var hasOverflow = scroller is not null
            && scroller.ExtentWidth > Math.Max(TimelineNavigationRail.ActualWidth, scroller.ViewportWidth) + 0.5d;
        var visibility = hasOverflow ? Visibility.Visible : Visibility.Collapsed;
        PreviousTimelineButton.Visibility = visibility;
        NextTimelineButton.Visibility = visibility;
        PreviousTimelineButton.IsEnabled = hasOverflow && scroller!.HorizontalOffset > 0.5d;
        NextTimelineButton.IsEnabled = hasOverflow
            && scroller!.HorizontalOffset < scroller.ScrollableWidth - 0.5d;
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nestedMatch)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private void TimelineImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image)
        {
            SetTimelineImageSource(image);
        }
    }

    private void TimelineImage_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is Image image)
        {
            SetTimelineImageSource(image);
        }
    }

    private void TimelineImage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image)
        {
            image.Source = null;
        }
    }

    private static void SetTimelineImageSource(Image image)
    {
        if (image.DataContext is not ScreenshotTimelineEntry entry)
        {
            image.Source = null;
            return;
        }

        var sourceUri = new Uri(entry.Path, UriKind.Absolute);
        if (image.Source is BitmapImage { UriSource: { } currentUri }
            && currentUri.Equals(sourceUri))
        {
            return;
        }

        // Only a realized ListView container reaches this path; recycled thumbnails release their decoded bitmap on Unloaded.
        image.Source = new BitmapImage
        {
            DecodePixelWidth = 220,
            UriSource = sourceUri
        };
    }

    private sealed record ScreenshotTimelineEntry(
        string Path,
        string DateText,
        string TimeText,
        string AutomationName);
}
