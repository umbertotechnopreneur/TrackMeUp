// SPDX-License-Identifier: MIT

namespace TrackMeUp.Presentation;

/// <summary>Identifies the single top-level surface rendered by the detached world-clock window.</summary>
public enum WorldClockWindowSurface
{
    /// <summary>The equal-column clock comparison is visible.</summary>
    Clocks,

    /// <summary>The world-clock options surface is visible.</summary>
    Options
}

/// <summary>Describes the width projection for an equal-column world-clock surface.</summary>
public sealed record WorldClockColumnsLayout(double MinimumWidth, double Width, bool IsCentered);

/// <summary>Describes the mode rollback required after a failed reference-time conversion.</summary>
public sealed record WorldClockConversionFailureState(
    bool IsLive,
    bool CustomProjectionValid,
    bool RestoreLastSnapshotControls);

/// <summary>Owns the active world-clock surface and projects timer and viewport state.</summary>
public sealed class WorldClockWindowLayoutState
{
    private const double MinimumColumnWidth = 280d;
    private static readonly TimeSpan MinuteBoundaryMargin = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets the currently active top-level surface.</summary>
    public WorldClockWindowSurface Surface { get; private set; } = WorldClockWindowSurface.Clocks;

    /// <summary>Shows one top-level surface without resetting the current clock projection.</summary>
    /// <param name="surface">Surface to make current.</param>
    public void ShowSurface(WorldClockWindowSurface surface)
    {
        if (!Enum.IsDefined(surface))
        {
            throw new ArgumentOutOfRangeException(nameof(surface));
        }

        Surface = surface;
    }

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
        if (clockCount is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(clockCount), clockCount, "World clocks support one through twelve columns.");
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
