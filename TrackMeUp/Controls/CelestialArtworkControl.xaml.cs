// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Application;

namespace TrackMeUp.Controls;

/// <summary>Identifies a decorative, non-measurement illustration in the four-column celestial atlas.</summary>
public enum CelestialArtworkKind
{
    /// <summary>Mercury illustration.</summary>
    Mercury,
    /// <summary>Venus illustration.</summary>
    Venus,
    /// <summary>Mars illustration.</summary>
    Mars,
    /// <summary>Jupiter illustration.</summary>
    Jupiter,
    /// <summary>Saturn illustration.</summary>
    Saturn,
    /// <summary>Uranus illustration.</summary>
    Uranus,
    /// <summary>Neptune illustration.</summary>
    Neptune,
    /// <summary>Sunrise illustration.</summary>
    Sunrise,
    /// <summary>Sunset illustration.</summary>
    Sunset,
    /// <summary>Blue-hour illustration.</summary>
    BlueHour,
    /// <summary>Civil-twilight illustration.</summary>
    Twilight,
    /// <summary>Equinox and solstice illustration.</summary>
    Seasons
}

/// <summary>Displays a reusable atlas thumbnail using native brush cropping, without asset I/O or scientific calculations.</summary>
public sealed partial class CelestialArtworkControl : UserControl
{
    /// <summary>Identifies the selected decorative atlas cell.</summary>
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(CelestialArtworkKind), typeof(CelestialArtworkControl),
        new PropertyMetadata(CelestialArtworkKind.Mercury, OnKindChanged));

    /// <summary>Creates one lightweight photographic-style thumbnail.</summary>
    public CelestialArtworkControl()
    {
        InitializeComponent();
        UpdateCell();
    }

    /// <summary>Gets or sets the atlas illustration; positions and visibility always come from the astronomical DTO.</summary>
    public CelestialArtworkKind Kind
    {
        get => (CelestialArtworkKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Maps an astronomical planet identifier to its presentation-only illustration.</summary>
    internal static CelestialArtworkKind ForPlanet(CelestialBodyKind body) => body switch
    {
        CelestialBodyKind.Mercury => CelestialArtworkKind.Mercury,
        CelestialBodyKind.Venus => CelestialArtworkKind.Venus,
        CelestialBodyKind.Mars => CelestialArtworkKind.Mars,
        CelestialBodyKind.Jupiter => CelestialArtworkKind.Jupiter,
        CelestialBodyKind.Saturn => CelestialArtworkKind.Saturn,
        CelestialBodyKind.Uranus => CelestialArtworkKind.Uranus,
        CelestialBodyKind.Neptune => CelestialArtworkKind.Neptune,
        _ => throw new ArgumentOutOfRangeException(nameof(body), body, "The Sun and Moon use their existing photographed phase control.")
    };

    private static void OnKindChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((CelestialArtworkControl)sender).UpdateCell();

    private void UpdateCell()
    {
        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown celestial atlas cell.");
        }

        var index = (int)Kind;
        AtlasTransform.TranslateX = -(index % 4);
        AtlasTransform.TranslateY = -(index / 4);
        ThumbnailFrame.CornerRadius = new CornerRadius(index < 7 && Kind != CelestialArtworkKind.Saturn ? 48 : 18);
    }
}
