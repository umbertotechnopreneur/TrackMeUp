// SPDX-License-Identifier: MIT

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Plots Core-computed horizontal sky coordinates on a responsive, zoomable all-sky chart.</summary>
public sealed partial class LocalSkyControl : UserControl
{
    private CelestialSnapshot? _snapshot;
    private LocalizationService _strings = new("system");
    private double _centerX;
    private double _centerY;
    private double _radius;

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
        UiLocalization.SetAccessibleLabel(SkyViewport, _strings.Translate("Celestial.Sky.ChartDescription"));
        Render();
    }

    /// <summary>Changes presentation magnification while retaining the physical sky coordinates.</summary>
    internal void SetZoom(double zoom) => SkyViewport.ChangeView(null, null, (float)zoom);

    private void SkyViewport_SizeChanged(object sender, SizeChangedEventArgs e) => Render();

    private void Render()
    {
        SkyCanvas.Children.Clear();
        var width = Math.Max(0d, SkyViewport.ActualWidth - 8d);
        var height = Math.Max(0d, SkyViewport.ActualHeight - 8d);
        if (_snapshot is null || width < 60d || height < 60d)
        {
            return;
        }

        SkyCanvas.Width = width;
        SkyCanvas.Height = height;
        _centerX = width / 2d;
        _centerY = height / 2d;
        _radius = Math.Max(1d, (Math.Min(width, height) / 2d) - 26d);
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
                Opacity = altitude == 0d ? 0.65d : 0.8d
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
                StrokeThickness = 0.6d
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
                Opacity = 0.42d
            });
        }

        foreach (var star in _snapshot.Stars.Where(star => star.AltitudeDegrees >= 0d))
        {
            var point = Project(star.AltitudeDegrees, star.AzimuthDegrees);
            var diameter = Math.Clamp(5d - (star.Magnitude ?? 2d), 2d, 7d);
            var dot = new Ellipse { Width = diameter, Height = diameter, Fill = Brush("TextFillColorPrimaryBrush"), Opacity = 0.8d };
            UiLocalization.SetAccessibleLabel(dot, star.Name);
            Add(dot, point.X - (diameter / 2d), point.Y - (diameter / 2d));
        }

        if (_radius > 130d)
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
            var diameter = body.Kind is CelestialBodyKind.Sun or CelestialBodyKind.Moon ? 30d : 9d;
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
                : new Ellipse { Width = diameter, Height = diameter, Fill = accent };
            var name = _strings.Translate($"CelestialBody{body.Kind}");
            UiLocalization.SetAccessibleLabel(dot, _strings.Format("Celestial.Sky.BodyPosition", name, body.AltitudeDegrees, body.AzimuthDegrees));
            Add(dot, point.X - (diameter / 2d), point.Y - (diameter / 2d));
            if (_radius > 80d)
            {
                Label(name, point.X + (diameter / 2d) + 5d, point.Y - 9d, Brush("TextFillColorPrimaryBrush"), 12d);
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

    private void Label(string text, double x, double y, Brush foreground, double size)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeights.Light,
            Foreground = foreground,
            MaxWidth = Math.Max(20d, SkyCanvas.Width - Math.Max(0d, x)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false
        };
        Add(label, Math.Clamp(x, 0d, Math.Max(0d, SkyCanvas.Width - 20d)), Math.Clamp(y, 0d, Math.Max(0d, SkyCanvas.Height - 18d)));
    }

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
