// SPDX-License-Identifier: MIT

using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TrackMeUp.Application;
using TrackMeUp.Controls;
using TrackMeUp.Presentation;
using TrackMeUp.Services;

namespace TrackMeUp;

/// <summary>Hosts the independent world-clock comparison and time-zone conversion surface.</summary>
public sealed partial class WorldClockWindow : Window
{
    private const int LogicalWindowWidth = 1120;
    private const int LogicalWindowHeight = 720;
    private const int LogicalScreenMargin = 24;
    private readonly ITrackMeUpApplication _application;
    private readonly MicaDialogService _dialogs;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherQueueTimer _refreshTimer;
    private readonly SemaphoreSlim _currentRefreshGate = new(1, 1);
    private readonly Dictionary<string, WorldClockColumnControl> _columns = new(StringComparer.Ordinal);
    private LocalizationService _strings = new("system");
    private MenuFlyout? _worldClockMenuFlyout;
    private MenuFlyoutItem? _addWorldClockMenuItem;
    private ToggleMenuFlyoutItem? _alwaysOnTopMenuItem;
    private SvgImageSource? _weatherAttributionLogoSource;
    private WorldClockSnapshot? _snapshot;
    private XamlRoot? _xamlRoot;
    private string? _referenceCityId;
    private bool _loaded;
    private bool _updatingReferenceControls;
    private bool _isLive = true;
    private bool _pendingLiveConversion;
    private bool _customProjectionValid = true;
    private string? _lastConversionErrorKey;
    private bool _allowClose;
    private bool _closeInProgress;
    private bool _closed;
    private bool _wasMinimized;
    private int _requestVersion;

    private bool IsClosing => _closed || _lifetimeCancellation.IsCancellationRequested;

    /// <summary>Creates the independent world-clock window over the shared application facade.</summary>
    internal WorldClockWindow(
        ITrackMeUpApplication application,
        MicaDialogService dialogs,
        AppSettings settings)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        ArgumentNullException.ThrowIfNull(settings);
        InitializeComponent();

        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)));
        _placement = new WindowPlacementService(
            _application,
            this,
            _appWindow,
            WindowStateKeys.WorldClocks,
            LogicalWindowWidth,
            LogicalWindowHeight,
            LogicalScreenMargin);
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        _refreshTimer = DispatcherQueue.CreateTimer();
        _refreshTimer.IsRepeating = false;
        _refreshTimer.Interval = TimeSpan.FromMinutes(1);
        _refreshTimer.Tick += RefreshTimer_Tick;
        _appWindow.Changed += AppWindow_Changed;
        _appWindow.Closing += WorldClockWindow_Closing;
        RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;
        ApplySettings(settings);
        _placement.ApplyDefaultBounds(RootGrid);
        Closed += WorldClockWindow_Closed;
    }

    /// <summary>Applies the current application theme and language to the detached surface.</summary>
    internal void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _strings = new LocalizationService(settings.UiLanguage);
        RootGrid.RequestedTheme = settings.Theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        UiLocalization.Apply(RootGrid, _strings);
        Title = T("WorldClock.Landmark");
        ReferenceInstantLabel.Text = T("WorldClock.ReferenceInstant");
        NowButton.Content = T("WorldClock.Now");
        var moreOptionsName = T("WorldClock.MoreOptions");
        AutomationProperties.SetName(HeaderMenuButton, moreOptionsName);
        ToolTipService.SetToolTip(HeaderMenuButton, moreOptionsName);
        if (_addWorldClockMenuItem is not null)
        {
            SetMenuItemLabel(_addWorldClockMenuItem, "WorldClock.Add");
        }

        if (_alwaysOnTopMenuItem is not null)
        {
            SetMenuItemLabel(_alwaysOnTopMenuItem, "WorldClock.AlwaysOnTop");
        }
        AutomationProperties.SetName(ReferenceCityComboBox, T("WorldClock.ReferenceCity"));
        AutomationProperties.SetName(ReferenceDatePicker, T("WorldClock.ReferenceDate"));
        AutomationProperties.SetName(ReferenceTimePicker, T("WorldClock.ReferenceTime"));
        AutomationProperties.SetName(ReferenceInstantField, T("WorldClock.ReferenceInstant"));
        ToolTipService.SetToolTip(ReferenceInstantField, T("WorldClock.ReferenceInstant"));
        AutomationProperties.SetLabeledBy(ReferenceInstantField, ReferenceInstantLabel);
        AutomationProperties.SetName(NowButton, T("WorldClock.Now"));
        AutomationProperties.SetName(ClockColumnsHost, T("WorldClock.Landmark"));
        AutomationProperties.SetLocalizedLandmarkType(ClockColumnsHost, T("WorldClock.Landmark"));
        AutomationProperties.SetName(LoadingIndicator, T("WorldClock.Loading"));
        var weatherAttribution = T("WorldClock.WeatherAttribution");
        WeatherAttributionText.Text = weatherAttribution;
        AutomationProperties.SetName(WeatherAttributionButton, weatherAttribution);
        ToolTipService.SetToolTip(WeatherAttributionButton, weatherAttribution);
        ApplyThemeChrome(RootGrid.RequestedTheme == ElementTheme.Default ? RootGrid.ActualTheme : RootGrid.RequestedTheme);
        if (_snapshot is not null)
        {
            ApplySnapshot(_snapshot);
        }
    }

    private void HeaderMenuButton_Click(object sender, RoutedEventArgs e) =>
        EnsureWorldClockMenuFlyout().ShowAt(HeaderMenuButton);

    private MenuFlyout EnsureWorldClockMenuFlyout()
    {
        if (_worldClockMenuFlyout is not null)
        {
            return _worldClockMenuFlyout;
        }

        var addItem = new MenuFlyoutItem
        {
            Icon = new SymbolIcon(Symbol.Add),
            Tag = "WorldClock.Add"
        };
        SetMenuItemLabel(addItem, "WorldClock.Add");
        addItem.Click += AddWorldClockMenuItem_Click;

        var alwaysOnTopItem = new ToggleMenuFlyoutItem
        {
            Icon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = "\uE718"
            },
            Tag = "WorldClock.AlwaysOnTop"
        };
        SetMenuItemLabel(alwaysOnTopItem, "WorldClock.AlwaysOnTop");
        alwaysOnTopItem.Click += AlwaysOnTopMenuItem_Click;

        var presenterStyle = new Style(typeof(MenuFlyoutPresenter));
        presenterStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 224d));
        presenterStyle.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(12d)));
        var flyout = new MenuFlyout
        {
            MenuFlyoutPresenterStyle = presenterStyle,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            ShouldConstrainToRootBounds = false
        };
        flyout.Items.Add(addItem);
        flyout.Items.Add(alwaysOnTopItem);
        flyout.Opened += WorldClockMenuFlyout_Opened;

        _addWorldClockMenuItem = addItem;
        _alwaysOnTopMenuItem = alwaysOnTopItem;
        _worldClockMenuFlyout = flyout;
        return flyout;
    }

    private void WorldClockMenuFlyout_Opened(object? sender, object e)
    {
        if (_addWorldClockMenuItem is null || _alwaysOnTopMenuItem is null)
        {
            throw new InvalidOperationException("The world-clock menu must be initialized before it opens.");
        }

        _addWorldClockMenuItem.IsEnabled = _snapshot is { } snapshot
            && snapshot.Clocks.Count < snapshot.MaximumClocks;

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            _alwaysOnTopMenuItem.IsEnabled = true;
            _alwaysOnTopMenuItem.IsChecked = presenter.IsAlwaysOnTop;
            return;
        }

        _alwaysOnTopMenuItem.IsEnabled = false;
        _alwaysOnTopMenuItem.IsChecked = false;
    }

    private async void AddWorldClockMenuItem_Click(object sender, RoutedEventArgs e) =>
        await AddCityAsync();

    private void AlwaysOnTopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem menuItem)
        {
            throw new InvalidOperationException("Always-on-top must be invoked by its toggle menu item.");
        }

        if (_appWindow.Presenter is not OverlappedPresenter presenter)
        {
            menuItem.IsChecked = false;
            return;
        }

        presenter.IsAlwaysOnTop = menuItem.IsChecked;
    }

    private void SetMenuItemLabel(MenuFlyoutItem item, string key)
    {
        var label = T(key);
        item.Text = label;
        AutomationProperties.SetName(item, label);
        ToolTipService.SetToolTip(item, label);
    }

    private void SetMenuItemLabel(ToggleMenuFlyoutItem item, string key)
    {
        var label = T(key);
        item.Text = label;
        AutomationProperties.SetName(item, label);
        ToolTipService.SetToolTip(item, label);
    }

    /// <summary>Closes immediately while the composition root is already shutting down.</summary>
    internal void CloseForShutdown()
    {
        _allowClose = true;
        _lifetimeCancellation.Cancel();
        Close();
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        if (RootGrid.XamlRoot is { } xamlRoot)
        {
            _xamlRoot = xamlRoot;
            _xamlRoot.Changed += XamlRoot_Changed;
        }

        _placement.ApplyDefaultBounds(RootGrid);
        try
        {
            await _placement.RestoreAsync(RootGrid, _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The detached surface was closed while its optional state was loading.
            return;
        }
        catch (Exception)
        {
            if (IsClosing)
            {
                return;
            }

            ShowFailure("WorldClock.PlacementFailed");
        }

        await RefreshCurrentAsync();
    }

    private async void RefreshTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        await RefreshCurrentAsync();
    }

    private async Task<bool> RefreshCurrentAsync()
    {
        if (!_isLive)
        {
            return false;
        }

        try
        {
            await _currentRefreshGate.WaitAsync(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (IsClosing)
        {
            return false;
        }

        var snapshotApplied = false;
        try
        {
            if (!_isLive || IsClosing)
            {
                return false;
            }

            var version = Interlocked.Increment(ref _requestVersion);
            ShowLoading(_snapshot is null);
            var result = await _application.GetWorldClocksAsync(_lifetimeCancellation.Token);
            if (IsClosing || version != Volatile.Read(ref _requestVersion))
            {
                return false;
            }

            if (result.Succeeded && result.Value is not null)
            {
                ApplySnapshot(result.Value);
                snapshotApplied = true;
                return true;
            }

            ShowFailure(result.MessageKey);
            return false;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Closing the detached surface cancels optional presentation refreshes without a retry.
            return false;
        }
        catch (Exception)
        {
            if (!IsClosing)
            {
                ShowFailure("WorldClock.CatalogUnavailable");
            }

            return false;
        }
        finally
        {
            if (!IsClosing)
            {
                ShowLoading(false);
            }

            _currentRefreshGate.Release();
            if (!IsClosing && _isLive && !snapshotApplied)
            {
                // A failed live refresh keeps the last complete projection and retries once after one minute.
                ScheduleLiveRetry();
            }
        }
    }

    private async Task<bool> ConvertFromControlsAsync()
    {
        if (_updatingReferenceControls
            || _referenceCityId is null
            || ReferenceDatePicker.Date is not { } selectedDate)
        {
            return false;
        }

        if (_isLive)
        {
            _pendingLiveConversion = true;
        }

        var transitionStartedFromLive = _pendingLiveConversion;
        _isLive = false;
        _customProjectionValid = false;
        NowButton.IsEnabled = true;
        UpdateRefreshTimerState();
        var localTime = DateTime.SpecifyKind(
            selectedDate.Date + ReferenceTimePicker.Time,
            DateTimeKind.Unspecified);
        var version = Interlocked.Increment(ref _requestVersion);
        try
        {
            var result = await _application.ConvertWorldClocksAsync(
                new WorldClockConversionRequest(_referenceCityId, localTime),
                _lifetimeCancellation.Token);
            if (IsClosing || version != Volatile.Read(ref _requestVersion))
            {
                return false;
            }

            if (result.Succeeded && result.Value is not null)
            {
                _pendingLiveConversion = false;
                _customProjectionValid = true;
                _lastConversionErrorKey = null;
                ApplySnapshot(result.Value);
                return true;
            }

            HandleConversionFailure(transitionStartedFromLive, result.MessageKey);
            return false;
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Window shutdown cancels this conversion; the last complete projection remains visible.
            return false;
        }
        catch (Exception)
        {
            if (!IsClosing)
            {
                HandleConversionFailure(
                    transitionStartedFromLive,
                    "WorldClock.CatalogUnavailable");
            }

            return false;
        }
    }

    private void ApplySnapshot(WorldClockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Clocks.Count == 0)
        {
            throw new InvalidDataException("The world-clock projection must contain at least one city.");
        }

        UpdateWeatherStatus(snapshot.WeatherStatus);
        UpdateWeatherAttribution(snapshot);
        _snapshot = snapshot;
        if (_referenceCityId is null || snapshot.Clocks.All(clock => clock.CityId != _referenceCityId))
        {
            _referenceCityId = snapshot.Clocks[0].CityId;
        }

        var referenceClock = snapshot.Clocks.Single(clock => clock.CityId == _referenceCityId);
        UpdateReferenceControls(snapshot.Clocks, referenceClock);
        EnsureColumns(snapshot.Clocks);
        var canRemove = snapshot.Clocks.Count > 1;
        foreach (var clock in snapshot.Clocks)
        {
            _columns[clock.CityId].Apply(
                clock,
                referenceClock,
                clock.CityId == referenceClock.CityId,
                canRemove,
                _strings);
        }

        NowButton.Visibility = _isLive ? Visibility.Collapsed : Visibility.Visible;
        WorldClockNotificationBanner.Dismiss();
        UpdateRefreshTimerState();
    }

    private void UpdateWeatherStatus(WorldClockWeatherStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var messageKey = status.State switch
        {
            "available" => null,
            "not-requested" when status.ReasonCode == "explicit-instant" =>
                "WorldClock.WeatherStatus.ReferenceInstant",
            "disabled" => "WorldClock.WeatherStatus.Disabled",
            "partial" => "WorldClock.WeatherStatus.Partial",
            "unavailable" => "WorldClock.WeatherStatus.Unavailable",
            _ => throw new InvalidDataException($"Unsupported world-clock weather status '{status.State}'.")
        };
        if (messageKey is null)
        {
            WeatherStatusText.Text = string.Empty;
            AutomationProperties.SetName(WeatherStatusText, string.Empty);
            WeatherStatusText.Visibility = Visibility.Collapsed;
            return;
        }

        var message = T(messageKey);
        WeatherStatusText.Text = message;
        AutomationProperties.SetName(WeatherStatusText, message);
        WeatherStatusText.Visibility = Visibility.Visible;
    }

    private void UpdateWeatherAttribution(WorldClockSnapshot snapshot)
    {
        var displaysOpenWeatherObservation =
            string.Equals(snapshot.WeatherStatus.Provider, "openweather", StringComparison.Ordinal)
            && snapshot.Clocks.Any(static clock => clock.Weather is { IsFresh: true });
        if (displaysOpenWeatherObservation)
        {
            EnsureWeatherAttributionLogo();
        }

        WeatherAttributionButton.Visibility = displaysOpenWeatherObservation
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void EnsureWeatherAttributionLogo()
    {
        if (_weatherAttributionLogoSource is not null)
        {
            return;
        }

        var source = new SvgImageSource
        {
            RasterizePixelWidth = 168,
            RasterizePixelHeight = 64
        };
        source.Opened += WeatherAttributionLogo_Opened;
        source.OpenFailed += WeatherAttributionLogo_OpenFailed;
        _weatherAttributionLogoSource = source;
        WeatherAttributionLogo.Source = source;
        source.UriSource = new Uri("ms-appx:///Assets/WorldClocks/ThirdParty/OpenWeather/ow_logo.svg");
    }

    private void WeatherAttributionLogo_Opened(SvgImageSource sender, SvgImageSourceOpenedEventArgs args) =>
        WeatherAttributionLogoContainer.Visibility = Visibility.Visible;

    private void WeatherAttributionLogo_OpenFailed(SvgImageSource sender, SvgImageSourceFailedEventArgs args) =>
        WeatherAttributionLogoContainer.Visibility = Visibility.Collapsed;

    private async void WeatherAttributionButton_Click(object sender, RoutedEventArgs e)
    {
        WeatherAttributionButton.IsEnabled = false;
        try
        {
            var result = await _application.OpenProductLinkAsync(
                "openweather",
                _lifetimeCancellation.Token);
            if (!result.Succeeded && !IsClosing)
            {
                ShowFailure("About.LinkFailed");
            }
        }
        catch (OperationCanceledException) when (IsClosing)
        {
            // Closing the detached surface cancels an in-progress link launch.
        }
        catch (Exception)
        {
            if (!IsClosing)
            {
                ShowFailure("About.LinkFailed");
            }
        }
        finally
        {
            if (!IsClosing)
            {
                WeatherAttributionButton.IsEnabled = true;
            }
        }
    }

    private void HandleConversionFailure(bool transitionStartedFromLive, string messageKey)
    {
        var state = WorldClockWindowLayoutState.ResolveConversionFailure(transitionStartedFromLive);
        _pendingLiveConversion = false;
        _isLive = state.IsLive;
        _customProjectionValid = state.CustomProjectionValid;
        _lastConversionErrorKey = messageKey;
        if (state.RestoreLastSnapshotControls && _snapshot is not null)
        {
            ApplySnapshot(_snapshot);
        }
        else
        {
            UpdateRefreshTimerState();
        }

        // ApplySnapshot dismisses the previous banner, so publish the conversion failure last.
        ShowFailure(messageKey);
    }

    private void UpdateReferenceControls(IReadOnlyList<WorldClockItem> clocks, WorldClockItem referenceClock)
    {
        _updatingReferenceControls = true;
        try
        {
            var options = clocks.Select(clock => new ReferenceCityOption(clock.CityId, clock.CityName)).ToArray();
            ReferenceCityComboBox.ItemsSource = options;
            ReferenceCityComboBox.SelectedItem = options.Single(option => option.CityId == referenceClock.CityId);
            ReferenceDatePicker.Date = new DateTimeOffset(referenceClock.LocalTime.Date, referenceClock.LocalTime.Offset);
            ReferenceTimePicker.Time = referenceClock.LocalTime.TimeOfDay;
            var referenceInstantText = referenceClock.LocalTime
                .ToString("dd MMM yyyy · HH:mm", _strings.Culture)
                .ToUpper(_strings.Culture);
            AutomationProperties.SetName(
                ReferenceInstantField,
                $"{T("WorldClock.ReferenceInstant")}: {referenceInstantText}");
            ToolTipService.SetToolTip(ReferenceInstantField, referenceInstantText);
        }
        finally
        {
            _updatingReferenceControls = false;
        }
    }

    private void EnsureColumns(IReadOnlyList<WorldClockItem> clocks)
    {
        if (_columns.Count == clocks.Count
            && clocks.Select(clock => clock.CityId).SequenceEqual(_columns.Keys, StringComparer.Ordinal))
        {
            return;
        }

        foreach (var column in _columns.Values)
        {
            column.ReferenceRequested -= Column_ReferenceRequested;
            column.RemoveRequested -= Column_RemoveRequested;
        }

        _columns.Clear();
        ClockColumnsHost.Children.Clear();
        ClockColumnsHost.ColumnDefinitions.Clear();
        UpdateClockColumnsLayout(clocks.Count, ClockColumnsScroller.ActualWidth);

        for (var index = 0; index < clocks.Count; index++)
        {
            ClockColumnsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = new WorldClockColumnControl();
            column.ReferenceRequested += Column_ReferenceRequested;
            column.RemoveRequested += Column_RemoveRequested;
            _columns.Add(clocks[index].CityId, column);

            var host = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(42, 127, 127, 127)),
                BorderThickness = index < clocks.Count - 1 ? new Thickness(0, 0, 1, 0) : new Thickness(0),
                Child = column
            };
            Grid.SetColumn(host, index);
            ClockColumnsHost.Children.Add(host);
        }
    }

    private void ClockColumnsScroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_snapshot is not null)
        {
            UpdateClockColumnsLayout(_snapshot.Clocks.Count, e.NewSize.Width);
        }
    }

    private void UpdateClockColumnsLayout(int clockCount, double viewportWidth)
    {
        var layout = WorldClockWindowLayoutState.CalculateColumnsLayout(clockCount, Math.Max(0d, viewportWidth));
        ClockColumnsHost.MinWidth = layout.MinimumWidth;
        ClockColumnsHost.Width = layout.Width;
        ClockColumnsHost.MaxWidth = layout.Width;
        ClockColumnsHost.HorizontalAlignment = layout.IsCentered
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
    }

    private void ReferenceCityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingReferenceControls || ReferenceCityComboBox.SelectedItem is not ReferenceCityOption option)
        {
            return;
        }

        SetReferenceCity(option.CityId);
    }

    private void Column_ReferenceRequested(object? sender, WorldClockCityEventArgs e) =>
        SetReferenceCity(e.CityId);

    private void SetReferenceCity(string cityId)
    {
        if (_snapshot is null || _referenceCityId == cityId)
        {
            return;
        }

        _referenceCityId = cityId;
        Interlocked.Increment(ref _requestVersion);
        _customProjectionValid = true;
        _lastConversionErrorKey = null;
        ApplySnapshot(_snapshot);
    }

    private async void ReferenceDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_updatingReferenceControls)
        {
            return;
        }

        if (args.NewDate is null)
        {
            if (_snapshot is not null && _referenceCityId is not null)
            {
                var referenceClock = _snapshot.Clocks.Single(clock => clock.CityId == _referenceCityId);
                _updatingReferenceControls = true;
                try
                {
                    ReferenceDatePicker.Date = new DateTimeOffset(referenceClock.LocalTime.Date, referenceClock.LocalTime.Offset);
                }
                finally
                {
                    _updatingReferenceControls = false;
                }
            }

            ShowFailure("WorldClock.ReferenceDateRequired");
            return;
        }

        await ConvertFromControlsAsync();
    }

    private async void ReferenceTimePicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e) =>
        await ConvertFromControlsAsync();

    private async void NowButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingLiveConversion = false;
        _isLive = true;
        _customProjectionValid = true;
        _lastConversionErrorKey = null;
        UpdateRefreshTimerState();
        await RefreshCurrentAsync();
    }

    private async Task AddCityAsync()
    {
        try
        {
            if (!_isLive && !_customProjectionValid)
            {
                ShowFailure(_lastConversionErrorKey ?? "WorldClock.CatalogUnavailable");
                return;
            }

            var catalogResult = await _application.GetWorldClockCityCatalogAsync(_lifetimeCancellation.Token);
            if (IsClosing)
            {
                return;
            }

            if (!catalogResult.Succeeded || catalogResult.Value is null)
            {
                ShowFailure("WorldClock.CatalogUnavailable");
                return;
            }

            var selectedIds = _snapshot?.Clocks.Select(clock => clock.CityId).ToHashSet(StringComparer.Ordinal) ?? [];
            var options = catalogResult.Value.Cities.Where(city => !selectedIds.Contains(city.Id)).ToArray();
            if (options.Length == 0)
            {
                return;
            }

            var selectedCityId = await _dialogs.ShowWorldClockCityPickerAsync(
                _application,
                this,
                options,
                RootGrid.RequestedTheme,
                _strings);
            if (IsClosing || selectedCityId is null)
            {
                return;
            }

            Interlocked.Increment(ref _requestVersion);
            var result = await _application.AddWorldClockAsync(selectedCityId, _lifetimeCancellation.Token);
            if (IsClosing)
            {
                return;
            }

            Interlocked.Increment(ref _requestVersion);
            if (!result.Succeeded || result.Value is null)
            {
                ShowFailure(result.MessageKey);
                return;
            }

            _ = _isLive
                ? await RefreshCurrentAsync()
                : await ConvertFromControlsAsync();
        }
        catch (OperationCanceledException) when (IsClosing)
        {
            // Closing the detached surface cancels the optional picker or mutation.
        }
        catch (Exception)
        {
            if (!IsClosing)
            {
                ShowFailure("WorldClock.CatalogUnavailable");
            }
        }
    }

    private async void Column_RemoveRequested(object? sender, WorldClockCityEventArgs e)
    {
        try
        {
            if (!_isLive && !_customProjectionValid)
            {
                ShowFailure(_lastConversionErrorKey ?? "WorldClock.CatalogUnavailable");
                return;
            }

            var previousSnapshot = _snapshot;
            Interlocked.Increment(ref _requestVersion);
            var result = await _application.RemoveWorldClockAsync(e.CityId, _lifetimeCancellation.Token);
            if (IsClosing)
            {
                return;
            }

            Interlocked.Increment(ref _requestVersion);
            if (!result.Succeeded || result.Value is null)
            {
                ShowFailure(result.MessageKey);
                return;
            }

            if (_referenceCityId == e.CityId)
            {
                _referenceCityId = result.Value.CityIds[0];
                if (!_isLive && previousSnapshot is not null)
                {
                    var replacement = previousSnapshot.Clocks.Single(clock => clock.CityId == _referenceCityId);
                    _updatingReferenceControls = true;
                    try
                    {
                        ReferenceDatePicker.Date = new DateTimeOffset(replacement.LocalTime.Date, replacement.LocalTime.Offset);
                        ReferenceTimePicker.Time = replacement.LocalTime.TimeOfDay;
                    }
                    finally
                    {
                        _updatingReferenceControls = false;
                    }
                }
            }

            var projectionApplied = _isLive
                ? await RefreshCurrentAsync()
                : await ConvertFromControlsAsync();

            if (projectionApplied && !IsClosing)
            {
                _dialogs.ShowInfoBanner(
                    WorldClockNotificationBanner,
                    T("WorldClock.RemovedTitle"),
                    _strings.Format("WorldClock.RemovedMessage", e.CityName));
            }
        }
        catch (OperationCanceledException) when (IsClosing)
        {
            // Closing the detached surface cancels the optional mutation.
        }
        catch (Exception)
        {
            if (!IsClosing)
            {
                ShowFailure("WorldClock.CatalogUnavailable");
            }
        }
    }

    private void ShowFailure(string messageKey)
    {
        if (!IsClosing)
        {
            _dialogs.ShowWarningBanner(
                WorldClockNotificationBanner,
                T("WorldClock.ErrorTitle"),
                T(messageKey));
        }
    }

    private void ShowLoading(bool show)
    {
        LoadingIndicator.IsActive = show;
        LoadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ClockColumnsHost.Opacity = show ? 0.45d : 1d;
    }

    private void UpdateRefreshTimerState()
    {
        _refreshTimer.Stop();
        var minimized = _appWindow.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized;
        if (_isLive
            && !minimized
            && !_lifetimeCancellation.IsCancellationRequested
            && _snapshot is { } snapshot)
        {
            _refreshTimer.Interval = WorldClockWindowLayoutState.DelayUntilNextMinute(snapshot.InstantUtc);
            _refreshTimer.Start();
        }
    }

    private void ScheduleLiveRetry()
    {
        _refreshTimer.Stop();
        var minimized = _appWindow.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized;
        if (_isLive && !minimized && !_lifetimeCancellation.IsCancellationRequested)
        {
            _refreshTimer.Interval = TimeSpan.FromMinutes(1);
            _refreshTimer.Start();
        }
    }

    private async void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        var minimized = sender.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized;
        var restored = _wasMinimized && !minimized;
        _wasMinimized = minimized;
        UpdateRefreshTimerState();
        if (restored && _isLive && !IsClosing)
        {
            await RefreshCurrentAsync();
        }
    }

    private void ApplyThemeChrome(ElementTheme effectiveTheme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var dark = effectiveTheme == ElementTheme.Dark;
        var titleBar = _appWindow.TitleBar;
        titleBar.BackgroundColor = Colors.Transparent;
        titleBar.InactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = dark ? Colors.White : Colors.Black;
        titleBar.ButtonInactiveForegroundColor = dark
            ? Windows.UI.Color.FromArgb(160, 255, 255, 255)
            : Windows.UI.Color.FromArgb(160, 0, 0, 0);
        titleBar.ButtonHoverBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(32, 255, 255, 255)
            : Windows.UI.Color.FromArgb(24, 0, 0, 0);
        titleBar.ButtonPressedBackgroundColor = dark
            ? Windows.UI.Color.FromArgb(48, 255, 255, 255)
            : Windows.UI.Color.FromArgb(40, 0, 0, 0);
    }

    private void RootGrid_ActualThemeChanged(FrameworkElement sender, object args)
    {
        if (RootGrid.RequestedTheme == ElementTheme.Default)
        {
            ApplyThemeChrome(sender.ActualTheme);
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (Math.Abs(sender.RasterizationScale - _placement.RasterizationScale) >= 0.001d)
        {
            _placement.KeepCurrentBoundsInWorkArea(RootGrid);
        }

    }

    private async void WorldClockWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        if (_closeInProgress)
        {
            return;
        }

        _closeInProgress = true;
        var cancellationToken = _lifetimeCancellation.Token;
        try
        {
            await _placement.SaveAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _allowClose = true;
            Close();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application shutdown owns the final close and skips optional placement persistence.
        }
        catch (Exception exception)
        {
            _closeInProgress = false;
            _allowClose = true;
            _dialogs.ShowErrorBanner(
                WorldClockNotificationBanner,
                T("WorldClock.ErrorTitle"),
                $"{T("WorldClock.PlacementFailed")} ({exception.GetType().Name})");
        }
    }

    private void WorldClockWindow_Closed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _appWindow.Changed -= AppWindow_Changed;
        _appWindow.Closing -= WorldClockWindow_Closing;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        if (_weatherAttributionLogoSource is not null)
        {
            _weatherAttributionLogoSource.Opened -= WeatherAttributionLogo_Opened;
            _weatherAttributionLogoSource.OpenFailed -= WeatherAttributionLogo_OpenFailed;
            _weatherAttributionLogoSource = null;
        }

        foreach (var column in _columns.Values)
        {
            column.ReferenceRequested -= Column_ReferenceRequested;
            column.RemoveRequested -= Column_RemoveRequested;
        }

        _columns.Clear();
        if (_xamlRoot is not null)
        {
            _xamlRoot.Changed -= XamlRoot_Changed;
        }

        RootGrid.ActualThemeChanged -= RootGrid_ActualThemeChanged;
        _lifetimeCancellation.Cancel();
        _placement.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private string T(string key) => _strings.Translate(key);

    private sealed record ReferenceCityOption(string CityId, string CityName);
}
