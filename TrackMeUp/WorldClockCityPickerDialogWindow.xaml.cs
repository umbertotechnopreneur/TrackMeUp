// SPDX-License-Identifier: MIT

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
    private readonly CustomTitleBarController _titleBar;
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
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
        _titleBar = new CustomTitleBarController(
            this,
            _appWindow,
            RootGrid,
            TitleDragRegion,
            TitleBarLeftInsetColumn,
            TitleBarRightInsetColumn,
            static () => Array.Empty<FrameworkElement>(),
            useTallTitleBar: false);
        _placement = new WindowPlacementService(
            application,
            this,
            _appWindow,
            WindowStateKeys.WorldClockCityPicker,
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
        CityComboBox.PlaceholderText = strings.Translate("WorldClock.SearchCity");
        CancelButton.Content = strings.Translate("Dialog.Cancel");
        AddButton.Content = strings.Translate("WorldClock.Add");
        AutomationProperties.SetName(RootGrid, Title);
        AutomationProperties.SetName(DialogTitleText, Title);
        AutomationProperties.SetName(CityComboBox, strings.Translate("WorldClock.SearchCity"));
        AutomationProperties.SetName(CancelButton, strings.Translate("Dialog.Cancel"));
        AutomationProperties.SetName(AddButton, strings.Translate("WorldClock.Add"));
        CityComboBox.ItemsSource = _options;
        _appWindow.Closing += AppWindow_Closing;
        Closed += WorldClockCityPickerDialogWindow_Closed;
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
        _placement.ApplyDefaultSize(RootGrid);
        await _placement.RestoreAsync(RootGrid, CancellationToken.None);
        CityComboBox.Focus(FocusState.Programmatic);
    }

    /// <summary>Enables confirmation only for a city supplied by the packaged catalog.</summary>
    private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        AddButton.IsEnabled = CityComboBox.SelectedItem is WorldClockCityPickerOption;

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (CityComboBox.SelectedItem is WorldClockCityPickerOption option)
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

    /// <summary>Routes the native close command through the same placement-save path as dialog buttons.</summary>
    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isCompleting)
        {
            return;
        }

        args.Cancel = true;
        await CompleteAsync(null);
    }

    private void WorldClockCityPickerDialogWindow_Closed(object sender, WindowEventArgs args)
    {
        _appWindow.Closing -= AppWindow_Closing;
        Closed -= WorldClockCityPickerDialogWindow_Closed;
        _titleBar.Dispose();
        _completion.TrySetResult(_result);
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
        _ = await _placement.TrySaveForCloseAsync(CancellationToken.None);
        Close();
    }

    private sealed record WorldClockCityPickerOption(string Id, string DisplayName);
}
