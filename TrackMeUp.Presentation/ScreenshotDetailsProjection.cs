using System.Globalization;
using System.Text.RegularExpressions;
using TrackMeUp.Application;

namespace TrackMeUp.Presentation;

/// <summary>Identifies one safe, presentation-neutral Markdown block.</summary>
public enum SafeMarkdownBlockKind
{
    /// <summary>Displays a short section heading.</summary>
    Heading,

    /// <summary>Displays a regular paragraph.</summary>
    Paragraph,

    /// <summary>Displays an unordered-list item.</summary>
    Bullet,

    /// <summary>Displays an ordered-list item.</summary>
    Numbered
}

/// <summary>Contains plain text extracted from one supported Markdown block.</summary>
public sealed record SafeMarkdownBlock(SafeMarkdownBlockKind Kind, string Text, string? Marker = null);

/// <summary>Contains already-formatted values rendered by the passive screenshot details pane.</summary>
public sealed record ScreenshotDetailsViewState(
    string CapturedDate,
    string CapturedTime,
    string Application,
    string CaptureKind,
    string Origin,
    string ActivityIndex,
    string ActivityLabels,
    string? AnalysisTime,
    IReadOnlyList<SafeMarkdownBlock> AiDescription);

/// <summary>Builds safe screenshot-detail and Markdown projections without presentation-framework dependencies.</summary>
public static partial class ScreenshotDetailsProjection
{
    private const int MaximumMarkdownCharacters = 12_000;
    private const int MaximumMarkdownBlocks = 80;

    /// <summary>Formats one screenshot DTO for the current UI culture.</summary>
    /// <param name="item">Presentation-neutral screenshot data.</param>
    /// <param name="culture">Culture used to format dates and numbers.</param>
    /// <param name="localizedCaptureKind">Localized label for the stable capture-kind identifier.</param>
    /// <param name="localizedOrigin">Localized label for the stable capture-origin identifier.</param>
    /// <param name="missingValue">Placeholder rendered for unavailable optional values.</param>
    public static ScreenshotDetailsViewState Create(
        ScreenshotGalleryItem item,
        CultureInfo culture,
        string localizedCaptureKind,
        string localizedOrigin,
        string missingValue)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(localizedCaptureKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(localizedOrigin);
        ArgumentException.ThrowIfNullOrWhiteSpace(missingValue);

        var localTime = item.CapturedAt.ToLocalTime();
        var labels = item.SpanLabels is { Count: > 0 }
            ? string.Join("  ·  ", item.SpanLabels.Select(label => label.Label))
            : missingValue;
        return new ScreenshotDetailsViewState(
            localTime.ToString(culture.DateTimeFormat.LongDatePattern, culture),
            localTime.ToString("T", culture),
            string.IsNullOrWhiteSpace(item.ForegroundApplication) ? missingValue : item.ForegroundApplication,
            localizedCaptureKind,
            localizedOrigin,
            item.ActivityIndex is { } index ? index.ToString(culture) : missingValue,
            labels,
            item.AiAnalyzedAt?.ToLocalTime().ToString("g", culture),
            ParseMarkdown(item.AiDescriptionMarkdown));
    }

    /// <summary>
    /// Parses a bounded Markdown subset into inert text blocks; URLs and HTML are never made interactive.
    /// </summary>
    public static IReadOnlyList<SafeMarkdownBlock> ParseMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<SafeMarkdownBlock>();
        }

        var bounded = markdown.Length <= MaximumMarkdownCharacters
            ? markdown
            : string.Concat(markdown.AsSpan(0, MaximumMarkdownCharacters), "…");
        var result = new List<SafeMarkdownBlock>();
        var paragraph = new List<string>();

        foreach (var rawLine in bounded.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                FlushParagraph(result, paragraph);
                continue;
            }

            var heading = HeadingPattern().Match(line);
            if (heading.Success)
            {
                FlushParagraph(result, paragraph);
                AddBlock(result, new SafeMarkdownBlock(SafeMarkdownBlockKind.Heading, CleanInline(heading.Groups[1].Value)));
                continue;
            }

            var bullet = BulletPattern().Match(line);
            if (bullet.Success)
            {
                FlushParagraph(result, paragraph);
                AddBlock(result, new SafeMarkdownBlock(SafeMarkdownBlockKind.Bullet, CleanInline(bullet.Groups[1].Value), "•"));
                continue;
            }

            var numbered = NumberedPattern().Match(line);
            if (numbered.Success)
            {
                FlushParagraph(result, paragraph);
                AddBlock(result, new SafeMarkdownBlock(
                    SafeMarkdownBlockKind.Numbered,
                    CleanInline(numbered.Groups[2].Value),
                    numbered.Groups[1].Value + "."));
                continue;
            }

            paragraph.Add(CleanInline(line));
        }

        FlushParagraph(result, paragraph);
        return result;
    }

    private static void FlushParagraph(List<SafeMarkdownBlock> result, List<string> paragraph)
    {
        if (paragraph.Count == 0)
        {
            return;
        }

        AddBlock(result, new SafeMarkdownBlock(SafeMarkdownBlockKind.Paragraph, string.Join(' ', paragraph)));
        paragraph.Clear();
    }

    private static void AddBlock(List<SafeMarkdownBlock> result, SafeMarkdownBlock block)
    {
        if (result.Count >= MaximumMarkdownBlocks || string.IsNullOrWhiteSpace(block.Text))
        {
            return;
        }

        result.Add(block);
    }

    private static string CleanInline(string value)
    {
        // Generated Markdown is rendered as inert text: link targets and HTML tags are discarded by design.
        var text = MarkdownImageOrLinkPattern().Replace(value, match => match.Groups[1].Value);
        text = HtmlTagPattern().Replace(text, string.Empty);
        return text
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("~~", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    [GeneratedRegex(@"^#{1,3}\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^[-+*]\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^(\d+)[.)]\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedPattern();

    [GeneratedRegex(@"!?\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownImageOrLinkPattern();

    [GeneratedRegex(@"</?[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagPattern();
}
