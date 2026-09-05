// SPDX-License-Identifier: MIT

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Edits the weekly active-hours schedule with 15-minute selectable time blocks.</summary>
public sealed partial class WeeklyHoursEditor : UserControl
{
    private const int SlotsPerDay = WeeklyHoursGridProjection.SlotsPerDay;
    private const double SlotHeight = 12d;
    private const double DragMovementThreshold = 4d;
    private static IReadOnlyList<string> Days => ActiveHoursSchedule.Days;
    private readonly Dictionary<string, ToggleButton[]> _daySlots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _dayLabels = new(StringComparer.Ordinal);
    private LocalizationService _strings = new("system");
    private uint? _dragPointerId;
    private Point _dragStartPosition;
    private int _lastDragDayIndex;
    private int _lastDragSlotIndex;
    private bool? _dragSelectionValue;
    private bool _isDragging;

    /// <summary>Creates the reusable weekly hours editor.</summary>
    public WeeklyHoursEditor()
    {
        InitializeComponent();
        TimeGridHost.Height = SlotsPerDay * SlotHeight;
        DaysHost.Height = TimeGridHost.Height;
        BuildGrid();
        DaysHost.AddHandler(PointerPressedEvent, new PointerEventHandler(DaysHost_PointerPressed), true);
        DaysHost.AddHandler(PointerMovedEvent, new PointerEventHandler(DaysHost_PointerMoved), true);
        DaysHost.AddHandler(PointerReleasedEvent, new PointerEventHandler(DaysHost_PointerReleased), true);
        DaysHost.PointerCanceled += DaysHost_PointerCanceled;
        DaysHost.PointerCaptureLost += DaysHost_PointerCaptureLost;
    }

    /// <summary>Applies the selected locale to instructions, weekday names, and slot accessibility labels.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        UpdateLocalizedLabels();
    }

    /// <summary>Loads a normalized working-hours schedule into the selectable grid.</summary>
    public void LoadSchedule(IReadOnlyList<ActiveHoursDay>? schedule)
    {
        foreach (var dayName in Days)
        {
            var day = schedule?.LastOrDefault(candidate => string.Equals(candidate.Day, dayName, StringComparison.OrdinalIgnoreCase))
                ?? new ActiveHoursDay(dayName);
            var selectedSlots = WeeklyHoursGridProjection.ToSlots(day);
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                _daySlots[dayName][slot].IsChecked = selectedSlots[slot];
            }
        }

        UpdateLocalizedLabels();
    }

    /// <summary>Returns the current grid selection in the application's normalized schedule format.</summary>
    public IReadOnlyList<ActiveHoursDay> GetSchedule()
    {
        return Days.Select(day => WeeklyHoursGridProjection.FromSlots(
            day,
            _daySlots[day].Select(static button => button.IsChecked == true).ToArray())).ToArray();
    }

    /// <summary>Replaces the grid with a Monday-Friday 09:00-18:00 work week and clears weekends.</summary>
    public void ApplyStandardWorkWeek()
    {
        for (var dayIndex = 0; dayIndex < Days.Count; dayIndex++)
        {
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                _daySlots[Days[dayIndex]][slot].IsChecked = dayIndex < 5 && slot is >= 36 and < 72;
            }
        }
    }

    /// <summary>Clears every active-hours block in the editor.</summary>
    public void ClearAll()
    {
        foreach (var slots in _daySlots.Values)
        {
            foreach (var slot in slots)
            {
                slot.IsChecked = false;
            }
        }
    }

    private void BuildGrid()
    {
        var slotStyle = Resources["ScheduleSlotStyle"] as Style
            ?? throw new InvalidOperationException("The schedule slot style is required.");

        for (var dayIndex = 0; dayIndex < Days.Count; dayIndex++)
        {
            var day = Days[dayIndex];
            var label = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CharacterSpacing = 80,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Text = _strings.Culture.DateTimeFormat.GetAbbreviatedDayName(
                        Enum.Parse<DayOfWeek>(day, ignoreCase: true))
                    .TrimEnd('.')
                    .ToUpper(_strings.Culture)
            };
            _dayLabels.Add(day, label);
            Grid.SetColumn(label, dayIndex + 1);
            DaysHeaderHost.Children.Add(label);
            _daySlots.Add(day, new ToggleButton[SlotsPerDay]);
        }

        for (var slot = 0; slot < SlotsPerDay; slot++)
        {
            DaysHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SlotHeight) });
            if (slot % 4 == 0)
            {
                var timeLabel = new TextBlock
                {
                    Margin = new Thickness(0, 0, 10, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    FontSize = 9,
                    Opacity = 0.7,
                    Text = CreateSlotLabel(slot)
                };
                Grid.SetRow(timeLabel, slot);
                DaysHost.Children.Add(timeLabel);
            }

            for (var dayIndex = 0; dayIndex < Days.Count; dayIndex++)
            {
                var day = Days[dayIndex];
                var button = new ToggleButton
                {
                    Height = SlotHeight,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    CornerRadius = new CornerRadius(0),
                    Style = slotStyle,
                    Tag = slot
                };
                _daySlots[day][slot] = button;
                Grid.SetColumn(button, dayIndex + 1);
                Grid.SetRow(button, slot);
                DaysHost.Children.Add(button);
            }
        }
    }

    private void DaysHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
        {
            return;
        }

        var point = e.GetCurrentPoint(DaysHost);
        if ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
            || !TryGetSlot(point.Position, out var dayIndex, out var slotIndex, out var slot))
        {
            return;
        }

        // ToggleButton captures pointer input before this handled-events-too parent handler runs.
        // Transfer that capture now so the editor receives the full drag and owns pointer release.
        slot.ReleasePointerCapture(e.Pointer);
        if (!DaysHost.CapturePointer(e.Pointer))
        {
            return;
        }

        _dragPointerId = e.Pointer.PointerId;
        _dragStartPosition = point.Position;
        _lastDragDayIndex = dayIndex;
        _lastDragSlotIndex = slotIndex;
        _dragSelectionValue = !(slot.IsChecked == true);
        _isDragging = false;
        e.Handled = true;
    }

    private void DaysHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch
            || _dragPointerId != e.Pointer.PointerId
            || !_dragSelectionValue.HasValue)
        {
            return;
        }

        var point = e.GetCurrentPoint(DaysHost);
        if ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
            || (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse && !point.IsInContact))
        {
            return;
        }

        var position = point.Position;
        var hasTargetSlot = TryGetSlot(position, out var dayIndex, out var slotIndex, out _);
        if (!_isDragging)
        {
            var horizontalMovement = Math.Abs(position.X - _dragStartPosition.X);
            var verticalMovement = Math.Abs(position.Y - _dragStartPosition.Y);
            var enteredAnotherSlot = hasTargetSlot
                && (dayIndex != _lastDragDayIndex || slotIndex != _lastDragSlotIndex);
            if (!enteredAnotherSlot
                && horizontalMovement <= DragMovementThreshold
                && verticalMovement <= DragMovementThreshold)
            {
                return;
            }

            _isDragging = true;
            _daySlots[Days[_lastDragDayIndex]][_lastDragSlotIndex].IsChecked = _dragSelectionValue.Value;
        }

        if (hasTargetSlot)
        {
            ApplyDragPath(dayIndex, slotIndex, _dragSelectionValue.Value);
        }

        e.Handled = true;
    }

    private void DaysHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch || _dragPointerId != e.Pointer.PointerId)
        {
            return;
        }

        if (!_isDragging && _dragSelectionValue.HasValue)
        {
            _daySlots[Days[_lastDragDayIndex]][_lastDragSlotIndex].IsChecked = _dragSelectionValue.Value;
        }

        e.Handled = true;
        ClearDragGesture();
        DaysHost.ReleasePointerCapture(e.Pointer);
    }

    private void DaysHost_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_dragPointerId == e.Pointer.PointerId)
        {
            ClearDragGesture();
        }
    }

    private void DaysHost_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, DaysHost) && _dragPointerId == e.Pointer.PointerId)
        {
            ClearDragGesture();
        }
    }

    private void ClearDragGesture()
    {
        _dragPointerId = null;
        _dragSelectionValue = null;
        _isDragging = false;
    }

    private void ApplyDragPath(int targetDayIndex, int targetSlotIndex, bool selectionValue)
    {
        var dayIndex = _lastDragDayIndex;
        var slotIndex = _lastDragSlotIndex;
        var dayDistance = Math.Abs(targetDayIndex - dayIndex);
        var dayStep = dayIndex < targetDayIndex ? 1 : -1;
        var slotDistance = -Math.Abs(targetSlotIndex - slotIndex);
        var slotStep = slotIndex < targetSlotIndex ? 1 : -1;
        var error = dayDistance + slotDistance;

        while (true)
        {
            _daySlots[Days[dayIndex]][slotIndex].IsChecked = selectionValue;
            if (dayIndex == targetDayIndex && slotIndex == targetSlotIndex)
            {
                break;
            }

            var doubledError = 2 * error;
            if (doubledError >= slotDistance)
            {
                error += slotDistance;
                dayIndex += dayStep;
            }

            if (doubledError <= dayDistance)
            {
                error += dayDistance;
                slotIndex += slotStep;
            }
        }

        _lastDragDayIndex = targetDayIndex;
        _lastDragSlotIndex = targetSlotIndex;
    }

    private bool TryGetSlot(Point position, out int dayIndex, out int slotIndex, out ToggleButton slot)
    {
        dayIndex = -1;
        slotIndex = -1;
        slot = null!;

        if (position.X < 0 || position.Y < 0 || position.Y >= DaysHost.ActualHeight)
        {
            return false;
        }

        var columnStart = DaysHost.ColumnDefinitions[0].ActualWidth;
        for (var candidateDayIndex = 0; candidateDayIndex < Days.Count; candidateDayIndex++)
        {
            var columnWidth = DaysHost.ColumnDefinitions[candidateDayIndex + 1].ActualWidth;
            if (position.X >= columnStart && position.X < columnStart + columnWidth)
            {
                dayIndex = candidateDayIndex;
                break;
            }

            columnStart += columnWidth;
        }

        if (dayIndex < 0 || DaysHost.ActualHeight <= 0)
        {
            return false;
        }

        slotIndex = (int)Math.Floor(position.Y / (DaysHost.ActualHeight / SlotsPerDay));
        if (slotIndex < 0 || slotIndex >= SlotsPerDay)
        {
            return false;
        }

        slot = _daySlots[Days[dayIndex]][slotIndex];
        return true;
    }

    private void UpdateLocalizedLabels()
    {
        foreach (var day in Days)
        {
            var dayOfWeek = Enum.Parse<DayOfWeek>(day, ignoreCase: true);
            var dayName = _strings.Culture.DateTimeFormat.GetDayName(dayOfWeek);
            _dayLabels[day].Text = _strings.Culture.DateTimeFormat.GetAbbreviatedDayName(dayOfWeek)
                .TrimEnd('.')
                .ToUpper(_strings.Culture);
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                AutomationProperties.SetName(
                    _daySlots[day][slot],
                    _strings.Format("Schedule.Slot.Accessible", dayName, CreateSlotLabel(slot), CreateSlotLabel(slot + 1)));
            }
        }
    }

    private static string CreateSlotLabel(int slot) => WeeklyHoursGridProjection.FormatBoundary(slot);
}
