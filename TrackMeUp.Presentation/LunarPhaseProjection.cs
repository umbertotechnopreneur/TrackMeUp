// SPDX-License-Identifier: MIT

namespace TrackMeUp.Presentation;

/// <summary>Describes the localized phase name and illuminated lunar-disc percentage.</summary>
public sealed record LunarPhasePresentation(
    string LocalizationKey,
    double IlluminatedPercentage);

/// <summary>Projects the astronomy phase angle into stable presentation data.</summary>
public static class LunarPhaseProjection
{
    /// <summary>Creates an eight-phase presentation and a rounded illuminated percentage.</summary>
    public static LunarPhasePresentation Create(double phaseAngleDegrees)
    {
        if (!double.IsFinite(phaseAngleDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(phaseAngleDegrees));
        }

        var normalized = (phaseAngleDegrees % 360d + 360d) % 360d;
        var phaseIndex = (int)Math.Floor((normalized + 22.5d) / 45d) % 8;
        var localizationKey = phaseIndex switch
        {
            0 => "WorldClock.MoonPhase.New",
            1 => "WorldClock.MoonPhase.WaxingCrescent",
            2 => "WorldClock.MoonPhase.FirstQuarter",
            3 => "WorldClock.MoonPhase.WaxingGibbous",
            4 => "WorldClock.MoonPhase.Full",
            5 => "WorldClock.MoonPhase.WaningGibbous",
            6 => "WorldClock.MoonPhase.LastQuarter",
            7 => "WorldClock.MoonPhase.WaningCrescent",
            _ => throw new InvalidDataException("The lunar phase index is outside the supported range.")
        };
        var illuminatedPercentage = Math.Round(
            (1d - Math.Cos(normalized * Math.PI / 180d)) * 50d,
            MidpointRounding.AwayFromZero);
        return new LunarPhasePresentation(localizationKey, illuminatedPercentage);
    }
}
