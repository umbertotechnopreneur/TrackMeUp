// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

/// <summary>Verifies the isolated application-layer world-clock use cases.</summary>
public sealed class WorldClockApplicationServiceTests
{
    /// <summary>Ensures duplicate and over-capacity additions fail without changing persisted settings.</summary>
    [Fact]
    public void AddValidated_RejectsDuplicatesAndMaximumWithoutPersisting()
    {
        var settings = new SettingsSnapshot(new AppSettings(
            WorldClockCityIds: ["london", "paris", "tokyo", "hanoi"]));
        var persisted = new List<AppSettings>();
        using var service = CreateService(settings, persisted.Add);

        var duplicateId = service.NormalizeAndValidateCityId(" TOKYO ");
        var duplicate = service.AddValidated(duplicateId);
        var berlinId = service.NormalizeAndValidateCityId("berlin");
        var maximum = service.AddValidated(berlinId);

        Assert.False(duplicate.Succeeded);
        Assert.Equal("world_clocks.duplicate", duplicate.Code);
        Assert.Equal("cityId", Assert.Single(duplicate.Issues).Field);
        Assert.False(maximum.Succeeded);
        Assert.Equal("world_clocks.maximum_reached", maximum.Code);
        Assert.Equal("cityId", Assert.Single(maximum.Issues).Field);
        Assert.Empty(persisted);
        Assert.Equal(["london", "paris", "tokyo", "hanoi"], settings.Value.WorldClockCityIds);
    }

    /// <summary>Ensures removal preserves the one-clock minimum and reports missing selections.</summary>
    [Fact]
    public void Remove_RejectsUnknownAndFinalCityWithoutExtraPersistence()
    {
        var settings = new SettingsSnapshot(new AppSettings(WorldClockCityIds: ["london", "paris"]));
        var persisted = new List<AppSettings>();
        using var service = CreateService(settings, persisted.Add);

        var missing = service.Remove("tokyo");
        var removed = service.Remove(" PARIS ");
        var minimum = service.Remove("london");

        Assert.False(missing.Succeeded);
        Assert.Equal("world_clocks.not_found", missing.Code);
        Assert.True(removed.Succeeded);
        Assert.Equal(["london"], removed.Value?.CityIds);
        Assert.False(minimum.Succeeded);
        Assert.Equal("world_clocks.minimum_reached", minimum.Code);
        Assert.Single(persisted);
        Assert.Equal(["london"], settings.Value.WorldClockCityIds);
    }

    /// <summary>Ensures conversion failures identify the request field that needs correction.</summary>
    [Fact]
    public void Convert_MapsReferenceAndCivilTimeFailuresToTheirRequestFields()
    {
        var settings = new SettingsSnapshot(new AppSettings(WorldClockCityIds: ["london", "paris"]));
        using var service = CreateService(settings);

        var missingReference = service.Convert(new WorldClockConversionRequest(
            "tokyo",
            new DateTime(2026, 8, 30, 10, 18, 0, DateTimeKind.Unspecified)));
        var invalidCivilTime = service.Convert(new WorldClockConversionRequest(
            "paris",
            new DateTime(2026, 3, 29, 2, 30, 0, DateTimeKind.Unspecified)));

        Assert.False(missingReference.Succeeded);
        Assert.Equal("referenceCityId", Assert.Single(missingReference.Issues).Field);
        Assert.False(invalidCivilTime.Succeeded);
        Assert.Equal("world_clocks.local_time.invalid", invalidCivilTime.Code);
        Assert.Equal("referenceLocalTime", Assert.Single(invalidCivilTime.Issues).Field);
    }

    /// <summary>Ensures weather secrets are validated and written only through the environment contract.</summary>
    [Fact]
    public void SetWeatherKey_ValidatesBeforeWritingAndUsesOnlyTheEnvironmentVariableContract()
    {
        var settings = new SettingsSnapshot(new AppSettings(WorldClockCityIds: ["london"]));
        var writes = new List<(string Name, string Secret)>();
        using var service = CreateService(
            settings,
            setApiKey: (name, secret) => writes.Add((name, secret)));

        var invalid = service.SetWeatherKey("short");
        var secret = new string('a', 32);
        var stored = service.SetWeatherKey(secret);

        Assert.False(invalid.Succeeded);
        Assert.Equal("world_clocks.weather.key.invalid", invalid.Code);
        var write = Assert.Single(writes);
        Assert.Equal(OpenWeatherCurrentProvider.ApiKeyEnvironmentVariable, write.Name);
        Assert.Equal(secret, write.Secret);
        Assert.True(stored.Succeeded);
        Assert.Equal(OpenWeatherCurrentProvider.ApiKeyEnvironmentVariable, stored.Value);
    }

    private static WorldClockApplicationService CreateService(
        SettingsSnapshot settings,
        Action<AppSettings>? onPersist = null,
        Action<string, string>? setApiKey = null)
    {
        var worldClocks = new WorldClockService(
            RepositoryFile("TrackMeUp", "Assets", "WorldClocks", "world-clocks.sqlite3"));
        return new WorldClockApplicationService(
            worldClocks,
            settings,
            setApiKey ?? ((_, _) => { }),
            updated =>
            {
                onPersist?.Invoke(updated);
                settings.Replace(updated);
            });
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
