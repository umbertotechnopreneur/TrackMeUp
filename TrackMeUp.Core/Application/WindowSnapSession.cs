// SPDX-License-Identifier: MIT

namespace TrackMeUp.Application;

/// <summary>Describes visible window edges in physical desktop pixels, including negative monitor coordinates.</summary>
public readonly record struct WindowSnapRectangle(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Gets the horizontal extent; unrepresentable extents fail instead of overflowing.</summary>
    public int Width => checked(Right - Left);

    /// <summary>Gets the vertical extent; unrepresentable extents fail instead of overflowing.</summary>
    public int Height => checked(Bottom - Top);
}

/// <summary>Calculates five-pixel edge snapping for one drag; leaving the starting monitor suppresses its remaining moves.</summary>
public sealed class WindowSnapSession
{
    private const int SnapDistance = 5;
    private readonly WindowSnapRectangle _monitorBounds;
    private readonly WindowSnapRectangle _workArea;

    /// <summary>Starts a drag on a physical monitor and its contained work area; invalid or unrepresentable rectangles fail.</summary>
    public WindowSnapSession(WindowSnapRectangle monitorBounds, WindowSnapRectangle workArea)
    {
        Validate(monitorBounds, nameof(monitorBounds));
        Validate(workArea, nameof(workArea));
        if (!Contains(monitorBounds, workArea))
        {
            throw new ArgumentException("The work area must be contained within the monitor bounds.", nameof(workArea));
        }

        _monitorBounds = monitorBounds;
        _workArea = workArea;
    }

    /// <summary>Gets whether a raw proposal has left the starting monitor, permanently disabling snap until a new session.</summary>
    public bool IsSuppressed { get; private set; }

    /// <summary>Gets whether the last valid move matched an eligible edge, including an exact zero-distance match.</summary>
    public bool IsSnapped { get; private set; }

    /// <summary>
    /// Translates raw visible bounds toward nearby work-area or peer edges without resizing or clamping.
    /// Peers must overlap or lie within five pixels on the perpendicular axis. Equal-distance targets
    /// resolve toward the smaller desktop coordinate, independently of peer order. Every input is validated,
    /// including after suppression; callers must pass unsnapped bounds to avoid making an edge sticky.
    /// </summary>
    public WindowSnapRectangle Move(WindowSnapRectangle proposedBounds, IReadOnlyList<WindowSnapRectangle> peerBounds)
    {
        ArgumentNullException.ThrowIfNull(peerBounds);
        Validate(proposedBounds, nameof(proposedBounds));
        foreach (var peer in peerBounds)
        {
            Validate(peer, nameof(peerBounds));
        }

        if (!Contains(_monitorBounds, proposedBounds))
        {
            // Exiting the physical monitor releases snapping even when the next raw move re-enters it.
            IsSuppressed = true;
        }

        if (IsSuppressed)
        {
            IsSnapped = false;
            return proposedBounds;
        }

        int? horizontal = null;
        int? vertical = null;
        Consider(ref horizontal, (long)_workArea.Left - proposedBounds.Left,
            proposedBounds.Left, proposedBounds.Right, _monitorBounds.Left, _monitorBounds.Right);
        Consider(ref horizontal, (long)_workArea.Right - proposedBounds.Right,
            proposedBounds.Left, proposedBounds.Right, _monitorBounds.Left, _monitorBounds.Right);
        Consider(ref vertical, (long)_workArea.Top - proposedBounds.Top,
            proposedBounds.Top, proposedBounds.Bottom, _monitorBounds.Top, _monitorBounds.Bottom);
        Consider(ref vertical, (long)_workArea.Bottom - proposedBounds.Bottom,
            proposedBounds.Top, proposedBounds.Bottom, _monitorBounds.Top, _monitorBounds.Bottom);

        foreach (var peer in peerBounds)
        {
            if (NearIntervals(proposedBounds.Top, proposedBounds.Bottom, peer.Top, peer.Bottom))
            {
                ConsiderEdges(ref horizontal, proposedBounds.Left, proposedBounds.Right,
                    peer.Left, peer.Right, _monitorBounds.Left, _monitorBounds.Right);
            }

            if (NearIntervals(proposedBounds.Left, proposedBounds.Right, peer.Left, peer.Right))
            {
                ConsiderEdges(ref vertical, proposedBounds.Top, proposedBounds.Bottom,
                    peer.Top, peer.Bottom, _monitorBounds.Top, _monitorBounds.Bottom);
            }
        }

        var deltaX = horizontal ?? 0;
        var deltaY = vertical ?? 0;
        IsSnapped = horizontal.HasValue || vertical.HasValue;
        return new WindowSnapRectangle(
            checked(proposedBounds.Left + deltaX), checked(proposedBounds.Top + deltaY),
            checked(proposedBounds.Right + deltaX), checked(proposedBounds.Bottom + deltaY));
    }

    private static void ConsiderEdges(ref int? best, int first, int last, int targetFirst, int targetLast, int monitorFirst, int monitorLast)
    {
        Consider(ref best, (long)targetFirst - first, first, last, monitorFirst, monitorLast);
        Consider(ref best, (long)targetLast - last, first, last, monitorFirst, monitorLast);
        Consider(ref best, (long)targetLast - first, first, last, monitorFirst, monitorLast);
        Consider(ref best, (long)targetFirst - last, first, last, monitorFirst, monitorLast);
    }

    private static void Consider(ref int? best, long delta, int first, int last, int monitorFirst, int monitorLast)
    {
        if (Math.Abs(delta) > SnapDistance || first + delta < monitorFirst || last + delta > monitorLast)
        {
            return;
        }

        // Compare in widened arithmetic because valid desktop rectangles may straddle extreme signed coordinates.
        if (best is null || Math.Abs(delta) < Math.Abs(best.Value)
            || (Math.Abs(delta) == Math.Abs(best.Value) && delta < best.Value))
        {
            best = (int)delta;
        }
    }

    private static bool NearIntervals(int first, int last, int peerFirst, int peerLast) =>
        (long)first <= (long)peerLast + SnapDistance && (long)peerFirst <= (long)last + SnapDistance;

    private static bool Contains(WindowSnapRectangle container, WindowSnapRectangle value) =>
        value.Left >= container.Left && value.Right <= container.Right
        && value.Top >= container.Top && value.Bottom <= container.Bottom;

    private static void Validate(WindowSnapRectangle bounds, string parameterName)
    {
        var width = (long)bounds.Right - bounds.Left;
        var height = (long)bounds.Bottom - bounds.Top;
        if (width is <= 0 or > int.MaxValue || height is <= 0 or > int.MaxValue)
        {
            throw new ArgumentException("Snap rectangles must have positive, representable physical pixel extents.", parameterName);
        }
    }
}
