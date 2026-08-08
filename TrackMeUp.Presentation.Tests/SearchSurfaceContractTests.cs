using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class SearchSurfaceContractTests
{
    [Fact]
    public void FloatingSearchWindow_UsesMicaAndBoundedScreenshotResults()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "SearchWindow.xaml"));
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchWindow.xaml.cs"));
        var viewModelSource = File.ReadAllText(RepositoryFile("TrackMeUp.Presentation", "SearchViewModel.cs"));
        var list = window.Descendants().Single(element => HasName(element, "SearchResultsList"));

        Assert.Equal("Window", window.Root?.Name.LocalName);
        Assert.Contains(window.Descendants(), element => element.Name.LocalName == "MicaBackdrop" && element.Attribute("Kind")?.Value == "BaseAlt");
        Assert.Equal("True", list.Attribute("IsItemClickEnabled")?.Value);
        Assert.Contains(window.Descendants(), element => element.Name.LocalName == "Image" && element.Attribute("Source")?.Value == "{Binding ScreenshotUri}");
        Assert.Contains("presenter.IsAlwaysOnTop = true;", windowSource, StringComparison.Ordinal);
        Assert.Contains("public const int MaximumResults = 20;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Kinds = ImmutableHashSet.Create(StringComparer.Ordinal, \"screenshot\")", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("IncludeTextContent = false", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Limit = MaximumResults", viewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_ExposeSearchPreferencesAndOnlyOcrCanBeDisabled()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));

        Assert.Contains(options.Descendants(), element => HasName(element, "SearchOptionsView"));
        Assert.Contains(options.Descendants(), element => HasName(element, "SearchLanguageBox"));
        Assert.Contains(options.Descendants(), element => HasName(element, "SearchSynonymsSwitch"));
        Assert.Contains(options.Descendants(), element => HasName(element, "SearchTypoToleranceSwitch"));
        Assert.Contains(options.Descendants(), element => HasName(element, "OcrEnabledSwitch"));
        Assert.DoesNotContain(options.Descendants(), element => HasName(element, "SearchEnabledSwitch"));
        Assert.Contains("[\"ocr.enabled\"] = OcrEnabledSwitch.IsOn.ToString()", source, StringComparison.Ordinal);
        Assert.Contains("[\"search.synonyms\"] = SearchSynonymsSwitch.IsOn.ToString()", source, StringComparison.Ordinal);
        Assert.Contains("_application.RebuildSearchIndexAsync", source, StringComparison.Ordinal);
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

    private static bool HasName(XElement element, string name) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);
}
