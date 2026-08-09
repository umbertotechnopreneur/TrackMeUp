using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Globalization;
using TrackMeUp.Application;

namespace TrackMeUp.Controls;

/// <summary>Displays one selected screenshot in a passive zoomable viewer.</summary>
public sealed partial class ScreenshotImageViewerControl : UserControl
{
    private const float MinimumZoomFactor = 1f;
    private const float MaximumZoomFactor = 5f;
    private const float ZoomStep = 0.25f;

    private Uri? _currentSource;
    private bool _hasImage;

    /// <summary>Creates the single-image screenshot viewer.</summary>
    public ScreenshotImageViewerControl()
    {
        InitializeComponent();
        UpdateBaseContentSize();
        UpdateZoomControls();
    }

    /// <summary>Raised when the user asks the host window to export the displayed screenshot.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Replaces the currently displayed screenshot without owning gallery selection state.</summary>
    public void SetItem(ScreenshotGalleryItem? item, int selectedIndex, int totalCount, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (item is null)
        {
            ClearImage();
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= totalCount)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex), selectedIndex, "The selected screenshot must exist in the viewer source.");
        }

        if (!Uri.TryCreate(item.Path, UriKind.Absolute, out var source))
        {
            throw new InvalidDataException($"Screenshot path is not an absolute URI: {item.Path}");
        }

        if (_currentSource is null || !_currentSource.Equals(source))
        {
            _currentSource = source;
            _hasImage = true;
            ScreenshotImage.Source = new BitmapImage { UriSource = source };
            ResetZoom(disableAnimation: true);
        }

        var culture = CultureInfo.GetCultureInfo(language);
        var localTime = item.CapturedAt.ToLocalTime();
        AutomationProperties.SetName(
            ScreenshotImage,
            $"Screenshot {selectedIndex + 1} of {totalCount}, {localTime.ToString("f", culture)}");
        UpdateZoomControls();
    }

    private void ClearImage()
    {
        _currentSource = null;
        _hasImage = false;
        ScreenshotImage.Source = null;
        AutomationProperties.SetName(ScreenshotImage, "No screenshot selected");
        ResetZoom(disableAnimation: true);
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) =>
        SetZoom(ImageScroller.ZoomFactor - ZoomStep);

    private void ZoomResetButton_Click(object sender, RoutedEventArgs e) =>
        ResetZoom(disableAnimation: false);

    private void ZoomInButton_Click(object sender, RoutedEventArgs e) =>
        SetZoom(ImageScroller.ZoomFactor + ZoomStep);

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasImage)
        {
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetZoom(float zoomFactor)
    {
        if (!_hasImage)
        {
            return;
        }

        var target = Math.Clamp(zoomFactor, MinimumZoomFactor, MaximumZoomFactor);
        ImageScroller.ChangeView(null, null, target, disableAnimation: false);
        UpdateZoomControls();
    }

    private void ResetZoom(bool disableAnimation)
    {
        ImageScroller.ChangeView(null, null, MinimumZoomFactor, disableAnimation);
        UpdateZoomControls();
    }

    private void ImageScroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateBaseContentSize();

    private void ImageScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) =>
        UpdateZoomControls();

    private void UpdateBaseContentSize()
    {
        ImageHost.Width = Math.Max(1d, ImageScroller.ActualWidth);
        ImageHost.Height = Math.Max(1d, ImageScroller.ActualHeight);
    }

    private void UpdateZoomControls()
    {
        var zoom = ImageScroller.ZoomFactor;
        ZoomPercentText.Text = string.Format(CultureInfo.InvariantCulture, "{0:0}%", zoom * 100d);
        ZoomOutButton.IsEnabled = _hasImage && zoom > MinimumZoomFactor + 0.001f;
        ZoomResetButton.IsEnabled = _hasImage;
        ZoomInButton.IsEnabled = _hasImage && zoom < MaximumZoomFactor - 0.001f;
        SaveButton.IsEnabled = _hasImage;
    }
}
