// SPDX-License-Identifier: MIT

using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Plots Core-computed horizontal sky coordinates on a centered, responsive all-sky chart.</summary>
public sealed partial class LocalSkyControl : UserControl
{
    private CelestialSnapshot? _snapshot;
    private LocalizationService _strings = new("system");
    private double _centerX;
    private double _centerY;
    private double _radius;
    private double _viewportWidth;
    private double _viewportHeight;
    private readonly List<Rect> _labelBounds = [];

    /// <summary>Creates a passive sky chart without location, clock, or astronomy services.</summary>
    public LocalSkyControl()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => Render();
    }

    /// <summary>Renders one complete astronomical DTO; position calculations remain in Core.</summary>
    internal void Apply(CelestialSnapshot snapshot, LocalizationService strings)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
        ZenithColorStop.Color = ParseColor(snapshot.SkyAppearance.ZenithColor);
        UpperSkyColorStop.Color = ParseColor(snapshot.SkyAppearance.UpperSkyColor);
        HorizonColorStop.Color = ParseColor(snapshot.SkyAppearance.HorizonColor);
        UiLocalization.SetAccessibleLabel(SkyViewport, _strings.Translate("Celestial.Sky.ChartDescription"));
        Render();
    }

    private void SkyViewport_SizeChanged(object sender, SizeChangedEventArgs e) => Render();

    private void Render()
    {
        SkyCanvas.Children.Clear();
        _labelBounds.Clear();
        var width = Math.Max(0d, SkyViewport.ActualWidth);
        var height = Math.Max(0d, SkyViewport.ActualHeight);
        _viewportWidth = width;
        _viewportHeight = height;
        // The canvas follows its bounded viewport instead of carrying a scroll extent or a zoom transform.
        SkyCanvas.Clip = new RectangleGeometry { Rect = new Rect(0d, 0d, width, height) };
        if (_snapshot is null || width < 60d || height < 60d)
        {
            return;
        }

        _centerX = width / 2d;
        _centerY = height / 2d;
        _radius = Math.Max(1d, (Math.Min(width, height) / 2d) - 32d);
        var secondary = Brush("TextFillColorSecondaryBrush");
        var accent = Brush("AccentTextFillColorPrimaryBrush");
        var grid = Brush("DividerStrokeColorDefaultBrush");
        foreach (var altitude in new[] { 0d, 30d, 60d })
        {
            var radius = _radius * (1d - (altitude / 90d));
            Add(new Ellipse
            {
                Width = radius * 2d,
                Height = radius * 2d,
                Stroke = altitude == 0d ? accent : grid,
                StrokeThickness = altitude == 0d ? 1.5d : 1d,
                Opacity = altitude == 0d ? 0.65d : 0.6d,
                StrokeDashArray = altitude == 0d ? [] : [2d, 4d]
            }, _centerX - radius, _centerY - radius);
            if (altitude > 0d && _radius > 130d)
            {
                Label($"{altitude:0}°", _centerX + 6d, _centerY - radius, secondary, 10d);
            }
        }

        for (var azimuth = 0d; azimuth < 360d; azimuth += 45d)
        {
            var edge = Project(0, azimuth);
            SkyCanvas.Children.Add(new Line
            {
                X1 = _centerX,
                Y1 = _centerY,
                X2 = edge.X,
                Y2 = edge.Y,
                Stroke = grid,
                StrokeThickness = 0.6d,
                StrokeDashArray = [2d, 5d]
            });
        }

        Label(_strings.Translate("Celestial.Sky.North"), _centerX - 5d, _centerY - _radius - 23d, secondary, 13d);
        Label(_strings.Translate("Celestial.Sky.South"), _centerX - 5d, _centerY + _radius + 3d, secondary, 13d);
        Label(_strings.Translate("Celestial.Sky.East"), _centerX - _radius - 20d, _centerY - 8d, secondary, 13d);
        Label(_strings.Translate("Celestial.Sky.West"), _centerX + _radius + 8d, _centerY - 8d, secondary, 13d);
        if (_radius > 130d)
        {
            Label(_strings.Translate("Celestial.Sky.Zenith"), _centerX + 6d, _centerY + 5d, secondary, 10d);
        }

        var stars = _snapshot.Stars.ToDictionary(star => star.Id, StringComparer.Ordinal);
        foreach (var segment in _snapshot.ConstellationSegments)
        {
            var start = stars[segment.StartStarId];
            var end = stars[segment.EndStarId];
            if (start.AltitudeDegrees < 0d || end.AltitudeDegrees < 0d)
            {
                continue;
            }

            var from = Project(start.AltitudeDegrees, start.AzimuthDegrees);
            var to = Project(end.AltitudeDegrees, end.AzimuthDegrees);
            SkyCanvas.Children.Add(new Line
            {
                X1 = from.X,
                Y1 = from.Y,
                X2 = to.X,
                Y2 = to.Y,
                Stroke = accent,
                StrokeThickness = 1d,
                Opacity = 0.42d * _snapshot.SkyAppearance.StarOpacity
            });
        }

        foreach (var star in _snapshot.Stars.Where(star => star.AltitudeDegrees >= 0d))
        {
            var point = Project(star.AltitudeDegrees, star.AzimuthDegrees);
            var diameter = Math.Clamp(5d - (star.Magnitude ?? 2d), 2d, 7d);
            if (diameter > 3.4d)
            {
                Add(new Ellipse { Width = diameter * 4d, Height = diameter * 4d, Fill = Brush("TextFillColorPrimaryBrush"), Opacity = 0.035d * _snapshot.SkyAppearance.StarOpacity },
                    point.X - (diameter * 2d), point.Y - (diameter * 2d));
                Add(new Ellipse { Width = diameter * 2d, Height = diameter * 2d, Fill = accent, Opacity = 0.12d * _snapshot.SkyAppearance.StarOpacity },
                    point.X - diameter, point.Y - diameter);
            }

            var dot = new Ellipse { Width = diameter, Height = diameter, Fill = Brush("TextFillColorPrimaryBrush"), Opacity = 0.8d * _snapshot.SkyAppearance.StarOpacity };
            UiLocalization.SetAccessibleLabel(dot, star.Name);
            Add(dot, point.X - (diameter / 2d), point.Y - (diameter / 2d));
        }

        if (_radius > 130d && _snapshot.SkyAppearance.StarOpacity > 0.15d)
        {
            foreach (var constellation in _snapshot.ConstellationSegments.GroupBy(segment => segment.ConstellationId))
            {
                var positions = constellation.SelectMany(segment => new[] { segment.StartStarId, segment.EndStarId })
                    .Distinct().Select(id => stars[id]).Where(star => star.AltitudeDegrees > 5d).ToArray();
                if (positions.Length < 3)
                {
                    continue;
                }

                var points = positions.Select(star => Project(star.AltitudeDegrees, star.AzimuthDegrees)).ToArray();
                Label(_strings.Translate($"CelestialConstellation{constellation.Key}"),
                    points.Average(point => point.X) + 8d, points.Average(point => point.Y) + 8d, secondary, 11d);
            }
        }

        foreach (var body in _snapshot.Bodies.Where(body => body.IsAboveHorizon))
        {
            var point = Project(body.AltitudeDegrees, body.AzimuthDegrees);
            var diameter = body.Kind is CelestialBodyKind.Sun or CelestialBodyKind.Moon
                ? (_radius > 170d ? 44d : 30d)
                : (_radius > 170d ? 34d : 24d);
            if (body.Kind == CelestialBodyKind.Sun)
            {
                var glowSize = Math.Clamp(_radius * 0.9d, 60d, 220d);
                var glow = new RadialGradientBrush();
                glow.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(85, 255, 220, 141), Offset = 0d });
                glow.GradientStops.Add(new GradientStop { Color = Windows.UI.Color.FromArgb(0, 255, 182, 102), Offset = 1d });
                Add(new Ellipse
                {
                    Width = glowSize,
                    Height = glowSize,
                    Fill = glow,
                    Opacity = _snapshot.SkyAppearance.SunGlowOpacity * SkyDecorations.Opacity,
                    IsHitTestVisible = false
                }, point.X - (glowSize / 2d), point.Y - (glowSize / 2d));
            }

            Add(new Ellipse { Width = diameter * 2.5d, Height = diameter * 2.5d, Fill = accent, Opacity = 0.08d },
                point.X - (diameter * 1.25d), point.Y - (diameter * 1.25d));
            FrameworkElement dot = body.Kind is CelestialBodyKind.Sun or CelestialBodyKind.Moon
                ? new CelestialPhaseControl
                {
                    Width = diameter,
                    Height = diameter,
                    IsDaylight = body.Kind == CelestialBodyKind.Sun,
                    MoonPhaseAngleDegrees = _snapshot.MoonPhaseAngleDegrees
                }
                : new CelestialArtworkControl
                {
                    Width = diameter,
                    Height = diameter,
                    Kind = CelestialArtworkControl.ForPlanet(body.Kind)
                };
            var name = _strings.Translate($"CelestialBody{body.Kind}");
            UiLocalization.SetAccessibleLabel(dot, _strings.Format("Celestial.Sky.BodyPosition", name, body.AltitudeDegrees, body.AzimuthDegrees));
            Add(dot, point.X - (diameter / 2d), point.Y - (diameter / 2d));
            if (_radius > 80d)
            {
                Label(name, point.X + (diameter / 2d) + 5d, point.Y - 9d, Brush("TextFillColorPrimaryBrush"), _radius > 170d ? 14d : 12d);
            }
        }
    }

    private Point Project(double altitude, double azimuth)
    {
        // This is only the all-sky drawing projection: zenith at the center, east on the left.
        var radius = _radius * (1d - (altitude / 90d));
        var radians = azimuth * Math.PI / 180d;
        return new Point(_centerX - (radius * Math.Sin(radians)), _centerY - (radius * Math.Cos(radians)));
    }

    private static Windows.UI.Color ParseColor(string value)
    {
        if (value.Length != 7 || value[0] != '#')
        {
            throw new InvalidDataException("The sky palette must supply six-digit RGB colors.");
        }

        return Windows.UI.Color.FromArgb(255,
            byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private void Label(string text, double x, double y, Brush foreground, double size)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeights.Light,
            Foreground = foreground,
            MaxWidth = Math.Max(20d, _viewportWidth - 12d),
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Math.Min(_viewportWidth, label.DesiredSize.Width + 8d);
        var height = label.DesiredSize.Height + 2d;
        var left = x + width > _viewportWidth ? x - width - 12d : x;
        left = Math.Clamp(left, 0d, Math.Max(0d, _viewportWidth - width));
        var top = Math.Clamp(y, 0d, Math.Max(0d, _viewportHeight - height));
        foreach (var offset in new[] { 0d, -height, height, -2d * height, 2d * height })
        {
            var candidateTop = Math.Clamp(top + offset, 0d, Math.Max(0d, _viewportHeight - height));
            var candidate = new Rect(left, candidateTop, width, height);
            if (_labelBounds.All(existing => !Intersects(existing, candidate)))
            {
                top = candidateTop;
                break;
            }
        }

        _labelBounds.Add(new Rect(left, top, width, height));
        Add(new Border
        {
            Child = label,
            Padding = new Thickness(4, 1, 4, 1),
            CornerRadius = new CornerRadius(4),
            Background = LabelPalette.Background,
            IsHitTestVisible = false
        }, left, top);
    }

    private static bool Intersects(Rect first, Rect second) =>
        first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;

    private void Add(FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        SkyCanvas.Children.Add(element);
    }

    private Brush Brush(string key) => key switch
    {
        "TextFillColorPrimaryBrush" => PrimaryPalette.Background,
        "TextFillColorSecondaryBrush" => SecondaryPalette.Background,
        "AccentTextFillColorPrimaryBrush" => AccentPalette.Background,
        "DividerStrokeColorDefaultBrush" => DividerPalette.Background,
        _ => throw new ArgumentException("Unsupported sky theme brush.", nameof(key))
    };
}
