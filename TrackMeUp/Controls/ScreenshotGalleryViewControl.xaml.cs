using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TrackMeUp.Controls;

/// <summary>Displays the selected screenshot, metadata chips, and loading states.</summary>
public sealed partial class ScreenshotGalleryViewControl : UserControl
{
    /// <summary>Creates the gallery view control.</summary>
    public ScreenshotGalleryViewControl()
    {
        InitializeComponent();
        ImageViewer.OverlayVisibilityChanged += SetOverlayVisibility;
        VisualStateManager.GoToState(this, "OverlayHidden", false);
    }

    /// <summary>Gets the gallery surface that hosts pointer interactions.</summary>
    public Grid Surface => GallerySurface;

    /// <summary>Gets the single-image zoomable screenshot viewer.</summary>
    public ScreenshotImageViewerControl Viewer => ImageViewer;

    /// <summary>Gets the metadata summary panel.</summary>
    public Grid MetadataContainer => MetadataPanel;

    /// <summary>Gets the metadata date value text element.</summary>
    public TextBlock MetadataDateText => MetadataDateValueText;

    /// <summary>Gets the metadata time value text element.</summary>
    public TextBlock MetadataTimeText => MetadataTimeValueText;

    /// <summary>Gets the metadata foreground-app value text element.</summary>
    public TextBlock MetadataApplicationText => MetadataAppValueText;

    /// <summary>Gets the empty-state panel.</summary>
    public Grid EmptyPanel => EmptyGalleryPanel;

    /// <summary>Gets the empty-state message text element.</summary>
    public TextBlock EmptyText => EmptyGalleryText;

    /// <summary>Gets the loading indicator.</summary>
    public ProgressRing LoadingRing => GalleryProgressRing;

    private void SetOverlayVisibility(bool isVisible) =>
        VisualStateManager.GoToState(this, isVisible ? "OverlayVisible" : "OverlayHidden", true);
}
