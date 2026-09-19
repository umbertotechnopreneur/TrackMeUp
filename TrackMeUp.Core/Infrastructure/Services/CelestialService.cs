// SPDX-License-Identifier: MIT

using CosineKitty;
using TrackMeUp.Application;

namespace TrackMeUp.Services;

/// <summary>Calculates offline astronomical ephemerides without weather, location discovery, or presentation dependencies.</summary>
public static class CelestialService
{
    private static readonly object SnapshotGate = new();
    private static readonly Dictionary<(WorldClockCitySummary City, long UtcTicks), CelestialSnapshot> SnapshotCache = new();

    // J2000 ICRS coordinates and V magnitudes: SIMBAD/CDS, queried 2026-09-19.
    // Coordinates are scientific facts; attribution and limitations are recorded in THIRD_PARTY_NOTICES.md.
    private static readonly Star[] CatalogStars =
    [
        new("alf-ori", "Betelgeuse", 88.792939, 7.407064, 0.42),
        new("bet-ori", "Rigel", 78.634467, -8.201638, 0.13),
        new("gam-ori", "Bellatrix", 81.282764, 6.349703, 1.64),
        new("del-ori", "Mintaka", 83.001667, -0.299095, 2.41),
        new("eps-ori", "Alnilam", 84.053389, -1.201919, 1.69),
        new("zet-ori", "Alnitak", 85.189694, -1.942574, 1.77),
        new("kap-ori", "Saiph", 86.939120, -9.669605, 2.06),
        new("alf-cas", "Schedar", 10.126846, 56.537329, 2.23),
        new("bet-cas", "Caph", 2.294522, 59.149781, 2.27),
        new("gam-cas", "Gamma Cassiopeiae", 14.177213, 60.716740, 2.39),
        new("del-cas", "Ruchbah", 21.453964, 60.235284, 2.68),
        new("eps-cas", "Segin", 28.598892, 63.670100, 3.37),
        new("alf-uma", "Dubhe", 165.931965, 61.751035, 1.79),
        new("bet-uma", "Merak", 165.460332, 56.382434, 2.37),
        new("gam-uma", "Phecda", 178.457697, 53.694760, 2.44),
        new("del-uma", "Megrez", 183.856499, 57.032617, 3.32),
        new("eps-uma", "Alioth", 193.507290, 55.959823, 1.77),
        new("zet-uma", "Mizar", 200.981419, 54.925352, null),
        new("eta-uma", "Alkaid", 206.885157, 49.313267, 1.86),
        new("alf-cru", "Acrux", 186.649563, -63.099093, null),
        new("bet-cru", "Mimosa", 191.930287, -59.688772, 1.25),
        new("gam-cru", "Gacrux", 187.791498, -57.113213, 1.64),
        new("del-cru", "Imai", 183.786327, -58.748924, 2.752)
    ];

    private static readonly CelestialConstellationSegment[] Segments =
    [
        new("Orion", "alf-ori", "gam-ori"), new("Orion", "gam-ori", "del-ori"),
        new("Orion", "del-ori", "eps-ori"), new("Orion", "eps-ori", "zet-ori"),
        new("Orion", "zet-ori", "alf-ori"), new("Orion", "zet-ori", "kap-ori"),
        new("Orion", "kap-ori", "bet-ori"), new("Orion", "bet-ori", "del-ori"),
        new("Cassiopeia", "bet-cas", "alf-cas"), new("Cassiopeia", "alf-cas", "gam-cas"),
        new("Cassiopeia", "gam-cas", "del-cas"), new("Cassiopeia", "del-cas", "eps-cas"),
        new("UrsaMajor", "alf-uma", "bet-uma"), new("UrsaMajor", "bet-uma", "gam-uma"),
        new("UrsaMajor", "gam-uma", "del-uma"), new("UrsaMajor", "del-uma", "alf-uma"),
        new("UrsaMajor", "del-uma", "eps-uma"), new("UrsaMajor", "eps-uma", "zet-uma"),
        new("UrsaMajor", "zet-uma", "eta-uma"), new("Crux", "alf-cru", "gam-cru"),
        new("Crux", "bet-cru", "del-cru")
    ];

    /// <summary>Builds ephemerides for a catalog city; rejects missing cities, invalid coordinates/time zones, and unsupported dates. Cancellation propagates.</summary>
    public static CelestialSnapshot Build(CelestialRequest request, WorldClockCityCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(catalog);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CityId);
        var instant = request.InstantUtc.ToUniversalTime();
        if (instant.Year is < 1900 or > 2100)
        {
            // The product deliberately bounds its ephemeris range instead of suggesting historical/future precision.
            throw new ArgumentOutOfRangeException(nameof(request), "Celestial dates must be between 1900 and 2100.");
        }

        var city = catalog.Cities.SingleOrDefault(city => string.Equals(city.Id, request.CityId, StringComparison.Ordinal))
            ?? throw new ArgumentException("The celestial city is not in the approved catalog.", nameof(request));
        if (!double.IsFinite(city.Latitude) || city.Latitude is < -90 or > 90 ||
            !double.IsFinite(city.Longitude) || city.Longitude is < -180 or > 180)
        {
            throw new ArgumentException("The celestial city has invalid geographic coordinates.", nameof(catalog));
        }

        // Installed time-zone rules are authoritative; missing/corrupt rules fail rather than fall back to UTC.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZoneId);
        lock (SnapshotGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (city, instant.UtcTicks);
            if (SnapshotCache.TryGetValue(key, out var cached)) return cached;
            var snapshot = BuildCore(city, instant, zone, cancellationToken);
            if (SnapshotCache.Count >= 16) SnapshotCache.Remove(SnapshotCache.Keys.First());
            SnapshotCache.Add(key, snapshot);
            return snapshot;
        }
    }

    private static CelestialSnapshot BuildCore(WorldClockCitySummary city, DateTimeOffset instant, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var observer = new Observer(city.Latitude, city.Longitude, 0);
        var time = new AstroTime(instant.UtcDateTime);
        var bodies = Enum.GetValues<CelestialBodyKind>().Select(kind =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = Enum.Parse<Body>(kind.ToString());
            var equator = Astronomy.Equator(body, time, observer, EquatorEpoch.OfDate, Aberration.Corrected);
            var horizon = Astronomy.Horizon(time, observer, equator.ra, equator.dec, Refraction.Normal);
            return new CelestialBodyPosition(kind, horizon.altitude, horizon.azimuth, horizon.altitude >= 0);
        }).ToArray();

        var rotation = Astronomy.Rotation_EQJ_EQD(time);
        var stars = CatalogStars.Select(star =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = Astronomy.VectorFromSphere(new Spherical(star.Declination, star.RightAscension, 1), time);
            var equator = Astronomy.EquatorFromVector(Astronomy.RotateVector(rotation, vector));
            var horizon = Astronomy.Horizon(time, observer, equator.ra, equator.dec, Refraction.Normal);
            return new CelestialStarPosition(star.Id, star.Name, horizon.altitude, horizon.azimuth, star.Magnitude);
        }).ToArray();

        return new CelestialSnapshot(city.Id, city.Name, city.TimeZoneId, instant, TimeZoneInfo.ConvertTime(instant, zone),
            city.Latitude, city.Longitude, LocalAstronomy.CalculateGlobal(instant).MoonPhaseAngleDegrees, bodies[0].AltitudeDegrees,
            Array.AsReadOnly(bodies), Array.AsReadOnly(stars), Array.AsReadOnly(Segments), BuildAgenda(time, observer, zone, cancellationToken));
    }

    private static IReadOnlyList<CelestialAgendaEvent> BuildAgenda(AstroTime time, Observer observer, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var events = new List<CelestialAgendaEvent>();
        var end = time.AddDays(2);
        AddSolarEvents(CelestialEventKind.Sunrise, Direction.Rise, null);
        AddSolarEvents(CelestialEventKind.Sunset, Direction.Set, null);
        AddSolarEvents(CelestialEventKind.CivilDawn, Direction.Rise, -6);
        AddSolarEvents(CelestialEventKind.CivilDusk, Direction.Set, -6);
        AddBlueHours(Direction.Rise, CelestialEventKind.MorningBlueHour, -6, -4);
        AddBlueHours(Direction.Set, CelestialEventKind.EveningBlueHour, -4, -6);

        var quarter = Astronomy.SearchMoonQuarter(time);
        for (var index = 0; index < 4; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kind = quarter.quarter switch
            {
                0 => CelestialEventKind.NewMoon,
                1 => CelestialEventKind.FirstQuarter,
                2 => CelestialEventKind.FullMoon,
                3 => CelestialEventKind.LastQuarter,
                _ => throw new InvalidOperationException("Astronomy Engine returned an unsupported lunar quarter.")
            };
            AddEvent(kind, quarter.time);
            if (index < 3) quarter = Astronomy.NextMoonQuarter(quarter);
        }

        var year = time.ToUtcDateTime().Year;
        var seasons = new List<(CelestialEventKind Kind, AstroTime Time)>();
        for (var nextYear = year; nextYear <= year + 1; nextYear++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var season = Astronomy.Seasons(nextYear);
            seasons.Add((CelestialEventKind.MarchEquinox, season.mar_equinox));
            seasons.Add((CelestialEventKind.JuneSolstice, season.jun_solstice));
            seasons.Add((CelestialEventKind.SeptemberEquinox, season.sep_equinox));
            seasons.Add((CelestialEventKind.DecemberSolstice, season.dec_solstice));
        }

        var nextSeason = seasons.Where(item => item.Time.ut >= time.ut).MinBy(item => item.Time.ut);
        AddEvent(nextSeason.Kind, nextSeason.Time);
        return Array.AsReadOnly(events.OrderBy(item => item.StartUtc).ThenBy(item => item.Kind).ToArray());

        void AddEvent(CelestialEventKind kind, AstroTime start, AstroTime? finish = null)
        {
            var startUtc = new DateTimeOffset(start.ToUtcDateTime());
            DateTimeOffset? endUtc = finish is null ? null : new DateTimeOffset(finish.ToUtcDateTime());
            events.Add(new(kind, startUtc, endUtc, TimeZoneInfo.ConvertTime(startUtc, zone),
                endUtc is { } value ? TimeZoneInfo.ConvertTime(value, zone) : null));
        }

        void AddSolarEvents(CelestialEventKind kind, Direction direction, double? altitude)
        {
            var cursor = time;
            while (cursor.ut < end.ut)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // A null crossing is normal in polar day/night; do not invent sunrise or substitute another date.
                var crossing = altitude is { } degrees
                    ? Astronomy.SearchAltitude(Body.Sun, observer, direction, cursor, end.ut - cursor.ut, degrees)
                    : Astronomy.SearchRiseSet(Body.Sun, observer, direction, cursor, end.ut - cursor.ut);
                if (crossing is null || crossing.ut >= end.ut) break;
                AddEvent(kind, crossing);
                cursor = crossing.AddDays(1.0 / 86400);
            }
        }

        void AddBlueHours(Direction direction, CelestialEventKind kind, double startAltitude, double endAltitude)
        {
            // Look back one day to include an interval already in progress at the requested instant.
            var cursor = time.AddDays(-1);
            while (cursor.ut < end.ut)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = Astronomy.SearchAltitude(Body.Sun, observer, direction, cursor, end.ut - cursor.ut, startAltitude);
                if (start is null || start.ut >= end.ut) break;
                var finish = Astronomy.SearchAltitude(Body.Sun, observer, direction, start, 1, endAltitude);
                var reversal = Astronomy.SearchAltitude(Body.Sun, observer,
                    direction == Direction.Rise ? Direction.Set : Direction.Rise, start.AddDays(1.0 / 86400), 1, startAltitude);
                // At high latitudes the Sun can turn back before the other threshold. Such partial intervals are omitted.
                if (finish is not null && finish.ut > time.ut && (reversal is null || finish.ut < reversal.ut))
                    AddEvent(kind, start, finish);
                cursor = start.AddDays(1.0 / 86400);
            }
        }
    }

    private sealed record Star(string Id, string Name, double RightAscension, double Declination, double? Magnitude);
}
