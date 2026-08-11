using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Presentation;
using TrackMeUp.Runtime;
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

    [Fact]
    public async Task MainViewModel_StartsTrackingFromThePersistedLaunchPreferenceWithoutToggling()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, StartupRecordingApplicationProxy>();
        var recorder = (StartupRecordingApplicationProxy)(object)application;
        var viewModel = new MainViewModel(application);

        var result = await viewModel.InitializeAsync(LaunchOptions.Parse(Array.Empty<string>()), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Value?.Dashboard.IsTracking);
        Assert.Same(recorder.LastSession, result.Value?.LastSession);
        Assert.Same(recorder.LastSession, viewModel.LastSession);
        Assert.Equal(1, recorder.StartCalls);
        Assert.Equal(0, recorder.ToggleCalls);
        Assert.Equal("winui.launch", recorder.LastStartRequest?.Source);
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

    public class StartupRecordingApplicationProxy : DispatchProxy
    {
        public int StartCalls { get; private set; }

        public int ToggleCalls { get; private set; }

        public StartTrackingRequest? LastStartRequest { get; private set; }

        public LastSessionState LastSession { get; } = new(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            "Editor",
            "Document",
            "test-installation",
            null,
            @"C:\captures\latest.webp",
            DateTimeOffset.UtcNow);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "add_RuntimeStateChanged":
                case "remove_RuntimeStateChanged":
                    return null;
                case nameof(ITrackMeUpApplication.GetSettingsAsync):
                    return Task.FromResult(OperationResult<AppSettings>.Success(
                        "settings.loaded",
                        "SettingsLoaded",
                        new AppSettings(StartTrackingOnLaunch: true)));
                case nameof(ITrackMeUpApplication.StartTrackingAsync):
                    StartCalls++;
                    LastStartRequest = Assert.IsType<StartTrackingRequest>(args![0]);
                    return Task.FromResult(OperationResult<DashboardState>.Success(
                        "tracking.started",
                        "TrackingStarted",
                        Dashboard(isTracking: true)));
                case nameof(ITrackMeUpApplication.GetLastSessionAsync):
                    return Task.FromResult(OperationResult<LastSessionState?>.Success(
                        "session.last.loaded",
                        "LastSessionLoaded",
                        LastSession));
                case nameof(ITrackMeUpApplication.ToggleTrackingAsync):
                    ToggleCalls++;
                    return Task.FromResult(OperationResult<DashboardState>.Success(
                        "tracking.toggled",
                        "TrackingToggled",
                        Dashboard(isTracking: false)));
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }

        private static DashboardState Dashboard(bool isTracking) => new(
            isTracking ? "RUNNING" : "PAUSED",
            "STATE_READY",
            0,
            0,
            0,
            0,
            isTracking,
            null,
            DateTimeOffset.Now,
            DateTimeOffset.UtcNow);
    }
}
