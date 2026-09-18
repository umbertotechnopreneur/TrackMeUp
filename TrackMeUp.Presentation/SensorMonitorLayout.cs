// SPDX-License-Identifier: MIT

namespace TrackMeUp.Presentation;

/// <summary>Shares column widths and bounded row geometry between the monitor header and its devices.</summary>
public sealed record SensorTrackLayout(bool Stacked, double NameWidth, double ValueWidth, double TemperatureWidth,
    double Padding, double GraphHeight);

/// <summary>Keeps identity, capacity and temperature readable by paging short or narrow windows.</summary>
public static class SensorMonitorLayout
{
    /// <summary>Computes aligned desktop columns or a three-line layout for narrow windows.</summary>
    public static SensorTrackLayout ResolveTrack(double width, double height)
    {
        Validate(width);
        Validate(height);
        var stacked = width < 860;
        return new(stacked, Math.Clamp(width * .30, 220, 340), stacked ? 110 : Math.Clamp(width * .14, 116, 160),
            stacked ? 132 : Math.Clamp(width * .16, 132, 180), 10,
            Math.Clamp(height - 20 - (stacked ? 124 : 0), 12, 64));
    }

    /// <summary>Pages devices before their metadata would need to be hidden or text scaled down.</summary>
    public static int PageSize(double viewportHeight, double viewportWidth, int deviceCount)
    {
        Validate(viewportHeight);
        Validate(viewportWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(deviceCount);
        return Math.Min(deviceCount, Math.Max(1, (int)(viewportHeight / (viewportWidth < 860 ? 192 : 96))));
    }

    private static void Validate(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
