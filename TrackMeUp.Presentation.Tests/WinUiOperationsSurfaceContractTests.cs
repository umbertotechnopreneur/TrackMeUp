using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards complete, passive access to operational use cases from the WinUI frontend.</summary>
public sealed class WinUiOperationsSurfaceContractTests
{
    /// <summary>Guards the passive About diagnostics surface and its shared-runtime route.</summary>
    [Fact]
    public void AboutDiagnostics_UseFacadeRuntimeAndRedactedShareInfrastructure()
    {
        var about = File.ReadAllText(RepositoryFile("TrackMeUp", "AboutWindow.xaml.cs"));
        var runtime = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Runtime", "RuntimeHost.cs"));
        var logs = File.ReadAllText(RepositoryFile("TrackMeUp", "Services", "ApplicationLogService.cs"));

        Assert.Contains("_application.OpenApplicationLogFolderAsync", about, StringComparison.Ordinal);
        Assert.Contains("_application.ShareApplicationLogAsync", about, StringComparison.Ordinal);
        Assert.Contains("_application.OpenProductLinkAsync", about, StringComparison.Ordinal);
        Assert.DoesNotContain("System.IO", about, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", about, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics.log.open\"", runtime, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics.log.open_folder\"", runtime, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics.log.share\"", runtime, StringComparison.Ordinal);
        Assert.Contains("\"product.link.open\"", runtime, StringComparison.Ordinal);
        Assert.Contains("MaximumSharedSourceBytes", logs, StringComparison.Ordinal);
        Assert.Contains("CreateRedactedExport", logs, StringComparison.Ordinal);
        Assert.Contains("RedactForSharing", logs, StringComparison.Ordinal);
        Assert.Contains("OpenLogDirectory", logs, StringComparison.Ordinal);
    }

    /// <summary>Ensures operational tools are separate, reachable from settings, and usable at narrow widths.</summary>
    [Fact]
    public void OperationsSurface_UsesFocusedPagesAndSettingsNavigation()
    {
        var mainWindow = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));
        var snapshots = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "SnapshotAiOperationsControl.xaml"));
        var reports = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "ReportsOperationsControl.xaml"));
        var privacy = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "PrivacyOperationsControl.xaml"));
        var retention = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "RetentionOperationsControl.xaml"));
        var plugins = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "PluginOperationsControl.xaml"));

        Assert.Contains(mainWindow.Descendants(), element => element.Name.LocalName == "OperationsControl");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger");
        Assert.Contains(operations.Descendants(), element => element.Name.LocalName == "TimedInfoBar" && HasName(element, "OperationBanner"));
        Assert.DoesNotContain(operations.Descendants(), element => element.Name.LocalName == "ToggleButton" && element.Attribute("Tag")?.Value.StartsWith("Operations.Section.", StringComparison.Ordinal) == true);
        Assert.Contains(operations.Descendants(), element => HasName(element, "RuntimeCapabilitiesList"));
        Assert.Contains(operations.Descendants(), element => HasName(element, "SystemDisksList"));

        var settingsLinks = options.Descendants()
            .Where(element => element.Name.LocalName == "HyperlinkButton" && element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value.EndsWith("OperationsLink", StringComparison.Ordinal)))
            .ToArray();
        Assert.True(settingsLinks.Length >= 3);
        Assert.All(new[] { "SnapshotAiOperationsLink", "ReportsOperationsLink", "PrivacyOperationsLink", "RetentionOperationsLink", "PluginsOperationsLink" },
            name => Assert.Contains(settingsLinks, element => HasName(element, name)));

        var privacyLink = settingsLinks.Single(element => HasName(element, "PrivacyOperationsLink"));
        Assert.Contains(options.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Navigation.Privacy.Title");
        Assert.Contains(options.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Navigation.Privacy.Description");
        Assert.Contains(privacyLink.Descendants(), element => element.Attribute("Tag")?.Value == "Options.Navigation.Privacy.Action");
        Assert.Contains(privacyLink.Descendants(), element => element.Name.LocalName == "FontIcon" && element.Attribute("Glyph")?.Value == "\uE76C");

        var settingsLinkIcons = new[]
        {
            (Name: "SnapshotAiOperationsLink", Color: "#FFE88F6B", Glyph: "\uE7ED"),
            (Name: "ReportsOperationsLink", Color: "#FF7D9FF8", Glyph: "\uE787"),
            (Name: "PrivacyOperationsLink", Color: "#FFA97BEA", Glyph: "\uE72E"),
            (Name: "RetentionOperationsLink", Color: "#FF85A8DB", Glyph: "\uE823"),
            (Name: "PluginsOperationsLink", Color: "#FF71CBB7", Glyph: "\uE90F")
        };
        Assert.All(settingsLinkIcons, expected =>
        {
            var link = settingsLinks.Single(element => HasName(element, expected.Name));
            var icons = link.Descendants().Where(element => element.Name.LocalName == "FontIcon").ToArray();
            Assert.Equal(2, icons.Length);
            Assert.Equal(expected.Color, icons[0].Attribute("Foreground")?.Value);
            Assert.Equal(expected.Glyph, icons[0].Attribute("Glyph")?.Value);
            Assert.Equal("Raw", icons[0].Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.AccessibilityView").Value);
            Assert.Equal("\uE76C", icons[1].Attribute("Glyph")?.Value);
        });

        Assert.All(new[] { snapshots, reports, privacy, retention, plugins }, document =>
        {
            Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "InfoBar");
            Assert.Contains(document.Descendants(), element => element.Attribute("Tag")?.Value?.EndsWith(".Description", StringComparison.Ordinal) == true);
        });
        Assert.DoesNotContain(snapshots.Descendants(), element => element.Attribute("Tag")?.Value == "Operations.TakeSnapshotNow");
        Assert.All(new[] { "SnapshotAiSection", "ReportsSection", "PrivacySection", "RetentionSection", "PluginsSection" },
            name => Assert.Contains(operations.Descendants(), element => HasName(element, name)));

        var operationLinkIcons = new[]
        {
            (Name: "OpenSnapshotAiLink", Color: "#FFE88F6B", Glyph: "\uE7ED"),
            (Name: "OpenReportsLink", Color: "#FF7D9FF8", Glyph: "\uE787"),
            (Name: "OpenPrivacyLink", Color: "#FFA97BEA", Glyph: "\uE72E"),
            (Name: "OpenRetentionLink", Color: "#FF85A8DB", Glyph: "\uE823"),
            (Name: "OpenPluginsLink", Color: "#FF71CBB7", Glyph: "\uE90F")
        };
        Assert.All(operationLinkIcons, expected =>
        {
            var link = operations.Descendants().Single(element => HasName(element, expected.Name));
            var icons = link.Descendants().Where(element => element.Name.LocalName == "FontIcon").ToArray();
            Assert.Equal(2, icons.Length);
            Assert.Equal(expected.Color, icons[0].Attribute("Foreground")?.Value);
            Assert.Equal(expected.Glyph, icons[0].Attribute("Glyph")?.Value);
            Assert.Equal("Raw", icons[0].Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.AccessibilityView").Value);
            Assert.Equal("\uE76C", icons[1].Attribute("Glyph")?.Value);
        });
    }

    /// <summary>Guards automatic plugin loading and direct, per-plugin switch updates.</summary>
    [Fact]
    public void PluginOperations_AutoLoadAndUseOneWayPerPluginSwitches()
    {
        var plugins = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "PluginOperationsControl.xaml"));
        var pluginSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "PluginOperationsControl.xaml.cs"));
        var operationsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml.cs"));
        var contextSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsSectionContext.cs"));
        var list = plugins.Descendants().Single(element => HasName(element, "PluginsList"));
        var toggle = plugins.Descendants().Single(element => element.Name.LocalName == "ToggleSwitch");

        Assert.Equal("None", list.Attribute("SelectionMode")?.Value);
        Assert.Equal("{Binding Enabled, Mode=OneWay}", toggle.Attribute("IsOn")?.Value);
        Assert.Equal("{Binding Id}", toggle.Attribute("Tag")?.Value);
        Assert.Equal("PluginToggle_Toggled", toggle.Attribute("Toggled")?.Value);
        Assert.Equal(string.Empty, toggle.Attribute("OnContent")?.Value);
        Assert.Equal(string.Empty, toggle.Attribute("OffContent")?.Value);
        Assert.DoesNotContain(plugins.Descendants(), element => element.Name.LocalName == "Button");

        Assert.Contains("internal async Task LoadAsync()", pluginSource, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(pluginSource, "GetPluginsAsync"));
        Assert.Contains("showSuccess: false", pluginSource, StringComparison.Ordinal);
        Assert.Contains("SetPluginEnabledAsync(plugin.Id, requestedState, token)", pluginSource, StringComparison.Ordinal);
        Assert.Contains("_isApplyingPluginState", pluginSource, StringComparison.Ordinal);
        Assert.Contains("plugin.Enabled == toggle.IsOn", pluginSource, StringComparison.Ordinal);
        Assert.Contains("RestoreToggle(toggle, plugin.Enabled);", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshButton_Click", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EnableButton_Click", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DisableButton_Click", pluginSource, StringComparison.Ordinal);

        Assert.Contains("if (section == OperationsSection.Plugins)", operationsSource, StringComparison.Ordinal);
        Assert.Contains("_ = PluginsSection.LoadAsync();", operationsSource, StringComparison.Ordinal);
        Assert.Contains("bool showSuccess = true", contextSource, StringComparison.Ordinal);
        Assert.Contains("if (showSuccess)", contextSource, StringComparison.Ordinal);
        Assert.Contains("ResultMessage(result.MessageKey", contextSource, StringComparison.Ordinal);
        Assert.Contains("_tryTranslate(messageKey)", contextSource, StringComparison.Ordinal);
        Assert.Contains("key => _strings.TryTranslate(key, out var value) ? value : null", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("result.Code", contextSource, StringComparison.Ordinal);
        Assert.Contains("ResultMessage(result.MessageKey", operationsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("result.Code", operationsSource, StringComparison.Ordinal);
        Assert.Contains("Context.ResultMessage(result.MessageKey", pluginSource, StringComparison.Ordinal);
        Assert.DoesNotContain("result.Code", pluginSource, StringComparison.Ordinal);
    }

    /// <summary>Guards the shared overlay, single-layer Acrylic material, subtle geometry, and configurable timeout.</summary>
    [Fact]
    public void CentralBanners_UseOneTimedAcrylicOverlay()
    {
        var app = XDocument.Load(RepositoryFile("TrackMeUp", "App.xaml"));
        var mainWindow = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));
        var banner = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "TimedInfoBar.xaml"));
        var screenshotWindow = XDocument.Load(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml"));
        var service = File.ReadAllText(RepositoryFile("TrackMeUp", "MicaDialogService.cs"));
        var bannerSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "TimedInfoBar.xaml.cs"));
        var operationsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml.cs"));
        var mainWindowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var screenshotSource = File.ReadAllText(RepositoryFile("TrackMeUp", "ScreenshotWindow.xaml.cs"));

        var overlay = operations.Descendants().Single(element => element.Name.LocalName == "TimedInfoBar");
        Assert.True(HasName(overlay, "OperationBanner"));
        Assert.Equal("Top", overlay.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("100", overlay.Attributes().Single(attribute => attribute.Name.LocalName == "Canvas.ZIndex").Value);
        Assert.True(overlay.Parent is { } parent && HasName(parent, "RootLayout"));
        Assert.All(operations.Descendants().Where(element => element.Name.LocalName == "ScrollViewer"),
            scrollViewer => Assert.DoesNotContain(scrollViewer.Attributes(), attribute => attribute.Name.LocalName == "Grid.Row"));

        Assert.Single(banner.Descendants(), element => element.Name.LocalName == "InfoBar");
        var infoBar = banner.Descendants().Single(element => element.Name.LocalName == "InfoBar");
        var bannerSurface = banner.Descendants().Single(element => HasName(element, "BannerSurface"));
        var acrylicBackdrop = banner.Descendants().Single(element => HasName(element, "AcrylicBackdrop"));
        var progress = banner.Descendants().Single(element => element.Name.LocalName == "ProgressBar");
        Assert.Equal("620", banner.Root?.Attribute("MaxWidth")?.Value);
        Assert.Equal("Stretch", banner.Root?.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("{ThemeResource TimedInfoBarBackdropBrush}", acrylicBackdrop.Attribute("Background")?.Value);
        Assert.Equal("{ThemeResource TimedInfoBarGlassBorderBrush}", acrylicBackdrop.Attribute("BorderBrush")?.Value);
        Assert.Equal("1", acrylicBackdrop.Attribute("BorderThickness")?.Value);
        Assert.Equal("14", acrylicBackdrop.Attribute("CornerRadius")?.Value);
        Assert.Equal("14", infoBar.Attribute("CornerRadius")?.Value);
        Assert.Equal("82", infoBar.Attribute("MinHeight")?.Value);
        Assert.Equal("6,4,6,10", infoBar.Attribute("Padding")?.Value);
        Assert.DoesNotContain(banner.Descendants(), element => HasName(element, "FrostedVeil"));
        Assert.DoesNotContain(banner.Descendants(), element => element.Name.LocalName.Contains("GradientBrush", StringComparison.Ordinal));
        Assert.Equal("Transparent", infoBar.Attribute("Background")?.Value);
        Assert.Equal("Transparent", infoBar.Attribute("BorderBrush")?.Value);
        Assert.Equal("0", infoBar.Attribute("BorderThickness")?.Value);
        Assert.Equal("BannerInfoBar_Closing", infoBar.Attribute("Closing")?.Value);
        var transparentSemanticBackgrounds = infoBar.Descendants()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .Where(element => element.Attribute("Color")?.Value == "Transparent")
            .SelectMany(element => element.Attributes().Where(attribute => attribute.Name.LocalName == "Key"))
            .Select(attribute => attribute.Value)
            .ToArray();
        Assert.Equal(2, transparentSemanticBackgrounds.Count(key => key == "InfoBarInformationalSeverityBackgroundBrush"));
        Assert.Equal(2, transparentSemanticBackgrounds.Count(key => key == "InfoBarSuccessSeverityBackgroundBrush"));
        Assert.Equal(2, transparentSemanticBackgrounds.Count(key => key == "InfoBarWarningSeverityBackgroundBrush"));
        Assert.Equal(2, transparentSemanticBackgrounds.Count(key => key == "InfoBarErrorSeverityBackgroundBrush"));
        Assert.Equal("0", bannerSurface.Attribute("Opacity")?.Value);
        Assert.Equal("2", progress.Attribute("Height")?.Value);
        Assert.Equal("16,0,16,8", progress.Attribute("Margin")?.Value);
        Assert.Equal("{ThemeResource DividerStrokeColorDefaultBrush}", progress.Attribute("Background")?.Value);
        Assert.Equal("{ThemeResource BrandCoralBrush}", progress.Attribute("Foreground")?.Value);
        Assert.Equal("Raw", progress.Attributes().Single(attribute => attribute.Name.LocalName == "AutomationProperties.AccessibilityView").Value);

        var coralBrushes = app.Descendants()
            .Where(element => element.Name.LocalName == "SolidColorBrush" &&
                              element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "BrandCoralBrush"))
            .ToArray();
        Assert.Equal(2, coralBrushes.Length);
        Assert.All(coralBrushes, brush => Assert.Equal("#FFF9665B", brush.Attribute("Color")?.Value));
        Assert.Contains(app.Descendants(), element =>
            element.Name.LocalName == "StaticResource" &&
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "BrandCoralBrush") &&
            element.Attribute("ResourceKey")?.Value == "SystemColorHighlightColorBrush");

        var timedBackdropBrushes = app.Descendants()
            .Where(element => element.Name.LocalName == "AcrylicBrush" && HasKey(element, "TimedInfoBarBackdropBrush"))
            .ToArray();
        var timedBorders = app.Descendants()
            .Where(element => element.Name.LocalName == "SolidColorBrush" && HasKey(element, "TimedInfoBarGlassBorderBrush"))
            .ToArray();
        Assert.Equal(2, timedBackdropBrushes.Length);
        Assert.Contains(timedBackdropBrushes, brush => brush.Attribute("TintOpacity")?.Value == "0.68");
        Assert.Contains(timedBackdropBrushes, brush => brush.Attribute("TintOpacity")?.Value == "0.60");
        Assert.DoesNotContain(app.Descendants(), element => HasKey(element, "TimedInfoBarGlassVeilBrush"));
        Assert.Equal(2, timedBorders.Length);
        Assert.Contains(timedBorders, brush => brush.Attribute("Color")?.Value == "#4D727C78");
        Assert.Contains(timedBorders, brush => brush.Attribute("Color")?.Value == "#3DFFFFFF");
        Assert.Contains(app.Descendants(), element =>
            element.Name.LocalName == "StaticResource" && HasKey(element, "TimedInfoBarBackdropBrush") &&
            element.Attribute("ResourceKey")?.Value == "SystemColorWindowColorBrush");

        Assert.Contains("TimeSpan.FromSeconds(10)", service, StringComparison.Ordinal);
        Assert.Contains("TimeSpan? timeout = null", service, StringComparison.Ordinal);
        Assert.Contains("ValidateBannerTimeout", service, StringComparison.Ordinal);
        Assert.Contains("DispatcherQueueTimer", service, StringComparison.Ordinal);
        Assert.Contains("Stopwatch.GetElapsedTime", service, StringComparison.Ordinal);
        Assert.Contains("host.DispatcherQueue.HasThreadAccess", service, StringComparison.Ordinal);
        Assert.Contains("host.Dismissed", service, StringComparison.Ordinal);
        Assert.Contains("countdown.Generation != generation", service, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(80)", bannerSource, StringComparison.Ordinal);
        Assert.Contains("private const float BannerElevation = 18f;", bannerSource, StringComparison.Ordinal);
        Assert.Contains("args.Cancel = true;", bannerSource, StringComparison.Ordinal);
        Assert.Contains("new UISettings().AnimationsEnabled", bannerSource, StringComparison.Ordinal);
        Assert.Contains("_transitionGeneration", bannerSource, StringComparison.Ordinal);
        Assert.Contains("if (!_isPresented || _isDismissing)", bannerSource, StringComparison.Ordinal);
        Assert.Contains("generation != _transitionGeneration", bannerSource, StringComparison.Ordinal);
        Assert.Contains("StartOpacityTransition(1d, completion: null, initialOpacity: 0d);", bannerSource, StringComparison.Ordinal);
        Assert.Contains("StartOpacityTransition(0d, () => CompleteDismissal(generation));", bannerSource, StringComparison.Ordinal);
        Assert.Contains(screenshotWindow.Descendants(), element =>
            element.Name.LocalName == "TimedInfoBar" && HasName(element, "ScreenshotActionBanner"));
        Assert.Contains(mainWindow.Descendants(), element =>
            element.Name.LocalName == "TimedInfoBar" && HasName(element, "MainNotificationBanner"));
        Assert.Contains("IsFrameAnalysisNotification(notification)", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("\"Notification.AiAnalysisFailed.Title\" or", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("\"Notification.AiDailyLimitReached.Title\";", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowErrorBanner(MainNotificationBanner, title, message);", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowSuccessBanner(ScreenshotActionBanner, title, message);", screenshotSource, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowErrorBanner(ScreenshotActionBanner, title, message);", screenshotSource, StringComparison.Ordinal);
        Assert.Equal(5, CountOccurrences(operationsSource, "ownerWindow, OperationBanner"));
    }

    /// <summary>Ensures every requested operation remains delegated through the shared facade.</summary>
    [Fact]
    public void OperationsCodeBehind_InvokesCompleteFacadeSurfaceWithoutDirectIo()
    {
        var sources = new[]
        {
            "OperationsControl.xaml.cs",
            "SnapshotAiOperationsControl.xaml.cs",
            "ReportsOperationsControl.xaml.cs",
            "PrivacyOperationsControl.xaml.cs",
            "RetentionOperationsControl.xaml.cs",
            "PluginOperationsControl.xaml.cs"
        }.Select(file => File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", file))).ToArray();
        var source = string.Join(Environment.NewLine, sources);
        string[] requiredFacadeCalls =
        [
            "GetRuntimeHealthAsync",
            "CaptureSystemSnapshotAsync",
            "GetLatestScreenshotAsync",
            "OpenScreenshotFolderAsync",
            "AnalyzeCurrentActivityAsync",
            "GenerateTodayReportAsync",
            "GenerateDailyDigestAsync",
            "OpenReportsFolderAsync",
            "GetPrivacyRulesAsync",
            "AddPrivacyRuleAsync",
            "RemovePrivacyRuleAsync",
            "TestCurrentPrivacyAsync",
            "GetRetentionStatusAsync",
            "PreviewRetentionAsync",
            "RunRetentionAsync",
            "PrepareAtomicResetAsync",
            "GetPluginsAsync",
            "SetPluginEnabledAsync"
        ];

        Assert.All(requiredFacadeCalls, call => Assert.Contains(call, source, StringComparison.Ordinal));
        Assert.DoesNotContain("System.IO", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Registry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LocalStore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ScreenCaptureService", source, StringComparison.Ordinal);
    }

    /// <summary>Ensures destructive retention execution cannot be triggered without a safe-default dialog.</summary>
    [Fact]
    public void RetentionExecution_RequiresExplicitUiAndApplicationConfirmation()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "RetentionOperationsControl.xaml.cs"));

        Assert.Contains("Dialogs.ConfirmAsync(", source, StringComparison.Ordinal);
        Assert.Contains("MicaDialogRequest.Confirmation(", source, StringComparison.Ordinal);
        Assert.Contains("if (!confirmed)", source, StringComparison.Ordinal);
        Assert.Contains("new RetentionRequest(Execute: true, Confirmed: true)", source, StringComparison.Ordinal);
    }

    /// <summary>Guards the two-step warning flow and keeps destructive reset work behind the shared facade.</summary>
    [Fact]
    public void AtomicReset_RequiresTwoSharedDialogsAndUsesTheApplicationFacade()
    {
        var surface = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml.cs"));
        var button = surface.Descendants().Single(element => HasName(element, "AtomicNukeButton"));

        Assert.Equal("AtomicNukeButton_Click", button.Attribute("Click")?.Value);
        Assert.Contains(surface.Descendants(), element => element.Attribute("Tag")?.Value == "Operations.AtomicNuke.Description");
        Assert.Equal(2, CountOccurrences(source, "Dialogs.ConfirmAsync("));
        Assert.Contains("new AtomicResetRequest(firstConfirmation, finalConfirmation)", source, StringComparison.Ordinal);
        Assert.Contains("PrepareAtomicResetAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Delete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", source, StringComparison.Ordinal);
    }

    /// <summary>Prevents view code from bypassing the single queued dialog engine.</summary>
    [Fact]
    public void WindowViews_DoNotConstructAdHocSystemOrContentDialogs()
    {
        var trackMeUpDirectory = Path.GetDirectoryName(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"))!;
        var sources = Directory.EnumerateFiles(trackMeUpDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var path in sources)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("new ContentDialog", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MessageBox.Show", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new MessageDialog", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Guards the Acrylic, safe-cancel and cross-process notification contracts.</summary>
    [Fact]
    public void DialogEngine_IsAcrylicQueuedAccessibleAndFacadeBacked()
    {
        var service = File.ReadAllText(RepositoryFile("TrackMeUp", "MicaDialogService.cs"));
        var interop = File.ReadAllText(RepositoryFile("TrackMeUp", "Services", "WindowInteropService.cs"));
        var dialog = File.ReadAllText(RepositoryFile("TrackMeUp", "MicaDialogWindow.xaml.cs"));
        var dialogXaml = XDocument.Load(RepositoryFile("TrackMeUp", "MicaDialogWindow.xaml"));
        var connectionDialog = File.ReadAllText(RepositoryFile("TrackMeUp", "AiConnectionTestDialogWindow.xaml.cs"));
        var connectionDialogXaml = XDocument.Load(RepositoryFile("TrackMeUp", "AiConnectionTestDialogWindow.xaml"));
        var main = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var app = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var schedule = File.ReadAllText(RepositoryFile("TrackMeUp", "ScheduleWindow.xaml.cs"));

        Assert.Contains("SemaphoreSlim", service, StringComparison.Ordinal);
        Assert.Contains("AccentColor", service, StringComparison.Ordinal);
        Assert.Contains("WindowInteropService.DisableCurrentThreadPeerWindows(dialogHandle)", service, StringComparison.Ordinal);
        Assert.Contains("WindowInteropService.RestoreWindows(disabledPeerWindows)", service, StringComparison.Ordinal);
        Assert.Contains("EnumThreadWindows", interop, StringComparison.Ordinal);
        Assert.Contains("EnableWindow(windowHandle, false)", interop, StringComparison.Ordinal);
        Assert.Contains("EnableWindow(windowHandle, true)", interop, StringComparison.Ordinal);
        Assert.Contains("return await ShowAsync(application, owner, request, theme) == MicaDialogResult.Primary;", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SavePlacementAsync", service, StringComparison.Ordinal);
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(dialogXaml.Descendants(), element =>
            HasName(element, "RootGrid") && element.Attribute("Background")?.Value == "Transparent");
        Assert.DoesNotContain(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "Border"
            && element.Attribute("Background")?.Value?.Contains("LayerFillColorDefaultBrush", StringComparison.Ordinal) == true);
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "Rectangle"
            && HasName(element, "AccentVeil")
            && element.Attribute("IsHitTestVisible")?.Value == "False");
        Assert.DoesNotContain(dialogXaml.Descendants(), element => HasName(element, "AccentIconSurface"));
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "FontIcon"
            && HasName(element, "SeverityIcon")
            && element.Attribute("FontSize")?.Value == "30");
        Assert.DoesNotContain(dialogXaml.Descendants(), element => element.Attribute("Style")?.Value == "{StaticResource DialogActionButtonStyle}");
        Assert.Contains(dialogXaml.Descendants(), element => HasName(element, "PrimaryButton") && element.Attribute("Style")?.Value == "{StaticResource AccentButtonStyle}");
        Assert.Contains("ExtendsContentIntoTitleBar = true;", dialog, StringComparison.Ordinal);
        Assert.Contains("WindowInteropService.SetOwner(_windowHandle, ownerHandle)", dialog, StringComparison.Ordinal);
        Assert.Contains("WindowInteropService.MakeTopmostWithoutActivation(_windowHandle)", dialog, StringComparison.Ordinal);
        Assert.Contains("SetWindowLongPtr64", interop, StringComparison.Ordinal);
        Assert.Contains("SetWindowPos(windowHandle, HwndTopMost", interop, StringComparison.Ordinal);
        Assert.DoesNotContain("DllImport", dialog, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.Dialog", dialog, StringComparison.Ordinal);
        Assert.Contains("await _placement.RestoreAndCenterAsync(RootGrid, CancellationToken.None);", dialog, StringComparison.Ordinal);
        Assert.Contains("Closed += (_, _) => _completion.TrySetResult(_result);", dialog, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName", dialog, StringComparison.Ordinal);
        Assert.Contains(dialogXaml.Descendants(), element =>
            element.Name.LocalName == "ScrollViewer"
            && HasName(element, "MessageScrollViewer")
            && element.Attribute("VerticalScrollBarVisibility")?.Value == "Hidden"
            && element.Attribute("VerticalScrollMode")?.Value == "Disabled");
        Assert.Contains(dialogXaml.Descendants(), element =>
            HasName(element, "DialogMessageText") && element.Attribute("IsTextSelectionEnabled")?.Value == "True");
        Assert.Contains("MessageScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;", dialog, StringComparison.Ordinal);
        Assert.Contains("DialogBody.Measure", dialog, StringComparison.Ordinal);
        Assert.Contains("LogicalMaximumHeight", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("LogicalInformationHeight", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("LogicalConfirmationHeight", dialog, StringComparison.Ordinal);
        Assert.Contains("await _placement.SaveAsync(CancellationToken.None);", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("PrimaryButton.Background", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("GetContrastingForeground", dialog, StringComparison.Ordinal);
        Assert.Contains("AccentVeil.Fill = CreateAccentVeil(accent, theme);", dialog, StringComparison.Ordinal);
        Assert.Contains("new RadialGradientBrush", dialog, StringComparison.Ordinal);
        Assert.Contains("ElementTheme.Dark => (byte)30", dialog, StringComparison.Ordinal);
        Assert.Contains("VirtualKey.Escape", dialog, StringComparison.Ordinal);
        Assert.Contains(dialogXaml.Descendants(), element => element.Attribute("AutomationProperties.LiveSetting")?.Value == "Assertive");
        Assert.Contains("DrainApplicationNotificationsAsync", main, StringComparison.Ordinal);
        Assert.Contains("Enabled: true, HasKey: false", main, StringComparison.Ordinal);
        Assert.Contains("private readonly MicaDialogService _dialogs = new();", app, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ConfirmAsync(", schedule, StringComparison.Ordinal);
        Assert.Contains(connectionDialogXaml.Descendants(), element => element.Name.LocalName == "DesktopAcrylicBackdrop");
        Assert.Contains(connectionDialogXaml.Descendants(), element => HasName(element, "TerminalScrollViewer"));
        Assert.Contains(connectionDialogXaml.Descendants(), element => HasName(element, "TerminalText") && element.Attribute("FontFamily")?.Value == "Cascadia Mono");
        Assert.Contains("AiConnectionTestProtocol.Prompt", connectionDialog, StringComparison.Ordinal);
        Assert.Contains("AppendTerminalAsync", connectionDialog, StringComparison.Ordinal);
        Assert.Contains("RestoreAndCenterAsync", connectionDialog, StringComparison.Ordinal);
        Assert.Contains("UiLocalization.Apply(RootGrid, _strings);", connectionDialog, StringComparison.Ordinal);
        Assert.Contains("AiConnectionTest.Terminal.Response", connectionDialog, StringComparison.Ordinal);
        Assert.Contains("new LocalizationService(", service, StringComparison.Ordinal);
        Assert.Contains(connectionDialogXaml.Descendants(), element => element.Attribute("Tag")?.Value == "AiConnectionTest.Eyebrow");
        Assert.Contains(connectionDialogXaml.Descendants(), element => element.Attribute("Tag")?.Value == "AiConnectionTest.Console");
        Assert.Contains(connectionDialogXaml.Descendants(), element => element.Attribute("Tag")?.Value == "AiConnectionTest.Cancel");
    }

    private static bool HasName(XElement element, string value)
        => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == value);

    private static bool HasKey(XElement element, string key)
        => element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == key);

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        for (var offset = 0; (offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0; offset += value.Length)
        {
            count++;
        }

        return count;
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
