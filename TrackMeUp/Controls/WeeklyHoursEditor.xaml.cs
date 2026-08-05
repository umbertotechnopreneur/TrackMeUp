using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace TrackMeUp.Controls;

/// <summary>Edits the weekly active-hours schedule with 30-minute selectable time blocks.</summary>
public sealed partial class WeeklyHoursEditor : UserControl
{
    private const int SlotsPerDay = 48;
    private const double SlotWidth = 14d;
    private const double SlotHeight = 28d;
    private static readonly string[] Days = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];
    private readonly Dictionary<string, ToggleButton[]> _daySlots = new(StringComparer.Ordinal);
    private bool? _dragSelectionValue;

    /// <summary>Creates the reusable weekly hours editor.</summary>
    public WeeklyHoursEditor()
    {
        InitializeComponent();
        BuildGrid();
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
    }

    /// <summary>Returns the current grid selection in the application's normalized schedule format.</summary>
    public IReadOnlyList<ActiveHoursDay> GetSchedule()
    {
        return Days.Select(day => CreateDaySchedule(day, _daySlots[day])).ToArray();
    }

    private void BuildGrid()
    {
        var slotStyle = Resources["ScheduleSlotStyle"] as Style
            ?? throw new InvalidOperationException("The schedule slot style is required.");

        foreach (var day in Days)
        {
            var row = new Grid { ColumnSpacing = 0, Height = SlotHeight };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SlotsPerDay * SlotWidth) });

            var label = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Text = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(
                    Enum.Parse<DayOfWeek>(day, ignoreCase: true))
            };
            row.Children.Add(label);

            var slotsGrid = new Grid { Width = SlotsPerDay * SlotWidth, Height = SlotHeight };
            for (var column = 0; column < SlotsPerDay; column++)
            {
                slotsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SlotWidth) });
            }

            Grid.SetColumn(slotsGrid, 1);
            var slots = new ToggleButton[SlotsPerDay];
            for (var slot = 0; slot < SlotsPerDay; slot++)
            {
                var button = new ToggleButton
                {
                    Width = SlotWidth,
                    Height = SlotHeight,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    CornerRadius = new CornerRadius(0),
                    Style = slotStyle,
                    Tag = slot
                };
                ToolTipService.SetToolTip(button, CreateSlotLabel(slot));
                button.PointerPressed += Slot_PointerPressed;
                button.PointerEntered += Slot_PointerEntered;
                button.PointerReleased += Slot_PointerReleased;
                slots[slot] = button;
                Grid.SetColumn(button, slot);
                slotsGrid.Children.Add(button);
            }

            _daySlots.Add(day, slots);
            row.Children.Add(slotsGrid);
            DaysHost.Children.Add(row);
        }
    }

    private void Slot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ToggleButton slot)
        {
            return;
        }

        _dragSelectionValue = !(slot.IsChecked == true);
        slot.IsChecked = _dragSelectionValue;
        slot.CapturePointer(e.Pointer);
    }

    private void Slot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_dragSelectionValue.HasValue && sender is ToggleButton slot && e.Pointer.IsInContact)
        {
            slot.IsChecked = _dragSelectionValue;
        }
    }

    private void Slot_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragSelectionValue = null;
        if (sender is UIElement slot)
        {
            slot.ReleasePointerCaptures();
        }
    }

    private static bool[] ParseSelectedSlots(ActiveHoursDay day)
    {
        var slots = new bool[SlotsPerDay];
        if (!TryParseRange(day.ActivePeriod, out var activeStart, out var activeEnd))
        {
            return slots;
        }

        SetRange(slots, activeStart, activeEnd, true);
        foreach (var period in day.BreakPeriods.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseRange(period, out var breakStart, out var breakEnd))
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

    private static void SetRange(bool[] slots, TimeOnly start, TimeOnly end, bool value)
    {
        var startSlot = start.Hour * 2 + start.Minute / 30;
        var endSlot = end.Hour * 2 + end.Minute / 30;
        for (var slot = startSlot; slot < endSlot; slot++)
        {
            slots[slot] = value;
        }
    }

    private static bool TryParseRange(string value, out TimeOnly start, out TimeOnly end)
    {
        start = default;
        end = default;
        var parts = value.Split('-', StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && TimeOnly.TryParseExact(parts[0], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)
            && TimeOnly.TryParseExact(parts[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out end);
    }

    private static string CreateSlotLabel(int slot) => $"{slot / 2:00}:{(slot % 2) * 30:00}";
}
