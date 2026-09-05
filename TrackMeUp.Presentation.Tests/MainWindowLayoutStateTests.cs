// SPDX-License-Identifier: MIT

using System;
using TrackMeUp.Presentation;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class MainWindowLayoutStateTests
{
    /// <summary>Checks that content updates and surface changes do not overwrite the user's viewport.</summary>
    [Fact]
    public void ManualViewport_SurvivesContentChangesAndSurfaceSwitches()
    {
        var state = new MainWindowLayoutState();
        state.RecordManualSize(510, 390);
        state.RecordMeasuredHeight(1600);
        Assert.Equal(510, state.ResolveLogicalWidth(1200, 576));
        Assert.Equal(390, state.ResolveLogicalHeight(900, 20));

        state.ShowSurface(MainWindowSurface.Options);
        Assert.Equal(760, state.ResolveLogicalWidth(1200, 760));
        state.RecordManualSize(820, 630);
        Assert.Equal(630, state.ResolveLogicalHeight(900, 20));

        state.ShowSurface(MainWindowSurface.Player);
        Assert.Equal(510, state.ResolveLogicalWidth(1200, 576));
        Assert.Equal(390, state.ResolveLogicalHeight(900, 20));
    }

    /// <summary>Checks that a smaller display constrains bounds without losing the preferred viewport.</summary>
    [Fact]
    public void ManualViewport_ClampsToTheDisplayAndRecoversWhenSpaceReturns()
    {
        var state = new MainWindowLayoutState();
        state.RecordManualSize(820, 630);
        Assert.Equal(500, state.ResolveLogicalWidth(500.8, 576));
        Assert.Equal(400, state.ResolveLogicalHeight(400.9, 20));
        Assert.Equal(820, state.ResolveLogicalWidth(1200, 576));
        Assert.Equal(630, state.ResolveLogicalHeight(900, 20));
    }

    /// <summary>Checks that invalid window geometry fails before replacing the last valid size.</summary>
    [Fact]
    public void ManualViewport_RejectsInvalidGeometry()
    {
        var state = new MainWindowLayoutState();
        state.RecordManualSize(510, 390);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.RecordManualSize(double.NaN, 390));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.RecordManualSize(510, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ResolveLogicalWidth(double.PositiveInfinity, 576));
        Assert.Equal(510, state.ResolveLogicalWidth(1200, 576));
        Assert.Equal(390, state.ResolveLogicalHeight(900, 20));
    }

    [Fact]
    public void State_TracksAllPlayerSectionsWithoutResettingThemWhenChangingSurface()
    {
        var state = new MainWindowLayoutState();

        Assert.Equal(MainWindowSurface.Player, state.Surface);
        Assert.True(state.IsActivityScoreVisible);
        Assert.False(state.IsLastSessionVisible);
        Assert.False(state.IsPendingSnapshotVisible);
        Assert.False(state.IsOutsideActiveHoursWarningVisible);

        Assert.True(state.ToggleSection(MainWindowLayoutSection.LastSession));
        Assert.True(state.SetSectionVisibility(MainWindowLayoutSection.PendingSnapshot, true));
        Assert.True(state.SetSectionVisibility(MainWindowLayoutSection.OutsideActiveHoursWarning, true));
        state.ShowSurface(MainWindowSurface.Options);
        state.ShowSurface(MainWindowSurface.Player);

        Assert.True(state.IsLastSessionVisible);
        Assert.True(state.IsPendingSnapshotVisible);
        Assert.True(state.IsOutsideActiveHoursWarningVisible);
    }

    [Fact]
    public void RecordMeasuredHeight_RoundsUpAndPreservesTheLastValidMeasurementForTransientLayoutPasses()
    {
        var state = new MainWindowLayoutState();

        Assert.Equal(489, state.RecordMeasuredHeight(488.1));
        Assert.Equal(489, state.RecordMeasuredHeight(0));
        Assert.Equal(489, state.RecordMeasuredHeight(double.NaN));
    }

    [Fact]
    public void ResolveLogicalHeight_CapsSecondarySurfacesAndStillFitsSmallerDisplays()
    {
        var state = new MainWindowLayoutState();
        state.RecordMeasuredHeight(1400);

        state.ShowSurface(MainWindowSurface.Options);
        Assert.Equal(520, state.ResolveLogicalHeight(1200, 0));
        Assert.Equal(520, state.ResolveLogicalHeight(620, 0));

        state.ShowSurface(MainWindowSurface.Operations);
        Assert.Equal(520, state.ResolveLogicalHeight(1200, 0));
        Assert.Equal(520, state.ResolveLogicalHeight(620, 0));

        state.ShowSurface(MainWindowSurface.Player);
        Assert.Equal(1200, state.ResolveLogicalHeight(1200, 0));
    }

    [Fact]
    public void ResolveLogicalHeight_AddsOuterPaddingWithoutExceedingTheDisplay()
    {
        var state = new MainWindowLayoutState();
        state.RecordMeasuredHeight(304);

        Assert.Equal(324, state.ResolveLogicalHeight(900, 20));
        Assert.Equal(310, state.ResolveLogicalHeight(310, 20));
    }
}
