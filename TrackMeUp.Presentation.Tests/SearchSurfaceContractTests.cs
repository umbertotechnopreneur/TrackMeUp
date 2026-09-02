// SPDX-License-Identifier: MIT

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
        var mainWindow = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var resultControl = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "SearchResultItemControl.xaml"));
        var resultControlSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "SearchResultItemControl.xaml.cs"));
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchWindow.xaml.cs"));
        var mainWindowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var placementSource = File.ReadAllText(RepositoryFile("TrackMeUp", "WindowPlacementService.cs"));
        var viewModelSource = File.ReadAllText(RepositoryFile("TrackMeUp.Presentation", "SearchViewModel.cs"));
        var list = window.Descendants().Single(element => HasName(element, "SearchResultsList"));
        var queryBox = window.Descendants().Single(element => HasName(element, "QueryBox"));
        var activityGlow = window.Descendants().Single(element => HasName(element, "SearchActivityGlow"));
        var availability = window.Descendants().Single(element => HasName(element, "SearchAvailabilityText"));
        var activityStatus = window.Descendants().Single(element => HasName(element, "SearchActivityStatus"));
        var activityProgressRing = window.Descendants().Single(element => HasName(element, "SearchActivityProgressRing"));
        var emptyStateMarker = window.Descendants().Single(element =>
            element.Name.LocalName == "Ellipse"
            && element.Parent is { } parent
            && HasName(parent, "EmptyStatePanel"));
        var activityText = activityStatus.Descendants().Single(element =>
            element.Name.LocalName == "TextBlock" && element.Attribute("Tag")?.Value == "Search.Working");
        var resultThumbnailFrame = resultControl.Descendants().Single(element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Width")?.Value == "260"
            && element.Attribute("Height")?.Value == "146");
        var f3Shortcut = mainWindow.Descendants().Single(element =>
            element.Name.LocalName == "KeyboardAccelerator" && element.Attribute("Key")?.Value == "F3");
        var searchMenuItem = mainWindow.Descendants().Single(element => HasName(element, "SearchMenuItem"));

        Assert.Equal("Window", window.Root?.Name.LocalName);
        Assert.Equal("MainKeyboardAccelerator_Invoked", f3Shortcut.Attribute("Invoked")?.Value);
        Assert.Equal("F3 / Ctrl+Shift+P", searchMenuItem.Attribute("KeyboardAcceleratorTextOverride")?.Value);
        Assert.Contains("case Windows.System.VirtualKey.F3:", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains(window.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.DoesNotContain(window.Descendants(), element => HasName(element, "WindowTitleText"));
        Assert.Equal("48", queryBox.Attribute("MinHeight")?.Value);
        Assert.Equal("Center", queryBox.Attribute("VerticalContentAlignment")?.Value);
        Assert.Null(queryBox.Attribute("TextMemberPath"));
        Assert.Equal("1", availability.Parent?.Attribute("Grid.Row")?.Value);
        Assert.Equal("Search.Working", activityText.Attribute("Tag")?.Value);
        Assert.Equal("Polite", activityText.Attribute("AutomationProperties.LiveSetting")?.Value);
        Assert.Equal("Collapsed", activityStatus.Attribute("Visibility")?.Value);
        Assert.Equal("ProgressRing", activityProgressRing.Name.LocalName);
        Assert.Equal("True", activityProgressRing.Attribute("IsActive")?.Value);
        Assert.Equal("{StaticResource SearchActivityStatusBrush}", emptyStateMarker.Attribute("Fill")?.Value);
        Assert.DoesNotContain("SearchSuggestionAccentBrush", window.ToString(), StringComparison.Ordinal);
        Assert.Equal("Border", activityGlow.Name.LocalName);
        Assert.Equal("3", activityGlow.Attribute("Height")?.Value);
        Assert.Equal("Collapsed", activityGlow.Attribute("Visibility")?.Value);
        Assert.Null(activityGlow.Attribute("SizeChanged"));
        var activityGradientStops = activityGlow.Descendants()
            .Where(element => element.Name.LocalName == "GradientStop")
            .ToArray();
        Assert.Equal(3, activityGradientStops.Length);
        Assert.Equal("0", activityGradientStops[0].Attribute("Offset")?.Value);
        Assert.Equal("1", activityGradientStops[^1].Attribute("Offset")?.Value);
        Assert.DoesNotContain(window.Descendants(), element => HasName(element, "ResultCountText"));
        Assert.DoesNotContain(window.Descendants(), element => element.Name.LocalName == "AutoSuggestBox.ItemTemplate");
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
        Assert.Contains(resultControl.Descendants(), element => HasName(element, "MatchScoreChip"));
        Assert.Contains(resultControl.Descendants(), element => element.Attribute("Text")?.Value == "{Binding MatchLabel}");
        Assert.DoesNotContain(resultControl.Descendants(), element => element.Attribute("Text")?.Value == "MATCH");
        Assert.Contains(resultControl.Descendants(), element => element.Attribute("Text")?.Value == "{Binding MatchPercentDisplay}");
        Assert.Contains(resultControl.Descendants(), element => element.Attribute("Text")?.Value == "{Binding ActivityDisplay}");
        Assert.Contains(resultControl.Descendants(), element => HasName(element, "InstallationSourceBadge"));
        Assert.Contains(resultControl.Descendants(), element => HasName(element, "InstallationSourceIcon"));
        Assert.Contains(resultControl.Descendants(), element => HasName(element, "InstallationSourceText"));
        Assert.DoesNotContain(resultControl.Descendants(), element => element.Attribute("Text")?.Value?.Contains("CPU", StringComparison.Ordinal) == true);
        Assert.Contains("SnippetText.TextHighlighters", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("HoverThumbnailElevation = 18f", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("SemanticScoreColor", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("SmoothStep", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("SetThumbnailElevation(HoverThumbnailElevation)", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("SetThumbnailElevation(RestingThumbnailElevation)", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("RenderInstallation(result);", resultControlSource, StringComparison.Ordinal);
        Assert.Contains("InstallationProfileCatalog.Colors", resultControlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementCompositionPreview.SetIsTranslationEnabled", resultControlSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAlwaysOnTop", windowSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsResizable = false;", windowSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMinimizable = false;", windowSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMaximizable = false;", windowSource, StringComparison.Ordinal);
        Assert.Contains("MaximumLogicalWidth = 960", windowSource, StringComparison.Ordinal);
        Assert.Contains("CursorDisplayWidthRatio = 0.64d", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchWindow_Activated", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowActivationState.Deactivated", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CloseIfStillInactive", windowSource, StringComparison.Ordinal);
        Assert.Contains("RootGrid.RequestedTheme = ElementTheme.Light;", windowSource, StringComparison.Ordinal);
        Assert.Contains("CompactLogicalHeight = 168", windowSource, StringComparison.Ordinal);
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
        Assert.Contains("TimeSpan.FromMilliseconds(250)", windowSource, StringComparison.Ordinal);
        Assert.Contains("SearchActivityStatus.Visibility = Visibility.Visible", windowSource, StringComparison.Ordinal);
        Assert.Contains("SearchActivityStatus.Visibility = Visibility.Collapsed", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSuggestionsAsync", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchSuggestionDisplayItem", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ElementCompositionPreview", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Search.ResultCount", windowSource, StringComparison.Ordinal);
        Assert.Equal(2, windowSource.Split("BeginSearchActivity();", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, windowSource.Split("EndSearchActivity();", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("SuggestAsync", windowSource, StringComparison.Ordinal);
        Assert.Contains("ItemsStackPanel", window.ToString(), StringComparison.Ordinal);
        Assert.Contains("Limit = MaximumResults", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("ToPlainTextPreview", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("OrderByDescending(hit => hit.Score)", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("CalculateMatchPercent", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Func<long?, CultureInfo, string> formatClickCount", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Clicks —\"", viewModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("? \"click\" : \"clicks\"", viewModelSource, StringComparison.Ordinal);
        Assert.Contains("Search.Result.Match", windowSource, StringComparison.Ordinal);
        Assert.Contains("Search.Result.Clicks.One", windowSource, StringComparison.Ordinal);
        Assert.Contains("Search.Result.Clicks.Many", windowSource, StringComparison.Ordinal);
        Assert.Contains("Search.Result.Clicks.None", windowSource, StringComparison.Ordinal);
        Assert.Contains("GetSearchAvailabilityAsync", File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs")), StringComparison.Ordinal);
        Assert.Contains("Search.Empty.Title", File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void Options_ExposeSearchPreferencesAndOnlyOcrCanBeDisabled()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var localization = File.ReadAllText(RepositoryFile("TrackMeUp", "UiLocalization.cs"));

        Assert.Contains(options.Descendants(), element => HasName(element, "SearchOptionsView"));
        Assert.Contains(options.Descendants(), element => HasName(element, "SearchLanguageBox"));
        Assert.Contains(options.Descendants(), element => HasName(element, "SearchSynonymsSwitch"));
        Assert.Contains(options.Descendants(), element => HasName(element, "SearchTypoToleranceSwitch"));
        Assert.Contains(options.Descendants(), element => HasName(element, "OcrEnabledSwitch"));
        Assert.DoesNotContain(options.Descendants(), element => HasName(element, "SearchEnabledSwitch"));
        Assert.Contains("QueueAutoSave(\"ocr.enabled\"", source, StringComparison.Ordinal);
        Assert.Contains("QueueAutoSave(", source, StringComparison.Ordinal);
        Assert.Contains("\"search.synonyms\"", source, StringComparison.Ordinal);
        Assert.Contains("SearchIndexingRequested?.Invoke", source, StringComparison.Ordinal);
        Assert.Contains("DeclaredChildren(root)", localization, StringComparison.Ordinal);
        Assert.Contains("case Panel panel:", localization, StringComparison.Ordinal);
        Assert.Contains("case UserControl userControl", localization, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIndexingWindow_RendersMicaBeforeStartingCancellableFacadeOperation()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "SearchIndexingWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchIndexingWindow.xaml.cs"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var root = window.Descendants().Single(element => HasName(element, "RootGrid"));
        var resultsProgress = window.Descendants().Single(element => HasName(element, "ResultsProgressBar"));
        var suggestionsProgress = window.Descendants().Single(element => HasName(element, "SuggestionsProgressBar"));

        var backdrop = window.Descendants().Single(element => element.Name.LocalName == "MicaBackdrop");
        Assert.Equal("BaseAlt", backdrop.Attribute("Kind")?.Value);
        Assert.Equal("Transparent", root.Attribute("Background")?.Value);
        Assert.Equal("True", resultsProgress.Attribute("IsIndeterminate")?.Value);
        Assert.Equal("True", suggestionsProgress.Attribute("IsIndeterminate")?.Value);
        Assert.Contains(window.Descendants(), element => HasName(element, "CancelOrCloseButton"));
        Assert.Contains("_application.RebuildSearchIndexAsync", source, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, RunIndexingFromVisibleWindow)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("await RunIndexingAsync();", source[source.IndexOf("private async void RootGrid_Loaded", StringComparison.Ordinal)..source.IndexOf("private async void RunIndexingFromVisibleWindow", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("_rebuildCancellation?.Cancel()", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.SearchIndexing", source, StringComparison.Ordinal);
        Assert.Contains("options.SearchIndexingRequested += OptionsControl_SearchIndexingRequested", mainSource, StringComparison.Ordinal);
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
