// SPDX-License-Identifier: MIT

using System;
using TrackMeUp.Application;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class WorldClockResponsiveLayoutTests
{
    [Theory]
    [InlineData(1, WorldClockPresentationMode.Compact)]
    [InlineData(2, WorldClockPresentationMode.Compact)]
    [InlineData(1, WorldClockPresentationMode.Expanded)]
    [InlineData(2, WorldClockPresentationMode.Expanded)]
    public void PreferredSize_HasNoUnusedWidthAndSharesTheRestoreMinimum(int count, WorldClockPresentationMode mode)
    {
        var sizing = WorldClockWindowLayoutState.CalculateWindowSizing(count, mode);
        var columns = WorldClockWindowLayoutState.CalculateColumnsLayout(count, sizing.PreferredLogicalWidth);
        var minimum = WindowStateService.GetMinimumSize(WindowStateKeys.WorldClocks);

        Assert.Equal(sizing.PreferredLogicalWidth, columns.Width);
        Assert.Equal(minimum.Width, sizing.MinimumLogicalWidth);
        Assert.Equal(minimum.Height, sizing.MinimumLogicalHeight);
    }

    [Fact]
    public void ScaledTimeText_ExpandsColumnsEquallyAndScrollsInsteadOfClipping()
    {
        var columns = WorldClockWindowLayoutState.CalculateColumnsLayout(2, 640d, minimumColumnWidth: 410d);
        Assert.Equal(820d, columns.Width);
        Assert.Equal(820d, columns.MinimumWidth);
    }

    [Theory]
    [InlineData(WorldClockPresentationMode.Expanded, 610d, 610d, 290d, WorldClockDetailLevel.Expanded)]
    [InlineData(WorldClockPresentationMode.Expanded, 609d, 610d, 290d, WorldClockDetailLevel.Summary)]
    [InlineData(WorldClockPresentationMode.Expanded, 290d, 610d, 290d, WorldClockDetailLevel.Summary)]
    [InlineData(WorldClockPresentationMode.Expanded, 289d, 610d, 290d, WorldClockDetailLevel.Essential)]
    [InlineData(WorldClockPresentationMode.Expanded, 610d, 850d, 410d, WorldClockDetailLevel.Summary)]
    [InlineData(WorldClockPresentationMode.Compact, 1000d, 610d, 290d, WorldClockDetailLevel.Summary)]
    [InlineData(WorldClockPresentationMode.Compact, 240d, 610d, 290d, WorldClockDetailLevel.Essential)]
    public void Disclosure_UsesMeasuredContentAndHonorsCompactChoice(
        WorldClockPresentationMode mode, double viewport, double expandedHeight, double summaryHeight,
        WorldClockDetailLevel expected)
    {
        Assert.Equal(expected, WorldClockWindowLayoutState.CalculateDetailLevel(mode, viewport, expandedHeight, summaryHeight));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1d)]
    public void Measurement_RejectsInvalidDimensions(double value)
    {
        Assert.ThrowsAny<ArgumentException>(() => WorldClockWindowLayoutState.CalculateColumnsLayout(1, value));
        Assert.ThrowsAny<ArgumentException>(() => WorldClockWindowLayoutState.CalculateDetailLevel(WorldClockPresentationMode.Expanded, value, 600, 300));
    }

    [Fact]
    public void RestoredBounds_AreRetainedOnFirstSnapshotAndSubsequentRefreshes()
    {
        var state = new WorldClockWindowLayoutState();
        state.SetPlacementRestored(true);
        var initial = Assert.IsType<WorldClockWindowResizeRequest>(state.GetWindowResizeRequest(2));
        Assert.False(initial.ResizeToPreferred);
        state.AcceptWindowResizeRequest(initial);
        Assert.Null(state.GetWindowResizeRequest(2));

        state.TogglePresentationMode();
        Assert.True(Assert.IsType<WorldClockWindowResizeRequest>(state.GetWindowResizeRequest(2)).ResizeToPreferred);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 3)]
    public void CityChangeInOptions_IsDeferredUntilReturningWithoutALiveRefresh(int before, int after)
    {
        var state = new WorldClockWindowLayoutState();
        state.AcceptWindowResizeRequest(Assert.IsType<WorldClockWindowResizeRequest>(state.GetWindowResizeRequest(before)));
        state.ShowSurface(WorldClockWindowSurface.Options);
        Assert.Null(state.GetWindowResizeRequest(after));

        state.ShowSurface(WorldClockWindowSurface.Clocks);
        var pending = Assert.IsType<WorldClockWindowResizeRequest>(state.GetWindowResizeRequest(after));
        Assert.True(pending.ResizeToPreferred);
        Assert.Equal(WorldClockWindowLayoutState.CalculateWindowSizing(after, state.PresentationMode), pending.Sizing);
        // A failed resize is not acknowledged and remains retryable.
        Assert.Equal(pending, state.GetWindowResizeRequest(after));
        state.AcceptWindowResizeRequest(pending);
        Assert.Null(state.GetWindowResizeRequest(after));
    }

    [Fact]
    public void EmptyCitySet_ResetsTheSizingCycle()
    {
        var state = new WorldClockWindowLayoutState();
        state.AcceptWindowResizeRequest(Assert.IsType<WorldClockWindowResizeRequest>(state.GetWindowResizeRequest(1)));
        state.ResetWindowSizing();
        Assert.True(Assert.IsType<WorldClockWindowResizeRequest>(state.GetWindowResizeRequest(1)).ResizeToPreferred);
    }
}
