// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects world-clock presentation options and forwards mutations to the shared application facade.</summary>
public sealed partial class WorldClockOptionsControl : UserControl
{
    private ITrackMeUpApplication? _application;
    private LocalizationService _strings = new("system");
    private CancellationToken _lifetimeToken;
    private bool _updatingControls;
    private bool _busy;
    private bool _canAddClock;
    private bool _weatherKeyRefreshPending;
    private string? _weatherActionStatusKey;

    /// <summary>Creates the passive world-clock options surface.</summary>
    public WorldClockOptionsControl() => InitializeComponent();

    /// <summary>Occurs when the selected clocks must be refreshed.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>Occurs when the city picker should be shown.</summary>
    public event EventHandler? AddRequested;

    /// <summary>Occurs when a city should become the reference city.</summary>
    public event EventHandler<WorldClockCityEventArgs>? ReferenceRequested;

    /// <summary>Occurs when a city should be removed.</summary>
    public event EventHandler<WorldClockCityEventArgs>? RemoveRequested;

    /// <summary>Occurs when the native always-on-top presenter state should change.</summary>
    public event Action<bool>? AlwaysOnTopChanged;

    /// <summary>Occurs after the application returns a fully persisted settings snapshot.</summary>
    public event Action<AppSettings>? SettingsSaved;

    /// <summary>Occurs when a non-field-specific failure should use the window's transient banner.</summary>
    public event Action<string>? WarningRequested;

    /// <summary>Occurs when the weather-provider setup link should be opened by the host.</summary>
    public event EventHandler? ProviderLinkRequested;

    /// <summary>Attaches the shared application facade and applies the current localized presentation state.</summary>
    public void Initialize(
        ITrackMeUpApplication application,
        AppSettings settings,
        WorldClockSnapshot? snapshot,
        string? referenceCityId,
        bool alwaysOnTop,
        LocalizationService strings,
        CancellationToken lifetimeToken)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        ArgumentNullException.ThrowIfNull(settings);
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        _lifetimeToken = lifetimeToken;
        UiLocalization.Apply(this, _strings);
        ApplyLocalizedPresentation();
        ApplyState(settings, snapshot, referenceCityId, alwaysOnTop);
    }

    /// <summary>Refreshes controls from the latest settings, projection, and native presenter state.</summary>
    public void ApplyState(
        AppSettings settings,
        WorldClockSnapshot? snapshot,
        string? referenceCityId,
        bool alwaysOnTop)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _updatingControls = true;
        try
        {
            WeatherEnabledSwitch.IsOn = settings.WorldClockWeatherEnabled;
            AlwaysOnTopSwitch.IsOn = alwaysOnTop;
        }
        finally
        {
            _updatingControls = false;
        }

        ApplyWeatherStatus(snapshot?.WeatherStatus);
        RenderCities(snapshot, referenceCityId);
    }

    /// <summary>Reapplies localized text without changing option values.</summary>
    public void ApplyLanguage(LocalizationService strings)
    {
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        UiLocalization.Apply(this, _strings);
        ApplyLocalizedPresentation();
        if (_weatherActionStatusKey is not null)
        {
            ShowWeatherActionStatus(_weatherActionStatusKey);
        }
    }

    /// <summary>Completes the refresh requested after a weather key was stored.</summary>
    public void CompleteWeatherKeyRefresh(bool succeeded)
    {
        if (!_weatherKeyRefreshPending)
        {
            return;
        }

        _weatherKeyRefreshPending = false;
        if (!succeeded)
        {
            ShowWeatherActionStatus("WorldClock.Options.Weather.KeyRefreshFailed");
        }
    }

    private async void WeatherEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingControls || _application is null || _busy)
        {
            return;
        }

        var requested = WeatherEnabledSwitch.IsOn;
        ClearWeatherActionStatus();
        SetBusy(true);
        try
        {
            var result = await _application.PatchSettingsAsync(
                new SettingsPatch(new Dictionary<string, string?>
                {
                    ["world_clocks.weather.enabled"] = requested ? "true" : "false"
                }),
                _lifetimeToken);
            if (result.Succeeded && result.Value is not null)
            {
                SettingsSaved?.Invoke(result.Value);
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            RestoreWeatherToggle(!requested);
            WarningRequested?.Invoke("Options.SaveError");
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            // Closing the detached surface cancels the optional settings mutation.
        }
        catch (Exception)
        {
            RestoreWeatherToggle(!requested);
            WarningRequested?.Invoke("Options.SaveError");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SaveWeatherKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null || _busy)
        {
            return;
        }

        var secret = WeatherApiKeyBox.Password;
        ClearWeatherActionStatus();
        if (string.IsNullOrWhiteSpace(secret))
        {
            ShowWeatherActionStatus("WorldClock.Options.Weather.KeyInvalid");
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _application.SetWorldClockWeatherKeyAsync(secret, _lifetimeToken);
            if (result.Succeeded
                && string.Equals(result.Code, "world_clocks.weather.key.stored", StringComparison.Ordinal))
            {
                ShowWeatherActionStatus("WorldClock.Options.Weather.KeySaved");
                SetSaveWeatherKeyAction("WorldClock.Options.Weather.KeyAction.Change");
                _weatherKeyRefreshPending = true;
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (string.Equals(result.Code, "world_clocks.weather.key.invalid", StringComparison.Ordinal))
            {
                ShowWeatherActionStatus("WorldClock.Options.Weather.KeyInvalid");
            }
            else
            {
                WarningRequested?.Invoke("WorldClock.Options.Weather.KeySaveFailed");
            }
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            // Closing the detached surface cancels the optional credential mutation.
        }
        catch (Exception)
        {
            WarningRequested?.Invoke("WorldClock.Options.Weather.KeySaveFailed");
        }
        finally
        {
            // The secret exists only long enough to cross the application facade.
            WeatherApiKeyBox.Password = string.Empty;
            secret = string.Empty;
            SetBusy(false);
        }
    }

    private void AlwaysOnTopSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_updatingControls)
        {
            AlwaysOnTopChanged?.Invoke(AlwaysOnTopSwitch.IsOn);
        }
    }

    private void AddClockButton_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke(this, EventArgs.Empty);

    private void WeatherProviderLinkButton_Click(object sender, RoutedEventArgs e) =>
        ProviderLinkRequested?.Invoke(this, EventArgs.Empty);

    private void ReferenceRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_updatingControls || sender is not RadioButton { Tag: WorldClockItem clock })
        {
            return;
        }

        ReferenceRequested?.Invoke(this, new WorldClockCityEventArgs(clock.CityId, clock.CityName));
    }

    private void RemoveClockButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WorldClockItem clock })
        {
            RemoveRequested?.Invoke(this, new WorldClockCityEventArgs(clock.CityId, clock.CityName));
        }
    }

    private void RenderCities(WorldClockSnapshot? snapshot, string? referenceCityId)
    {
        CitiesHost.Children.Clear();
        if (snapshot is null)
        {
            _canAddClock = false;
            AddClockButton.IsEnabled = false;
            return;
        }

        _updatingControls = true;
        try
        {
            foreach (var clock in snapshot.Clocks)
            {
                if (CitiesHost.Children.Count > 0)
                {
                    CitiesHost.Children.Add(new Border
                    {
                        Style = (Style)Resources["WorldClockCitySeparatorStyle"]
                    });
                }

                var row = new Grid
                {
                    MinHeight = 52,
                    ColumnSpacing = 8,
                    Padding = new Thickness(0, 4, 0, 4)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var cityContent = new StackPanel { Spacing = 1 };
                cityContent.Children.Add(new TextBlock
                {
                    Text = clock.CityName,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                cityContent.Children.Add(new TextBlock
                {
                    Text = clock.LocalTime.ToString("HH:mm", _strings.Culture),
                    Style = (Style)Resources["WorldClockCityTimeTextStyle"]
                });

                var referenceButton = new RadioButton
                {
                    Content = cityContent,
                    GroupName = "WorldClockReferenceCity",
                    MinHeight = 44,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    IsChecked = string.Equals(clock.CityId, referenceCityId, StringComparison.Ordinal),
                    Tag = clock
                };
                var referenceName = _strings.Format("WorldClock.SetReference", clock.CityName);
                AutomationProperties.SetName(referenceButton, referenceName);
                ToolTipService.SetToolTip(referenceButton, referenceName);
                referenceButton.Checked += ReferenceRadioButton_Checked;
                row.Children.Add(referenceButton);

                var removeButton = new Button
                {
                    Width = 40,
                    Height = 40,
                    Padding = new Thickness(0),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Content = new SymbolIcon(Symbol.Delete),
                    Tag = clock,
                    IsEnabled = snapshot.Clocks.Count > 1
                };
                Grid.SetColumn(removeButton, 1);
                var removeName = _strings.Format("WorldClock.Remove", clock.CityName);
                AutomationProperties.SetName(removeButton, removeName);
                ToolTipService.SetToolTip(removeButton, removeName);
                removeButton.Click += RemoveClockButton_Click;
                row.Children.Add(removeButton);
                CitiesHost.Children.Add(row);
            }
        }
        finally
        {
            _updatingControls = false;
        }

        _canAddClock = snapshot.Clocks.Count < snapshot.MaximumClocks;
        AddClockButton.IsEnabled = !_busy && _canAddClock;
    }

    private void ApplyWeatherStatus(WorldClockWeatherStatus? status)
    {
        (string Key, string VisualState) presentation = status is null
            ? ("WorldClock.Options.Weather.ApiKeyStatus.Unavailable", "WeatherStatusInformational")
            : (status.State, status.ReasonCode) switch
        {
            ("available", _) => ("WorldClock.Options.Weather.ApiKeyStatus.Ready", "WeatherStatusReady"),
            ("configuration-required", "missing-api-key") =>
                ("WorldClock.Options.Weather.ApiKeyStatus.Missing", "WeatherStatusNeedsAttention"),
            ("configuration-required", "invalid-api-key") =>
                ("WorldClock.Options.Weather.ApiKeyStatus.Invalid", "WeatherStatusInvalid"),
            ("disabled", "user-disabled") => ("WorldClock.WeatherStatus.Disabled", "WeatherStatusInformational"),
            ("partial", _) => ("WorldClock.WeatherStatus.Partial", "WeatherStatusNeedsAttention"),
            ("unavailable", _) => ("WorldClock.WeatherStatus.Unavailable", "WeatherStatusInformational"),
            ("not-requested", "explicit-instant") =>
                ("WorldClock.WeatherStatus.ReferenceInstant", "WeatherStatusInformational"),
            _ => throw new InvalidDataException(
                $"Unsupported world-clock weather status '{status.State}/{status.ReasonCode}'.")
        };
        WeatherStatusText.Text = T(presentation.Key);
        AutomationProperties.SetName(WeatherStatusText, WeatherStatusText.Text);
        VisualStateManager.GoToState(this, presentation.VisualState, false);
        SetSaveWeatherKeyAction((status is null
            || status is { State: "configuration-required", ReasonCode: "missing-api-key" })
            ? "WorldClock.Options.Weather.KeyAction.Set"
            : "WorldClock.Options.Weather.KeyAction.Change");
    }

    private void RestoreWeatherToggle(bool value)
    {
        _updatingControls = true;
        WeatherEnabledSwitch.IsOn = value;
        _updatingControls = false;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BusyIndicator.IsActive = busy;
        BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        WeatherEnabledSwitch.IsEnabled = !busy;
        WeatherApiKeyBox.IsEnabled = !busy;
        WeatherProviderLinkButton.IsEnabled = !busy;
        SaveWeatherKeyButton.IsEnabled = !busy;
        AddClockButton.IsEnabled = !busy && _canAddClock;
        AlwaysOnTopSwitch.IsEnabled = !busy;
    }

    private void ShowWeatherActionStatus(string key)
    {
        _weatherActionStatusKey = key;
        WeatherActionStatusText.Text = T(key);
        WeatherActionStatusText.Visibility = Visibility.Visible;
        AutomationProperties.SetName(WeatherActionStatusText, WeatherActionStatusText.Text);
    }

    private void ClearWeatherActionStatus()
    {
        _weatherActionStatusKey = null;
        WeatherActionStatusText.Text = string.Empty;
        WeatherActionStatusText.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(WeatherActionStatusText, string.Empty);
    }

    private void ApplyLocalizedPresentation()
    {
        WeatherApiKeyBox.PlaceholderText = string.Empty;
        AutomationProperties.SetHelpText(WeatherApiKeyBox, WeatherApiKeyStorageNote.Text);
        AutomationProperties.SetName(WeatherProviderLinkButton, WeatherProviderLinkText.Text);
    }

    private void SetSaveWeatherKeyAction(string key)
    {
        var label = T(key);
        SaveWeatherKeyButton.Content = label;
        AutomationProperties.SetName(SaveWeatherKeyButton, label);
    }

    private string T(string key) => _strings.Translate(key);
}
