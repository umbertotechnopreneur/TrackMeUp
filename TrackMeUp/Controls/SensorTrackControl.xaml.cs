// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TrackMeUp.Presentation;
using TrackMeUp.Services;
using Windows.Foundation;

namespace TrackMeUp.Controls;

/// <summary>Renders a responsive device track from supplied readings and bounded graph points.</summary>
public sealed partial class SensorTrackControl : UserControl
{
    private IReadOnlyList<SensorTracePoint> _points = [];
    private DateTimeOffset _now;
    private SensorMonitorRow? _row;
    private SolidColorBrush _accent = new(Microsoft.UI.Colors.CornflowerBlue);

    /// <summary>Creates a passive sensor track.</summary>
    public SensorTrackControl() => InitializeComponent();

    /// <summary>Renders supplied data without accessing hardware or changing the sampling cadence.</summary>
    public void Apply(SensorMonitorRow row, IReadOnlyList<SensorTracePoint> points, DateTimeOffset now, LocalizationService strings)
    {
        _row = row;
        _points = points;
        _now = now;
        _accent = (SolidColorBrush)Resources["Sensor" + row.Category + "Brush"];
        DeviceIcon.Foreground = _accent;
        LevelBarFill.Background = _accent;
        DeviceIcon.Glyph = row.Category switch { "Cpu" => "\uE950", "Memory" => "\uE964", "Gpu" => "\uE7F4", "Storage" => "\uEDA2", _ => "\uE850" };
        CategoryText.Text = strings.Translate("Hardware.Category." + row.Category).ToUpper(strings.Culture);
        NameText.Text = row.Name;
        ValueText.Text = row.Value;
        CapacityText.Text = row.CapacityText;
        SecondaryText.Text = row.SecondaryValue;
        NoTraceText.Text = row.Percent is null ? strings.Translate("Common.NotAvailable") : strings.Translate("Sensors.Collecting");
        var updated = string.Format(strings.Culture, strings.Translate("Hardware.DeviceUpdated"), row.SampledAt.ToLocalTime().ToString("T", strings.Culture));
        var description = string.Join(" · ", new[] { CategoryText.Text, row.Name, row.Value, row.Temperature, row.Details, updated }.Where(text => text.Length > 0));
        AutomationProperties.SetName(this, description);
        ToolTipService.SetToolTip(this, description + "\n" + row.Source);
        ArrangeTrack();
        DrawTrace();
    }

    private void Track_SizeChanged(object sender, SizeChangedEventArgs e) => ArrangeTrack();

    private void ArrangeTrack()
    {
        if (_row is not { } row) return;
        var battery = row.Category == "Battery";
        var capacity = !battery && row.CapacityPercent.HasValue;
        var layout = SensorMonitorLayout.ResolveTrack(Track.ActualWidth, Track.ActualHeight, capacity, row.SecondaryValue.Length > 0);
        Track.Padding = new Thickness(0, layout.Padding, 0, layout.Padding);
        Track.ColumnSpacing = layout.Dense ? 8 : 14;
        Track.RowSpacing = layout.Stacked ? 4 : 0;
        IconColumn.Width = new GridLength(layout.Dense ? 22 : 32);
        NameColumn.Width = layout.Stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(layout.Dense ? 76 : 155);
        ValueColumn.Width = new GridLength(layout.Dense ? 78 : 108);
        ChartColumn.Width = layout.Stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        HeaderRow.Height = layout.Stacked ? new GridLength(52) : new GridLength(1, GridUnitType.Star);
        GraphRow.Height = layout.Stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetRow(GraphPanel, layout.Stacked ? 1 : 0);
        Grid.SetColumn(GraphPanel, layout.Stacked ? 1 : 3);
        Grid.SetColumnSpan(GraphPanel, layout.Stacked ? 3 : 1);
        Grid.SetColumnSpan(Values, layout.Stacked ? 2 : 1);
        Values.HorizontalAlignment = layout.Stacked ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        DeviceIcon.FontSize = layout.Dense ? 20 : 27;
        CategoryText.FontSize = layout.Dense ? 10 : 12;
        CategoryText.CharacterSpacing = layout.Dense ? 0 : 80;
        NameText.Visibility = layout.Dense ? Visibility.Collapsed : Visibility.Visible;
        ValueText.FontSize = layout.Dense ? 20 : layout.Stacked ? 26 : 28;

        // Secondary readings stay beside the value when the row cannot also fit a chart caption.
        var showCapacityCaption = capacity && layout.ShowCapacityCaption;
        MetricDetailText.Text = row.HasTemperature ? row.TemperatureValue
            : capacity && !showCapacityCaption ? row.CapacityText
            : row.SecondaryValue;
        MetricDetailText.Visibility = !layout.Dense && MetricDetailText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        CapacityText.Visibility = showCapacityCaption ? Visibility.Visible : Visibility.Collapsed;
        SecondaryText.Visibility = ((!layout.Dense && layout.ShowSecondary && row.HasTemperature) || (battery && !layout.Dense))
            && row.SecondaryValue.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (battery) MetricDetailText.Visibility = row.HasTemperature && !layout.Dense ? Visibility.Visible : Visibility.Collapsed;
        TraceHost.Visibility = battery ? Visibility.Collapsed : Visibility.Visible;
        TraceHost.Height = layout.GraphHeight;
        LevelBarTrack.Visibility = (battery && row.Percent.HasValue) || capacity ? Visibility.Visible : Visibility.Collapsed;
        LevelBarTrack.Height = battery ? 9 : 6;
        UpdateLevelBar();
    }

    private void LevelBarTrack_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateLevelBar();

    private void UpdateLevelBar() => LevelBarFill.Width = LevelBarTrack.ActualWidth
        * (_row?.Category == "Battery" ? _row.Percent ?? 0 : _row?.CapacityPercent ?? 0) / 100;

    private void TraceCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawTrace();

    private void DrawTrace()
    {
        TraceCanvas.Children.Clear();
        var width = TraceCanvas.ActualWidth;
        var height = TraceCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;
        TraceCanvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
        NoTraceText.Visibility = _points.Count(point => point.Value.HasValue) < 2 ? Visibility.Visible : Visibility.Collapsed;
        var segment = new List<Point>();
        foreach (var sample in _points)
        {
            // Missing readings split both the line and its fill; a gap must never look measured.
            if (sample.Value is not { } value) { DrawSegment(segment, height); segment.Clear(); continue; }
            segment.Add(new Point(Math.Clamp(1 - (_now - sample.Timestamp).TotalSeconds / 120, 0, 1) * width,
                (1 - value / 100) * Math.Max(0, height - 4) + 2));
        }
        DrawSegment(segment, height);
    }

    private void DrawSegment(IReadOnlyList<Point> points, double height)
    {
        if (points.Count < 2) return;
        var color = _accent.Color;
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        gradient.GradientStops.Add(new GradientStop { Offset = 0, Color = Windows.UI.Color.FromArgb(110, color.R, color.G, color.B) });
        gradient.GradientStops.Add(new GradientStop { Offset = 1, Color = Windows.UI.Color.FromArgb(0, color.R, color.G, color.B) });
        var fill = new Polygon { Fill = gradient };
        fill.Points.Add(new Point(points[0].X, height));
        var line = new Polyline { Stroke = _accent, StrokeThickness = 1.7, StrokeLineJoin = PenLineJoin.Round };
        foreach (var point in points) { fill.Points.Add(point); line.Points.Add(point); }
        fill.Points.Add(new Point(points[^1].X, height));
        TraceCanvas.Children.Add(fill);
        TraceCanvas.Children.Add(line);
    }
}
