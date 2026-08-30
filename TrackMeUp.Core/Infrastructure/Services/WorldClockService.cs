// SPDX-License-Identifier: MIT

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>Defines the stable world-clock selection contract.</summary>
public static class WorldClockSelection
{
    /// <summary>Maximum number of clocks visible in the comparison window.</summary>
    public const int MaximumClocks = 4;

    /// <summary>Initial selection matching the approved local-plus-capitals composition.</summary>
    public static IReadOnlyList<string> Defaults { get; } = ["ho-chi-minh-city", "london", "tokyo", "paris"];

    /// <summary>Validates persisted identifiers without requiring catalog I/O during settings deserialization.</summary>
    public static IReadOnlyList<string> NormalizePersisted(IReadOnlyList<string>? cityIds)
    {
        if (cityIds is null)
        {
            return Defaults;
        }

        if (cityIds.Count is < 1 or > MaximumClocks)
        {
            throw new InvalidDataException($"World-clock selection must contain between 1 and {MaximumClocks} cities.");
        }

        var normalized = cityIds.Select(static id => id?.Trim().ToLowerInvariant() ?? string.Empty).ToArray();
        if (normalized.Any(static id => id.Length is < 1 or > 80 || id.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
        {
            throw new InvalidDataException("World-clock selection contains an invalid city identifier.");
        }

        if (normalized.Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new InvalidDataException("World-clock selection contains duplicate cities.");
        }

        return normalized;
    }
}

/// <summary>Maps locally calculated daylight events to decorative packaged atmosphere layers.</summary>
internal static class WorldClockAtmosphereResolver
{
    private const string BackdropRoot = "Assets/WorldClocks/Overlays/Backdrops";
    private const string ForegroundRoot = "Assets/WorldClocks/Overlays/Foregrounds";

    internal static WorldClockAtmosphere Resolve(
        DateTimeOffset localTime,
        DateTimeOffset? sunrise,
        DateTimeOffset? sunset,
        bool isDaylight,
        string? currentConditionKey = null)
    {
        var dawnDistance = EventDistance(localTime, sunrise, -60, 45);
        var sunsetDistance = EventDistance(localTime, sunset, -45, 60);
        string phase;
        bool useGoldenHour;
        if (dawnDistance is not null && (sunsetDistance is null || dawnDistance <= sunsetDistance))
        {
            phase = "dawn";
            useGoldenHour = dawnDistance.Value <= 45;
        }
        else if (sunsetDistance is not null)
        {
            phase = "sunset";
            useGoldenHour = sunsetDistance.Value <= 45;
        }
        else
        {
            phase = isDaylight ? "day" : "night";
            useGoldenHour = false;
        }

        if (currentConditionKey is not null
            && currentConditionKey is not (
                "clear" or
                "cloudy" or
                "rain" or
                "snow" or
                "mixed-precipitation" or
                "fog" or
                "lightning"))
        {
            throw new InvalidDataException($"Unsupported current weather condition '{currentConditionKey}'.");
        }

        var backdrops = new List<string>();
        var foregrounds = new List<string>();
        if (phase == "night")
        {
            backdrops.Add($"{BackdropRoot}/stars.png");
        }
        else if (useGoldenHour)
        {
            backdrops.Add($"{BackdropRoot}/golden-hour.png");
        }

        var requiresClouds = currentConditionKey is
            "cloudy" or
            "rain" or
            "snow" or
            "mixed-precipitation" or
            "lightning";
        if (requiresClouds)
        {
            var cloudFileName = phase switch
            {
                "dawn" => "clouds-dawn.png",
                "sunset" => "clouds-sunset.png",
                "day" => "clouds-day.png",
                "night" => "clouds-night.png",
                _ => throw new InvalidDataException($"Unsupported local-time phase '{phase}'.")
            };
            backdrops.Add($"{BackdropRoot}/{cloudFileName}");
        }

        switch (currentConditionKey)
        {
            case "rain":
                foregrounds.Add($"{ForegroundRoot}/rain.png");
                break;
            case "snow":
                foregrounds.Add($"{ForegroundRoot}/snow.png");
                break;
            case "mixed-precipitation":
                foregrounds.Add($"{ForegroundRoot}/rain.png");
                foregrounds.Add($"{ForegroundRoot}/snow.png");
                break;
            case "fog":
                foregrounds.Add($"{ForegroundRoot}/fog.png");
                break;
            case "lightning":
                backdrops.Add($"{BackdropRoot}/lightning.png");
                break;
        }

        return new WorldClockAtmosphere(phase, backdrops, foregrounds);
    }

    private static double? EventDistance(
        DateTimeOffset localTime,
        DateTimeOffset? localEvent,
        double startMinutes,
        double endMinutes)
    {
        if (localEvent is null)
        {
            return null;
        }

        var delta = (localTime - localEvent.Value).TotalMinutes;
        return delta >= startMinutes && delta < endMinutes ? Math.Abs(delta) : null;
    }
}

/// <summary>Calculates clocks locally and optionally attaches fresh current weather through a bounded Core service.</summary>
public sealed class WorldClockService : IDisposable
{
    private readonly string _catalogPath;
    private readonly WorldClockWeatherService _currentWeather;
    private readonly TimeProvider _timeProvider;
    private IReadOnlyDictionary<string, CityRecord>? _cities;

    /// <summary>Creates a service over the packaged catalog and the optional environment-configured weather provider.</summary>
    public WorldClockService(
        string? catalogPath = null,
        ILogger<WorldClockService>? logger = null)
        : this(
            catalogPath,
            OpenWeatherCurrentProvider.CreateFromEnvironment(),
            TimeProvider.System,
            logger)
    {
    }

    internal WorldClockService(
        string? catalogPath,
        IWorldClockWeatherProvider weatherProvider,
        TimeProvider timeProvider,
        ILogger<WorldClockService>? logger = null)
    {
        _catalogPath = Path.GetFullPath(catalogPath ?? Path.Combine(AppContext.BaseDirectory, "Assets", "WorldClocks", "world-clocks.sqlite3"));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _currentWeather = new WorldClockWeatherService(weatherProvider, _timeProvider, logger);
    }

    /// <summary>Gets the distributed 100-capital catalog plus the approved local city.</summary>
    public WorldClockCityCatalog GetCatalog()
    {
        var cities = LoadCities().Values
            .OrderByDescending(static city => city.IsCapital)
            .ThenBy(static city => city.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(static city => new WorldClockCitySummary(
                city.Id,
                city.Name,
                city.CountryCode,
                city.TimeZoneId,
                city.Latitude,
                city.Longitude,
                city.IsCapital))
            .ToArray();
        return new WorldClockCityCatalog(cities, WorldClockSelection.MaximumClocks);
    }

    /// <summary>Builds a deterministic world-clock snapshot for the supplied UTC instant.</summary>
    public WorldClockSnapshot BuildSnapshot(IReadOnlyList<string>? cityIds, DateTimeOffset utcInstant)
    {
        var selection = WorldClockSelection.NormalizePersisted(cityIds);
        var cities = LoadCities();
        return BuildSnapshotCore(
            selection,
            cities,
            utcInstant,
            new Dictionary<string, WorldClockWeather>(StringComparer.Ordinal),
            new WorldClockWeatherStatus(
                "openweather",
                "not-requested",
                "explicit-instant",
                selection.Count,
                0));
    }

    /// <summary>Builds the current snapshot and optionally enriches it with fresh cached weather.</summary>
    public async Task<WorldClockSnapshot> BuildCurrentSnapshotAsync(
        IReadOnlyList<string>? cityIds,
        CancellationToken cancellationToken)
    {
        var selection = WorldClockSelection.NormalizePersisted(cityIds);
        var cities = LoadCities();
        var locations = selection.Select(cityId =>
        {
            if (!cities.TryGetValue(cityId, out var city))
            {
                throw new InvalidDataException($"World-clock city '{cityId}' is not present in the distributed catalog.");
            }

            return new WorldClockWeatherLocation(city.Id, city.Latitude, city.Longitude);
        }).ToArray();
        var weather = await _currentWeather.LoadCurrentAsync(locations, cancellationToken).ConfigureAwait(false);
        // Project clocks after optional network work so the returned local times are current at completion.
        var instantUtc = _timeProvider.GetUtcNow();
        var snapshotWeather = _currentWeather.RevalidateForSnapshot(weather, instantUtc);
        return BuildSnapshotCore(
            selection,
            cities,
            instantUtc,
            snapshotWeather.Observations,
            snapshotWeather.Status);
    }

    private static WorldClockSnapshot BuildSnapshotCore(
        IReadOnlyList<string> selection,
        IReadOnlyDictionary<string, CityRecord> cities,
        DateTimeOffset utcInstant,
        IReadOnlyDictionary<string, WorldClockWeather> weatherByCity,
        WorldClockWeatherStatus weatherStatus)
    {
        var items = new List<WorldClockItem>(selection.Count);
        foreach (var cityId in selection)
        {
            if (!cities.TryGetValue(cityId, out var city))
            {
                throw new InvalidDataException($"World-clock city '{cityId}' is not present in the distributed catalog.");
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZoneId);
            var localTime = TimeZoneInfo.ConvertTime(utcInstant, timeZone);
            var events = LocalAstronomy.Calculate(city.Latitude, city.Longitude, timeZone, utcInstant);
            var season = ResolveSeason(city.Hemisphere, localTime.Month);
            var skylineRelativePath = season == "summer" ? city.SummerAssetPath : city.WinterAssetPath;
            var skylineAssetPath = $"Assets/WorldClocks/{skylineRelativePath}";
            var isDaylight = events.SunAltitudeDegrees >= -0.833;
            weatherByCity.TryGetValue(city.Id, out var weather);
            items.Add(new WorldClockItem(
                city.Id,
                city.Name,
                city.CountryCode,
                city.TimeZoneId,
                localTime,
                isDaylight,
                events.Sunrise,
                events.Sunset,
                events.MoonPhaseAngleDegrees,
                skylineAssetPath,
                season,
                WorldClockAtmosphereResolver.Resolve(
                    localTime,
                    events.Sunrise,
                    events.Sunset,
                    isDaylight,
                    weather?.ConditionKey),
                weather));
        }

        return new WorldClockSnapshot(
            utcInstant.ToUniversalTime(),
            items,
            WorldClockSelection.MaximumClocks,
            weatherStatus);
    }

    /// <summary>Resolves a selected city's local civil time and projects every selected clock at that instant.</summary>
    internal WorldClockSnapshot BuildSnapshotForLocalTime(
        IReadOnlyList<string>? cityIds,
        WorldClockConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var selection = WorldClockSelection.NormalizePersisted(cityIds);
        var referenceCityId = request.ReferenceCityId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!selection.Contains(referenceCityId, StringComparer.Ordinal))
        {
            throw new WorldClockConversionException(
                "world_clocks.reference_not_selected",
                "WorldClocksReferenceNotSelected",
                "reference_not_selected");
        }

        var cities = LoadCities();
        if (!cities.TryGetValue(referenceCityId, out var referenceCity))
        {
            throw new WorldClockConversionException(
                "world_clocks.reference_not_found",
                "WorldClocksNotFound",
                "not_found");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(referenceCity.TimeZoneId);
        var localTime = DateTime.SpecifyKind(request.ReferenceLocalTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localTime))
        {
            throw new WorldClockConversionException(
                "world_clocks.local_time.invalid",
                "WorldClocksLocalTimeInvalid",
                "invalid");
        }

        if (timeZone.IsAmbiguousTime(localTime))
        {
            throw new WorldClockConversionException(
                "world_clocks.local_time.ambiguous",
                "WorldClocksLocalTimeAmbiguous",
                "ambiguous");
        }

        var utcInstant = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone), TimeSpan.Zero);
        return BuildSnapshot(selection, utcInstant);
    }

    /// <summary>Throws when a city identifier is absent from the immutable packaged catalog.</summary>
    public void ValidateCityId(string cityId)
    {
        if (string.IsNullOrWhiteSpace(cityId) || !LoadCities().ContainsKey(cityId.Trim().ToLowerInvariant()))
        {
            throw new ArgumentException("City identifier is not present in the distributed world-clock catalog.", nameof(cityId));
        }
    }

    private IReadOnlyDictionary<string, CityRecord> LoadCities()
    {
        if (_cities is not null)
        {
            return _cities;
        }

        if (!File.Exists(_catalogPath))
        {
            // Missing product content is a packaging error: no online or hard-coded fallback is allowed.
            throw new FileNotFoundException("The distributed world-clock catalog is missing.", _catalogPath);
        }

        var builder = new SqliteConnectionStringBuilder { DataSource = _catalogPath, Mode = SqliteOpenMode.ReadOnly };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT value FROM catalog_metadata WHERE key = 'schema_version';";
        if (!string.Equals(versionCommand.ExecuteScalar() as string, "1", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The distributed world-clock catalog schema is unsupported.");
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.id, c.name, c.country_code, c.latitude, c.longitude, c.timezone_id,
                   c.is_capital, c.hemisphere,
                   summer.relative_path, winter.relative_path
            FROM city c
            JOIN skyline_asset summer ON summer.city_id = c.id AND summer.season = 'summer'
            JOIN skyline_asset winter ON winter.city_id = c.id AND winter.season = 'winter';
            """;
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, CityRecord>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var city = new CityRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9));
            if (!result.TryAdd(city.Id, city))
            {
                throw new InvalidDataException($"Duplicate city '{city.Id}' in the distributed world-clock catalog.");
            }
        }

        if (result.Values.Count(static city => city.IsCapital) != 100 || result.Count != 101)
        {
            throw new InvalidDataException("The distributed world-clock catalog must contain 100 capitals plus one local city.");
        }

        _cities = result;
        return result;
    }

    private static string ResolveSeason(string hemisphere, int month) => hemisphere switch
    {
        "north" => month is >= 4 and <= 9 ? "summer" : "winter",
        "south" => month is >= 10 or <= 3 ? "summer" : "winter",
        "equatorial" => month is >= 4 and <= 9 ? "summer" : "winter",
        _ => throw new InvalidDataException($"Unsupported season model '{hemisphere}'.")
    };

    private sealed record CityRecord(
        string Id,
        string Name,
        string CountryCode,
        double Latitude,
        double Longitude,
        string TimeZoneId,
        bool IsCapital,
        string Hemisphere,
        string SummerAssetPath,
        string WinterAssetPath);

    /// <summary>Releases optional current-weather cache timers.</summary>
    public void Dispose() => _currentWeather.Dispose();
}

internal sealed class WorldClockConversionException(
    string code,
    string messageKey,
    string validationCode) : Exception(messageKey)
{
    internal string Code { get; } = code;

    internal string MessageKey { get; } = messageKey;

    internal string ValidationCode { get; } = validationCode;
}

internal static class LocalAstronomy
{
    private const double DegreesToRadians = Math.PI / 180d;
    private const double RadiansToDegrees = 180d / Math.PI;

    internal sealed record Result(
        DateTimeOffset? Sunrise,
        DateTimeOffset? Sunset,
        double SunAltitudeDegrees,
        double MoonPhaseAngleDegrees);

    /// <summary>Calculates apparent rise/set crossings and the lunar phase for one local civil day.</summary>
    public static Result Calculate(double latitude, double longitude, TimeZoneInfo timeZone, DateTimeOffset utcNow)
    {
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);
        var (startUtc, endUtc) = GetUtcDayBounds(localDate, timeZone);

        var sunCrossings = FindCrossings(startUtc, endUtc, instant => Altitude(SunPosition(JulianDay(instant)), latitude, longitude, instant) + 0.833);
        var julianNow = JulianDay(utcNow);
        var sun = SunPosition(julianNow);
        var moon = MoonPosition(julianNow);
        var phaseAngle = NormalizeDegrees(moon.EclipticLongitudeDegrees - sun.EclipticLongitudeDegrees);
        return new Result(
            ToLocal(sunCrossings.Rise, timeZone),
            ToLocal(sunCrossings.Set, timeZone),
            Altitude(sun, latitude, longitude, utcNow),
            phaseAngle);
    }

    /// <summary>Resolves the first UTC instant of one local date and the following local date.</summary>
    internal static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetUtcDayBounds(
        DateOnly localDate,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var startUtc = ResolveLocalDateStart(localDate, timeZone);
        var endUtc = ResolveLocalDateStart(localDate.AddDays(1), timeZone);
        if (endUtc <= startUtc)
        {
            throw new InvalidDataException("The resolved local-day bounds are not chronological.");
        }

        return (startUtc, endUtc);
    }

    private static DateTimeOffset ResolveLocalDateStart(DateOnly localDate, TimeZoneInfo timeZone)
    {
        var localTime = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localTime))
        {
            var firstInvalidTick = localTime.Ticks;
            var firstValid = localTime;
            var searchLimit = localTime.AddDays(2);
            do
            {
                firstValid = firstValid.AddHours(1);
                if (firstValid > searchLimit)
                {
                    throw new InvalidDataException($"Local date '{localDate:yyyy-MM-dd}' has no valid boundary in time zone '{timeZone.Id}'.");
                }
            }
            while (timeZone.IsInvalidTime(firstValid));

            var lastInvalidTick = firstInvalidTick;
            var firstValidTick = firstValid.Ticks;
            while (firstValidTick - lastInvalidTick > 1)
            {
                var candidateTick = lastInvalidTick + ((firstValidTick - lastInvalidTick) / 2);
                var candidate = new DateTime(candidateTick, DateTimeKind.Unspecified);
                if (timeZone.IsInvalidTime(candidate))
                {
                    lastInvalidTick = candidateTick;
                }
                else
                {
                    firstValidTick = candidateTick;
                }
            }

            localTime = new DateTime(firstValidTick, DateTimeKind.Unspecified);
        }

        var offset = timeZone.IsAmbiguousTime(localTime)
            ? timeZone.GetAmbiguousTimeOffsets(localTime).Max()
            : timeZone.GetUtcOffset(localTime);
        return new DateTimeOffset(localTime, offset).ToUniversalTime();
    }

    private static (DateTimeOffset? Rise, DateTimeOffset? Set) FindCrossings(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        Func<DateTimeOffset, double> horizonFunction)
    {
        var step = TimeSpan.FromMinutes(5);
        var previousTime = startUtc;
        var previousValue = horizonFunction(previousTime);
        DateTimeOffset? rise = null;
        DateTimeOffset? set = null;
        for (var currentTime = startUtc + step; currentTime <= endUtc; currentTime += step)
        {
            var currentValue = horizonFunction(currentTime);
            if ((previousValue <= 0d && currentValue > 0d) || (previousValue > 0d && currentValue <= 0d))
            {
                var crossing = RefineCrossing(previousTime, currentTime, horizonFunction, previousValue <= 0d);
                if (previousValue <= 0d)
                {
                    rise ??= crossing;
                }
                else
                {
                    set ??= crossing;
                }
            }

            previousTime = currentTime;
            previousValue = currentValue;
        }

        return (rise, set);
    }

    private static DateTimeOffset RefineCrossing(
        DateTimeOffset lower,
        DateTimeOffset upper,
        Func<DateTimeOffset, double> horizonFunction,
        bool rising)
    {
        for (var iteration = 0; iteration < 14; iteration++)
        {
            var midpoint = lower + TimeSpan.FromTicks((upper - lower).Ticks / 2);
            var above = horizonFunction(midpoint) > 0d;
            if (above == rising)
            {
                upper = midpoint;
            }
            else
            {
                lower = midpoint;
            }
        }

        return lower + TimeSpan.FromTicks((upper - lower).Ticks / 2);
    }

    private static DateTimeOffset? ToLocal(DateTimeOffset? utc, TimeZoneInfo timeZone) =>
        utc is null ? null : TimeZoneInfo.ConvertTime(utc.Value, timeZone);

    private static EquatorialPosition SunPosition(double julianDay)
    {
        var days = julianDay - 2451545d;
        var meanAnomaly = NormalizeDegrees(357.52911 + 0.98560028 * days);
        var meanLongitude = NormalizeDegrees(280.46646 + 0.98564736 * days);
        var longitude = NormalizeDegrees(meanLongitude
            + 1.914602 * Sin(meanAnomaly)
            + 0.019993 * Sin(2d * meanAnomaly)
            + 0.000289 * Sin(3d * meanAnomaly));
        return FromEcliptic(longitude, 0d, days);
    }

    private static EquatorialPosition MoonPosition(double julianDay)
    {
        var days = julianDay - 2451545d;
        var meanLongitude = NormalizeDegrees(218.3164477 + 13.17639648 * days);
        var meanAnomaly = NormalizeDegrees(134.9633964 + 13.06499295 * days);
        var elongation = NormalizeDegrees(297.8501921 + 12.19074912 * days);
        var argumentLatitude = NormalizeDegrees(93.272095 + 13.22935024 * days);
        var solarAnomaly = NormalizeDegrees(357.5291092 + 0.98560028 * days);
        var longitude = meanLongitude
            + 6.289 * Sin(meanAnomaly)
            + 1.274 * Sin(2d * elongation - meanAnomaly)
            + 0.658 * Sin(2d * elongation)
            + 0.214 * Sin(2d * meanAnomaly)
            - 0.186 * Sin(solarAnomaly)
            - 0.059 * Sin(2d * elongation - 2d * meanAnomaly)
            - 0.057 * Sin(2d * elongation - solarAnomaly - meanAnomaly)
            + 0.053 * Sin(2d * elongation + meanAnomaly)
            + 0.046 * Sin(2d * elongation - solarAnomaly)
            + 0.041 * Sin(solarAnomaly - meanAnomaly);
        var latitude = 5.128 * Sin(argumentLatitude)
            + 0.280 * Sin(meanAnomaly + argumentLatitude)
            + 0.277 * Sin(meanAnomaly - argumentLatitude)
            + 0.173 * Sin(2d * elongation - argumentLatitude)
            + 0.055 * Sin(2d * elongation + argumentLatitude - meanAnomaly)
            + 0.046 * Sin(2d * elongation - argumentLatitude - meanAnomaly)
            + 0.033 * Sin(2d * elongation + argumentLatitude)
            + 0.017 * Sin(2d * meanAnomaly + argumentLatitude);
        return FromEcliptic(NormalizeDegrees(longitude), latitude, days);
    }

    private static EquatorialPosition FromEcliptic(double longitude, double latitude, double days)
    {
        var obliquity = (23.439291 - 0.00000036 * days) * DegreesToRadians;
        var lon = longitude * DegreesToRadians;
        var lat = latitude * DegreesToRadians;
        var x = Math.Cos(lon) * Math.Cos(lat);
        var y = Math.Sin(lon) * Math.Cos(lat) * Math.Cos(obliquity) - Math.Sin(lat) * Math.Sin(obliquity);
        var z = Math.Sin(lon) * Math.Cos(lat) * Math.Sin(obliquity) + Math.Sin(lat) * Math.Cos(obliquity);
        return new EquatorialPosition(
            NormalizeDegrees(Math.Atan2(y, x) * RadiansToDegrees),
            Math.Asin(z) * RadiansToDegrees,
            longitude);
    }

    private static double Altitude(EquatorialPosition position, double latitude, double longitude, DateTimeOffset utc)
    {
        var julianDay = JulianDay(utc);
        var centuries = (julianDay - 2451545d) / 36525d;
        var sidereal = NormalizeDegrees(280.46061837
            + 360.98564736629 * (julianDay - 2451545d)
            + 0.000387933 * centuries * centuries
            - centuries * centuries * centuries / 38710000d);
        var hourAngle = NormalizeSignedDegrees(sidereal + longitude - position.RightAscensionDegrees) * DegreesToRadians;
        var lat = latitude * DegreesToRadians;
        var dec = position.DeclinationDegrees * DegreesToRadians;
        return Math.Asin(Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(hourAngle)) * RadiansToDegrees;
    }

    private static double JulianDay(DateTimeOffset instant) => instant.ToUniversalTime().ToUnixTimeMilliseconds() / 86400000d + 2440587.5d;

    private static double Sin(double degrees) => Math.Sin(degrees * DegreesToRadians);

    private static double NormalizeDegrees(double value) => (value % 360d + 360d) % 360d;

    private static double NormalizeSignedDegrees(double value)
    {
        var normalized = NormalizeDegrees(value);
        return normalized > 180d ? normalized - 360d : normalized;
    }

    private readonly record struct EquatorialPosition(
        double RightAscensionDegrees,
        double DeclinationDegrees,
        double EclipticLongitudeDegrees);
}
