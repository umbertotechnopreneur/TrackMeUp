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

    [Fact]
    public void TogglePresentationMode_SwitchesDensityWithoutChangingTheActiveSurface()
    {
        var state = new WorldClockWindowLayoutState();

        Assert.Equal(WorldClockPresentationMode.Compact, state.TogglePresentationMode());
        Assert.Equal(WorldClockWindowSurface.Clocks, state.Surface);
        Assert.Equal(WorldClockPresentationMode.Expanded, state.TogglePresentationMode());
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
    [InlineData(1, 1200d, 320d, 1200d)]
    [InlineData(2, 500d, 640d, 640d)]
    [InlineData(3, 700d, 960d, 960d)]
    [InlineData(4, 1400d, 1280d, 1400d)]
    [InlineData(12, 1400d, 3840d, 3840d)]
    public void CalculateColumnsLayout_PreservesReadableEqualColumns(
        int clockCount,
        double viewportWidth,
        double expectedMinimumWidth,
        double expectedWidth)
    {
        var layout = WorldClockWindowLayoutState.CalculateColumnsLayout(clockCount, viewportWidth);

        Assert.Equal(expectedMinimumWidth, layout.MinimumWidth);
        Assert.Equal(expectedWidth, layout.Width);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void CalculateColumnsLayout_RejectsUnsupportedClockCounts(int clockCount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldClockWindowLayoutState.CalculateColumnsLayout(clockCount, 800d));

    [Theory]
    [InlineData(1, WorldClockPresentationMode.Compact, 480, 400, 480, 320)]
    [InlineData(2, WorldClockPresentationMode.Compact, 960, 400, 480, 320)]
    [InlineData(3, WorldClockPresentationMode.Compact, 1120, 400, 480, 320)]
    [InlineData(1, WorldClockPresentationMode.Expanded, 480, 680, 480, 320)]
    [InlineData(2, WorldClockPresentationMode.Expanded, 780, 680, 480, 320)]
    public void CalculateWindowSizing_UsesTheCityCountAndPresentationMode(
        int clockCount,
        WorldClockPresentationMode presentationMode,
        int preferredWidth,
        int preferredHeight,
        int minimumWidth,
        int minimumHeight)
    {
        var sizing = WorldClockWindowLayoutState.CalculateWindowSizing(clockCount, presentationMode);

        Assert.Equal(preferredWidth, sizing.PreferredLogicalWidth);
        Assert.Equal(preferredHeight, sizing.PreferredLogicalHeight);
        Assert.Equal(minimumWidth, sizing.MinimumLogicalWidth);
        Assert.Equal(minimumHeight, sizing.MinimumLogicalHeight);
    }

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
