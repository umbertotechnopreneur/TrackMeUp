// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class WinUiSurfaceContractTests
{
    private static readonly string[] CanonicalUiLocales = ProductLanguageCatalog.UiLocales.ToArray();

    [Fact]
    public void ExecutableManifest_DeclaresPerMonitorV2DpiAwareness()
    {
        var manifest = XDocument.Load(RepositoryFile("TrackMeUp", "app.manifest"));

        Assert.Contains(
            manifest.Descendants(),
            element => element.Name.LocalName == "dpiAwareness" && element.Value.Trim() == "PerMonitorV2");
    }

    [Fact]
    public void CompactSurfaces_ScrollWhereNeededAndKeepAboutFixed()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var about = XDocument.Load(RepositoryFile("TrackMeUp", "AboutWindow.xaml"));
        var aboutSource = File.ReadAllText(RepositoryFile("TrackMeUp", "AboutWindow.xaml.cs"));
        var licenses = XDocument.Load(RepositoryFile("TrackMeUp", "ThirdPartyLicensesWindow.xaml"));
        var licensesSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ThirdPartyLicensesWindow.xaml.cs"));

        Assert.Contains(player.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal(2, options.Descendants().Count(element => element.Name.LocalName == "ScrollViewer"));
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger");
        Assert.DoesNotContain(about.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.DoesNotContain(about.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.Contains(about.Descendants(), element => HasName(element, "HeroImage") && element.Name.LocalName == "Image");
        Assert.DoesNotContain(about.Descendants(), element => element.Name.LocalName is "ThemeShadow" or "LinearGradientBrush");
        Assert.Contains(about.Descendants(), element => HasName(element, "CreatedByButton") && element.Attribute("Content")?.Value == "umbertogiacobbi.biz");
        Assert.DoesNotContain(about.Descendants(), element => element.Name.LocalName == "HyperlinkButton" || element.Attribute("NavigateUri") is not null);
        Assert.Contains(about.Descendants(), element => HasName(element, "ShowLogButton"));
        Assert.Contains(about.Descendants(), element => HasName(element, "ShareLogButton"));
        Assert.Contains(about.Descendants(), element => HasName(element, "IssuesButton"));
        Assert.Contains(about.Descendants(), element => HasName(element, "LicensesButton"));
        Assert.Contains(about.Descendants(), element => HasName(element, "RepositoryFooterButton"));
        Assert.Contains(about.Descendants(), element => element.Attribute("Tag")?.Value == "About.FavoriteMessage");
        Assert.Contains(licenses.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(licenses.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(licenses.Descendants(), element => element.Name.LocalName == "ItemsControl" && element.Attribute("ItemsSource")?.Value.Contains("LicenseRows", StringComparison.Ordinal) == true);
        Assert.Contains("ThirdPartyLicenseCatalog.RuntimeDependencies", licensesSource, StringComparison.Ordinal);
        var licenseRowGrid = licenses.Descendants().Single(element => element.Name.LocalName == "Grid" && element.Attribute("RowSpacing")?.Value == "4");
        var licenseRowDefinitions = licenseRowGrid.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions");
        Assert.Equal(2, licenseRowDefinitions.Elements().Count(element => element.Name.LocalName == "RowDefinition"));
        Assert.Contains(about.Descendants(), element => HasName(element, "DiagnosticsInfoBar"));
        Assert.Contains(about.Descendants(), element => HasName(element, "CloseButton") && element.Attribute("HorizontalAlignment")?.Value == "Right");
        Assert.DoesNotContain(about.Descendants(), element => element.Attribute("Text")?.Value == "•••");
        Assert.Contains("private const int LogicalWindowWidth = 940;", aboutSource, StringComparison.Ordinal);
        Assert.Contains("private const int LogicalWindowHeight = 650;", aboutSource, StringComparison.Ordinal);
        Assert.Contains("_titleBar = new CustomTitleBarController(", aboutSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetTitleBar(TitleBarDragRegion);", aboutSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsResizable = false;", aboutSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMaximizable = false;", aboutSource, StringComparison.Ordinal);
        Assert.Contains("_application.OpenApplicationLogFolderAsync", aboutSource, StringComparison.Ordinal);
        Assert.Contains("_application.ShareApplicationLogAsync", aboutSource, StringComparison.Ordinal);
        Assert.Contains("_application.OpenProductLinkAsync", aboutSource, StringComparison.Ordinal);
        Assert.Contains("ThirdPartyLicensesWindow", aboutSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", aboutSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", aboutSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", aboutSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsAndOperations_KeepFlatHierarchyWithScopedModelFeedback()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));

        var optionExpanders = options.Descendants().Where(element => element.Name.LocalName == "Expander").ToArray();
        Assert.Equal(2, optionExpanders.Length);
        Assert.Contains(optionExpanders, element => HasName(element, "ApiKeyExpander"));
        Assert.Contains(optionExpanders, element => HasName(element, "AiDailyLimitExpander"));
        Assert.DoesNotContain(operations.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.DoesNotContain(operations.Descendants(), element => element.Attribute("CornerRadius") is not null);
        Assert.DoesNotContain(operations.Descendants(), element => element.Attribute("Click")?.Value == "BackButton_Click");
        Assert.DoesNotContain(operations.Descendants(), element => element.Attribute("Tag")?.Value is "Operations.Title" or "Operations.Subtitle");
        Assert.Equal("Collapsed", operations.Descendants().Single(element => HasName(element, "OperationProgress")).Attribute("Visibility")?.Value);
        Assert.Contains(options.Descendants(), element => element.Attribute("Style")?.Value.Contains("BodyStrongTextBlockStyle", StringComparison.Ordinal) == true);
        Assert.Contains(operations.Descendants(), element => element.Attribute("Style")?.Value.Contains("SubtitleTextBlockStyle", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(options.Descendants(), element => element.Attribute("Tag")?.Value.StartsWith("Options.Section.ActiveHours", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("active_hours.", optionsSource, StringComparison.Ordinal);
        Assert.Contains(options.Descendants(), element => HasName(element, "ModelInfoCard") && element.Attribute("CornerRadius")?.Value == "6");
        Assert.Contains(options.Descendants(), element => HasName(element, "ModelAccentBar") && element.Attribute("CornerRadius")?.Value == "2");
        var ocrAiView = options.Descendants().Single(element => HasName(element, "OcrAiOptionsView"));
        Assert.Single(ocrAiView.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal(1, options.Descendants().Count(element => element.Name.LocalName == "StackPanel" && element.Attribute("Padding")?.Value == "0,0,18,12"));
        Assert.Contains(options.Descendants(), element => HasName(element, "OptionsColumnsGrid"));
        Assert.Contains(options.Descendants(), element => HasName(element, "OptionsSecondaryColumn") && element.Attribute("Width")?.Value == "0");
        Assert.Contains(options.Descendants(), element =>
            element.Name.LocalName == "AdaptiveTrigger" && element.Attribute("MinWindowWidth")?.Value == "560");
        Assert.DoesNotContain(options.Descendants(), element => HasName(element, "SaveOptionsButton"));
        Assert.Contains("RegisterAutoSaveHandlers();", optionsSource, StringComparison.Ordinal);
        Assert.Contains("QueueAutoSave", optionsSource, StringComparison.Ordinal);
        var openFolderButton = options.Descendants().Single(element => HasName(element, "OpenScreenshotFolderButton"));
        Assert.Null(openFolderButton.Attribute("Content"));
        Assert.Contains(openFolderButton.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE8A7");
        var keepScreenshots = options.Descendants().Single(element => HasName(element, "KeepScreenshotsSwitch"));
        Assert.Equal("Right", keepScreenshots.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("0", keepScreenshots.Attribute("MinWidth")?.Value);
        Assert.Contains(options.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Section.Startup");
        Assert.Contains(options.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Section.Snapshots");
        Assert.DoesNotContain(options.Descendants(), element => element.Attribute("Tag")?.Value == "Options.ExportReport");
        Assert.DoesNotContain("ExportReportButton_Click", optionsSource, StringComparison.Ordinal);
        foreach (var compactSwitchName in new[] { "StartWithWindowsSwitch", "StartTrackingOnLaunchSwitch", "ScreenshotsEnabledSwitch" })
        {
            var compactSwitch = options.Descendants().Single(element => HasName(element, compactSwitchName));
            Assert.Equal("1", compactSwitch.Attribute("Grid.Column")?.Value);
            Assert.Equal("0", compactSwitch.Attribute("MinWidth")?.Value);
            Assert.Null(compactSwitch.Attribute("Header"));
        }
    }

    [Fact]
    public void OpenAiOptions_UseCatalogPickerAndPerSnapshotLayout()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var aiView = options.Descendants().Single(element => HasName(element, "AiOptionsView"));
        var modelPicker = options.Descendants().Single(element => HasName(element, "ModelBox"));
        var testConnectionButton = options.Descendants().Single(element => HasName(element, "TestConnectionButton"));
        var thinkingEffortPicker = options.Descendants().Single(element => HasName(element, "AiReasoningEffortBox"));

        Assert.Equal("ComboBox", modelPicker.Name.LocalName);
        Assert.Equal("Options.Model", modelPicker.Attribute("Tag")?.Value);
        Assert.Contains(modelPicker.Descendants(), element => element.Name.LocalName == "DataTemplate");
        Assert.Contains(modelPicker.Descendants(), element => element.Attribute("Background")?.Value == "{Binding AccentBrush}");
        Assert.Equal("ComboBox", thinkingEffortPicker.Name.LocalName);
        Assert.Equal("Options.Reasoning", thinkingEffortPicker.Attribute("Tag")?.Value);
        Assert.Contains(thinkingEffortPicker.Ancestors(), element => ReferenceEquals(element, aiView));
        Assert.DoesNotContain(thinkingEffortPicker.Ancestors(), element => HasName(element, "AiAdvancedPanel"));
        Assert.Contains(testConnectionButton.Ancestors(), element => ReferenceEquals(element, aiView));
        Assert.Equal("TestConnectionButton_Click", testConnectionButton.Attribute("Click")?.Value);
        Assert.DoesNotContain(options.Descendants(), element =>
            HasName(element, "AnalysisIntervalBox") || HasName(element, "AutomaticAnalysisBox"));
        Assert.DoesNotContain(options.Descendants(), element =>
            element.Attribute("Tag")?.Value.Contains("AnalysisInterval", StringComparison.Ordinal) == true
            || element.Attribute("Tag")?.Value.Contains("AiPrivacy", StringComparison.Ordinal) == true);
        Assert.Contains("GetAiModelCatalogAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainMenu_ExposesActionsFromAlwaysVisibleEllipsis()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var titleBarSource = File.ReadAllText(RepositoryFile("TrackMeUp", "CustomTitleBarController.cs"));
        var captureActions = player.Descendants().Single(element => HasName(element, "CaptureActionsPanel"));
        var takeScreenshotButton = player.Descendants().Single(element => HasName(element, "TakeScreenshotButton"));
        var pendingSnapshotPanel = player.Descendants().Single(element => HasName(element, "PendingSnapshotPanel"));
        var deleteSnapshotButton = player.Descendants().Single(element => HasName(element, "DeleteSnapshotButton"));
        var deleteAvailableLabel = pendingSnapshotPanel.Descendants().Single(element => element.Attribute("Tag")?.Value == "Snapshot.DeleteAvailable");
        var deleteCountdown = pendingSnapshotPanel.Descendants().Single(element => HasName(element, "SnapshotDeleteCountdownText"));
        var searchButton = player.Descendants().Single(element => HasName(element, "TitleBarSearchButton"));
        var reportButton = player.Descendants().Single(element => HasName(element, "TitleBarReportButton"));
        var minimizeToTrayButton = player.Descendants().Single(element => HasName(element, "TitleBarMinimizeToTrayButton"));
        var minimizeToTrayMenuItem = player.Descendants().Single(element => HasName(element, "MinimizeToTrayMenuItem"));
        var moreButton = player.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "TitleBarMoreButton"));
        var dragRegion = player.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "DragRegion"));
        var playerPanel = player.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "PlayerPanel"));
        var menu = player.Descendants().Single(element => element.Name.LocalName == "MenuFlyout" && element.Attribute("Opened")?.Value == "MoreMenu_Opened");
        var menuTags = menu
            .Descendants()
            .Where(element => element.Attribute("Tag") is not null)
            .Select(element => element.Attribute("Tag")!.Value)
            .ToArray();

        Assert.Equal("{StaticResource TrackMeUpTitleBarCommandButtonStyle}", moreButton.Attribute("Style")?.Value);
        Assert.Null(moreButton.Attribute("Visibility"));
        Assert.Equal("6", searchButton.Attribute("Grid.Column")?.Value);
        Assert.Equal("TitleBarSearchButton_Click", searchButton.Attribute("Click")?.Value);
        Assert.Contains(searchButton.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE721");
        Assert.Equal("7", reportButton.Attribute("Grid.Column")?.Value);
        Assert.Null(reportButton.Attribute("Margin"));
        Assert.Equal("TitleBarReportButton_Click", reportButton.Attribute("Click")?.Value);
        Assert.Contains(reportButton.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE9F9");
        Assert.Equal("Collapsed", reportButton.Attribute("Visibility")?.Value);
        Assert.Equal("8", minimizeToTrayButton.Attribute("Grid.Column")?.Value);
        Assert.Null(minimizeToTrayButton.Attribute("Margin"));
        Assert.Equal("MinimizeToTrayButton_Click", minimizeToTrayButton.Attribute("Click")?.Value);
        Assert.Equal("Main.Menu.MinimizeToTray", minimizeToTrayButton.Attribute("Tag")?.Value);
        Assert.Equal("Minimize to notification area", minimizeToTrayButton.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal("Minimize to notification area", minimizeToTrayButton.Attribute("ToolTipService.ToolTip")?.Value);
        Assert.Equal("Collapsed", minimizeToTrayButton.Attribute("Visibility")?.Value);
        Assert.Contains(minimizeToTrayButton.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE921");
        Assert.Equal("5", moreButton.Attribute("Grid.Column")?.Value);
        Assert.Null(moreButton.Attribute("Margin"));
        Assert.Contains(
            moreButton.Descendants(),
            element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE712");
        Assert.Equal("TopEdgeAlignedRight", menu.Attribute("Placement")?.Value);
        Assert.Equal("False", menu.Attribute("ShouldConstrainToRootBounds")?.Value);
        Assert.Equal("Horizontal", captureActions.Attribute("Orientation")?.Value);
        Assert.Equal("32", takeScreenshotButton.Attribute("Width")?.Value);
        Assert.Equal("Transparent", takeScreenshotButton.Attribute("Background")?.Value);
        Assert.Equal("Collapsed", pendingSnapshotPanel.Attribute("Visibility")?.Value);
        Assert.Equal("32", deleteSnapshotButton.Attribute("Width")?.Value);
        Assert.Equal("Snapshot.Delete", deleteSnapshotButton.Attribute("Tag")?.Value);
        Assert.Equal("Delete snapshot", deleteSnapshotButton.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal(deleteSnapshotButton.Attribute("AutomationProperties.Name")?.Value, deleteSnapshotButton.Attribute("ToolTipService.ToolTip")?.Value);
        Assert.Equal("Transparent", deleteSnapshotButton.Attribute("Background")?.Value);
        Assert.Same(takeScreenshotButton.Parent, deleteSnapshotButton.Parent);
        Assert.Contains(captureActions.Descendants(), element => ReferenceEquals(element, deleteSnapshotButton));
        Assert.Equal("Collapsed", deleteSnapshotButton.Attribute("Visibility")?.Value);
        Assert.Equal("1", pendingSnapshotPanel.Parent?.Attribute("Grid.Column")?.Value);
        Assert.Equal("Content", pendingSnapshotPanel.Attribute("AutomationProperties.AccessibilityView")?.Value);
        Assert.Equal("{ThemeResource SystemFillColorCriticalBrush}", deleteAvailableLabel.Attribute("Foreground")?.Value);
        Assert.Equal("Raw", deleteAvailableLabel.Attribute("AutomationProperties.AccessibilityView")?.Value);
        Assert.Equal("2", deleteCountdown.Attribute("Grid.Column")?.Value);
        Assert.Equal("36", deleteCountdown.Attribute("MinWidth")?.Value);
        Assert.Equal("Right", deleteCountdown.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Right", deleteCountdown.Attribute("TextAlignment")?.Value);
        Assert.DoesNotContain(pendingSnapshotPanel.Descendants(), element => element.Name.LocalName == "ProgressBar");
        Assert.DoesNotContain(pendingSnapshotPanel.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE74D");
        Assert.Contains("TakeScreenshotButton.IsEnabled = false;", mainSource, StringComparison.Ordinal);
        Assert.Contains("TakeScreenshotButton.IsEnabled = _screenshotStorageReady && enableCapture;", mainSource, StringComparison.Ordinal);
        Assert.Contains("HidePendingSnapshotDeleteUi(enableCapture: true);", mainSource, StringComparison.Ordinal);
        Assert.Contains("HidePendingSnapshotDeleteUi(enableCapture: false);", mainSource, StringComparison.Ordinal);
        Assert.Contains("FormatPendingSnapshotCountdown(remaining)", mainSource, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(remaining.TotalSeconds, 1d, PendingSnapshotDeleteSeconds)", mainSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetHelpText(DeleteSnapshotButton, accessibleStatus);", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"00:{Math.Max", mainSource, StringComparison.Ordinal);
        Assert.Equal(4, menu.Descendants().Count(element => element.Name.LocalName == "MenuFlyoutSubItem"));
        Assert.Equal(2, menu.Descendants().Count(element => element.Name.LocalName == "ToggleMenuFlyoutItem"));
        Assert.DoesNotContain(menu.Descendants(), element => element.Name.LocalName == "Button");
        Assert.All(
            menu.Descendants().Where(element => element.Name.LocalName is "MenuFlyoutSubItem" or "MenuFlyoutItem" or "ToggleMenuFlyoutItem"),
            item => Assert.False(string.IsNullOrWhiteSpace(item.Attribute("ToolTipService.ToolTip")?.Value)));
        Assert.Contains(MenuGlyph(player, "ReportsMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE9F9");
        Assert.Contains(MenuGlyph(player, "ActivityCalendarMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE787");
        Assert.Contains(MenuGlyph(player, "ScreenshotsMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE8B9");
        Assert.Contains(MenuGlyph(player, "CaptureMenu"), element => element.Attribute("Glyph")?.Value == "\uE722");
        Assert.Contains(MenuGlyph(player, "ScheduleMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE8C0");
        Assert.Contains(MenuGlyph(player, "ScreenshotsMenuToggle"), element => element.Attribute("Glyph")?.Value == "\uE8B8");
        Assert.Contains(MenuGlyph(player, "QuickSetupMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE9D5");
        Assert.Contains(MenuGlyph(player, "OperationsMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE90F");
        Assert.Contains(MenuGlyph(player, "AiProviderMenu"), element => element.Attribute("Glyph")?.Value == "\uE99A");
        Assert.Contains(MenuGlyph(player, "OpenAiMenuToggle"), element => element.Attribute("Glyph")?.Value == "\uE9A3");
        Assert.Contains(MenuGlyph(player, "AiPricingMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE8C7");
        Assert.Contains(MenuGlyph(player, "MinimizeToTrayMenuItem"), element => element.Attribute("Glyph")?.Value == "\uE921");
        Assert.Equal(
            ["Main.Menu.Activity", "Search.Title", "Reports.Title", "ActivityCalendar.MenuTitle", "Screenshots.Caption", "Main.Menu.Capture", "Schedule.Snapshots", "MenuToggleScreenshot", "Main.Menu.Settings", "QuickSetup.MenuTitle", "MenuTitleOptions", "Main.Menu.Operations", "Main.Menu.AiProvider", "MenuToggleOpenAi", "AiPricing.MenuTitle", "Main.Menu.MinimizeToTray", "MenuTitleAbout"],
            menuTags);
        Assert.Equal("Main.Menu.MinimizeToTray", minimizeToTrayMenuItem.Attribute("Tag")?.Value);
        Assert.Equal("MinimizeToTrayButton_Click", minimizeToTrayMenuItem.Attribute("Click")?.Value);
        Assert.Equal(
            minimizeToTrayMenuItem.Attribute("Text")?.Value,
            minimizeToTrayMenuItem.Attribute("ToolTipService.ToolTip")?.Value);
        Assert.Contains("flyout.ShowAt(TitleBarMoreButton);", mainSource, StringComparison.Ordinal);
        Assert.Contains("TitleBarMoreButton,", mainSource, StringComparison.Ordinal);
        Assert.Contains("TitleBarSearchButton,", mainSource, StringComparison.Ordinal);
        Assert.Contains("TitleBarReportButton,", mainSource, StringComparison.Ordinal);
        Assert.Contains("TitleBarMinimizeToTrayButton", mainSource, StringComparison.Ordinal);
        Assert.Contains("element.Visibility == Visibility.Visible", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("ShowPanel(OperationsPanel, MainWindowSurface.Operations);", mainSource, StringComparison.Ordinal);
        Assert.Contains("TitleBarBackButton.Visibility = Visibility.Visible;", mainSource, StringComparison.Ordinal);
        Assert.Contains("OperationsControl.NavigateBack();", mainSource, StringComparison.Ordinal);
        Assert.Contains("OperationsControl_BackRequested", mainSource, StringComparison.Ordinal);
        Assert.Contains("options.OperationsSectionRequested += OptionsControl_OperationsSectionRequested", mainSource, StringComparison.Ordinal);
        Assert.Contains("OperationsControl.NavigateTo(section, returnToOverview: false);", mainSource, StringComparison.Ordinal);
        Assert.Contains("ApplyMainMenuLabels();", mainSource, StringComparison.Ordinal);
        Assert.Contains("ApplyMenuAccessibility", mainSource, StringComparison.Ordinal);
        Assert.Contains("AiPricingMenuItem.IsEnabled = IsOpenAiPricingAvailable(result.Value);", mainSource, StringComparison.Ordinal);
        Assert.Contains("[\"screenshots.enabled\"]", mainSource, StringComparison.Ordinal);
        Assert.Contains("CaptureManualScreenshotAsync", mainSource, StringComparison.Ordinal);
        Assert.Contains("DeletePendingManualScreenshotAsync", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyzeCapturedScreenshotAsync", mainSource, StringComparison.Ordinal);
        Assert.Contains("FormatCurrentContext(currentContext)", mainSource, StringComparison.Ordinal);
        Assert.Contains("T(_layoutState.IsLastSessionVisible ? \"LastSession.Hide\" : \"LastSession.Show\")", mainSource, StringComparison.Ordinal);
        Assert.Single(dragRegion.Descendants(), element => element.Attribute("Text")?.Value == "TRACK ME UP");
        Assert.DoesNotContain(playerPanel.Descendants(), element => element.Attribute("Text")?.Value == "TRACK ME UP");
        Assert.DoesNotContain("InputNonClientPointerSource", mainSource, StringComparison.Ordinal);
        Assert.Contains("InputNonClientPointerSource", titleBarSource, StringComparison.Ordinal);
        Assert.Contains(player.Descendants(), element => element.Name.LocalName == "ToggleMenuFlyoutItem");
    }

    [Fact]
    public void ScreenshotWindow_GroupsViewerCommandsInTheNativeHeaderToolbar()
    {
        var screenshotWindow = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml"));
        var header = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotHeaderControl.xaml"));
        var viewer = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));
        var headerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotHeaderControl.xaml.cs"));
        var viewerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml.cs"));
        var actionBanner = screenshotWindow.Descendants().Single(element => HasName(element, "ScreenshotActionBanner"));
        var toolbar = header.Descendants().Single(element => HasName(element, "ScreenshotToolbar"));
        var namedToolbarControls = toolbar
            .Descendants()
            .Where(element => element.Name.LocalName is "AppBarButton" or "AppBarToggleButton")
            .Select(element => element.Attributes().Single(attribute => attribute.Name.LocalName == "Name").Value)
            .ToArray();
        var actionButtons = new[]
        {
            (Name: "SaveButton", Tag: "Screenshots.Toolbar.Save", Click: "SaveButton_Click"),
            (Name: "ShareButton", Tag: "Screenshots.Toolbar.Share", Click: "ShareButton_Click"),
            (Name: "OpenFolderButton", Tag: "Screenshots.Toolbar.OpenFolder", Click: "OpenFolderButton_Click"),
            (Name: "DeleteScreenshotButton", Tag: "Screenshots.Toolbar.DeleteScreenshot", Click: "DeleteScreenshotButton_Click"),
            (Name: "DeleteAnalysisButton", Tag: "Screenshots.Toolbar.DeleteAnalysis", Click: "DeleteAnalysisButton_Click")
        };

        Assert.Equal("TimedInfoBar", actionBanner.Name.LocalName);
        Assert.DoesNotContain(screenshotWindow.Descendants(), element => HasName(element, "TitleBarMoreButton"));
        Assert.DoesNotContain(screenshotWindow.Descendants(), element => element.Name.LocalName is "Flyout" or "MenuFlyout");
        Assert.DoesNotContain("MoreMenu_Opened", source, StringComparison.Ordinal);
        Assert.Equal(
            ["ZoomOutButton", "ZoomResetButton", "ZoomInButton", "DetailsToggleButton", "SaveButton", "ShareButton", "OpenFolderButton", "DeleteScreenshotButton", "DeleteAnalysisButton"],
            namedToolbarControls);
        Assert.Equal(
            Array.IndexOf(namedToolbarControls, "SaveButton") - 1,
            Array.IndexOf(namedToolbarControls, "DetailsToggleButton"));
        Assert.Equal("CommandBar", toolbar.Name.LocalName);
        Assert.Equal("Transparent", toolbar.Attribute("Background")?.Value);
        Assert.Equal("0", toolbar.Attribute("BorderThickness")?.Value);
        Assert.Equal(3, toolbar.Descendants().Count(element => element.Name.LocalName == "AppBarSeparator"));
        Assert.DoesNotContain(toolbar.Descendants(), element => element.Name.LocalName is "Border" or "ThemeShadow");
        foreach (var action in actionButtons)
        {
            var button = toolbar.Descendants().Single(element => HasName(element, action.Name));
            Assert.Equal(action.Tag, button.Attribute("Tag")?.Value);
            Assert.Equal(action.Click, button.Attribute("Click")?.Value);
            Assert.Contains(button.Descendants(), element =>
                element.Name.LocalName == "FontIcon"
                && element.Attribute("AutomationProperties.AccessibilityView")?.Value == "Raw");
        }

        Assert.Contains("HeaderSection.SaveRequested += HeaderSection_SaveRequested;", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.ShareRequested += HeaderSection_ShareRequested;", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.OpenFolderRequested += HeaderSection_OpenFolderRequested;", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.DeleteScreenshotRequested += HeaderSection_DeleteScreenshotRequested;", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.DeleteAnalysisRequested += HeaderSection_DeleteAnalysisRequested;", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.ZoomOutRequested += HeaderSection_ZoomOutRequested;", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.ZoomResetRequested += HeaderSection_ZoomResetRequested;", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.ZoomInRequested += HeaderSection_ZoomInRequested;", source, StringComparison.Ordinal);
        Assert.Contains("await SaveSelectedScreenshotAsync();", source, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? SaveRequested;", headerSource, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? ShareRequested;", headerSource, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? OpenFolderRequested;", headerSource, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? DeleteScreenshotRequested;", headerSource, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? DeleteAnalysisRequested;", headerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveRequested", viewerSource, StringComparison.Ordinal);
        Assert.DoesNotContain(viewer.Descendants(), element =>
            element.Name.LocalName is "Button" or "ToggleButton" or "AppBarButton" or "AppBarToggleButton" or "CommandBar");
        Assert.Contains("ShowActionResult(result", source, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowSuccessBanner(ScreenshotActionBanner, title, message);", source, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowErrorBanner(ScreenshotActionBanner, title, message);", source, StringComparison.Ordinal);
        Assert.Contains("DeleteScreenshotAsync", source, StringComparison.Ordinal);
        Assert.Contains("DeleteScreenshotAnalysisAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenshotWindow_UsesFullAcrylicSingleImageHierarchyWithSafeMarkdownDetails()
    {
        var screenshotWindow = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml"));
        var header = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotHeaderControl.xaml"));
        var gallery = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotGalleryViewControl.xaml"));
        var viewer = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml"));
        var details = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotDetailsControl.xaml"));
        var dayOverview = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotDayOverviewControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));
        var headerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotHeaderControl.xaml.cs"));
        var viewerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml.cs"));
        var detailsToggle = header.Descendants().Single(element => HasName(element, "DetailsToggleButton"));
        var toolbar = header.Descendants().Single(element => HasName(element, "ScreenshotToolbar"));
        var detailsPane = screenshotWindow.Descendants().Single(element => HasName(element, "DetailsPane"));
        var resizeGrip = screenshotWindow.Descendants().Single(element => HasName(element, "DetailsResizeGrip"));
        var gallerySection = screenshotWindow.Descendants().Single(element => HasName(element, "GallerySection"));
        var timeline = screenshotWindow.Descendants().Single(element => HasName(element, "TimelineSection"));
        var sidebarBrushes = screenshotWindow.Descendants()
            .Where(element => element.Name.LocalName == "AcrylicBrush" && HasKey(element, "ScreenshotSidebarBackdropBrush"))
            .ToArray();

        Assert.Contains(screenshotWindow.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.DoesNotContain(screenshotWindow.Descendants(), element => element.Name.LocalName == "Rectangle");
        Assert.Equal("AppBarToggleButton", detailsToggle.Name.LocalName);
        Assert.Equal("DetailsToggleButton_Click", detailsToggle.Attribute("Click")?.Value);
        Assert.Equal("Collapsed", detailsPane.Attribute("Visibility")?.Value);
        Assert.Equal("2", detailsPane.Attribute("Grid.Row")?.Value);
        Assert.Equal("3", detailsPane.Attribute("Grid.RowSpan")?.Value);
        Assert.Equal("{ThemeResource ScreenshotSidebarBackdropBrush}", detailsPane.Attribute("Background")?.Value);
        Assert.Equal("{ThemeResource ScreenshotSidebarBorderBrush}", detailsPane.Attribute("BorderBrush")?.Value);
        Assert.Equal("1,0,0,0", detailsPane.Attribute("BorderThickness")?.Value);
        Assert.Equal("0", detailsPane.Attribute("CornerRadius")?.Value);
        Assert.Equal("0", detailsPane.Attribute("Margin")?.Value);
        Assert.Equal("0,0,0", detailsPane.Attribute("Translation")?.Value);
        Assert.DoesNotContain(detailsPane.Descendants(), element => element.Name.LocalName == "ThemeShadow");
        Assert.Equal(2, sidebarBrushes.Length);
        Assert.All(sidebarBrushes, brush => Assert.InRange(
            double.Parse(brush.Attribute("TintOpacity")!.Value, System.Globalization.CultureInfo.InvariantCulture),
            0d,
            0.15d));
        Assert.Equal(2, screenshotWindow.Descendants().Count(element =>
            element.Name.LocalName == "LinearGradientBrush" && HasKey(element, "ScreenshotSidebarBorderBrush")));
        Assert.Null(gallerySection.Attribute("Grid.ColumnSpan"));
        Assert.Equal("0", gallerySection.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", timeline.Attribute("Grid.ColumnSpan")?.Value);
        Assert.Equal("HorizontalResizeGrip", resizeGrip.Name.LocalName);
        Assert.Equal("DetailsResizeGrip_PointerPressed", resizeGrip.Attribute("PointerPressed")?.Value);
        Assert.Equal("DetailsResizeGrip_PointerMoved", resizeGrip.Attribute("PointerMoved")?.Value);
        Assert.Equal("DetailsResizeGrip_PointerReleased", resizeGrip.Attribute("PointerReleased")?.Value);
        Assert.Equal("DetailsResizeGrip_PointerCanceled", resizeGrip.Attribute("PointerCanceled")?.Value);
        Assert.Equal("DetailsResizeGrip_PointerCaptureLost", resizeGrip.Attribute("PointerCaptureLost")?.Value);
        Assert.Equal("DetailsResizeGrip_KeyDown", resizeGrip.Attribute("KeyDown")?.Value);
        Assert.Equal("Screenshots.Details.Resize", resizeGrip.Attribute("Tag")?.Value);
        Assert.Equal("12", resizeGrip.Attribute("Width")?.Value);
        Assert.Contains("public event Action<bool>? DetailsVisibilityRequested;", headerSource, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.DetailsVisibilityRequested += HeaderSection_DetailsVisibilityRequested;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DetailsVisibilityRequested", viewerSource, StringComparison.Ordinal);
        Assert.Contains("private const double MaximumDetailsPaneWidthRatio = 0.5d;", source, StringComparison.Ordinal);
        Assert.Contains("_detailsResizeStartWidth + (_detailsResizeStartPointerX - currentPointerX)", source, StringComparison.Ordinal);
        Assert.Contains("grip.CapturePointer(e.Pointer)", source, StringComparison.Ordinal);
        Assert.Contains("grip.ReleasePointerCapture(e.Pointer)", source, StringComparison.Ordinal);
        Assert.Contains("RootGrid.ActualWidth * MaximumDetailsPaneWidthRatio", source, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(requestedWidth, minimumWidth, maximumWidth)", source, StringComparison.Ordinal);
        Assert.Contains("ScreenshotDetailsProjection.Create", source, StringComparison.Ordinal);
        Assert.Contains("_detailsPaneOpenPreference = result.Value.ScreenshotDetailsPaneOpen;", source, StringComparison.Ordinal);
        Assert.Contains("SetDetailsPaneVisibility(hasItems && _detailsPaneOpenPreference);", source, StringComparison.Ordinal);
        Assert.Contains("_application.PatchSettingsAsync(", source, StringComparison.Ordinal);
        Assert.Contains("[\"screenshots.details_pane_open\"] = isVisible ? \"true\" : \"false\"", source, StringComparison.Ordinal);
        Assert.Contains(header.Descendants(), element =>
            HasName(element, "ExtendedDateText")
            && element.Attribute("FontSize")?.Value == "24"
            && element.Attribute("FontWeight")?.Value == "SemiLight");
        Assert.Equal("CommandBar", toolbar.Name.LocalName);
        Assert.Equal("1", toolbar.Attribute("Grid.Column")?.Value);
        Assert.Equal("Transparent", toolbar.Attribute("Background")?.Value);
        Assert.Equal("0", toolbar.Attribute("BorderThickness")?.Value);
        var datePicker = header.Descendants().Single(element => HasName(element, "SelectedDatePicker"));
        Assert.Equal("CalendarDatePicker", datePicker.Name.LocalName);
        Assert.Equal("200", datePicker.Attribute("Width")?.Value);
        Assert.Equal("40", datePicker.Attribute("Height")?.Value);
        Assert.NotEqual("0", datePicker.Attribute("Opacity")?.Value);
        Assert.NotEqual("False", datePicker.Attribute("IsTabStop")?.Value);
        Assert.DoesNotContain(header.Descendants(), element =>
            element.Name.LocalName == "Button"
            && element.Attribute("Click")?.Value == "OpenDatePickerButton_Click");
        Assert.Contains("SelectedDatePicker.DateChanged += SelectedDatePicker_DateChanged;", source, StringComparison.Ordinal);
        Assert.Contains("await LoadGalleryAsync(_selectedDate);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCalendarOpen = true", File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotHeaderControl.xaml.cs")), StringComparison.Ordinal);
        var privacyStatus = header.Descendants().Single(element => HasName(element, "PrivacyStatusBadge"));
        Assert.Equal("StackPanel", privacyStatus.Name.LocalName);
        Assert.Null(privacyStatus.Attribute("Background"));
        Assert.Null(privacyStatus.Attribute("BorderBrush"));
        Assert.Null(privacyStatus.Attribute("BorderThickness"));
        Assert.Contains(dayOverview.Descendants(), element => HasName(element, "MarkerCanvas"));
        Assert.Contains(dayOverview.Descendants(), element => HasName(element, "SelectionRangeIndicator"));
        Assert.Contains(gallery.Descendants(), element => element.Name.LocalName == "ScreenshotImageViewerControl");
        Assert.DoesNotContain(gallery.Descendants(), element => HasName(element, "MetadataPanel"));
        Assert.DoesNotContain(viewer.Descendants(), element =>
            element.Name.LocalName is "Button" or "ToggleButton" or "AppBarButton" or "AppBarToggleButton" or "CommandBar");
        Assert.DoesNotContain(header.Descendants(), element => HasKey(element, "ScreenshotMetadataChipStyle"));
        Assert.DoesNotContain(toolbar.Descendants(), element => element.Name.LocalName is "Border" or "ThemeShadow");
        Assert.Contains(header.Descendants(), element => HasName(element, "MetadataDateValueText"));
        Assert.Contains(header.Descendants(), element => HasName(element, "MetadataTimeValueText"));
        Assert.Contains(header.Descendants(), element => HasName(element, "MetadataAppValueText"));
        Assert.DoesNotContain(header.Descendants(), element => HasName(element, "MetadataActivityIndexValueText"));
        Assert.Contains("HeaderSection.SetMetadata(", source, StringComparison.Ordinal);
        Assert.Contains("HeaderSection.ClearMetadata();", source, StringComparison.Ordinal);
        Assert.Contains(details.Descendants(), element => HasName(element, "AiMarkdownHost"));
        Assert.Contains(details.Descendants(), element => HasName(element, "PrivacyStatusValueText"));
        Assert.Contains(details.Descendants(), element => HasName(element, "WindowTitleValueText"));
        Assert.Contains(details.Descendants(), element => element.Attribute("Style")?.Value.Contains("ScreenshotDetailRowStyle", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(details.Descendants(), element => element.Name.LocalName is "WebView2" or "Hyperlink" or "HyperlinkButton");
    }

    [Fact]
    public void SnapshotSchedule_UsesASeparateThemedWindow()
    {
        var schedule = XDocument.Load(RepositoryFile("TrackMeUp", "ScheduleWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var scheduleSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ScheduleWindow.xaml.cs"));
        var weeklyHours = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml"));
        var weeklyHoursSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WeeklyHoursEditor.xaml.cs"));

        Assert.Equal("Window", schedule.Root?.Name.LocalName);
        Assert.Contains(schedule.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(schedule.Descendants(), element => HasName(element, "WorkingHoursEditor"));
        Assert.Equal(
            "Inline",
            schedule.Descendants().Single(element => HasName(element, "IntervalNumberBox")).Attribute("SpinButtonPlacementMode")?.Value);
        Assert.Equal("Rectangle", weeklyHours.Descendants().Single(element => HasName(element, "GridInteractionSurface")).Name.LocalName);
        Assert.Contains("GridInteractionSurface.CapturePointer", weeklyHoursSource, StringComparison.Ordinal);
        Assert.Contains("GridInteractionSurface_PointerMoved", weeklyHoursSource, StringComparison.Ordinal);
        Assert.Contains("settingsResult.Value.ScreenshotIntervalMinutes", mainSource, StringComparison.Ordinal);
        Assert.Contains("ScheduleConfirmed += ScheduleWindow_ScheduleConfirmed", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleScreenshotDialog", mainSource, StringComparison.Ordinal);
        Assert.Contains("RootGrid.RequestedTheme = theme switch", scheduleSource, StringComparison.Ordinal);
        Assert.Contains("_titleBar = new CustomTitleBarController(", scheduleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetTitleBar(TitleBarDragRegion);", scheduleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_IsFixedSizeAndRestoresUserPlacementWithoutAutomaticRecentering()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));

        Assert.Contains("presenter.IsResizable = false;", source, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMaximizable = false;", source, StringComparison.Ordinal);
        Assert.Contains("ResizeForLogicalContent(_layoutState.LogicalHeight);", source, StringComparison.Ordinal);
        Assert.Contains("ResizeForCurrentLayout(animate: false);", source, StringComparison.Ordinal);
        Assert.Contains("_appWindow.Changed += AppWindow_Changed;", source, StringComparison.Ordinal);
        Assert.Contains("var workArea = CurrentWorkArea();", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Main", source, StringComparison.Ordinal);
        Assert.Contains("await _placement.RestoreAsync(RootGrid, _lifecycle.Token);", source, StringComparison.Ordinal);
        Assert.Contains("await _placement.TrySaveForCloseAsync(CancellationToken.None);", source, StringComparison.Ordinal);
        Assert.Contains("_placement.KeepCurrentBoundsInWorkArea(RootGrid);", source, StringComparison.Ordinal);
        Assert.Contains("positionChangedByUser", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyFlyoutPosition(_position);\r\n        Activate();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DetachedWindows_UseSharedWindowPlacementService()
    {
        var placement = File.ReadAllText(RepositoryFile("TrackMeUp", "WindowPlacementService.cs"));
        var reports = File.ReadAllText(RepositoryFile("TrackMeUp", "ReportsWindow.xaml.cs"));
        var worldClocks = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var screenshots = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));
        var search = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchWindow.xaml.cs"));
        var about = File.ReadAllText(RepositoryFile("TrackMeUp", "AboutWindow.xaml.cs"));
        var schedule = File.ReadAllText(RepositoryFile("TrackMeUp", "ScheduleWindow.xaml.cs"));
        var searchIndexing = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchIndexingWindow.xaml.cs"));
        var core = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "WindowStateService.cs"));

        Assert.Contains("RestoreWindowStateAsync", placement, StringComparison.Ordinal);
        Assert.Contains("SaveWindowStateAsync", placement, StringComparison.Ordinal);
        Assert.Contains("RestoreAndCenterAsync", placement, StringComparison.Ordinal);
        Assert.Contains("RestoreAsync", placement, StringComparison.Ordinal);
        Assert.Contains("ApplyDefaultSize", placement, StringComparison.Ordinal);
        Assert.Contains("OpeningWorkArea()", placement, StringComparison.Ordinal);
        Assert.Contains("KeepCurrentBoundsInWorkArea", placement, StringComparison.Ordinal);
        Assert.Contains("WindowStateService.GetMinimumSize(_windowKey)", placement, StringComparison.Ordinal);
        Assert.Contains("WmGetMinMaxInfo = 0x0024", placement, StringComparison.Ordinal);
        Assert.Contains("SetWindowSubclass(_windowHandle, _subclassProc, _subclassId, 0)", placement, StringComparison.Ordinal);
        Assert.Contains("RemoveWindowSubclass(_windowHandle, _subclassProc, _subclassId)", placement, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Reports", reports, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.WorldClocks", worldClocks, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Screenshots", screenshots, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Search", search, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.About", about, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Schedule", schedule, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.SearchIndexing", searchIndexing, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Screenshots => new(760, 540)", core, StringComparison.Ordinal);
        Assert.Contains("_placement.Dispose();", reports, StringComparison.Ordinal);
        Assert.Contains("_placement.Dispose();", worldClocks, StringComparison.Ordinal);
        Assert.Contains("_placement.Dispose();", screenshots, StringComparison.Ordinal);
        Assert.Contains("_placement.Dispose();", searchIndexing, StringComparison.Ordinal);
        Assert.DoesNotContain("private void ResizeForLogicalContent()", screenshots, StringComparison.Ordinal);
        Assert.DoesNotContain("private void ResizeForLogicalContent()", reports, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowClosePlacementFailures_DoNotEscapeAsyncVoidCallbacksOrSkipCleanup()
    {
        var placement = File.ReadAllText(RepositoryFile("TrackMeUp", "WindowPlacementService.cs"));
        var closeSources = new[]
        {
            "AboutWindow.xaml.cs",
            "ReportsWindow.xaml.cs",
            "SearchWindow.xaml.cs",
            "ScheduleWindow.xaml.cs",
            "SearchIndexingWindow.xaml.cs",
            "QuickSetupWindow.xaml.cs",
            "OcrTextWindow.xaml.cs",
            "ThirdPartyLicensesWindow.xaml.cs",
            "WorldClockCityPickerDialogWindow.xaml.cs",
            "ActivityCalendarDialogWindow.xaml.cs",
            "AiPricingDialogWindow.xaml.cs",
            "AiConnectionTestDialogWindow.xaml.cs",
            "AiScreenshotReprocessingDialogWindow.xaml.cs",
        }.Select(file => File.ReadAllText(RepositoryFile("TrackMeUp", file)));

        Assert.Contains("internal async Task<bool> TrySaveForCloseAsync", placement, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", placement, StringComparison.Ordinal);
        Assert.Contains("Trace.TraceError", placement, StringComparison.Ordinal);
        Assert.All(closeSources, source =>
        {
            Assert.Contains("TrySaveForCloseAsync", source, StringComparison.Ordinal);
            Assert.DoesNotContain("_placement.SaveAsync(CancellationToken.None)", source, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ExpandedPlayer_ShowsSnapshotPlaceholderAndSeparatedLocalUtcClock()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var windowInterop = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Infrastructure", "Services", "WindowInteropService.cs"));
        var localTime = player.Descendants().Single(element => HasName(element, "LocalTimeText"));
        var utcTime = player.Descendants().Single(element => HasName(element, "UtcTimeText"));
        var clockRow = localTime.Parent ?? throw new InvalidOperationException("Local and UTC clocks must have a layout row.");
        var placeholder = player.Descendants().Single(element => HasName(element, "ScreenshotPlaceholderImage"));
        var previewButton = player.Descendants().Single(element => HasName(element, "ScreenshotPreviewButton"));
        var previewSurface = player.Descendants().Single(element => HasName(element, "ScreenshotPreviewSurface"));
        var screenshotStatus = player.Descendants().Single(element => HasName(element, "ScreenshotStatusText"));
        var openOverlay = player.Descendants().Single(element => HasName(element, "ScreenshotOpenOverlay"));

        Assert.Equal("Left", localTime.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Right", utcTime.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("0,6,0,10", clockRow.Attribute("Margin")?.Value);
        Assert.Equal("11", localTime.Attribute("FontSize")?.Value);
        Assert.Equal("11", utcTime.Attribute("FontSize")?.Value);
        Assert.Equal("ms-appx:///Assets/TrackMeUpSnapshotPlaceholder.png", placeholder.Attribute("Source")?.Value);
        Assert.Equal("132", previewButton.Attribute("Width")?.Value);
        Assert.Equal("92", previewButton.Attribute("Height")?.Value);
        Assert.Equal("0", previewButton.Attribute("BorderThickness")?.Value);
        Assert.Equal("124", previewSurface.Attribute("Width")?.Value);
        Assert.Equal("78", previewSurface.Attribute("Height")?.Value);
        Assert.Equal("0,0,20", previewSurface.Attribute("Translation")?.Value);
        Assert.Null(previewSurface.Attribute("BorderBrush"));
        Assert.Null(previewSurface.Attribute("BorderThickness"));
        Assert.Contains(previewSurface.Descendants(), element => element.Name.LocalName == "ThemeShadow");
        Assert.Equal("ScreenshotPreviewButton_PointerEntered", previewButton.Attribute("PointerEntered")?.Value);
        Assert.Equal("ScreenshotPreviewButton_PointerExited", previewButton.Attribute("PointerExited")?.Value);
        Assert.Equal("Screenshot.Status.Off", screenshotStatus.Attribute("Tag")?.Value);
        Assert.Null(screenshotStatus.Attribute("Visibility"));
        Assert.Equal("0", openOverlay.Attribute("Opacity")?.Value);
        Assert.Null(openOverlay.Attribute("BorderBrush"));
        Assert.Null(openOverlay.Attribute("BorderThickness"));
        Assert.Contains("LocalTimeText.Text = _strings.Format(\"Main.Time.Local\", state.LocalTime);", source, StringComparison.Ordinal);
        Assert.Contains("UtcTimeText.Text = _strings.Format(\"Main.Time.Utc\", state.UtcTime);", source, StringComparison.Ordinal);
        Assert.Contains("new ScreenshotPreviewRequestedEventArgs(screenshotPath, capturedAt)", source, StringComparison.Ordinal);
        Assert.Contains("session?.ScreenshotCapturedAt is { } capturedAt", source, StringComparison.Ordinal);
        Assert.Contains("ScreenshotStatusText.Text = T(_screenshotsEnabled ? \"Screenshot.Status.On\" : \"Screenshot.Status.Off\");", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScreenshotStatusText.Visibility = Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("MainWindowLayoutState", source, StringComparison.Ordinal);
        Assert.Contains("RootGrid.Measure(new Size(CurrentLogicalWindowWidth, double.PositiveInfinity));", source, StringComparison.Ordinal);
        Assert.Contains("private const int LogicalWindowWidth = 576;", source, StringComparison.Ordinal);
        Assert.Contains("private const int LogicalExpandedWindowWidth = 760;", source, StringComparison.Ordinal);
        Assert.Contains("_layoutState.Surface == MainWindowSurface.Player", source, StringComparison.Ordinal);
        Assert.Contains("private const int LogicalWindowHeightPadding = 20;", source, StringComparison.Ordinal);
        Assert.Contains("WindowInteropService.ApplyPlayerWindowChrome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DwmSetWindowAttribute(", source, StringComparison.Ordinal);
        Assert.Contains("private const int DwmWindowAttributeCornerPreference = 33;", windowInterop, StringComparison.Ordinal);
        Assert.Contains("private const int DwmWindowAttributeBorderColor = 34;", windowInterop, StringComparison.Ordinal);
        Assert.Contains("private const uint DwmWindowCornerPreferenceRound = 2;", windowInterop, StringComparison.Ordinal);
        Assert.Contains("private const uint DwmColorNone = 0xFFFFFFFE;", windowInterop, StringComparison.Ordinal);
        Assert.Contains("OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)", windowInterop, StringComparison.Ordinal);
        Assert.Contains("_layoutState.ResolveLogicalHeight(availableHeight / scale, LogicalWindowHeightPadding)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowStartup_DelegatesPersistedAutoStartToTheViewModelPolicy()
    {
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var viewModelSource = File.ReadAllText(RepositoryFile("TrackMeUp.Presentation", "ViewModels.cs"));
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var startupStart = viewModelSource.IndexOf("public async Task<OperationResult<MainWindowStartupState>> InitializeAsync", StringComparison.Ordinal);
        var refreshStart = viewModelSource.IndexOf("public Task<OperationResult<DashboardState>> RefreshAsync", startupStart, StringComparison.Ordinal);

        Assert.Contains("_viewModel.InitializeAsync(options, cancellationToken)", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("if (options.StartTracking && !options.Paused)", mainSource, StringComparison.Ordinal);
        Assert.True(startupStart >= 0 && refreshStart > startupStart, "MainViewModel startup method was not found.");
        var startupSource = viewModelSource[startupStart..refreshStart];
        Assert.Contains("TrackingStartupPolicy.ShouldStart(options, effectiveSettings)", startupSource, StringComparison.Ordinal);
        Assert.Contains("_application.StartTrackingAsync", startupSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleTrackingAsync", startupSource, StringComparison.Ordinal);
        Assert.Contains("TrackingStartupPolicy.ShouldStart(options, settings.Value)", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void StartupActivationPolicy_PromotesStartupTaskToUiLaunchWithWindowsFlag()
    {
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var policyStart = appSource.IndexOf("internal static class StartupActivationPolicy", StringComparison.Ordinal);

        Assert.True(policyStart >= 0, "StartupActivationPolicy source contract was not found.");
        var policySource = appSource[policyStart..];
        Assert.Contains("activationKind == ExtendedActivationKind.StartupTask", policySource, StringComparison.Ordinal);
        Assert.Contains("options with { Mode = LaunchMode.Ui, StartWithWindows = true }", policySource, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagedManifest_DeclaresStableStartupTaskAndAppReadsRichActivation()
    {
        const string Uap5Namespace = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";
        var manifest = XDocument.Load(RepositoryFile("TrackMeUp", "Package.appxmanifest"));
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var extension = manifest
            .Descendants()
            .Single(element =>
                element.Name == XName.Get("Extension", Uap5Namespace)
                && element.Attribute("Category")?.Value == "windows.startupTask");
        var startupTask = extension.Elements().Single(element => element.Name == XName.Get("StartupTask", Uap5Namespace));

        Assert.Equal("TrackMeUp.exe", extension.Attribute("Executable")?.Value);
        Assert.Equal("Windows.FullTrustApplication", extension.Attribute("EntryPoint")?.Value);
        Assert.Equal("TrackMeUpStartup", startupTask.Attribute("TaskId")?.Value);
        Assert.Equal("false", startupTask.Attribute("Enabled")?.Value);
        Assert.Contains("StartupActivationPolicy.Apply(", appSource, StringComparison.Ordinal);
        Assert.Contains("AppInstance.GetCurrent().GetActivatedEventArgs()?.Kind", appSource, StringComparison.Ordinal);
        Assert.Contains("ExtendedActivationKind.StartupTask", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenshotStorageMigration_BlocksTrackingAndUsesANonDismissibleProgressWindow()
    {
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var dialog = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotStorageMigrationDialogWindow.xaml"));
        var dialogSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotStorageMigrationDialogWindow.xaml.cs"));
        var initializationStart = mainSource.IndexOf(
            "private async Task InitializeAsync(LaunchOptions options, CancellationToken cancellationToken)",
            StringComparison.Ordinal);
        var initializationEnd = mainSource.IndexOf(
            "private async Task<string?> ReconcileWindowsStartupAsync(",
            initializationStart,
            StringComparison.Ordinal);
        Assert.True(initializationStart >= 0 && initializationEnd > initializationStart, "MainWindow initialization source contract was not found.");
        var initialization = mainSource[initializationStart..initializationEnd];

        Assert.True(
            initialization.IndexOf("await _lifecycle.WaitUntilLoadedAsync(cancellationToken);", StringComparison.Ordinal)
            < initialization.IndexOf("EnsureScreenshotStorageMigratedAsync", StringComparison.Ordinal));
        Assert.True(
            initialization.IndexOf("EnsureScreenshotStorageMigratedAsync", StringComparison.Ordinal)
            < initialization.IndexOf("_viewModel.InitializeAsync", StringComparison.Ordinal));
        Assert.True(
            initialization.IndexOf("_viewModel.InitializeAsync", StringComparison.Ordinal)
            < initialization.IndexOf("_dashboardRefreshReady = true;", StringComparison.Ordinal));
        Assert.Contains("SetScreenshotStorageReady(false);", mainSource, StringComparison.Ordinal);
        Assert.Contains("SetScreenshotStorageReady(true);", initialization, StringComparison.Ordinal);
        Assert.Contains("if (!_screenshotStorageReady)", mainSource, StringComparison.Ordinal);
        Assert.Contains("TrackingButton.IsEnabled = isReady;", mainSource, StringComparison.Ordinal);
        Assert.Contains("TitleBarMoreButton.IsEnabled = isReady;", mainSource, StringComparison.Ordinal);
        Assert.Contains("CaptureMenu.IsEnabled = isReady;", mainSource, StringComparison.Ordinal);
        Assert.Contains("SetStartupEnabledAsync", mainSource, StringComparison.Ordinal);
        Assert.Contains("MigrateScreenshotStorageAsync", appSource, StringComparison.Ordinal);
        Assert.Contains("GetScreenshotStorageMigrationStatusAsync", appSource, StringComparison.Ordinal);
        Assert.Contains("ShowStandaloneScreenshotStorageMigrationAsync", appSource, StringComparison.Ordinal);
        Assert.Contains("StartBackgroundRuntimeAsync", appSource, StringComparison.Ordinal);
        Assert.Contains("StartReportsAsync", appSource, StringComparison.Ordinal);
        Assert.Contains(dialog.Descendants(), element => HasName(element, "MigrationProgressRing") && element.Name.LocalName == "ProgressRing");
        Assert.DoesNotContain(dialog.Descendants(), element => element.Name.LocalName is "Button" or "HyperlinkButton");
        Assert.Contains("args.Cancel = !_allowClose;", dialogSource, StringComparison.Ordinal);
        Assert.Contains("_application.MigrateScreenshotStorageAsync", dialogSource, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeWindows_ExposeTitleBarsAndAcrylicWithoutOpaquePanelCards()
    {
        var app = XDocument.Load(RepositoryFile("TrackMeUp", "App.xaml"));
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var reports = XDocument.Load(RepositoryFile("TrackMeUp", "ReportsWindow.xaml"));
        var about = XDocument.Load(RepositoryFile("TrackMeUp", "AboutWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var reportsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ReportsWindow.xaml.cs"));
        var titleBarSource = File.ReadAllText(RepositoryFile("TrackMeUp", "CustomTitleBarController.cs"));
        var highContrastResources = player
            .Descendants()
            .Single(element => element.Name.LocalName == "ResourceDictionary" && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "HighContrast"));
        var applicationHighContrastResources = app
            .Descendants()
            .Single(element => element.Name.LocalName == "ResourceDictionary" && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "HighContrast"));

        Assert.Contains("SystemBackdrop = new DesktopAcrylicBackdrop", mainSource, StringComparison.Ordinal);
        Assert.Contains(reports.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(about.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains("_titleBar = new CustomTitleBarController(", mainSource, StringComparison.Ordinal);
        Assert.Contains("_window.SetTitleBar(_dragRegion);", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("_titleBar = new CustomTitleBarController(", reportsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SetTitleBar(TitleBarDragRegion)", reportsSource, StringComparison.Ordinal);
        Assert.DoesNotContain(player.Descendants(), element => element.Attribute("Background")?.Value.Contains("FlyoutSurfaceBrush", StringComparison.Ordinal) == true);
        Assert.Contains(highContrastResources.Descendants(), element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "PlayerAccentBrush"));
        Assert.Contains(applicationHighContrastResources.Descendants(), element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "PlayerAccentTextBrush"));
        Assert.Contains(highContrastResources.Descendants(), element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "PlayerAccentGlyphBrush"));
    }

    [Fact]
    public void Player_KeepsOnlyNativeCloseAndConfirmsTrackingSuspension()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var systemButtonGap = player.Descendants().Single(element => HasName(element, "TitleBarSystemButtonGapColumn"));
        var dragRegion = player.Descendants().Single(element => HasName(element, "DragRegion"));
        var reportButton = player.Descendants().Single(element => HasName(element, "TitleBarReportButton"));
        var minimizeToTrayButton = player.Descendants().Single(element => HasName(element, "TitleBarMinimizeToTrayButton"));
        var closeStart = mainSource.IndexOf("private async void AppWindow_Closing", StringComparison.Ordinal);
        var closeEnd = mainSource.IndexOf("private static void FadeIn", closeStart, StringComparison.Ordinal);
        Assert.True(closeStart >= 0 && closeEnd > closeStart, "Main-window close lifecycle source contract was not found.");
        var closeSource = mainSource[closeStart..closeEnd];

        Assert.Equal("12", systemButtonGap.Attribute("Width")?.Value);
        Assert.Null(dragRegion.Attribute("Grid.ColumnSpan"));
        Assert.Null(dragRegion.Attribute("Grid.Column"));
        Assert.Equal("Collapsed", reportButton.Attribute("Visibility")?.Value);
        Assert.Equal("Collapsed", minimizeToTrayButton.Attribute("Visibility")?.Value);
        Assert.Contains("presenter.IsMaximizable = false;", mainSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMinimizable = false;", mainSource, StringComparison.Ordinal);
        Assert.Contains("_appWindow.Closing += AppWindow_Closing;", mainSource, StringComparison.Ordinal);
        Assert.Contains("args.Cancel = true;", closeSource, StringComparison.Ordinal);
        Assert.Contains("_closeConfirmationInProgress", closeSource, StringComparison.Ordinal);
        Assert.Contains("SystemMessageBoxRequest.Confirmation(", closeSource, StringComparison.Ordinal);
        Assert.Contains("T(\"Dialog.CloseTracking.Message\")", closeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Dialog.CloseTracking.Confirm", closeSource, StringComparison.Ordinal);
        Assert.True(
            closeSource.IndexOf("await _placement.TrySaveForCloseAsync", StringComparison.Ordinal)
            < closeSource.IndexOf("_allowClose = true;", StringComparison.Ordinal));
        Assert.True(
            closeSource.IndexOf("_allowClose = true;", StringComparison.Ordinal)
            < closeSource.IndexOf("Close();", StringComparison.Ordinal));
        Assert.True(
            closeSource.IndexOf("Close();", StringComparison.Ordinal)
            < closeSource.LastIndexOf("_closeConfirmationInProgress = false;", StringComparison.Ordinal),
            "The reentrancy guard must remain set through placement persistence and the final close request.");
        Assert.Contains("_appWindow.Closing -= AppWindow_Closing;", mainSource, StringComparison.Ordinal);
        Assert.Contains("_window?.CloseForShutdown();", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrollBars_UseGlobalPointerScopedFadeBehavior()
    {
        var app = XDocument.Load(RepositoryFile("TrackMeUp", "App.xaml"));
        var behavior = File.ReadAllText(RepositoryFile("TrackMeUp", "ScrollBarRevealBehavior.cs"));
        var scrollViewerStyle = app.Descendants().Single(element =>
            element.Name.LocalName == "Style" && element.Attribute("TargetType")?.Value == "ScrollViewer");
        var scrollBarStyle = app.Descendants().Single(element =>
            element.Name.LocalName == "Style" && element.Attribute("TargetType")?.Value == "ScrollBar");

        Assert.Contains(scrollViewerStyle.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "local:ScrollBarRevealBehavior.IsEnabled"
            && element.Attribute("Value")?.Value == "True");
        Assert.Contains(scrollBarStyle.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Opacity"
            && element.Attribute("Value")?.Value == "0");
        Assert.Contains("scrollViewer.PointerEntered += ScrollViewer_PointerEntered", behavior, StringComparison.Ordinal);
        Assert.Contains("scrollViewer.PointerExited += ScrollViewer_PointerExited", behavior, StringComparison.Ordinal);
        Assert.Contains("new DoubleAnimation", behavior, StringComparison.Ordinal);
        Assert.Contains("DescendantScrollBars(scrollViewer)", behavior, StringComparison.Ordinal);
    }

    [Fact]
    public void InfoBars_UseGlobalNeutralGlassAndSoftElevation()
    {
        var app = XDocument.Load(RepositoryFile("TrackMeUp", "App.xaml"));
        var infoBarStyle = app.Descendants().Single(element =>
            element.Name.LocalName == "Style" && element.Attribute("TargetType")?.Value == "InfoBar");
        var severityResources = app.Descendants()
            .Where(element => element.Name.LocalName == "StaticResource")
            .Where(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value.StartsWith("InfoBar", StringComparison.Ordinal)))
            .ToArray();
        var severityBackgrounds = severityResources
            .Where(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value.EndsWith("SeverityBackgroundBrush", StringComparison.Ordinal)))
            .ToArray();
        var severityIconBackgrounds = severityResources
            .Where(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value.EndsWith("SeverityIconBackground", StringComparison.Ordinal)))
            .ToArray();
        var severityIconForegrounds = severityResources
            .Where(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value.EndsWith("SeverityIconForeground", StringComparison.Ordinal)))
            .ToArray();

        Assert.Equal(12, severityBackgrounds.Length);
        Assert.Equal(8, severityBackgrounds.Count(resource => resource.Attribute("ResourceKey")?.Value == "AcrylicInAppFillColorDefaultBrush"));
        Assert.Equal(4, severityBackgrounds.Count(resource => resource.Attribute("ResourceKey")?.Value == "SystemColorWindowColorBrush"));
        Assert.Equal(12, severityIconBackgrounds.Length);
        Assert.Equal(8, severityIconBackgrounds.Count(resource => resource.Attribute("ResourceKey")?.Value == "ControlFillColorTransparentBrush"));
        Assert.Equal(4, severityIconBackgrounds.Count(resource => resource.Attribute("ResourceKey")?.Value == "SystemColorWindowColorBrush"));
        Assert.Equal(12, severityIconForegrounds.Length);
        Assert.Equal(8, severityIconForegrounds.Count(resource => resource.Attribute("ResourceKey")?.Value == "TextFillColorPrimaryBrush"));
        Assert.Equal(4, severityIconForegrounds.Count(resource => resource.Attribute("ResourceKey")?.Value == "SystemColorWindowTextColorBrush"));
        Assert.DoesNotContain(app.Descendants(), element =>
            (element.Name.LocalName is "AcrylicBrush" or "SolidColorBrush" or "LinearGradientBrush") &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Key" && attribute.Value.StartsWith("InfoBar", StringComparison.Ordinal)));
        Assert.Contains(infoBarStyle.Descendants(), element => element.Name.LocalName == "ThemeShadow");
        Assert.Contains(infoBarStyle.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "BorderBrush" && element.Attribute("Value")?.Value == "{ThemeResource SurfaceStrokeColorDefaultBrush}");
        Assert.Contains(infoBarStyle.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "BorderThickness" && element.Attribute("Value")?.Value == "1");
        Assert.Contains(infoBarStyle.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "CornerRadius" && element.Attribute("Value")?.Value == "12");
        Assert.Contains(infoBarStyle.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "Foreground" && element.Attribute("Value")?.Value == "{ThemeResource TextFillColorPrimaryBrush}");
        Assert.Contains(infoBarStyle.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "local:InfoBarElevationBehavior.IsEnabled" && element.Attribute("Value")?.Value == "True");
    }

    [Fact]
    public void DashboardMetricLabels_UseOneApplicationLevelStyle()
    {
        var app = XDocument.Load(RepositoryFile("TrackMeUp", "App.xaml"));
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var style = app.Descendants().Single(element =>
            element.Name.LocalName == "Style" && HasKey(element, "DashboardMetricLabelTextBlockStyle"));
        var trackingLabel = player.Descendants().Single(element => HasName(element, "TrackingStateText"));
        var monthlySpendLabel = player.Descendants().Single(element => element.Attribute("Tag")?.Value == "Main.AiMonthlySpend");
        var accentBrushes = app.Descendants()
            .Where(element => HasKey(element, "PlayerAccentTextBrush"))
            .ToArray();

        Assert.Equal("TextBlock", style.Attribute("TargetType")?.Value);
        Assert.Equal(3, accentBrushes.Length);
        Assert.DoesNotContain(player.Descendants(), element => HasKey(element, "PlayerAccentTextBrush"));
        Assert.Contains(style.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "CharacterSpacing" && element.Attribute("Value")?.Value == "100");
        Assert.Contains(style.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "FontSize" && element.Attribute("Value")?.Value == "12");
        Assert.Contains(style.Descendants(), element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "FontWeight" && element.Attribute("Value")?.Value == "SemiBold");
        Assert.Equal("{StaticResource DashboardMetricLabelTextBlockStyle}", trackingLabel.Attribute("Style")?.Value);
        Assert.Equal("{StaticResource DashboardMetricLabelTextBlockStyle}", monthlySpendLabel.Attribute("Style")?.Value);
    }

    [Fact]
    public void AiMonthlySpend_IsAnOptInPlayerPreference()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var playerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var panel = player.Descendants().Single(element => HasName(element, "AiMonthlySpendPanel"));
        var visibilitySwitch = options.Descendants().Single(element => HasName(element, "ShowAiMonthlySpendSwitch"));

        Assert.Equal("Collapsed", panel.Attribute("Visibility")?.Value);
        Assert.Equal("Options.AiMonthlySpendVisibility", visibilitySwitch.Attribute("Tag")?.Value);
        Assert.Contains("QueueAutoSave(", optionsSource, StringComparison.Ordinal);
        Assert.Contains("\"ai.show_monthly_spend\"", optionsSource, StringComparison.Ordinal);
        Assert.Contains("ShowAiMonthlySpendSwitch.IsOn = settings.ShowAiMonthlySpend;", optionsSource, StringComparison.Ordinal);
        Assert.Equal(
            2,
            playerSource.Split("if (!_showAiMonthlySpend || !AiState.Enabled)", StringSplitOptions.None).Length - 1);
        Assert.Contains("_showAiMonthlySpend = settings.ShowAiMonthlySpend;", playerSource, StringComparison.Ordinal);
        Assert.Contains("AiSpendFailureRetryInterval", playerSource, StringComparison.Ordinal);
        Assert.Contains("GetAiPricingOverviewAsync(_lifecycle.Token)", playerSource, StringComparison.Ordinal);
        Assert.Contains(
            "_nextAiSpendRefreshAt = DateTimeOffset.UtcNow.Add(AiSpendFailureRetryInterval);",
            playerSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LanguagePicker_OffersSystemModeAndExplicitOverrides()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var languagePicker = options.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "LanguageBox"));
        var searchLanguagePicker = options.Descendants().Single(element => HasName(element, "SearchLanguageBox"));
        var ocrLanguagePicker = options.Descendants().Single(element => HasName(element, "OcrLanguageBox"));
        string?[] uiAndSearchChoices = ["system", "en-US", "it-IT", "fr-FR", "de-DE", "es-ES", "zh-Hans", "vi-VN", "ko-KR", "pt-PT", "pt-BR"];
        string?[] ocrChoices = ["system", "en-US", "it-IT", "fr-FR", "de-DE", "es-ES", "zh-CN", "ko-KR", "pt-PT", "pt-BR"];

        Assert.Equal("Options.Language", languagePicker.Attribute("Tag")?.Value);
        Assert.Equal(
            uiAndSearchChoices,
            languagePicker.Descendants()
                .Where(element => element.Name.LocalName == "ComboBoxItem")
                .Select(element => element.Attribute("Tag")?.Value));
        Assert.Equal(
            uiAndSearchChoices,
            searchLanguagePicker.Descendants()
                .Where(element => element.Name.LocalName == "ComboBoxItem")
                .Select(element => element.Attribute("Tag")?.Value));
        Assert.Equal(
            ocrChoices,
            ocrLanguagePicker.Descendants()
                .Where(element => element.Name.LocalName == "ComboBoxItem")
                .Select(element => element.Attribute("Tag")?.Value));
    }

    [Fact]
    public void TaskbarWidgetOptions_ExposeOptInVisibilityAndSupportedAnchors()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var visibilitySwitch = options.Descendants().Single(element => HasName(element, "TaskbarWidgetVisibleSwitch"));
        var positionPicker = options.Descendants().Single(element => HasName(element, "TaskbarWidgetPositionBox"));
        var positions = positionPicker
            .Descendants()
            .Where(element => element.Name.LocalName == "ComboBoxItem")
            .Select(element => element.Attribute("Tag")?.Value)
            .OfType<string>()
            .ToArray();

        Assert.Contains(options.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Section.TaskbarWidget");
        Assert.Contains(options.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Section.TaskbarWidget.Description");
        Assert.Equal("TaskbarWidgetVisibleSwitch_Toggled", visibilitySwitch.Attribute("Toggled")?.Value);
        Assert.Null(visibilitySwitch.Attribute("IsOn"));
        Assert.Equal("False", positionPicker.Attribute("IsEnabled")?.Value);
        Assert.Equal(["left", "right"], positions);
        Assert.Contains("QueueAutoSave(\"taskbar.widget.visible\"", source, StringComparison.Ordinal);
        Assert.Contains("TaskbarWidgetPositionBox.IsEnabled = TaskbarWidgetVisibleSwitch.IsOn;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskbarSurface_UsesAnAlphaCapableWpfHost()
    {
        var widget = XDocument.Load(RepositoryFile("TrackMeUp.Taskbar", "TaskbarWidgetWindow.xaml"));
        Assert.Equal("True", widget.Root?.Attribute("AllowsTransparency")?.Value);
        Assert.Equal("Transparent", widget.Root?.Attribute("Background")?.Value);
        Assert.Equal("None", widget.Root?.Attribute("WindowStyle")?.Value);
        Assert.Equal("False", widget.Root?.Attribute("ShowInTaskbar")?.Value);
    }

    [Fact]
    public void TaskbarSurface_ParentsHiddenHwndBeforeFirstVisibleFrame()
    {
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp.Taskbar", "TaskbarWidgetWindow.xaml.cs"));
        var surfaceSource = File.ReadAllText(RepositoryFile("TrackMeUp.Taskbar", "TaskbarWidgetSurface.cs"));
        var hostSource = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Infrastructure", "Services", "TaskbarWidgetHost.cs"));
        var prepareStart = windowSource.IndexOf("internal void PrepareForTaskbar", StringComparison.Ordinal);
        var prepareEnd = windowSource.IndexOf("internal void ApplySettings", StringComparison.Ordinal);
        var attachStart = surfaceSource.IndexOf("private bool TryAttachPreparedWindow", StringComparison.Ordinal);
        var attachEnd = surfaceSource.IndexOf("private void RecoverFromExplorerChanges", StringComparison.Ordinal);

        Assert.True(prepareStart >= 0 && prepareEnd > prepareStart, "PrepareForTaskbar source contract was not found.");
        Assert.True(attachStart >= 0 && attachEnd > attachStart, "TaskbarWidgetSurface attach lifecycle contract was not found.");
        var prepareSource = windowSource[prepareStart..prepareEnd];
        var attachSource = surfaceSource[attachStart..attachEnd];
        Assert.Contains("Opacity = 1;", prepareSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Opacity = 0;", prepareSource, StringComparison.Ordinal);
        Assert.Contains("_ = Handle;", prepareSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Show();", prepareSource, StringComparison.Ordinal);
        Assert.Contains("_host.Attach", attachSource, StringComparison.Ordinal);
        Assert.Contains("window.Show();", attachSource, StringComparison.Ordinal);
        Assert.True(
            attachSource.IndexOf("_host.Attach", StringComparison.Ordinal) < attachSource.IndexOf("window.Show();", StringComparison.Ordinal),
            "The HWND must be parented before WPF shows its first visible frame.");
        Assert.DoesNotContain("ContentRendered", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("DispatcherTimer", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("_host.Recover()", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("_host.HasValidWidgetHandle", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("PrepareReplacementWindow();", surfaceSource, StringComparison.Ordinal);
        Assert.Contains("showWindow: false", hostSource, StringComparison.Ordinal);
        Assert.Contains("SetWindowRgn", hostSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskbarSurface_StartsAndStopsWithoutPreLoopDispatcherCallsOrLeakedReplacementWindows()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp.Taskbar", "TaskbarWidgetSurface.cs"));
        var disposeStart = source.IndexOf("public void Dispose()", StringComparison.Ordinal);
        var dispatcherStart = source.IndexOf("private void DispatcherThreadMain()", StringComparison.Ordinal);
        var prepareStart = source.IndexOf("private void PrepareReplacementWindow()", StringComparison.Ordinal);

        Assert.True(disposeStart >= 0 && dispatcherStart > disposeStart, "Taskbar disposal lifecycle source contract was not found.");
        Assert.True(prepareStart > dispatcherStart, "Taskbar replacement lifecycle source contract was not found.");
        var disposeSource = source[disposeStart..dispatcherStart];
        var prepareSource = source[prepareStart..];
        Assert.DoesNotContain("ManualResetEventSlim", source, StringComparison.Ordinal);
        Assert.Contains("TaskCompletionSource", source, StringComparison.Ordinal);
        Assert.Contains("_lifetimeCancellation.Cancel();", disposeSource, StringComparison.Ordinal);
        Assert.Contains("BeginInvoke(DispatcherPriority.Send", disposeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("dispatcher.Invoke(", disposeSource, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("BeginInvoke(DispatcherPriority.Send, new Action(SignalDispatcherStarted))", StringComparison.Ordinal)
            < source.IndexOf("Dispatcher.Run();", StringComparison.Ordinal),
            "Startup completion must be queued before the dispatcher loop begins.");
        Assert.Contains("CloseCurrentWindow();", prepareSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_RemainsVisibleWhenTaskbarWidgetAttaches()
    {
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var startUiStart = appSource.IndexOf("private void StartUi", StringComparison.Ordinal);
        var startUiEnd = appSource.IndexOf("private void StartReports", StringComparison.Ordinal);
        var applyWidgetStart = appSource.IndexOf("private void ApplyTaskbarWidgetSettings", StringComparison.Ordinal);
        var applyWidgetEnd = appSource.IndexOf("private void DisposeTaskbarWidget", StringComparison.Ordinal);

        Assert.True(startUiStart >= 0 && startUiEnd > startUiStart, "App.StartUi source contract was not found.");
        Assert.True(applyWidgetStart >= 0 && applyWidgetEnd > applyWidgetStart, "Taskbar widget settings lifecycle contract was not found.");
        var startUiSource = appSource[startUiStart..startUiEnd];
        var applyWidgetSource = appSource[applyWidgetStart..applyWidgetEnd];
        Assert.Contains("_window.Activate();", startUiSource, StringComparison.Ordinal);
        Assert.Contains("_ = CompleteUiStartupAsync(application, options);", startUiSource, StringComparison.Ordinal);
        Assert.Contains("private async Task CompleteUiStartupAsync", startUiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetAwaiter().GetResult()", startUiSource, StringComparison.Ordinal);
        Assert.True(
            startUiSource.IndexOf("_window.Activate();", StringComparison.Ordinal) < startUiSource.IndexOf("ApplyTaskbarWidgetSettings(settings);", StringComparison.Ordinal),
            "MainWindow must activate before optional taskbar-widget initialization.");
        Assert.Contains("if (!settings.TaskbarWidgetVisible)", applyWidgetSource, StringComparison.Ordinal);
        Assert.Contains("new TaskbarWidgetSurface", applyWidgetSource, StringComparison.Ordinal);
        Assert.Contains("taskbarWidgetSurface.Attach(settings.TaskbarWidgetPosition)", applyWidgetSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HideTopLevelWindow", startUiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstRun", startUiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AlwaysShowInTaskbar", startUiSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenshotWindow_SavesPlacementBeforeItsNativeHandleIsDestroyed()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));
        var closingStart = source.IndexOf("private void ScreenshotWindow_Closing", StringComparison.Ordinal);
        var closedStart = source.IndexOf("private void ScreenshotWindow_Closed", StringComparison.Ordinal);

        Assert.True(closingStart >= 0 && closedStart > closingStart, "Screenshot close lifecycle source contract was not found.");
        var closingSource = source[closingStart..closedStart];
        var closedSource = source[closedStart..];
        Assert.Contains("_appWindow.Closing += ScreenshotWindow_Closing;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("args.Cancel = true;", closingSource, StringComparison.Ordinal);
        Assert.Contains("_ = _placement.TrySaveForCloseAsync(CancellationToken.None);", closingSource, StringComparison.Ordinal);
        Assert.True(
            closingSource.IndexOf("TrySaveForCloseAsync", StringComparison.Ordinal) >= 0,
            "Placement persistence must start while the screenshot window handle is still valid.");
        Assert.DoesNotContain("_placement.SaveAsync", closedSource, StringComparison.Ordinal);
        Assert.Contains("_placement.Dispose();", closedSource, StringComparison.Ordinal);
        Assert.Contains("_screenshotsWindow.CloseForShutdown();", File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenshotIconOnlyControls_UseLocalizedTooltipsAndAutomationNames()
    {
        var viewer = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml"));
        var header = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotHeaderControl.xaml"));
        var documents = new[]
        {
            viewer,
            XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ScreenshotTimelineControl.xaml")),
            header
        };
        var screenshotWindow = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml"));
        var localizationCatalogs = CanonicalUiLocales.Select(LoadLocalizationKeys).ToArray();
        var uiLocalization = File.ReadAllText(RepositoryFile("TrackMeUp", "UiLocalization.cs"));
        var headerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotHeaderControl.xaml.cs"));

        Assert.All(
            localizationCatalogs.Skip(1),
            catalog => Assert.Equal(
                localizationCatalogs[0].OrderBy(static key => key, StringComparer.Ordinal),
                catalog.OrderBy(static key => key, StringComparer.Ordinal)));

        var iconOnlyButtons = documents
            .SelectMany(document => document.Descendants())
            .Where(element => element.Name.LocalName is "Button" or "ToggleButton" or "AppBarButton" or "AppBarToggleButton")
            .Where(element => element.Attribute("Content") is null)
            .Where(element => element.Descendants().Any(child => child.Name.LocalName == "FontIcon"))
            .ToArray();

        Assert.NotEmpty(iconOnlyButtons);
        Assert.All(iconOnlyButtons, button => AssertLocalizedScreenshotCommand(button, localizationCatalogs));
        Assert.DoesNotContain(viewer.Descendants(), element =>
            element.Name.LocalName is "Button" or "ToggleButton" or "AppBarButton" or "AppBarToggleButton");
        Assert.All(
            header.Descendants().Where(element => element.Name.LocalName == "FontIcon"),
            icon => Assert.Equal("Raw", icon.Attribute("AutomationProperties.AccessibilityView")?.Value));
        Assert.Contains("SetCommandLabel(ZoomOutButton, \"Screenshots.Toolbar.ZoomOut\");", headerSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(MetadataDateItem", headerSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(MetadataTimeItem", headerSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(MetadataApplicationItem", headerSource, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(PrivacyStatusBadge, statusText);", headerSource, StringComparison.Ordinal);

        var resizeGrip = screenshotWindow.Descendants().Single(element => HasName(element, "DetailsResizeGrip"));
        AssertLocalizedScreenshotCommand(resizeGrip, localizationCatalogs);
        Assert.Contains("ToolTipService.SetToolTip", uiLocalization, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName", uiLocalization, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopResidualAccessibilityAndBuiltInMetadata_AreLocalizedAtRuntime()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var operationsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml.cs"));
        var pluginsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "PluginOperationsControl.xaml.cs"));
        var privacy = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "PrivacyOperationsControl.xaml"));
        var privacySource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "PrivacyOperationsControl.xaml.cs"));
        var retentionSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "RetentionOperationsControl.xaml.cs"));
        var viewerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml.cs"));

        Assert.Equal("Options.Theme.system", options.Descendants().Single(element => HasName(element, "ThemeSystemButton")).Attribute("Tag")?.Value);
        Assert.Equal("Options.Theme.light", options.Descendants().Single(element => HasName(element, "ThemeLightButton")).Attribute("Tag")?.Value);
        Assert.Equal("Options.Theme.dark", options.Descendants().Single(element => HasName(element, "ThemeDarkButton")).Attribute("Tag")?.Value);
        Assert.Equal("Options.AiConnection.Test", options.Descendants().Single(element => HasName(element, "TestConnectionButton")).Attribute("Tag")?.Value);
        Assert.Contains("AutomationProperties.SetName(KeepScreenshotsSwitch, T(\"Options.KeepSnapshots.Header\"));", optionsSource, StringComparison.Ordinal);
        Assert.Contains("BuiltInModelKeys", optionsSource, StringComparison.Ordinal);
        Assert.Contains("Options.Model.Description.{model.Key}", optionsSource, StringComparison.Ordinal);
        Assert.Contains("Explicitly loaded external model catalogs own their descriptive metadata.", optionsSource, StringComparison.Ordinal);

        Assert.Contains("AutomationProperties.SetName(OperationProgress", operationsSource, StringComparison.Ordinal);
        Assert.Contains("BuiltInPluginIds", pluginsSource, StringComparison.Ordinal);
        Assert.Contains("Operations.Plugin.{plugin.Id}", pluginsSource, StringComparison.Ordinal);
        Assert.Contains("External plugin metadata is supplied by the plugin", pluginsSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(PluginsList", pluginsSource, StringComparison.Ordinal);
        Assert.Contains(privacy.Descendants(), element => element.Attribute("Text")?.Value == "{Binding TypeLabel}");
        Assert.Contains("Operations.PrivacyType.{rule.Type}", privacySource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(PrivacyRulesList", privacySource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(RetentionPathsList", retentionSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(this, _strings.Translate(\"Screenshots.Caption\"));", viewerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LowStorageNotifications_KeepLocalizedDetailStructuredUntilRendering()
    {
        var contracts = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "Contracts.cs"));
        var application = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "TrackMeUpApplication.cs"));
        var main = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));

        Assert.Contains("record LocalizedNotificationDetail", contracts, StringComparison.Ordinal);
        Assert.Contains("\"Notification.ScreenshotStorageLow.Detail\"", application, StringComparison.Ordinal);
        Assert.DoesNotContain("Available free space:", application, StringComparison.Ordinal);
        Assert.Contains("notification.LocalizedDetail is { } localizedDetail", main, StringComparison.Ordinal);
        Assert.Contains("localizedDetail.MessageKey", main, StringComparison.Ordinal);
        Assert.Contains("localizedDetail.Arguments.Select", main, StringComparison.Ordinal);
    }

    [Fact]
    public void HtmlReport_UsesTheSelectedUiLanguageCatalogAndCulture()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Infrastructure", "Services", "HtmlReportService.cs"));

        Assert.Contains("new LocalizationService(_store.LoadSettings().UiLanguage)", source, StringComparison.Ordinal);
        Assert.Contains("var culture = strings.Culture;", source, StringComparison.Ordinal);
        Assert.Contains("Html(strings.Language)", source, StringComparison.Ordinal);
        Assert.Contains("\"HtmlReport.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("lang=\\\"it\\\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tempo attivo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tempo inattivo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Utilizzo AI", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Nessuna richiesta AI", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CultureInfo.CurrentCulture", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopLocalizationCatalogs_HaveExactLocaleKeyAndFormatParity()
    {
        var localizationDirectory = Path.GetDirectoryName(
            RepositoryFile("TrackMeUp.Core", "Localization", "en-US.json"))!;
        var actualLocales = Directory.GetFiles(localizationDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(static locale => locale, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(CanonicalUiLocales.OrderBy(static locale => locale, StringComparer.Ordinal), actualLocales);

        var catalogs = CanonicalUiLocales.ToDictionary(
            static locale => locale,
            LoadLocalizationCatalog,
            StringComparer.Ordinal);
        var english = catalogs["en-US"];
        foreach (var (locale, catalog) in catalogs)
        {
            Assert.Equal(
                english.Keys.OrderBy(static key => key, StringComparer.Ordinal),
                catalog.Keys.OrderBy(static key => key, StringComparer.Ordinal));
            foreach (var (key, value) in catalog)
            {
                _ = CompositeFormat.Parse(value);
                Assert.Equal(FormatItems(english[key]), FormatItems(value));
            }

            if (!locale.Equals("en-US", StringComparison.Ordinal))
            {
                var unchangedEntries = english.Count(entry =>
                    string.Equals(entry.Value, catalog[entry.Key], StringComparison.Ordinal));
                Assert.True(
                    unchangedEntries <= 150,
                    $"Localization catalog '{locale}' still contains {unchangedEntries} unchanged English entries.");
            }
        }

        var portugueseDialectDifferences = catalogs["pt-PT"].Count(entry =>
            !string.Equals(entry.Value, catalogs["pt-BR"][entry.Key], StringComparison.Ordinal));
        Assert.True(
            portugueseDialectDifferences >= 200,
            $"Portuguese product catalogs differ in only {portugueseDialectDifferences} entries.");
        Assert.Equal("Capturas de ecrã automáticas", catalogs["pt-PT"]["MenuToggleScreenshot"]);
        Assert.Equal("Capturas de tela automáticas", catalogs["pt-BR"]["MenuToggleScreenshot"]);
    }

    [Fact]
    public void DesktopDynamicCopy_UsesResolvedLocalizationCultureAndFormats()
    {
        var reports = File.ReadAllText(RepositoryFile("TrackMeUp", "ReportsWindow.xaml.cs"));
        var screenshots = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));
        var main = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var pricing = File.ReadAllText(RepositoryFile("TrackMeUp", "AiPricingDialogWindow.xaml.cs"));
        var options = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var indexing = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchIndexingWindow.xaml.cs"));
        var imageViewer = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotImageViewerControl.xaml.cs"));
        var timeline = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "ScreenshotTimelineControl.xaml.cs"));

        Assert.Contains("_strings.Format(\"Reports.Error.ReportUnavailable\"", reports, StringComparison.Ordinal);
        Assert.Contains("_strings.Format(\"Screenshots.Count.Many\"", screenshots, StringComparison.Ordinal);
        Assert.Contains("_strings.Format(\"Main.Time.Local\"", main, StringComparison.Ordinal);
        Assert.Contains("var culture = _strings.Culture;", pricing, StringComparison.Ordinal);
        Assert.Contains("_strings.Format(\"Options.AiQuota.Usage\"", options, StringComparison.Ordinal);
        Assert.Contains("_strings.Format(\"SearchIndex.Completed.Description\"", indexing, StringComparison.Ordinal);
        Assert.Contains("_strings.Format(\"Screenshots.Image.Accessible\"", imageViewer, StringComparison.Ordinal);
        Assert.Contains("_strings.Format(\"Screenshots.Timeline.ItemAccessible\"", timeline, StringComparison.Ordinal);
        Assert.DoesNotContain("CultureInfo.CurrentCulture", reports + screenshots + main + pricing + options + indexing + imageViewer + timeline, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickSetup_UsesFourAccessibleProfileControlsOnVisibleAcrylic()
    {
        var quickSetup = XDocument.Load(RepositoryFile("TrackMeUp", "QuickSetupWindow.xaml"));
        var quickSetupMarkup = File.ReadAllText(RepositoryFile("TrackMeUp", "QuickSetupWindow.xaml"));
        var quickSetupSource = File.ReadAllText(RepositoryFile("TrackMeUp", "QuickSetupWindow.xaml.cs"));
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var profiles = quickSetup
            .Descendants()
            .Where(element => element.Name.LocalName == "ToggleButton" && element.Attribute("Tag") is not null)
            .ToArray();

        Assert.Contains(quickSetup.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Equal("Transparent", quickSetup.Descendants().Single(element => HasName(element, "RootGrid")).Attribute("Background")?.Value);
        Assert.Equal("Stretch", quickSetup.Descendants().Single(element => element.Name.LocalName == "ScrollViewer").Attribute("HorizontalContentAlignment")?.Value);
        Assert.Equal("Hidden", quickSetup.Descendants().Single(element => element.Name.LocalName == "ScrollViewer").Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal(4, quickSetup.Descendants().Count(element => element.Name.LocalName == "Viewbox"));
        Assert.Contains(quickSetup.Descendants(), element =>
            HasName(element, "TitleBarBrandMark")
            && element.Name.LocalName == "Image"
            && element.Attribute("Style")?.Value == "{StaticResource TrackMeUpTitleBarLogoStyle}");
        Assert.Equal(
            [QuickSetupProfileIds.Complete, QuickSetupProfileIds.Assisted, QuickSetupProfileIds.LocalRecord, QuickSetupProfileIds.EssentialOffline],
            profiles.Select(profile => profile.Attribute("Tag")!.Value).ToArray());
        Assert.All(profiles, profile => Assert.False(string.IsNullOrWhiteSpace(profile.Attributes().Single(attribute => attribute.Name.LocalName == "Name").Value)));
        Assert.Contains(quickSetup.Descendants(), element => HasName(element, "StartWithWindowsCheckBox"));
        Assert.Contains(quickSetup.Descendants(), element => HasName(element, "ApplyInfoBar"));
        Assert.DoesNotContain(quickSetup.Descendants(), element => element.Name.LocalName is "LinearGradientBrush" or "RadialGradientBrush");
        Assert.DoesNotContain("SystemControlHighlightAccentBrush", quickSetupMarkup, StringComparison.Ordinal);
        Assert.Contains("ApplyQuickSetupProfileAsync", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("LogicalWindowWidth = 860", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("LogicalWindowHeight = 650", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("ApplyLanguage();", quickSetupSource, StringComparison.Ordinal);
        Assert.Equal(
            1,
            quickSetupSource.Split("_placement.ApplyDefaultBounds(RootGrid);", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false)", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(CompleteProfileButton", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(AssistedProfileButton", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(LocalRecordProfileButton", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(EssentialOfflineProfileButton", quickSetupSource, StringComparison.Ordinal);
        Assert.Contains("!settings.QuickSetupCompleted", appSource, StringComparison.Ordinal);
        Assert.Contains(player.Descendants(), element => HasName(element, "QuickSetupMenuItem") && element.Attribute("Tag")?.Value == "QuickSetup.MenuTitle");
    }

    [Theory]
    [InlineData("TrackMeUp/MainWindow.xaml")]
    [InlineData("TrackMeUp/Controls/OptionsControl.xaml")]
    [InlineData("TrackMeUp.Taskbar/TaskbarWidgetWindow.xaml")]
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

    private static bool HasName(XElement element, string name) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);

    private static XElement[] MenuGlyph(XDocument document, string itemName) =>
        document.Descendants()
            .Single(element => HasName(element, itemName))
            .Descendants()
            .Where(element => element.Name.LocalName == "FontIcon")
            .ToArray();

    private static bool HasKey(XElement element, string key) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == key);

    private static void AssertLocalizedScreenshotCommand(
        XElement element,
        IReadOnlyList<HashSet<string>> localizationCatalogs)
    {
        var tag = element.Attribute("Tag")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(tag));
        Assert.StartsWith("Screenshots.", tag, StringComparison.Ordinal);
        Assert.Contains(element.Attributes(), attribute => attribute.Name.LocalName == "AutomationProperties.Name" && !string.IsNullOrWhiteSpace(attribute.Value));
        Assert.Contains(element.Attributes(), attribute => attribute.Name.LocalName == "ToolTipService.ToolTip" && !string.IsNullOrWhiteSpace(attribute.Value));
        Assert.All(localizationCatalogs, catalog => Assert.Contains(tag!, catalog));
    }

    private static HashSet<string> LoadLocalizationKeys(string locale)
        => LoadLocalizationCatalog(locale).Keys.ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> LoadLocalizationCatalog(string locale)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Localization", $"{locale}.json")));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert.Equal(JsonValueKind.String, property.Value.ValueKind);
            var value = property.Value.GetString();
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.True(entries.TryAdd(property.Name, value!), $"Duplicate localization key '{property.Name}' in {locale}.");
        }

        return entries;
    }

    private static string[] FormatItems(string value) =>
        Regex.Matches(value, @"\{[0-9]+(?:,-?[0-9]+)?(?::[^{}]+)?\}")
            .Select(static match => match.Value)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
}
