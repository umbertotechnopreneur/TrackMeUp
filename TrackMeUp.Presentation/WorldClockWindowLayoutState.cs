// SPDX-License-Identifier: MIT

namespace TrackMeUp.Presentation;

/// <summary>Describes the width projection for an equal-column world-clock surface.</summary>
public sealed record WorldClockColumnsLayout(double MinimumWidth, double Width, bool IsCentered);

/// <summary>Describes the mode rollback required after a failed reference-time conversion.</summary>
public sealed record WorldClockConversionFailureState(
    bool IsLive,
    bool CustomProjectionValid,
    bool RestoreLastSnapshotControls);

/// <summary>Projects timer and viewport state for the independent world-clock window.</summary>
public static class WorldClockWindowLayoutState
{
    private const double MinimumColumnWidth = 280d;
    private static readonly TimeSpan MinuteBoundaryMargin = TimeSpan.FromMilliseconds(100);

    /// <summary>Returns a one-shot delay that lands just after the next UTC minute boundary.</summary>
    public static TimeSpan DelayUntilNextMinute(DateTimeOffset instant)
    {
        var ticksIntoMinute = instant.UtcDateTime.Ticks % TimeSpan.TicksPerMinute;
        var ticksUntilNextMinute = TimeSpan.TicksPerMinute - ticksIntoMinute;
        return TimeSpan.FromTicks(ticksUntilNextMinute) + MinuteBoundaryMargin;
    }

    /// <summary>Calculates an equal-column width that scrolls instead of compressing narrow content.</summary>
    public static WorldClockColumnsLayout CalculateColumnsLayout(int clockCount, double viewportWidth)
    {
        if (clockCount is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(clockCount), clockCount, "World clocks support one through four columns.");
        }

        if (!double.IsFinite(viewportWidth) || viewportWidth < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), viewportWidth, "Viewport width must be finite and non-negative.");
        }

        var minimumWidth = MinimumColumnWidth * clockCount;
        var maximumWidth = clockCount switch
        {
            1 => 390d,
            2 => 780d,
            _ => double.PositiveInfinity
        };
        var availableWidth = viewportWidth > 0d ? viewportWidth : minimumWidth;
        var width = Math.Max(minimumWidth, Math.Min(availableWidth, maximumWidth));
        return new WorldClockColumnsLayout(minimumWidth, width, clockCount <= 2 && availableWidth > width);
    }

    /// <summary>Keeps an existing explicit projection, or transactionally restores a live projection.</summary>
    public static WorldClockConversionFailureState ResolveConversionFailure(bool transitionStartedFromLive) =>
        transitionStartedFromLive
            ? new WorldClockConversionFailureState(
                IsLive: true,
                CustomProjectionValid: true,
                RestoreLastSnapshotControls: true)
            : new WorldClockConversionFailureState(
                IsLive: false,
                CustomProjectionValid: false,
                RestoreLastSnapshotControls: false);
}
