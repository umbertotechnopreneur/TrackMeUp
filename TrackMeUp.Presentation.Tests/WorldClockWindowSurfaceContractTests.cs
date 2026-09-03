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

    /// <summary>Verifies the acrylic window, integrated header, options layer, and theme-aware clock compositing contract.</summary>
    [Fact]
    public void Window_UsesDesktopAcrylicIntegratedHeaderOptionsSurfaceAndThemeAwareCompositing()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml"));
        var titleBarStyles = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "TitleBarOverflowButtonStyles.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var titleBarSource = File.ReadAllText(RepositoryFile("TrackMeUp", "CustomTitleBarController.cs"));
        var columnSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml.cs"));
        var optionsSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml.cs"));
        var root = window.Descendants().Single(element => HasName(element, "RootGrid"));
        var scroller = window.Descendants().Single(element => HasName(element, "ClockColumnsScroller"));
        var columns = window.Descendants().Single(element => HasName(element, "ClockColumnsHost"));
        var clocksSurface = window.Descendants().Single(element => HasName(element, "ClocksSurface"));
        var optionsPanel = window.Descendants().Single(element => HasName(element, "OptionsPanel"));
        var optionsHost = window.Descendants().Single(element => HasName(element, "OptionsHost"));
        var header = window.Descendants().Single(element => HasName(element, "HeaderDragRegion"));
        var titleBarLogo = window.Descendants().Single(element => HasName(element, "TitleBarLogo"));
        var titleBarIdentityHost = window.Descendants().Single(element => HasName(element, "TitleBarIdentityHost"));
        var titleBarOptionsHost = window.Descendants().Single(element => HasName(element, "TitleBarOptionsHost"));
        var titleBarSystemButtonGap = window.Descendants().Single(element => HasName(element, "TitleBarSystemButtonGapColumn"));
        var skyline = column.Descendants().Single(element => HasName(element, "SkylineImage"));
        var cityName = column.Descendants().Single(element => HasName(element, "CityNameText"));
        var dateRelation = column.Descendants().Single(element => HasName(element, "DateRelationText"));
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
        Assert.Equal("48", root.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions").Elements().First().Attribute("Height")?.Value);
        Assert.Equal(["Auto", "*", "Auto"], header.Elements().Single(element => element.Name.LocalName == "Grid.ColumnDefinitions").Elements().Select(element => element.Attribute("Width")?.Value ?? string.Empty).ToArray());
        Assert.Equal("0", titleBarIdentityHost.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", titleBarOptionsHost.Attribute("Grid.Column")?.Value);
        Assert.Equal("12", titleBarSystemButtonGap.Attribute("Width")?.Value);
        Assert.Equal("{StaticResource TrackMeUpTitleBarLogoStyle}", titleBarLogo.Attribute("Style")?.Value);
        var titleBarLogoStyle = titleBarStyles.Descendants().Single(element => KeyValueOrNull(element) == "TrackMeUpTitleBarLogoStyle");
        Assert.Contains(titleBarLogoStyle.Descendants(), element => element.Attribute("Property")?.Value == "Width" && element.Attribute("Value")?.Value == "22");
        Assert.Contains(titleBarLogoStyle.Descendants(), element => element.Attribute("Property")?.Value == "Height" && element.Attribute("Value")?.Value == "22");
        Assert.Contains(titleBarLogoStyle.Descendants(), element => element.Attribute("Property")?.Value == "Source" && element.Attribute("Value")?.Value == "ms-appx:///Assets/TrackMeUpSquare44Logo.png");
        var columnRows = cityName.Parent?.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions").Elements().ToArray() ?? [];
        Assert.Equal("Auto", columnRows[0].Attribute("Height")?.Value);
        Assert.Equal("Auto", columnRows[3].Attribute("Height")?.Value);
        Assert.True(HasName(dateRelation.Parent!, "TimeInfo"));
        Assert.Equal("0", dateRelation.Attribute("Margin")?.Value);
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
        Assert.Equal("Enabled", scroller.Attribute("VerticalScrollMode")?.Value);
        Assert.Equal("ClockColumnsScroller_SizeChanged", scroller.Attribute("SizeChanged")?.Value);
        Assert.Equal("DropDownButton", referenceInstant.Name.LocalName);
        Assert.Equal("40", referenceInstant.Attribute("Height")?.Value);
        Assert.Equal("1", referenceInstant.Attribute("Grid.Column")?.Value);
        Assert.Null(referenceInstant.Attribute("Grid.ColumnSpan"));
        Assert.Equal("Stretch", referenceInstant.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Center", referenceInstant.Attribute("VerticalAlignment")?.Value);
        Assert.Contains(referenceInstant.Descendants(), element => element.Name.LocalName == "Flyout");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceCityComboBox") && element.Name.LocalName == "ComboBox");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceDatePicker") && element.Name.LocalName == "CalendarDatePicker");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceTimePicker") && element.Name.LocalName == "TimePicker");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceInstantFlyoutTitle") && element.Attribute("Tag")?.Value == "WorldClock.ReferenceInstant.Title");
        Assert.Contains(window.Descendants(), element => HasName(element, "ReferenceTimeZoneText"));
        Assert.Contains(window.Descendants(), element => HasName(element, "NowButton"));
        Assert.Contains(window.Descendants(), element => HasName(element, "ApplyReferenceButton")
            && element.Attribute("Tag")?.Value == "WorldClock.Apply"
            && element.Attribute("Click")?.Value == "ApplyReferenceButton_Click");
        Assert.Contains("ReferenceTimeZoneText.Text", source, StringComparison.Ordinal);
        Assert.Contains("ApplyReferenceButton_Click", source, StringComparison.Ordinal);
        Assert.Equal("WorldClock.Options.Open", optionsButton.Attribute("Tag")?.Value);
        Assert.Equal("{StaticResource TrackMeUpTitleBarCommandButtonStyle}", optionsButton.Attribute("Style")?.Value);
        Assert.Equal("OptionsButton_Click", optionsButton.Attribute("Click")?.Value);
        Assert.Contains(optionsButton.Descendants(), element => element.Name.LocalName == "SymbolIcon" && element.Attribute("Symbol")?.Value == "More");
        Assert.Equal("WorldClock.Options.Back", backButton.Attribute("Tag")?.Value);
        Assert.Equal("{StaticResource TrackMeUpTitleBarCommandButtonStyle}", backButton.Attribute("Style")?.Value);
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
        Assert.Contains("_titleBar = new CustomTitleBarController(", source, StringComparison.Ordinal);
        Assert.Contains("() => [HeaderBackButton, ReferenceInstantButton, PresentationModeButton, OptionsButton]", source, StringComparison.Ordinal);
        Assert.Contains("TitleBarLogo.Visibility = optionsVisible ? Visibility.Collapsed : Visibility.Visible;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InputNonClientPointerSource", source, StringComparison.Ordinal);
        Assert.Contains("_window.ExtendsContentIntoTitleBar = true;", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("_window.SetTitleBar(_dragRegion);", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("PreferredHeightOption = TitleBarHeightOption.Tall;", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("titleBar.ButtonHoverForegroundColor = palette.HoverForeground;", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("titleBar.ButtonPressedBackgroundColor = palette.PressedBackground;", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("new AccessibilitySettings().HighContrast", titleBarSource, StringComparison.Ordinal);
        Assert.Contains("NonClientRegionKind.Passthrough", titleBarSource, StringComparison.Ordinal);
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
        Assert.Equal("UniformToFill", skyline.Attribute("Stretch")?.Value);
        Assert.Contains(column.Descendants(), element => HasName(element, "SolarArcPanel"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SolarDaylightPath"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SolarElapsedPath"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SolarCurrentTimeText"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SolarDaylightDurationText"));
        Assert.Contains(column.Descendants(), element => HasName(element, "WeatherAdornmentHost"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SunriseLabelText"));
        Assert.Contains(column.Descendants(), element => HasName(element, "SunsetLabelText"));
        Assert.True(IndexOfName(sceneChildren, "BackdropLayerHost") < IndexOfName(sceneChildren, "SkylineImage"));
        Assert.True(IndexOfName(sceneChildren, "SkylineImage") < IndexOfName(sceneChildren, "ForegroundLayerHost"));
        Assert.DoesNotContain(sceneChildren, element => HasName(element, "SolarArcPanel") || HasName(element, "WeatherPanel"));
        Assert.True(HasName(column.Descendants().Single(element => HasName(element, "WeatherPanel")).Parent!, "ClockDetailsLayout"));
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

    /// <summary>Verifies mutually exclusive centered loading, empty, and populated clock presentation states.</summary>
    [Fact]
    public void LoadingAndEmptyStates_AreCenteredLocalizedAndReuseTheExistingAddFlow()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var loading = window.Descendants().Single(element => HasName(element, "LoadingState"));
        var loadingText = window.Descendants().Single(element => HasName(element, "LoadingStatusText"));
        var empty = window.Descendants().Single(element => HasName(element, "EmptyClocksState"));
        var emptyAdd = window.Descendants().Single(element => HasName(element, "EmptyStateAddButton"));
        var scroller = window.Descendants().Single(element => HasName(element, "ClockColumnsScroller"));
        var catalogs = Directory.GetFiles(
            RepositoryFile("TrackMeUp.Core", "Localization"),
            "*.json",
            SearchOption.TopDirectoryOnly);

        Assert.Equal("Center", loading.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Center", loading.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("Polite", AttributeValue(loading, "AutomationProperties.LiveSetting"));
        Assert.Equal("Collapsed", loading.Attribute("Visibility")?.Value);
        Assert.Equal("WorldClock.Loading", loadingText.Attribute("Tag")?.Value);
        Assert.Equal("Center", empty.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Center", empty.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("Collapsed", empty.Attribute("Visibility")?.Value);
        Assert.Equal("WorldClock.Add", emptyAdd.Attribute("Tag")?.Value);
        Assert.Equal("EmptyStateAddButton_Click", emptyAdd.Attribute("Click")?.Value);
        Assert.Contains(emptyAdd.Descendants(), element =>
            element.Name.LocalName == "SymbolIcon" && element.Attribute("Symbol")?.Value == "Add");
        Assert.Equal("Collapsed", scroller.Attribute("Visibility")?.Value);
        Assert.Contains("private async void EmptyStateAddButton_Click", source, StringComparison.Ordinal);
        Assert.Contains("EmptyStateAddButton_Click(object sender, RoutedEventArgs e) => await AddCityAsync();", source, StringComparison.Ordinal);
        Assert.Contains("LoadingState.Visibility = show ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("EmptyClocksState.Visibility = !show && hasEmptySnapshot ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("ClockColumnsScroller.Visibility = !show && hasClocks ? Visibility.Visible : Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("ShowLoading(_snapshot is null || _snapshot.Clocks.Count == 0);", source, StringComparison.Ordinal);
        Assert.Contains("if (snapshot.Clocks.Count == 0)", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(ReferenceInstantButton, referenceInstantLabel);", source, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(ReferenceInstantButton, referenceInstantLabel);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("must contain at least one city", source, StringComparison.Ordinal);

        Assert.All(catalogs, catalog =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("WorldClock.Loading").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("WorldClock.Empty.Title").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("WorldClock.Empty.Description").GetString()));
        });
    }

    /// <summary>Verifies safe key masking and explicit inline feedback for every provider-validation outcome.</summary>
    [Fact]
    public void WeatherKeyConfiguration_UsesSafeMaskAndLocalizedValidationFeedback()
    {
        var options = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockOptionsControl.xaml.cs"));
        var contracts = File.ReadAllText(RepositoryFile("TrackMeUp.Core", "Application", "Contracts.cs"));
        var keyBox = options.Descendants().Single(element => HasName(element, "WeatherApiKeyBox"));
        var actionStatus = options.Descendants().Single(element => HasName(element, "WeatherActionStatusText"));
        var catalogs = Directory.GetFiles(
            RepositoryFile("TrackMeUp.Core", "Localization"),
            "*.json",
            SearchOption.TopDirectoryOnly);
        string[] feedbackKeys =
        [
            "WorldClock.Options.Weather.ApiKey.ConfiguredHelp",
            "WorldClock.Options.Weather.KeyValidating",
            "WorldClock.Options.Weather.KeySaved",
            "WorldClock.Options.Weather.KeySavedRateLimited",
            "WorldClock.Options.Weather.KeyInvalid",
            "WorldClock.Options.Weather.KeyRejected",
            "WorldClock.Options.Weather.KeyValidationUnavailable",
            "WorldClock.Options.Weather.KeySaveFailed"
        ];

        Assert.Equal("PasswordBox", keyBox.Name.LocalName);
        Assert.Equal("Peek", keyBox.Attribute("PasswordRevealMode")?.Value);
        Assert.Equal(string.Empty, keyBox.Attribute("PlaceholderText")?.Value);
        Assert.Equal("Polite", AttributeValue(actionStatus, "AutomationProperties.LiveSetting"));
        Assert.Contains("private const string ConfiguredWeatherKeyMask = \"****************\";", source, StringComparison.Ordinal);
        Assert.Contains("SetWeatherKeyPresence(status?.IsProviderConfigured == true);", source, StringComparison.Ordinal);
        Assert.Contains("WeatherApiKeyBox.PlaceholderText = configured", source, StringComparison.Ordinal);
        Assert.Contains("WeatherApiKeyBox.Password = string.Empty;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WeatherApiKeyBox.Password = ConfiguredWeatherKeyMask", source, StringComparison.Ordinal);
        Assert.Contains("WorldClock.Options.Weather.KeyValidating", source, StringComparison.Ordinal);
        Assert.Contains("world_clocks.weather.key.rejected", source, StringComparison.Ordinal);
        Assert.Contains("world_clocks.weather.key.validation_unavailable", source, StringComparison.Ordinal);
        Assert.Contains("world_clocks.weather.key.stored_rate_limited", source, StringComparison.Ordinal);
        Assert.Contains("SetWeatherKeyPresence(configured: true);", source, StringComparison.Ordinal);
        Assert.Contains("SetSaveWeatherKeyAction(\"WorldClock.Options.Weather.KeyAction.Change\");", source, StringComparison.Ordinal);
        Assert.Contains("bool IsProviderConfigured", contracts, StringComparison.Ordinal);

        Assert.All(catalogs, catalog =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            Assert.All(feedbackKeys, key =>
            {
                Assert.True(document.RootElement.TryGetProperty(key, out var value), $"{catalog}: {key}");
                Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{catalog}: {key}");
            });
        });
    }

    [Fact]
    public void OpeningFailure_IsVisibleAndLocalizedInEveryCatalog()
    {
        var appSource = File.ReadAllText(RepositoryFile("TrackMeUp", "App.xaml.cs"));
        var mainSource = File.ReadAllText(RepositoryFile("TrackMeUp", "MainWindow.xaml.cs"));
        var localizationDirectory = RepositoryFile("TrackMeUp.Core", "Localization");
        var catalogs = Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly);

        Assert.Contains("_window.ShowWorldClockOpenFailure();", appSource, StringComparison.Ordinal);
        Assert.Contains("internal void ShowWorldClockOpenFailure()", mainSource, StringComparison.Ordinal);
        Assert.Contains("_dialogs.ShowWarningBanner(", mainSource, StringComparison.Ordinal);
        Assert.Contains("T(\"WorldClock.OpenFailed\")", mainSource, StringComparison.Ordinal);
        Assert.Equal(10, catalogs.Length);
        Assert.All(catalogs, catalog =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            Assert.True(document.RootElement.TryGetProperty("WorldClock.OpenFailed", out var message), catalog);
            Assert.False(string.IsNullOrWhiteSpace(message.GetString()), catalog);
        });
    }

    /// <summary>Verifies that fresh current weather is presented with localized text and a condition icon.</summary>
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
            "lightning",
            "unknown"
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
            .Prepend("WorldClock.DaylightDuration")
            .Prepend("WorldClock.SunsetLabel")
            .Prepend("WorldClock.SunriseLabel")
            .ToArray();

        Assert.Equal("StackPanel", weatherPanel.Name.LocalName);
        Assert.NotSame(scene, weatherPanel.Parent);
        Assert.True(HasName(weatherPanel.Parent!, "ClockDetailsLayout"));
        Assert.Equal("Top", weatherPanel.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("0,8,0,0", weatherPanel.Attribute("Margin")?.Value);
        Assert.Null(weatherPanel.Attribute("Visibility"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherTemperatureText"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherConditionText"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherAdornmentHost"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherSunIcon") && element.Name.LocalName == "FontIcon");
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherCloudIcon"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherRainIcon"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherSnowIcon"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherFogIcon"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherLightningIcon"));
        Assert.Contains(weatherPanel.Descendants(), element => HasName(element, "WeatherUnknownIcon"));
        Assert.DoesNotContain(weatherPanel.Descendants(), element => element.Name.LocalName == "Button");
        Assert.Contains("weather is null || !weather.IsFresh", source, StringComparison.Ordinal);
        Assert.Contains("WeatherTemperatureText.Text = \"—\";", source, StringComparison.Ordinal);
        Assert.Contains("strings.Translate(\"WorldClock.WeatherNoData\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WeatherPanel.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("weather.TemperatureCelsius", source, StringComparison.Ordinal);
        Assert.Contains("ApplyWeatherIcon(weather.ConditionKey);", source, StringComparison.Ordinal);
        Assert.Contains("WeatherSunIcon.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("WeatherRainIcon.Visibility", source, StringComparison.Ordinal);
        Assert.Contains("WeatherSnowIcon.Visibility", source, StringComparison.Ordinal);
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

    /// <summary>Verifies that night clocks expose localized lunar phase and illumination summaries accessibly.</summary>
    [Fact]
    public void NightClock_PresentsLocalizedLunarPhaseAndIlluminationAccessibly()
    {
        string[] phaseKeys =
        [
            "WorldClock.MoonPhase.New",
            "WorldClock.MoonPhase.WaxingCrescent",
            "WorldClock.MoonPhase.FirstQuarter",
            "WorldClock.MoonPhase.WaxingGibbous",
            "WorldClock.MoonPhase.Full",
            "WorldClock.MoonPhase.WaningGibbous",
            "WorldClock.MoonPhase.LastQuarter",
            "WorldClock.MoonPhase.WaningCrescent",
            "WorldClock.MoonPhase.Summary"
        ];
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml.cs"));
        var summary = column.Descendants().Single(element => HasName(element, "MoonPhaseSummaryText"));
        var localizationDirectory = RepositoryFile("TrackMeUp.Core", "Localization");
        var catalogs = Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly);

        Assert.Equal("Collapsed", summary.Attribute("Visibility")?.Value);
        Assert.Equal("Wrap", summary.Attribute("TextWrapping")?.Value);
        Assert.Contains("LunarPhaseProjection.Create(clock.MoonPhaseAngleDegrees)", source, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.SetToolTip(MoonPhaseSummaryText, summary);", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(MoonPhaseSummaryText, summary);", source, StringComparison.Ordinal);
        Assert.All(catalogs, catalog =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            Assert.All(phaseKeys, key =>
            {
                Assert.True(document.RootElement.TryGetProperty(key, out var value), $"{catalog}: {key}");
                Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{catalog}: {key}");
            });
        });
    }

    /// <summary>Verifies explicit weather status in options and attribution limited to fresh observations.</summary>
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
    public void CompactWidget_UsesAnAccessibleTitleBarToggleAndKeepsTheAtmosphericCityLayers()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var windowSource = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var columnSource = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml.cs"));
        var localizationDirectory = RepositoryFile("TrackMeUp.Core", "Localization");
        var presentationButton = window.Descendants().Single(element => HasName(element, "PresentationModeButton"));
        var compactDaylight = column.Descendants().Single(element => HasName(element, "CompactDaylightDurationText"));
        var compactTimeZone = column.Descendants().Single(element => HasName(element, "CompactTimeZoneText"));

        Assert.Equal("PresentationModeButton_Click", presentationButton.Attribute("Click")?.Value);
        Assert.Equal("True", presentationButton.Attribute("IsTabStop")?.Value);
        Assert.Equal("True", presentationButton.Attribute("AllowFocusOnInteraction")?.Value);
        Assert.Equal("WorldClock.Layout.Compact", presentationButton.Attribute("Tag")?.Value);
        Assert.Equal("Switch to compact layout", AttributeValue(presentationButton, "AutomationProperties.Name"));
        Assert.Equal(AttributeValue(presentationButton, "AutomationProperties.Name"), AttributeValue(presentationButton, "ToolTipService.ToolTip"));
        Assert.Equal("Collapsed", compactDaylight.Attribute("Visibility")?.Value);
        Assert.Equal("Collapsed", compactTimeZone.Attribute("Visibility")?.Value);
        Assert.Contains("column.SetPresentationMode(presentationMode);", windowSource, StringComparison.Ordinal);
        Assert.Contains("UpdateClockColumnsLayout(snapshot.Clocks.Count, ClockColumnsScroller.ActualWidth);", windowSource, StringComparison.Ordinal);
        Assert.Contains("_placement.ResizeForContent(", windowSource, StringComparison.Ordinal);
        Assert.Contains("SolarArcPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;", columnSource, StringComparison.Ordinal);
        Assert.Contains("CompactDaylightDurationText.Visibility = detail == WorldClockDetailLevel.Summary", columnSource, StringComparison.Ordinal);
        Assert.Contains("CompactTimeZoneText.Visibility = !expanded", columnSource, StringComparison.Ordinal);
        Assert.Contains("ClockDetailsLayout.Measure(new Size(_viewportWidth, double.PositiveInfinity));", columnSource, StringComparison.Ordinal);
        Assert.Contains("WorldClockWindowLayoutState.CalculateDetailLevel(", columnSource, StringComparison.Ordinal);
        Assert.Contains("column.SetViewportSize(", windowSource, StringComparison.Ordinal);

        foreach (var catalog in Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalog));
            foreach (var key in new[] { "WorldClock.Layout.Compact", "WorldClock.Layout.Expanded" })
            {
                Assert.True(document.RootElement.TryGetProperty(key, out var value), $"{catalog}: {key}");
                Assert.False(string.IsNullOrWhiteSpace(value.GetString()), $"{catalog}: {key}");
            }
        }
    }

    [Fact]
    public void ResponsiveLayout_ReservesAttributionSpaceAndAppliesPendingResizeBeforeTheLiveBranch()
    {
        var window = XDocument.Load(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml"));
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "WorldClockWindow.xaml.cs"));
        var attribution = window.Descendants().Single(element => HasName(element, "WeatherAttributionButton"));
        var scroller = window.Descendants().Single(element => HasName(element, "ClockColumnsScroller"));
        var surfaceRows = attribution.Parent!.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions").Elements().ToArray();
        var duration = column.Descendants().Single(element => HasName(element, "CompactDaylightDurationText"));
        var utc = column.Descendants().Single(element => HasName(element, "CompactTimeZoneText"));
        var scene = column.Descendants().Single(element => HasName(element, "SceneGrid"));

        Assert.Equal("1", attribution.Attribute("Grid.Row")?.Value);
        Assert.Equal("Auto", surfaceRows[1].Attribute("Height")?.Value);
        Assert.Null(scroller.Attribute("Grid.Row"));
        Assert.Equal("Auto", scroller.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Same(duration.Parent, utc.Parent);
        Assert.Equal("1", utc.Attribute("Grid.Column")?.Value);
        Assert.Equal("Canvas", scene.Parent!.Name.LocalName);
        Assert.DoesNotContain(scene.Descendants(), element => element.Name.LocalName == "TextBlock");

        var returnToClocks = source.IndexOf("private async Task ShowClocksSurfaceAsync()", StringComparison.Ordinal);
        var resize = source.IndexOf("ApplySmartWindowSizing(snapshot.Clocks.Count);", returnToClocks, StringComparison.Ordinal);
        var liveBranch = source.IndexOf("if (_isLive)", returnToClocks, StringComparison.Ordinal);
        Assert.True(resize > returnToClocks && resize < liveBranch);
        Assert.Contains("_layoutState.SetPlacementRestored(restored);", source, StringComparison.Ordinal);
        Assert.Contains("if (request.ResizeToPreferred)", source, StringComparison.Ordinal);
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
        Assert.Contains("SetIconButtonLabel(PresentationModeButton, key);", windowSource, StringComparison.Ordinal);
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
    public void SolarTimeline_UsesLocalizedDurationAndUnlabelledCurrentMarker()
    {
        var column = XDocument.Load(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml"));
        var source = File.ReadAllText(RepositoryFile("TrackMeUp", "Controls", "WorldClockColumnControl.xaml.cs"));
        var currentTime = column.Descendants().Single(element => HasName(element, "SolarCurrentTimeText"));
        var duration = column.Descendants().Single(element => HasName(element, "SolarDaylightDurationText"));

        Assert.Equal("Collapsed", currentTime.Attribute("Visibility")?.Value);
        Assert.Equal("Center", currentTime.Attribute("TextAlignment")?.Value);
        Assert.Equal("Center", duration.Attribute("TextAlignment")?.Value);
        Assert.Contains("strings.Format(\"WorldClock.DaylightDuration\", durationText)", source, StringComparison.Ordinal);
        Assert.Contains("SolarCurrentTimeText.Text = isInDaylight ? FormatTime(clock.LocalTime, strings) : string.Empty;", source, StringComparison.Ordinal);
        Assert.Contains("BuildSolarArcGeometry", source, StringComparison.Ordinal);
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
