using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class WorldClockServiceTests
{
    [Fact]
    public void Catalog_ContainsExactlyOneHundredCapitalsAndLocalCity()
    {
        var catalogPath = RepositoryFile("TrackMeUp", "Assets", "WorldClocks", "world-clocks.sqlite3");
        var service = new WorldClockService(catalogPath);

        var catalog = service.GetCatalog();

        Assert.Equal(101, catalog.Cities.Count);
        Assert.Equal(100, catalog.Cities.Count(static city => city.IsCapital));
        Assert.Equal(WorldClockSelection.MaximumClocks, catalog.MaximumClocks);
        Assert.Contains(catalog.Cities, static city => city.Id == "ho-chi-minh-city" && !city.IsCapital);
    }

    [Fact]
    public void Astronomy_KnownNewAndFullMoonInstantsHaveExpectedIllumination()
    {
        var utc = TimeZoneInfo.Utc;

        var newMoon = LocalAstronomy.Calculate(0d, 0d, utc, new DateTimeOffset(2000, 1, 6, 18, 14, 0, TimeSpan.Zero));
        var fullMoon = LocalAstronomy.Calculate(0d, 0d, utc, new DateTimeOffset(2000, 1, 21, 4, 40, 0, TimeSpan.Zero));

        Assert.InRange(newMoon.MoonIllumination, 0d, 0.02d);
        Assert.InRange(fullMoon.MoonIllumination, 0.98d, 1d);
        Assert.Equal("new-moon", newMoon.MoonPhaseKey);
        Assert.Equal("full-moon", fullMoon.MoonPhaseKey);
    }

    [Fact]
    public void Astronomy_LondonEquinoxProducesPlausibleLocalSunriseAndSunset()
    {
        var result = LocalAstronomy.Calculate(
            51.5074,
            -0.1278,
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));

        Assert.NotNull(result.Sunrise);
        Assert.NotNull(result.Sunset);
        Assert.InRange(result.Sunrise.Value.Hour, 5, 7);
        Assert.InRange(result.Sunset.Value.Hour, 17, 19);
        Assert.True(result.Sunrise < result.Sunset);
    }

    [Fact]
    public void Selection_RejectsDuplicatesAndMoreThanFourClocks()
    {
        Assert.Throws<InvalidDataException>(() => WorldClockSelection.NormalizePersisted(["london", "london"]));
        Assert.Throws<InvalidDataException>(() => WorldClockSelection.NormalizePersisted(["london", "paris", "tokyo", "hanoi", "berlin"]));
    }

    [Fact]
    public void CatalogAssets_HaveTwoDistinctLicensedChecksummedImagesPerCity()
    {
        var catalogPath = RepositoryFile("TrackMeUp", "Assets", "WorldClocks", "world-clocks.sqlite3");
        using var connection = new SqliteConnection($"Data Source={catalogPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM (
                SELECT city_id
                FROM skyline_asset
                GROUP BY city_id
                HAVING COUNT(*) = 2
                   AND COUNT(DISTINCT title) = 2
                   AND MIN(LENGTH(author)) > 0
                   AND MIN(LENGTH(license_name)) > 0
                   AND MIN(LENGTH(sha256)) = 64
            );
            """;

        Assert.Equal(101L, (long)command.ExecuteScalar()!);
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
