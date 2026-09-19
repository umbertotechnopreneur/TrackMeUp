// SPDX-License-Identifier: MIT

using System.Globalization;

namespace TrackMeUp.Application;

/// <summary>Provides decorative sky colors and visibility weights for a calculated solar altitude.</summary>
public sealed record CelestialSkyAppearance(
    string ZenithColor, string UpperSkyColor, string HorizonColor, double StarOpacity, double SunGlowOpacity);

/// <summary>Interpolates twenty-four artistic skies against actual solar elevation, including polar day/night.</summary>
public static class CelestialSkyPalette
{
    /// <summary>Number of smoothly blended sky keyframes; these are not fixed civil-hour or weather forecasts.</summary>
    public const int PaletteCount = 24;

    private static readonly Frame[] Frames =
    [
        new(-90, "050A18", "080F20", "101A2C", 1, 0),
        new(-50, "060B1B", "091224", "111C31", 1, 0),
        new(-30, "070D21", "0C1730", "14213D", 1, 0),
        new(-24, "080F26", "101C3A", "1A2C50", 1, 0),
        new(-20, "09112D", "142245", "213961", 1, 0),
        new(-18, "0B1534", "19294E", "2B436F", 1, 0.02),
        new(-15, "101B3E", "22375D", "3D527D", 0.92, 0.06),
        new(-12, "15234A", "304369", "625C83", 0.78, 0.14),
        new(-9, "1A2B54", "464F73", "A47389", 0.52, 0.30),
        new(-6, "20355F", "625C7E", "D3948B", 0.22, 0.58),
        new(-4, "263E6A", "7B6986", "EDAD88", 0.09, 0.82),
        new(-2, "2B4774", "91788A", "F6C391", 0.02, 1),
        new(0, "30517F", "9B8B99", "F9D69F", 0, 1),
        new(2, "335B8C", "999FAF", "F6DDAE", 0, 0.94),
        new(4, "326598", "90ACC1", "EFDFBC", 0, 0.80),
        new(6, "2D6DA3", "84B8CF", "E0DFC7", 0, 0.65),
        new(9, "2875AD", "79C0DA", "D0E1D3", 0, 0.46),
        new(12, "237BB5", "70C3E1", "C4E3DC", 0, 0.32),
        new(18, "1F7EBB", "65C1E4", "BAE3E3", 0, 0.18),
        new(25, "1B79B8", "59B9E0", "B3E1E7", 0, 0.10),
        new(35, "196FB0", "4DAEDA", "ACDFE8", 0, 0.06),
        new(50, "18619E", "419FD2", "A9DDE8", 0, 0.04),
        new(70, "17538C", "3592CA", "A9DDEB", 0, 0.02),
        new(90, "174C82", "2E8CC6", "A9DDEA", 0, 0.02)
    ];

    /// <summary>Creates a continuous palette; invalid elevations fail instead of inventing a sky state.</summary>
    public static CelestialSkyAppearance Create(double solarAltitudeDegrees)
    {
        if (!double.IsFinite(solarAltitudeDegrees) || solarAltitudeDegrees is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(solarAltitudeDegrees), "Solar altitude must be finite and within the physical horizon range.");

        var upperIndex = Array.FindIndex(Frames, frame => frame.Altitude >= solarAltitudeDegrees);
        var lower = Frames[Math.Max(0, upperIndex - 1)];
        var upper = Frames[upperIndex];
        var blend = upper.Altitude == lower.Altitude ? 0 : (solarAltitudeDegrees - lower.Altitude) / (upper.Altitude - lower.Altitude);
        return new CelestialSkyAppearance(
            BlendColor(lower.Zenith, upper.Zenith, blend),
            BlendColor(lower.UpperSky, upper.UpperSky, blend),
            BlendColor(lower.Horizon, upper.Horizon, blend),
            lower.Stars + (upper.Stars - lower.Stars) * blend,
            lower.Glow + (upper.Glow - lower.Glow) * blend);
    }

    private static string BlendColor(string first, string second, double blend)
    {
        var channels = new byte[3];
        for (var channel = 0; channel < 3; channel++)
        {
            var a = byte.Parse(first.AsSpan(channel * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(second.AsSpan(channel * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            channels[channel] = (byte)Math.Round(a + (b - a) * blend);
        }
        return $"#{channels[0]:X2}{channels[1]:X2}{channels[2]:X2}";
    }

    private sealed record Frame(double Altitude, string Zenith, string UpperSky, string Horizon, double Stars, double Glow);
}
