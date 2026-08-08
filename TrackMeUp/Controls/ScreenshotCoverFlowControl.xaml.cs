using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Globalization;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using Windows.Foundation;
using Windows.System;
using Windows.UI.ViewManagement;

namespace TrackMeUp.Controls;

/// <summary>
/// Renders a circular, virtualized screenshot cover flow and reports selection intent to its owner.
/// </summary>
public sealed partial class ScreenshotCoverFlowControl : UserControl
{
    private const int PoolRadius = ScreenshotCoverFlowProjection.StagingRadius;
    private const int PoolSize = ScreenshotCoverFlowProjection.SlotCount;
    private const int VisibleRadius = ScreenshotCoverFlowProjection.VisibleRadius;
    private const int ThumbnailDecodeWidth = 1280;
    private const double DefaultAspectRatio = 16d / 9d;
    private const double DragActivationDistance = 7d;
    private const double InertiaProjectionSeconds = 0.16d;
    private const double MinimumFlickVelocity = 1.05d;
    private const double SideRevealDurationSeconds = 0.18d;
    private const int MaximumQueuedNavigation = 4;

    private readonly List<CoverSlot> _slots = new(PoolSize);
    private readonly Dictionary<string, double> _aspectRatios = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _animationTimer;
    private readonly UISettings _uiSettings = new();
    private IReadOnlyList<ScreenshotGalleryItem> _items = Array.Empty<ScreenshotGalleryItem>();
    private ScreenshotCoverFlowRing<ScreenshotGalleryItem> _ring =
        ScreenshotCoverFlowProjection.Create(Array.Empty<ScreenshotGalleryItem>());
    private int _selectedIndex;
    private int _selectionBeforeMotion;
    private int _queuedNavigation;
    private int? _pendingTargetIndex;
    private double _motionOffset;
    private double _animationFrom;
    private double _animationTo;
    private double _maximumCoverWidth = 640d;
    private double _maximumCoverHeight = 360d;
    private long _animationStartedAt;
    private double _animationDurationSeconds;
    private double _sideRevealProgress = 1d;
    private bool _isAnimating;
    private bool _isRevealingSides;
    private bool _isPointerTracking;
    private bool _isDragging;
    private bool _hasPointerCapture;
    private bool _suppressSlotClick;
    private uint _trackedPointerId;
    private double _pointerPressedX;
    private double _lastPointerX;
    private long _lastPointerTimestamp;
    private double _velocityStepsPerSecond;
    private int _transitionDirection;

    /// <summary>Creates the seven-slot circular cover-flow surface.</summary>
    public ScreenshotCoverFlowControl()
    {
        InitializeComponent();
        CreateSlotPool();

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += AnimationTimer_Tick;

        InteractionSurface.AddHandler(
            PointerPressedEvent,
            new PointerEventHandler(InteractionSurface_PointerPressed),
            handledEventsToo: true);
        InteractionSurface.AddHandler(
            PointerMovedEvent,
            new PointerEventHandler(InteractionSurface_PointerMoved),
            handledEventsToo: true);
        InteractionSurface.AddHandler(
            PointerReleasedEvent,
            new PointerEventHandler(InteractionSurface_PointerReleased),
            handledEventsToo: true);
        InteractionSurface.AddHandler(
            PointerWheelChangedEvent,
            new PointerEventHandler(InteractionSurface_PointerWheelChanged),
            handledEventsToo: true);
        InteractionSurface.PointerCanceled += InteractionSurface_PointerCanceled;
        InteractionSurface.PointerCaptureLost += InteractionSurface_PointerCaptureLost;
        KeyDown += ScreenshotCoverFlowControl_KeyDown;
        Unloaded += ScreenshotCoverFlowControl_Unloaded;

        UpdateNavigationAvailability();
        ApplyLayout();
    }

    /// <summary>Raised whenever user or programmatic motion settles and external selection must resynchronize.</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>Gets the currently selected source index, or zero when the source is empty.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Gets the number of pooled image containers currently bound to source items.</summary>
    public int RealizedItemCount => _slots.Count(static slot => slot.ItemIndex >= 0);

    /// <summary>
    /// Replaces the source snapshot and positions the flow on <paramref name="selectedIndex"/>.
    /// </summary>
    /// <param name="items">Finite screenshot source used by the circular visual pool.</param>
    /// <param name="selectedIndex">Initial source index, or zero for an empty source.</param>
    public void SetItems(IReadOnlyList<ScreenshotGalleryItem> items, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(items);
        if ((items.Count == 0 && selectedIndex != 0)
            || (items.Count > 0 && (selectedIndex < 0 || selectedIndex >= items.Count)))
        {
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        }

        CancelInteraction();
        var retainedPaths = items.Select(static item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stalePath in _aspectRatios.Keys.Where(path => !retainedPaths.Contains(path)).ToArray())
        {
            _aspectRatios.Remove(stalePath);
        }

        _ring = ScreenshotCoverFlowProjection.Create(items, selectedIndex);
        _items = _ring.Items;
        _selectedIndex = _ring.SelectedIndex ?? 0;
        _selectionBeforeMotion = _selectedIndex;
        _motionOffset = 0d;
        _transitionDirection = 0;
        _queuedNavigation = 0;
        _pendingTargetIndex = null;
        RebindSlots();
        UpdateNavigationAvailability();
        ApplyLayout();
    }

    /// <summary>Animates to the previous screenshot, wrapping at the beginning.</summary>
    public void MovePrevious() => RequestNavigation(-1);

    /// <summary>Animates to the next screenshot, wrapping at the end.</summary>
    public void MoveNext() => RequestNavigation(1);

    /// <summary>Animates to a source index using the shortest circular direction.</summary>
    public void MoveToIndex(int targetIndex)
    {
        if ((_items.Count == 0 && targetIndex != 0)
            || (_items.Count > 0 && (targetIndex < 0 || targetIndex >= _items.Count)))
        {
            throw new ArgumentOutOfRangeException(nameof(targetIndex));
        }

        if (_items.Count == 0)
        {
            return;
        }

        if (_isAnimating || _isPointerTracking)
        {
            CancelInteraction();
            _motionOffset = 0d;
            RebindSlots();
            ApplyLayout();
        }

        var delta = ScreenshotCoverFlowProjection.ShortestDelta(_selectedIndex, targetIndex, _items.Count);
        if (delta == 0)
        {
            return;
        }

        _selectionBeforeMotion = _selectedIndex;
        if (Math.Abs(delta) <= PoolRadius)
        {
            var firstStep = Math.Sign(delta);
            _queuedNavigation = delta - firstStep;
            StartSnap(firstStep);
            return;
        }

        // Preserve the current pose, replace only the invisible staging presenter, then sweep
        // that target through the flow. The logical selection is committed only at the end.
        var direction = Math.Sign(delta);
        StageDistantTarget(targetIndex, direction);
        ApplyLayout();
        StartSnap(direction * PoolRadius);
    }

    private void CreateSlotPool()
    {
        var slotStyle = (Style)Resources["CoverFlowSlotButtonStyle"];
        for (var offset = -PoolRadius; offset <= PoolRadius; offset++)
        {
            var image = new Image
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Stretch = Stretch.Uniform
            };
            AutomationProperties.SetAccessibilityView(image, AccessibilityView.Raw);
            var imageFrame = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Shadow = new ThemeShadow(),
                Translation = new System.Numerics.Vector3(0f, 0f, 10f),
                Child = image
            };
            var transform = new CompositeTransform();
            var projection = new PlaneProjection
            {
                CenterOfRotationX = 0.5d,
                CenterOfRotationY = 0.5d
            };
            var button = new Button
            {
                Style = slotStyle,
                Content = imageFrame,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = transform,
                RenderTransformOrigin = new Point(0.5d, 0.5d),
                Projection = projection,
                IsTabStop = offset == 0
            };
            var slot = new CoverSlot(offset, button, image, transform, projection);
            image.Tag = slot;
            image.ImageOpened += SlotImage_ImageOpened;
            button.Tag = slot;
            button.Click += Slot_Click;
            SlotsHost.Children.Add(button);
            _slots.Add(slot);
        }
    }

    private void RebindSlots()
    {
        var reusableSources = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in _slots)
        {
            if (slot.SourcePath is { } existingPath && slot.Image.Source is { } existingSource)
            {
                reusableSources.TryAdd(existingPath, existingSource);
            }
        }

        foreach (var projectedSlot in _ring.Slots)
        {
            var slot = _slots[projectedSlot.PoolIndex];
            slot.RelativeOffset = projectedSlot.RelativeOffset;
            if (_items.Count == 0 || (_items.Count == 1 && projectedSlot.RelativeOffset != 0))
            {
                slot.ItemIndex = -1;
                slot.SourcePath = null;
                slot.AspectRatio = DefaultAspectRatio;
                slot.Image.Source = null;
                slot.Button.Visibility = Visibility.Collapsed;
                continue;
            }

            var itemIndex = projectedSlot.LogicalIndex
                ?? throw new InvalidOperationException("A non-empty cover-flow slot must have a logical index.");
            var item = projectedSlot.Item
                ?? throw new InvalidOperationException("A non-empty cover-flow slot must have an item.");
            slot.ItemIndex = itemIndex;
            slot.AspectRatio = _aspectRatios.GetValueOrDefault(item.Path, DefaultAspectRatio);
            slot.Button.Visibility = Visibility.Visible;
            if (!string.Equals(slot.SourcePath, item.Path, StringComparison.OrdinalIgnoreCase))
            {
                if (!reusableSources.TryGetValue(item.Path, out var source))
                {
                    // Invalid or non-absolute paths fail immediately; the presentation must not hide a broken gallery contract.
                    source = new BitmapImage
                    {
                        DecodePixelWidth = ThumbnailDecodeWidth,
                        UriSource = new Uri(item.Path, UriKind.Absolute)
                    };
                    reusableSources[item.Path] = source;
                }

                slot.Image.Source = source;
                slot.SourcePath = item.Path;
            }

            var localTime = item.CapturedAt.ToLocalTime();
            var selectedStatus = projectedSlot.RelativeOffset == 0 ? ", selected" : string.Empty;
            AutomationProperties.SetName(
                slot.Button,
                $"Screenshot {itemIndex + 1} of {_items.Count}, {localTime.ToString("f", CultureInfo.CurrentCulture)}{selectedStatus}");
            AutomationProperties.SetPositionInSet(slot.Button, itemIndex + 1);
            AutomationProperties.SetSizeOfSet(slot.Button, _items.Count);
        }

        UpdateSlotInteractivity();
    }

    private void StageDistantTarget(int targetIndex, int direction)
    {
        var stagingOffset = direction * PoolRadius;
        var projectedSlot = _ring.Slots.Single(slot => slot.RelativeOffset == stagingOffset);
        var slot = _slots[projectedSlot.PoolIndex];
        var item = _items[targetIndex];
        slot.RelativeOffset = stagingOffset;
        slot.ItemIndex = targetIndex;
        slot.AspectRatio = _aspectRatios.GetValueOrDefault(item.Path, DefaultAspectRatio);
        slot.Button.Visibility = Visibility.Visible;

        if (!string.Equals(slot.SourcePath, item.Path, StringComparison.OrdinalIgnoreCase))
        {
            var reusableSource = _slots
                .Where(candidate => string.Equals(candidate.SourcePath, item.Path, StringComparison.OrdinalIgnoreCase))
                .Select(static candidate => candidate.Image.Source)
                .FirstOrDefault(static source => source is not null);
            slot.Image.Source = reusableSource ?? new BitmapImage
            {
                DecodePixelWidth = ThumbnailDecodeWidth,
                UriSource = new Uri(item.Path, UriKind.Absolute)
            };
            slot.SourcePath = item.Path;
        }

        var localTime = item.CapturedAt.ToLocalTime();
        AutomationProperties.SetName(
            slot.Button,
            $"Screenshot {targetIndex + 1} of {_items.Count}, {localTime.ToString("f", CultureInfo.CurrentCulture)}");
        AutomationProperties.SetPositionInSet(slot.Button, targetIndex + 1);
        AutomationProperties.SetSizeOfSet(slot.Button, _items.Count);
        AutomationProperties.SetAccessibilityView(slot.Button, AccessibilityView.Raw);
        slot.IsInteractive = false;
        slot.Button.IsTabStop = false;
        _pendingTargetIndex = targetIndex;
    }

    private void ApplyLayout()
    {
        var viewportWidth = Math.Max(1d, InteractionSurface.ActualWidth);
        var animationsEnabled = _uiSettings.AnimationsEnabled;
        var presenterBoundsWidth = animationsEnabled
            ? _maximumCoverWidth
            : Math.Min(_maximumCoverWidth, Math.Clamp(viewportWidth * 0.18d, 120d, 260d));
        foreach (var slot in _slots)
        {
            if (slot.ItemIndex < 0)
            {
                continue;
            }

            var relativePosition = slot.RelativeOffset - _motionOffset;
            var pose = ScreenshotCoverFlowLayout.CalculatePose(
                relativePosition,
                viewportWidth,
                _transitionDirection,
                reducedMotion: !animationsEnabled);
            var presenterSize = ScreenshotCoverFlowLayout.FitPresenter(
                slot.AspectRatio,
                presenterBoundsWidth,
                _maximumCoverHeight);
            slot.Button.Width = presenterSize.Width;
            slot.Button.Height = presenterSize.Height;
            slot.Transform.TranslateX = pose.TranslateX;
            slot.Transform.TranslateY = pose.TranslateY;
            slot.Transform.ScaleX = pose.Scale;
            slot.Transform.ScaleY = pose.Scale;
            slot.Projection.RotationY = pose.RotationY;
            slot.Projection.GlobalOffsetZ = pose.Depth;
            var sideReveal = Math.Abs(relativePosition) < 0.5d ? 1d : _sideRevealProgress;
            slot.Button.Opacity = pose.Opacity * sideReveal;
            slot.Button.IsHitTestVisible = slot.IsInteractive
                && Math.Abs(relativePosition) <= VisibleRadius + 0.42d;
            Canvas.SetZIndex(slot.Button, pose.ZIndex);
        }
    }

    private void UpdateSlotInteractivity()
    {
        var representedItems = new HashSet<int>();
        foreach (var slot in _slots
                     .OrderBy(static candidate => Math.Abs(candidate.RelativeOffset))
                     .ThenBy(static candidate => candidate.RelativeOffset))
        {
            var isVisiblePoolSlot = Math.Abs(slot.RelativeOffset) <= VisibleRadius;
            slot.IsInteractive = slot.ItemIndex >= 0
                && isVisiblePoolSlot
                && representedItems.Add(slot.ItemIndex);
            slot.Button.IsTabStop = slot.IsInteractive && slot.RelativeOffset == 0;
            AutomationProperties.SetAccessibilityView(
                slot.Button,
                slot.IsInteractive ? AccessibilityView.Content : AccessibilityView.Raw);
        }
    }

    private void RequestNavigation(int delta)
    {
        if (_items.Count <= 1 || delta == 0 || _isPointerTracking)
        {
            return;
        }

        if (_isAnimating)
        {
            _queuedNavigation = Math.Clamp(
                _queuedNavigation + delta,
                -MaximumQueuedNavigation,
                MaximumQueuedNavigation);
            return;
        }

        _selectionBeforeMotion = _selectedIndex;
        StartSnap(Math.Clamp(delta, -VisibleRadius, VisibleRadius));
    }

    private void StartSnap(int targetOffset)
    {
        _animationFrom = _motionOffset;
        _animationTo = targetOffset;
        _transitionDirection = Math.Sign(_animationTo - _animationFrom);
        if (!_uiSettings.AnimationsEnabled || Math.Abs(_animationTo - _animationFrom) < 0.001d)
        {
            _motionOffset = _animationTo;
            CompleteSnap();
            return;
        }

        var travel = Math.Abs(_animationTo - _animationFrom);
        _animationDurationSeconds = 0.22d + (Math.Min(travel, VisibleRadius) * 0.055d);
        _animationStartedAt = Stopwatch.GetTimestamp();
        _isAnimating = true;
        _animationTimer.Start();
    }

    private void AnimationTimer_Tick(object? sender, object e)
    {
        if (_isRevealingSides)
        {
            RevealSidesAtCurrentTime();
            return;
        }

        var elapsedSeconds = (Stopwatch.GetTimestamp() - _animationStartedAt) / (double)Stopwatch.Frequency;
        var progress = Math.Clamp(elapsedSeconds / _animationDurationSeconds, 0d, 1d);
        var easedProgress = 1d - Math.Pow(1d - progress, 3d);
        _motionOffset = _animationFrom + ((_animationTo - _animationFrom) * easedProgress);
        ApplyLayout();

        if (progress >= 1d)
        {
            _animationTimer.Stop();
            _isAnimating = false;
            _motionOffset = _animationTo;
            CompleteSnap();
        }
    }

    private void CompleteSnap()
    {
        var committedOffset = (int)Math.Round(_motionOffset, MidpointRounding.AwayFromZero);
        var pendingTargetIndex = _pendingTargetIndex;
        _pendingTargetIndex = null;
        if (_items.Count > 0 && committedOffset != 0)
        {
            if (pendingTargetIndex is { } targetIndex)
            {
                _ring = _ring.RebaseSelection(targetIndex);
                _selectedIndex = _ring.SelectedIndex!.Value;
            }
            else
            {
                AdvanceRing(committedOffset);
            }
        }

        _motionOffset = 0d;
        _transitionDirection = 0;
        _sideRevealProgress = pendingTargetIndex is not null && _uiSettings.AnimationsEnabled ? 0d : 1d;
        RebindSlots();
        ApplyLayout();

        // Always notify at a motion boundary. A timeline selection can change visually before
        // the cover commits; a no-op/canceled drag must still resynchronize that external view.
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);

        if (_sideRevealProgress < 1d)
        {
            StartSideReveal();
        }
        else
        {
            StartQueuedNavigationIfNeeded();
        }
    }

    private void StartSideReveal()
    {
        _animationStartedAt = Stopwatch.GetTimestamp();
        _animationDurationSeconds = SideRevealDurationSeconds;
        _isRevealingSides = true;
        _isAnimating = true;
        _animationTimer.Start();
    }

    private void RevealSidesAtCurrentTime()
    {
        var elapsedSeconds = (Stopwatch.GetTimestamp() - _animationStartedAt) / (double)Stopwatch.Frequency;
        var progress = Math.Clamp(elapsedSeconds / _animationDurationSeconds, 0d, 1d);
        _sideRevealProgress = 1d - Math.Pow(1d - progress, 2d);
        ApplyLayout();
        if (progress < 1d)
        {
            return;
        }

        _animationTimer.Stop();
        _sideRevealProgress = 1d;
        _isRevealingSides = false;
        _isAnimating = false;
        ApplyLayout();
        StartQueuedNavigationIfNeeded();
    }

    private void StartQueuedNavigationIfNeeded()
    {
        if (_queuedNavigation == 0 || _items.Count <= 1)
        {
            return;
        }

        var nextDelta = Math.Sign(_queuedNavigation);
        _queuedNavigation -= nextDelta;
        _selectionBeforeMotion = _selectedIndex;
        StartSnap(nextDelta);
    }

    private void NormalizeDraggedMotion()
    {
        while (_motionOffset > 1d)
        {
            AdvanceRing(1);
            _motionOffset -= 1d;
            RebindSlots();
        }

        while (_motionOffset < -1d)
        {
            AdvanceRing(-1);
            _motionOffset += 1d;
            RebindSlots();
        }
    }

    private void InteractionSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isPointerTracking)
        {
            return;
        }

        if (_items.Count <= 1 || IsNavigationButtonSource(e.OriginalSource))
        {
            Focus(FocusState.Pointer);
            return;
        }

        var point = e.GetCurrentPoint(InteractionSurface);
        var isMouse = e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse;
        if ((isMouse && !point.Properties.IsLeftButtonPressed)
            || (!isMouse && !e.Pointer.IsInContact))
        {
            return;
        }

        _isPointerTracking = true;
        _isDragging = false;
        _hasPointerCapture = false;
        _suppressSlotClick = false;
        _trackedPointerId = e.Pointer.PointerId;
        _pointerPressedX = point.Position.X;
        _lastPointerX = point.Position.X;
        _lastPointerTimestamp = Stopwatch.GetTimestamp();
        _velocityStepsPerSecond = 0d;
        Focus(FocusState.Pointer);
    }

    private void InteractionSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerTracking || e.Pointer.PointerId != _trackedPointerId)
        {
            return;
        }

        var point = e.GetCurrentPoint(InteractionSurface);
        if (!_isDragging && Math.Abs(point.Position.X - _pointerPressedX) < DragActivationDistance)
        {
            return;
        }

        if (!_isDragging)
        {
            CancelAnimationAtCurrentPosition();
            _selectionBeforeMotion = _selectedIndex;
            _isDragging = true;
            _suppressSlotClick = true;
            _hasPointerCapture = InteractionSurface.CapturePointer(e.Pointer);
        }

        var now = Stopwatch.GetTimestamp();
        var elapsedSeconds = Math.Max(
            1d / 240d,
            (now - _lastPointerTimestamp) / (double)Stopwatch.Frequency);
        var deltaSteps = -((point.Position.X - _lastPointerX) / GetDragPixelsPerItem());
        if (Math.Abs(deltaSteps) > 0.0001d)
        {
            _transitionDirection = Math.Sign(deltaSteps);
        }

        var instantaneousVelocity = deltaSteps / elapsedSeconds;
        _velocityStepsPerSecond = (_velocityStepsPerSecond * 0.68d) + (instantaneousVelocity * 0.32d);
        _motionOffset += deltaSteps;
        NormalizeDraggedMotion();
        ApplyLayout();
        _lastPointerX = point.Position.X;
        _lastPointerTimestamp = now;
        e.Handled = true;
    }

    private void InteractionSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerTracking || e.Pointer.PointerId != _trackedPointerId)
        {
            return;
        }

        var wasDragging = _isDragging;
        _isPointerTracking = false;
        _isDragging = false;
        if (_hasPointerCapture)
        {
            _hasPointerCapture = false;
            InteractionSurface.ReleasePointerCapture(e.Pointer);
        }

        if (!wasDragging)
        {
            return;
        }

        var projectedOffset = _motionOffset + (_velocityStepsPerSecond * InertiaProjectionSeconds);
        var targetOffset = (int)Math.Round(projectedOffset, MidpointRounding.AwayFromZero);
        if (targetOffset == 0 && Math.Abs(_velocityStepsPerSecond) >= MinimumFlickVelocity)
        {
            targetOffset = Math.Sign(_velocityStepsPerSecond);
        }

        targetOffset = Math.Clamp(targetOffset, -VisibleRadius, VisibleRadius);
        StartSnap(targetOffset);
        ClearClickSuppressionAfterRouting();
        e.Handled = true;
    }

    private void InteractionSurface_PointerCanceled(object sender, PointerRoutedEventArgs e)
        => CancelPointerGesture(e.Pointer.PointerId);

    private void InteractionSurface_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, InteractionSurface))
        {
            CancelPointerGesture(e.Pointer.PointerId);
        }
    }

    private void CancelPointerGesture(uint pointerId)
    {
        if (!_isPointerTracking || pointerId != _trackedPointerId)
        {
            return;
        }

        var wasDragging = _isDragging;
        _isPointerTracking = false;
        _isDragging = false;
        _hasPointerCapture = false;
        if (!wasDragging)
        {
            _suppressSlotClick = false;
            return;
        }

        _ring = _ring.RebaseSelection(_selectionBeforeMotion);
        _selectedIndex = _ring.SelectedIndex ?? 0;
        _motionOffset = 0d;
        _transitionDirection = 0;
        RebindSlots();
        ApplyLayout();
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        ClearClickSuppressionAfterRouting();
    }

    private void InteractionSurface_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_items.Count <= 1 || _isPointerTracking)
        {
            return;
        }

        var delta = e.GetCurrentPoint(InteractionSurface).Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        Focus(FocusState.Pointer);
        RequestNavigation(delta > 0 ? -1 : 1);
        e.Handled = true;
    }

    private void ScreenshotCoverFlowControl_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Left)
        {
            MovePrevious();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Right)
        {
            MoveNext();
            e.Handled = true;
        }
    }

    private void Slot_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressSlotClick || _isPointerTracking || _isAnimating || sender is not Button { Tag: CoverSlot slot })
        {
            return;
        }

        if (slot.ItemIndex >= 0 && Math.Abs(slot.RelativeOffset) <= VisibleRadius)
        {
            RequestNavigation(slot.RelativeOffset);
        }
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e) => MovePrevious();

    private void NextButton_Click(object sender, RoutedEventArgs e) => MoveNext();

    private void InteractionSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InteractionSurface.Clip = new RectangleGeometry { Rect = new Rect(0d, 0d, e.NewSize.Width, e.NewSize.Height) };
        _maximumCoverWidth = Math.Clamp(e.NewSize.Width * 0.54d, 300d, 720d);
        _maximumCoverHeight = Math.Clamp(e.NewSize.Height * 0.82d, 220d, 560d);

        ApplyLayout();
    }

    private double GetDragPixelsPerItem()
        => Math.Max(112d, Math.Min(_maximumCoverWidth * 0.54d, InteractionSurface.ActualWidth * 0.30d));

    private void SlotImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: CoverSlot slot, Source: BitmapImage bitmap }
            || slot.SourcePath is not { } sourcePath)
        {
            return;
        }

        if (bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0)
        {
            throw new InvalidDataException($"Screenshot dimensions are unavailable: {sourcePath}");
        }

        var aspectRatio = bitmap.PixelWidth / (double)bitmap.PixelHeight;
        _aspectRatios[sourcePath] = aspectRatio;
        foreach (var candidate in _slots.Where(candidate =>
                     string.Equals(candidate.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)))
        {
            candidate.AspectRatio = aspectRatio;
        }

        ApplyLayout();
    }

    private bool IsNavigationButtonSource(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (ReferenceEquals(current, PreviousButton) || ReferenceEquals(current, NextButton))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void UpdateNavigationAvailability()
    {
        var canNavigate = _items.Count > 1;
        PreviousButton.IsEnabled = canNavigate;
        NextButton.IsEnabled = canNavigate;
        PreviousButton.Visibility = canNavigate ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = canNavigate ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CancelAnimationAtCurrentPosition()
    {
        _animationTimer.Stop();
        _isAnimating = false;
        _isRevealingSides = false;
        _transitionDirection = 0;
        _sideRevealProgress = 1d;
        _queuedNavigation = 0;
        if (_pendingTargetIndex is not null)
        {
            _pendingTargetIndex = null;
            RebindSlots();
        }
    }

    private void CancelInteraction()
    {
        _animationTimer.Stop();
        _isAnimating = false;
        _isRevealingSides = false;
        _transitionDirection = 0;
        _sideRevealProgress = 1d;
        _queuedNavigation = 0;
        _pendingTargetIndex = null;
        _isPointerTracking = false;
        _isDragging = false;
        _hasPointerCapture = false;
        _suppressSlotClick = false;
        InteractionSurface.ReleasePointerCaptures();
    }

    private void ClearClickSuppressionAfterRouting()
    {
        if (!DispatcherQueue.TryEnqueue(() => _suppressSlotClick = false))
        {
            _suppressSlotClick = false;
        }
    }

    private void ScreenshotCoverFlowControl_Unloaded(object sender, RoutedEventArgs e) => CancelInteraction();

    private void AdvanceRing(int delta)
    {
        var direction = Math.Sign(delta);
        for (var step = 0; step < Math.Abs(delta); step++)
        {
            _ring = direction > 0 ? _ring.MoveNext() : _ring.MovePrevious();
        }

        _selectedIndex = _ring.SelectedIndex ?? 0;
    }

    private sealed class CoverSlot(
        int baseOffset,
        Button button,
        Image image,
        CompositeTransform transform,
        PlaneProjection projection)
    {
        public int RelativeOffset { get; set; } = baseOffset;

        public Button Button { get; } = button;

        public Image Image { get; } = image;

        public CompositeTransform Transform { get; } = transform;

        public PlaneProjection Projection { get; } = projection;

        public int ItemIndex { get; set; } = -1;

        public string? SourcePath { get; set; }

        public double AspectRatio { get; set; } = DefaultAspectRatio;

        public bool IsInteractive { get; set; }
    }
}
