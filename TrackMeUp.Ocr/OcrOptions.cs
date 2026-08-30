// SPDX-License-Identifier: MIT

namespace TrackMeUp.Ocr;

/// <summary>
/// Configures local screenshot text extraction.
/// </summary>
public sealed record OcrOptions
{
    /// <summary>
    /// Gets whether local OCR is enabled. The default is <see langword="false"/>.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the optional BCP-47 language tag requested from Windows OCR.
    /// When this value is <see langword="null"/>, Windows selects the first supported user-profile language.
    /// </summary>
    public string? PreferredLanguageTag { get; init; }
}
