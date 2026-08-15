namespace TrackMeUp.Presentation;

/// <summary>Identifies one case-insensitive match inside OCR text.</summary>
public readonly record struct OcrTextMatch(int StartIndex, int Length);

/// <summary>Provides deterministic, presentation-only matching for OCR text.</summary>
public static class OcrTextSearch
{
    /// <summary>Finds non-overlapping matches from left to right once the query contains at least two characters.</summary>
    public static IReadOnlyList<OcrTextMatch> FindMatches(string text, string query)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length < 2 || text.Length < normalizedQuery.Length)
        {
            return [];
        }

        var matches = new List<OcrTextMatch>();
        var searchStart = 0;
        while (searchStart <= text.Length - normalizedQuery.Length)
        {
            var matchStart = text.IndexOf(normalizedQuery, searchStart, StringComparison.OrdinalIgnoreCase);
            if (matchStart < 0)
            {
                break;
            }

            matches.Add(new OcrTextMatch(matchStart, normalizedQuery.Length));
            searchStart = matchStart + normalizedQuery.Length;
        }

        return matches;
    }
}
