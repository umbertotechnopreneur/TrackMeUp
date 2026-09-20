// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;
using TrackMeUp.Presentation;

namespace TrackMeUp;

public sealed partial class MainWindow
{
    private FeatureAccessSnapshot? _featureAccessState;
#if DEBUG
    private MenuFlyoutItem? _debugFeatureProfileItem;
    private bool _featureProfileChanging;
#endif

    private void InitializeFeatureAccessMenu()
    {
#if DEBUG
        _debugFeatureProfileItem = new MenuFlyoutItem();
        _debugFeatureProfileItem.Click += DebugFeatureProfile_Click;
        MainMenuFlyout.Items.Add(new MenuFlyoutSeparator());
        MainMenuFlyout.Items.Add(_debugFeatureProfileItem);
        UpdateDebugFeatureMenu();
#endif
    }

    private void ApplyFeatureAccess(FeatureAccessSnapshot? access)
    {
        if (_dashboardSurfaceClosed) return;
        _featureAccessState = access;
        PlayerLabelFeatureGate.Access = access;
        UpdateDebugFeatureMenu();
    }

    private void UpdateDebugFeatureMenu()
    {
#if DEBUG
        if (_debugFeatureProfileItem is null) return;
        _debugFeatureProfileItem.Text = T(_featureAccessState?.Tier == ProductTier.Premium
            ? "Premium.DebugSwitchFree" : "Premium.DebugSwitchPremium");
        _debugFeatureProfileItem.IsEnabled = !_featureProfileChanging && _featureAccessState is { CanSimulate: true };
#endif
    }

    private async Task RefreshFeatureAccessAsync()
    {
        try
        {
            var result = await _application.GetFeatureAccessAsync(_lifecycle.Token);
            if (_dashboardSurfaceClosed) return;
            if (!result.Succeeded || result.Value is null) throw new InvalidOperationException("Feature access could not be loaded.");
            ApplyFeatureAccess(result.Value);
        }
        catch (OperationCanceledException) when (_lifecycle.IsCancellationRequested) { }
        catch (Exception)
        {
            // A failed access lookup never unlocks controls or claims a verified license.
            ApplyFeatureAccess(null);
            if (!_dashboardSurfaceClosed)
                await _dialogs.ShowInformativeAsync(this, DialogRequest.Informative(T("Premium.Status"), T("Premium.Error"), T("Dialog.Ok")));
        }
    }

#if DEBUG
    private async void DebugFeatureProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_featureProfileChanging || _featureAccessState is not { CanSimulate: true } current) return;
        _featureProfileChanging = true;
        UpdateDebugFeatureMenu();
        try
        {
            // The host serializes simulation with settings changes; the frontend never writes an entitlement flag.
            var result = await _application.SimulateFeatureAccessAsync(
                current.Tier == ProductTier.Premium ? ProductTier.Free : ProductTier.Premium, _lifecycle.Token);
            if (!result.Succeeded || result.Value is null) throw new InvalidOperationException("Debug simulation failed.");
            if (_dashboardSurfaceClosed) return;
            ApplyFeatureAccess(result.Value);
            var settings = await _application.GetSettingsAsync(_lifecycle.Token);
            if (!settings.Succeeded || settings.Value is null) throw new InvalidOperationException("Settings refresh failed.");
            if (_dashboardSurfaceClosed) return;
            _optionsControl?.ApplyExternalSettings(settings.Value);
            ApplySettings(settings.Value);
        }
        catch (OperationCanceledException) when (_lifecycle.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_dashboardSurfaceClosed)
                await _dialogs.ShowInformativeAsync(this, DialogRequest.Informative(T("Premium.Status"), T("Premium.Error"), T("Dialog.Ok")));
        }
        finally { _featureProfileChanging = false; UpdateDebugFeatureMenu(); }
    }
#endif
}
