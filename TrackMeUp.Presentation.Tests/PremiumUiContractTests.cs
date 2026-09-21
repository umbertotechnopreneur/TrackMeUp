// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Checks the shared Premium affordances and requested navigation simplifications.</summary>
public sealed class PremiumUiContractTests
{
    /// <summary>Save-protected windows show a single badge in the title bar, not in their editable content.</summary>
    [Theory]
    [InlineData("ScheduleWindow.xaml")]
    [InlineData("ReportExportWindow.xaml")]
    [InlineData("MainWindow.xaml")]
    public void PremiumBadge_IsUniqueAndInTitleBar(string file)
    {
        var document = XDocument.Load(PathFor("TrackMeUp", file));
        var badge = Assert.Single(document.Descendants(), element => element.Name.LocalName == "PremiumBadge");
        Assert.Equal("TitlePremiumBadge", Name(badge));
        Assert.Contains(badge.Ancestors(), element => Name(element)?.EndsWith("DragRegion", StringComparison.Ordinal) == true);
    }

    /// <summary>Labels stay left-aligned beside management, with no redundant history submenu.</summary>
    [Fact]
    public void MainWindow_KeepsLabelActionsTogetherAndHistoryActionsOnlyAtTopLevel()
    {
        var document = XDocument.Load(PathFor("TrackMeUp", "MainWindow.xaml"));
        var actions = document.Descendants().Single(element => Name(element) == "PlayerLabelActionsPanel");
        Assert.Equal("Right", actions.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Center", actions.Attribute("VerticalAlignment")?.Value);
        Assert.Contains(actions.Descendants(), element => Name(element) == "ManageLabelsButton"
            && element.Attribute("Grid.Row")?.Value == "1" && element.Attribute("HorizontalAlignment")?.Value == "Right");
        Assert.Contains(actions.Descendants(), element => Name(element) == "PlayerLabelFeatureGate" && element.Attribute("HorizontalAlignment")?.Value == "Right");
        Assert.DoesNotContain(actions.Descendants(), element => element.Name.LocalName == "PremiumBadge");
        Assert.DoesNotContain(document.Descendants(), element => Name(element) == "ActivityMenu");
        foreach (var name in new[] { "QuickSearchMenuItem", "QuickActivityCalendarMenuItem", "QuickScreenshotGalleryMenuItem" })
            Assert.Equal("MenuFlyout", document.Descendants().Single(element => Name(element) == name).Parent!.Name.LocalName);
    }

    /// <summary>The celestial menu shares the main menu geometry and uses a colored icon on every action.</summary>
    [Fact]
    public void WorldClocks_UseColoredMenuIconsAndAnAddButtonBadge()
    {
        var window = XDocument.Load(PathFor("TrackMeUp", "WorldClockWindow.xaml"));
        var menu = window.Descendants().Single(element => Name(element) == "WorldMapButton").Descendants().Single(element => element.Name.LocalName == "MenuFlyout");
        var setters = menu.Descendants().Where(element => element.Name.LocalName == "Setter").ToArray();
        Assert.Contains(setters, element => element.Attribute("Property")?.Value == "MinWidth" && element.Attribute("Value")?.Value == "320");
        Assert.Contains(setters, element => element.Attribute("Property")?.Value == "CornerRadius" && element.Attribute("Value")?.Value == "12");
        foreach (var item in menu.Descendants().Where(element => element.Name.LocalName == "MenuFlyoutItem"))
            Assert.Contains(item.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Foreground") is not null);
        var options = XDocument.Load(PathFor("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml"));
        var add = options.Descendants().Single(element => Name(element) == "AddClockButton");
        Assert.Contains(add.Parent!.Elements(), element => Name(element) == "AddClockPremiumBadge");
    }

    /// <summary>Only the existing separator remains around the OCR/AI settings action.</summary>
    [Fact]
    public void OcrSettings_HaveNoDecorativeHeadingOrDuplicateBorder()
    {
        var document = XDocument.Load(PathFor("TrackMeUp", "Controls", "OptionsControl.xaml"));
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Section.Ai");
        var action = document.Descendants().Single(element => Name(element) == "OcrAiSettingsButton");
        Assert.Equal("0", action.Attribute("BorderThickness")?.Value);
        Assert.DoesNotContain("OptionsPanoramaVioletBrush", document.ToString(), StringComparison.Ordinal);
    }

    /// <summary>The archive badge follows page navigation; Free execution invokes the shared upgrade dialog.</summary>
    [Fact]
    public void ProtectedActions_UseRuntimeAccessAndStandardDialogs()
    {
        var main = File.ReadAllText(PathFor("TrackMeUp", "MainWindow.xaml.cs"));
        Assert.Contains("_operationsControl?.IsArchivePageVisible == true", main, StringComparison.Ordinal);
        Assert.Contains("saveResult.Code == \"feature.premium_required\"", main, StringComparison.Ordinal);
        var archives = File.ReadAllText(PathFor("TrackMeUp", "Controls", "InstallationTransferOperationsControl.xaml.cs"));
        Assert.Equal(2, archives.Split("if (!await EnsureArchiveAccessAsync())", StringSplitOptions.None).Length - 1);
        Assert.Contains("ProductFeature.DataTransfer", archives, StringComparison.Ordinal);
        Assert.Contains("Context.Dialogs.ShowInformativeAsync", archives, StringComparison.Ordinal);
        var labels = File.ReadAllText(PathFor("TrackMeUp", "Controls", "ActivityLabelsEditor.cs"));
        Assert.Contains("result.Code == \"feature.label_limit\"", labels, StringComparison.Ordinal);
        Assert.Contains("button.BorderThickness = new Thickness(0);", labels, StringComparison.Ordinal);
        Assert.Contains("ToggleButtonBackground", labels, StringComparison.Ordinal);
    }

    private static string? Name(XElement element) => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name")?.Value;

    private static string PathFor(params string[] segments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TrackMeUp.slnx")))
                return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
        throw new DirectoryNotFoundException("Could not locate the TrackMeUp repository root.");
    }
}
