using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.System;

namespace TrackMeUp;

/// <summary>Collects one world-clock city selection without owning catalog or persistence behavior.</summary>
internal sealed partial class WorldClockCityPickerDialogWindow : Window
{
    private const int LogicalWidth = 500;
    private const int LogicalHeight = 560;
    private const int LogicalScreenMargin = 24;
    private readonly TaskCompletionSource<string?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IReadOnlyList<WorldClockCityPickerOption> _options;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly IntPtr _windowHandle;
    private string? _result;
    private bool _isCompleting;

    internal WorldClockCityPickerDialogWindow(
        ITrackMeUpApplication application,
        IReadOnlyList<WorldClockCitySummary> cities,
        ElementTheme theme,
        LocalizationService strings,
        AppWindow ownerAppWindow,
        IntPtr ownerHandle)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(cities);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(ownerAppWindow);
        _options = cities
            .Select(city => new WorldClockCityPickerOption(city.Id, $"{city.Name} · {city.CountryCode}"))
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        InitializeComponent();
        Title = strings.Translate("WorldClock.PickerTitle");
        RootGrid.RequestedTheme = theme;
        RootGrid.Language = strings.Language;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.Dialog,
            LogicalWidth,
            LogicalHeight,
            LogicalScreenMargin,
            ownerAppWindow.Id);
        WindowInteropService.SetOwner(_windowHandle, ownerHandle);
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        DialogTitleText.Text = Title;
        CitySearchBox.PlaceholderText = strings.Translate("WorldClock.SearchCity");
        CancelButton.Content = strings.Translate("Dialog.Cancel");
        AddButton.Content = strings.Translate("WorldClock.Add");
        AutomationProperties.SetName(RootGrid, Title);
        AutomationProperties.SetName(DialogTitleText, Title);
        AutomationProperties.SetName(CitySearchBox, strings.Translate("WorldClock.SearchCity"));
        AutomationProperties.SetName(CityList, Title);
        AutomationProperties.SetName(CancelButton, strings.Translate("Dialog.Cancel"));
        AutomationProperties.SetName(AddButton, strings.Translate("WorldClock.Add"));
        CityList.ItemsSource = _options;
        Closed += (_, _) => _completion.TrySetResult(_result);
    }

    internal IntPtr WindowHandle => _windowHandle;

    internal Task<string?> ShowAsync()
    {
        WindowInteropService.MakeTopmostWithoutActivation(_windowHandle);
        Activate();
        return _completion.Task;
    }

    internal void DisposePlacement() => _placement.Dispose();

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _placement.ApplyDefaultBounds(RootGrid);
        await _placement.RestoreAndCenterAsync(RootGrid, CancellationToken.None);
        CitySearchBox.Focus(FocusState.Programmatic);
    }

    private void CitySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
        {
            return;
        }

        var query = sender.Text.Trim();
        CityList.ItemsSource = string.IsNullOrEmpty(query)
            ? _options
            : _options.Where(option => option.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        CityList.SelectedItem = null;
    }

    private void CityList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        AddButton.IsEnabled = CityList.SelectedItem is WorldClockCityPickerOption;

    private async void CityList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WorldClockCityPickerOption option)
        {
            await CompleteAsync(option.Id);
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (CityList.SelectedItem is WorldClockCityPickerOption option)
        {
            await CompleteAsync(option.Id);
        }
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e) => await CompleteAsync(null);

    private async void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            await CompleteAsync(null);
        }
    }

    private async Task CompleteAsync(string? cityId)
    {
        if (_isCompleting)
        {
            return;
        }

        _isCompleting = true;
        AddButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        _result = cityId;
        await _placement.SaveAsync(CancellationToken.None);
        Close();
    }

    private sealed record WorldClockCityPickerOption(string Id, string DisplayName);
}
