using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Search;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class SearchViewModelTests
{
    [Fact]
    public async Task SearchAsync_RequestsAtMostTwentyScreenshotsAndProjectsDateAndUri()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, SearchApplicationProxy>();
        var proxy = (SearchApplicationProxy)(object)application;
        var viewModel = new SearchViewModel(application);

        var result = await viewModel.SearchAsync("riunione", CultureInfo.GetCultureInfo("it-IT"), CancellationToken.None);

        Assert.True(result.Succeeded);
        var request = Assert.IsType<SearchRequest>(proxy.Request);
        Assert.Equal(SearchViewModel.MaximumResults, request.Limit);
        Assert.Equal(0, request.Offset);
        Assert.True(request.IncludeTextContent);
        Assert.Equal(["screenshot"], request.Kinds.ToArray());
        Assert.Equal(2, viewModel.Results.Count);
        var item = viewModel.Results[0];
        Assert.Equal(@"C:\captures\meeting.png", item.ScreenshotPath);
        Assert.StartsWith("file:///C:/captures/meeting.png", item.ScreenshotUri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", item.CapturedAtDisplay, StringComparison.Ordinal);
        Assert.Equal("Teams · Project review", item.ActiveWindowDisplay);
        Assert.Equal("riunione", item.Query);
        Assert.Contains("riunione", item.TextSnippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#", item.TextSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain("**", item.TextSnippet, StringComparison.Ordinal);
        Assert.Equal("42 clicks · CPU 37% · GPU 61%", item.ActivityDisplay);
        Assert.Equal(4.5f, item.Score);
        Assert.Equal(100, item.MatchPercent);
        Assert.Equal("100%", item.MatchPercentDisplay);
        Assert.Equal(2.25f, viewModel.Results[1].Score);
        Assert.Equal(50, viewModel.Results[1].MatchPercent);
        Assert.Equal(23, viewModel.TotalCount);
    }

    [Fact]
    public async Task SuggestAsync_ProjectsCleanMarkdownFreeTextAndWeightedConfidence()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, SearchApplicationProxy>();
        var proxy = (SearchApplicationProxy)(object)application;
        var viewModel = new SearchViewModel(application);

        var result = await viewModel.SuggestAsync("spo", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("spo", proxy.SuggestionRequest?.Text);
        Assert.Equal(SearchViewModel.MaximumSuggestions, proxy.SuggestionRequest?.Limit);
        Assert.Collection(
            result.Value!,
            suggestion =>
            {
                Assert.Equal("Spotify", suggestion.Text);
                Assert.Equal(99, suggestion.ConfidencePercent);
            },
            suggestion =>
            {
                Assert.Equal("The user is listening to Spotify while reviewing liked songs.", suggestion.Text);
                Assert.InRange(suggestion.ConfidencePercent, 55, 98);
            });
        Assert.All(result.Value!, suggestion => Assert.DoesNotContain("#", suggestion.Text, StringComparison.Ordinal));
    }

    public class SearchApplicationProxy : DispatchProxy
    {
        public SearchRequest? Request { get; private set; }

        public SearchSuggestionRequest? SuggestionRequest { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITrackMeUpApplication.SearchAsync))
            {
                Request = Assert.IsType<SearchRequest>(args![0]);
                var response = new SearchResponse
                {
                    Hits =
                    [
                        new SearchHit
                        {
                            Document = new SearchDocument
                            {
                                Id = "screenshot:lower-score",
                                Kind = "screenshot",
                                Timestamp = new DateTimeOffset(2026, 8, 9, 8, 30, 0, TimeSpan.Zero),
                                Application = "Outlook",
                                WindowTitle = "Planning",
                                OcrRawText = "Promemoria per la riunione settimanale",
                                AttributesRaw = ImmutableDictionary<string, string?>.Empty,
                                CapturePath = @"C:\captures\planning.png"
                            },
                            Score = 2.25f
                        },
                        new SearchHit
                        {
                            Document = new SearchDocument
                            {
                                Id = "screenshot:meeting",
                                Kind = "screenshot",
                                Timestamp = new DateTimeOffset(2026, 8, 9, 9, 30, 0, TimeSpan.Zero),
                                Application = "Teams",
                                WindowTitle = "Project review",
                                OcrRawText = "## Activity\n\nAppunti della **riunione** di progetto",
                                AttributesRaw = ImmutableDictionary<string, string?>.Empty
                                    .Add(SearchAttributeKeys.MouseClicks, "42")
                                    .Add(SearchAttributeKeys.CpuUsagePercent, "37")
                                    .Add(SearchAttributeKeys.GpuUsagePercent, "61"),
                                CapturePath = @"C:\captures\meeting.png"
                            },
                            Score = 4.5f
                        }
                    ],
                    TotalCount = 23,
                    Offset = 0
                };
                return Task.FromResult(OperationResult<SearchResponse>.Success(
                    "search.completed",
                    "SearchCompleted",
                    response));
            }

            if (targetMethod?.Name == nameof(ITrackMeUpApplication.GetSearchSuggestionsAsync))
            {
                SuggestionRequest = Assert.IsType<SearchSuggestionRequest>(args![0]);
                IReadOnlyList<SearchSuggestion> suggestions =
                [
                    new SearchSuggestion { Text = "Spotify", Weight = 8 },
                    new SearchSuggestion
                    {
                        Text = "## Activity\n\nThe user is listening to **Spotify** while reviewing [liked songs](https://example.test/private).",
                        Weight = 2
                    }
                ];
                return Task.FromResult(OperationResult<IReadOnlyList<SearchSuggestion>>.Success(
                    "search.suggestions.completed",
                    "SearchSuggestionsCompleted",
                    suggestions));
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
