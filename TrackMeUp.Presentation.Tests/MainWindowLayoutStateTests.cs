using TrackMeUp.Presentation;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class MainWindowLayoutStateTests
{
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
    public void ResolveLogicalHeight_CapsOptionsAndStillFitsSmallerDisplays()
    {
        var state = new MainWindowLayoutState();
        state.RecordMeasuredHeight(1400);

        state.ShowSurface(MainWindowSurface.Options);
        Assert.Equal(760, state.ResolveLogicalHeight(1200));
        Assert.Equal(620, state.ResolveLogicalHeight(620));

        state.ShowSurface(MainWindowSurface.Player);
        Assert.Equal(1200, state.ResolveLogicalHeight(1200));
    }
}
