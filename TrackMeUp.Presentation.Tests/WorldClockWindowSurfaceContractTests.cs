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
    public void Window_UsesDesktopAcrylicIntegratedHeaderOptionsSurfaceAndThemeAwareCompositing()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var columnSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml.cs"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml.cs"));
        var root = window.Descendants().Single(element => HasName(element, "RootGrid"));
        var scroller = window.Descendants().Single(element => HasName(element, "ClockColumnsScroller"));
        var columns = window.Descendants().Single(element => HasName(element, "ClockColumnsHost"));
        var clocksSurface = window.Descendants().Single(element => HasName(element, "ClocksSurface"));
        var optionsPanel = window.Descendants().Single(element => HasName(element, "OptionsPanel"));
        var optionsHost = window.Descendants().Single(element => HasName(element, "OptionsHost"));
        var header = window.Descendants().Single(element => HasName(element, "HeaderDragRegion"));
        var skyline = column.Descendants().Single(element => HasName(element, "SkylineImage"));
        var referenceInstant = window.Descendants().Single(element => HasName(element, "ReferenceInstantButton"));
        var optionsButton = window.Descendants().Single(element => HasName(element, "OptionsButton"));
        var backButton = window.Descendants().Single(element => HasName(element, "HeaderBackButton"));
        var optionsHeaderLabel = window.Descendants().Single(element => HasName(element, "OptionsHeaderLabel"));
        var weatherProviderLink = options.Descendants().Single(element => HasName(element, "WeatherProviderLinkButton"));
        var scene = column.Descendants().Single(element => HasName(element, "SceneGrid"));
        var sceneChildren = scene.Elements().ToArray();
        var windowThemeDictionaries = window.Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary" && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"))
            .ToArray();
        var columnThemeDictionaries = column.Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary" && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"))
            .ToArray();

        Assert.Contains("SystemBackdrop = new DesktopAcrylicBackdrop();", source, StringComparison.Ordinal);
        Assert.DoesNotContain(window.Descendants(), element => element.Name.LocalName == "MicaBackdrop");
        Assert.DoesNotContain("MicaBackdrop", source, StringComparison.Ordinal);
        Assert.Equal("Transparent", root.Attribute("Background")?.Value);
        Assert.Equal("{ThemeResource WorldClockHeaderTintBrush}", header.Attribute("Background")?.Value);
        Assert.Equal(["Dark", "HighContrast", "Light"], windowThemeDictionaries.Select(KeyValue).OrderBy(value => value).ToArray());
        Assert.Equal(["Dark", "HighContrast", "Light"], columnThemeDictionaries.Select(KeyValue).OrderBy(value => value).ToArray());
        Assert.All(windowThemeDictionaries, dictionary =>
        {
            Assert.Contains(dictionary.Descendants(), element => KeyValueOrNull(element) == "WorldClockWindowTintBrush");
            Assert.Contains(dictionary.Descendants(), element => KeyValueOrNull(element) == "WorldClockHeaderTintBrush");
            Assert.Contains(dictionary.Descendants(), element => KeyValueOrNull(element) == "WorldClockReferenceFieldBrush");
        });
        Assert.All(columnThemeDictionaries, dictionary =>
        {
            Assert.Contains(dictionary.Descendants(), element => KeyValueOrNull(element) == "WorldClockBackdropOpacity");
            Assert.Contains(dictionary.Descendants(), element => KeyValueOrNull(element) == "WorldClockSkylineOpacity");
            Assert.Contains(dictionary.Descendants(), element => KeyValueOrNull(element) == "WorldClockForegroundOpacity");
            Assert.Contains(dictionary.Descendants(), element => KeyValueOrNull(element) == "WorldClockSceneFadeBrush");
        });
        Assert.Equal("ScrollViewer", scroller.Name.LocalName);
        Assert.Equal("Enabled", scroller.Attribute("HorizontalScrollMode")?.Value);
        Assert.Equal("Auto", scroller.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", scroller.Attribute("VerticalScrollMode")?.Value);
        Assert.Equal("ClockColumnsScroller_SizeChanged", scroller.Attribute("SizeChanged")?.Value);
        Assert.Equal("DropDownButton", referenceInstant.Name.LocalName);
        Assert.Contains(referenceInstant.Descendants(), element => element.Name.LocalName == "Flyout");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceCityComboBox") && element.Name.LocalName == "ComboBox");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceDatePicker") && element.Name.LocalName == "CalendarDatePicker");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceTimePicker") && element.Name.LocalName == "TimePicker");
        Assert.Contains(window.Descendants(), element => HasName(element, "NowButton"));
        Assert.Equal("WorldClock.Options.Open", optionsButton.Attribute("Tag")?.Value);
        Assert.Equal("{StaticResource TitleBarOverflowButtonStyle}", optionsButton.Attribute("Style")?.Value);
        Assert.Equal("OptionsButton_Click", optionsButton.Attribute("Click")?.Value);
        Assert.Contains(optionsButton.Descendants(), element => element.Name.LocalName == "SymbolIcon" && element.Attribute("Symbol")?.Value == "More");
        Assert.Equal("WorldClock.Options.Back", backButton.Attribute("Tag")?.Value);
        Assert.Equal("HeaderBackButton_Click", backButton.Attribute("Click")?.Value);
        Assert.Equal("Collapsed", backButton.Attribute("Visibility")?.Value);
        Assert.Equal("Collapsed", optionsHeaderLabel.Attribute("Visibility")?.Value);
        Assert.Null(clocksSurface.Attribute("Visibility"));
        Assert.Equal("Collapsed", optionsPanel.Attribute("Visibility")?.Value);
        Assert.Equal("{ThemeResource WorldClockOptionsOverlayBrush}", optionsPanel.Attribute("Background")?.Value);
        Assert.Equal("ContentPresenter", optionsHost.Name.LocalName);
        Assert.Contains("ShowOptionsSurface()", source, StringComparison.Ordinal);
        Assert.Contains("ClocksSurface.IsHitTestVisible = false;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ClocksSurface.Visibility = Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("OptionsPanel.Visibility = Visibility.Visible;", source, StringComparison.Ordinal);
        Assert.Contains("_layoutState.ShowSurface(WorldClockWindowSurface.Options);", source, StringComparison.Ordinal);
        Assert.Contains("HeaderBackButton.Focus(FocusState.Programmatic)", source, StringComparison.Ordinal);
        Assert.Contains("OptionsHeaderLabel.Visibility = optionsVisible ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("OptionsPanel.Visibility = Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("ClocksSurface.Visibility = Visibility.Visible;", source, StringComparison.Ordinal);
        Assert.Contains("ClocksSurface.IsHitTestVisible = true;", source, StringComparison.Ordinal);
        Assert.Contains("_layoutState.ShowSurface(WorldClockWindowSurface.Clocks);", source, StringComparison.Ordinal);
        Assert.Contains("OptionsButton.Focus(FocusState.Programmatic)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new MenuFlyout", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleMenuFlyoutItem", source, StringComparison.Ordinal);
        Assert.Contains("SystemBackdrop = new DesktopAcrylicBackdrop();", source, StringComparison.Ordinal);
        Assert.Contains("ExtendsContentIntoTitleBar = true;", source, StringComparison.Ordinal);
        Assert.Contains("SetTitleBar(HeaderDragRegion);", source, StringComparison.Ordinal);
        Assert.Contains("NonClientRegionKind.Passthrough", source, StringComparison.Ordinal);
        Assert.Contains("presenter.IsAlwaysOnTop = alwaysOnTop;", source, StringComparison.Ordinal);
        Assert.Contains("await AddCityAsync();", source, StringComparison.Ordinal);
        Assert.Contains(options.Descendants(), element => HasName(element, "WeatherEnabledSwitch") && element.Attribute("Tag")?.Value == "WorldClock.Options.Weather");
        Assert.Contains(options.Descendants(), element => HasName(element, "WeatherEnabledSwitch") && element.Attribute("IsOn")?.Value == "True");
        Assert.Contains(options.Descendants(), element => HasName(element, "WeatherApiKeyBox") && element.Name.LocalName == "PasswordBox");
        Assert.Contains(options.Descendants(), element => HasName(element, "AlwaysOnTopSwitch") && element.Attribute("Tag")?.Value == "WorldClock.Options.AlwaysOnTop");
        Assert.Contains(options.Descendants(), element => HasName(element, "CitiesHost"));
        Assert.Contains(options.Descendants(), element => HasName(element, "AddClockButton") && element.Attribute("Tag")?.Value == "WorldClock.Add");
        Assert.Equal("WorldClock.Options.Weather.ProviderLink", weatherProviderLink.Attribute("Tag")?.Value);
        Assert.Equal("WeatherProviderLinkButton_Click", weatherProviderLink.Attribute("Click")?.Value);
        Assert.Contains(options.Descendants(), element => element.Name.LocalName == "AdaptiveTrigger" && element.Attribute("MinWindowWidth")?.Value == "760");
        Assert.Contains("PatchSettingsAsync(", optionsSource, StringComparison.Ordinal);
        Assert.Contains("[\"world_clocks.weather.enabled\"]", optionsSource, StringComparison.Ordinal);
        Assert.Contains("SetWorldClockWeatherKeyAsync(secret", optionsSource, StringComparison.Ordinal);
        Assert.Contains("WeatherApiKeyBox.Password = string.Empty;", optionsSource, StringComparison.Ordinal);
        Assert.Contains("public event EventHandler? ProviderLinkRequested;", optionsSource, StringComparison.Ordinal);
        Assert.Contains("ProviderLinkRequested?.Invoke(this, EventArgs.Empty);", optionsSource, StringComparison.Ordinal);
        Assert.Contains("options.ProviderLinkRequested += OptionsControl_ProviderLinkRequested;", source, StringComparison.Ordinal);
        Assert.Contains("OpenProductLinkAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Environment.", optionsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", optionsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", optionsSource, StringComparison.Ordinal);
        Assert.Equal("Custom", AttributeValue(columns, "AutomationProperties.LandmarkType"));
        Assert.Equal("{ThemeResource WorldClockSkylineOpacity}", skyline.Attribute("Opacity")?.Value);
        Assert.Equal("Uniform", skyline.Attribute("Stretch")?.Value);
        Assert.Contains(column.Descendants(), element => element.Name.LocalName == "CelestialPhaseControl");
        Assert.Contains(column.Descendants(), element => HasName(element, "WeatherAdornmentHost"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SunriseLabelText"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SunsetLabelText"));
        Assert.True(IndexOfName(sceneChildren, "BackdropLayerHost") < IndexOfName(sceneChildren, "SkylineImage"));
        Assert.True(IndexOfName(sceneChildren, "SkylineImage") < IndexOfName(sceneChildren, "CelestialPhase"));
        Assert.True(IndexOfName(sceneChildren, "CelestialPhase") < IndexOfName(sceneChildren, "ForegroundLayerHost"));
        Assert.True(IndexOfName(sceneChildren, "ForegroundLayerHost") < IndexOfName(sceneChildren, "WeatherPanel"));
        Assert.DoesNotContain(column.Descendants(), element => HasName(element, "ReferenceButton") || HasName(element, "RemoveButton"));
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
        Assert.Contains("strings.Translate(\"WorldClock.LocalTime\")", columnSource, StringComparison.Ordinal);
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
        Assert.Contains("_dialogs.ShowWarningBanner(", mainSource, StringComparison.Ordinal);
        Assert.Contains("T(\"WorldClock.OpenFailed\")", mainSource, StringComparison.Ordinal);
        Assert.Contains("the Desktop Acrylic backdrop remains visible", readme, StringComparison.Ordinal);
        Assert.Contains("a full options layer appears over the clock canvas", readme, StringComparison.Ordinal);
        Assert.Contains("current weather is enabled by default", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("the Mica backdrop remains visible", readme, StringComparison.Ordinal);
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
        var scene = column.Descendants().Single(element => HasName(element, "SceneGrid"));
        var localizationDirectory = RepositoryFile("TrackMeUp.Core", "Localization");
        var catalogs = Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly);
        var requiredLocalizationKeys = conditionKeys
            .Select(condition => $"WorldClock.WeatherCondition.{condition}")
            .Prepend("WorldClock.WeatherTemperature")
            .Prepend("WorldClock.WeatherNoData")
            .Prepend("WorldClock.SunsetLabel")
            .Prepend("WorldClock.SunriseLabel")
            .ToArray();

        Assert.Equal("StackPanel", weatherPanel.Name.LocalName);
        Assert.Same(scene, weatherPanel.Parent);
        Assert.Equal("Center", weatherPanel.Attribute("VerticalAlignment")?.Value);
        Assert.Null(weatherPanel.Attribute("Visibility"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherTemperatureText"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherConditionText"));
        Assert.DoesNotContain(weatherPanel.Descendants(), element => element.Name.LocalName is "Button" or "FontIcon" or "SymbolIcon");
        Assert.Contains("weather is null || !weather.IsFresh", source, StringComparison.Ordinal);
        Assert.Contains("WeatherTemperatureText.Text = \"—\";", source, StringComparison.Ordinal);
        Assert.Contains("strings.Translate(\"WorldClock.WeatherNoData\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WeatherPanel.Visibility", source, StringComparison.Ordinal);
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
    public void WeatherStatus_IsExplicitInOptionsAndOpenWeatherAttributionAppearsOnlyForFreshObservations()
    {
        string[] statusKeys =
        [
            "WorldClock.WeatherStatus.Disabled",
            "WorldClock.WeatherStatus.ConfigurationRequired",
            "WorldClock.WeatherStatus.Unavailable",
            "WorldClock.WeatherStatus.Partial",
            "WorldClock.WeatherStatus.ReferenceInstant",
            "WorldClock.Options.Weather.ApiKeyStatus.Ready",
            "WorldClock.Options.Weather.ApiKeyStatus.Missing",
            "WorldClock.Options.Weather.ApiKeyStatus.Invalid",
            "WorldClock.Options.Weather.ApiKeyStatus.Unavailable"
        ];
        string[] obsoleteKeys =
        [
            "WorldClock.MoreOptions",
            "WorldClock.AlwaysOnTop",
            "WorldClock.IlluminationCompact",
            "WorldClock.Detail",
            "WorldClock.Sun",
            "WorldClock.MoonWithPhase",
            "WorldClock.Moonrise",
            "WorldClock.Moonset"
        ];
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml.cs"));
        var applicationSource = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "TrackMeUpApplication.cs"));
        var status = options.Descendants().Single(element => HasName(element, "WeatherStatusText"));
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

        Assert.Equal("Polite", AttributeValue(status, "AutomationProperties.LiveSetting"));
        Assert.DoesNotContain(window.Descendants(), element => HasName(element, "WeatherStatusText"));
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
        Assert.Contains("\"disabled\" when status.ReasonCode == \"user-disabled\" => null", source, StringComparison.Ordinal);
        Assert.Contains("\"configuration-required\" when status.ReasonCode == \"missing-api-key\" => null", source, StringComparison.Ordinal);
        Assert.Contains("\"configuration-required\" when status.ReasonCode == \"invalid-api-key\" => null", source, StringComparison.Ordinal);
        Assert.Contains("\"partial\" => \"WorldClock.WeatherStatus.Partial\"", source, StringComparison.Ordinal);
        Assert.Contains("\"unavailable\" => \"WorldClock.WeatherStatus.Unavailable\"", source, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowWarningBanner(", source, StringComparison.Ordinal);
        Assert.Contains("(\"available\", _) => (\"WorldClock.Options.Weather.ApiKeyStatus.Ready\", \"WeatherStatusReady\")", optionsSource, StringComparison.Ordinal);
        Assert.Contains("(\"WorldClock.Options.Weather.ApiKeyStatus.Missing\", \"WeatherStatusNeedsAttention\")", optionsSource, StringComparison.Ordinal);
        Assert.Contains("(\"WorldClock.Options.Weather.ApiKeyStatus.Invalid\", \"WeatherStatusInvalid\")", optionsSource, StringComparison.Ordinal);
        Assert.Contains("(\"disabled\", \"user-disabled\") => (\"WorldClock.WeatherStatus.Disabled\", \"WeatherStatusInformational\")", optionsSource, StringComparison.Ordinal);
        Assert.Contains("(\"WorldClock.WeatherStatus.ReferenceInstant\", \"WeatherStatusInformational\")", optionsSource, StringComparison.Ordinal);
        Assert.Contains("WeatherStatusText.Text = T(presentation.Key);", optionsSource, StringComparison.Ordinal);
        Assert.Contains("VisualStateManager.GoToState(this, presentation.VisualState, false);", optionsSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(WeatherStatusText, WeatherStatusText.Text);", optionsSource, StringComparison.Ordinal);
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
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml"));
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml.cs"));
        var buttons = window.Descendants()
            .Concat(column.Descendants())
            .Concat(options.Descendants())
            .Where(element =>
                element.Name.LocalName == "Button"
                && element.Descendants().Any(child => child.Name.LocalName is "SymbolIcon" or "FontIcon")
                && !element.Descendants().Any(child => child.Name.LocalName == "TextBlock"))
            .ToArray();

        Assert.NotEmpty(buttons);
        Assert.All(buttons, button =>
        {
            var accessibleName = AttributeValue(button, "AutomationProperties.Name");
            Assert.False(string.IsNullOrWhiteSpace(accessibleName));
            Assert.Equal(accessibleName, AttributeValue(button, "ToolTipService.ToolTip"));
            Assert.StartsWith("WorldClock.", AttributeValue(button, "Tag"), StringComparison.Ordinal);
        });
        Assert.Contains("SetIconButtonLabel(OptionsButton, \"WorldClock.Options.Open\");", windowSource, StringComparison.Ordinal);
        Assert.Contains("SetIconButtonLabel(HeaderBackButton, \"WorldClock.Options.Back\");", windowSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(button, label);", windowSource, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(button, label);", windowSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(referenceButton, referenceName);", optionsSource, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(referenceButton, referenceName);", optionsSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(removeButton, removeName);", optionsSource, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(removeButton, removeName);", optionsSource, StringComparison.Ordinal);
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

    private static string KeyValue(XElement element) =>
        element.Attributes().Single(attribute => attribute.Name.LocalName == "Key").Value;

    private static string? KeyValueOrNull(XElement element) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value;

    private static int IndexOfName(XElement[] elements, string name)
    {
        var index = Array.FindIndex(elements, element => HasName(element, name));
        return index >= 0
            ? index
            : throw new InvalidDataException($"Expected '{name}' in the compositing stack.");
    }

    private static string AttributeValue(XElement element, string localName) =>
        element.Attributes().Single(attribute => attribute.Name.LocalName == localName).Value;
}
