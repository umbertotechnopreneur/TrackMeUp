using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Presentation;

namespace TrackMeUp.Controls;

/// <summary>Passively renders one local screenshot match and highlights the literal query passage.</summary>
public sealed partial class SearchResultItemControl : UserControl
{
    /// <summary>Identifies the immutable result rendered by this control.</summary>
    public static readonly DependencyProperty ResultProperty = DependencyProperty.Register(
        nameof(Result),
        typeof(ScreenshotSearchResult),
        typeof(SearchResultItemControl),
        new PropertyMetadata(null, OnResultChanged));

    /// <summary>Creates an empty screenshot-result renderer.</summary>
    public SearchResultItemControl() => InitializeComponent();

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
        control.RenderSnippet(result);
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
}
