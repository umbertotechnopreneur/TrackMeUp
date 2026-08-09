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
        var metadataPanel = gallery.Descendants().Single(element => HasName(element, "MetadataPanel"));
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
        Assert.Contains(viewer.Descendants(), element => element.Name.LocalName == "Image" && element.Attribute("Stretch")?.Value == "Uniform");
        Assert.Contains(viewer.Descendants(), element => HasName(element, "ZoomRail") && element.Attribute("VerticalAlignment")?.Value == "Bottom");
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
        Assert.Equal("3", gallerySection.Attribute("Grid.RowSpan")?.Value);
        Assert.Contains(window.Descendants(), element =>
            HasName(element, "TimelineSection") && element.Attribute("Margin")?.Value == "0");
        Assert.Contains("public void SetItem(ScreenshotGalleryItem? item", viewerSource, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? SaveRequested", viewerSource, StringComparison.Ordinal);
        Assert.Contains("ImageScroller.ChangeView", viewerSource, StringComparison.Ordinal);
        Assert.Contains("new BitmapImage", viewerSource, StringComparison.Ordinal);
        Assert.Contains("FindDescendant<ScrollViewer>", timelineSource, StringComparison.Ordinal);
        Assert.Contains("scroller.ExtentWidth", timelineSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CoverFlow.SelectedIndexChanged", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MoveToIndex", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FilmstripPanel.Children.Clear", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new BitmapImage", windowSource, StringComparison.Ordinal);
    }

    private static bool HasName(XElement element, string value)
        => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == value);

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
