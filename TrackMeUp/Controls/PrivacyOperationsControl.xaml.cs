using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects and renders local privacy-rule operations.</summary>
public sealed partial class PrivacyOperationsControl : UserControl
{
    private LocalizationService _strings = new("system");
    private OperationsSectionContext? _context;
    private PrivacyRule[] _rules = [];

    /// <summary>Creates the independent privacy operations surface.</summary>
    public PrivacyOperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
        AutomationProperties.SetName(PrivacyRulesList, _strings.Translate("Operations.Privacy"));
        ApplyPrivacyRules();
    }

    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow, TimedInfoBar banner) =>
        _context = new OperationsSectionContext(
            application,
            dialogs,
            ownerWindow,
            banner,
            Progress,
            SectionBody,
            key => _strings.TryTranslate(key, out var value) ? value : null);

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("PrivacyOperationsControl must be initialized before use.");

    private async void AddPrivacyRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var type = SelectedTag(PrivacyRuleTypeBox, "process");
        var result = await Context.ExecuteAsync((application, token) => application.AddPrivacyRuleAsync(type, PrivacyRuleValueBox.Text, token));
        if (result is { Succeeded: true })
        {
            PrivacyRuleValueBox.Text = string.Empty;
            await RefreshPrivacyRulesAsync();
        }
    }

    private async void ListPrivacyRulesButton_Click(object sender, RoutedEventArgs e) => await RefreshPrivacyRulesAsync();

    private async Task RefreshPrivacyRulesAsync()
    {
        var result = await Context.ExecuteAsync((application, token) => application.GetPrivacyRulesAsync(token));
        if (result is { Succeeded: true, Value: { } rules })
        {
            _rules = rules.ToArray();
            ApplyPrivacyRules();
        }
    }

    private async void RemovePrivacyRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (PrivacyRulesList.SelectedItem is not PrivacyRuleListItem item)
        {
            Context.ShowStatus(
                _strings.Translate("Operations.Privacy.SelectionRequired.Title"),
                _strings.Translate("Operations.Privacy.SelectionRequired.Message"),
                InfoBarSeverity.Warning);
            return;
        }

        var result = await Context.ExecuteAsync((application, token) => application.RemovePrivacyRuleAsync(item.Id, token));
        if (result is { Succeeded: true })
        {
            await RefreshPrivacyRulesAsync();
        }
    }

    private async void TestPrivacyButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await Context.ExecuteAsync((application, token) => application.TestCurrentPrivacyAsync(token));
        if (result is { Succeeded: true, Value: { } blocked })
        {
            PrivacyTestText.Text = blocked
                ? _strings.Translate("Operations.Privacy.ContextBlocked")
                : _strings.Translate("Operations.Privacy.ContextAllowed");
        }
    }

    private static string SelectedTag(ComboBox comboBox, string fallback) => comboBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private void ApplyPrivacyRules() =>
        PrivacyRulesList.ItemsSource = _rules
            .Select(rule => new PrivacyRuleListItem(
                rule.Id,
                _strings.Translate($"Operations.PrivacyType.{rule.Type}"),
                rule.Value))
            .ToArray();

    private sealed record PrivacyRuleListItem(string Id, string TypeLabel, string Value);
}
