using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class PresentationBoundaryTests
{
    [Fact]
    public void PresentationAssembly_DoesNotReferenceWinUiOrSpectre()
    {
        var references = typeof(TrackMeUp.Presentation.MainViewModel).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

        Assert.DoesNotContain(references, name => name?.Contains("WinUI", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(references, name => name?.Contains("Spectre", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ReportViewModel_DelegatesTypedQueryToSharedFacade()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, RecordingApplicationProxy>();
        var recorder = (RecordingApplicationProxy)(object)application;
        var viewModel = new ReportViewModel(application);
        var query = new ReportQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), string.Empty, ReportView.HourOfWeek);

        var result = await viewModel.LoadAsync(query, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Same(query, recorder.ReportQuery);
        Assert.Same(query, viewModel.Query);
        Assert.Same(result.Value, viewModel.Snapshot);
        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorCode);
    }

    public class RecordingApplicationProxy : DispatchProxy
    {
        public ReportQuery? ReportQuery { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITrackMeUpApplication.GetReportAsync))
            {
                ReportQuery = Assert.IsType<ReportQuery>(args![0]);
                var snapshot = new ReportSnapshot(
                    2,
                    new ReportRange(ReportQuery.From, ReportQuery.ToInclusive, "SE Asia Standard Time", 5),
                    new ReportTotals(0, 0, 0, 0, 0, 0),
                    [],
                    [],
                    [],
                    [],
                    new ReportDataQuality(false, null, null, 0, 0, 432000, 0),
                    AiUsageSummary.Empty);
                return Task.FromResult(OperationResult<ReportSnapshot>.Success("report.query.ok", "ReportQueryOk", snapshot));
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
