using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Globalization;
using TrackMeUp.Application;

namespace TrackMeUp.Controls;

/// <summary>Displays a virtualized screenshot timeline and synchronizes its passive selection state.</summary>
public sealed partial class ScreenshotTimelineControl : UserControl
{
    private IReadOnlyList<ScreenshotTimelineEntry> _entries = Array.Empty<ScreenshotTimelineEntry>();
    private bool _updatingSelection;

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
        if (items.Count == 0 && selectedIndex != -1)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex), selectedIndex, "An empty timeline must use selection index -1.");
        }

        if (items.Count > 0 && (selectedIndex < 0 || selectedIndex >= items.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex), selectedIndex, "The selected screenshot must exist in the timeline.");
        }

        var culture = CultureInfo.GetCultureInfo(language);
        _entries = items
            .Select((item, index) => CreateEntry(item, index, culture))
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

    private static ScreenshotTimelineEntry CreateEntry(ScreenshotGalleryItem item, int index, CultureInfo culture)
    {
        if (!Uri.TryCreate(item.Path, UriKind.Absolute, out _))
        {
            throw new InvalidDataException($"Screenshot path is not an absolute URI: {item.Path}");
        }

        var localTime = item.CapturedAt.ToLocalTime();
        return new ScreenshotTimelineEntry(
            item.Path,
            localTime.ToString("d MMM", culture),
            localTime.ToString("HH:mm", culture),
            $"Screenshot {index + 1}, {localTime.ToString("f", culture)}");
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

        _ = DispatcherQueue.TryEnqueue(() => FilmstripList.ScrollIntoView(selectedItem, ScrollIntoViewAlignment.Leading));
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
