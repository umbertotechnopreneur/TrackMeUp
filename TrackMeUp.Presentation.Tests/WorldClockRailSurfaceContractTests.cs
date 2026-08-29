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
    public void Player_TitleBarToggleCollapsesAndRestoresTheWorldClockRailAccessibly()
    {
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var toggle = main.Descendants().Single(element => HasName(element, "WorldClockRailToggleButton"));
        var rail = main.Descendants().Single(element => HasName(element, "WorldClockRail"));
        var railColumn = main.Descendants().Single(element => HasName(element, "WorldClockRailColumn"));
        var dragRegion = main.Descendants().Single(element => HasName(element, "DragRegion"));
        var captionColumn = main.Descendants().Single(element => HasName(element, "TitleBarCaptionColumn"));
        var systemButtonGap = main.Descendants().Single(element => HasName(element, "TitleBarSystemButtonGapColumn"));
        var icon = toggle.Descendants().Single(element => element.Name.LocalName == "SymbolIcon");

        Assert.Equal("WorldClockRailToggleButton_Click", toggle.Attribute("Click")?.Value);
        Assert.Equal("WorldClock.ShowRail", toggle.Attribute("Tag")?.Value);
        Assert.Equal("True", toggle.Attribute("AllowFocusOnInteraction")?.Value);
        Assert.Equal("True", toggle.Attribute("IsTabStop")?.Value);
        Assert.Equal(
            AttributeValue(toggle, "AutomationProperties.Name"),
            AttributeValue(toggle, "ToolTipService.ToolTip"));
        Assert.Equal("World", icon.Attribute("Symbol")?.Value);
        Assert.True(toggle.Parent is not null && HasName(toggle.Parent, "DragRegion"));
        Assert.Equal("*", captionColumn.Attribute("Width")?.Value);
        Assert.Equal("64", captionColumn.Attribute("MinWidth")?.Value);
        Assert.Equal("12", systemButtonGap.Attribute("Width")?.Value);
        Assert.Equal("0", railColumn.Attribute("Width")?.Value);
        Assert.Equal("1", rail.Attribute("Grid.Row")?.Value);
        Assert.Null(rail.Attribute("Grid.RowSpan"));
        Assert.Equal("Collapsed", rail.Attribute("Visibility")?.Value);
        Assert.Equal("2", dragRegion.Attribute("Grid.ColumnSpan")?.Value);
        Assert.Null(dragRegion.Attribute("Grid.Column"));
        Assert.Contains("private bool _isWorldClockRailVisible;", source, StringComparison.Ordinal);
        Assert.Contains("_isWorldClockRailVisible = !_isWorldClockRailVisible;", source, StringComparison.Ordinal);
        Assert.Contains("WorldClockRailColumn.Width = isVisible", source, StringComparison.Ordinal);
        Assert.Contains("WorldClockRail.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("WorldClock.ShowRail", source, StringComparison.Ordinal);
        Assert.Contains("WorldClock.HideRail", source, StringComparison.Ordinal);
        Assert.Contains("_worldClockRefreshTimer.Stop();", source, StringComparison.Ordinal);
        Assert.Contains("passthroughRects.Add(ElementRect(WorldClockRailToggleButton, scale));", source, StringComparison.Ordinal);
        Assert.Contains("QueueTitleBarLayoutUpdate();", source, StringComparison.Ordinal);
        Assert.Contains("RootGrid.UpdateLayout();", source, StringComparison.Ordinal);
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
        var heroActions = hero.Descendants().Single(element =>
            element.Name.LocalName == "StackPanel" &&
            element.Attribute("Orientation")?.Value == "Horizontal" &&
            element.Elements().Count(child => child.Name.LocalName == "Button") == 2);
        var compactActions = compact.Descendants().Single(element =>
            element.Name.LocalName == "StackPanel" &&
            element.Attribute("Orientation")?.Value == "Horizontal" &&
            element.Elements().Count(child => child.Name.LocalName == "Button") == 2);
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
        Assert.Null(heroActions.Attribute("Opacity"));
        Assert.Null(compactActions.Attribute("Opacity"));
        Assert.All(
            heroActions.Elements().Concat(compactActions.Elements()),
            button =>
            {
                Assert.Equal("Transparent", button.Attribute("Background")?.Value);
                Assert.Equal("0", button.Attribute("BorderThickness")?.Value);
                Assert.Null(button.Attribute("BorderBrush"));
                Assert.Null(button.Attribute("IsHitTestVisible"));
            });
        Assert.Equal("0", railRoot.Attribute("Margin")?.Value);
        Assert.Equal("0", railRoot.Attribute("Padding")?.Value);
        Assert.Equal("0", itemsPanel.Attribute("Spacing")?.Value);
        Assert.Equal("0", card.Attribute("BorderThickness")?.Value);
        Assert.Equal("0", card.Attribute("CornerRadius")?.Value);
        Assert.Null(card.Attribute("BorderBrush"));
        Assert.Null(card.Attribute("Translation"));
        Assert.Null(card.Attribute("PointerEntered"));
        Assert.Null(card.Attribute("PointerExited"));
        Assert.Null(card.Attribute("GotFocus"));
        Assert.Null(card.Attribute("LostFocus"));
        Assert.DoesNotContain(card.Elements(), element => element.Name.LocalName == "Border.Shadow");
        Assert.Contains("index == 0", source, StringComparison.Ordinal);
        Assert.Contains("HeroVisibility", source, StringComparison.Ordinal);
        Assert.Contains("CompactVisibility", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionsVisible", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionsOpacity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CardBorderBrush", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ClockCard_", source, StringComparison.Ordinal);
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
