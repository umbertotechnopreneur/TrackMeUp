// SPDX-License-Identifier: MIT

namespace TrackMeUp.Presentation;

/// <summary>Contains bounded track geometry for a measured viewport, without scaling the text.</summary>
public sealed record SensorTrackLayout(bool Stacked, bool Dense, bool ShowSecondary, bool ShowCapacityCaption,
    double Padding, double GraphHeight);

/// <summary>Fits live devices to the available height and width instead of measuring an unbounded scrolling stack.</summary>
public static class SensorMonitorLayout
{
    /// <summary>Computes one row's content budget, retaining readable values even in a short window.</summary>
    public static SensorTrackLayout ResolveTrack(double width, double height, bool capacity, bool secondary)
    {
        Validate(width);
        Validate(height);
        var dense = height < 96;
        var stacked = width < 720 && !dense;
        var padding = dense ? 5d : 8d;
        var showSecondary = secondary && !capacity && !dense && height >= (stacked ? 112 : 80);
        var showCapacityCaption = capacity && !dense && height >= (stacked ? 120 : 86);
        var budget = height - padding * 2 - (stacked ? 56 : 0) - (showSecondary ? 20 : 0)
            - (capacity ? 10 : 0) - (showCapacityCaption ? 20 : 0);
        return new(stacked, dense, showSecondary, showCapacityCaption, padding, Math.Clamp(budget, 12, 80));
    }

    /// <summary>Gets the number of readable device rows on one page; normal laptop layouts fit in a single page.</summary>
    public static int PageSize(double viewportHeight, int deviceCount)
    {
        Validate(viewportHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(deviceCount);
        return Math.Min(deviceCount, Math.Max(1, (int)(viewportHeight / 48)));
    }

    private static void Validate(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
