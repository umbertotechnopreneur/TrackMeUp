// SPDX-License-Identifier: MIT

using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace TrackMeUp;

/// <summary>Applies the Z translation required for a global ThemeShadow on InfoBars.</summary>
public static class InfoBarElevationBehavior
{
    private const float Elevation = 12f;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(InfoBarElevationBehavior),
        new PropertyMetadata(false, IsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void IsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InfoBar infoBar)
        {
            throw new InvalidOperationException("InfoBar elevation can only be attached to an InfoBar.");
        }

        var isEnabled = (bool)args.NewValue;
        ElementCompositionPreview.SetIsTranslationEnabled(infoBar, isEnabled);
        infoBar.Translation = isEnabled ? new Vector3(0f, 0f, Elevation) : Vector3.Zero;
    }
}
