// SPDX-License-Identifier: MIT

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Edits the weekly active-hours schedule with 15-minute selectable time blocks.</summary>
public sealed partial class WeeklyHoursEditor : UserControl
{
    private const int SlotsPerDay = WeeklyHoursGridProjection.SlotsPerDay;
    private const int HoursPerDay = 24;
    private const int SlotsPerHour = SlotsPerDay / HoursPerDay;
    private const int SlotGridColumns = 1;
    private const double HourRowHeight = 24d;
    private const double DragMovementThreshold = 4d;
    private const double SevenColumnBreakpoint = 780d;
    private const double FourColumnBreakpoint = 500d;
    private const double TwoColumnBreakpoint = 300d;
    private static IReadOnlyList<string> Days => ActiveHoursSchedule.Days;
    private readonly Dictionary<string, ToggleButton[]> _daySlots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _dayLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _daySummaries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _dayCards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Grid> _dayTimelines = new(StringComparer.Ordinal);
    private readonly Dictionary<ToggleButton, string> _slotDays = new();
    private LocalizationService _strings = new("system");
    private int _responsiveColumnCount;
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
        BuildGrid();
        ApplyResponsiveLayout(SevenColumnBreakpoint);
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
        UpdateAllDaySummaries();
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

        UpdateAllDaySummaries();
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

        UpdateAllDaySummaries();
    }

    private void BuildGrid()
    {
        var slotStyle = RequiredStyle("ScheduleSlotStyle");
        var regularCardStyle = RequiredStyle("ScheduleDayCardStyle");
        var weekendCardStyle = RequiredStyle("ScheduleWeekendDayCardStyle");
        var regularDayLabelStyle = RequiredStyle("ScheduleDayLabelStyle");
        var weekendDayLabelStyle = RequiredStyle("ScheduleWeekendDayLabelStyle");
        var timeLabelStyle = RequiredStyle("ScheduleTimeLabelStyle");
        var regularSummaryFooterStyle = RequiredStyle("ScheduleSummaryFooterStyle");
        var weekendSummaryFooterStyle = RequiredStyle("ScheduleWeekendSummaryFooterStyle");
        var regularSummaryTextStyle = RequiredStyle("ScheduleSummaryTextStyle");
        var weekendSummaryTextStyle = RequiredStyle("ScheduleWeekendSummaryTextStyle");

        for (var dayIndex = 0; dayIndex < Days.Count; dayIndex++)
        {
            var day = Days[dayIndex];
            var isWeekend = dayIndex >= 5;
            var label = new TextBlock
            {
                Style = isWeekend ? weekendDayLabelStyle : regularDayLabelStyle
            };
            var summary = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Style = isWeekend ? weekendSummaryTextStyle : regularSummaryTextStyle,
                Text = "—"
            };
            var timeline = BuildDayTimeline(day, slotStyle, timeLabelStyle);
            var summaryFooter = new Border
            {
                Style = isWeekend ? weekendSummaryFooterStyle : regularSummaryFooterStyle,
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            FontFamily = new FontFamily("Segoe Fluent Icons"),
                            FontSize = 12,
                            Text = "\uE823",
                            VerticalAlignment = VerticalAlignment.Center,
                            Style = isWeekend ? weekendSummaryTextStyle : regularSummaryTextStyle
                        },
                        summary
                    }
                }
            };
            var cardContent = new Grid
            {
                RowSpacing = 8,
                Children =
                {
                    label,
                    timeline,
                    summaryFooter
                }
            };
            cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(timeline, 1);
            Grid.SetRow(summaryFooter, 2);

            var card = new Border
            {
                Style = isWeekend ? weekendCardStyle : regularCardStyle,
                Child = cardContent
            };
            _dayLabels.Add(day, label);
            _daySummaries.Add(day, summary);
            _dayCards.Add(day, card);
            DaysHost.Children.Add(card);
        }
    }

    private Grid BuildDayTimeline(string day, Style slotStyle, Style timeLabelStyle)
    {
        var timeAxis = new Grid
        {
            Width = 39,
            Height = HoursPerDay * HourRowHeight,
            VerticalAlignment = VerticalAlignment.Top
        };
        var timeline = new Grid
        {
            Height = HoursPerDay * HourRowHeight,
            MinWidth = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        _daySlots.Add(day, new ToggleButton[SlotsPerDay]);
        _dayTimelines.Add(day, timeline);

        for (var hour = 0; hour < HoursPerDay; hour++)
        {
            timeAxis.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HourRowHeight) });
            timeline.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HourRowHeight) });
            var hourGrid = new Grid();
            for (var column = 0; column < SlotGridColumns; column++)
            {
                hourGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (var row = 0; row < SlotsPerHour / SlotGridColumns; row++)
            {
                hourGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            for (var quarter = 0; quarter < SlotsPerHour; quarter++)
            {
                var slot = (hour * SlotsPerHour) + quarter;
                var button = new ToggleButton
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    CornerRadius = new CornerRadius(0),
                    Style = slotStyle,
                    Tag = slot
                };
                button.Checked += Slot_CheckedChanged;
                button.Unchecked += Slot_CheckedChanged;
                _daySlots[day][slot] = button;
                _slotDays.Add(button, day);
                Grid.SetColumn(button, quarter % SlotGridColumns);
                Grid.SetRow(button, quarter / SlotGridColumns);
                hourGrid.Children.Add(button);
            }

            Grid.SetRow(hourGrid, hour);
            timeline.Children.Add(hourGrid);
        }

        foreach (var hour in new[] { 0, 6, 12, 18 })
        {
            var timeLabel = new TextBlock
            {
                Style = timeLabelStyle,
                Text = $"{hour:00}:00",
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(timeLabel, hour);
            timeAxis.Children.Add(timeLabel);
        }

        var endLabel = new TextBlock
        {
            Style = timeLabelStyle,
            Text = "24:00",
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Grid.SetRow(endLabel, HoursPerDay - 1);
        timeAxis.Children.Add(endLabel);

        var timelineHost = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                timeAxis,
                timeline
            }
        };
        timelineHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        timelineHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(timeline, 1);
        return timelineHost;
    }

    private void DaysHost_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout(e.NewSize.Width);

    private void ApplyResponsiveLayout(double availableWidth)
    {
        var columnCount = availableWidth >= SevenColumnBreakpoint
            ? 7
            : availableWidth >= FourColumnBreakpoint
                ? 4
                : availableWidth >= TwoColumnBreakpoint
                    ? 2
                    : 1;
        if (columnCount == _responsiveColumnCount)
        {
            return;
        }

        _responsiveColumnCount = columnCount;
        DaysHost.ColumnDefinitions.Clear();
        DaysHost.RowDefinitions.Clear();
        for (var column = 0; column < columnCount; column++)
        {
            DaysHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rowCount = (int)Math.Ceiling(Days.Count / (double)columnCount);
        for (var row = 0; row < rowCount; row++)
        {
            DaysHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var dayIndex = 0; dayIndex < Days.Count; dayIndex++)
        {
            var card = _dayCards[Days[dayIndex]];
            Grid.SetColumn(card, dayIndex % columnCount);
            Grid.SetRow(card, dayIndex / columnCount);
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
        if (position.X < 0 || position.Y < 0)
        {
            return false;
        }

        for (var candidateDayIndex = 0; candidateDayIndex < Days.Count; candidateDayIndex++)
        {
            var timeline = _dayTimelines[Days[candidateDayIndex]];
            if (timeline.ActualWidth <= 0 || timeline.ActualHeight <= 0)
            {
                continue;
            }

            var origin = timeline.TransformToVisual(DaysHost).TransformPoint(new Point(0, 0));
            var relativeX = position.X - origin.X;
            var relativeY = position.Y - origin.Y;
            if (relativeX < 0
                || relativeX >= timeline.ActualWidth
                || relativeY < 0
                || relativeY >= timeline.ActualHeight)
            {
                continue;
            }

            dayIndex = candidateDayIndex;
            var hourHeight = timeline.ActualHeight / HoursPerDay;
            var hour = Math.Min(HoursPerDay - 1, (int)Math.Floor(relativeY / hourHeight));
            var withinHourY = relativeY - (hour * hourHeight);
            var segmentRow = Math.Min(
                (SlotsPerHour / SlotGridColumns) - 1,
                (int)Math.Floor(withinHourY / (hourHeight / (SlotsPerHour / SlotGridColumns))));
            var segmentColumn = Math.Min(
                SlotGridColumns - 1,
                (int)Math.Floor(relativeX / (timeline.ActualWidth / SlotGridColumns)));
            slotIndex = (hour * SlotsPerHour) + (segmentRow * SlotGridColumns) + segmentColumn;
            slot = _daySlots[Days[dayIndex]][slotIndex];
            return true;
        }

        return false;
    }

    private void Slot_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && _slotDays.TryGetValue(button, out var day))
        {
            UpdateDaySummary(day);
        }
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
            AutomationProperties.SetName(_dayCards[day], dayName);
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                AutomationProperties.SetName(
                    _daySlots[day][slot],
                    _strings.Format("Schedule.Slot.Accessible", dayName, CreateSlotLabel(slot), CreateSlotLabel(slot + 1)));
            }

            UpdateDaySummary(day);
        }
    }

    private void UpdateAllDaySummaries()
    {
        foreach (var day in Days)
        {
            UpdateDaySummary(day);
        }
    }

    private void UpdateDaySummary(string day)
    {
        var schedule = WeeklyHoursGridProjection.FromSlots(
            day,
            _daySlots[day].Select(static button => button.IsChecked == true).ToArray());
        var summary = string.IsNullOrEmpty(schedule.ActivePeriod)
            ? "—"
            : schedule.ActivePeriod.Replace("-", "–", StringComparison.Ordinal);
        _daySummaries[day].Text = summary;
        var dayName = _strings.Culture.DateTimeFormat.GetDayName(Enum.Parse<DayOfWeek>(day, ignoreCase: true));
        AutomationProperties.SetName(_daySummaries[day], $"{dayName}: {summary}");
    }

    private Style RequiredStyle(string key) => Resources[key] as Style
        ?? throw new InvalidOperationException($"The schedule style '{key}' is required.");

    private static string CreateSlotLabel(int slot) => WeeklyHoursGridProjection.FormatBoundary(slot);
}
