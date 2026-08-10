using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class SearchSurfaceContractTests
{
    [Fact]
    public void FloatingSearchWindow_UsesAcrylicAndBoundedScreenshotResults()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "SearchWindow.xaml"));
        var resultControl = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "SearchResultItemControl.xaml"));
        var resultControlSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "SearchResultItemControl.xaml.cs"));
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchWindow.xaml.cs"));
        var placementSource = File.ReadAllText(RepositoryFile("TrackMeUp", "WindowPlacementService.cs"));
        var viewModelSource = File.ReadAllText(RepositoryFile("TrackMeUp.Presentation", "SearchViewModel.cs"));
        var list = window.Descendants().Single(element => HasName(element, "SearchResultsList"));
        var queryBox = window.Descendants().Single(element => HasName(element, "QueryBox"));
        var resultThumbnailFrame = resultControl.Descendants().Single(element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Width")?.Value == "260"
            && element.Attribute("Height")?.Value == "146");

        Assert.Equal("Window", window.Root?.Name.LocalName);
        Assert.Contains(window.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.DoesNotContain(window.Descendants(), element => HasName(element, "WindowTitleText"));
        Assert.Equal("48", queryBox.Attribute("MinHeight")?.Value);
        Assert.Equal("Center", queryBox.Attribute("VerticalContentAlignment")?.Value);
        Assert.Equal("Text", queryBox.Attribute("TextMemberPath")?.Value);
        Assert.Contains(queryBox.Descendants(), element => element.Name.LocalName == "Ellipse" && element.Attribute("Fill")?.Value == "{StaticResource SearchSuggestionAccentBrush}");
        Assert.Contains(queryBox.Descendants(), element => element.Attribute("Text")?.Value == "{Binding ConfidenceDisplay}");
        Assert.Equal("True", list.Attribute("IsItemClickEnabled")?.Value);
        Assert.Contains(window.Descendants(), element => element.Name.LocalName == "SearchResultItemControl");
        Assert.Contains(resultControl.Descendants(), element => element.Name.LocalName == "Image" && element.Attribute("Source")?.Value == "{Binding ScreenshotUri}");
        Assert.Equal("260", resultThumbnailFrame.Attribute("Width")?.Value);
        Assert.Equal("146", resultThumbnailFrame.Attribute("Height")?.Value);
        Assert.Equal("SnapshotThumbnailFrame", resultThumbnailFrame.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value);
        Assert.Equal("0,0,4", resultThumbnailFrame.Attribute("Translation")?.Value);
        Assert.Equal("SnapshotThumbnailFrame_PointerEntered", resultThumbnailFrame.Attribute("PointerEntered")?.Value);
        Assert.Equal("SnapshotThumbnailFrame_PointerExited", resultThumbnailFrame.Attribute("PointerExited")?.Value);
        Assert.Contains(resultThumbnailFrame.Descendants(), element => element.Name.LocalName == "Vector3Transition" && element.Attribute("Duration")?.Value == "0:0:0.16");
        Assert.Contains(resultThumbnailFrame.Descendants(), element => element.Name.LocalName == "Image" && element.Attribute("Stretch")?.Value == "Uniform");
        Assert.Contains(resultControl.Descendants(), element => element.Name.LocalName == "ThemeShadow");
        Assert.Contains("SnippetText.TextHighlighters", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("HoverThumbnailElevation = 18f", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("SetThumbnailElevation(HoverThumbnailElevation)", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("SetThumbnailElevation(RestingThumbnailElevation)", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsAlwaysOnTop = true;", windowSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsResizable = false;", windowSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMinimizable = false;", windowSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMaximizable = false;", windowSource, StringComparison.Ordinal);
        Assert.Contains("MaximumLogicalWidth = 960", windowSource, StringComparison.Ordinal);
        Assert.Contains("CursorDisplayWidthRatio = 0.64d", windowSource, StringComparison.Ordinal);
        Assert.Contains("Activated += SearchWindow_Activated", windowSource, StringComparison.Ordinal);
        Assert.Contains("WindowActivationState.Deactivated", windowSource, StringComparison.Ordinal);
        Assert.Contains("Close();", windowSource, StringComparison.Ordinal);
        Assert.Contains("RootGrid.RequestedTheme = ElementTheme.Light;", windowSource, StringComparison.Ordinal);
        Assert.Contains("CompactLogicalHeight = 140", windowSource, StringComparison.Ordinal);
        Assert.Contains("ResultLogicalHeight = 180", windowSource, StringComparison.Ordinal);
        Assert.Contains("MaximumCursorDisplayHeightRatio = 0.78d", windowSource, StringComparison.Ordinal);
        Assert.Contains("ResizeForCurrentState();", windowSource, StringComparison.Ordinal);
        Assert.Contains("ResizeAndCenterOnCursorDisplay", placementSource, StringComparison.Ordinal);
        Assert.Contains("int maximumLogicalWidth", placementSource, StringComparison.Ordinal);
        Assert.Contains("int logicalHeight", placementSource, StringComparison.Ordinal);
        Assert.Contains("public const int MaximumResults = 20;", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Kinds = ImmutableHashSet.Create(StringComparer.Ordinal, \"screenshot\")", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("IncludeTextContent = true", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("MinimumQueryLength = 3", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("SearchDebounce", windowSource, StringComparison.Ordinal);
        Assert.Contains("ItemsStackPanel", window.ToString(), StringComparison.Ordinal);
        Assert.Contains("Limit = MaximumResults", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ToPlainTextPreview", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("CalculateSuggestionConfidence", viewModelSource, StringComparison.Ordinal);
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
        Assert.Contains("SearchIndexingRequested?.Invoke", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIndexingWindow_UsesAcrylicIndeterminateProgressAndCancellableFacadeOperation()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "SearchIndexingWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchIndexingWindow.xaml.cs"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var root = window.Descendants().Single(element => HasName(element, "RootGrid"));
        var resultsProgress = window.Descendants().Single(element => HasName(element, "ResultsProgressBar"));
        var suggestionsProgress = window.Descendants().Single(element => HasName(element, "SuggestionsProgressBar"));

        Assert.Contains(window.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Equal("Transparent", root.Attribute("Background")?.Value);
        Assert.Equal("True", resultsProgress.Attribute("IsIndeterminate")?.Value);
        Assert.Equal("True", suggestionsProgress.Attribute("IsIndeterminate")?.Value);
        Assert.Contains(window.Descendants(), element => HasName(element, "CancelOrCloseButton"));
        Assert.Contains("_application.RebuildSearchIndexAsync", source, StringComparison.Ordinal);
        Assert.Contains("_rebuildCancellation?.Cancel()", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.SearchIndexing", source, StringComparison.Ordinal);
        Assert.Contains("OptionsControl.SearchIndexingRequested += OptionsControl_SearchIndexingRequested", mainSource, StringComparison.Ordinal);
        Assert.Contains("new SearchIndexingWindow(", mainSource, StringComparison.Ordinal);
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
