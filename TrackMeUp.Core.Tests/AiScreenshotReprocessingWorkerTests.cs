using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class AiScreenshotReprocessingWorkerTests
{
    private const string TestApiKeyVariable = "TRACKMEUP_OPENAI_APIKEY";

    [Fact]
    public async Task PreviewCountsScreensAndCapturesWithoutCallingProvider()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 1, screensPerCapture: 2);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: true);
            await using var application = CreateApplication(fixture.Store, analysis);

            var result = await PreviewTodayAsync(application);

            Assert.True(result.Succeeded);
            Assert.NotNull(result.Value);
            Assert.Equal(2, result.Value.MissingDescriptionScreenshotCount);
            Assert.Equal(1, result.Value.MissingDescriptionCaptureCount);
            Assert.Equal(2, result.Value.EligibleScreenshotCount);
            Assert.Equal(1, result.Value.EligibleCaptureCount);
            Assert.True(result.Value.CanStart);
            Assert.Null(result.Value.ActiveJobId);
            Assert.Equal(0, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task MultiMonitorCaptureWithPrivacyRulesIsMetadataBlockedAndRecheckedAtExecution()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 1, screensPerCapture: 2);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: true);
            await using var application = CreateApplication(fixture.Store, analysis);
            var initialPlan = await PreviewTodayAsync(application);
            Assert.Equal(1, initialPlan.Value!.EligibleCaptureCount);

            fixture.Store.SaveSettings(fixture.Store.LoadSettings() with
            {
                PrivacyWindowTitles = "rule|unrelated title"
            });
            var blockedPlan = await PreviewTodayAsync(application);
            Assert.Equal(1, blockedPlan.Value!.MissingMetadataCaptureCount);
            Assert.Equal(0, blockedPlan.Value.EligibleCaptureCount);
            Assert.False(blockedPlan.Value.CanStart);

            var start = await application.StartAiScreenshotReprocessingAsync(initialPlan.Value.PlanId, CancellationToken.None);
            Assert.True(start.Succeeded);
            var completed = await WaitForStatusAsync(
                application,
                start.Value!.JobId,
                AiScreenshotReprocessJobStatuses.CompletedWithErrors);

            Assert.Equal(1, completed.SkippedCaptures);
            Assert.Equal(0, completed.SucceededCaptures);
            Assert.Equal(0, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task StartReturnsImmediatelyAndPausePreventsTheNextCaptureFromStarting()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 2, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: false);
            await using var application = CreateApplication(fixture.Store, analysis);
            var preview = await PreviewTodayAsync(application);

            var stopwatch = Stopwatch.StartNew();
            var start = await application.StartAiScreenshotReprocessingAsync(preview.Value!.PlanId, CancellationToken.None);
            stopwatch.Stop();
            Assert.True(start.Succeeded);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
            await analysis.Entered.WaitAsync(TimeSpan.FromSeconds(5));

            var pause = await application.PauseAiScreenshotReprocessingAsync(start.Value!.JobId, CancellationToken.None);
            Assert.True(pause.Succeeded);
            Assert.Equal(AiScreenshotReprocessJobStatuses.PauseRequested, pause.Value!.Status);
            Assert.Equal(0, pause.Value.CompletedCaptures);
            Assert.Equal(2, pause.Value.RemainingCaptures);

            analysis.Release();
            var paused = await WaitForStatusAsync(
                application,
                start.Value.JobId,
                AiScreenshotReprocessJobStatuses.PausedByUser);
            Assert.Equal(1, paused.CompletedCaptures);
            Assert.Equal(1, paused.SucceededCaptures);
            Assert.Equal(1, paused.RemainingCaptures);
            Assert.Equal(1, analysis.HistoricalCallCount);

            var resume = await application.ResumeAiScreenshotReprocessingAsync(start.Value.JobId, CancellationToken.None);
            Assert.True(resume.Succeeded);
            var completed = await WaitForStatusAsync(
                application,
                start.Value.JobId,
                AiScreenshotReprocessJobStatuses.Completed);
            Assert.Equal(2, completed.CompletedScreenshots);
            Assert.Equal(2, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task FailedAttemptDoesNotBackfillBeyondFrozenDailyAllowance()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(
                directory,
                captureCount: 3,
                screensPerCapture: 1,
                dailyLimit: 1);
            var analysis = new ControlledHistoricalAnalysisService(
                fixture.Store.LoadSettings().InstallationId,
                initiallyReleased: true,
                failRequests: true);
            await using var application = CreateApplication(fixture.Store, analysis);
            var preview = await PreviewTodayAsync(application);

            Assert.Equal(3, preview.Value!.EligibleCaptureCount);
            Assert.Equal(1, preview.Value.ProcessableTodayCaptureCount);
            var start = await application.StartAiScreenshotReprocessingAsync(preview.Value.PlanId, CancellationToken.None);
            Assert.True(start.Succeeded);
            Assert.Equal(1, start.Value!.TotalCaptures);

            var completed = await WaitForStatusAsync(
                application,
                start.Value.JobId,
                AiScreenshotReprocessJobStatuses.CompletedWithErrors);
            Assert.Equal(1, completed.FailedCaptures);
            Assert.Equal(1, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task StartRejectsAPlanWhenAllowanceChangedAfterPreview()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(
                directory,
                captureCount: 3,
                screensPerCapture: 1,
                dailyLimit: 2);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: true);
            await using var application = CreateApplication(fixture.Store, analysis);
            var preview = await PreviewTodayAsync(application);
            Assert.Equal(2, preview.Value!.ProcessableTodayCaptureCount);

            AppendSuccessfulVisualUsage(fixture.Store);
            var start = await application.StartAiScreenshotReprocessingAsync(preview.Value.PlanId, CancellationToken.None);

            Assert.False(start.Succeeded);
            Assert.Equal("ai.screenshot_reprocess.plan.stale", start.Code);
            Assert.Null(fixture.Store.LoadActiveAiReprocessJob());
            Assert.Equal(0, analysis.HistoricalCallCount);

            var refreshed = await PreviewTodayAsync(application);
            Assert.Equal(1, refreshed.Value!.ProcessableTodayCaptureCount);
        });
    }

    [Fact]
    public async Task PrivacyMutationWinsTheBoundaryBeforeTheNextBatchItem()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 2, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: false);
            await using var application = CreateApplication(fixture.Store, analysis);
            var preview = await PreviewTodayAsync(application);
            var start = await application.StartAiScreenshotReprocessingAsync(preview.Value!.PlanId, CancellationToken.None);
            await analysis.Entered.WaitAsync(TimeSpan.FromSeconds(5));

            var mutation = application.AddPrivacyRuleAsync("process", "devenv", CancellationToken.None);
            Assert.False(mutation.IsCompleted);
            analysis.Release();
            var mutationResult = await mutation.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(mutationResult.Succeeded);

            var completed = await WaitForStatusAsync(
                application,
                start.Value!.JobId,
                AiScreenshotReprocessJobStatuses.CompletedWithErrors);
            Assert.Equal(1, completed.SucceededCaptures);
            Assert.Equal(1, completed.SkippedCaptures);
            Assert.Equal(1, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task ChangedArtifactIdentityIsSkippedEvenWhenScreenshotCountIsUnchanged()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 2, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: false);
            await using var application = CreateApplication(fixture.Store, analysis);
            var preview = await PreviewTodayAsync(application);
            var start = await application.StartAiScreenshotReprocessingAsync(preview.Value!.PlanId, CancellationToken.None);
            await analysis.Entered.WaitAsync(TimeSpan.FromSeconds(5));

            var second = fixture.Captures[1];
            var replacement = Path.Combine(directory, $"{second.CaptureId}_1.0.0_manual_monitor-2.webp");
            File.WriteAllBytes(replacement, [4, 5, 6]);
            fixture.Store.UpsertScreenshotIntervalTelemetry(
                second.CaptureId,
                [replacement],
                new ScreenshotIntervalTelemetry(second.CapturedAt.AddMinutes(-1), second.CapturedAt, 10, 5));

            analysis.Release();
            var completed = await WaitForStatusAsync(
                application,
                start.Value!.JobId,
                AiScreenshotReprocessJobStatuses.CompletedWithErrors);
            Assert.Equal(1, completed.SucceededCaptures);
            Assert.Equal(1, completed.SkippedCaptures);
            Assert.Equal(1, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task ScreenshotDirectoryChangeInvalidatesAPlan()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 1, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: true);
            await using var application = CreateApplication(fixture.Store, analysis);
            var preview = await PreviewTodayAsync(application);
            var replacementDirectory = Path.Combine(directory, "replacement");
            Directory.CreateDirectory(replacementDirectory);
            fixture.Store.SaveSettings(fixture.Store.LoadSettings() with { ScreenshotDirectory = replacementDirectory });

            var start = await application.StartAiScreenshotReprocessingAsync(preview.Value!.PlanId, CancellationToken.None);

            Assert.False(start.Succeeded);
            Assert.Equal("ai.screenshot_reprocess.configuration.changed", start.Code);
            Assert.Equal(0, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task LiveOcrRefinementWaitsForTheHistoricalSingleFlightBoundary()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 2, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: false);
            var refinement = new ControlledOcrRefinementService();
            await using var application = CreateApplication(fixture.Store, analysis, refinement);
            var preview = await PreviewTodayAsync(application);
            var start = await application.StartAiScreenshotReprocessingAsync(preview.Value!.PlanId, CancellationToken.None);
            await analysis.Entered.WaitAsync(TimeSpan.FromSeconds(5));

            var liveCapture = CreateUntrackedCapture(directory);
            var liveAnalysis = application.AnalyzeCapturedScreenshotAsync(
                new AnalyzeCapturedScreenshotRequest(liveCapture, KeepCapture: true, Origin: "snapshot.manual"),
                CancellationToken.None);
            Assert.False(refinement.Entered.IsCompleted);

            analysis.Release();
            await refinement.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            var liveResult = await liveAnalysis.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(liveResult.Succeeded);
            Assert.Equal(1, refinement.CallCount);
            Assert.Equal(1, analysis.LiveCallCount);

            var completed = await WaitForStatusAsync(
                application,
                start.Value!.JobId,
                AiScreenshotReprocessJobStatuses.Completed);
            Assert.Equal(2, completed.SucceededCaptures);
            Assert.Equal(2, analysis.HistoricalCallCount);
        });
    }

    [Fact]
    public async Task LastDailyAllowanceIsReservedForTheRequiredDescription()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(
                directory,
                captureCount: 1,
                screensPerCapture: 1,
                dailyLimit: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: true);
            var refinement = new ControlledOcrRefinementService();
            await using var application = CreateApplication(fixture.Store, analysis, refinement);

            var result = await application.AnalyzeCapturedScreenshotAsync(
                new AnalyzeCapturedScreenshotRequest(CreateUntrackedCapture(directory), KeepCapture: true, Origin: "snapshot.manual"),
                CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(0, refinement.CallCount);
            Assert.Equal(1, analysis.LiveCallCount);
        });
    }

    [Fact]
    public async Task LiveCaptureRevalidatesSettingsAfterWaitingForTheVisualGate()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 1, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: false);
            var refinement = new ControlledOcrRefinementService();
            await using var application = CreateApplication(fixture.Store, analysis, refinement);
            var preview = await PreviewTodayAsync(application);
            _ = await application.StartAiScreenshotReprocessingAsync(preview.Value!.PlanId, CancellationToken.None);
            await analysis.Entered.WaitAsync(TimeSpan.FromSeconds(5));

            var liveAnalysis = application.AnalyzeCapturedScreenshotAsync(
                new AnalyzeCapturedScreenshotRequest(CreateUntrackedCapture(directory), KeepCapture: true, Origin: "snapshot.manual"),
                CancellationToken.None);
            fixture.Store.SaveSettings(fixture.Store.LoadSettings() with { OpenAiEnabled = false });
            analysis.Release();

            var result = await liveAnalysis.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Succeeded);
            Assert.Equal("ai.disabled", result.Code);
            Assert.False(fixture.Store.LoadSettings().OpenAiEnabled);
            Assert.Equal(0, refinement.CallCount);
            Assert.Equal(0, analysis.LiveCallCount);
        });
    }

    [Fact]
    public async Task LiveAnalysisRechecksQuotaAfterAcquiringTheSharedGate()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 1, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: true);
            var allowance = 1;
            var invocationCount = 0;
            await using var service = CreateService(
                fixture.Store,
                analysis,
                _ => CostGate(Volatile.Read(ref allowance) == 1));
            var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var first = service.RunLiveAnalysisAsync(async () =>
            {
                Interlocked.Increment(ref invocationCount);
                firstEntered.TrySetResult(true);
                await releaseFirst.Task;
                return 1;
            }, CancellationToken.None);
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = service.RunLiveAnalysisAsync(() =>
            {
                Interlocked.Increment(ref invocationCount);
                return Task.FromResult(2);
            }, CancellationToken.None);

            Volatile.Write(ref allowance, 0);
            releaseFirst.TrySetResult(true);
            Assert.Equal(1, await first);
            await Assert.ThrowsAsync<AiDailyAnalysisQuotaReachedException>(() => second);
            Assert.Equal(1, invocationCount);
        });
    }

    [Fact]
    public async Task DisposeWaitsForAnInFlightLiveVisualOperation()
    {
        await WithApiKeyAsync(async directory =>
        {
            var fixture = CreateStoreWithCaptures(directory, captureCount: 1, screensPerCapture: 1);
            var analysis = new ControlledHistoricalAnalysisService(fixture.Store.LoadSettings().InstallationId, initiallyReleased: true);
            var service = CreateService(fixture.Store, analysis, _ => CostGate(allowed: true));
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = service.RunLiveAnalysisAsync(async () =>
            {
                entered.TrySetResult(true);
                await release.Task;
                return true;
            }, CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var dispose = service.DisposeAsync().AsTask();
            await Task.Yield();
            Assert.False(dispose.IsCompleted);
            release.TrySetResult(true);

            Assert.True(await operation);
            await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        });
    }

    private static StoreFixture CreateStoreWithCaptures(
        string directory,
        int captureCount,
        int screensPerCapture,
        int dailyLimit = 20)
    {
        var store = new LocalStore(directory);
        var settings = store.LoadSettings() with
        {
            OpenAiEnabled = true,
            ScreenshotsEnabled = true,
            AiApiKeyName = TestApiKeyVariable,
            ScreenshotDirectory = directory,
            OpenAiDailyLimit = dailyLimit
        };
        store.SaveSettings(settings);
        var captures = new List<TestCapture>(captureCount);
        for (var captureIndex = 0; captureIndex < captureCount; captureIndex++)
        {
            var captureId = Guid.NewGuid().ToString("N");
            var paths = Enumerable.Range(1, screensPerCapture)
                .Select(index => Path.Combine(directory, $"{captureId}_1.0.0_manual_monitor-{index}.webp"))
                .ToArray();
            foreach (var path in paths)
            {
                File.WriteAllBytes(path, [1, 2, 3]);
            }

            var localCaptureTime = DateTime.SpecifyKind(
                DateTime.Today.AddHours(12).AddMinutes(captureIndex * 2),
                DateTimeKind.Unspecified);
            var capturedAt = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(localCaptureTime, TimeZoneInfo.Local),
                TimeSpan.Zero);
            store.UpsertScreenshotIntervalTelemetry(
                captureId,
                paths,
                new ScreenshotIntervalTelemetry(capturedAt.AddMinutes(-1), capturedAt, 10, 5));
            store.AppendSample(new ActivitySample(
                capturedAt.AddSeconds(1),
                60,
                "active",
                "devenv",
                "Visual Studio",
                $"Editing capture {captureIndex + 1}",
                "TrackMeUp",
                settings.InstallationId,
                4,
                2));
            captures.Add(new TestCapture(captureId, capturedAt, paths));
        }

        return new StoreFixture(store, captures);
    }

    private static TrackMeUpApplication CreateApplication(
        LocalStore store,
        IAiAnalysisService analysis,
        IAiOcrRefinementService? refinement = null)
    {
        var utilities = new UtilityService();
        return new TrackMeUpApplication(
            store,
            utilities,
            new TrackingDomainService(store),
            new UnexpectedCaptureService(),
            new SystemSnapshotService(),
            analysis,
            new StartupService(),
            new BuildInformationService(),
            ocrRefinement: refinement);
    }

    private static ScreenshotCaptureResult CreateUntrackedCapture(string directory)
    {
        var captureId = Guid.NewGuid().ToString("N");
        var path = Path.Combine(directory, $"{captureId}_1.0.0_manual_monitor-live.webp");
        File.WriteAllBytes(path, [7, 8, 9]);
        return new ScreenshotCaptureResult(
            captureId,
            [path],
            [path],
            ScreenshotCaptureOrigins.Manual);
    }

    private static AiScreenshotReprocessingService CreateService(
        LocalStore store,
        IAiAnalysisService analysis,
        Func<AppSettings, AnalysisCostGate> buildCostGate) =>
        new(
            store,
            analysis,
            _ => true,
            buildCostGate,
            (_, _, _) => false);

    private static Task<OperationResult<AiScreenshotReprocessPlan>> PreviewTodayAsync(ITrackMeUpApplication application) =>
        application.PreviewAiScreenshotReprocessingAsync(
            new AiScreenshotReprocessRequest(DateOnly.FromDateTime(DateTime.Today)),
            CancellationToken.None);

    private static AnalysisCostGate CostGate(bool allowed) =>
        new(allowed, allowed ? null : "daily_limit", 0.02m, allowed ? 0 : 1, 0.02m);

    private static void AppendSuccessfulVisualUsage(LocalStore store)
    {
        var now = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var usage = new AiRequestUsageRecord(
            Guid.NewGuid().ToString("N"),
            correlationId,
            now,
            now.AddMilliseconds(10),
            "manual",
            "screen_analysis",
            "test-provider",
            "provider.invalid",
            "test-model",
            "test-model",
            null,
            null,
            200,
            10,
            null,
            1,
            10,
            100,
            new AiUsageMetrics(10, 5, 15),
            "stop",
            true,
            null);
        var analysis = new AiAnalysis(
            now,
            "Visual Studio",
            "Live work",
            "Live description",
            store.LoadSettings().InstallationId,
            null,
            CorrelationId: correlationId,
            Origin: "manual");
        store.AppendAiAnalysisAndUsage(usage, analysis);
    }

    private static async Task<AiScreenshotReprocessJobSnapshot> WaitForStatusAsync(
        ITrackMeUpApplication application,
        Guid jobId,
        string expectedStatus)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        AiScreenshotReprocessJobSnapshot? latest = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = await application.GetAiScreenshotReprocessingJobAsync(jobId, CancellationToken.None);
            latest = status.Value;
            if (status.Value?.Status == expectedStatus)
            {
                return status.Value;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Expected job status '{expectedStatus}', but latest was '{latest?.Status ?? "missing"}' " +
            $"(completed={latest?.CompletedCaptures}, succeeded={latest?.SucceededCaptures}, " +
            $"skipped={latest?.SkippedCaptures}, failed={latest?.FailedCaptures}, pause={latest?.PauseReason ?? "none"}).");
    }

    private static async Task WithApiKeyAsync(Func<string, Task> test)
    {
        var directory = CreateTemporaryDirectory();
        var previousKey = Environment.GetEnvironmentVariable(TestApiKeyVariable, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(TestApiKeyVariable, "sk-test-only-key-1234567890", EnvironmentVariableTarget.Process);
        try
        {
            await test(directory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestApiKeyVariable, previousKey, EnvironmentVariableTarget.Process);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "TrackMeUp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record StoreFixture(LocalStore Store, IReadOnlyList<TestCapture> Captures);

    private sealed record TestCapture(
        string CaptureId,
        DateTimeOffset CapturedAt,
        IReadOnlyList<string> Paths);

    private sealed class UnexpectedCaptureService : IScreenCaptureService
    {
        public ScreenshotCaptureResult CaptureByMode(string directory, string captureMode, string captureOrigin) =>
            throw new InvalidOperationException("Historical processing must not capture a new screenshot.");
    }

    private sealed class ControlledHistoricalAnalysisService : IAiAnalysisService
    {
        private readonly string _installationId;
        private readonly bool _failRequests;
        private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _historicalCallCount;
        private int _liveCallCount;

        internal ControlledHistoricalAnalysisService(
            string installationId,
            bool initiallyReleased,
            bool failRequests = false)
        {
            _installationId = installationId;
            _failRequests = failRequests;
            if (initiallyReleased)
            {
                _released.TrySetResult(true);
            }
        }

        internal Task Entered => _entered.Task;

        internal int HistoricalCallCount => Volatile.Read(ref _historicalCallCount);

        internal int LiveCallCount => Volatile.Read(ref _liveCallCount);

        internal void Release() => _released.TrySetResult(true);

        public Task<AiAnalysis> AnalyzeCurrentScreenAsync(
            AnalysisContextSnapshot? activity,
            bool allowCapture = true,
            string origin = "manual",
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The test expects historical analysis only.");

        public Task<AiAnalysis> AnalyzeCapturedScreenAsync(
            AnalysisContextSnapshot? activity,
            ScreenshotCaptureResult captureResult,
            bool keepCapture,
            string origin,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _liveCallCount);
            return Task.FromResult(new AiAnalysis(
                DateTimeOffset.UtcNow,
                activity?.Application ?? "Visual Studio",
                activity?.Context ?? "Live work",
                "Live description",
                _installationId,
                string.Join(';', captureResult.StoredScreenshotPaths),
                CorrelationId: captureResult.CaptureId,
                Origin: origin,
                TextSnapshots: captureResult.TextSnapshots));
        }

        public async Task<AiAnalysis> AnalyzeHistoricalCapturedScreenAsync(
            AnalysisContextSnapshot activity,
            ScreenshotCaptureResult captureResult,
            string origin,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _historicalCallCount);
            _entered.TrySetResult(true);
            await _released.Task.WaitAsync(cancellationToken);
            if (_failRequests)
            {
                throw new AiProviderRequestException(
                    "Synthetic provider failure.",
                    new AiProviderFailure("synthetic", 500, 1));
            }

            return new AiAnalysis(
                DateTimeOffset.UtcNow,
                activity.Application,
                activity.Context,
                "Historical description",
                _installationId,
                string.Join(';', captureResult.StoredScreenshotPaths),
                CorrelationId: captureResult.CaptureId,
                Origin: origin,
                TextSnapshots: captureResult.TextSnapshots);
        }
    }

    private sealed class ControlledOcrRefinementService : IAiOcrRefinementService
    {
        private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;

        internal Task Entered => _entered.Task;

        internal int CallCount => Volatile.Read(ref _callCount);

        public Task<ScreenshotCaptureResult> RefineAsync(
            ScreenshotCaptureResult capture,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            _entered.TrySetResult(true);
            return Task.FromResult(capture);
        }
    }
}
