// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Renders one city in the detached world-clock comparison surface.</summary>
public sealed partial class WorldClockColumnControl : UserControl
{
    private string? _skylineAssetPath;
    private string[] _backdropAssetPaths = [];
    private string[] _foregroundAssetPaths = [];

    /// <summary>Creates one passive world-clock column.</summary>
    public WorldClockColumnControl()
    {
        InitializeComponent();
    }

    /// <summary>Applies the latest locally calculated city projection.</summary>
    public void Apply(
        WorldClockItem clock,
        WorldClockItem referenceClock,
        bool isReference,
        LocalizationService strings)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(referenceClock);
        ArgumentNullException.ThrowIfNull(strings);
        if (!double.IsFinite(clock.MoonPhaseAngleDegrees))
        {
            throw new InvalidDataException("The world-clock lunar phase must be finite.");
        }

        var accent = clock.IsDaylight ? DayAccentResource.Background : NightAccentResource.Background;
        CityNameText.Text = clock.CityName.ToUpper(strings.Culture);
        LocalTimeText.Text = clock.LocalTime.ToString("HH:mm", strings.Culture);
        LocalTimeText.Foreground = accent;
        DayStateText.Text = strings.Translate(clock.IsDaylight ? "WorldClock.Day" : "WorldClock.Night");
        DayStateText.Foreground = accent;
        OffsetText.Text = isReference
            ? strings.Translate("WorldClock.LocalTime")
            : strings.Format("WorldClock.OffsetFromReference", FormatOffset(clock.LocalTime.Offset - referenceClock.LocalTime.Offset));
        OffsetText.Foreground = accent;
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
        var moonPhaseSummary = ApplyMoonPhase(clock, strings, accent);
        SunriseIcon.Foreground = accent;
        SunsetIcon.Foreground = accent;
        SunriseHorizon.Fill = accent;
        SunsetHorizon.Fill = accent;
        var sunriseTime = FormatTime(clock.Sunrise, strings);
        var sunsetTime = FormatTime(clock.Sunset, strings);
        SunriseLabelText.Text = strings.Translate("WorldClock.SunriseLabel");
        SunsetLabelText.Text = strings.Translate("WorldClock.SunsetLabel");
        SunriseTimeText.Text = sunriseTime;
        SunsetTimeText.Text = sunsetTime;
        var sunriseSummary = strings.Format("WorldClock.Sunrise", sunriseTime);
        var sunsetSummary = strings.Format("WorldClock.Sunset", sunsetTime);
        var weatherSummary = ApplyWeather(clock.Weather, strings);

        var accessibleDetails = new[]
        {
            clock.CityName,
            LocalTimeText.Text,
            OffsetText.Text,
            DayStateText.Text,
            moonPhaseSummary,
            weatherSummary,
            sunriseSummary,
            sunsetSummary
        }.Where(static detail => !string.IsNullOrEmpty(detail));
        var accessibleSummary = string.Join(", ", accessibleDetails);
        AutomationProperties.SetName(ColumnRoot, accessibleSummary);
        AutomationProperties.SetLocalizedLandmarkType(ColumnRoot, clock.CityName);
    }

    private string ApplyMoonPhase(
        WorldClockItem clock,
        LocalizationService strings,
        Brush accent)
    {
        if (clock.IsDaylight)
        {
            MoonPhaseSummaryText.Text = string.Empty;
            MoonPhaseSummaryText.Visibility = Visibility.Collapsed;
            ToolTipService.SetToolTip(MoonPhaseSummaryText, null);
            AutomationProperties.SetName(MoonPhaseSummaryText, string.Empty);
            return string.Empty;
        }

        var presentation = LunarPhaseProjection.Create(clock.MoonPhaseAngleDegrees);
        var phaseName = strings.Translate(presentation.LocalizationKey);
        var summary = strings.Format(
            "WorldClock.MoonPhase.Summary",
            phaseName,
            presentation.IlluminatedPercentage);
        MoonPhaseSummaryText.Text = summary;
        MoonPhaseSummaryText.Foreground = accent;
        MoonPhaseSummaryText.Visibility = Visibility.Visible;
        ToolTipService.SetToolTip(MoonPhaseSummaryText, summary);
        AutomationProperties.SetName(MoonPhaseSummaryText, summary);
        return summary;
    }

    private string ApplyWeather(WorldClockWeather? weather, LocalizationService strings)
    {
        if (weather is null || !weather.IsFresh)
        {
            WeatherTemperatureText.Text = "—";
            WeatherConditionText.Text = strings.Translate("WorldClock.WeatherNoData");
            WeatherPanel.Opacity = 0.58d;
            WeatherAdornmentHost.Visibility = Visibility.Collapsed;
            var unavailableSummary = WeatherConditionText.Text;
            AutomationProperties.SetName(WeatherPanel, unavailableSummary);
            return unavailableSummary;
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
            "unknown" => "WorldClock.WeatherCondition.unknown",
            _ => throw new InvalidDataException($"Unsupported world-clock weather condition '{weather.ConditionKey}'.")
        };
        WeatherTemperatureText.Text = string.Concat(
            weather.TemperatureCelsius.ToString("0", strings.Culture),
            "°");
        var accessibleTemperature = strings.Format(
            "WorldClock.WeatherTemperature",
            weather.TemperatureCelsius);
        WeatherConditionText.Text = strings.Translate(conditionKey);
        var summary = $"{accessibleTemperature}, {WeatherConditionText.Text}";
        WeatherPanel.Opacity = 1d;
        WeatherAdornmentHost.Visibility = weather.ConditionKey is "clear" or "unknown"
            ? Visibility.Collapsed
            : Visibility.Visible;
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
