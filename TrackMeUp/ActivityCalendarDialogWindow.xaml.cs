using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Graphics;
using Windows.System;
using Windows.UI;

namespace TrackMeUp;

/// <summary>Shows a native rolling activity calendar backed only by aggregate application-layer report data.</summary>
internal sealed partial class ActivityCalendarDialogWindow : Window
{
    private const int ExpectedReportContractVersion = 4;
    private const int LogicalWidth = 860;
    private const int LogicalHeight = 620;
    private const int LogicalScreenMargin = 24;
    private const int GwlHwndParent = -8;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopMost = new(-1);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly ITrackMeUpApplication _application;
    private readonly LocalizationService _strings;
    private readonly CultureInfo _culture;
    private readonly AppWindow _appWindow;
    private readonly AppWindow _ownerAppWindow;
    private readonly WindowPlacementService _placement;
    private readonly IntPtr _windowHandle;
    private IReadOnlyDictionary<DateOnly, ReportCalendarCell> _recordedDays = new Dictionary<DateOnly, ReportCalendarCell>();
    private bool _isCompleting;
    private bool _isLoaded;

    /// <summary>Creates a passive calendar dialog that obtains daily aggregates through the application facade.</summary>
    internal ActivityCalendarDialogWindow(
        ITrackMeUpApplication application,
        ElementTheme theme,
        LocalizationService strings,
        AppWindow ownerAppWindow,
        IntPtr ownerHandle)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        _ownerAppWindow = ownerAppWindow ?? throw new ArgumentNullException(nameof(ownerAppWindow));
        _culture = CultureInfo.GetCultureInfo(_strings.Language);
        InitializeComponent();
        Title = T("ActivityCalendar.Title");
        RootGrid.RequestedTheme = theme;
        RootGrid.Language = _strings.Language;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.ActivityCalendar,
            LogicalWidth,
            LogicalHeight,
            LogicalScreenMargin,
            ownerAppWindow.Id);
        SetWindowOwner(_windowHandle, ownerHandle);
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        ApplyLocalizedContent();
        Closed += ActivityCalendarDialogWindow_Closed;
    }

    /// <summary>Activates the queued acrylic surface and completes after closure.</summary>
    internal Task ShowAsync()
    {
        SetWindowPos(_windowHandle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        Activate();
        return _completion.Task;
    }

    internal IntPtr WindowHandle => _windowHandle;

    internal void DisposePlacement()
    {
        _placement.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        var scale = Math.Max(0.1d, RootGrid.XamlRoot?.RasterizationScale ?? 1d);
        var area = DisplayArea.GetFromWindowId(_ownerAppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var margin = (int)Math.Ceiling(LogicalScreenMargin * scale);
        var width = Math.Clamp(
            (int)Math.Ceiling(LogicalWidth * scale),
            1,
            Math.Max(1, area.Width - (margin * 2)));
        var height = Math.Clamp(
            (int)Math.Ceiling(LogicalHeight * scale),
            1,
            Math.Max(1, area.Height - (margin * 2)));
        _appWindow.Resize(new SizeInt32(width, height));

        await _placement.RestoreAndCenterAsync(RootGrid, CancellationToken.None);
        CloseButton.Focus(FocusState.Programmatic);
        await LoadCalendarAsync();
    }

    private async Task LoadCalendarAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(-365);
        StatusText.Text = T("ActivityCalendar.Loading");

        try
        {
            var result = await _application.GetReportAsync(
                new ReportQuery(from, today, string.Empty, ReportView.Calendar),
                _lifetimeCancellation.Token);
            if (!result.Succeeded || result.Value is null)
            {
                ShowError(string.Format(_culture, T("ActivityCalendar.Error"), result.Code));
                return;
            }

            if (!TryApplySnapshot(result.Value))
            {
                ShowError(T("ActivityCalendar.InvalidData"));
                return;
            }

            ShowCalendar(from, today);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Closing the dialog cancels the aggregate report request; no stale UI state is applied.
        }
        catch (Exception)
        {
            ShowError(T("ActivityCalendar.Unavailable"));
        }
    }

    private bool TryApplySnapshot(ReportSnapshot snapshot)
    {
        if (snapshot.ContractVersion != ExpectedReportContractVersion)
        {
            return false;
        }

        var recordedDays = new Dictionary<DateOnly, ReportCalendarCell>();
        var reportDates = new HashSet<DateOnly>();
        foreach (var cell in snapshot.Calendar)
        {
            if (!reportDates.Add(cell.Date) || !IsValidCell(cell))
            {
                return false;
            }

            if (cell.HasData && !recordedDays.TryAdd(cell.Date, cell))
            {
                return false;
            }
        }

        _recordedDays = recordedDays;
        return true;
    }

    private static bool IsValidCell(ReportCalendarCell cell) =>
        cell.ActiveSeconds >= 0 &&
        cell.IdleSeconds >= 0 &&
        cell.TrackedSeconds >= 0 &&
        cell.KeyPresses >= 0 &&
        cell.MouseClicks >= 0 &&
        cell.SampleCount >= 0 &&
        (cell.HasData
            ? cell.ActivityScore is >= 0 and <= 100
            : cell.ActivityScore is null);

    private void ShowCalendar(DateOnly from, DateOnly today)
    {
        // Apply the range only after the report map exists so newly realized day items receive deterministic markers.
        ActivityCalendarView.MinDate = ToCalendarDate(from);
        ActivityCalendarView.MaxDate = ToCalendarDate(today);
        LoadingRing.IsActive = false;
        StatusPanel.Visibility = Visibility.Collapsed;
        CalendarPanel.Visibility = Visibility.Visible;
        DayDetailsBorder.Visibility = Visibility.Visible;
        CalendarLegendText.Text = _recordedDays.Count == 0
            ? T("ActivityCalendar.Empty")
            : string.Format(_culture, T("ActivityCalendar.Legend"), _recordedDays.Count);

        var selectedDate = _recordedDays.ContainsKey(today)
            ? today
            : _recordedDays.Keys.DefaultIfEmpty(today).Max();
        var calendarDate = ToCalendarDate(selectedDate);
        ActivityCalendarView.SelectedDates.Clear();
        ActivityCalendarView.SelectedDates.Add(calendarDate);
        ActivityCalendarView.SetDisplayDate(calendarDate);
        UpdateSelectedDay(selectedDate);
    }

    private void ActivityCalendarView_CalendarViewDayItemChanging(
        CalendarView sender,
        CalendarViewDayItemChangingEventArgs args)
    {
        var date = FromCalendarDate(args.Item.Date);
        if (!_recordedDays.TryGetValue(date, out var cell))
        {
            args.Item.SetDensityColors(Array.Empty<Color>());
            var noDataLabel = string.Format(
                _culture,
                T("ActivityCalendar.Day.NoDataAccessible"),
                date.ToString("D", _culture));
            AutomationProperties.SetName(args.Item, noDataLabel);
            ToolTipService.SetToolTip(args.Item, noDataLabel);
            return;
        }

        var score = cell.ActivityScore!.Value;
        var densityCount = Math.Clamp(((score + 24) / 25), 1, 4);
        args.Item.SetDensityColors(Enumerable.Repeat(ActivityDensityColor(), densityCount));
        var label = string.Format(
            _culture,
            T("ActivityCalendar.Day.ScoreAccessible"),
            date.ToString("D", _culture),
            score);
        AutomationProperties.SetName(args.Item, label);
        ToolTipService.SetToolTip(args.Item, label);
    }

    private void ActivityCalendarView_SelectedDatesChanged(
        CalendarView sender,
        CalendarViewSelectedDatesChangedEventArgs args)
    {
        if (args.AddedDates.Count > 0)
        {
            UpdateSelectedDay(FromCalendarDate(args.AddedDates[0]));
        }
    }

    private void UpdateSelectedDay(DateOnly date)
    {
        SelectedDateText.Text = date.ToString("D", _culture);
        if (!_recordedDays.TryGetValue(date, out var cell))
        {
            DayStatusText.Text = T("ActivityCalendar.NoActivity");
            DayMetricsPanel.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(
                DayDetailsBorder,
                string.Format(_culture, T("ActivityCalendar.Day.NoDataAccessible"), SelectedDateText.Text));
            return;
        }

        var score = cell.ActivityScore!.Value;
        DayStatusText.Text = T("ActivityCalendar.RecordedActivity");
        ScoreValueText.Text = score.ToString("N0", _culture);
        ScoreProgressBar.Value = score;
        ActiveTimeValueText.Text = FormatDuration(cell.ActiveSeconds);
        IdleTimeValueText.Text = FormatDuration(cell.IdleSeconds);
        TrackedTimeValueText.Text = FormatDuration(cell.TrackedSeconds);
        KeyPressesValueText.Text = cell.KeyPresses.ToString("N0", _culture);
        MouseClicksValueText.Text = cell.MouseClicks.ToString("N0", _culture);
        SamplesValueText.Text = cell.SampleCount.ToString("N0", _culture);
        DayMetricsPanel.Visibility = Visibility.Visible;

        var scoreLabel = string.Format(_culture, T("ActivityCalendar.ScoreAccessible"), score);
        AutomationProperties.SetName(ScoreValueText, scoreLabel);
        AutomationProperties.SetName(DayDetailsBorder, $"{SelectedDateText.Text}. {scoreLabel}");
    }

    private void ApplyLocalizedContent()
    {
        DialogTitleText.Text = T("ActivityCalendar.Title");
        DialogSubtitleText.Text = T("ActivityCalendar.Subtitle");
        StatusText.Text = T("ActivityCalendar.Loading");
        ScoreLabelText.Text = T("ActivityCalendar.Score");
        ActiveTimeLabelText.Text = T("ActivityCalendar.ActiveTime");
        IdleTimeLabelText.Text = T("ActivityCalendar.IdleTime");
        TrackedTimeLabelText.Text = T("ActivityCalendar.TrackedTime");
        KeyPressesLabelText.Text = T("ActivityCalendar.KeyPresses");
        MouseClicksLabelText.Text = T("ActivityCalendar.MouseClicks");
        SamplesLabelText.Text = T("ActivityCalendar.Samples");
        CloseButton.Content = T("About.Close");
        AutomationProperties.SetName(RootGrid, T("ActivityCalendar.Title"));
        AutomationProperties.SetName(DialogTitleText, DialogTitleText.Text);
        AutomationProperties.SetName(DialogSubtitleText, DialogSubtitleText.Text);
        AutomationProperties.SetName(ActivityCalendarView, T("ActivityCalendar.Title"));
        AutomationProperties.SetName(CloseButton, T("About.Close"));
    }

    private void ShowError(string message)
    {
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
        StatusIcon.Visibility = Visibility.Visible;
        StatusText.Text = message;
    }

    private string FormatDuration(long seconds)
    {
        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        var remainingSeconds = seconds % 60;
        return string.Format(
            _culture,
            T("ActivityCalendar.Duration"),
            hours,
            minutes,
            remainingSeconds);
    }

    private string T(string key) => _strings.Translate(key);

    private static DateTimeOffset ToCalendarDate(DateOnly date) =>
        new(new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local));

    private static DateOnly FromCalendarDate(DateTimeOffset date) => DateOnly.FromDateTime(date.Date);

    private static Color ActivityDensityColor() =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("SystemAccentColor", out var resource) && resource is Color color
            ? color
            : Colors.CornflowerBlue;

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        await CompleteAsync();
    }

    private async void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;
        await CompleteAsync();
    }

    private async Task CompleteAsync()
    {
        if (_isCompleting)
        {
            return;
        }

        _isCompleting = true;
        CloseButton.IsEnabled = false;
        _lifetimeCancellation.Cancel();
        await _placement.SaveAsync(CancellationToken.None);
        Close();
    }

    private void ActivityCalendarDialogWindow_Closed(object sender, WindowEventArgs args)
    {
        _lifetimeCancellation.Cancel();
        _completion.TrySetResult();
    }

    private static void SetWindowOwner(IntPtr windowHandle, IntPtr ownerHandle)
    {
        if (ownerHandle == IntPtr.Zero)
        {
            return;
        }

        _ = IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, GwlHwndParent, ownerHandle)
            : new IntPtr(SetWindowLongPtr32(windowHandle, GwlHwndParent, ownerHandle.ToInt32()));
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLongPtr32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
