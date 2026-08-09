using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Displays option controls and forwards typed requests to the shared application facade.</summary>
public sealed partial class OptionsControl : UserControl
{
    private ITrackMeUpApplication? _application;
    private AiApplicationState? _aiState;
    private LocalizationService _strings = new("system");
    private IReadOnlyList<AiModelOption> _modelOptions = Array.Empty<AiModelOption>();
    private string _requestedThinkingEffort = "auto";
    private bool _updatingAiToggle;

    /// <summary>Initializes the options control.</summary>
    public OptionsControl() => InitializeComponent();

    /// <summary>Occurs when the host should restore the player panel.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Occurs after a successfully persisted application settings snapshot is returned.</summary>
    public event Action<AppSettings>? SettingsSaved;

    /// <summary>Occurs after visible option content changes and the host may need to re-measure its window.</summary>
    public event EventHandler? LayoutChanged;

    /// <summary>Occurs when the user requests a real, bounded test of the saved AI connection.</summary>
    public event EventHandler? AiConnectionTestRequested;

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        if (SelectedModel() is { } selectedModel)
        {
            PopulateThinkingEfforts(selectedModel, SelectedTag(AiReasoningEffortBox, _requestedThinkingEffort));
        }
        var openFolderLabel = T("Options.OpenFolderAction");
        AutomationProperties.SetName(OpenScreenshotFolderButton, openFolderLabel);
        ToolTipService.SetToolTip(OpenScreenshotFolderButton, openFolderLabel);
        AutomationProperties.SetName(TaskbarWidgetVisibleSwitch, T("Options.TaskbarWidget.Visible"));
        AutomationProperties.SetName(StartWithWindowsSwitch, T("Options.StartWithWindows.Header"));
        AutomationProperties.SetName(StartTrackingOnLaunchSwitch, T("Options.StartTracking.Header"));
        AutomationProperties.SetName(ScreenshotsEnabledSwitch, T("Options.SnapshotsEnabled.Header"));
        AutomationProperties.SetName(WatermarkSwitch, T("Options.Watermark.Header"));
        AutomationProperties.SetName(SearchSettingsButton, T("Options.SearchConfiguration.Title"));
        AutomationProperties.SetName(SearchSynonymsSwitch, T("Options.Search.Synonyms"));
        AutomationProperties.SetName(SearchTypoToleranceSwitch, T("Options.Search.TypoTolerance"));
        AutomationProperties.SetName(OcrEnabledSwitch, T("Options.Ocr.Enabled"));
        AutomationProperties.SetName(RebuildSearchIndexButton, T("Options.Search.Rebuild"));
        UpdateApiKeyPresentation();
        UpdateScreenshotModeHint();
        NotifyLayoutChanged();
    }

    /// <summary>Attaches the shared application facade and loads persisted settings into controls.</summary>
    public async void Initialize(ITrackMeUpApplication application, AiApplicationState aiState)
    {
        _application = application;
        if (_aiState is not null)
        {
            _aiState.PropertyChanged -= AiState_PropertyChanged;
        }
        _aiState = aiState;
        _aiState.PropertyChanged += AiState_PropertyChanged;
        DataContext = aiState;
        UpdateApiKeyPresentation();
        var settingsTask = application.GetSettingsAsync(CancellationToken.None);
        var catalogTask = application.GetAiModelCatalogAsync(CancellationToken.None);
        var aiStateTask = aiState.LoadAsync(CancellationToken.None);
        await Task.WhenAll(settingsTask, catalogTask, aiStateTask);

        var settingsResult = await settingsTask;
        var catalogResult = await catalogTask;
        if (!catalogResult.Succeeded || catalogResult.Value is null || catalogResult.Value.Models.Count == 0)
        {
            StatusText.Text = T("Options.ModelCatalogError");
            SaveOptionsButton.IsEnabled = false;
            return;
        }

        ConfigureModelOptions(catalogResult.Value);
        if (settingsResult.Succeeded && settingsResult.Value is not null)
        {
            ApplySettings(settingsResult.Value);
        }
    }

    /// <summary>Moves from AI configuration to general options, then back to the player.</summary>
    public void NavigateBack()
    {
        if (SearchOptionsView.Visibility == Visibility.Visible)
        {
            SearchOptionsView.Visibility = Visibility.Collapsed;
            AppOptionsView.Visibility = Visibility.Visible;
            NotifyLayoutChanged();
            return;
        }

        if (AiOptionsView.Visibility == Visibility.Visible)
        {
            AiOptionsView.Visibility = Visibility.Collapsed;
            AppOptionsView.Visibility = Visibility.Visible;
            NotifyLayoutChanged();
            return;
        }

        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the focused AI configuration view without changing persisted settings.</summary>
    private async void AiSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        AppOptionsView.Visibility = Visibility.Collapsed;
        AiOptionsView.Visibility = Visibility.Visible;
        NotifyLayoutChanged();
        if (_aiState is not null)
        {
            await _aiState.LoadAsync(CancellationToken.None);
        }
    }

    /// <summary>Shows the focused local-search and OCR settings view.</summary>
    private void SearchSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        AppOptionsView.Visibility = Visibility.Collapsed;
        SearchOptionsView.Visibility = Visibility.Visible;
        NotifyLayoutChanged();
    }

    private void OcrEnabledSwitch_Toggled(object sender, RoutedEventArgs e) =>
        OcrLanguageBox.IsEnabled = OcrEnabledSwitch.IsOn;

    /// <summary>Requests a complete rebuild of the reconstructible local search index.</summary>
    private async void RebuildSearchIndexButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null)
        {
            return;
        }

        RebuildSearchIndexButton.IsEnabled = false;
        try
        {
            var result = await _application.RebuildSearchIndexAsync(CancellationToken.None);
            StatusText.Text = result.Succeeded
                ? string.Format(T("Options.Search.RebuildCompleted"), result.Value)
                : T("Options.Search.RebuildError");
        }
        finally
        {
            RebuildSearchIndexButton.IsEnabled = true;
        }
    }

    /// <summary>Maintains a single selected theme in the segmented theme control.</summary>
    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeSystemButton.IsChecked = ReferenceEquals(sender, ThemeSystemButton);
        ThemeLightButton.IsChecked = ReferenceEquals(sender, ThemeLightButton);
        ThemeDarkButton.IsChecked = ReferenceEquals(sender, ThemeDarkButton);
    }

    private void TaskbarWidgetVisibleSwitch_Toggled(object sender, RoutedEventArgs e) =>
        TaskbarWidgetPositionBox.IsEnabled = TaskbarWidgetVisibleSwitch.IsOn;

    /// <summary>Shows or hides lower-frequency application settings.</summary>
    private void GeneralAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var show = GeneralAdvancedPanel.Visibility != Visibility.Visible;
        GeneralAdvancedPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        GeneralAdvancedChevron.Glyph = show ? "\uE70E" : "\uE70D";
        NotifyLayoutChanged();
    }

    /// <summary>Shows or hides lower-frequency provider and analysis settings.</summary>
    private void AiAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var show = AiAdvancedPanel.Visibility != Visibility.Visible;
        AiAdvancedPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        AiAdvancedChevron.Glyph = show ? "\uE70E" : "\uE70D";
        NotifyLayoutChanged();
    }

    /// <summary>Keeps the host window measurement synchronized with the API-key expander.</summary>
    private void ApiKeyExpander_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(e.NewSize.Height - e.PreviousSize.Height) > 0.5)
        {
            NotifyLayoutChanged();
        }
    }

    /// <summary>Opens the configured screen-capture folder through the shared application facade.</summary>
    private async void OpenScreenshotFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null)
        {
            return;
        }

        var result = await _application.OpenScreenshotFolderAsync(ScreenshotFolderBox.Text, CancellationToken.None);
        StatusText.Text = result.Succeeded ? result.Value ?? string.Empty : T("Options.OpenFolderError");
    }

    /// <summary>Forwards a secret to the application facade without placing it in settings or UI state.</summary>
    private async void SetApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null || _aiState is null || string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            StatusText.Text = T("ApiKeyMissing");
            return;
        }

        var provider = SelectedTag(AiProviderBox, "openai");
        var keyName = string.IsNullOrWhiteSpace(AiApiKeyNameBox.Text) ? DefaultApiKeyName(provider) : AiApiKeyNameBox.Text.Trim();
        var secret = ApiKeyBox.Password;
        ApiKeyBox.Password = string.Empty;
        var result = await _aiState.SetSecretAsync(keyName, secret, CancellationToken.None);
        secret = string.Empty;
        var keyReady = result.Succeeded && _aiState.CanEnable;
        if (keyReady)
        {
            ApiKeyExpander.IsExpanded = false;
        }

        StatusText.Text = keyReady
            ? T("ApiKeySaved")
            : result.Succeeded
                ? T("Options.ApiKeyStatus.Invalid")
            : result.Code == "ai.key.stored_status_unavailable"
                ? T("Options.ApiKeyStatus.RefreshFailed")
                : T("Options.ApiKeyError");
    }

    /// <summary>Forwards an AI enabled-state request through the shared observable application state.</summary>
    private async void OpenAiEnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_aiState is null || _updatingAiToggle || OpenAiEnabledSwitch.IsOn == _aiState.Enabled)
        {
            return;
        }

        var result = await _aiState.SetEnabledAsync(OpenAiEnabledSwitch.IsOn, CancellationToken.None);
        if (result.Succeeded)
        {
            return;
        }

        _updatingAiToggle = true;
        OpenAiEnabledSwitch.IsOn = _aiState.Enabled;
        _updatingAiToggle = false;
        StatusText.Text = T("Options.OpenAi.KeyRequired");
    }

    /// <summary>Builds a typed, whitelisted patch and forwards persistence to the application facade.</summary>
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_application is null)
        {
            return;
        }

        var selectedModel = SelectedModel();
        if (selectedModel is null)
        {
            StatusText.Text = T("Options.ModelRequired");
            return;
        }

        if (!selectedModel.SupportsImageInput)
        {
            StatusText.Text = T("Options.ModelImageUnsupported");
            return;
        }

        var thinkingEffort = SelectedTag(AiReasoningEffortBox, "auto");
        if (!selectedModel.SupportedThinkingEfforts.Contains(thinkingEffort, StringComparer.Ordinal))
        {
            StatusText.Text = T("Options.ThinkingEffortUnsupported");
            return;
        }

        var patch = new SettingsPatch(new Dictionary<string, string?>
        {
            ["ai.model"] = selectedModel.Key,
            ["screenshots.directory"] = ScreenshotFolderBox.Text,
            ["screenshots.keep"] = KeepScreenshotsSwitch.IsOn.ToString(),
            ["screenshots.enabled"] = ScreenshotsEnabledSwitch.IsOn.ToString(),
            ["search.language"] = SelectedTag(SearchLanguageBox, "system"),
            ["search.synonyms"] = SearchSynonymsSwitch.IsOn.ToString(),
            ["search.typo_tolerance"] = SearchTypoToleranceSwitch.IsOn.ToString(),
            ["ocr.enabled"] = OcrEnabledSwitch.IsOn.ToString(),
            ["ocr.language"] = SelectedTag(OcrLanguageBox, "system"),
            ["startup.enabled"] = StartWithWindowsSwitch.IsOn.ToString(),
            ["tracking.start_on_launch"] = StartTrackingOnLaunchSwitch.IsOn.ToString(),
            ["screenshots.watermark"] = WatermarkSwitch.IsOn.ToString(),
            ["ai.provider"] = SelectedTag(AiProviderBox, "openai"),
            ["ai.endpoint"] = string.IsNullOrWhiteSpace(AiEndpointBox.Text) ? DefaultEndpoint(SelectedTag(AiProviderBox, "openai")) : AiEndpointBox.Text.Trim(),
            ["ai.key_variable"] = string.IsNullOrWhiteSpace(AiApiKeyNameBox.Text) ? DefaultApiKeyName(SelectedTag(AiProviderBox, "openai")) : AiApiKeyNameBox.Text.Trim(),
            ["ai.output_detail"] = SelectedTag(AiOutputDetailBox, "balanced"),
            ["ai.reasoning_effort"] = thinkingEffort,
            ["ai.custom_prompt"] = AiCustomPromptBox.Text,
            ["ai.include_device_location"] = IncludeDeviceLocationSwitch.IsOn.ToString(),
            ["screenshots.mode"] = SelectedTag(ScreenshotModeBox, "all-screens"),
            ["language"] = SelectedTag(LanguageBox, "system"),
            ["theme"] = SelectedTheme(),
            ["position"] = SelectedTag(PositionBox, "bottom-center"),
            ["taskbar.widget.visible"] = TaskbarWidgetVisibleSwitch.IsOn.ToString(),
            ["taskbar.widget.position"] = SelectedTag(TaskbarWidgetPositionBox, "left")
        });
        var result = await _application.PatchSettingsAsync(patch, CancellationToken.None);
        if (result.Succeeded && result.Value is not null)
        {
            if (_aiState is not null)
            {
                await _aiState.LoadAsync(CancellationToken.None);
            }
            SettingsSaved?.Invoke(result.Value);
            StatusText.Text = T("OptionsSaved");
        }
        else
        {
            StatusText.Text = T("Options.SaveError");
        }
    }

    /// <summary>Forwards the explicit connection-test intent to the owning Mica dialog coordinator.</summary>
    private void TestConnectionButton_Click(object sender, RoutedEventArgs e) => AiConnectionTestRequested?.Invoke(this, EventArgs.Empty);

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
        _requestedThinkingEffort = settings.AiReasoningEffort;
        SelectModel(settings.Model);
        ScreenshotFolderBox.Text = settings.ScreenshotDirectory;
        KeepScreenshotsSwitch.IsOn = settings.KeepScreenshots;
        ScreenshotsEnabledSwitch.IsOn = settings.ScreenshotsEnabled;
        SelectTag(SearchLanguageBox, settings.SearchLanguage, "system");
        SearchSynonymsSwitch.IsOn = settings.SearchSynonymsEnabled;
        SearchTypoToleranceSwitch.IsOn = settings.SearchTypoToleranceEnabled;
        OcrEnabledSwitch.IsOn = settings.OcrEnabled;
        SelectTag(OcrLanguageBox, settings.OcrLanguage, "system");
        OcrLanguageBox.IsEnabled = settings.OcrEnabled;
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
        TaskbarWidgetVisibleSwitch.IsOn = settings.TaskbarWidgetVisible;
        SelectTag(TaskbarWidgetPositionBox, settings.TaskbarWidgetPosition, "left");
        TaskbarWidgetPositionBox.IsEnabled = settings.TaskbarWidgetVisible;
        SelectTheme(settings.Theme);
        AiCustomPromptBox.Text = settings.AiCustomPrompt;
        IncludeDeviceLocationSwitch.IsOn = settings.IncludeDeviceLocation;
        UpdateScreenshotModeHint();
        NotifyLayoutChanged();
    }

    private void UpdateScreenshotModeHint() => ScreenshotModeHintBox.Text = SelectedTag(ScreenshotModeBox, "all-screens") == "active-window" ? T("Options.SnapshotHintActive") : T("Options.SnapshotHintAll");

    private void ConfigureModelOptions(AiModelCatalogSnapshot catalog)
    {
        _modelOptions = catalog.Models
            .Select(model => new AiModelOption(model, CreateBrush(model.Color)))
            .ToArray();
        ModelBox.ItemsSource = _modelOptions;
    }

    private void SelectModel(string identifier)
    {
        ModelBox.SelectedItem = _modelOptions.FirstOrDefault(option =>
            string.Equals(option.Key, identifier, StringComparison.OrdinalIgnoreCase) ||
            option.Aliases.Contains(identifier, StringComparer.OrdinalIgnoreCase));
        if (ModelBox.SelectedItem is null)
        {
            ModelInfoCard.Visibility = Visibility.Collapsed;
            StatusText.Text = T("Options.ModelUnsupported");
            NotifyLayoutChanged();
        }
    }

    private AiModelDescriptor? SelectedModel() => (ModelBox.SelectedItem as AiModelOption)?.Descriptor;

    private void ModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var model = SelectedModel();
        if (model is null)
        {
            ModelInfoCard.Visibility = Visibility.Collapsed;
            NotifyLayoutChanged();
            return;
        }

        var preferredEffort = AiReasoningEffortBox.SelectedItem is null
            ? _requestedThinkingEffort
            : SelectedTag(AiReasoningEffortBox, _requestedThinkingEffort);
        PopulateThinkingEfforts(model, preferredEffort);
        _requestedThinkingEffort = SelectedTag(AiReasoningEffortBox, "auto");
        var option = (AiModelOption)ModelBox.SelectedItem;
        ModelAccentBar.Background = option.AccentBrush;
        ModelInfoCard.BorderBrush = option.AccentBrush;
        ModelDescriptionText.Text = model.Description;
        ModelKeyText.Text = model.Key;
        ModelPreviewBadge.Visibility = model.IsPreview ? Visibility.Visible : Visibility.Collapsed;
        ModelCapabilityText.Text = T(model.SupportsImageInput ? "Options.ModelImageInput" : "Options.ModelTextOnly");
        ModelCapabilityBadge.Visibility = Visibility.Visible;
        ModelInfoCard.Visibility = Visibility.Visible;
        StatusText.Text = model.SupportsImageInput ? string.Empty : T("Options.ModelImageUnsupported");
        NotifyLayoutChanged();
    }

    private void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    private void PopulateThinkingEfforts(AiModelDescriptor model, string preferredEffort)
    {
        AiReasoningEffortBox.Items.Clear();
        foreach (var effort in model.SupportedThinkingEfforts)
        {
            AiReasoningEffortBox.Items.Add(new ComboBoxItem
            {
                Tag = effort,
                Content = T($"Options.Reasoning.{effort}")
            });
        }

        SelectTag(AiReasoningEffortBox, preferredEffort, "auto");
        AiReasoningEffortBox.IsEnabled = AiReasoningEffortBox.Items.Count > 1;
    }

    private static SolidColorBrush CreateBrush(string hexColor)
    {
        var red = Convert.ToByte(hexColor.Substring(1, 2), 16);
        var green = Convert.ToByte(hexColor.Substring(3, 2), 16);
        var blue = Convert.ToByte(hexColor.Substring(5, 2), 16);
        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, red, green, blue));
    }

    private void AiState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AiApplicationState.HasKey)
            or nameof(AiApplicationState.CanEnable)
            or nameof(AiApplicationState.Enabled)
            or nameof(AiApplicationState.IsStatusUnavailable))
        {
            UpdateApiKeyPresentation();
        }
    }

    private void UpdateApiKeyPresentation()
    {
        AutomationProperties.SetName(OpenAiEnabledSwitch, T("Options.OpenAi.Header"));
        AutomationProperties.SetHelpText(
            OpenAiEnabledSwitch,
            _aiState is null || _aiState.IsStatusUnavailable
                ? T("Options.ApiKeyStatus.Unavailable")
                : !_aiState.CanEnable && !_aiState.Enabled
                    ? T("Options.OpenAi.KeyRequired")
                    : string.Empty);

        var (messageKey, glyph, visualState, actionKey) = _aiState switch
        {
            null => ("Options.ApiKeyStatus.Unavailable", "\uE7BA", "ApiKeyNeedsAttention", "Options.ApiKeyAction.Set"),
            { IsStatusUnavailable: true } => ("Options.ApiKeyStatus.Unavailable", "\uE7BA", "ApiKeyNeedsAttention", "Options.ApiKeyAction.Set"),
            { CanEnable: true } => ("Options.ApiKeyStatus.Set", "\uE73E", "ApiKeyReady", "Options.ApiKeyAction.Change"),
            { HasKey: false } => ("Options.ApiKeyStatus.Missing", "\uE7BA", "ApiKeyNeedsAttention", "Options.ApiKeyAction.Set"),
            _ => ("Options.ApiKeyStatus.Invalid", "\uE7BA", "ApiKeyNeedsAttention", "Options.ApiKeyAction.Set")
        };

        var actionLabel = T(actionKey);
        ApiKeyStatusIcon.Glyph = glyph;
        ApiKeyStatusText.Text = T(messageKey);
        ApiKeyActionText.Text = actionLabel;
        AutomationProperties.SetName(ApiKeyExpander, actionLabel);
        AutomationProperties.SetHelpText(ApiKeyExpander, ApiKeyStatusText.Text);
        VisualStateManager.GoToState(this, visualState, false);
        NotifyLayoutChanged();
    }

    private string T(string key) => _strings.Translate(key);

    private string SelectedTheme() => ThemeLightButton.IsChecked == true ? "light" : ThemeDarkButton.IsChecked == true ? "dark" : "system";

    private void SelectTheme(string theme)
    {
        ThemeSystemButton.IsChecked = theme is not "light" and not "dark";
        ThemeLightButton.IsChecked = theme == "light";
        ThemeDarkButton.IsChecked = theme == "dark";
    }

    private static string SelectedTag(ComboBox comboBox, string fallback) => (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private static void SelectTag(ComboBox comboBox, string value, string fallback) => comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, value)) ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, fallback));

    private static string DefaultEndpoint(string provider) => SettingsCatalog.GetDefaultEndpoint(provider);

    private static string DefaultApiKeyName(string provider) => SettingsCatalog.GetDefaultApiKeyVariable(provider);

    private static bool IsDefaultOrKnownEndpoint(string? endpoint) => string.IsNullOrWhiteSpace(endpoint) || endpoint is "https://api.openai.com/v1/responses" or "https://openrouter.ai/api/v1/chat/completions" or "https://api.anthropic.com/v1/messages";

    private static bool IsDefaultOrKnownApiKeyName(string? keyName) => string.IsNullOrWhiteSpace(keyName) || keyName.Trim() is "TRACKMEUP_OPENAI_APIKEY" or "OPENAI_API_KEY" or "OPENROUTER_API_KEY" or "ANTHROPIC_API_KEY";
}

internal sealed class AiModelOption(AiModelDescriptor descriptor, SolidColorBrush accentBrush)
{
    public AiModelDescriptor Descriptor { get; } = descriptor;

    public string Key => Descriptor.Key;

    public string Name => Descriptor.Name;

    public IReadOnlyList<string> Aliases => Descriptor.Aliases;

    public string CapabilityLabel => Descriptor.SupportsImageInput ? string.Empty : "Text only";

    public SolidColorBrush AccentBrush { get; } = accentBrush;
}
