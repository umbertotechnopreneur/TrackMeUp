namespace TrackMeUp.Presentation;

/// <summary>Describes one reusable physical slot in the screenshot cover-flow ring.</summary>
/// <typeparam name="T">Reference type projected by the ring.</typeparam>
public sealed record ScreenshotCoverFlowSlot<T>(
    int PoolIndex,
    int RelativeOffset,
    int? LogicalIndex,
    T? Item)
    where T : class
{
    /// <summary>Gets whether the slot belongs to the five-item visible window.</summary>
    public bool IsVisible => Math.Abs(RelativeOffset) <= ScreenshotCoverFlowProjection.VisibleRadius;

    /// <summary>Gets whether the slot stages the next item immediately outside the visible window.</summary>
    public bool IsStaging => Math.Abs(RelativeOffset) == ScreenshotCoverFlowProjection.StagingRadius;
}

/// <summary>
/// Projects an ordered item list onto a constant seven-slot circular ring without duplicating the source list.
/// </summary>
/// <typeparam name="T">Reference type projected by the ring.</typeparam>
public sealed class ScreenshotCoverFlowRing<T>
    where T : class
{
    private readonly T[] _items;
    private readonly IReadOnlyList<T> _readOnlyItems;
    private readonly ScreenshotCoverFlowSlot<T>[] _slots;
    private readonly IReadOnlyList<ScreenshotCoverFlowSlot<T>> _readOnlySlots;

    internal ScreenshotCoverFlowRing(
        T[] items,
        int? selectedIndex,
        ScreenshotCoverFlowSlot<T>[] slots,
        int transitionDelta)
    {
        _items = items;
        _readOnlyItems = Array.AsReadOnly(_items);
        SelectedIndex = selectedIndex;
        _slots = slots;
        _readOnlySlots = Array.AsReadOnly(_slots);
        TransitionDelta = transitionDelta;
    }

    /// <summary>Gets the immutable source snapshot without circular duplicates.</summary>
    public IReadOnlyList<T> Items => _readOnlyItems;

    /// <summary>Gets the seven physical slots ordered from offset -3 through +3.</summary>
    public IReadOnlyList<ScreenshotCoverFlowSlot<T>> Slots => _readOnlySlots;

    /// <summary>Gets the selected logical item index, or <see langword="null"/> for an empty source.</summary>
    public int? SelectedIndex { get; }

    /// <summary>Gets the selected item, or <see langword="null"/> for an empty source.</summary>
    public T? SelectedItem => SelectedIndex is { } index ? _items[index] : null;

    /// <summary>
    /// Gets the signed logical movement used to create this projection from its predecessor.
    /// Positive values move forward; negative values move backward.
    /// </summary>
    public int TransitionDelta { get; }

    /// <summary>Gets whether movement can select a different item.</summary>
    public bool CanNavigate => _items.Length > 1;

    /// <summary>Moves forward by one item while preserving physical slots for overlapping items.</summary>
    public ScreenshotCoverFlowRing<T> MoveNext() => MoveAdjacent(1);

    /// <summary>Moves backward by one item while preserving physical slots for overlapping items.</summary>
    public ScreenshotCoverFlowRing<T> MovePrevious() => MoveAdjacent(-1);

    /// <summary>
    /// Rebases the ring around a target logical index using the shortest circular delta.
    /// Existing pool slots rotate with their items; only slots without an overlapping item are rebound.
    /// </summary>
    /// <param name="targetLogicalIndex">Target index; values outside the source range are wrapped.</param>
    public ScreenshotCoverFlowRing<T> RebaseSelection(int targetLogicalIndex)
    {
        if (SelectedIndex is not { } selectedIndex || _items.Length <= 1)
        {
            return this;
        }

        var targetIndex = ScreenshotCoverFlowProjection.WrapIndex(targetLogicalIndex, _items.Length)!.Value;
        var delta = ScreenshotCoverFlowProjection.ShortestDelta(selectedIndex, targetIndex, _items.Length);
        return delta == 0 ? this : Reproject(targetIndex, delta);
    }

    private ScreenshotCoverFlowRing<T> MoveAdjacent(int delta)
    {
        if (SelectedIndex is not { } selectedIndex || _items.Length <= 1)
        {
            return this;
        }

        var targetIndex = ScreenshotCoverFlowProjection.WrapIndex(selectedIndex + delta, _items.Length)!.Value;
        return Reproject(targetIndex, delta);
    }

    private ScreenshotCoverFlowRing<T> Reproject(int targetIndex, int delta)
    {
        var slots = new ScreenshotCoverFlowSlot<T>[ScreenshotCoverFlowProjection.SlotCount];
        for (var relativeOffset = -ScreenshotCoverFlowProjection.StagingRadius;
             relativeOffset <= ScreenshotCoverFlowProjection.StagingRadius;
             relativeOffset++)
        {
            // Rotate the fixed pool with the transition. Overlapping logical items therefore keep both
            // their physical presenter and their decoded image; only the wrapped staging slot is rebound.
            var sourceOffset = ScreenshotCoverFlowProjection.WrapSlotOffset(relativeOffset + delta);
            var sourceSlot = _slots[sourceOffset + ScreenshotCoverFlowProjection.StagingRadius];
            var logicalIndex = ScreenshotCoverFlowProjection.WrapIndex(targetIndex + relativeOffset, _items.Length)!.Value;
            slots[relativeOffset + ScreenshotCoverFlowProjection.StagingRadius] = new ScreenshotCoverFlowSlot<T>(
                sourceSlot.PoolIndex,
                relativeOffset,
                logicalIndex,
                _items[logicalIndex]);
        }

        return new ScreenshotCoverFlowRing<T>(_items, targetIndex, slots, delta);
    }
}

/// <summary>Creates and navigates the fixed seven-slot screenshot cover-flow projection.</summary>
public static class ScreenshotCoverFlowProjection
{
    /// <summary>Gets the number of visible items on either side of the selected item.</summary>
    public const int VisibleRadius = 2;

    /// <summary>Gets the outer radius containing the two staging slots.</summary>
    public const int StagingRadius = VisibleRadius + 1;

    /// <summary>Gets the fixed physical slot count: five visible slots plus two staging slots.</summary>
    public const int SlotCount = (StagingRadius * 2) + 1;

    /// <summary>Creates an immutable circular projection from an ordered source snapshot.</summary>
    /// <typeparam name="T">Reference type projected by the ring.</typeparam>
    /// <param name="items">Unique ordered source items. The projection does not duplicate this collection.</param>
    /// <param name="selectedIndex">Initial logical selection; values outside the source range are wrapped.</param>
    public static ScreenshotCoverFlowRing<T> Create<T>(IReadOnlyList<T> items, int selectedIndex = 0)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Any(item => item is null))
        {
            throw new ArgumentException("Cover-flow items cannot contain null values.", nameof(items));
        }

        var snapshot = items.ToArray();
        var normalizedSelection = WrapIndex(selectedIndex, snapshot.Length);
        var slots = new ScreenshotCoverFlowSlot<T>[SlotCount];
        for (var relativeOffset = -StagingRadius; relativeOffset <= StagingRadius; relativeOffset++)
        {
            var poolIndex = relativeOffset + StagingRadius;
            var logicalIndex = normalizedSelection is { } selection
                ? WrapIndex(selection + relativeOffset, snapshot.Length)
                : null;
            slots[poolIndex] = new ScreenshotCoverFlowSlot<T>(
                poolIndex,
                relativeOffset,
                logicalIndex,
                logicalIndex is { } index ? snapshot[index] : null);
        }

        return new ScreenshotCoverFlowRing<T>(snapshot, normalizedSelection, slots, transitionDelta: 0);
    }

    /// <summary>Wraps any logical index into the item range, returning null for an empty source.</summary>
    public static int? WrapIndex(int logicalIndex, int itemCount)
    {
        if (itemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count cannot be negative.");
        }

        if (itemCount == 0)
        {
            return null;
        }

        return (int)(((long)logicalIndex % itemCount + itemCount) % itemCount);
    }

    /// <summary>
    /// Returns the shortest signed circular delta between two logical indices.
    /// An exact half-ring tie deterministically favors the positive direction.
    /// </summary>
    public static int ShortestDelta(int fromLogicalIndex, int toLogicalIndex, int itemCount)
    {
        if (itemCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count cannot be negative.");
        }

        if (itemCount <= 1)
        {
            return 0;
        }

        var from = WrapIndex(fromLogicalIndex, itemCount)!.Value;
        var to = WrapIndex(toLogicalIndex, itemCount)!.Value;
        var forward = (int)(((long)to - from + itemCount) % itemCount);
        var backward = forward - itemCount;
        return forward <= Math.Abs((long)backward) ? forward : backward;
    }

    internal static int WrapSlotOffset(int relativeOffset)
        => (int)(((long)relativeOffset + StagingRadius) % SlotCount + SlotCount) % SlotCount - StagingRadius;
}
