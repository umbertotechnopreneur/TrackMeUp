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
    public void CompactSurfaces_ScrollWhereNeededAndKeepAboutFixed()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var about = XDocument.Load(RepositoryFile("TrackMeUp", "AboutWindow.xaml"));
        var aboutSource = File.ReadAllText(RepositoryFile("TrackMeUp", "AboutWindow.xaml.cs"));

        Assert.Contains(player.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Equal(2, options.Descendants().Count(element => element.Name.LocalName == "ScrollViewer"));
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger");
        Assert.DoesNotContain(about.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.DoesNotContain(about.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.Contains(about.Descendants(), element => HasName(element, "AboutIcon") && element.Attribute("CornerRadius")?.Value == "20");
        Assert.Contains(about.Descendants(), element => HasName(element, "CreatedByLink") && element.Attribute("Content")?.Value == "Umberto Giacobbi");
        Assert.Contains(about.Descendants(), element => HasName(element, "CloseButton") && element.Attribute("HorizontalAlignment")?.Value == "Stretch");
        Assert.DoesNotContain(about.Descendants(), element => element.Attribute("Text")?.Value == "•••");
        Assert.Contains("private const int LogicalWindowWidth = 430;", aboutSource, StringComparison.Ordinal);
        Assert.Contains("private const int LogicalWindowHeight = 450;", aboutSource, StringComparison.Ordinal);
        Assert.Contains("SetTitleBar(TitleBarDragRegion);", aboutSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsResizable = false;", aboutSource, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMaximizable = false;", aboutSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsAndOperations_KeepFlatHierarchyWithScopedModelFeedback()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));

        Assert.DoesNotContain(options.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.DoesNotContain(operations.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.DoesNotContain(operations.Descendants(), element => element.Attribute("CornerRadius") is not null);
        Assert.Contains(options.Descendants(), element => element.Attribute("Style")?.Value.Contains("BodyStrongTextBlockStyle", StringComparison.Ordinal) == true);
        Assert.Contains(operations.Descendants(), element => element.Attribute("Style")?.Value.Contains("SubtitleTextBlockStyle", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(options.Descendants(), element => element.Attribute("Tag")?.Value.StartsWith("Options.Section.ActiveHours", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("active_hours.", optionsSource, StringComparison.Ordinal);
        Assert.Contains(options.Descendants(), element => HasName(element, "ModelInfoCard") && element.Attribute("CornerRadius")?.Value == "6");
        Assert.Contains(options.Descendants(), element => HasName(element, "ModelAccentBar") && element.Attribute("CornerRadius")?.Value == "2");
        Assert.Equal(2, options.Descendants().Count(element => element.Name.LocalName == "StackPanel" && element.Attribute("Padding")?.Value == "0,0,18,12"));
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "Border" && element.Attribute("Padding")?.Value == "0,10,18,0");
        var openFolderButton = options.Descendants().Single(element => HasName(element, "OpenScreenshotFolderButton"));
        Assert.Null(openFolderButton.Attribute("Content"));
        Assert.Contains(openFolderButton.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE8A7");
        var keepScreenshots = options.Descendants().Single(element => HasName(element, "KeepScreenshotsSwitch"));
        Assert.Equal("Right", keepScreenshots.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("0", keepScreenshots.Attribute("MinWidth")?.Value);
    }

    [Fact]
    public void OpenAiOptions_UseCatalogPickerAndPerSnapshotLayout()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml.cs"));
        var aiView = options.Descendants().Single(element => HasName(element, "AiOptionsView"));
        var modelPicker = options.Descendants().Single(element => HasName(element, "ModelBox"));
        var thinkingEffortPicker = options.Descendants().Single(element => HasName(element, "AiReasoningEffortBox"));

        Assert.Equal("ComboBox", modelPicker.Name.LocalName);
        Assert.Equal("Options.Model", modelPicker.Attribute("Tag")?.Value);
        Assert.Contains(modelPicker.Descendants(), element => element.Name.LocalName == "DataTemplate");
        Assert.Contains(modelPicker.Descendants(), element => element.Attribute("Background")?.Value == "{Binding AccentBrush}");
        Assert.Equal("ComboBox", thinkingEffortPicker.Name.LocalName);
        Assert.Equal("Options.Reasoning", thinkingEffortPicker.Attribute("Tag")?.Value);
        Assert.Contains(thinkingEffortPicker.Ancestors(), element => ReferenceEquals(element, aiView));
        Assert.DoesNotContain(thinkingEffortPicker.Ancestors(), element => HasName(element, "AiAdvancedPanel"));
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
        var captureActions = player.Descendants().Single(element => HasName(element, "CaptureActionsPanel"));
        var takeScreenshotButton = player.Descendants().Single(element => HasName(element, "TakeScreenshotButton"));
        var pendingSnapshotPanel = player.Descendants().Single(element => HasName(element, "PendingSnapshotPanel"));
        var deleteSnapshotButton = player.Descendants().Single(element => HasName(element, "DeleteSnapshotButton"));
        var deleteCountdownProgress = player.Descendants().Single(element => HasName(element, "SnapshotDeleteCountdownProgress"));
        var moreButton = player.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "TitleBarMoreButton"));
        var dragRegion = player.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "DragRegion"));
        var playerPanel = player.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "PlayerPanel"));
        var menu = player.Descendants().Single(element => element.Name.LocalName == "Flyout" && element.Attribute("Opened")?.Value == "MoreMenu_Opened");
        var menuTags = menu
            .Descendants()
            .Where(element => element.Name.LocalName == "TextBlock" && element.Attribute("Tag") is not null)
            .Select(element => element.Attribute("Tag")!.Value)
            .ToArray();

        Assert.Equal("Transparent", moreButton.Attribute("Background")?.Value);
        Assert.Null(moreButton.Attribute("Visibility"));
        Assert.Contains(moreButton.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE712");
        Assert.Equal("TopEdgeAlignedRight", menu.Attribute("Placement")?.Value);
        Assert.Equal("False", menu.Attribute("ShouldConstrainToRootBounds")?.Value);
        Assert.Equal("Horizontal", captureActions.Attribute("Orientation")?.Value);
        Assert.Equal("32", takeScreenshotButton.Attribute("Width")?.Value);
        Assert.Equal("{ThemeResource SnapshotCaptureBrush}", takeScreenshotButton.Attribute("Background")?.Value);
        Assert.Equal("Collapsed", pendingSnapshotPanel.Attribute("Visibility")?.Value);
        Assert.Equal("32", deleteSnapshotButton.Attribute("Width")?.Value);
        Assert.Equal("{ThemeResource SnapshotDeleteBrush}", deleteSnapshotButton.Attribute("Background")?.Value);
        Assert.Same(captureActions, deleteSnapshotButton.Parent);
        Assert.Equal("Collapsed", deleteSnapshotButton.Attribute("Visibility")?.Value);
        Assert.Equal("1", pendingSnapshotPanel.Parent?.Attribute("Grid.Column")?.Value);
        Assert.Equal("Stretch", deleteCountdownProgress.Attribute("HorizontalAlignment")?.Value);
        Assert.Contains("TakeScreenshotButton.IsEnabled = false;", mainSource, StringComparison.Ordinal);
        Assert.Contains("TakeScreenshotButton.IsEnabled = true;", mainSource, StringComparison.Ordinal);
        Assert.Equal(6, menu.Descendants().Count(element => element.Name.LocalName == "Button"));
        Assert.Equal(2, menu.Descendants().Count(element => element.Name.LocalName == "ToggleSwitch"));
        Assert.Equal(
            ["Reports.Title", "Screenshots.Caption", "Schedule.Snapshots", "MenuTitleOptions", "Main.Menu.Operations", "MenuToggleOpenAi", "MenuToggleScreenshot", "MenuTitleAbout"],
            menuTags);
        Assert.Contains("flyout.ShowAt(TitleBarMoreButton);", mainSource, StringComparison.Ordinal);
        Assert.Contains("ShowPanel(OperationsPanel, OperationsHeight);", mainSource, StringComparison.Ordinal);
        Assert.Contains("ApplyOverflowCommandLabel(OperationsMenuItem, T(\"Main.Menu.Operations\"));", mainSource, StringComparison.Ordinal);
        Assert.Contains("[\"screenshots.enabled\"]", mainSource, StringComparison.Ordinal);
        Assert.Contains("CaptureManualScreenshotAsync", mainSource, StringComparison.Ordinal);
        Assert.Contains("DeletePendingManualScreenshotAsync", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AnalyzeCapturedScreenshotAsync", mainSource, StringComparison.Ordinal);
        Assert.Single(dragRegion.Descendants(), element => element.Attribute("Text")?.Value == "TRACK ME UP");
        Assert.DoesNotContain(playerPanel.Descendants(), element => element.Attribute("Text")?.Value == "TRACK ME UP");
        Assert.Contains("InputNonClientPointerSource", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain(player.Descendants(), element => element.Name.LocalName == "ToggleMenuFlyoutItem");
    }

    [Fact]
    public void ScreenshotWindow_ExposesFileAndDeletionActionsFromEllipsis()
    {
        var screenshotWindow = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));
        var moreButton = screenshotWindow.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "TitleBarMoreButton"));
        var menu = screenshotWindow.Descendants().Single(element => element.Name.LocalName == "Flyout" && element.Attribute("Opened")?.Value == "MoreMenu_Opened");
        var actionInfoBar = screenshotWindow.Descendants().Single(element => HasName(element, "ScreenshotActionInfoBar"));
        var openFolderItem = menu.Descendants().Single(element => HasName(element, "OpenScreenshotFolderMenuItem"));
        var menuTags = menu
            .Descendants()
            .Where(element => element.Name.LocalName == "TextBlock" && element.Attribute("Tag") is not null)
            .Select(element => element.Attribute("Tag")!.Value)
            .ToArray();

        Assert.Contains(moreButton.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE712");
        Assert.Equal("TopEdgeAlignedRight", menu.Attribute("Placement")?.Value);
        Assert.Equal("False", menu.Attribute("ShouldConstrainToRootBounds")?.Value);
        Assert.Equal("False", actionInfoBar.Attribute("IsOpen")?.Value);
        Assert.Equal(5, menu.Descendants().Count(element => element.Name.LocalName == "Button"));
        Assert.Equal(
            ["Screenshots.Menu.Save", "Screenshots.Menu.Share", "Screenshots.Menu.OpenFolder", "Screenshots.Menu.DeleteScreenshot", "Screenshots.Menu.DeleteSnapshot"],
            menuTags);
        Assert.Contains("SaveScreenshotMenuItem_Click", source, StringComparison.Ordinal);
        Assert.Contains("ShareScreenshotMenuItem_Click", source, StringComparison.Ordinal);
        Assert.Contains("OpenScreenshotFolderMenuItem_Click", source, StringComparison.Ordinal);
        Assert.Contains("SaveScreenshotMenuItem.IsEnabled = hasSelection;", source, StringComparison.Ordinal);
        Assert.Contains("ShareScreenshotMenuItem.IsEnabled = hasSelection;", source, StringComparison.Ordinal);
        Assert.Null(openFolderItem.Attribute("IsEnabled"));
        Assert.DoesNotContain("OpenScreenshotFolderMenuItem.IsEnabled = hasSelection;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = GetSelectedItem();", source, StringComparison.Ordinal);
        Assert.Contains("ApplyMenuCommandLabel(SaveScreenshotMenuItem, \"Screenshots.Menu.Save\");", source, StringComparison.Ordinal);
        Assert.Contains("ShowActionResult(result", source, StringComparison.Ordinal);
        Assert.Contains("DeleteScreenshotAsync", source, StringComparison.Ordinal);
        Assert.Contains("DeleteSnapshotAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotSchedule_UsesASeparateThemedWindow()
    {
        var schedule = XDocument.Load(RepositoryFile("TrackMeUp", "ScheduleWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var scheduleSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ScheduleWindow.xaml.cs"));

        Assert.Equal("Window", schedule.Root?.Name.LocalName);
        Assert.Contains(schedule.Descendants(), element => element.Name.LocalName == "MicaBackdrop");
        Assert.Contains(schedule.Descendants(), element => HasName(element, "WorkingHoursEditor"));
        Assert.Contains("settingsResult.Value.ScreenshotIntervalMinutes", mainSource, StringComparison.Ordinal);
        Assert.Contains("ScheduleConfirmed += ScheduleWindow_ScheduleConfirmed", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ScheduleScreenshotDialog", mainSource, StringComparison.Ordinal);
        Assert.Contains("RootGrid.RequestedTheme = theme switch", scheduleSource, StringComparison.Ordinal);
        Assert.Contains("SetTitleBar(TitleBarDragRegion);", scheduleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_IsFixedSizeAndDoesNotRestorePersistedWindowState()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));

        Assert.Contains("presenter.IsResizable = false;", source, StringComparison.Ordinal);
        Assert.Contains("presenter.IsMaximizable = false;", source, StringComparison.Ordinal);
        Assert.Contains("ResizeForLogicalContent(PlayerHeight);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowStateKeys.Main", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreWindowState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveWindowState", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpandedPlayer_ShowsSnapshotPlaceholderAndSeparatedLocalUtcClock()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var localTime = player.Descendants().Single(element => HasName(element, "LocalTimeText"));
        var utcTime = player.Descendants().Single(element => HasName(element, "UtcTimeText"));
        var placeholder = player.Descendants().Single(element => HasName(element, "ScreenshotPlaceholderImage"));
        var previewButton = player.Descendants().Single(element => HasName(element, "ScreenshotPreviewButton"));
        var openOverlay = player.Descendants().Single(element => HasName(element, "ScreenshotOpenOverlay"));

        Assert.Equal("Left", localTime.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Right", utcTime.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("11", localTime.Attribute("FontSize")?.Value);
        Assert.Equal("11", utcTime.Attribute("FontSize")?.Value);
        Assert.Equal("ms-appx:///Assets/TrackMeUpSnapshotPlaceholder.png", placeholder.Attribute("Source")?.Value);
        Assert.Equal("ScreenshotPreviewButton_PointerEntered", previewButton.Attribute("PointerEntered")?.Value);
        Assert.Equal("ScreenshotPreviewButton_PointerExited", previewButton.Attribute("PointerExited")?.Value);
        Assert.Equal("0", openOverlay.Attribute("Opacity")?.Value);
        Assert.Contains("LocalTimeText.Text = $\"Local time {state.LocalTime:HH:mm:ss}\";", source, StringComparison.Ordinal);
        Assert.Contains("UtcTimeText.Text = $\"UTC {state.UtcTime:HH:mm:ss}\";", source, StringComparison.Ordinal);
        Assert.Contains("new ScreenshotPreviewRequestedEventArgs(screenshotPath, capturedAt)", source, StringComparison.Ordinal);
        Assert.Contains("session?.ScreenshotCapturedAt is { } capturedAt", source, StringComparison.Ordinal);
        Assert.Contains("private const int ExpandedPlayerHeight = 456;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeWindows_ExposeTitleBarsAndMicaWithoutOpaquePanelCards()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var reports = XDocument.Load(RepositoryFile("TrackMeUp", "ReportsWindow.xaml"));
        var about = XDocument.Load(RepositoryFile("TrackMeUp", "AboutWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var reportsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ReportsWindow.xaml.cs"));

        var playerUsesMica = player.Descendants().Any(element => element.Name.LocalName == "MicaBackdrop")
            || mainSource.Contains("SystemBackdrop = new MicaBackdrop", StringComparison.Ordinal);
        Assert.True(playerUsesMica, "MainWindow must use MicaBackdrop in XAML or assign it in code-behind.");
        Assert.Contains(reports.Descendants(), element => element.Name.LocalName == "MicaBackdrop");
        Assert.Contains(about.Descendants(), element => element.Name.LocalName == "MicaBackdrop");
        Assert.Contains("SetTitleBar(DragRegion)", mainSource, StringComparison.Ordinal);
        Assert.Contains("SetTitleBar(TitleBarDragRegion)", reportsSource, StringComparison.Ordinal);
        Assert.DoesNotContain(player.Descendants(), element => element.Attribute("Background")?.Value.Contains("FlyoutSurfaceBrush", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void LanguagePicker_OffersSystemModeAndExplicitOverrides()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var languagePicker = options.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "LanguageBox"));

        Assert.Equal("Options.Language", languagePicker.Attribute("Tag")?.Value);
        Assert.Contains(languagePicker.Descendants(), element => element.Attribute("Tag")?.Value == "system");
        Assert.Contains(languagePicker.Descendants(), element => element.Attribute("Tag")?.Value == "en");
    }

    [Fact]
    public void TaskbarSurface_UsesAnAlphaCapableWpfHost()
    {
        var widget = XDocument.Load(RepositoryFile("TrackMeUp.Taskbar", "TaskbarWidgetWindow.xaml"));
        var hostSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Services", "TaskbarWidgetHost.cs"));

        Assert.Equal("True", widget.Root?.Attribute("AllowsTransparency")?.Value);
        Assert.Equal("Transparent", widget.Root?.Attribute("Background")?.Value);
        Assert.Equal("None", widget.Root?.Attribute("WindowStyle")?.Value);
        Assert.Equal("False", widget.Root?.Attribute("ShowInTaskbar")?.Value);
        Assert.Contains("WsExNoActivate", hostSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskbarSurface_ParentsHiddenHwndBeforeFirstVisibleFrame()
    {
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp.Taskbar", "TaskbarWidgetWindow.xaml.cs"));
        var surfaceSource = File.ReadAllText(RepositoryFile("TrackMeUp.Taskbar", "TaskbarWidgetSurface.cs"));
        var hostSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Services", "TaskbarWidgetHost.cs"));
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
    public void MainWindow_RemainsVisibleWhenTaskbarWidgetAttaches()
    {
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var startUiStart = appSource.IndexOf("private void StartUi", StringComparison.Ordinal);
        var startUiEnd = appSource.IndexOf("private void StartReports", StringComparison.Ordinal);

        Assert.True(startUiStart >= 0 && startUiEnd > startUiStart, "App.StartUi source contract was not found.");
        var startUiSource = appSource[startUiStart..startUiEnd];
        Assert.Contains("_window.Activate();", startUiSource, StringComparison.Ordinal);
        Assert.True(
            startUiSource.IndexOf("_window.Activate();", StringComparison.Ordinal) < startUiSource.IndexOf("new TaskbarWidgetSurface", StringComparison.Ordinal),
            "MainWindow must activate before optional taskbar-widget initialization.");
        Assert.DoesNotContain("HideTopLevelWindow", startUiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstRun", startUiSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AlwaysShowInTaskbar", startUiSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TrackMeUp/MainWindow.xaml")]
    [InlineData("TrackMeUp/Controls/OptionsControl.xaml")]
    [InlineData("TrackMeUp/Controls/OperationsControl.xaml")]
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
}
