using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Displays option controls and forwards typed requests to the shared application facade.</summary>
public sealed partial class OptionsControl : UserControl
{
    private ITrackMeUpApplication? _application;
    private LocalizationService _strings = new("system");

    /// <summary>Initializes the options control.</summary>
    public OptionsControl() => InitializeComponent();

    /// <summary>Occurs when the host should restore the player panel.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Occurs after a successfully persisted application settings snapshot is returned.</summary>
    public event Action<AppSettings>? SettingsSaved;

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        UpdateScreenshotModeHint();
    }

    /// <summary>Attaches the shared application facade and loads persisted settings into controls.</summary>
    public async void Initialize(ITrackMeUpApplication application)
    {
        _application = application;
        var result = await application.GetSettingsAsync(CancellationToken.None);
        if (result.Succeeded && result.Value is not null)
        {
            ApplySettings(result.Value);
        }
    }

    /// <summary>Forwards navigation intent to the host view.</summary>
    private void BackButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Forwards a secret to the application facade without placing it in settings or UI state.</summary>
    private async void SetApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null || string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            StatusText.Text = T("ApiKeyMissing");
            return;
        }

        var provider = SelectedTag(AiProviderBox, "openai");
        var keyName = string.IsNullOrWhiteSpace(AiApiKeyNameBox.Text) ? DefaultApiKeyName(provider) : AiApiKeyNameBox.Text.Trim();
        var secret = ApiKeyBox.Password;
        ApiKeyBox.Password = string.Empty;
        var result = await _application.SetAiKeyAsync(keyName, secret, CancellationToken.None);
        secret = string.Empty;
        StatusText.Text = result.Succeeded ? T("ApiKeySaved") : T("Options.ApiKeyError");
    }

    /// <summary>Builds a typed, whitelisted patch and forwards persistence to the application facade.</summary>
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null)
        {
            return;
        }

        var patch = new SettingsPatch(new Dictionary<string, string?>
        {
            ["ai.model"] = string.IsNullOrWhiteSpace(ModelBox.Text) ? "gpt-5.6" : ModelBox.Text.Trim(),
            ["screenshots.directory"] = ScreenshotFolderBox.Text,
            ["screenshots.keep"] = KeepScreenshotsSwitch.IsOn.ToString(),
            ["ai.enabled"] = OpenAiEnabledSwitch.IsOn.ToString(),
            ["ai.automatic"] = AutomaticAnalysisSwitch.IsOn.ToString(),
            ["screenshots.enabled"] = ScreenshotsEnabledSwitch.IsOn.ToString(),
            ["startup.enabled"] = StartWithWindowsSwitch.IsOn.ToString(),
            ["tracking.start_on_launch"] = StartTrackingOnLaunchSwitch.IsOn.ToString(),
            ["screenshots.watermark"] = WatermarkSwitch.IsOn.ToString(),
            ["ai.provider"] = SelectedTag(AiProviderBox, "openai"),
            ["ai.endpoint"] = string.IsNullOrWhiteSpace(AiEndpointBox.Text) ? DefaultEndpoint(SelectedTag(AiProviderBox, "openai")) : AiEndpointBox.Text.Trim(),
            ["ai.key_variable"] = string.IsNullOrWhiteSpace(AiApiKeyNameBox.Text) ? DefaultApiKeyName(SelectedTag(AiProviderBox, "openai")) : AiApiKeyNameBox.Text.Trim(),
            ["ai.output_detail"] = SelectedTag(AiOutputDetailBox, "balanced"),
            ["ai.reasoning_effort"] = SelectedTag(AiReasoningEffortBox, "auto"),
            ["ai.custom_prompt"] = AiCustomPromptBox.Text,
            ["ai.include_device_location"] = IncludeDeviceLocationSwitch.IsOn.ToString(),
            ["screenshots.mode"] = SelectedTag(ScreenshotModeBox, "all-screens"),
            ["language"] = SelectedTag(LanguageBox, "system"),
            ["theme"] = SelectedTag(ThemeBox, "system"),
            ["position"] = SelectedTag(PositionBox, "bottom-center"),
            ["taskbar.widget.position"] = SelectedTag(TaskbarWidgetPositionBox, "left"),
            ["active_hours.monday.active"] = MondayActiveHoursBox.Text,
            ["active_hours.monday.breaks"] = MondayBreaksBox.Text,
            ["active_hours.tuesday.active"] = TuesdayActiveHoursBox.Text,
            ["active_hours.tuesday.breaks"] = TuesdayBreaksBox.Text,
            ["active_hours.wednesday.active"] = WednesdayActiveHoursBox.Text,
            ["active_hours.wednesday.breaks"] = WednesdayBreaksBox.Text,
            ["active_hours.thursday.active"] = ThursdayActiveHoursBox.Text,
            ["active_hours.thursday.breaks"] = ThursdayBreaksBox.Text,
            ["active_hours.friday.active"] = FridayActiveHoursBox.Text,
            ["active_hours.friday.breaks"] = FridayBreaksBox.Text,
            ["active_hours.saturday.active"] = SaturdayActiveHoursBox.Text,
            ["active_hours.saturday.breaks"] = SaturdayBreaksBox.Text,
            ["active_hours.sunday.active"] = SundayActiveHoursBox.Text,
            ["active_hours.sunday.breaks"] = SundayBreaksBox.Text
        });
        var result = await _application.PatchSettingsAsync(patch, CancellationToken.None);
        if (result.Succeeded && result.Value is not null)
        {
            SettingsSaved?.Invoke(result.Value);
            StatusText.Text = T("OptionsSaved");
        }
        else
        {
            StatusText.Text = T("Options.SaveError");
        }
    }

    /// <summary>Forwards report generation to the application facade.</summary>
    private async void ExportReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null) return;
        var result = await _application.GenerateTodayReportAsync(null, false, CancellationToken.None);
        StatusText.Text = result.Succeeded ? $"{T("ReportCreated")}: {result.Value}" : T("Options.ReportError");
    }

    /// <summary>Applies provider defaults as presentation convenience only.</summary>
    private void AiProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var provider = SelectedTag(AiProviderBox, "openai");
        if (IsDefaultOrKnownEndpoint(AiEndpointBox.Text)) AiEndpointBox.Text = DefaultEndpoint(provider);
        if (IsDefaultOrKnownApiKeyName(AiApiKeyNameBox.Text)) AiApiKeyNameBox.Text = DefaultApiKeyName(provider);
    }

    /// <summary>Updates the local capture-mode hint.</summary>
    private void ScreenshotModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateScreenshotModeHint();

    private void ApplySettings(AppSettings settings)
    {
        ApplyLanguage(settings.UiLanguage);
        ModelBox.Text = settings.Model;
        ScreenshotFolderBox.Text = settings.ScreenshotDirectory;
        KeepScreenshotsSwitch.IsOn = settings.KeepScreenshots;
        AutomaticAnalysisSwitch.IsOn = settings.AutomaticAnalysis;
        OpenAiEnabledSwitch.IsOn = settings.OpenAiEnabled;
        ScreenshotsEnabledSwitch.IsOn = settings.ScreenshotsEnabled;
        StartWithWindowsSwitch.IsOn = settings.StartWithWindows;
        StartTrackingOnLaunchSwitch.IsOn = settings.StartTrackingOnLaunch;
        WatermarkSwitch.IsOn = settings.WatermarkScreenshots;
        SelectTag(AiProviderBox, settings.AiProvider, "openai");
        AiEndpointBox.Text = string.IsNullOrWhiteSpace(settings.AiEndpoint) ? DefaultEndpoint(settings.AiProvider) : settings.AiEndpoint;
        AiApiKeyNameBox.Text = string.IsNullOrWhiteSpace(settings.AiApiKeyName) ? DefaultApiKeyName(settings.AiProvider) : settings.AiApiKeyName;
        SelectTag(AiOutputDetailBox, settings.AiOutputDetail, "balanced");
        SelectTag(AiReasoningEffortBox, settings.AiReasoningEffort, "auto");
        SelectTag(ScreenshotModeBox, settings.ScreenshotCaptureMode, "all-screens");
        SelectTag(LanguageBox, settings.UiLanguage, "system");
        SelectTag(PositionBox, settings.FlyoutPosition, "bottom-center");
        SelectTag(TaskbarWidgetPositionBox, settings.TaskbarWidgetPosition, "left");
        SelectTag(ThemeBox, settings.Theme, "system");
        AiCustomPromptBox.Text = settings.AiCustomPrompt;
        IncludeDeviceLocationSwitch.IsOn = settings.IncludeDeviceLocation;
        ApplyActiveHours(settings, "monday", MondayActiveHoursBox, MondayBreaksBox);
        ApplyActiveHours(settings, "tuesday", TuesdayActiveHoursBox, TuesdayBreaksBox);
        ApplyActiveHours(settings, "wednesday", WednesdayActiveHoursBox, WednesdayBreaksBox);
        ApplyActiveHours(settings, "thursday", ThursdayActiveHoursBox, ThursdayBreaksBox);
        ApplyActiveHours(settings, "friday", FridayActiveHoursBox, FridayBreaksBox);
        ApplyActiveHours(settings, "saturday", SaturdayActiveHoursBox, SaturdayBreaksBox);
        ApplyActiveHours(settings, "sunday", SundayActiveHoursBox, SundayBreaksBox);
        UpdateScreenshotModeHint();
    }

    private void UpdateScreenshotModeHint() => ScreenshotModeHintBox.Text = SelectedTag(ScreenshotModeBox, "all-screens") == "active-window" ? T("Options.SnapshotHintActive") : T("Options.SnapshotHintAll");

    private string T(string key) => _strings.Translate(key);

    private static string SelectedTag(ComboBox comboBox, string fallback) => (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private static void SelectTag(ComboBox comboBox, string value, string fallback) => comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, value)) ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, fallback));

    private static void ApplyActiveHours(AppSettings settings, string day, TextBox activeHoursBox, TextBox breaksBox)
    {
        var configuredDay = settings.ActiveHours?.FirstOrDefault(candidate => string.Equals(candidate.Day, day, StringComparison.OrdinalIgnoreCase));
        activeHoursBox.Text = configuredDay?.ActivePeriod ?? string.Empty;
        breaksBox.Text = configuredDay?.BreakPeriods ?? string.Empty;
    }

    private static string DefaultEndpoint(string provider) => SettingsCatalog.GetDefaultEndpoint(provider);

    private static string DefaultApiKeyName(string provider) => SettingsCatalog.GetDefaultApiKeyVariable(provider);

    private static bool IsDefaultOrKnownEndpoint(string? endpoint) => string.IsNullOrWhiteSpace(endpoint) || endpoint is "https://api.openai.com/v1/responses" or "https://openrouter.ai/api/v1/chat/completions" or "https://api.anthropic.com/v1/messages";

    private static bool IsDefaultOrKnownApiKeyName(string? keyName) => string.IsNullOrWhiteSpace(keyName) || keyName.Trim() is "TRACKMEUP_OPENAI_APIKEY" or "OPENAI_API_KEY" or "OPENROUTER_API_KEY" or "ANTHROPIC_API_KEY";
}
