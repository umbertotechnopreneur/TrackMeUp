using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Presentation;

namespace TrackMeUp.Controls;

/// <summary>Passively renders safe metadata and Markdown blocks for the selected screenshot.</summary>
public sealed partial class ScreenshotDetailsControl : UserControl
{
    /// <summary>Creates the screenshot detail pane.</summary>
    public ScreenshotDetailsControl() => InitializeComponent();

    /// <summary>Replaces every displayed value with one immutable screenshot-detail projection.</summary>
    /// <param name="state">The selected screenshot details, or <see langword="null"/> when no screenshot is selected.</param>
    /// <param name="emptyAiDescriptionText">Localized contextual copy to show when the description has no rendered blocks.</param>
    public void Render(ScreenshotDetailsViewState? state, string emptyAiDescriptionText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyAiDescriptionText);

        CapturedAtSummaryText.Text = state is null ? "--" : $"{state.CapturedDate} · {state.CapturedTime}";
        ActivityIndexValueText.Text = state?.ActivityIndex ?? "--";
        ApplicationValueText.Text = state?.Application ?? "--";
        CaptureKindValueText.Text = state?.CaptureKind ?? "--";
        OriginValueText.Text = state?.Origin ?? "--";
        ActivityLabelsValueText.Text = state?.ActivityLabels ?? "--";
        AnalysisTimeValueText.Text = state?.AnalysisTime ?? "--";
        AnalysisTimePanel.Visibility = string.IsNullOrWhiteSpace(state?.AnalysisTime)
            ? Visibility.Collapsed
            : Visibility.Visible;

        AiMarkdownHost.Children.Clear();
        var blocks = state?.AiDescription ?? Array.Empty<SafeMarkdownBlock>();
        NoAiDescriptionText.Text = emptyAiDescriptionText;
        NoAiDescriptionText.Visibility = blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var block in blocks)
        {
            AiMarkdownHost.Children.Add(CreateMarkdownBlock(block));
        }
    }

    private static TextBlock CreateMarkdownBlock(SafeMarkdownBlock block)
    {
        var isHeading = block.Kind == SafeMarkdownBlockKind.Heading;
        var prefix = block.Kind is SafeMarkdownBlockKind.Bullet or SafeMarkdownBlockKind.Numbered
            ? $"{block.Marker}  "
            : string.Empty;
        return new TextBlock
        {
            Margin = block.Kind is SafeMarkdownBlockKind.Bullet or SafeMarkdownBlockKind.Numbered
                ? new Thickness(4, 0, 0, 0)
                : new Thickness(0),
            FontSize = isHeading ? 15 : 13,
            FontWeight = isHeading ? FontWeights.SemiBold : FontWeights.Normal,
            IsTextSelectionEnabled = true,
            Opacity = isHeading ? 1d : 0.82d,
            Text = prefix + block.Text,
            TextWrapping = TextWrapping.Wrap
        };
    }
}
