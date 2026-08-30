// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Guards the shared title-bar and main-window lifetime ownership contracts.</summary>
public sealed class WindowChromeLifecycleContractTests
{
    private static readonly string[] MigratedTopLevelWindows =
    [
        "AboutWindow",
        "OcrTextWindow",
        "QuickSetupWindow",
        "ReportsWindow",
        "ScheduleWindow",
        "ScreenshotWindow",
        "SearchIndexingWindow",
        "SearchWindow",
        "ThirdPartyLicensesWindow"
    ];

    private static readonly string[] MigratedDialogWindows =
    [
        "ActivityCalendarDialogWindow",
        "AiConnectionTestDialogWindow",
        "AiPricingDialogWindow",
        "AiScreenshotReprocessingDialogWindow",
        "ScreenshotStorageMigrationDialogWindow",
        "WorldClockCityPickerDialogWindow"
    ];

    /// <summary>Verifies that the main and world-clock windows share the DPI-aware title-bar controller.</summary>
    [Fact]
    public void MainAndWorldClock_UseOneDpiAwareTitleBarController()
    {
        var controller = File.ReadAllText(RepositoryFile("TrackMeUp", "CustomTitleBarController.cs"));
        var main = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var worldClock = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));

        Assert.Contains("PreferredHeightOption = TitleBarHeightOption.Tall", controller, StringComparison.Ordinal);
        Assert.Contains("xamlRoot.RasterizationScale", controller, StringComparison.Ordinal);
        Assert.Contains("_appWindow.TitleBar.LeftInset / scale", controller, StringComparison.Ordinal);
        Assert.Contains("_appWindow.TitleBar.RightInset / scale", controller, StringComparison.Ordinal);
        Assert.Contains("NonClientRegionKind.Passthrough", controller, StringComparison.Ordinal);
        Assert.Contains("new AccessibilitySettings().HighContrast", controller, StringComparison.Ordinal);
        Assert.Contains("new CustomTitleBarController(", main, StringComparison.Ordinal);
        Assert.Contains("new CustomTitleBarController(", worldClock, StringComparison.Ordinal);
        Assert.DoesNotContain("InputNonClientPointerSource", main, StringComparison.Ordinal);
        Assert.DoesNotContain("InputNonClientPointerSource", worldClock, StringComparison.Ordinal);
    }

    /// <summary>Verifies shared title-bar command sizing, label alignment, and PNG brand artwork.</summary>
    [Fact]
    public void TitleBars_ShareAlignedCommandsLabelsAndPngBrandMark()
    {
        var styles = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "TitleBarOverflowButtonStyles.xaml"));
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var worldClock = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var commandStyle = styles.Descendants().Single(element => KeyValue(element) == "TrackMeUpTitleBarCommandButtonStyle");
        var logoStyle = styles.Descendants().Single(element => KeyValue(element) == "TrackMeUpTitleBarLogoStyle");

        AssertSetter(commandStyle, "Width", "40");
        AssertSetter(commandStyle, "Height", "40");
        AssertSetter(commandStyle, "VerticalAlignment", "Center");
        AssertSetter(logoStyle, "Width", "22");
        AssertSetter(logoStyle, "Height", "22");
        AssertSetter(logoStyle, "Source", "ms-appx:///Assets/TrackMeUpSquare44Logo.png");
        Assert.DoesNotContain(".webp", styles.ToString(), StringComparison.OrdinalIgnoreCase);

        Assert.All(
            main.Descendants().Where(element => HasName(element, "TitleBarLogo"))
                .Concat(worldClock.Descendants().Where(element => HasName(element, "TitleBarLogo"))),
            logo => Assert.Equal("{StaticResource TrackMeUpTitleBarLogoStyle}", logo.Attribute("Style")?.Value));
        Assert.Equal("48", FirstRowHeight(main));
        Assert.Equal("48", FirstRowHeight(worldClock));
    }

    /// <summary>Verifies that main-window initialization is tracked and cancelled with the window lifetime.</summary>
    [Fact]
    public void MainInitialization_IsTrackedAndCancelledWithWindowLifetime()
    {
        var lifecycle = File.ReadAllText(RepositoryFile("TrackMeUp", "WindowSurfaceLifecycle.cs"));
        var main = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));

        Assert.Contains("private Task? _initializationTask;", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_initializationTask = ObserveInitializationAsync", lifecycle, StringComparison.Ordinal);
        Assert.Contains("WaitUntilLoadedAsync", lifecycle, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("_lifecycle.StartInitialization(cancellationToken => InitializeAsync(options, cancellationToken));", main, StringComparison.Ordinal);
        Assert.Contains("await _lifecycle.WaitUntilLoadedAsync(cancellationToken);", main, StringComparison.Ordinal);
        Assert.Contains("_viewModel.InitializeAsync(options, cancellationToken)", main, StringComparison.Ordinal);
        Assert.Contains("_application.GetSettingsAsync(cancellationToken)", main, StringComparison.Ordinal);
        Assert.Contains("_application.GetScreenshotStorageMigrationStatusAsync(cancellationToken)", main, StringComparison.Ordinal);
        Assert.Contains("_lifecycle.Cancel();", main, StringComparison.Ordinal);
        Assert.Contains("_lifecycle.Dispose();", main, StringComparison.Ordinal);
    }

    /// <summary>Verifies that migrated top-level windows delegate native chrome, insets, and palette handling.</summary>
    [Fact]
    public void TopLevelWindows_DelegateNativeChromeInsetsAndPaletteToSharedController()
    {
        foreach (var windowName in MigratedTopLevelWindows)
        {
            var source = File.ReadAllText(RepositoryFile("TrackMeUp", $"{windowName}.xaml.cs"));

            Assert.Contains("new CustomTitleBarController(", source, StringComparison.Ordinal);
            Assert.Contains("_titleBar.Dispose();", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SetTitleBar(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("UpdateTitleBarInsets", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AppWindowTitleBar", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies that standard top-level title bars share the PNG brand mark and 48-DIP alignment.</summary>
    [Fact]
    public void StandardTopLevelTitleBars_UseSharedPngBrandMarkAndFortyEightDipAlignment()
    {
        foreach (var windowName in MigratedTopLevelWindows.Where(static name => name != "SearchWindow"))
        {
            var document = XDocument.Load(RepositoryFile("TrackMeUp", $"{windowName}.xaml"));
            var titleBar = document.Descendants().Single(element => HasName(element, "TitleBarDragRegion"));

            Assert.Equal("48", titleBar.Attribute("Height")?.Value);
            Assert.Contains(
                titleBar.Descendants(),
                element => element.Name.LocalName == "Image"
                    && element.Attribute("Style")?.Value == "{StaticResource TrackMeUpTitleBarLogoStyle}");
        }
    }

    /// <summary>Verifies that floating search opts out of tall chrome while retaining its fixed light theme.</summary>
    [Fact]
    public void FloatingSearch_PreservesCompactFixedLightChromeThroughControllerOptOut()
    {
        var document = XDocument.Load(RepositoryFile("TrackMeUp", "SearchWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "SearchWindow.xaml.cs"));
        var titleBar = document.Descendants().Single(element => HasName(element, "TitleBarDragRegion"));

        Assert.Equal("40", FirstRowHeight(document));
        Assert.Equal("40", titleBar.Attribute("Height")?.Value);
        Assert.Contains("useTallTitleBar: false", source, StringComparison.Ordinal);
        Assert.Contains("_titleBar.ApplyTheme(ElementTheme.Light);", source, StringComparison.Ordinal);
    }

    /// <summary>Verifies that owned dialogs delegate chrome without changing their established header heights.</summary>
    [Fact]
    public void OwnedDialogs_DelegateChromeWithoutChangingTheirExistingHeaderHeight()
    {
        var compactDialogs = new HashSet<string>(StringComparer.Ordinal)
        {
            "ActivityCalendarDialogWindow",
            "AiPricingDialogWindow",
            "ScreenshotStorageMigrationDialogWindow",
            "WorldClockCityPickerDialogWindow"
        };

        foreach (var windowName in MigratedDialogWindows)
        {
            var source = File.ReadAllText(RepositoryFile("TrackMeUp", $"{windowName}.xaml.cs"));
            var document = XDocument.Load(RepositoryFile("TrackMeUp", $"{windowName}.xaml"));
            var titleBar = document.Descendants().Single(element => HasName(element, "TitleDragRegion"));

            Assert.Contains("new CustomTitleBarController(", source, StringComparison.Ordinal);
            Assert.Contains("_titleBar.Dispose();", source, StringComparison.Ordinal);
            Assert.DoesNotContain("SetTitleBar(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AppWindowTitleBar", source, StringComparison.Ordinal);
            Assert.Contains(titleBar.Descendants(), element => HasName(element, "TitleBarLeftInsetColumn"));
            Assert.Contains(titleBar.Descendants(), element => HasName(element, "TitleBarRightInsetColumn"));
            Assert.Equal(compactDialogs.Contains(windowName) ? "44" : "48", FirstRowHeight(document));
            Assert.Equal(compactDialogs.Contains(windowName), source.Contains("useTallTitleBar: false", StringComparison.Ordinal));
        }
    }

    /// <summary>Verifies that window code-behind no longer owns custom title-bar setup directly.</summary>
    [Fact]
    public void WindowCodeBehind_HasNoRemainingDirectCustomTitleBarOwnership()
    {
        var windowDirectory = Path.GetDirectoryName(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"))!;
        var offenders = Directory
            .EnumerateFiles(windowDirectory, "*.xaml.cs", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("SetTitleBar(", StringComparison.Ordinal)
                    || source.Contains("ExtendsContentIntoTitleBar", StringComparison.Ordinal)
                    || source.Contains("AppWindowTitleBar", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string FirstRowHeight(XDocument document) => document
        .Descendants()
        .First(element => element.Name.LocalName == "Grid.RowDefinitions")
        .Elements()
        .First()
        .Attribute("Height")?.Value ?? string.Empty;

    private static void AssertSetter(XElement style, string property, string value) =>
        Assert.Contains(
            style.Descendants(),
            element => element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == property
                && element.Attribute("Value")?.Value == value);

    private static bool HasName(XElement element, string name) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name);

    private static string? KeyValue(XElement element) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value;

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
