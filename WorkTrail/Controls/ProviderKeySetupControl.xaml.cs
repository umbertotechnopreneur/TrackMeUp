// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WorkTrail.Application;
using WorkTrail.Services;

namespace WorkTrail.Controls;

/// <summary>Collects a provider credential and renders verification results from the shared application facade.</summary>
public sealed partial class ProviderKeySetupControl : UserControl
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private CancellationTokenRegistration _ownerCancellation;
    private IWorkTrailApplication _application = null!;
    private LocalizationService _strings = null!;
    private AppSettings? _settings;
    private string _provider = "openai";
    private bool _weather;
    private bool _busy;
    private bool _verified;
    private bool _closed;
    private bool _canTestExisting;
    private bool _finished;

    /// <summary>Creates the passive credential entry sheet.</summary>
    public ProviderKeySetupControl()
    {
        _token = _lifetime.Token;
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            if (_closed) return;
            _closed = true;
            _lifetime.Cancel();
            KeyBox.Password = string.Empty;
            _ownerCancellation.Dispose();
            _lifetime.Dispose();
        };
    }

    /// <summary>Occurs when the sheet is dismissed, with whether its verification succeeded.</summary>
    public event Action<bool>? Dismissed;

    /// <summary>Occurs when the user chooses to return to the non-AI profile choices.</summary>
    public event Action? WithoutAiRequested;

    /// <summary>Loads redacted provider configuration without reading any secret into the view.</summary>
    public async Task InitializeAsync(IWorkTrailApplication application, LocalizationService strings, bool weather, CancellationToken ownerToken)
    {
        _application = application;
        _strings = strings;
        _weather = weather;
        _ownerCancellation = ownerToken.Register(() => _lifetime.Cancel());
        UiLocalization.Apply(this, strings);
        _provider = weather ? "openweather" : "openai";
        ApplyCopy();
        SetBusy(true);
        try
        {
            if (!weather)
            {
                var settings = await application.GetSettingsAsync(_token);
                if (_closed || _token.IsCancellationRequested) return;
                if (!settings.Succeeded || settings.Value is null)
                {
                    ShowFailure("ProviderSetup.Error.Configuration");
                    return;
                }

                _settings = settings.Value;
                _provider = _settings.AiProvider.ToLowerInvariant();
                if (_provider is not ("openai" or "openrouter" or "anthropic"))
                {
                    _settings = null;
                    ShowFailure("ProviderSetup.Error.Configuration");
                    return;
                }

                var status = await application.GetAiStatusAsync(_token);
                if (_closed || _token.IsCancellationRequested) return;
                _canTestExisting = status.Succeeded && status.Value?.CanEnable == true;
                ExistingKeyText.Visibility = _canTestExisting ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!_closed)
            {
                ApplyCopy();
                KeyBox.Focus(FocusState.Programmatic);
            }
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception) { if (!_closed) ShowFailure("ProviderSetup.Error.Unavailable"); }
        finally { if (!_closed) SetBusy(false); }
    }

    private void ApplyCopy()
    {
        var name = _provider switch { "openweather" => "OpenWeather", "openrouter" => "OpenRouter", "anthropic" => "Anthropic", _ => "OpenAI" };
        HeadingText.Text = string.Format(_strings.Culture, T("ProviderSetup.Heading"), name);
        DescriptionText.Text = T(_weather ? "ProviderSetup.Weather.Description" : "ProviderSetup.Ai.Description");
        ExplanationText.Text = string.Format(_strings.Culture, T("ProviderSetup.Explanation"), name);
        KeyBox.Header = string.Format(_strings.Culture, T("ProviderSetup.KeyLabel"), name);
        AutomationProperties.SetName(KeyBox, KeyBox.Header.ToString());
        ExampleText.Text = string.Format(_strings.Culture, T("ProviderSetup.Example"), _provider switch
        {
            "openweather" => "a1b2********************9e8f",
            "openrouter" => "sk-or-v1-ab12************xy89",
            "anthropic" => "sk-ant-ab12************xy89",
            _ => "sk-proj-ab12************xy89"
        });
        InstructionsText.Text = T(_weather ? "ProviderSetup.Weather.Instructions" : "ProviderSetup.Ai.Instructions");
        SetLinkLabel(PortalButton, "ProviderSetup.Portal");
        SetLinkLabel(GuideButton, "ProviderSetup.Guide");
        SetLinkLabel(PricingButton, "ProviderSetup.Pricing");
        SetLinkLabel(WithoutAiButton, "ProviderSetup.WithoutAi");
        ActivationText.Visibility = _weather ? Visibility.Visible : Visibility.Collapsed;
        WithoutAiButton.Visibility = _weather ? Visibility.Collapsed : Visibility.Visible;
        CostBar.Title = T("ProviderSetup.CostTitle");
        CostBar.Message = T(_weather ? "ProviderSetup.Weather.Cost" : "ProviderSetup.Ai.Cost");
        PrivacyText.Text = T(_weather ? "ProviderSetup.Weather.Privacy" : "ProviderSetup.Ai.Privacy");
        ResultBar.Message = T("ProviderSetup.NotVerified");
        UpdateDismissLabel();
    }

    private async void VerifyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _closed) return;
        _verified = false;
        SetBusy(true);
        ResultBar.Severity = InfoBarSeverity.Informational;
        ResultBar.Message = T("ProviderSetup.Verifying");
        var secret = KeyBox.Password;
        try
        {
            bool succeeded;
            string message;
            if (_weather)
            {
                var result = await _application.SetWorldClockWeatherKeyAsync(secret, _token);
                succeeded = result.Succeeded;
                message = result.Code switch
                {
                    "world_clocks.weather.key.invalid" => "ProviderSetup.Error.Key",
                    "world_clocks.weather.key.rejected" => "ProviderSetup.Weather.Rejected",
                    "world_clocks.weather.key.validation_unavailable" => "ProviderSetup.Error.Unavailable",
                    _ => result.MessageKey
                };
            }
            else if (secret.Length == 0 && _canTestExisting)
            {
                var result = await _application.TestAiConnectionAsync(_token);
                succeeded = result.Succeeded;
                message = result.MessageKey;
            }
            else
            {
                var result = await _application.SetAiKeyAsync(_settings!.AiApiKeyName, secret, _token);
                succeeded = result.Succeeded;
                message = result.Code == "ai.key.invalid" ? "ProviderSetup.Error.Key" : result.MessageKey;
            }

            if (_closed) return;
            KeyBox.Password = string.Empty;
            ShowKeyButton.IsChecked = false;
            KeyBox.PasswordRevealMode = PasswordRevealMode.Hidden;
            _verified = succeeded;
            _finished = true;
            ResultBar.Severity = succeeded ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Message = succeeded ? T("ProviderSetup.Success") : T(message) + " " + T(_weather ? "ProviderSetup.Retry" : "ProviderSetup.Ai.Retry");
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception) { if (!_closed) ShowFailure("ProviderSetup.Error.Unavailable"); }
        finally
        {
            secret = string.Empty;
            if (!_closed) { KeyBox.Password = string.Empty; SetBusy(false); }
        }
    }

    private void ShowFailure(string key)
    {
        _verified = false;
        _finished = true;
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Message = T(key) + " " + T(_weather ? "ProviderSetup.Retry" : "ProviderSetup.Ai.Retry");
        UpdateDismissLabel();
    }

    private void KeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_strings is null || _busy || _closed) return;
        _verified = false;
        ResultBar.Severity = InfoBarSeverity.Informational;
        ResultBar.Message = T("ProviderSetup.NotVerified");
        UpdateDismissLabel();
    }

    private void ShowKeyButton_Click(object sender, RoutedEventArgs e) =>
        KeyBox.PasswordRevealMode = ShowKeyButton.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;

    private void SetBusy(bool busy)
    {
        _busy = busy;
        KeyBox.IsEnabled = !busy && !_finished;
        ShowKeyButton.IsEnabled = !busy && !_finished;
        VerifyButton.IsEnabled = !busy && !_finished && (_weather || _settings is not null);
        Progress.IsActive = busy;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        WithoutAiButton.IsEnabled = !busy;
        UpdateDismissLabel();
    }

    private void UpdateDismissLabel()
    {
        DismissButton.Content = T(_verified ? "Dialog.Ok" : "About.Close");
        AutomationProperties.SetName(DismissButton, DismissButton.Content.ToString());
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed) return;
        _lifetime.Cancel();
        Dismissed?.Invoke(_verified && !_busy);
    }

    private void WithoutAiButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed) return;
        _lifetime.Cancel();
        WithoutAiRequested?.Invoke();
    }
    private async void PortalButton_Click(object sender, RoutedEventArgs e) => await OpenLinkAsync("keys");
    private async void GuideButton_Click(object sender, RoutedEventArgs e) => await OpenLinkAsync("guide");
    private async void PricingButton_Click(object sender, RoutedEventArgs e) => await OpenLinkAsync("pricing");

    private async Task OpenLinkAsync(string suffix)
    {
        try
        {
            var result = await _application.OpenProductLinkAsync(_provider + "-" + suffix, _token);
            if (!result.Succeeded && !_closed) ShowLinkFailure();
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (Exception) { if (!_closed) ShowLinkFailure(); }
    }

    private void ShowLinkFailure()
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Message = T("About.LinkFailed.Description");
    }

    private void SetLinkLabel(HyperlinkButton button, string key)
    {
        var label = T(key);
        button.Content = label;
        UiLocalization.SetAccessibleLabel(button, label);
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (HelpPanel is null) return;
        var narrow = e.NewSize.Width < 760;
        Grid.SetColumn(HelpPanel, narrow ? 0 : 1);
        Grid.SetRow(HelpPanel, narrow ? 1 : 0);
        HelpColumn.Width = narrow ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
    }

    private string T(string key) => _strings.Translate(key);
}
