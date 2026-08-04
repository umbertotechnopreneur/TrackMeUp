using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class WinUiSurfaceContractTests
{
    [Fact]
    public void ExecutableManifest_DeclaresPerMonitorV2DpiAwareness()
    {
        var manifest = XDocument.Load(RepositoryFile("TrackMeUp", "app.manifest"));

        Assert.Contains(
            manifest.Descendants(),
            element => element.Name.LocalName == "dpiAwareness" && element.Value.Trim() == "PerMonitorV2");
    }

    [Fact]
    public void CompactSurfaces_ProvideScrollingAndAdaptiveOptions()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var about = XDocument.Load(RepositoryFile("TrackMeUp", "AboutWindow.xaml"));

        Assert.Contains(player.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger");
        Assert.Contains(about.Descendants(), element => element.Name.LocalName == "ScrollViewer");
    }

    [Theory]
    [InlineData("TrackMeUp/MainWindow.xaml")]
    [InlineData("TrackMeUp/Controls/OptionsControl.xaml")]
    [InlineData("TrackMeUp/Controls/OperationsControl.xaml")]
    [InlineData("TrackMeUp/TaskbarWidgetWindow.xaml")]
    public void IconOnlyButtons_HaveExplicitAutomationNames(string relativePath)
    {
        var document = XDocument.Load(RepositoryFile(relativePath.Split('/')));
        var iconOnlyButtons = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(element => element.Attribute("Content") is null)
            .Where(element => element.Descendants().Any(child => child.Name.LocalName is "FontIcon" or "Image"))
            .ToArray();

        Assert.NotEmpty(iconOnlyButtons);
        Assert.All(
            iconOnlyButtons,
            button => Assert.Contains(button.Attributes(), attribute => attribute.Name.LocalName == "AutomationProperties.Name"));
    }

    private static string RepositoryFile(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TrackMeUp.slnx")))
            {
                return Path.Combine(new[] { directory.FullName }.Concat(pathSegments).ToArray());
            }
        }

        throw new DirectoryNotFoundException("Could not locate the TrackMeUp repository root.");
    }
}
