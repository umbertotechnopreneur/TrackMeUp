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

    [Fact]
    public void SettingsAndOperations_UseNativeTypographyInsteadOfCardHierarchy()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OptionsControl.xaml"));
        var operations = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "OperationsControl.xaml"));

        Assert.DoesNotContain(options.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.DoesNotContain(operations.Descendants(), element => element.Name.LocalName == "Expander");
        Assert.DoesNotContain(options.Descendants(), element => element.Attribute("CornerRadius") is not null);
        Assert.DoesNotContain(operations.Descendants(), element => element.Attribute("CornerRadius") is not null);
        Assert.Contains(options.Descendants(), element => element.Attribute("Style")?.Value.Contains("TitleTextBlockStyle", StringComparison.Ordinal) == true);
        Assert.Contains(operations.Descendants(), element => element.Attribute("Style")?.Value.Contains("SubtitleTextBlockStyle", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void MainMenu_UsesNativeSwitchAndSnapshotProductWording()
    {
        var player = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var moreButton = player.Descendants().Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "MoreButton"));

        Assert.Equal("Transparent", moreButton.Attribute("Background")?.Value);
        Assert.Contains(player.Descendants(), element => element.Name.LocalName == "ToggleSwitch" && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "OpenAiMenuToggle"));
        Assert.Contains(player.Descendants(), element => element.Attribute("Tag")?.Value == "Main.Menu.TakeSnapshotNow" && element.Attribute("Text")?.Value == "Take snapshot now");
        Assert.DoesNotContain(player.Descendants(), element => element.Name.LocalName == "ToggleMenuFlyoutItem");
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
}
