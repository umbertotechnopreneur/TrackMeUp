using System;
using System.Collections.Generic;
using System.Linq;
using TrackMeUp.Presentation;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class ScreenshotCoverFlowProjectionTests
{
    [Fact]
    public void EmptySource_CreatesSevenUnmappedSlots()
    {
        var ring = ScreenshotCoverFlowProjection.Create(Array.Empty<Item>());

        Assert.Null(ring.SelectedIndex);
        Assert.Null(ring.SelectedItem);
        Assert.False(ring.CanNavigate);
        Assert.Equal(7, ring.Slots.Count);
        Assert.Equal(5, ring.Slots.Count(slot => slot.IsVisible));
        Assert.Equal(2, ring.Slots.Count(slot => slot.IsStaging));
        Assert.All(ring.Slots, slot =>
        {
            Assert.Null(slot.LogicalIndex);
            Assert.Null(slot.Item);
        });
        Assert.Null(ScreenshotCoverFlowProjection.WrapIndex(123, 0));
        Assert.Equal(0, ScreenshotCoverFlowProjection.ShortestDelta(7, -9, 0));
    }

    [Fact]
    public void SingleItem_WrapsEverySlotAndCannotMove()
    {
        var only = new Item("only");
        var ring = ScreenshotCoverFlowProjection.Create(new[] { only }, selectedIndex: -42);

        Assert.Equal(0, ring.SelectedIndex);
        Assert.Same(only, ring.SelectedItem);
        Assert.False(ring.CanNavigate);
        Assert.All(ring.Slots, slot =>
        {
            Assert.Equal(0, slot.LogicalIndex);
            Assert.Same(only, slot.Item);
        });
        Assert.Same(ring, ring.MoveNext());
        Assert.Same(ring, ring.MovePrevious());
        Assert.Same(ring, ring.RebaseSelection(500));
        Assert.Equal(0, ScreenshotCoverFlowProjection.ShortestDelta(0, 500, 1));
    }

    [Fact]
    public void TwoItems_WrapInBothDirectionsWithoutDuplicatingTheSource()
    {
        var first = new Item("first");
        var second = new Item("second");
        var ring = ScreenshotCoverFlowProjection.Create(new[] { first, second });

        var next = ring.MoveNext();
        var previous = ring.MovePrevious();

        Assert.Equal(2, ring.Items.Count);
        Assert.Equal(1, next.SelectedIndex);
        Assert.Same(second, next.SelectedItem);
        Assert.Equal(1, next.TransitionDelta);
        Assert.Equal(1, previous.SelectedIndex);
        Assert.Same(second, previous.SelectedItem);
        Assert.Equal(-1, previous.TransitionDelta);
        Assert.Equal(0, next.MoveNext().SelectedIndex);
        Assert.Equal(0, previous.MovePrevious().SelectedIndex);
    }

    [Fact]
    public void ManyItems_MapLogicalIndicesAcrossFiveVisibleAndTwoStagingSlots()
    {
        var items = Enumerable.Range(0, 10).Select(index => new Item(index.ToString())).ToArray();
        var ring = ScreenshotCoverFlowProjection.Create(items);

        Assert.Equal(new[] { -3, -2, -1, 0, 1, 2, 3 }, ring.Slots.Select(slot => slot.RelativeOffset));
        Assert.Equal(new int?[] { 7, 8, 9, 0, 1, 2, 3 }, ring.Slots.Select(slot => slot.LogicalIndex));
        Assert.Equal(new[] { "7", "8", "9", "0", "1", "2", "3" }, ring.Slots.Select(slot => slot.Item?.Id));
        Assert.Equal(Enumerable.Range(0, 7), ring.Slots.Select(slot => slot.PoolIndex));
        Assert.All(ring.Slots, slot => Assert.Same(items[slot.LogicalIndex!.Value], slot.Item));
    }

    [Fact]
    public void AdjacentRebase_PreservesPoolAndItemForSixOverlappingSlots()
    {
        var items = Enumerable.Range(0, 10).Select(index => new Item(index.ToString())).ToArray();
        var before = ScreenshotCoverFlowProjection.Create(items);

        var after = before.MoveNext();

        Assert.Equal(1, after.SelectedIndex);
        Assert.Equal(1, after.TransitionDelta);
        foreach (var afterSlot in after.Slots.Where(slot => slot.RelativeOffset < ScreenshotCoverFlowProjection.StagingRadius))
        {
            var beforeSlot = before.Slots.Single(slot => slot.RelativeOffset == afterSlot.RelativeOffset + 1);
            Assert.Equal(beforeSlot.PoolIndex, afterSlot.PoolIndex);
            Assert.Same(beforeSlot.Item, afterSlot.Item);
        }

        var incoming = after.Slots.Single(slot => slot.RelativeOffset == ScreenshotCoverFlowProjection.StagingRadius);
        var recycled = before.Slots.Single(slot => slot.RelativeOffset == -ScreenshotCoverFlowProjection.StagingRadius);
        Assert.Equal(recycled.PoolIndex, incoming.PoolIndex);
        Assert.NotSame(recycled.Item, incoming.Item);
    }

    [Fact]
    public void RebaseSelection_UsesShortestDeltaAndPreservesEveryOverlappingPoolItem()
    {
        var items = Enumerable.Range(0, 10).Select(index => new Item(index.ToString())).ToArray();
        var before = ScreenshotCoverFlowProjection.Create(items);

        var after = before.RebaseSelection(8);

        Assert.Equal(8, after.SelectedIndex);
        Assert.Equal(-2, after.TransitionDelta);
        foreach (var afterSlot in after.Slots.Where(slot => slot.RelativeOffset >= -1))
        {
            var beforeSlot = before.Slots.Single(slot => slot.RelativeOffset == afterSlot.RelativeOffset - 2);
            Assert.Equal(beforeSlot.PoolIndex, afterSlot.PoolIndex);
            Assert.Same(beforeSlot.Item, afterSlot.Item);
        }
    }

    [Theory]
    [InlineData(9, 0, 10, 1)]
    [InlineData(0, 9, 10, -1)]
    [InlineData(0, 5, 10, 5)]
    [InlineData(0, 1, 2, 1)]
    [InlineData(-1, 10, 10, 1)]
    public void ShortestDelta_ReturnsDeterministicWrappedMovement(int from, int to, int count, int expected)
        => Assert.Equal(expected, ScreenshotCoverFlowProjection.ShortestDelta(from, to, count));

    [Fact]
    public void SourceSnapshot_IsUnaffectedByCallerMutation()
    {
        var first = new Item("first");
        var source = new List<Item> { first, new("second") };
        var ring = ScreenshotCoverFlowProjection.Create(source);

        source.Clear();

        Assert.Equal(2, ring.Items.Count);
        Assert.Same(first, ring.Items[0]);
        Assert.Equal(1, ring.MoveNext().SelectedIndex);
    }

    private sealed record Item(string Id);
}
