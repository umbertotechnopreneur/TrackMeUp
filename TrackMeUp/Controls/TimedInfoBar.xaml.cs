using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace TrackMeUp.Controls;

/// <summary>Renders one closable InfoBar with a decorative timeout indicator over its lower edge.</summary>
public sealed partial class TimedInfoBar : UserControl
{
    private const float BannerElevation = 12f;
    private bool _isPresented;

    /// <summary>Creates an initially collapsed timed banner host.</summary>
    public TimedInfoBar()
    {
        InitializeComponent();
        ElementCompositionPreview.SetIsTranslationEnabled(BannerSurface, true);
        BannerSurface.Translation = new Vector3(0f, 0f, BannerElevation);
        BannerInfoBar.Closed += BannerInfoBar_Closed;
        Unloaded += TimedInfoBar_Unloaded;
    }

    /// <summary>Occurs when the banner is closed manually, programmatically, or because its host unloads.</summary>
    internal event EventHandler? Dismissed;

    /// <summary>Gets the determinate indicator updated by the centralized banner service.</summary>
    internal ProgressBar CountdownIndicator => CountdownProgress;

    /// <summary>Displays a banner without owning its timeout lifecycle.</summary>
    internal void Present(string title, string message, InfoBarSeverity severity)
    {
        BannerInfoBar.Title = title;
        BannerInfoBar.Message = message;
        BannerInfoBar.Severity = severity;
        CountdownProgress.Value = CountdownProgress.Maximum;
        Visibility = Visibility.Visible;
        _isPresented = true;
        BannerInfoBar.IsOpen = true;
    }

    /// <summary>Closes the current banner and clears its decorative countdown.</summary>
    internal void Dismiss()
    {
        BannerInfoBar.IsOpen = false;
        CompleteDismissal();
    }

    private void BannerInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args) => CompleteDismissal();

    private void TimedInfoBar_Unloaded(object sender, RoutedEventArgs e) => Dismiss();

    private void CompleteDismissal()
    {
        if (!_isPresented)
        {
            return;
        }

        _isPresented = false;
        CountdownProgress.Value = CountdownProgress.Minimum;
        Visibility = Visibility.Collapsed;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
