// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Edits the weekly active-hours schedule with 30-minute selectable time blocks.</summary>
public sealed partial class WeeklyHoursEditor : UserControl
{
    private const int SlotsPerDay = 48;
    private const double TimeLabelWidth = 64d;
    private const double DayColumnWidth = 96d;
    private const double SlotHeight = 6.5d;
    private static IReadOnlyList<string> Days => ActiveHoursSchedule.Days;
    private readonly Dictionary<string, ToggleButton[]> _daySlots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _dayLabels = new(StringComparer.Ordinal);
    private LocalizationService _strings = new("system");
    private bool? _dragSelectionValue;

    /// <summary>Creates the reusable weekly hours editor.</summary>
    public WeeklyHoursEditor()
    {
        InitializeComponent();
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
            var selectedSlots = ParseSelectedSlots(day);
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
        return Days.Select(day => CreateDaySchedule(day, _daySlots[day])).ToArray();
    }

    /// <summary>Replaces the grid with a Monday-Friday 09:00-18:00 work week and clears weekends.</summary>
    public void ApplyStandardWorkWeek()
    {
        for (var dayIndex = 0; dayIndex < Days.Count; dayIndex++)
        {
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                _daySlots[Days[dayIndex]][slot].IsChecked = dayIndex < 5 && slot is >= 18 and < 36;
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
                    Width = DayColumnWidth,
                    Height = SlotHeight,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    CornerRadius = new CornerRadius(0),
                    IsHitTestVisible = false,
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
        if (!TryGetSlot(e, out var slot))
        {
            return;
        }

        _dragSelectionValue = !(slot.IsChecked == true);
        slot.IsChecked = _dragSelectionValue;
        DaysHost.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void DaysHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragSelectionValue.HasValue && e.Pointer.IsInContact && TryGetSlot(e, out var slot))
        {
            slot.IsChecked = _dragSelectionValue;
            e.Handled = true;
        }
    }

    private void DaysHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragSelectionValue = null;
        DaysHost.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void DaysHost_PointerCanceled(object sender, PointerRoutedEventArgs e) => _dragSelectionValue = null;

    private void DaysHost_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => _dragSelectionValue = null;

    private bool TryGetSlot(PointerRoutedEventArgs e, out ToggleButton slot)
    {
        var position = e.GetCurrentPoint(DaysHost).Position;
        var dayIndex = (int)Math.Floor((position.X - TimeLabelWidth) / DayColumnWidth);
        var slotIndex = (int)Math.Floor(position.Y / SlotHeight);
        if (dayIndex < 0 || dayIndex >= Days.Count || slotIndex < 0 || slotIndex >= SlotsPerDay)
        {
            slot = null!;
            return false;
        }

        slot = _daySlots[Days[dayIndex]][slotIndex];
        return true;
    }

    private static bool[] ParseSelectedSlots(ActiveHoursDay day)
    {
        var slots = new bool[SlotsPerDay];
        if (!ActiveHoursSchedule.TryParseRange(day.ActivePeriod, out var activeStart, out var activeEnd))
        {
            return slots;
        }

        SetRange(slots, activeStart, activeEnd, true);
        foreach (var period in day.BreakPeriods.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ActiveHoursSchedule.TryParseRange(period, out var breakStart, out var breakEnd))
            {
                SetRange(slots, breakStart, breakEnd, false);
            }
        }

        return slots;
    }

    private static ActiveHoursDay CreateDaySchedule(string day, IReadOnlyList<ToggleButton> slots)
    {
        var firstSelected = Array.FindIndex(slots.ToArray(), button => button.IsChecked == true);
        if (firstSelected < 0)
        {
            return new ActiveHoursDay(day);
        }

        var lastSelected = Array.FindLastIndex(slots.ToArray(), button => button.IsChecked == true);
        var breaks = new List<string>();
        var slot = firstSelected;
        while (slot <= lastSelected)
        {
            if (slots[slot].IsChecked == true)
            {
                slot++;
                continue;
            }

            var breakStart = slot;
            while (slot <= lastSelected && slots[slot].IsChecked != true)
            {
                slot++;
            }

            breaks.Add($"{CreateSlotLabel(breakStart)}-{CreateSlotLabel(slot)}");
        }

        return new ActiveHoursDay(
            day,
            $"{CreateSlotLabel(firstSelected)}-{CreateSlotLabel(lastSelected + 1)}",
            string.Join(", ", breaks));
    }

    private static void SetRange(bool[] slots, int startMinutes, int endMinutes, bool value)
    {
        var startSlot = startMinutes / 30;
        var endSlot = endMinutes / 30;
        for (var slot = startSlot; slot < endSlot; slot++)
        {
            slots[slot] = value;
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
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                AutomationProperties.SetName(
                    _daySlots[day][slot],
                    _strings.Format("Schedule.Slot.Accessible", dayName, CreateSlotLabel(slot), CreateSlotLabel(slot + 1)));
            }
        }
    }

    private static string CreateSlotLabel(int slot) => $"{slot / 2:00}:{(slot % 2) * 30:00}";
}
