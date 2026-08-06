using Microsoft.UI.Xaml.Controls;

namespace TrackMeUp.Controls;

/// <summary>Displays screenshot previews, navigation controls, metadata, and loading states.</summary>
public sealed partial class ScreenshotGalleryViewControl : UserControl
{
    /// <summary>Creates the gallery view control.</summary>
    public ScreenshotGalleryViewControl() => InitializeComponent();

    /// <summary>Gets the gallery surface that hosts pointer interactions.</summary>
    public Grid Surface => GallerySurface;

    /// <summary>Gets the panel that hosts previous/current/next preview frames.</summary>
    public Grid CoverFlow => CoverFlowPanel;

    /// <summary>Gets the current screenshot frame.</summary>
    public Border CurrentFrame => GalleryImageFrame;

    /// <summary>Gets the previous screenshot frame.</summary>
    public Border PreviousFrame => PreviousPreviewFrame;

    /// <summary>Gets the next screenshot frame.</summary>
    public Border NextFrame => NextPreviewFrame;

    /// <summary>Gets the current screenshot image control.</summary>
    public Image CurrentGalleryImage => CurrentImage;

    /// <summary>Gets the previous screenshot image control.</summary>
    public Image PreviousGalleryImage => PreviousImage;

    /// <summary>Gets the next screenshot image control.</summary>
    public Image NextGalleryImage => NextImage;

    /// <summary>Gets the previous navigation button.</summary>
    public Button PreviousNavigationButton => PreviousButton;

    /// <summary>Gets the next navigation button.</summary>
    public Button NextNavigationButton => NextButton;

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

    /// <summary>Gets the empty-state panel.</summary>
    public Grid EmptyPanel => EmptyGalleryPanel;

    /// <summary>Gets the empty-state message text element.</summary>
    public TextBlock EmptyText => EmptyGalleryText;

    /// <summary>Gets the loading indicator.</summary>
    public ProgressRing LoadingRing => GalleryProgressRing;
}
