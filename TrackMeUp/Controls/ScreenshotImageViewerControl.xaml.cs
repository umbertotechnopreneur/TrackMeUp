using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Displays one selected screenshot in a passive zoomable viewer.</summary>
public sealed partial class ScreenshotImageViewerControl : UserControl
{
    private const double ScreenshotFrameMargin = 28d;
    private const double ScreenshotFramePadding = 8d;
    private const float MinimumZoomFactor = 1f;
    private const float MaximumZoomFactor = 5f;
    private const float ZoomStep = 0.25f;
    private const float MouseWheelDeltaPerNotch = 120f;

    private Uri? _currentSource;
    private bool _hasImage;
    private double _imagePixelWidth;
    private double _imagePixelHeight;
    private uint? _dragPointerId;
    private Point _dragStartPosition;
    private double _dragStartHorizontalOffset;
    private double _dragStartVerticalOffset;
    private bool _isPointerInside;
    private bool _hasKeyboardFocus;
    private bool _areOverlayControlsVisible;
    private LocalizationService _strings = new("system");

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
        VisualStateManager.GoToState(this, "OverlayHidden", false);
    }

    /// <summary>Raised when the user asks the host window to export the displayed screenshot.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Raised when the user asks the host window to share the displayed screenshot.</summary>
    public event EventHandler? ShareRequested;

    /// <summary>Raised when the user asks the host window to open the screenshot folder.</summary>
    public event EventHandler? OpenFolderRequested;

    /// <summary>Raised when the user asks the host window to delete the displayed screenshot file.</summary>
    public event EventHandler? DeleteScreenshotRequested;

    /// <summary>Raised when the user asks the host window to delete the displayed screenshot metadata.</summary>
    public event EventHandler? DeleteSnapshotRequested;

    /// <summary>Raised when the user changes the requested snapshot-details visibility.</summary>
    public event Action<bool>? DetailsVisibilityRequested;

    /// <summary>Raised when the hover or keyboard state changes overlay visibility.</summary>
    public event Action<bool>? OverlayVisibilityChanged;

    /// <summary>Replaces the currently displayed screenshot without owning gallery selection state.</summary>
    public void SetItem(ScreenshotGalleryItem? item, int selectedIndex, int totalCount, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _strings = new LocalizationService(language);
        AutomationProperties.SetName(this, _strings.Translate("Screenshots.Caption"));
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

        var localTime = item.CapturedAt.ToLocalTime();
        AutomationProperties.SetName(
            ScreenshotImage,
            _strings.Format("Screenshots.Image.Accessible", selectedIndex + 1, totalCount, localTime));
        UpdateZoomControls();
    }

    internal void SetDetailsState(bool isEnabled, bool isVisible, string localizedLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizedLabel);
        DetailsToggleButton.IsEnabled = isEnabled;
        DetailsToggleButton.IsChecked = isVisible;
        DetailsToggleButton.Tag = isVisible ? "Screenshots.Details.Hide" : "Screenshots.Details.Show";
        AutomationProperties.SetName(DetailsToggleButton, localizedLabel);
        ToolTipService.SetToolTip(DetailsToggleButton, localizedLabel);
    }

    internal void SetPointerInside(bool isInside)
    {
        _isPointerInside = isInside;
        UpdateOverlayVisibility();
    }

    private void ClearImage()
    {
        _currentSource = null;
        _hasImage = false;
        _imagePixelWidth = 0d;
        _imagePixelHeight = 0d;
        ScreenshotImage.Source = null;
        ScreenshotFrame.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(ScreenshotImage, _strings.Translate("Screenshots.Image.None"));
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
        ScreenshotFrame.Visibility = Visibility.Visible;
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

    private void DetailsToggleButton_Click(object sender, RoutedEventArgs e) =>
        DetailsVisibilityRequested?.Invoke(DetailsToggleButton.IsChecked == true);

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasImage)
        {
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShareButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasImage)
        {
            ShareRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e) =>
        OpenFolderRequested?.Invoke(this, EventArgs.Empty);

    private void DeleteScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasImage)
        {
            DeleteScreenshotRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DeleteSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hasImage)
        {
            DeleteSnapshotRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ImageHost_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ImageScroller);
        if (!_hasImage
            || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse
            || point.Properties.IsHorizontalMouseWheel
            || point.Properties.MouseWheelDelta == 0)
        {
            return;
        }

        var target = ImageScroller.ZoomFactor
            + ((point.Properties.MouseWheelDelta / MouseWheelDeltaPerNotch) * ZoomStep);
        SetZoom(target, point.Position, disableAnimation: true);
        e.Handled = true;
    }

    private void SetZoom(float zoomFactor)
    {
        var (viewportWidth, viewportHeight) = GetViewportSize();
        SetZoom(
            zoomFactor,
            new Point(viewportWidth / 2d, viewportHeight / 2d),
            disableAnimation: false);
    }

    private void SetZoom(float zoomFactor, Point viewportAnchor, bool disableAnimation)
    {
        if (!_hasImage)
        {
            return;
        }

        var target = Math.Clamp(zoomFactor, MinimumZoomFactor, MaximumZoomFactor);
        var currentZoom = Math.Max(MinimumZoomFactor, ImageScroller.ZoomFactor);
        var (viewportWidth, viewportHeight) = GetViewportSize();
        var anchorX = Math.Clamp(viewportAnchor.X, 0d, viewportWidth);
        var anchorY = Math.Clamp(viewportAnchor.Y, 0d, viewportHeight);
        var contentAnchorX = (ImageScroller.HorizontalOffset + anchorX) / currentZoom;
        var contentAnchorY = (ImageScroller.VerticalOffset + anchorY) / currentZoom;
        var horizontalOffset = Math.Clamp(
            (contentAnchorX * target) - anchorX,
            0d,
            Math.Max(0d, (ImageHost.Width * target) - viewportWidth));
        var verticalOffset = Math.Clamp(
            (contentAnchorY * target) - anchorY,
            0d,
            Math.Max(0d, (ImageHost.Height * target) - viewportHeight));
        ImageScroller.ChangeView(horizontalOffset, verticalOffset, target, disableAnimation);
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
            ScreenshotFrame.Width = Math.Max(1d, viewportWidth - (ScreenshotFrameMargin * 2d));
            ScreenshotFrame.Height = Math.Max(1d, viewportHeight - (ScreenshotFrameMargin * 2d));
            return;
        }

        var availableWidth = Math.Max(1d, viewportWidth - (ScreenshotFrameMargin * 2d) - (ScreenshotFramePadding * 2d));
        var availableHeight = Math.Max(1d, viewportHeight - (ScreenshotFrameMargin * 2d) - (ScreenshotFramePadding * 2d));
        var containScale = Math.Min(availableWidth / _imagePixelWidth, availableHeight / _imagePixelHeight);
        var imageWidth = _imagePixelWidth * containScale;
        var imageHeight = _imagePixelHeight * containScale;
        ScreenshotFrame.Width = imageWidth + (ScreenshotFramePadding * 2d);
        ScreenshotFrame.Height = imageHeight + (ScreenshotFramePadding * 2d);
        ImageHost.Width = Math.Max(viewportWidth, ScreenshotFrame.Width + (ScreenshotFrameMargin * 2d));
        ImageHost.Height = Math.Max(viewportHeight, ScreenshotFrame.Height + (ScreenshotFrameMargin * 2d));
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

    private void ViewerRoot_GotFocus(object sender, RoutedEventArgs e)
    {
        _hasKeyboardFocus = true;
        UpdateOverlayVisibility();
    }

    private void ViewerRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            SetPointerInside(true);
        }
    }

    private void ViewerRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            SetPointerInside(false);
        }
    }

    private void ViewerRoot_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!DispatcherQueue.TryEnqueue(UpdateKeyboardFocusState))
        {
            UpdateKeyboardFocusState();
        }
    }

    private void UpdateKeyboardFocusState()
    {
        var focusedElement = XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        _hasKeyboardFocus = focusedElement is not null && IsDescendantOf(focusedElement, ViewerRoot);
        UpdateOverlayVisibility();
    }

    private void UpdateOverlayVisibility()
    {
        var isVisible = _isPointerInside || _hasKeyboardFocus;
        if (_areOverlayControlsVisible == isVisible)
        {
            return;
        }

        _areOverlayControlsVisible = isVisible;
        VisualStateManager.GoToState(this, isVisible ? "OverlayVisible" : "OverlayHidden", true);
        OverlayVisibilityChanged?.Invoke(isVisible);
    }

    private static bool IsDescendantOf(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateZoomControls()
    {
        var zoom = ImageScroller.ZoomFactor;
        ZoomPercentText.Text = _strings.Format("Screenshots.ZoomPercent", zoom);
        ZoomOutButton.IsEnabled = _hasImage && zoom > MinimumZoomFactor + 0.001f;
        ZoomResetButton.IsEnabled = _hasImage;
        ZoomInButton.IsEnabled = _hasImage && zoom < MaximumZoomFactor - 0.001f;
        SaveButton.IsEnabled = _hasImage;
        ShareButton.IsEnabled = _hasImage;
        DeleteScreenshotButton.IsEnabled = _hasImage;
        DeleteSnapshotButton.IsEnabled = _hasImage;
    }
}
