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

        IsSearching = true;
        try
        {
            var response = await _application.SearchAsync(
                new SearchRequest
                {
                    Text = text.Trim(),
                    Kinds = ImmutableHashSet.Create(StringComparer.Ordinal, "screenshot"),
                    IncludeTextContent = false,
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
                .Select(hit => Project(hit, culture))
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

    private static ScreenshotSearchResult Project(SearchHit hit, CultureInfo culture)
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
            hit.Score);
    }
}
