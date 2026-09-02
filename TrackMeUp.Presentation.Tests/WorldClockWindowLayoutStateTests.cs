// SPDX-License-Identifier: MIT

using System;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class WorldClockWindowLayoutStateTests
{
    [Fact]
    public void Surface_DefaultsToClocksAndSwitchesWithoutLosingTheOwnerState()
    {
        var state = new WorldClockWindowLayoutState();

        Assert.Equal(WorldClockWindowSurface.Clocks, state.Surface);
        state.ShowSurface(WorldClockWindowSurface.Options);
        Assert.Equal(WorldClockWindowSurface.Options, state.Surface);
        state.ShowSurface(WorldClockWindowSurface.Clocks);
        Assert.Equal(WorldClockWindowSurface.Clocks, state.Surface);
    }

    [Fact]
    public void ShowSurface_RejectsUndefinedValues()
    {
        var state = new WorldClockWindowLayoutState();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.ShowSurface((WorldClockWindowSurface)int.MaxValue));
    }

    [Theory]
    [InlineData("2026-08-30T12:34:00.0000000+00:00", 60.1d)]
    [InlineData("2026-08-30T12:34:15.2500000+00:00", 44.85d)]
    [InlineData("2026-08-30T12:34:59.9900000+00:00", 0.11d)]
    public void DelayUntilNextMinute_LandsJustAfterMinuteBoundary(string instantText, double expectedSeconds)
    {
        var delay = WorldClockWindowLayoutState.DelayUntilNextMinute(DateTimeOffset.Parse(instantText));

        Assert.Equal(expectedSeconds, delay.TotalSeconds, precision: 6);
    }

    [Theory]
    [InlineData(1, 1200d, 280d, 390d, true)]
    [InlineData(2, 500d, 560d, 560d, false)]
    [InlineData(3, 700d, 840d, 840d, false)]
    [InlineData(4, 1400d, 1120d, 1400d, false)]
    [InlineData(12, 1400d, 3360d, 3360d, false)]
    public void CalculateColumnsLayout_PreservesReadableEqualColumns(
        int clockCount,
        double viewportWidth,
        double expectedMinimumWidth,
        double expectedWidth,
        bool expectedCentered)
    {
        var layout = WorldClockWindowLayoutState.CalculateColumnsLayout(clockCount, viewportWidth);

        Assert.Equal(expectedMinimumWidth, layout.MinimumWidth);
        Assert.Equal(expectedWidth, layout.Width);
        Assert.Equal(expectedCentered, layout.IsCentered);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void CalculateColumnsLayout_RejectsUnsupportedClockCounts(int clockCount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldClockWindowLayoutState.CalculateColumnsLayout(clockCount, 800d));

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, false, false, false)]
    public void ResolveConversionFailure_RestoresOnlyATransitionThatStartedLive(
        bool transitionStartedFromLive,
        bool expectedLive,
        bool expectedValid,
        bool expectedRestoreControls)
    {
        var state = WorldClockWindowLayoutState.ResolveConversionFailure(transitionStartedFromLive);

        Assert.Equal(expectedLive, state.IsLive);
        Assert.Equal(expectedValid, state.CustomProjectionValid);
        Assert.Equal(expectedRestoreControls, state.RestoreLastSnapshotControls);
    }
}
