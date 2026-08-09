using System;
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
        var item = Assert.Single(viewModel.Results);
        Assert.Equal(@"C:\captures\meeting.png", item.ScreenshotPath);
        Assert.StartsWith("file:///C:/captures/meeting.png", item.ScreenshotUri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", item.CapturedAtDisplay, StringComparison.Ordinal);
        Assert.Equal(23, viewModel.TotalCount);
    }

    public class SearchApplicationProxy : DispatchProxy
    {
        public SearchRequest? Request { get; private set; }

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
                                Id = "screenshot:meeting",
                                Kind = "screenshot",
                                Timestamp = new DateTimeOffset(2026, 8, 9, 9, 30, 0, TimeSpan.Zero),
                                Application = "Teams",
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

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
