using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrackMeUp.Presentation;

namespace TrackMeUp.Controls;

/// <summary>Passively renders safe metadata and Markdown blocks for the selected screenshot.</summary>
public sealed partial class ScreenshotDetailsControl : UserControl
{
    private string? _ocrText;

    /// <summary>Creates the screenshot detail pane.</summary>
    public ScreenshotDetailsControl() => InitializeComponent();

    /// <summary>Raised when the user requests the OCR text currently rendered by this passive detail pane.</summary>
    public event Action<string>? OcrTextRequested;

    /// <summary>Replaces every displayed value with one immutable screenshot-detail projection.</summary>
    /// <param name="state">The selected screenshot details, or <see langword="null"/> when no screenshot is selected.</param>
    /// <param name="emptyAiDescriptionText">Localized contextual copy to show when the description has no rendered blocks.</param>
    /// <param name="privacyStatusText">Localized application-wide privacy-filter status.</param>
    public void Render(
        ScreenshotDetailsViewState? state,
        string emptyAiDescriptionText,
        string privacyStatusText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyAiDescriptionText);
        ArgumentException.ThrowIfNullOrWhiteSpace(privacyStatusText);

        CapturedAtSummaryText.Text = state is null ? "--" : $"{state.CapturedDate} · {state.CapturedTime}";
        ActivityIndexValueText.Text = state?.ActivityIndex ?? "--";
        ApplicationValueText.Text = state?.Application ?? "--";
        WindowTitleValueText.Text = state?.WindowTitle ?? "--";
        ScreenValueText.Text = state?.Screen ?? "--";
        CaptureKindValueText.Text = state?.CaptureKind ?? "--";
        OriginValueText.Text = state?.Origin ?? "--";
        ActivityLabelsValueText.Text = state?.ActivityLabels ?? "--";
        MouseClicksValueText.Text = state?.MouseClicks ?? "--";
        CpuUsageValueText.Text = state?.CpuUsage ?? "--";
        GpuUsageValueText.Text = state?.GpuUsage ?? "--";
        PrivacyStatusValueText.Text = privacyStatusText;
        AnalysisTimeValueText.Text = state?.AnalysisTime ?? "--";
        AnalysisTimePanel.Visibility = string.IsNullOrWhiteSpace(state?.AnalysisTime)
            ? Visibility.Collapsed
            : Visibility.Visible;
        _ocrText = string.IsNullOrWhiteSpace(state?.OcrText) ? null : state.OcrText;
        OcrTextSection.Visibility = _ocrText is null ? Visibility.Collapsed : Visibility.Visible;
        OpenOcrTextButton.IsEnabled = _ocrText is not null;

        AiMarkdownHost.Children.Clear();
        var blocks = state?.AiDescription ?? Array.Empty<SafeMarkdownBlock>();
        NoAiDescriptionText.Text = emptyAiDescriptionText;
        NoAiDescriptionText.Visibility = blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var block in blocks)
        {
            AiMarkdownHost.Children.Add(CreateMarkdownBlock(block));
        }
    }

    private void OpenOcrTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_ocrText is { } ocrText)
        {
            OcrTextRequested?.Invoke(ocrText);
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
            FontSize = isHeading ? 16 : 14,
            FontWeight = isHeading ? FontWeights.SemiBold : FontWeights.Normal,
            IsTextSelectionEnabled = true,
            LineHeight = isHeading ? 22 : 20,
            Opacity = 1d,
            Text = prefix + block.Text,
            TextWrapping = TextWrapping.Wrap
        };
    }
}
