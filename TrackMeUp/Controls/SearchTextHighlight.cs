// SPDX-License-Identifier: MIT

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TrackMeUp.Controls;

/// <summary>Renders literal, case-insensitive query matches in the fixed-light search surface.</summary>
internal static class SearchTextHighlight
{
    /// <summary>Replaces inert text and its highlights so recycled rows never retain another result's ranges.</summary>
    internal static void Apply(TextBlock target, string? text, string? query)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.TextHighlighters.Clear();
        target.Text = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(text))
        {
            return;
        }

        var highlighter = new TextHighlighter
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 255, 246, 183)),
            Foreground = new SolidColorBrush(Color.FromArgb(255, 32, 32, 32))
        };
        var start = 0;
        while ((start = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            highlighter.Ranges.Add(new TextRange { StartIndex = start, Length = query.Length });
            start += query.Length;
        }

        if (highlighter.Ranges.Count > 0)
        {
            target.TextHighlighters.Add(highlighter);
        }
    }
}