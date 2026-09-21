// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml.Controls;

namespace WorkTrail;

/// <summary>Renders localized reset warnings over theme-aware Acrylic and decorative artwork.</summary>
public sealed partial class AtomicResetDialog : ContentDialog
{
    /// <summary>Initializes one confirmation step without performing any reset operation.</summary>
    internal AtomicResetDialog(DialogRequest request)
    {
        InitializeComponent();
        Title = request.Title;
        WarningMessage.Text = request.Message;
        PrimaryButtonText = request.PrimaryButtonText;
        CloseButtonText = request.CloseButtonText;
    }
}
