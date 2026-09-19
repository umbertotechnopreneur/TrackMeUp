// SPDX-License-Identifier: MIT

namespace TrackMeUp.Presentation;

/// <summary>Shares column widths and bounded row geometry between the monitor header and its devices.</summary>
public sealed record SensorTrackLayout(bool Stacked, double NameWidth, double ValueWidth, double TemperatureWidth,
    double Padding, double RowHeight);

/// <summary>Keeps identity, capacity and temperature readable by paging short or narrow windows.</summary>
public static class SensorMonitorLayout
{
    /// <summary>Computes aligned desktop columns or two compact lines over a background trace.</summary>
    public static SensorTrackLayout ResolveTrack(double width)
    {
        Validate(width);
        var stacked = width < 860;
        var valueWidth = stacked ? 132 : Math.Clamp(width * .18, 116, 160);
        var temperatureWidth = stacked ? 0 : Math.Clamp(width * .20, 132, 180);
        // The icon and three gutters occupy 76 px; the graph shares the entire row behind these columns.
        return new(stacked, Math.Max(0, width - valueWidth - temperatureWidth - 76), valueWidth,
            temperatureWidth, 10, stacked ? 148 : 96);
    }

    /// <summary>Pages devices before their metadata would need to be hidden or text scaled down.</summary>
    public static int PageSize(double viewportHeight, double viewportWidth, int deviceCount)
    {
        Validate(viewportHeight);
        Validate(viewportWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(deviceCount);
        return Math.Min(deviceCount, Math.Max(1, (int)(viewportHeight / ResolveTrack(viewportWidth).RowHeight)));
    }

    private static void Validate(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
