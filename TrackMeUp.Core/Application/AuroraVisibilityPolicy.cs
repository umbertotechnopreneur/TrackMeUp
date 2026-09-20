// SPDX-License-Identifier: MIT

using CosineKitty;

namespace TrackMeUp.Application;

/// <summary>Filters significant aurora annotations by approximate magnetic latitude and local darkness; does not predict visibility probability.</summary>
internal static class AuroraVisibilityPolicy
{
    /// <summary>Checks the approximate Kp oval boundary plus a possible horizon-view allowance.</summary>
    internal static bool IsLatitudeEligible(double latitude, double longitude, double kp)
    {
        ValidateCoordinates(latitude, longitude);
        if (!double.IsFinite(kp) || kp is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(kp));
        if (kp < 5) return false; // Significant storm annotations, not general aurora monitoring.
        const double radians = Math.PI / 180;
        // NOAA NCEI WMM2025 centered dipole: 80.79 N geocentric, 72.76 W. Approximate, not corrected geomagnetic coordinates.
        // https://www.ncei.noaa.gov/products/wandering-geomagnetic-poles
        var geocentric = Math.Atan2(0.99330562001 * Math.Sin(latitude * radians), Math.Cos(latitude * radians));
        var magneticLatitude = Math.Asin(Math.Clamp(Math.Sin(geocentric) * Math.Sin(80.79 * radians)
            + Math.Cos(geocentric) * Math.Cos(80.79 * radians) * Math.Cos((longitude + 72.76) * radians), -1, 1)) / radians;
        // NOAA average oval edge: 66 - 2*Kp degrees. Nine degrees allow a possible horizon view (~1000 km).
        // https://www.swpc.noaa.gov/content/tips-viewing-aurora
        return Math.Abs(magneticLatitude) >= 66 - 2 * kp - 9;
    }

    /// <summary>Checks a three-hour forecast period for darkness at bounded half-hour samples.</summary>
    internal static bool HasDarkness(DateTimeOffset start, DateTimeOffset end, double latitude, double longitude)
    {
        if (end <= start || end - start > TimeSpan.FromHours(3)) throw new ArgumentOutOfRangeException(nameof(end));
        for (var time = start; time < end; time = time.AddMinutes(30))
            if (IsDark(time, latitude, longitude)) return true;
        return IsDark(end, latitude, longitude);
    }

    /// <summary>Requires the Sun to be at least six degrees below the local horizon, excluding daylight and polar day.</summary>
    internal static bool IsDark(DateTimeOffset instant, double latitude, double longitude)
    {
        ValidateCoordinates(latitude, longitude);
        var time = new AstroTime(instant.UtcDateTime);
        var observer = new Observer(latitude, longitude, 0);
        var equator = Astronomy.Equator(Body.Sun, time, observer, EquatorEpoch.OfDate, Aberration.Corrected);
        return Astronomy.Horizon(time, observer, equator.ra, equator.dec, Refraction.Normal).altitude <= -6;
    }

    /// <summary>Rejects invalid coordinates rather than assigning a different location.</summary>
    internal static void ValidateCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
    }
}
