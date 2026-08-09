using Microsoft.UI.Xaml.Controls;

namespace TrackMeUp.Controls;

/// <summary>Displays the selected screenshot, metadata chips, and loading states.</summary>
public sealed partial class ScreenshotGalleryViewControl : UserControl
{
    /// <summary>Creates the gallery view control.</summary>
    public ScreenshotGalleryViewControl() => InitializeComponent();

    /// <summary>Gets the gallery surface that hosts pointer interactions.</summary>
    public Grid Surface => GallerySurface;

    /// <summary>Gets the single-image zoomable screenshot viewer.</summary>
    public ScreenshotImageViewerControl Viewer => ImageViewer;

    /// <summary>Gets the metadata summary panel.</summary>
    public Border MetadataContainer => MetadataPanel;

    /// <summary>Gets the metadata date value text element.</summary>
    public TextBlock MetadataDateText => MetadataDateValueText;

    /// <summary>Gets the metadata time value text element.</summary>
    public TextBlock MetadataTimeText => MetadataTimeValueText;

    /// <summary>Gets the metadata foreground-app value text element.</summary>
    public TextBlock MetadataApplicationText => MetadataAppValueText;

    /// <summary>Gets the metadata origin value text element.</summary>
    public TextBlock MetadataOriginText => MetadataOriginValueText;

    /// <summary>Gets the metadata activity-label history value text element.</summary>
    public TextBlock MetadataSpanLabelsText => MetadataSpanLabelsValueText;

    /// <summary>Gets the metadata activity-index value text element.</summary>
    public TextBlock MetadataActivityIndexText => MetadataActivityIndexValueText;

    /// <summary>Gets the empty-state panel.</summary>
    public Grid EmptyPanel => EmptyGalleryPanel;

    /// <summary>Gets the empty-state message text element.</summary>
    public TextBlock EmptyText => EmptyGalleryText;

    /// <summary>Gets the loading indicator.</summary>
    public ProgressRing LoadingRing => GalleryProgressRing;
}
