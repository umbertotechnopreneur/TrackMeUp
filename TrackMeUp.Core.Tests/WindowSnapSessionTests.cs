// SPDX-License-Identifier: MIT

using System;
using TrackMeUp.Application;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class WindowSnapSessionTests
{
    private static readonly WindowSnapRectangle Monitor = new(0, 0, 1000, 1000);

    /// <summary>All four working-area edges snap at five physical pixels, with no resizing.</summary>
    [Theory]
    [InlineData(5, 200, 105, 300, 0, 200, 100, 300)]
    [InlineData(895, 200, 995, 300, 900, 200, 1000, 300)]
    [InlineData(200, 5, 300, 105, 200, 0, 300, 100)]
    [InlineData(200, 895, 300, 995, 200, 900, 300, 1000)]
    public void Move_SnapsAllWorkAreaEdgesWithinFivePixels(int left, int top, int right, int bottom,
        int expectedLeft, int expectedTop, int expectedRight, int expectedBottom)
    {
        var session = new WindowSnapSession(Monitor, Monitor);
        var proposed = new WindowSnapRectangle(left, top, right, bottom);
        var result = session.Move(proposed, []);
        Assert.Equal(new WindowSnapRectangle(expectedLeft, expectedTop, expectedRight, expectedBottom), result);
        Assert.Equal(proposed.Width, result.Width);
        Assert.Equal(proposed.Height, result.Height);
        Assert.False(session.IsSuppressed);
    }

    /// <summary>Six pixels is outside the inclusive threshold on every edge.</summary>
    [Theory]
    [InlineData(6, 200, 106, 300)]
    [InlineData(894, 200, 994, 300)]
    [InlineData(200, 6, 300, 106)]
    [InlineData(200, 894, 300, 994)]
    public void Move_DoesNotSnapAtSixPixels(int left, int top, int right, int bottom)
    {
        var proposed = new WindowSnapRectangle(left, top, right, bottom);
        Assert.Equal(proposed, new WindowSnapSession(Monitor, Monitor).Move(proposed, []));
    }

    /// <summary>Each move uses its raw bounds and releases a snapped edge as soon as the threshold is exceeded.</summary>
    [Fact]
    public void Move_DoesNotAccumulatePreviousSnapOffset()
    {
        var session = new WindowSnapSession(Monitor, Monitor);
        Assert.Equal(0, session.Move(new(5, 200, 105, 300), []).Left);
        Assert.Equal(6, session.Move(new(6, 200, 106, 300), []).Left);
        Assert.False(session.IsSuppressed);
    }

    /// <summary>Exact edge matches count as snapped even without translation, and moving back into the interior clears that state.</summary>
    [Fact]
    public void IsSnapped_TracksExactEdgesAndResetsForInteriorMoves()
    {
        var session = new WindowSnapSession(Monitor, Monitor);
        Assert.False(session.IsSnapped);
        var exactEdge = new WindowSnapRectangle(0, 200, 100, 300);
        Assert.Equal(exactEdge, session.Move(exactEdge, []));
        Assert.True(session.IsSnapped);
        var interior = new WindowSnapRectangle(200, 200, 300, 300);
        Assert.Equal(interior, session.Move(interior, []));
        Assert.False(session.IsSnapped);
        Assert.Equal(interior, session.Move(interior, [new(300, 200, 400, 300)]));
        Assert.True(session.IsSnapped);
        session.Move(new(-1, 200, 99, 300), []);
        Assert.True(session.IsSuppressed);
        Assert.False(session.IsSnapped);
        session.Move(exactEdge, []);
        Assert.False(session.IsSnapped);
    }

    /// <summary>Monitors above or left of the primary display retain their signed desktop coordinates.</summary>
    [Fact]
    public void Move_SupportsNegativeMonitorCoordinates()
    {
        var monitor = new WindowSnapRectangle(-1920, -1080, 0, 0);
        var session = new WindowSnapSession(monitor, monitor);
        Assert.Equal(new WindowSnapRectangle(-1920, -1080, -1720, -880), session.Move(new(-1915, -1075, -1715, -875), []));
        Assert.Equal(new WindowSnapRectangle(-200, -200, 0, 0), session.Move(new(-205, -205, -5, -5), []));
    }

    /// <summary>Peers support adjacency on all sides and matching corresponding left/right/top/bottom edges.</summary>
    [Theory]
    [InlineData(195, 430, 395, 530, 200, 430, 400, 530)]
    [InlineData(605, 430, 805, 530, 600, 430, 800, 530)]
    [InlineData(430, 195, 530, 395, 430, 200, 530, 400)]
    [InlineData(430, 605, 530, 805, 430, 600, 530, 800)]
    [InlineData(405, 450, 505, 550, 400, 450, 500, 550)]
    [InlineData(495, 450, 595, 550, 500, 450, 600, 550)]
    [InlineData(450, 405, 550, 505, 450, 400, 550, 500)]
    [InlineData(450, 495, 550, 595, 450, 500, 550, 600)]
    public void Move_SnapsAdjacentAndAlignedPeerEdges(int left, int top, int right, int bottom,
        int expectedLeft, int expectedTop, int expectedRight, int expectedBottom)
    {
        var session = new WindowSnapSession(Monitor, Monitor);
        Assert.Equal(new WindowSnapRectangle(expectedLeft, expectedTop, expectedRight, expectedBottom),
            session.Move(new(left, top, right, bottom), [new(400, 400, 600, 600)]));
    }

    /// <summary>A matching edge on a distant window is not an eligible target.</summary>
    [Fact]
    public void Move_RejectsPeersDistantOnPerpendicularAxis()
    {
        var session = new WindowSnapSession(Monitor, Monitor);
        var proposed = new WindowSnapRectangle(195, 100, 395, 200);
        Assert.Equal(proposed, session.Move(proposed, [new(400, 400, 600, 600)]));
        proposed = new WindowSnapRectangle(100, 195, 200, 395);
        Assert.Equal(proposed, session.Move(proposed, [new(400, 400, 600, 600)]));
    }

    /// <summary>Corner proximity is accepted at five pixels but rejected beyond it.</summary>
    [Fact]
    public void Move_BoundsPerpendicularProximityToFivePixels()
    {
        var proposed = new WindowSnapRectangle(200, 200, 300, 300);
        var session = new WindowSnapSession(Monitor, Monitor);
        Assert.Equal(new WindowSnapRectangle(205, 205, 305, 305), session.Move(proposed, [new(305, 305, 405, 405)]));
        Assert.Equal(proposed, session.Move(proposed, [new(305, 306, 405, 406)]));
    }

    /// <summary>The closest eligible edge wins; equal distances are deterministic regardless of peer enumeration order.</summary>
    [Fact]
    public void Move_ChoosesSmallestDeltaWithStableTies()
    {
        var proposed = new WindowSnapRectangle(400, 400, 600, 600);
        var left = new WindowSnapRectangle(397, 350, 697, 650);
        var right = new WindowSnapRectangle(403, 350, 703, 650);
        var session = new WindowSnapSession(Monitor, Monitor);
        Assert.Equal(397, session.Move(proposed, [left, right]).Left);
        Assert.Equal(397, session.Move(proposed, [right, left]).Left);
        Assert.Equal(402, session.Move(proposed, [left, new(402, 350, 702, 650)]).Left);
    }

    /// <summary>Touching the work area is not leaving the monitor; taskbar space neither suppresses nor forcibly clamps a move.</summary>
    [Fact]
    public void Move_UsesWorkAreaForTargetsAndMonitorForSuppression()
    {
        var session = new WindowSnapSession(Monitor, new(40, 20, 1000, 960));
        Assert.Equal(new WindowSnapRectangle(40, 200, 140, 300), session.Move(new(35, 200, 135, 300), []));
        Assert.Equal(new WindowSnapRectangle(200, 860, 300, 960), session.Move(new(200, 865, 300, 965), []));
        var taskbarBounds = new WindowSnapRectangle(200, 970, 300, 990);
        Assert.Equal(taskbarBounds, session.Move(taskbarBounds, []));
        Assert.False(session.IsSuppressed);
    }

    /// <summary>Crossing any physical monitor edge disables snapping for the rest of that drag, including re-entry.</summary>
    [Theory]
    [InlineData(-1, 100, 99, 200)]
    [InlineData(901, 100, 1001, 200)]
    [InlineData(100, -1, 200, 99)]
    [InlineData(100, 901, 200, 1001)]
    public void Move_SuppressesAfterEscapeUntilNewSession(int left, int top, int right, int bottom)
    {
        var session = new WindowSnapSession(Monitor, Monitor);
        var outside = new WindowSnapRectangle(left, top, right, bottom);
        Assert.Equal(outside, session.Move(outside, []));
        Assert.True(session.IsSuppressed);
        var reentry = new WindowSnapRectangle(5, 200, 105, 300);
        Assert.Equal(reentry, session.Move(reentry, [new(106, 200, 206, 300)]));
        var nextDrag = new WindowSnapSession(Monitor, Monitor);
        Assert.Equal(0, nextDrag.Move(reentry, []).Left);
        Assert.False(nextDrag.IsSuppressed);
    }

    /// <summary>A partially off-monitor peer cannot pull a valid window beyond the starting monitor.</summary>
    [Fact]
    public void Move_RejectsOutOfMonitorSnapTargets()
    {
        var session = new WindowSnapSession(Monitor, new(20, 20, 980, 980));
        var proposed = new WindowSnapRectangle(1, 200, 101, 300);
        Assert.Equal(proposed, session.Move(proposed, [new(-2, 150, 198, 350)]));
        Assert.False(session.IsSuppressed);
        // An inside edge of an otherwise off-monitor peer remains a usable alignment target.
        Assert.Equal(new WindowSnapRectangle(3, 200, 103, 300), session.Move(proposed, [new(-197, 150, 103, 350)]));
    }

    /// <summary>Valid extreme signed coordinates do not overflow delta or dimension arithmetic.</summary>
    [Fact]
    public void Move_WidensArithmeticBeforeComparingEdges()
    {
        var monitor = new WindowSnapRectangle(int.MinValue, -100, int.MinValue + 1000, 900);
        var proposed = new WindowSnapRectangle(int.MinValue + 5, 200, int.MinValue + 105, 300);
        var result = new WindowSnapSession(monitor, monitor).Move(proposed, [new(int.MaxValue - 100, 200, int.MaxValue, 300)]);
        Assert.Equal(int.MinValue, result.Left);
        Assert.Equal(100, result.Width);
    }

    /// <summary>Invalid monitor/work rectangles, malformed peers and null lists fail explicitly, even after suppression.</summary>
    [Fact]
    public void Inputs_RejectInvalidRectanglesAndNullPeers()
    {
        Assert.Throws<ArgumentException>(() => new WindowSnapSession(default, Monitor));
        Assert.Throws<ArgumentException>(() => new WindowSnapSession(Monitor, new(100, 100, 100, 200)));
        Assert.Throws<ArgumentException>(() => new WindowSnapSession(Monitor, new(-1, 0, 1000, 1000)));
        Assert.Throws<ArgumentException>(() => new WindowSnapSession(new(int.MinValue, 0, int.MaxValue, 100), Monitor));
        var session = new WindowSnapSession(Monitor, Monitor);
        Assert.Throws<ArgumentNullException>(() => session.Move(new(100, 100, 200, 200), null!));
        Assert.Throws<ArgumentException>(() => session.Move(new(200, 100, 100, 200), []));
        Assert.Throws<ArgumentException>(() => session.Move(new(100, 100, 200, 200), [new(0, 1, 10, 0)]));
        Assert.Throws<ArgumentException>(() => session.Move(new(int.MinValue, 0, int.MaxValue, 100), []));
        Assert.Throws<ArgumentException>(() => session.Move(new(100, 100, 200, 200), [new(0, int.MinValue, 100, int.MaxValue)]));
        Assert.False(session.IsSuppressed);
        session.Move(new(-1, 100, 99, 200), []);
        Assert.Throws<ArgumentException>(() => session.Move(default, []));
        Assert.Throws<ArgumentException>(() => session.Move(new(100, 100, 200, 200), [default]));
    }
}
