// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace WorkTrail.Controls;

/// <summary>Renders the same localized, high-contrast-aware Premium marker on headers and commands.</summary>
public sealed partial class PremiumBadge : UserControl
{
    /// <summary>Creates a non-interactive entitlement marker.</summary>
    public PremiumBadge() => InitializeComponent();

    /// <summary>Sets the localized caption and the identical accessible name.</summary>
    public string Text
    {
        get => Caption.Text;
        set { Caption.Text = value; AutomationProperties.SetName(this, value); }
    }
}
