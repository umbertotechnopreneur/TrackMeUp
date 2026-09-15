// SPDX-License-Identifier: MIT

using TrackMeUp.Presentation;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

/// <summary>Verifies that late preview loads cannot replace the user's current screenshot selection.</summary>
public sealed class SearchPreviewSelectionStateTests
{
    /// <summary>Returning quickly to a previous result must reject the first request for that same file.</summary>
    [Fact]
    public void RapidSelectionRejectsEarlierCompletionEvenWhenPathMatchesAgain()
    {
        var state = new SearchPreviewSelectionState();
        Assert.True(state.Select("first.png"));
        var initialGeneration = state.Generation;
        Assert.True(state.Select("second.png"));
        Assert.False(state.IsCurrent("first.png", initialGeneration));
        Assert.True(state.Select("first.png"));
        Assert.False(state.IsCurrent("first.png", initialGeneration));
        Assert.True(state.IsCurrent("first.png", state.Generation));
    }

    /// <summary>Loading/progress updates for the same selection reuse its pending or decoded bitmap.</summary>
    [Fact]
    public void RenderingSameSelectionDoesNotRestartImageLoad()
    {
        var state = new SearchPreviewSelectionState();
        state.Select("selected.png");
        var generation = state.Generation;
        Assert.False(state.Select("selected.png"));
        Assert.Equal(generation, state.Generation);
        Assert.True(state.IsCurrent("selected.png", generation));
    }

    /// <summary>Clearing results or closing the window invalidates a load that finishes after cancellation.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClearedOrClosedPreviewRejectsPendingCompletion(bool closing)
    {
        var state = new SearchPreviewSelectionState();
        state.Select("selected.png");
        var generation = state.Generation;
        if (closing)
        {
            state.Invalidate();
        }
        else
        {
            Assert.True(state.Select(null));
        }

        Assert.False(state.IsCurrent("selected.png", generation));
    }
}
