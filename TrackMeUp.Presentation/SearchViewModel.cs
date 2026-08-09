using System.Collections.Immutable;
using System.Globalization;
using TrackMeUp.Application;
using TrackMeUp.Search;

namespace TrackMeUp.Presentation;

/// <summary>Represents one screenshot match rendered by the local-search surface.</summary>
public sealed record ScreenshotSearchResult(
    string ScreenshotPath,
    string ScreenshotUri,
    DateTimeOffset CapturedAt,
    string CapturedAtDisplay,
    string Application,
    string WindowTitle,
    string TextSnippet,
    float Score);

/// <summary>Executes bounded screenshot queries through the shared application facade.</summary>
public sealed class SearchViewModel : ViewModelBase
{
    private readonly ITrackMeUpApplication _application;
    private IReadOnlyList<ScreenshotSearchResult> _results = Array.Empty<ScreenshotSearchResult>();
    private bool _isSearching;
    private int _totalCount;

    /// <summary>Gets the maximum number of screenshot rows rendered by the floating window.</summary>
    public const int MaximumResults = 20;

    /// <summary>Gets the minimum query length used by the live search surface.</summary>
    public const int MinimumQueryLength = 3;

    /// <summary>Gets the maximum number of type-ahead suggestions shown by the search surface.</summary>
    public const int MaximumSuggestions = 8;

    /// <summary>Creates a local-search presentation model.</summary>
    public SearchViewModel(ITrackMeUpApplication application) =>
        _application = application ?? throw new ArgumentNullException(nameof(application));

    /// <summary>Gets the current bounded screenshot result list.</summary>
    public IReadOnlyList<ScreenshotSearchResult> Results
    {
        get => _results;
        private set => Set(ref _results, value);
    }

    /// <summary>Gets whether a local query is currently running.</summary>
    public bool IsSearching
    {
        get => _isSearching;
        private set => Set(ref _isSearching, value);
    }

    /// <summary>Gets the total match count before the 20-result window limit.</summary>
    public int TotalCount
    {
        get => _totalCount;
        private set => Set(ref _totalCount, value);
    }

    /// <summary>Clears the current query results without accessing application state.</summary>
    public void Clear()
    {
        Results = Array.Empty<ScreenshotSearchResult>();
        TotalCount = 0;
    }

    /// <summary>Searches retained screenshots and projects the bounded response for WinUI.</summary>
    public async Task<OperationResult<IReadOnlyList<ScreenshotSearchResult>>> SearchAsync(
        string text,
        CultureInfo culture,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (string.IsNullOrWhiteSpace(text))
        {
            Clear();
            return OperationResult<IReadOnlyList<ScreenshotSearchResult>>.Success(
                "search.query.cleared",
                "SearchQueryCleared",
                Results);
        }

        var query = text.Trim();
        if (query.Length < MinimumQueryLength)
        {
            Clear();
            return OperationResult<IReadOnlyList<ScreenshotSearchResult>>.Success(
                "search.query.too-short",
                "SearchQueryTooShort",
                Results);
        }

        IsSearching = true;
        try
        {
            var response = await _application.SearchAsync(
                new SearchRequest
                {
                    Text = query,
                    Kinds = ImmutableHashSet.Create(StringComparer.Ordinal, "screenshot"),
                    IncludeTextContent = true,
                    Offset = 0,
                    Limit = MaximumResults
                },
                cancellationToken);
            if (!response.Succeeded || response.Value is null)
            {
                Results = Array.Empty<ScreenshotSearchResult>();
                TotalCount = 0;
                return new OperationResult<IReadOnlyList<ScreenshotSearchResult>>(
                    false,
                    response.Code,
                    response.MessageKey,
                    null,
                    response.Issues);
            }

            var projected = response.Value.Hits
                .Select(hit => Project(hit, culture, query))
                .ToArray();
            if (projected.Length > MaximumResults)
            {
                throw new InvalidDataException("The application returned more screenshot search results than requested.");
            }

            Results = projected;
            TotalCount = response.Value.TotalCount;
            return OperationResult<IReadOnlyList<ScreenshotSearchResult>>.Success(
                "search.query.completed",
                "SearchQueryCompleted",
                projected);
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>Gets prefix and infix suggestions from the local suggestion index.</summary>
    public async Task<OperationResult<IReadOnlyList<string>>> SuggestAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var query = text.Trim();
        if (query.Length < MinimumQueryLength)
        {
            return OperationResult<IReadOnlyList<string>>.Success(
                "search.suggestions.cleared",
                "SearchQueryCleared",
                Array.Empty<string>());
        }

        var response = await _application.GetSearchSuggestionsAsync(
            new SearchSuggestionRequest
            {
                Text = query,
                Limit = MaximumSuggestions
            },
            cancellationToken);
        return response;
    }

    private static ScreenshotSearchResult Project(SearchHit hit, CultureInfo culture, string query)
    {
        var document = hit.Document;
        if (!string.Equals(document.Kind, "screenshot", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(document.CapturePath)
            || !Uri.TryCreate(document.CapturePath, UriKind.Absolute, out var screenshotUri)
            || !screenshotUri.IsFile)
        {
            throw new InvalidDataException("A screenshot search hit must contain an absolute local capture path.");
        }

        return new ScreenshotSearchResult(
            document.CapturePath,
            screenshotUri.AbsoluteUri,
            document.Timestamp,
            document.Timestamp.ToLocalTime().ToString("g", culture),
            string.IsNullOrWhiteSpace(document.Application) ? "TrackMeUp" : document.Application,
            string.IsNullOrWhiteSpace(document.WindowTitle) ? "—" : document.WindowTitle,
            BuildSnippet(document, query),
            hit.Score);
    }

    private static string BuildSnippet(SearchDocument document, string query)
    {
        var candidates = new[]
        {
            document.OcrCorrectedText,
            document.OcrRawText,
            document.OcrStructuredSummary,
            document.AiDescription,
            document.Context,
            document.WindowTitle
        };
        var source = candidates.FirstOrDefault(value =>
            !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            ?? candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrWhiteSpace(source))
        {
            return "—";
        }

        var compact = string.Join(' ', source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var matchIndex = compact.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0 || compact.Length <= 180)
        {
            return compact.Length <= 180 ? compact : $"{compact[..180].TrimEnd()}…";
        }

        const int radius = 78;
        var start = Math.Max(0, matchIndex - radius);
        var length = Math.Min(180, compact.Length - start);
        var snippet = compact.Substring(start, length).Trim();
        return $"{(start > 0 ? "…" : string.Empty)}{snippet}{(start + length < compact.Length ? "…" : string.Empty)}";
    }
}
