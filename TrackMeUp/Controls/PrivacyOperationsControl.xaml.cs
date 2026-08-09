using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects and renders local privacy-rule operations.</summary>
public sealed partial class PrivacyOperationsControl : UserControl
{
    private LocalizationService _strings = new("system");
    private OperationsSectionContext? _context;

    /// <summary>Creates the independent privacy operations surface.</summary>
    public PrivacyOperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
    }

    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow) =>
        _context = new OperationsSectionContext(application, dialogs, ownerWindow, StatusInfoBar, Progress, SectionBody, L);

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
            PrivacyRulesList.ItemsSource = rules.ToArray();
        }
    }

    private async void RemovePrivacyRuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (PrivacyRulesList.SelectedItem is not PrivacyRule rule)
        {
            Context.ShowStatus(L("Selection required", "Selezione richiesta"), L("Select the privacy rule to remove.", "Seleziona la regola privacy da rimuovere."), InfoBarSeverity.Warning);
            return;
        }

        var result = await Context.ExecuteAsync((application, token) => application.RemovePrivacyRuleAsync(rule.Id, token));
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
                ? L("The current context is blocked by privacy rules.", "Il contesto corrente è bloccato dalle regole privacy.")
                : L("The current context is not blocked.", "Il contesto corrente non è bloccato.");
        }
    }

    private static string SelectedTag(ComboBox comboBox, string fallback) => comboBox.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private string L(string english, string italian) => _strings.Language == "it" ? italian : english;
}
