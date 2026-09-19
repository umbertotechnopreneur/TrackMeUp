// SPDX-License-Identifier: MIT

namespace TrackMeUp.Application;

/// <summary>Requests a city-based ephemeris for an instant between 1900 and 2100 inclusive.</summary>
public sealed record CelestialRequest(string CityId, DateTimeOffset InstantUtc);

/// <summary>Identifies the Sun, Moon, or a planet visible from Earth.</summary>
public enum CelestialBodyKind { Sun, Moon, Mercury, Venus, Mars, Jupiter, Saturn, Uranus, Neptune }

/// <summary>Contains apparent horizontal coordinates; above the horizon does not guarantee visibility through daylight or weather.</summary>
public sealed record CelestialBodyPosition(CelestialBodyKind Kind, double AltitudeDegrees, double AzimuthDegrees, bool IsAboveHorizon);

/// <summary>Contains a fixed catalog star projected with precession and nutation; proper motion is omitted and missing V magnitudes remain null.</summary>
public sealed record CelestialStarPosition(string Id, string Name, double AltitudeDegrees, double AzimuthDegrees, double? Magnitude);

/// <summary>Connects catalog star identifiers in a schematic constellation figure, not an IAU boundary.</summary>
public sealed record CelestialConstellationSegment(string ConstellationId, string StartStarId, string EndStarId);

/// <summary>Identifies calculated solar, lunar, and seasonal events; blue hour uses geometric solar altitude from -6 to -4 degrees.</summary>
public enum CelestialEventKind
{
    Sunrise, Sunset, CivilDawn, CivilDusk, MorningBlueHour, EveningBlueHour,
    NewMoon, FirstQuarter, FullMoon, LastQuarter,
    MarchEquinox, JuneSolstice, SeptemberEquinox, DecemberSolstice
}

/// <summary>Contains event instants and city-local offsets; interval endpoints are null for instantaneous events.</summary>
public sealed record CelestialAgendaEvent(
    CelestialEventKind Kind, DateTimeOffset StartUtc, DateTimeOffset? EndUtc,
    DateTimeOffset StartLocal, DateTimeOffset? EndLocal);

/// <summary>Contains one source-backed local sky and a sorted agenda: solar events over 48 hours, four lunar quarters, and the next season.</summary>
public sealed record CelestialSnapshot(
    string CityId, string CityName, string TimeZoneId,
    DateTimeOffset InstantUtc, DateTimeOffset LocalTime,
    double Latitude, double Longitude, double MoonPhaseAngleDegrees, double SunAltitudeDegrees,
    IReadOnlyList<CelestialBodyPosition> Bodies, IReadOnlyList<CelestialStarPosition> Stars,
    IReadOnlyList<CelestialConstellationSegment> ConstellationSegments, IReadOnlyList<CelestialAgendaEvent> Agenda);
