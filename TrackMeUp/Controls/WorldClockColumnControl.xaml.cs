// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Renders one city in the detached world-clock comparison surface.</summary>
public sealed partial class WorldClockColumnControl : UserControl
{
    private static readonly SolidColorBrush DayBrush = new(Windows.UI.Color.FromArgb(255, 232, 91, 66));
    private static readonly SolidColorBrush NightBrush = new(Windows.UI.Color.FromArgb(255, 116, 110, 207));
    private WorldClockItem? _clock;
    private string? _skylineAssetPath;
    private string[] _backdropAssetPaths = [];
    private string[] _foregroundAssetPaths = [];

    /// <summary>Creates one passive world-clock column.</summary>
    public WorldClockColumnControl()
    {
        InitializeComponent();
    }

    /// <summary>Occurs when this city should become the conversion reference.</summary>
    public event EventHandler<WorldClockCityEventArgs>? ReferenceRequested;

    /// <summary>Occurs when the user requests removal of this city.</summary>
    public event EventHandler<WorldClockCityEventArgs>? RemoveRequested;

    /// <summary>Gets the city currently rendered by this column.</summary>
    public string? CityId => _clock?.CityId;

    /// <summary>Applies the latest locally calculated city projection.</summary>
    public void Apply(
        WorldClockItem clock,
        WorldClockItem referenceClock,
        bool isReference,
        bool canRemove,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(referenceClock);
        ArgumentNullException.ThrowIfNull(strings);
        if (!double.IsFinite(clock.MoonPhaseAngleDegrees))
        {
            throw new InvalidDataException("The world-clock lunar phase must be finite.");
        }

        _clock = clock;

        var accent = clock.IsDaylight ? DayBrush : NightBrush;
        CityNameText.Text = clock.CityName.ToUpper(strings.Culture);
        LocalTimeText.Text = clock.LocalTime.ToString("HH:mm", strings.Culture);
        LocalTimeText.Foreground = accent;
        DayStateText.Text = strings.Translate(clock.IsDaylight ? "WorldClock.Day" : "WorldClock.Night");
        DayStateText.Foreground = accent;
        OffsetText.Text = isReference
            ? strings.Translate("WorldClock.Reference")
            : strings.Format("WorldClock.OffsetFromReference", FormatOffset(clock.LocalTime.Offset - referenceClock.LocalTime.Offset));
        OffsetText.Foreground = isReference ? DayBrush : accent;
        ReferenceMarker.Fill = isReference ? DayBrush : accent;
        ReferenceMarker.Opacity = isReference ? 1d : 0.34d;
        ReferenceButton.Visibility = isReference ? Visibility.Collapsed : Visibility.Visible;
        RemoveButton.Visibility = canRemove ? Visibility.Visible : Visibility.Collapsed;
        DateRelationText.Text = DateRelation(clock.LocalTime.Date, referenceClock.LocalTime.Date, strings);
        DateRelationText.Visibility = string.IsNullOrEmpty(DateRelationText.Text) ? Visibility.Collapsed : Visibility.Visible;

        ApplySkyline(clock.SkylineAssetPath);
        _backdropAssetPaths = ApplyLayers(
            BackdropLayerHost,
            clock.Atmosphere.BackdropAssetPaths,
            _backdropAssetPaths,
            Stretch.UniformToFill);
        _foregroundAssetPaths = ApplyLayers(
            ForegroundLayerHost,
            clock.Atmosphere.ForegroundAssetPaths,
            _foregroundAssetPaths,
            Stretch.UniformToFill);
        CelestialPhase.IsDaylight = clock.IsDaylight;
        CelestialPhase.MoonPhaseAngleDegrees = clock.MoonPhaseAngleDegrees;
        SunriseText.Text = strings.Format("WorldClock.Sunrise", FormatTime(clock.Sunrise, strings));
        SunsetText.Text = strings.Format("WorldClock.Sunset", FormatTime(clock.Sunset, strings));
        var weatherSummary = ApplyWeather(clock.Weather, strings);

        var referenceName = strings.Format("WorldClock.SetReference", clock.CityName);
        AutomationProperties.SetName(ReferenceButton, referenceName);
        ToolTipService.SetToolTip(ReferenceButton, referenceName);
        var removeName = strings.Format("WorldClock.Remove", clock.CityName);
        AutomationProperties.SetName(RemoveButton, removeName);
        ToolTipService.SetToolTip(RemoveButton, removeName);
        var accessibleSummary = string.IsNullOrEmpty(weatherSummary)
            ? $"{clock.CityName}, {LocalTimeText.Text}, {OffsetText.Text}, {DayStateText.Text}, {SunriseText.Text}, {SunsetText.Text}"
            : $"{clock.CityName}, {LocalTimeText.Text}, {OffsetText.Text}, {DayStateText.Text}, {weatherSummary}, {SunriseText.Text}, {SunsetText.Text}";
        AutomationProperties.SetName(ColumnRoot, accessibleSummary);
        AutomationProperties.SetLocalizedLandmarkType(ColumnRoot, clock.CityName);
    }

    private string ApplyWeather(WorldClockWeather? weather, LocalizationService strings)
    {
        if (weather is null || !weather.IsFresh)
        {
            WeatherTemperatureText.Text = string.Empty;
            WeatherConditionText.Text = string.Empty;
            WeatherPanel.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(WeatherPanel, string.Empty);
            return string.Empty;
        }

        var conditionKey = weather.ConditionKey switch
        {
            "clear" => "WorldClock.WeatherCondition.clear",
            "cloudy" => "WorldClock.WeatherCondition.cloudy",
            "rain" => "WorldClock.WeatherCondition.rain",
            "snow" => "WorldClock.WeatherCondition.snow",
            "mixed-precipitation" => "WorldClock.WeatherCondition.mixed-precipitation",
            "fog" => "WorldClock.WeatherCondition.fog",
            "lightning" => "WorldClock.WeatherCondition.lightning",
            _ => throw new InvalidDataException($"Unsupported world-clock weather condition '{weather.ConditionKey}'.")
        };
        WeatherTemperatureText.Text = strings.Format(
            "WorldClock.WeatherTemperature",
            weather.TemperatureCelsius);
        WeatherConditionText.Text = strings.Translate(conditionKey);
        var summary = $"{WeatherTemperatureText.Text}, {WeatherConditionText.Text}";
        WeatherPanel.Visibility = Visibility.Visible;
        AutomationProperties.SetName(WeatherPanel, summary);
        return summary;
    }

    private void ApplySkyline(string assetPath)
    {
        if (string.Equals(_skylineAssetPath, assetPath, StringComparison.Ordinal))
        {
            return;
        }

        SkylineImage.Source = PackagedBitmap(assetPath);
        _skylineAssetPath = assetPath;
    }

    private static string[] ApplyLayers(
        Grid host,
        IReadOnlyList<string> assetPaths,
        IReadOnlyList<string> currentPaths,
        Stretch stretch)
    {
        ArgumentNullException.ThrowIfNull(assetPaths);
        if (assetPaths.SequenceEqual(currentPaths, StringComparer.Ordinal))
        {
            return currentPaths as string[] ?? currentPaths.ToArray();
        }

        host.Children.Clear();
        foreach (var assetPath in assetPaths)
        {
            var image = new Image
            {
                Source = PackagedBitmap(assetPath),
                Stretch = stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false
            };
            AutomationProperties.SetAccessibilityView(image, AccessibilityView.Raw);
            host.Children.Add(image);
        }

        return assetPaths.ToArray();
    }

    private static BitmapImage PackagedBitmap(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new InvalidDataException("World-clock visual layer path is empty.");
        }

        var normalized = assetPath.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/WorldClocks/", StringComparison.Ordinal)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || !normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"World-clock visual layer path is invalid: {assetPath}");
        }

        return new BitmapImage(new Uri($"ms-appx:///{normalized}"))
        {
            DecodePixelWidth = 960
        };
    }

    private void ReferenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (_clock is { } clock)
        {
            ReferenceRequested?.Invoke(this, new WorldClockCityEventArgs(clock.CityId, clock.CityName));
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_clock is { } clock)
        {
            RemoveRequested?.Invoke(this, new WorldClockCityEventArgs(clock.CityId, clock.CityName));
        }
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "−" : "+";
        var absolute = offset.Duration();
        return absolute.Minutes == 0
            ? $"{sign}{absolute.Hours + (absolute.Days * 24)}"
            : $"{sign}{absolute.Hours + (absolute.Days * 24)}:{absolute.Minutes:00}";
    }

    private static string DateRelation(DateTime cityDate, DateTime referenceDate, LocalizationService strings) =>
        (cityDate.Date - referenceDate.Date).Days switch
        {
            -1 => strings.Translate("WorldClock.PreviousDay"),
            0 => string.Empty,
            1 => strings.Translate("WorldClock.NextDay"),
            _ => cityDate.ToString("ddd d MMM", strings.Culture)
        };

    private static string FormatTime(DateTimeOffset? value, LocalizationService strings) =>
        value?.ToString("HH:mm", strings.Culture) ?? strings.Translate("WorldClock.NoEvent");
}

/// <summary>Identifies a city selected from the detached world-clock surface.</summary>
public sealed class WorldClockCityEventArgs(string cityId, string cityName) : EventArgs
{
    /// <summary>Gets the stable packaged city identifier.</summary>
    public string CityId { get; } = cityId;

    /// <summary>Gets the localized catalog city name.</summary>
    public string CityName { get; } = cityName;
}
