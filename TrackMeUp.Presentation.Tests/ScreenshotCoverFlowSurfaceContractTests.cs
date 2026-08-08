using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class ScreenshotCoverFlowSurfaceContractTests
{
    [Fact]
    public void ScreenshotGallery_UsesARecycledCoverFlowAndVirtualizedTimeline()
    {
        var gallery = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotGalleryViewControl.xaml"));
        var timeline = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotTimelineControl.xaml"));
        var coverFlow = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotCoverFlowControl.xaml"));
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml"));
        var coverFlowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotCoverFlowControl.xaml.cs"));
        var timelineSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotTimelineControl.xaml.cs"));
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));

        Assert.Contains(gallery.Descendants(), element => element.Name.LocalName == "ScreenshotCoverFlowControl");
        Assert.DoesNotContain(gallery.Descendants(), element => HasName(element, "PreviousPreviewFrame") || HasName(element, "NextPreviewFrame"));
        Assert.Contains(timeline.Descendants(), element => element.Name.LocalName == "ListView" && HasName(element, "FilmstripList"));
        Assert.Contains(timeline.Descendants(), element => element.Name.LocalName == "ItemsStackPanel" && element.Attribute("Orientation")?.Value == "Horizontal");
        Assert.Contains(timeline.Descendants(), element => HasName(element, "PreviousTimelineButton"));
        Assert.Contains(timeline.Descendants(), element => HasName(element, "NextTimelineButton"));
        Assert.Contains(timeline.Descendants(), element =>
            element.Name.LocalName == "Image" && element.Attribute("Stretch")?.Value == "Uniform");
        Assert.DoesNotContain(timeline.Descendants(), element => HasName(element, "FilmstripPanel"));
        Assert.Contains(window.Descendants(), element =>
            HasName(element, "TimelineSection") && element.Attribute("Margin")?.Value == "0,0,0,20");
        Assert.Contains(coverFlow.Descendants(), element => HasName(element, "PreviousButton"));
        Assert.Contains(coverFlow.Descendants(), element => HasName(element, "NextButton"));
        Assert.Contains(coverFlow.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "CoverFlowSlotButtonStyle")
            && element.Elements().Any(setter =>
                setter.Attribute("Property")?.Value == "BorderThickness"
                && setter.Attribute("Value")?.Value == "0"));
        Assert.Contains("ScreenshotCoverFlowProjection.StagingRadius", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("public int RealizedItemCount", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("MoveToIndex", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("PointerWheelChangedEvent", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("AnimationsEnabled", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("Stretch = Stretch.Uniform", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("ScreenshotCoverFlowLayout.FitPresenter", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("ImageOpened += SlotImage_ImageOpened", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("Shadow = new ThemeShadow()", coverFlowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Stretch.UniformToFill", coverFlowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("slot.Image.Clip", coverFlowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CornerRadius = new CornerRadius", coverFlowSource, StringComparison.Ordinal);
        Assert.Contains("FindDescendant<ScrollViewer>", timelineSource, StringComparison.Ordinal);
        Assert.Contains("scroller.ExtentWidth", timelineSource, StringComparison.Ordinal);
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
