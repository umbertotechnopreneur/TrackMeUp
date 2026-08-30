using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class WorldClockRailSurfaceContractTests
{
    [Fact]
    public void Player_UsesReusableAccessibleWorldClockRail()
    {
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var rail = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));

        Assert.Contains(main.Descendants(), element => HasName(element, "WorldClockRail"));
        Assert.Contains(rail.Descendants(), element => HasName(element, "AddClockButton"));
        Assert.All(
            rail.Descendants().Where(element => element.Name.LocalName == "Button"),
            button =>
            {
                Assert.Contains(button.Attributes(), attribute => attribute.Name.LocalName == "AutomationProperties.Name" && !string.IsNullOrWhiteSpace(attribute.Value));
                Assert.Contains(button.Attributes(), attribute => attribute.Name.LocalName == "ToolTipService.ToolTip" && !string.IsNullOrWhiteSpace(attribute.Value));
            });
        Assert.Contains("GetWorldClockRailAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetWorldClockCityCatalogAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CityPicker_UsesTheCompleteCatalogInOneSearchableComboBox()
    {
        var picker = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockCityPickerDialogWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockCityPickerDialogWindow.xaml.cs"));
        var comboBox = picker.Descendants().Single(element => HasName(element, "CityComboBox"));

        Assert.Equal("ComboBox", comboBox.Name.LocalName);
        Assert.Equal("DisplayName", comboBox.Attribute("DisplayMemberPath")?.Value);
        Assert.Equal("True", comboBox.Attribute("IsTextSearchEnabled")?.Value);
        Assert.Equal("320", comboBox.Attribute("MaxDropDownHeight")?.Value);
        Assert.Equal("CityComboBox_SelectionChanged", comboBox.Attribute("SelectionChanged")?.Value);
        Assert.DoesNotContain(picker.Descendants(), element => element.Name.LocalName is "AutoSuggestBox" or "ListView");
        Assert.Contains("CityComboBox.ItemsSource = _options;", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.WorldClockCityPicker", source, StringComparison.Ordinal);
        Assert.Contains("_placement.ApplyDefaultSize(RootGrid);", source, StringComparison.Ordinal);
        Assert.Contains("await _placement.RestoreAsync(RootGrid, CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("_appWindow.Closing += AppWindow_Closing;", source, StringComparison.Ordinal);
        Assert.Contains("await _placement.TrySaveForCloseAsync(CancellationToken.None);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Rail_LeavesTheWindowMaterialVisibleWithoutAnOuterCard()
    {
        var rail = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml.cs"));
        var railRoot = Assert.Single(rail.Root?.Elements() ?? []);

        Assert.Equal("Grid", railRoot.Name.LocalName);
        Assert.True(HasName(railRoot, "RailRoot"));
        Assert.Null(railRoot.Attribute("Background"));
        Assert.Null(railRoot.Attribute("BorderBrush"));
        Assert.Null(railRoot.Attribute("BorderThickness"));
        Assert.Null(railRoot.Attribute("CornerRadius"));
        Assert.Equal("Custom", AttributeValue(railRoot, "AutomationProperties.LandmarkType"));
        Assert.Contains("WorldClock.Landmark", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(RailRoot, landmarkName);", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetLocalizedLandmarkType(RailRoot, landmarkName);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("#F5121416", rail.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(rail.Descendants(), element => HasName(element, "HeaderText"));
        Assert.DoesNotContain("ClockCard_DataContextChanged", rail.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ClockCard_DataContextChanged", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_TitleBarClockOpensAFlyoutWithoutResizingOrMovingTheWindow()
    {
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var button = main.Descendants().Single(element => HasName(element, "WorldClockButton"));
        var flyout = main.Descendants().Single(element => HasName(element, "WorldClockFlyout"));
        var rail = main.Descendants().Single(element => HasName(element, "WorldClockRail"));
        var dragRegion = main.Descendants().Single(element => HasName(element, "DragRegion"));
        var captionColumn = main.Descendants().Single(element => HasName(element, "TitleBarCaptionColumn"));
        var title = main.Descendants().Single(element => HasName(element, "TitleBarTitleText"));
        var systemButtonGap = main.Descendants().Single(element => HasName(element, "TitleBarSystemButtonGapColumn"));
        var icon = button.Descendants().Single(element => element.Name.LocalName == "SymbolIcon");

        Assert.Null(button.Attribute("Click"));
        Assert.Equal("WorldClock.ShowRail", button.Attribute("Tag")?.Value);
        Assert.Equal("True", button.Attribute("AllowFocusOnInteraction")?.Value);
        Assert.Equal("True", button.Attribute("IsTabStop")?.Value);
        Assert.Equal(
            AttributeValue(button, "AutomationProperties.Name"),
            AttributeValue(button, "ToolTipService.ToolTip"));
        Assert.Equal("Clock", icon.Attribute("Symbol")?.Value);
        Assert.True(button.Parent is not null && HasName(button.Parent, "DragRegion"));
        Assert.Contains(button.Descendants(), element => ReferenceEquals(element, flyout));
        Assert.Equal("BottomEdgeAlignedLeft", flyout.Attribute("Placement")?.Value);
        Assert.Equal("False", flyout.Attribute("ShouldConstrainToRootBounds")?.Value);
        Assert.Equal("WorldClockFlyout_Opened", flyout.Attribute("Opened")?.Value);
        Assert.Equal("WorldClockFlyout_Closed", flyout.Attribute("Closed")?.Value);
        Assert.Equal("278", rail.Attribute("Width")?.Value);
        Assert.Equal("560", rail.Attribute("Height")?.Value);
        Assert.Equal("*", captionColumn.Attribute("Width")?.Value);
        Assert.Equal("64", captionColumn.Attribute("MinWidth")?.Value);
        Assert.Equal("12", systemButtonGap.Attribute("Width")?.Value);
        Assert.Equal("2", button.Attribute("Grid.Column")?.Value);
        Assert.Equal("3", title.Attribute("Grid.Column")?.Value);
        Assert.Null(dragRegion.Attribute("Grid.Column"));
        Assert.Null(dragRegion.Attribute("Grid.ColumnSpan"));
        Assert.Contains("private bool _isWorldClockFlyoutOpen;", source, StringComparison.Ordinal);
        Assert.Contains("_isWorldClockFlyoutOpen = true;", source, StringComparison.Ordinal);
        Assert.Contains("_isWorldClockFlyoutOpen = false;", source, StringComparison.Ordinal);
        Assert.Contains("_worldClockRefreshTimer.Stop();", source, StringComparison.Ordinal);
        Assert.Contains("passthroughRects.Add(ElementRect(WorldClockButton, scale));", source, StringComparison.Ordinal);
        Assert.Contains("QueueTitleBarLayoutUpdate();", source, StringComparison.Ordinal);
        Assert.Contains("RootGrid.UpdateLayout();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldClockRailColumn", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LogicalWorldClockRailWidth", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetRegionRects(NonClientRegionKind.Caption", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Rail_UsesHeroAndCompactCardsWithoutTheSupersededLinearTimeline()
    {
        var rail = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml.cs"));
        var hero = rail.Descendants().Single(element => element.Attribute("Visibility")?.Value == "{Binding HeroVisibility}");
        var compact = rail.Descendants().Single(element => element.Attribute("Visibility")?.Value == "{Binding CompactVisibility}");
        var heroCelestial = hero.Descendants().Single(element => element.Name.LocalName == "CelestialPhaseControl");
        var compactCelestial = compact.Descendants().Single(element => element.Name.LocalName == "CelestialPhaseControl");
        var heroActions = hero.Descendants().Single(element => HasName(element, "HeroClockActions"));
        var compactActions = compact.Descendants().Single(element => HasName(element, "CompactClockActions"));
        var heroFirstRow = hero.Descendants().First(element => element.Name.LocalName == "RowDefinition");
        var cardTemplate = rail.Descendants().Single(element => element.Name.LocalName == "DataTemplate");
        var card = cardTemplate.Elements().Single(element => element.Name.LocalName == "Border");
        var itemsPanel = rail.Descendants().Single(element =>
            element.Name.LocalName == "ItemsPanelTemplate").Elements().Single();
        var railRoot = rail.Descendants().Single(element => HasName(element, "RailRoot"));

        Assert.Equal("116", heroCelestial.Attribute("Width")?.Value);
        Assert.Equal("116", heroCelestial.Attribute("Height")?.Value);
        Assert.Equal("70", compactCelestial.Attribute("Width")?.Value);
        Assert.Equal("70", compactCelestial.Attribute("Height")?.Value);
        Assert.Equal("Auto", heroFirstRow.Attribute("Height")?.Value);
        Assert.Equal("0,1,1,0", heroActions.Attribute("Margin")?.Value);
        Assert.Equal("0,1,1,0", compactActions.Attribute("Margin")?.Value);
        Assert.Equal("0", heroActions.Attribute("Opacity")?.Value);
        Assert.Equal("0", compactActions.Attribute("Opacity")?.Value);
        Assert.Equal("False", heroActions.Attribute("IsHitTestVisible")?.Value);
        Assert.Equal("False", compactActions.Attribute("IsHitTestVisible")?.Value);
        Assert.All(
            heroActions.Elements().Concat(compactActions.Elements()),
            button =>
            {
                Assert.Equal("Transparent", button.Attribute("Background")?.Value);
                Assert.Equal("0", button.Attribute("BorderThickness")?.Value);
                Assert.Null(button.Attribute("BorderBrush"));
                Assert.Null(button.Attribute("IsHitTestVisible"));
                Assert.Equal("ClockActionButton_GotFocus", button.Attribute("GotFocus")?.Value);
                Assert.Equal("ClockActionButton_LostFocus", button.Attribute("LostFocus")?.Value);
            });
        Assert.Equal("0", railRoot.Attribute("Margin")?.Value);
        Assert.Equal("0", railRoot.Attribute("Padding")?.Value);
        Assert.Equal("0", itemsPanel.Attribute("Spacing")?.Value);
        Assert.Equal("0", card.Attribute("BorderThickness")?.Value);
        Assert.Equal("0", card.Attribute("CornerRadius")?.Value);
        Assert.Null(card.Attribute("BorderBrush"));
        Assert.Null(card.Attribute("Translation"));
        Assert.Equal("ClockCard_PointerEntered", card.Attribute("PointerEntered")?.Value);
        Assert.Equal("ClockCard_PointerExited", card.Attribute("PointerExited")?.Value);
        Assert.Null(card.Attribute("GotFocus"));
        Assert.Null(card.Attribute("LostFocus"));
        Assert.DoesNotContain(card.Elements(), element => element.Name.LocalName == "Border.Shadow");
        Assert.Contains("index == 0", source, StringComparison.Ordinal);
        Assert.Contains("HeroVisibility", source, StringComparison.Ordinal);
        Assert.Contains("CompactVisibility", source, StringComparison.Ordinal);
        Assert.Contains("SetClockActionsVisible", source, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(140)", source, StringComparison.Ordinal);
        Assert.Contains("DoubleAnimation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardBorderBrush", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockCard_DataContextChanged", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TimelineMarkerMargin", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DaylightMargin", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DaylightWidth", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpandedDetails_UseReusableDaylightArcControl()
    {
        var rail = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml"));
        var arc = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "DaylightArcControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "DaylightArcControl.xaml.cs"));
        var usage = rail.Descendants().Single(element => element.Name.LocalName == "DaylightArcControl");
        var details = usage.Parent
            ?? throw new InvalidDataException("The daylight arc must remain inside the expanded details surface.");
        var arcRoot = arc.Descendants().Single(element => HasName(element, "ArcRoot"));
        string[] expectedHourLabels = ["0", "6", "12", "18", "24"];
        var actualHourLabels = arc.Descendants()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => element.Attribute("Text")?.Value
                ?? throw new InvalidDataException("Every daylight-arc label must define Text."))
            .ToArray();

        Assert.Equal("{Binding CurrentHour}", usage.Attribute("CurrentHour")?.Value);
        Assert.Equal("{Binding SunriseHour}", usage.Attribute("SunriseHour")?.Value);
        Assert.Equal("{Binding SunsetHour}", usage.Attribute("SunsetHour")?.Value);
        Assert.Equal("StackPanel", details.Name.LocalName);
        Assert.Equal("{Binding DetailsVisibility}", details.Attribute("Visibility")?.Value);
        Assert.Equal("0,9,0,8", details.Attribute("Padding")?.Value);
        var cardContent = details.Parent
            ?? throw new InvalidDataException("The expanded details must remain inside the clock-card content stack.");
        Assert.Equal("0,12,0,13", cardContent.Attribute("Padding")?.Value);
        Assert.All(
            cardContent.Elements().Where(element => element.Name.LocalName == "Grid" && element.Attribute("MinHeight") is not null),
            clockLayout => Assert.Equal("14,0,14,0", clockLayout.Attribute("Margin")?.Value));
        Assert.Null(details.Attribute("Background"));
        Assert.Null(details.Attribute("BorderBrush"));
        Assert.Null(details.Attribute("BorderThickness"));
        Assert.Equal("{Binding IsDaylight}", usage.Attribute("IsDaylight")?.Value);
        Assert.Equal("Raw", AttributeValue(arcRoot, "AutomationProperties.AccessibilityView"));
        Assert.Equal("False", arcRoot.Attribute("IsHitTestVisible")?.Value);
        Assert.Contains(arc.Descendants(), element => HasName(element, "FullArc"));
        Assert.Contains(arc.Descendants(), element => HasName(element, "DayArc"));
        Assert.Contains(arc.Descendants(), element => HasName(element, "CurrentMarker"));
        Assert.Equal(expectedHourLabels, actualHourLabels);
        Assert.Contains("CurrentHourProperty", source, StringComparison.Ordinal);
        Assert.Contains("SunriseHourProperty", source, StringComparison.Ordinal);
        Assert.Contains("SunsetHourProperty", source, StringComparison.Ordinal);
        Assert.Contains("BuildArc", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CelestialPhaseControl_UsesPackagedSunAndMoonAssets()
    {
        var celestial = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "CelestialPhaseControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "CelestialPhaseControl.xaml.cs"));
        var project = XDocument.Load(RepositoryFile("TrackMeUp", "TrackMeUp.csproj"));
        var assetSources = celestial.Descendants()
            .Where(element => element.Name.LocalName is "Image" or "ImageBrush")
            .Select(element => AttributeValue(
                element,
                element.Name.LocalName == "Image" ? "Source" : "ImageSource"))
            .ToArray();
        var celestialContent = project.Descendants().Single(element =>
            element.Name.LocalName == "Content" &&
            element.Attribute("Include")?.Value == @"Assets\Celestial\*.png");
        string[] expectedAssetSources =
        [
            "ms-appx:///Assets/Celestial/sun-premium.png",
            "ms-appx:///Assets/Celestial/moon-premium.png",
        ];

        Assert.Equal(expectedAssetSources, assetSources);
        Assert.All(
            new[] { "sun-premium.png", "moon-premium.png" },
            assetName => Assert.True(
                new FileInfo(RepositoryFile("TrackMeUp", "Assets", "Celestial", assetName)).Length > 0,
                $"Celestial asset '{assetName}' must exist and contain image data."));
        Assert.Equal(
            "PreserveNewest",
            celestialContent.Elements().Single(element => element.Name.LocalName == "CopyToOutputDirectory").Value);
        Assert.Equal(
            "PreserveNewest",
            celestialContent.Elements().Single(element => element.Name.LocalName == "CopyToPublishDirectory").Value);
        Assert.Contains("IsDaylightProperty", source, StringComparison.Ordinal);
        Assert.Contains("MoonPhaseAngleDegreesProperty", source, StringComparison.Ordinal);
        Assert.Contains("SunVisual.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("MoonVisual.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("MoonShadow.Data = BuildMoonShadow", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AddButton_UsesOneLocalizedLabelAndDisappearsAtTheMaximum()
    {
        var rail = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockRailControl.xaml.cs"));
        var addButton = rail.Descendants().Single(element => HasName(element, "AddClockButton"));
        var accessibleName = AttributeValue(addButton, "AutomationProperties.Name");
        var tooltip = AttributeValue(addButton, "ToolTipService.ToolTip");

        Assert.False(string.IsNullOrWhiteSpace(accessibleName));
        Assert.Equal(accessibleName, tooltip);
        Assert.Contains("WorldClock.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldClock.Header", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldClock.MaximumReached", source, StringComparison.Ordinal);
        Assert.Contains("AddClockHost.Visibility = addVisibility;", source, StringComparison.Ordinal);
        Assert.Contains("AddClockButton.Visibility = addVisibility;", source, StringComparison.Ordinal);
        Assert.Contains("AddClockButton.IsEnabled = _canAdd;", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(AddClockButton, addName);", source, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(AddClockButton, addName);", source, StringComparison.Ordinal);
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

    private static string AttributeValue(XElement element, string localName) =>
        element.Attributes().Single(attribute => attribute.Name.LocalName == localName).Value;
}
