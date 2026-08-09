using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Services;

namespace TrackMeUp.Controls;

/// <summary>Collects and renders local context-plugin operations.</summary>
public sealed partial class PluginOperationsControl : UserControl
{
    private LocalizationService _strings = new("system");
    private OperationsSectionContext? _context;

    /// <summary>Creates the independent plugin operations surface.</summary>
    public PluginOperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
    }

    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow) =>
        _context = new OperationsSectionContext(application, dialogs, ownerWindow, StatusInfoBar, Progress, SectionBody, L);

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("PluginOperationsControl must be initialized before use.");

    private async void ListPluginsButton_Click(object sender, RoutedEventArgs e) => await RefreshPluginsAsync();

    private async Task RefreshPluginsAsync()
    {
        var result = await Context.ExecuteAsync((application, token) => application.GetPluginsAsync(token));
        if (result is { Succeeded: true, Value: { } plugins })
        {
            PluginsList.ItemsSource = plugins.ToArray();
        }
    }

    private async void EnablePluginButton_Click(object sender, RoutedEventArgs e) => await SetSelectedPluginAsync(enabled: true);

    private async void DisablePluginButton_Click(object sender, RoutedEventArgs e) => await SetSelectedPluginAsync(enabled: false);

    private async Task SetSelectedPluginAsync(bool enabled)
    {
        if (PluginsList.SelectedItem is not PluginInfo plugin)
        {
            Context.ShowStatus(L("Selection required", "Selezione richiesta"), L("Select the plugin to change.", "Seleziona il plugin da modificare."), InfoBarSeverity.Warning);
            return;
        }

        var result = await Context.ExecuteAsync((application, token) => application.SetPluginEnabledAsync(plugin.Id, enabled, token));
        if (result is { Succeeded: true })
        {
            await RefreshPluginsAsync();
        }
    }

    private string L(string english, string italian) => _strings.Language == "it" ? italian : english;
}
