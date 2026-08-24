using System.Numerics;
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
    private const float SelectedTimelineScale = 1.2f;
    private const double EstimatedTimelineContainerWidth = 184d;
    private const double EstimatedTimelineContainerMargin = 4d;
    private const int CenteringPassLimit = 4;

    private IReadOnlyList<ScreenshotTimelineEntry> _entries = Array.Empty<ScreenshotTimelineEntry>();
    private ScrollViewer? _timelineScroller;
    private int _selectionCenterGeneration;
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

    private ScreenshotTimelineEntry CreateEntry(ScreenshotGalleryItem item, int index)
    {
        if (!Uri.TryCreate(item.Path, UriKind.Absolute, out _))
        {
            throw new InvalidDataException($"Screenshot path is not an absolute URI: {item.Path}");
        }

        var localTime = item.CapturedAt.ToLocalTime();
        var installation = item.Installation
            ?? throw new InvalidDataException("Screenshot installation provenance is required by the timeline.");
        return new ScreenshotTimelineEntry(
            item.Path,
            _strings.Format("Screenshots.Timeline.Time", localTime),
            $"{_strings.Format("Screenshots.Timeline.ItemAccessible", index + 1, localTime)} · {installation.FriendlyName} · {installation.MachineName}",
            InstallationAppearance.CreateAccentBrush(installation.Color),
            InstallationAppearance.GetIconGlyph(installation.Icon));
    }

    private void FilmstripList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateChangedContainerVisuals(e);
        if (FilmstripList.SelectedIndex < 0)
        {
            return;
        }

        BringSelectionIntoView();
        if (_updatingSelection)
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

        var generation = ++_selectionCenterGeneration;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsCurrentCenteringRequest(selectedItem, generation))
            {
                return;
            }

            AttachTimelineScroller();
            UpdateItemsPanelEdgeMargins(selectedContainer: null);
            FilmstripList.ScrollIntoView(selectedItem, ScrollIntoViewAlignment.Leading);
            QueueCenteringPass(selectedItem, generation, pass: 0);
        });
    }

    private void QueueCenteringPass(object selectedItem, int generation, int pass)
    {
        _ = DispatcherQueue.TryEnqueue(() => CenterSelection(selectedItem, generation, pass));
    }

    private void CenterSelection(object selectedItem, int generation, int pass)
    {
        if (!IsCurrentCenteringRequest(selectedItem, generation))
        {
            return;
        }

        AttachTimelineScroller();
        if (_timelineScroller is not { ViewportWidth: > 0d } scroller
            || FilmstripList.ContainerFromItem(selectedItem) is not ListViewItem selectedContainer
            || selectedContainer.ActualWidth <= 0d)
        {
            RetryCentering(selectedItem, generation, pass);
            return;
        }

        if (UpdateItemsPanelEdgeMargins(selectedContainer))
        {
            FilmstripList.ScrollIntoView(selectedItem, ScrollIntoViewAlignment.Leading);
            RetryCentering(selectedItem, generation, pass);
            return;
        }

        var itemCenter = selectedContainer
            .TransformToVisual(scroller)
            .TransformPoint(new Windows.Foundation.Point(selectedContainer.ActualWidth / 2d, 0d))
            .X;
        var targetOffset = Math.Clamp(
            scroller.HorizontalOffset + itemCenter - (scroller.ViewportWidth / 2d),
            0d,
            scroller.ScrollableWidth);
        scroller.ChangeView(targetOffset, null, null, disableAnimation: false);
        QueueNavigationAvailabilityUpdate();
    }

    private void RetryCentering(object selectedItem, int generation, int pass)
    {
        if (pass < CenteringPassLimit)
        {
            QueueCenteringPass(selectedItem, generation, pass + 1);
        }
    }

    private bool IsCurrentCenteringRequest(object selectedItem, int generation)
        => generation == _selectionCenterGeneration
            && ReferenceEquals(FilmstripList.SelectedItem, selectedItem);

    private bool UpdateItemsPanelEdgeMargins(ListViewItem? selectedContainer)
    {
        if (FilmstripList.ItemsPanelRoot is not FrameworkElement panel)
        {
            return false;
        }

        var viewportWidth = _timelineScroller is { ViewportWidth: > 0d } scroller
            ? scroller.ViewportWidth
            : FilmstripList.ActualWidth;
        if (viewportWidth <= 0d)
        {
            return false;
        }

        var containerWidth = selectedContainer is { ActualWidth: > 0d }
            ? selectedContainer.ActualWidth
            : EstimatedTimelineContainerWidth;
        var containerMargin = selectedContainer?.Margin
            ?? new Thickness(EstimatedTimelineContainerMargin, 0d, EstimatedTimelineContainerMargin, 0d);
        var left = Math.Max(0d, (viewportWidth / 2d) - (containerWidth / 2d) - containerMargin.Left);
        var right = Math.Max(0d, (viewportWidth / 2d) - (containerWidth / 2d) - containerMargin.Right);
        var current = panel.Margin;
        if (Math.Abs(current.Left - left) < 0.5d
            && Math.Abs(current.Right - right) < 0.5d)
        {
            return false;
        }

        panel.Margin = new Thickness(left, current.Top, right, current.Bottom);
        return true;
    }

    private void FilmstripList_ContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem container)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            ApplyContainerSelectionVisual(container, isSelected: false, animate: false);
            return;
        }

        if (container.ContentTemplateRoot is not FrameworkElement)
        {
            if (args.Phase < 2u)
            {
                args.RegisterUpdateCallback(args.Phase + 1u, FilmstripList_ContainerContentChanging);
            }

            return;
        }

        ApplyContainerSelectionVisual(
            container,
            isSelected: args.ItemIndex == FilmstripList.SelectedIndex,
            animate: true);
    }

    private void UpdateChangedContainerVisuals(SelectionChangedEventArgs args)
    {
        foreach (var removedItem in args.RemovedItems)
        {
            ApplyItemSelectionVisual(removedItem, isSelected: false);
        }

        foreach (var addedItem in args.AddedItems)
        {
            ApplyItemSelectionVisual(addedItem, isSelected: true);
        }
    }

    private void ApplyItemSelectionVisual(object item, bool isSelected)
    {
        if (FilmstripList.ContainerFromItem(item) is ListViewItem container)
        {
            ApplyContainerSelectionVisual(container, isSelected, animate: true);
        }
    }

    private static void ApplyContainerSelectionVisual(
        ListViewItem container,
        bool isSelected,
        bool animate)
    {
        Canvas.SetZIndex(container, isSelected ? 1 : 0);
        if (container.ContentTemplateRoot is not FrameworkElement cardRoot)
        {
            return;
        }

        if (cardRoot.FindName("TimelineThumbnailVisual") is not FrameworkElement thumbnailVisual)
        {
            return;
        }

        if (thumbnailVisual.ActualWidth > 0d && thumbnailVisual.ActualHeight > 0d)
        {
            thumbnailVisual.CenterPoint = new Vector3(
                (float)(thumbnailVisual.ActualWidth / 2d),
                (float)(thumbnailVisual.ActualHeight / 2d),
                0f);
        }
        SetScale(
            thumbnailVisual,
            isSelected ? new Vector3(SelectedTimelineScale, SelectedTimelineScale, 1f) : Vector3.One,
            animate);

        if (cardRoot.FindName("TimelineSelectionGlow") is UIElement glow)
        {
            SetOpacity(glow, isSelected ? 1d : 0d, animate);
        }

        if (cardRoot.FindName("TimelineSelectionChrome") is UIElement chrome)
        {
            SetOpacity(chrome, isSelected ? 1d : 0d, animate);
        }
    }

    private static void SetScale(UIElement element, Vector3 scale, bool animate)
    {
        var transition = element.ScaleTransition;
        if (!animate)
        {
            element.ScaleTransition = null;
        }

        element.Scale = scale;
        if (!animate)
        {
            element.ScaleTransition = transition;
        }
    }

    private static void SetOpacity(UIElement element, double opacity, bool animate)
    {
        var transition = element.OpacityTransition;
        if (!animate)
        {
            element.OpacityTransition = null;
        }

        element.Opacity = opacity;
        if (!animate)
        {
            element.OpacityTransition = transition;
        }
    }

    private void FilmstripList_Loaded(object sender, RoutedEventArgs e)
    {
        AttachTimelineScroller();
        BringSelectionIntoView();
        QueueNavigationAvailabilityUpdate();
    }

    private void FilmstripList_Unloaded(object sender, RoutedEventArgs e)
    {
        _selectionCenterGeneration++;
        if (_timelineScroller is not null)
        {
            _timelineScroller.ViewChanged -= TimelineScroller_ViewChanged;
            _timelineScroller = null;
        }
    }

    private void FilmstripList_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.5d)
        {
            return;
        }

        BringSelectionIntoView();
        QueueNavigationAvailabilityUpdate();
    }

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

        var page = Math.Max(184d, scroller.ViewportWidth * 0.72d);
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
            DecodePixelWidth = 432,
            UriSource = sourceUri
        };
    }

    private sealed record ScreenshotTimelineEntry(
        string Path,
        string TimeText,
        string AutomationName,
        SolidColorBrush InstallationBrush,
        string InstallationGlyph);
}
