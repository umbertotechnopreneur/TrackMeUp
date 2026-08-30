// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class WorldClockWindowSurfaceContractTests
{
    [Fact]
    public void Player_OpensOneIndependentWorldClockWindowInsteadOfAFlyout()
    {
        var main = XDocument.Load(RepositoryFile("TrackMeUp", "MainWindow.xaml"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var button = main.Descendants().Single(element => HasName(element, "WorldClockButton"));
        var icon = button.Descendants().Single(element => element.Name.LocalName == "SymbolIcon");

        Assert.Equal("WorldClockButton_Click", button.Attribute("Click")?.Value);
        Assert.Equal("WorldClock.OpenWindow", button.Attribute("Tag")?.Value);
        Assert.Equal("True", button.Attribute("AllowFocusOnInteraction")?.Value);
        Assert.Equal("True", button.Attribute("IsTabStop")?.Value);
        Assert.Equal("Clock", icon.Attribute("Symbol")?.Value);
        Assert.DoesNotContain(button.Descendants(), element => element.Name.LocalName == "Flyout");
        Assert.DoesNotContain(main.Descendants(), element => HasName(element, "WorldClockRail"));
        Assert.Contains("public event EventHandler? WorldClocksRequested;", mainSource, StringComparison.Ordinal);
        Assert.Contains("WorldClocksRequested?.Invoke(this, EventArgs.Empty);", mainSource, StringComparison.Ordinal);
        Assert.Contains("private WorldClockWindow? _worldClockWindow;", appSource, StringComparison.Ordinal);
        Assert.Contains("if (_worldClockWindow is not null)", appSource, StringComparison.Ordinal);
        Assert.Contains("_worldClockWindow.Activate();", appSource, StringComparison.Ordinal);
        Assert.Contains("_worldClockWindow.CloseForShutdown();", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldClockFlyout", mainSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorldClockRefreshTimer", mainSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Window_UsesMicaCompositeReferencePickerAndEqualOpenColumns()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var columnSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml.cs"));
        var root = window.Descendants().Single(element => HasName(element, "RootGrid"));
        var scroller = window.Descendants().Single(element => HasName(element, "ClockColumnsScroller"));
        var columns = window.Descendants().Single(element => HasName(element, "ClockColumnsHost"));
        var skyline = column.Descendants().Single(element => HasName(element, "SkylineImage"));
        var referenceInstant = window.Descendants().Single(element => HasName(element, "ReferenceInstantField"));
        var moreButton = window.Descendants().Single(element => HasName(element, "HeaderMenuButton"));

        Assert.Equal("BaseAlt", (string?)window.Descendants().Single(element => element.Name.LocalName == "MicaBackdrop").Attribute("Kind"));
        Assert.Equal("Transparent", root.Attribute("Background")?.Value);
        Assert.Equal("ScrollViewer", scroller.Name.LocalName);
        Assert.Equal("Enabled", scroller.Attribute("HorizontalScrollMode")?.Value);
        Assert.Equal("Auto", scroller.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", scroller.Attribute("VerticalScrollMode")?.Value);
        Assert.Equal("ClockColumnsScroller_SizeChanged", scroller.Attribute("SizeChanged")?.Value);
        Assert.Equal("Border", referenceInstant.Name.LocalName);
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceCityComboBox") && element.Name.LocalName == "ComboBox");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceDatePicker") && element.Name.LocalName == "CalendarDatePicker");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceTimePicker") && element.Name.LocalName == "TimePicker");
        Assert.Contains(window.Descendants(), element => HasName(element, "NowButton"));
        Assert.Equal("WorldClock.MoreOptions", moreButton.Attribute("Tag")?.Value);
        Assert.Equal("{StaticResource TitleBarOverflowButtonStyle}", moreButton.Attribute("Style")?.Value);
        Assert.Equal("True", moreButton.Attribute("AllowFocusOnInteraction")?.Value);
        Assert.Equal("True", moreButton.Attribute("IsTabStop")?.Value);
        Assert.Equal("HeaderMenuButton_Click", moreButton.Attribute("Click")?.Value);
        Assert.Contains("new MenuFlyout", source, StringComparison.Ordinal);
        Assert.Contains("new ToggleMenuFlyoutItem", source, StringComparison.Ordinal);
        Assert.Contains("new Style(typeof(MenuFlyoutPresenter))", source, StringComparison.Ordinal);
        Assert.Contains("SetMenuItemLabel(alwaysOnTopItem, \"WorldClock.AlwaysOnTop\");", source, StringComparison.Ordinal);
        Assert.Contains("presenter.IsAlwaysOnTop = menuItem.IsChecked;", source, StringComparison.Ordinal);
        Assert.Contains("await AddCityAsync();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetTitleBar(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExtendsContentIntoTitleBar", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NonClientRegionKind.Passthrough", source, StringComparison.Ordinal);
        Assert.Equal("Custom", AttributeValue(columns, "AutomationProperties.LandmarkType"));
        Assert.Equal("0.19", skyline.Attribute("Opacity")?.Value);
        Assert.Contains(column.Descendants(), element => element.Name.LocalName == "CelestialPhaseControl");
        Assert.Contains("new GridLength(1, GridUnitType.Star)", source, StringComparison.Ordinal);
        Assert.Contains("_refreshTimer.IsRepeating = false;", source, StringComparison.Ordinal);
        Assert.Contains("WorldClockWindowLayoutState.DelayUntilNextMinute(snapshot.InstantUtc)", source, StringComparison.Ordinal);
        Assert.Contains("WorldClockWindowLayoutState.CalculateColumnsLayout", source, StringComparison.Ordinal);
        Assert.Contains("BorderThickness = index < clocks.Count - 1", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.WorldClocks", source, StringComparison.Ordinal);
        Assert.Contains("GetWorldClocksAsync", source, StringComparison.Ordinal);
        Assert.Contains("ConvertWorldClocksAsync", source, StringComparison.Ordinal);
        Assert.Contains("AddWorldClockAsync", source, StringComparison.Ordinal);
        Assert.Contains("RemoveWorldClockAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", source, StringComparison.Ordinal);
        Assert.Contains("clock.LocalTime.ToString(\"HH:mm\", strings.Culture)", columnSource, StringComparison.Ordinal);
        Assert.Contains("FormatOffset(clock.LocalTime.Offset - referenceClock.LocalTime.Offset)", columnSource, StringComparison.Ordinal);
        Assert.Contains("!normalized.EndsWith(\".png\", StringComparison.OrdinalIgnoreCase)", columnSource, StringComparison.Ordinal);
        Assert.Contains("!double.IsFinite(clock.MoonPhaseAngleDegrees)", columnSource, StringComparison.Ordinal);
    }

    [Fact]
    public void OpeningFailure_IsVisibleAndLocalizedInEveryCatalog()
    {
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var readme = File.ReadAllText(RepositoryFile("README.md"));
        var localizationDirectory = RepositoryFile("TrackMeUp.Core", "Localization");
        var catalogs = Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly);

        Assert.Contains("_window.ShowWorldClockOpenFailure();", appSource, StringComparison.Ordinal);
        Assert.Contains("internal void ShowWorldClockOpenFailure()", mainSource, StringComparison.Ordinal);
        Assert.Contains("T(\"WorldClock.OpenFailed\")", mainSource, StringComparison.Ordinal);
        Assert.Contains("the Mica backdrop remains visible", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("the Acrylic backdrop remains visible", readme, StringComparison.Ordinal);
        Assert.Equal(10, catalogs.Length);
        Assert.All(catalogs, catalog =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            Assert.True(document.RootElement.TryGetProperty("WorldClock.OpenFailed", out var message), catalog);
            Assert.False(string.IsNullOrWhiteSpace(message.GetString()), catalog);
        });
    }

    [Fact]
    public void FreshCurrentWeather_IsRenderedAsLocalizedTextOnly()
    {
        string[] conditionKeys =
        [
            "clear",
            "cloudy",
            "rain",
            "snow",
            "mixed-precipitation",
            "fog",
            "lightning"
        ];
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml.cs"));
        var weatherPanel = column.Descendants().Single(element => HasName(element, "WeatherPanel"));
        var localizationDirectory = RepositoryFile("TrackMeUp.Core", "Localization");
        var catalogs = Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly);
        var requiredLocalizationKeys = conditionKeys
            .Select(condition => $"WorldClock.WeatherCondition.{condition}")
            .Prepend("WorldClock.WeatherTemperature")
            .ToArray();

        Assert.Equal("StackPanel", weatherPanel.Name.LocalName);
        Assert.Equal("5", AttributeValue(weatherPanel, "Grid.Row"));
        Assert.Equal("Collapsed", weatherPanel.Attribute("Visibility")?.Value);
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherTemperatureText"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherConditionText"));
        Assert.DoesNotContain(weatherPanel.Descendants(), element => element.Name.LocalName is "Button" or "FontIcon" or "SymbolIcon");
        Assert.Contains("weather is null || !weather.IsFresh", source, StringComparison.Ordinal);
        Assert.Contains("WeatherPanel.Visibility = Visibility.Visible;", source, StringComparison.Ordinal);
        Assert.Contains("WeatherPanel.Visibility = Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("weather.TemperatureCelsius", source, StringComparison.Ordinal);
        Assert.All(conditionKeys, condition =>
            Assert.Contains($"\"{condition}\" => \"WorldClock.WeatherCondition.{condition}\"", source, StringComparison.Ordinal));
        Assert.DoesNotContain("HttpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("openweather", source, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10, catalogs.Length);
        Assert.All(catalogs, catalog =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            Assert.All(requiredLocalizationKeys, key =>
            {
                Assert.True(document.RootElement.TryGetProperty(key, out var value), $"{catalog}: {key}");
                Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{catalog}: {key}");
            });
        });
    }

    [Fact]
    public void WeatherStatusAndRequiredOpenWeatherAttribution_AreVisibleOnlyWhenApplicable()
    {
        string[] statusKeys =
        [
            "WorldClock.WeatherStatus.Disabled",
            "WorldClock.WeatherStatus.Unavailable",
            "WorldClock.WeatherStatus.Partial",
            "WorldClock.WeatherStatus.ReferenceInstant"
        ];
        string[] obsoleteKeys =
        [
            "WorldClock.IlluminationCompact",
            "WorldClock.Detail",
            "WorldClock.Sun",
            "WorldClock.MoonWithPhase",
            "WorldClock.Moonrise",
            "WorldClock.Moonset"
        ];
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var applicationSource = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "TrackMeUpApplication.cs"));
        var status = window.Descendants().Single(element => HasName(element, "WeatherStatusText"));
        var attribution = window.Descendants().Single(element => HasName(element, "WeatherAttributionButton"));
        var attributionText = window.Descendants().Single(element => HasName(element, "WeatherAttributionText"));
        var logo = attribution.Descendants().Single(element => HasName(element, "WeatherAttributionLogo"));
        var logoPath = RepositoryFile(
            "TrackMeUp",
            "Assets",
            "WorldClocks",
            "ThirdParty",
            "OpenWeather",
            "ow_logo.svg");
        var localizationDirectory = RepositoryFile("TrackMeUp.Core", "Localization");
        var catalogs = Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly);

        Assert.Equal("Collapsed", status.Attribute("Visibility")?.Value);
        Assert.Equal("Polite", AttributeValue(status, "AutomationProperties.LiveSetting"));
        Assert.Equal("HyperlinkButton", attribution.Name.LocalName);
        Assert.Equal("Collapsed", attribution.Attribute("Visibility")?.Value);
        Assert.Equal("WeatherAttributionButton_Click", attribution.Attribute("Click")?.Value);
        Assert.Equal("WorldClock.WeatherAttribution", attribution.Attribute("Tag")?.Value);
        Assert.Equal("Weather data provided by OpenWeather", attributionText.Attribute("Text")?.Value);
        Assert.Null(logo.Element(logo.Name.Namespace + "Image.Source"));
        Assert.Equal(
            "fd0ad613ebcdb5f013df98bf75603c83fe1f3f0a5f677118b99557da8ac9281c",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(logoPath))).ToLowerInvariant());

        Assert.Contains("\"available\" => null", source, StringComparison.Ordinal);
        Assert.Contains("\"disabled\" => \"WorldClock.WeatherStatus.Disabled\"", source, StringComparison.Ordinal);
        Assert.Contains("\"partial\" => \"WorldClock.WeatherStatus.Partial\"", source, StringComparison.Ordinal);
        Assert.Contains("\"unavailable\" => \"WorldClock.WeatherStatus.Unavailable\"", source, StringComparison.Ordinal);
        Assert.Contains("status.ReasonCode == \"explicit-instant\"", source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(snapshot.WeatherStatus.Provider, \"openweather\"", source, StringComparison.Ordinal);
        Assert.Contains("clock.Weather is { IsFresh: true }", source, StringComparison.Ordinal);
        Assert.Contains("WeatherAttributionButton.Visibility = displaysOpenWeatherObservation", source, StringComparison.Ordinal);
        Assert.Contains("private void EnsureWeatherAttributionLogo()", source, StringComparison.Ordinal);
        Assert.Contains("RasterizePixelWidth = 168", source, StringComparison.Ordinal);
        Assert.Contains("source.OpenFailed += WeatherAttributionLogo_OpenFailed;", source, StringComparison.Ordinal);
        Assert.Contains("source.UriSource = new Uri(\"ms-appx:///Assets/WorldClocks/ThirdParty/OpenWeather/ow_logo.svg\");", source, StringComparison.Ordinal);
        Assert.Contains("OpenProductLinkAsync(", source, StringComparison.Ordinal);
        Assert.Contains("\"openweather\"", source, StringComparison.Ordinal);
        Assert.Equal(2, source.Split("ShowFailure(\"About.LinkFailed\")", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("ShowFailure(\"ProductLinkUnavailable\")", source, StringComparison.Ordinal);
        Assert.Contains("https://openweathermap.org/", applicationSource, StringComparison.Ordinal);
        Assert.Contains("\"openweather\" => OpenWeatherUrl", applicationSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(WeatherAttributionButton, weatherAttribution);", source, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(WeatherAttributionButton, weatherAttribution);", source, StringComparison.Ordinal);

        Assert.Equal(10, catalogs.Length);
        Assert.All(catalogs, catalog =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            Assert.All(statusKeys, key =>
            {
                Assert.True(document.RootElement.TryGetProperty(key, out var value), $"{catalog}: {key}");
                Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{catalog}: {key}");
            });
            Assert.Equal(
                "Weather data provided by OpenWeather",
                document.RootElement.GetProperty("WorldClock.WeatherAttribution").GetString());
            Assert.False(string.IsNullOrWhiteSpace(
                document.RootElement.GetProperty("About.LinkFailed").GetString()));
            Assert.All(obsoleteKeys, key => Assert.False(document.RootElement.TryGetProperty(key, out _), $"{catalog}: {key}"));
            Assert.DoesNotContain(
                document.RootElement.EnumerateObject(),
                property => property.Name.StartsWith("WorldClock.MoonPhase.", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void FailedLiveConversion_RestoresTheLiveSnapshotBeforePublishingTheFailure()
    {
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));

        Assert.Contains("private bool _pendingLiveConversion;", source, StringComparison.Ordinal);
        Assert.Contains("var transitionStartedFromLive = _pendingLiveConversion;", source, StringComparison.Ordinal);
        Assert.Contains("HandleConversionFailure(transitionStartedFromLive, result.MessageKey);", source, StringComparison.Ordinal);
        Assert.Contains("WorldClockWindowLayoutState.ResolveConversionFailure(transitionStartedFromLive)", source, StringComparison.Ordinal);
        Assert.Contains("if (state.RestoreLastSnapshotControls && _snapshot is not null)", source, StringComparison.Ordinal);

        var handlerStart = source.IndexOf("private void HandleConversionFailure", StringComparison.Ordinal);
        var restore = source.IndexOf("ApplySnapshot(_snapshot);", handlerStart, StringComparison.Ordinal);
        var failure = source.IndexOf("ShowFailure(messageKey);", handlerStart, StringComparison.Ordinal);
        var nowHandler = source.IndexOf("private async void NowButton_Click", StringComparison.Ordinal);
        var clearPending = source.IndexOf("_pendingLiveConversion = false;", nowHandler, StringComparison.Ordinal);
        var restoreLive = source.IndexOf("_isLive = true;", clearPending, StringComparison.Ordinal);
        Assert.True(handlerStart >= 0 && restore > handlerStart && failure > restore);
        Assert.True(nowHandler >= 0 && clearPending > nowHandler && restoreLive > clearPending);
    }

    [Fact]
    public void IconOnlyCommands_HaveMatchingAccessibleNamesAndTooltips()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var buttons = window.Descendants()
            .Concat(column.Descendants())
            .Where(element => element.Name.LocalName == "Button" && element.Descendants().Any(child => child.Name.LocalName is "SymbolIcon" or "FontIcon"))
            .ToArray();

        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.Equal(
            AttributeValue(button, "AutomationProperties.Name"),
            AttributeValue(button, "ToolTipService.ToolTip")));
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
        Assert.Equal("CityComboBox_SelectionChanged", comboBox.Attribute("SelectionChanged")?.Value);
        Assert.Contains("CityComboBox.ItemsSource = _options;", source, StringComparison.Ordinal);
        Assert.Contains("WindowStateKeys.WorldClockCityPicker", source, StringComparison.Ordinal);
        Assert.Contains("await _placement.TrySaveForCloseAsync(CancellationToken.None);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CelestialPhaseControl_UsesPackagedSunAndMoonAssets()
    {
        var celestial = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "CelestialPhaseControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "CelestialPhaseControl.xaml.cs"));
        var project = XDocument.Load(RepositoryFile("TrackMeUp", "TrackMeUp.csproj"));
        var assetSources = celestial.Descendants()
            .Where(element => element.Name.LocalName is "Image" or "ImageBrush")
            .Select(element => AttributeValue(element, element.Name.LocalName == "Image" ? "Source" : "ImageSource"))
            .ToArray();
        var celestialContent = project.Descendants().Single(element =>
            element.Name.LocalName == "Content" && element.Attribute("Include")?.Value == @"Assets\Celestial\*.png");

        Assert.Equal(
            ["ms-appx:///Assets/Celestial/sun-premium.png", "ms-appx:///Assets/Celestial/moon-premium.png"],
            assetSources);
        Assert.Equal("PreserveNewest", celestialContent.Elements().Single(element => element.Name.LocalName == "CopyToOutputDirectory").Value);
        Assert.Contains("MoonShadow.Data = BuildMoonShadow", source, StringComparison.Ordinal);
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
