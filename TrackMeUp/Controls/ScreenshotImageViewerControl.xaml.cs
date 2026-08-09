using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Globalization;
using TrackMeUp.Application;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Displays one selected screenshot in a passive zoomable viewer.</summary>
public sealed partial class ScreenshotImageViewerControl : UserControl
{
    private const float MinimumZoomFactor = 1f;
    private const float MaximumZoomFactor = 5f;
    private const float ZoomStep = 0.25f;

    private Uri? _currentSource;
    private bool _hasImage;
    private double _imagePixelWidth;
    private double _imagePixelHeight;
    private uint? _dragPointerId;
    private Point _dragStartPosition;
    private double _dragStartHorizontalOffset;
    private double _dragStartVerticalOffset;

    /// <summary>Creates the single-image screenshot viewer.</summary>
    public ScreenshotImageViewerControl()
    {
        InitializeComponent();
        ImageScroller.AddHandler(PointerPressedEvent, new PointerEventHandler(ImageScroller_PointerPressed), true);
        ImageScroller.AddHandler(PointerMovedEvent, new PointerEventHandler(ImageScroller_PointerMoved), true);
        ImageScroller.AddHandler(PointerReleasedEvent, new PointerEventHandler(ImageScroller_PointerReleased), true);
        ImageScroller.PointerCanceled += ImageScroller_PointerCanceled;
        ImageScroller.PointerCaptureLost += ImageScroller_PointerCaptureLost;
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
            _imagePixelWidth = 0d;
            _imagePixelHeight = 0d;
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
        _imagePixelWidth = 0d;
        _imagePixelHeight = 0d;
        ScreenshotImage.Source = null;
        AutomationProperties.SetName(ScreenshotImage, "No screenshot selected");
        ResetZoom(disableAnimation: true);
    }

    private void ScreenshotImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (ScreenshotImage.Source is not BitmapImage bitmap
            || bitmap.PixelWidth <= 0
            || bitmap.PixelHeight <= 0)
        {
            return;
        }

        _imagePixelWidth = bitmap.PixelWidth;
        _imagePixelHeight = bitmap.PixelHeight;
        UpdateBaseContentSize();
        if (!DispatcherQueue.TryEnqueue(() => ResetZoom(disableAnimation: true)))
        {
            ResetZoom(disableAnimation: true);
        }
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
        var currentZoom = Math.Max(MinimumZoomFactor, ImageScroller.ZoomFactor);
        var (viewportWidth, viewportHeight) = GetViewportSize();
        var contentCenterX = (ImageScroller.HorizontalOffset + (viewportWidth / 2d)) / currentZoom;
        var contentCenterY = (ImageScroller.VerticalOffset + (viewportHeight / 2d)) / currentZoom;
        var horizontalOffset = Math.Clamp(
            (contentCenterX * target) - (viewportWidth / 2d),
            0d,
            Math.Max(0d, (ImageHost.Width * target) - viewportWidth));
        var verticalOffset = Math.Clamp(
            (contentCenterY * target) - (viewportHeight / 2d),
            0d,
            Math.Max(0d, (ImageHost.Height * target) - viewportHeight));
        ImageScroller.ChangeView(horizontalOffset, verticalOffset, target, disableAnimation: false);
        UpdateZoomControls();
    }

    private void ResetZoom(bool disableAnimation)
    {
        _dragPointerId = null;
        ImageScroller.ReleasePointerCaptures();
        UpdateBaseContentSize();
        var (viewportWidth, viewportHeight) = GetViewportSize();
        ImageScroller.ChangeView(
            Math.Max(0d, (ImageHost.Width - viewportWidth) / 2d),
            Math.Max(0d, (ImageHost.Height - viewportHeight) / 2d),
            MinimumZoomFactor,
            disableAnimation);
        UpdateZoomControls();
    }

    private void ImageScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBaseContentSize();
        if (!DispatcherQueue.TryEnqueue(CenterCurrentView))
        {
            CenterCurrentView();
        }
    }

    private void ImageScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e) =>
        UpdateZoomControls();

    private void UpdateBaseContentSize()
    {
        var (viewportWidth, viewportHeight) = GetViewportSize();
        if (!_hasImage || _imagePixelWidth <= 0d || _imagePixelHeight <= 0d)
        {
            ImageHost.Width = viewportWidth;
            ImageHost.Height = viewportHeight;
            return;
        }

        // The content owns the cover-sized rectangle, so the ScrollViewer clips it without discarding source pixels.
        var coverScale = Math.Max(viewportWidth / _imagePixelWidth, viewportHeight / _imagePixelHeight);
        ImageHost.Width = Math.Max(viewportWidth, _imagePixelWidth * coverScale);
        ImageHost.Height = Math.Max(viewportHeight, _imagePixelHeight * coverScale);
    }

    private (double Width, double Height) GetViewportSize() =>
        (Math.Max(1d, ImageScroller.ActualWidth), Math.Max(1d, ImageScroller.ActualHeight));

    private void CenterCurrentView()
    {
        var zoom = Math.Max(MinimumZoomFactor, ImageScroller.ZoomFactor);
        var (viewportWidth, viewportHeight) = GetViewportSize();
        ImageScroller.ChangeView(
            Math.Max(0d, ((ImageHost.Width * zoom) - viewportWidth) / 2d),
            Math.Max(0d, ((ImageHost.Height * zoom) - viewportHeight) / 2d),
            null,
            disableAnimation: true);
    }

    private void ImageScroller_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ImageScroller);
        if (!_hasImage
            || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
            || !point.Properties.IsLeftButtonPressed
            || (ImageScroller.ScrollableWidth <= 0.5d && ImageScroller.ScrollableHeight <= 0.5d)
            || !ImageScroller.CapturePointer(e.Pointer))
        {
            return;
        }

        _dragPointerId = e.Pointer.PointerId;
        _dragStartPosition = point.Position;
        _dragStartHorizontalOffset = ImageScroller.HorizontalOffset;
        _dragStartVerticalOffset = ImageScroller.VerticalOffset;
        e.Handled = true;
    }

    private void ImageScroller_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragPointerId != e.Pointer.PointerId)
        {
            return;
        }

        var point = e.GetCurrentPoint(ImageScroller);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _dragPointerId = null;
            ImageScroller.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
            return;
        }

        var horizontalOffset = Math.Clamp(
            _dragStartHorizontalOffset - (point.Position.X - _dragStartPosition.X),
            0d,
            ImageScroller.ScrollableWidth);
        var verticalOffset = Math.Clamp(
            _dragStartVerticalOffset - (point.Position.Y - _dragStartPosition.Y),
            0d,
            ImageScroller.ScrollableHeight);
        ImageScroller.ChangeView(horizontalOffset, verticalOffset, null, disableAnimation: true);
        e.Handled = true;
    }

    private void ImageScroller_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragPointerId != e.Pointer.PointerId)
        {
            return;
        }

        _dragPointerId = null;
        ImageScroller.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void ImageScroller_PointerCanceled(object sender, PointerRoutedEventArgs e) =>
        _dragPointerId = null;

    private void ImageScroller_PointerCaptureLost(object sender, PointerRoutedEventArgs e) =>
        _dragPointerId = null;

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
