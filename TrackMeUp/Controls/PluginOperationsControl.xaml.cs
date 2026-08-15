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
    private PluginInfo[] _plugins = [];
    private bool _isApplyingPluginState;

    /// <summary>Creates the independent plugin operations surface.</summary>
    public PluginOperationsControl() => InitializeComponent();

    /// <summary>Applies an explicit language override or resolves the Windows UI language for system mode.</summary>
    public void ApplyLanguage(string language)
    {
        _strings = new LocalizationService(language);
        UiLocalization.Apply(this, _strings);
    }

    internal void Initialize(ITrackMeUpApplication application, MicaDialogService dialogs, Window ownerWindow, TimedInfoBar banner) =>
        _context = new OperationsSectionContext(application, dialogs, ownerWindow, banner, Progress, SectionBody, L, key => _strings.Translate(key));

    private OperationsSectionContext Context => _context ?? throw new InvalidOperationException("PluginOperationsControl must be initialized before use.");

    internal async Task LoadAsync()
    {
        var result = await Context.ExecuteAsync((application, token) => application.GetPluginsAsync(token), showSuccess: false);
        if (result is { Succeeded: true, Value: { } plugins })
        {
            ApplyPlugins(plugins.ToArray());
        }
    }

    private async void PluginToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPluginState || sender is not ToggleSwitch toggle || toggle.Tag is not string pluginId)
        {
            return;
        }

        var plugin = _plugins.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Id, pluginId));
        if (plugin is null || plugin.Enabled == toggle.IsOn)
        {
            // Initial binding and programmatic restoration can raise Toggled; neither is a user mutation.
            return;
        }

        var requestedState = toggle.IsOn;
        var result = await Context.ExecuteAsync(
            (application, token) => application.SetPluginEnabledAsync(plugin.Id, requestedState, token),
            showSuccess: false);
        if (result is { Succeeded: true, Value: { } updatedPlugin } &&
            StringComparer.Ordinal.Equals(updatedPlugin.Id, plugin.Id) &&
            updatedPlugin.Enabled == requestedState)
        {
            ReplacePlugin(updatedPlugin);
            Context.ShowStatus(
                L("Operation completed", "Operazione completata"),
                Context.ResultMessage(result.MessageKey, succeeded: true),
                InfoBarSeverity.Success);
            return;
        }

        RestoreToggle(toggle, plugin.Enabled);
        if (result is { Succeeded: true })
        {
            Context.ShowStatus(
                L("Operation failed", "Operazione non completata"),
                L("The runtime returned an invalid plugin state.", "Il runtime ha restituito uno stato del plugin non valido."),
                InfoBarSeverity.Error);
        }
    }

    private void ApplyPlugins(PluginInfo[] plugins)
    {
        _isApplyingPluginState = true;
        try
        {
            _plugins = plugins;
            PluginsList.ItemsSource = plugins;
        }
        finally
        {
            _isApplyingPluginState = false;
        }
    }

    private void ReplacePlugin(PluginInfo updatedPlugin)
    {
        var index = Array.FindIndex(_plugins, plugin => StringComparer.Ordinal.Equals(plugin.Id, updatedPlugin.Id));
        if (index < 0)
        {
            throw new InvalidOperationException("The updated plugin is not present in the loaded plugin collection.");
        }

        var updatedPlugins = (PluginInfo[])_plugins.Clone();
        updatedPlugins[index] = updatedPlugin;
        ApplyPlugins(updatedPlugins);
    }

    private void RestoreToggle(ToggleSwitch toggle, bool enabled)
    {
        _isApplyingPluginState = true;
        try
        {
            toggle.IsOn = enabled;
        }
        finally
        {
            _isApplyingPluginState = false;
        }
    }

    private string L(string english, string italian) => _strings.Language == "it" ? italian : english;
}
