// SPDX-License-Identifier: MIT

namespace TrackMeUp;

/// <summary>Describes the localized copy for one standard WinUI confirmation or acknowledgement dialog.</summary>
internal sealed record DialogRequest(
    string Title,
    string Message,
    string PrimaryButtonText,
    string? CloseButtonText)
{
    /// <summary>Creates a one-button acknowledgement request.</summary>
    internal static DialogRequest Informative(string title, string message, string primaryButtonText) =>
        new(title, message, primaryButtonText, CloseButtonText: null);

    /// <summary>Creates a safe-default confirmation request with an explicit cancel action.</summary>
    internal static DialogRequest Confirmation(
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText) =>
        new(title, message, primaryButtonText, closeButtonText);
}
