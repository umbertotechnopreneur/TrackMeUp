using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using Windows.Foundation;
using Windows.UI;

namespace TrackMeUp.Controls;

/// <summary>Passively renders one local screenshot match and highlights the literal query passage.</summary>
public sealed partial class SearchResultItemControl : UserControl
{
    private const float RestingThumbnailElevation = 4f;
    private const float HoverThumbnailElevation = 18f;

    /// <summary>Identifies the immutable result rendered by this control.</summary>
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(
        nameof(Result),
        typeof(ScreenshotSearchResult),
        typeof(SearchResultItemControl),
        new PropertyMetadata(null, OnResultChanged));

    /// <summary>Creates an empty screenshot-result renderer.</summary>
    public SearchResultItemControl()
    {
        InitializeComponent();
        SetThumbnailElevation(RestingThumbnailElevation);
    }

    /// <summary>Gets or sets the immutable result rendered by the control.</summary>
    public ScreenshotSearchResult? Result
    {
        get => (ScreenshotSearchResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    private static void OnResultChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (SearchResultItemControl)dependencyObject;
        var result = (ScreenshotSearchResult?)args.NewValue;
        control.DataContext = result;
        control.ApplyMatchScoreStyle(result?.MatchPercent ?? 0);
        control.RenderInstallation(result);
        control.RenderSnippet(result);
    }

    private void RenderInstallation(ScreenshotSearchResult? result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.InstallationName))
        {
            InstallationSourcePanel.Visibility = Visibility.Collapsed;
            InstallationSourceSeparator.Visibility = Visibility.Collapsed;
            return;
        }

        if (!InstallationProfileCatalog.Colors.Contains(result.InstallationColor, StringComparer.Ordinal)
            || !InstallationProfileCatalog.Icons.Contains(result.InstallationIcon, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Search result installation appearance is invalid.");
        }

        InstallationSourceBadge.Background = InstallationAppearance.CreateAccentBrush(result.InstallationColor!);
        InstallationSourceIcon.Glyph = InstallationAppearance.GetIconGlyph(result.InstallationIcon!);
        InstallationSourceText.Text = result.InstallationName;
        var accessibleName = result.InstallationDisplay;
        AutomationProperties.SetName(InstallationSourcePanel, accessibleName);
        ToolTipService.SetToolTip(InstallationSourcePanel, accessibleName);
        InstallationSourcePanel.Visibility = Visibility.Visible;
        InstallationSourceSeparator.Visibility = Visibility.Visible;
    }

    private void ApplyMatchScoreStyle(int matchPercent)
    {
        var position = Math.Clamp(matchPercent, 0, 100) / 100d;
        var center = SemanticScoreColor(position);
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        gradient.GradientStops.Add(new GradientStop
        {
            Color = WithAlpha(SemanticScoreColor(Math.Max(0d, position - 0.18d)), 48),
            Offset = 0d
        });
        gradient.GradientStops.Add(new GradientStop
        {
            Color = WithAlpha(center, 68),
            Offset = 0.5d
        });
        gradient.GradientStops.Add(new GradientStop
        {
            Color = WithAlpha(SemanticScoreColor(Math.Min(1d, position + 0.18d)), 48),
            Offset = 1d
        });
        MatchScoreChip.Background = gradient;
        MatchScoreChip.BorderBrush = new SolidColorBrush(WithAlpha(center, 170));
    }

    private static Color SemanticScoreColor(double position)
    {
        var normalized = Math.Clamp(position, 0d, 1d);
        if (normalized <= 0.5d)
        {
            return Blend(Color.FromArgb(255, 220, 76, 62), Color.FromArgb(255, 245, 191, 66), SmoothStep(normalized * 2d));
        }

        return Blend(Color.FromArgb(255, 245, 191, 66), Color.FromArgb(255, 66, 173, 103), SmoothStep((normalized - 0.5d) * 2d));
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        from.A,
        (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
        (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
        (byte)Math.Round(from.B + ((to.B - from.B) * amount)));

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static double SmoothStep(double value)
    {
        var normalized = Math.Clamp(value, 0d, 1d);
        return normalized * normalized * (3d - (2d * normalized));
    }

    private void RenderSnippet(ScreenshotSearchResult? result)
    {
        SnippetText.TextHighlighters.Clear();
        SnippetText.Text = result?.TextSnippet ?? string.Empty;
        if (result is null || string.IsNullOrWhiteSpace(result.Query))
        {
            return;
        }

        var highlighter = new TextHighlighter
        {
            Background = (Brush)Resources["SearchResultHighlightBrush"],
            Foreground = (Brush)Resources["SearchResultHighlightTextBrush"]
        };
        var matchStart = 0;
        while ((matchStart = result.TextSnippet.IndexOf(result.Query, matchStart, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            highlighter.Ranges.Add(new TextRange
            {
                StartIndex = matchStart,
                Length = result.Query.Length
            });
            matchStart += result.Query.Length;
        }

        if (highlighter.Ranges.Count > 0)
        {
            SnippetText.TextHighlighters.Add(highlighter);
        }
    }

    private void SnapshotThumbnailFrame_PointerEntered(object sender, PointerRoutedEventArgs args) =>
        SetThumbnailElevation(HoverThumbnailElevation);

    private void SnapshotThumbnailFrame_PointerExited(object sender, PointerRoutedEventArgs args) =>
        SetThumbnailElevation(RestingThumbnailElevation);

    private void SetThumbnailElevation(float elevation) =>
        SnapshotThumbnailFrame.Translation = new Vector3(0f, 0f, elevation);
}
