// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Text.Json;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class SpaceWeatherLocationFilterTests
{
    private const string SettingKey = "world_clocks.space_weather.hide_by_location";
    private static readonly DateTimeOffset Instant = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Preserves the existing filter by default and round-trips the explicit choice through persisted JSON.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Settings_DefaultToFilteredAndRoundTrip(bool enabled)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.True(new AppSettings().HideSpaceWeatherByLocation);
        Assert.True(JsonSerializer.Deserialize<AppSettings>("{}", options)!.HideSpaceWeatherByLocation);
        var result = SettingsCatalog.Apply(new AppSettings(), new SettingsPatch(new Dictionary<string, string?>
        {
            [SettingKey] = enabled ? "true" : "false"
        }));
        Assert.True(result.Succeeded);
        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(result.Value, options), options)!;
        Assert.Equal(enabled, restored.HideSpaceWeatherByLocation);
        Assert.True(SettingsCatalog.TryGetValue(restored, SettingKey, out var value));
        Assert.Equal(enabled, Assert.IsType<bool>(value));
        Assert.False(SettingsCatalog.Apply(restored, new SettingsPatch(new Dictionary<string, string?>
        {
            [SettingKey] = "invalid"
        })).Succeeded);
    }

    /// <summary>Disabling the filter reveals a valid storm at low latitude without reviving expired alerts.</summary>
    [Fact]
    public void Alerts_OnlyBypassLocationAndDarkness()
    {
        var alert = new SpaceWeatherAlert("synthetic", Instant.AddHours(-1), Instant.AddHours(-1), Instant.AddHours(1),
            SpaceWeatherEventKind.GeomagneticStorm, 1, 5, "Synthetic storm");
        Assert.False(CelestialSpaceWeatherService.IsRelevantAlert(alert, Instant, 0, 0, true));
        Assert.True(CelestialSpaceWeatherService.IsRelevantAlert(alert, Instant, 0, 0, false));
        foreach (var filter in new[] { true, false })
        {
            Assert.False(CelestialSpaceWeatherService.IsRelevantAlert(alert with { ValidToUtc = Instant }, Instant, 0, 0, filter));
            Assert.False(CelestialSpaceWeatherService.IsRelevantAlert(alert with { ValidFromUtc = Instant.AddHours(1) }, Instant, 0, 0, filter));
            Assert.False(CelestialSpaceWeatherService.IsRelevantAlert(alert with { IssuedUtc = Instant.AddHours(1) }, Instant, 0, 0, filter));
            Assert.True(CelestialSpaceWeatherService.IsRelevantAlert(alert with { Kind = SpaceWeatherEventKind.RadioBlackout }, Instant, 0, 0, filter));
        }
    }

    /// <summary>Global forecasts remain significant, predicted and within the same 48-hour window.</summary>
    [Fact]
    public void Forecasts_KeepSeverityAndTimeBoundsWhenUnfiltered()
    {
        var period = new SpaceWeatherKpForecast(Instant, 5, "predicted", 1);
        Assert.False(CelestialSpaceWeatherService.IsRelevantForecast(period, Instant, 0, 0, true));
        Assert.True(CelestialSpaceWeatherService.IsRelevantForecast(period, Instant, 0, 0, false));
        Assert.False(CelestialSpaceWeatherService.IsRelevantForecast(period with { KpIndex = 4 }, Instant, 0, 0, false));
        Assert.False(CelestialSpaceWeatherService.IsRelevantForecast(period with { KpIndex = double.NaN }, Instant, 0, 0, false));
        Assert.False(CelestialSpaceWeatherService.IsRelevantForecast(period with { Status = "observed" }, Instant, 0, 0, false));
        Assert.False(CelestialSpaceWeatherService.IsRelevantForecast(period with { StartUtc = Instant.AddHours(-3) }, Instant, 0, 0, false));
        Assert.False(CelestialSpaceWeatherService.IsRelevantForecast(period with { StartUtc = Instant.AddHours(48) }, Instant, 0, 0, false));
    }

    /// <summary>A polar-day forecast stays hidden only when the geographic filter is enabled.</summary>
    [Fact]
    public void Filter_AccountsForDarknessAtHighLatitude()
    {
        var summer = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
        var period = new SpaceWeatherKpForecast(summer, 7, "predicted", 3);
        Assert.False(CelestialSpaceWeatherService.IsRelevantForecast(period, summer, 80, 0, true));
        Assert.True(CelestialSpaceWeatherService.IsRelevantForecast(period, summer, 80, 0, false));
        var winter = new DateTimeOffset(2026, 12, 21, 0, 0, 0, TimeSpan.Zero);
        Assert.True(CelestialSpaceWeatherService.IsRelevantForecast(period with { StartUtc = winter }, winter, 80, 0, true));
    }
}
