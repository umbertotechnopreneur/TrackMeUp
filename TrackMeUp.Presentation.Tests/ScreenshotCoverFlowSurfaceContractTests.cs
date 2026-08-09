using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class ScreenshotCoverFlowSurfaceContractTests
{
    [Fact]
    public void ScreenshotGallery_UsesSingleZoomableViewerAndVirtualizedTimeline()
    {
        var gallery = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotGalleryViewControl.xaml"));
        var viewer = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml"));
        var timeline = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotTimelineControl.xaml"));
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml"));
        var viewerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml.cs"));
        var timelineSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotTimelineControl.xaml.cs"));
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));
        var imageScroller = viewer.Descendants().Single(element => HasName(element, "ImageScroller"));
        var screenshotImage = viewer.Descendants().Single(element => HasName(element, "ScreenshotImage"));
        var zoomRail = viewer.Descendants().Single(element => HasName(element, "ZoomRail"));
        var metadataPanel = gallery.Descendants().Single(element => HasName(element, "MetadataPanel"));
        var metadataChipStyle = gallery.Descendants().Single(element =>
            element.Name.LocalName == "Style" && HasKey(element, "ScreenshotMetadataChipStyle"));
        var filmstripSurface = timeline.Descendants().Single(element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Background")?.Value == "{ThemeResource AcrylicInAppFillColorBaseBrush}");
        var gallerySection = window.Descendants().Single(element => HasName(element, "GallerySection"));

        Assert.Contains(gallery.Descendants(), element => element.Name.LocalName == "ScreenshotImageViewerControl");
        Assert.DoesNotContain(gallery.Descendants(), element => element.Name.LocalName == "ScreenshotCoverFlowControl");
        Assert.DoesNotContain(gallery.Descendants(), element => HasName(element, "PreviousPreviewFrame") || HasName(element, "NextPreviewFrame"));
        Assert.Contains(viewer.Descendants(), element => element.Name.LocalName == "ScrollViewer" && element.Attribute("ZoomMode")?.Value == "Enabled");
        Assert.Contains(viewer.Descendants(), element => element.Name.LocalName == "ScrollViewer" && element.Attribute("MaxZoomFactor")?.Value == "5");
        Assert.Equal("Hidden", imageScroller.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", imageScroller.Attribute("HorizontalScrollMode")?.Value);
        Assert.Equal("Hidden", imageScroller.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Auto", imageScroller.Attribute("VerticalScrollMode")?.Value);
        Assert.Equal("Uniform", screenshotImage.Attribute("Stretch")?.Value);
        Assert.Equal("ScreenshotImage_ImageOpened", screenshotImage.Attribute("ImageOpened")?.Value);
        Assert.Contains(viewer.Descendants(), element => HasName(element, "ZoomRail") && element.Attribute("VerticalAlignment")?.Value == "Bottom");
        Assert.Equal("{ThemeResource AcrylicInAppFillColorDefaultBrush}", zoomRail.Attribute("Background")?.Value);
        Assert.Contains(viewer.Descendants(), element => HasName(element, "ZoomOutButton"));
        Assert.Contains(viewer.Descendants(), element => HasName(element, "ZoomResetButton"));
        Assert.Contains(viewer.Descendants(), element => HasName(element, "ZoomPercentText"));
        Assert.Contains(viewer.Descendants(), element => HasName(element, "ZoomInButton"));
        Assert.Contains(viewer.Descendants(), element => HasName(element, "SaveButton"));
        Assert.Contains(viewer.Descendants(), element => HasName(element, "SaveButton")
            && element.Descendants().Any(child => child.Name.LocalName == "FontIcon" && child.Attribute("Glyph")?.Value == "\uE74E"));
        Assert.Contains(timeline.Descendants(), element => element.Name.LocalName == "ListView" && HasName(element, "FilmstripList"));
        Assert.Contains(timeline.Descendants(), element => element.Name.LocalName == "ItemsStackPanel" && element.Attribute("Orientation")?.Value == "Horizontal");
        Assert.Contains(timeline.Descendants(), element => HasName(element, "PreviousTimelineButton"));
        Assert.Contains(timeline.Descendants(), element => HasName(element, "NextTimelineButton"));
        Assert.Contains(timeline.Descendants(), element =>
            element.Name.LocalName == "Image" && element.Attribute("Stretch")?.Value == "Uniform");
        Assert.DoesNotContain(timeline.Descendants(), element => HasName(element, "FilmstripPanel"));
        Assert.Equal("Bottom", metadataPanel.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("False", metadataPanel.Attribute("IsHitTestVisible")?.Value);
        Assert.Contains(metadataChipStyle.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Background"
            && element.Attribute("Value")?.Value == "{ThemeResource AcrylicInAppFillColorDefaultBrush}");
        Assert.NotNull(filmstripSurface);
        Assert.Equal("3", gallerySection.Attribute("Grid.RowSpan")?.Value);
        Assert.Contains(window.Descendants(), element =>
            HasName(element, "TimelineSection") && element.Attribute("Margin")?.Value == "0");
        Assert.Contains("public void SetItem(ScreenshotGalleryItem? item", viewerSource, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? SaveRequested", viewerSource, StringComparison.Ordinal);
        Assert.Contains("ImageScroller.ChangeView", viewerSource, StringComparison.Ordinal);
        Assert.Contains("new BitmapImage", viewerSource, StringComparison.Ordinal);
        Assert.Contains("bitmap.PixelWidth", viewerSource, StringComparison.Ordinal);
        Assert.Contains("var coverScale = Math.Max", viewerSource, StringComparison.Ordinal);
        Assert.Contains("PointerDeviceType.Mouse", viewerSource, StringComparison.Ordinal);
        Assert.Contains("ImageScroller.CapturePointer(e.Pointer)", viewerSource, StringComparison.Ordinal);
        Assert.Contains("ImageScroller.ReleasePointerCapture(e.Pointer)", viewerSource, StringComparison.Ordinal);
        Assert.Contains("_dragStartHorizontalOffset - (point.Position.X - _dragStartPosition.X)", viewerSource, StringComparison.Ordinal);
        Assert.Contains("ImageScroller.ChangeView(horizontalOffset, verticalOffset, null, disableAnimation: true)", viewerSource, StringComparison.Ordinal);
        Assert.Contains("FindDescendant<ScrollViewer>", timelineSource, StringComparison.Ordinal);
        Assert.Contains("scroller.ExtentWidth", timelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CoverFlow.SelectedIndexChanged", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveToIndex", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmstripPanel.Children.Clear", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new BitmapImage", windowSource, StringComparison.Ordinal);
    }

    private static bool HasName(XElement element, string value)
        => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == value);

    private static bool HasKey(XElement element, string value)
        => element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == value);

    private static string RepositoryFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "TrackMeUp.slnx")))
        {
            current = current.Parent;
        }

        return current is null
            ? throw new DirectoryNotFoundException("Repository root could not be located from the test output directory.")
            : Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
    }
}
