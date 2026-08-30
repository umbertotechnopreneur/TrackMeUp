// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Runtime;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class RuntimeProtocolTests
{
    [Fact]
    public void EndpointNames_AreStableAndDoNotExposeInstallationId()
    {
        var endpoint = RuntimeProtocol.CreateEndpoint("machine-private-installation-id");

        Assert.StartsWith("Local\\TrackMeUp.Runtime.", endpoint.MutexName);
        Assert.StartsWith("TrackMeUp.Runtime.", endpoint.PipeName);
        Assert.DoesNotContain("machine-private-installation-id", endpoint.MutexName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LaunchOptions_StripsCliSwitchFromCommandArguments()
    {
        var options = LaunchOptions.Parse(["-cli", "--language", "it-IT", "status"]);

        Assert.Equal(LaunchMode.Cli, options.Mode);
        Assert.Equal("it-IT", options.Language);
        Assert.Equal(["status"], options.RemainingArguments);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("pt")]
    [InlineData("zh")]
    [InlineData("zh-Hant")]
    public void LaunchOptions_RejectsUnsupportedOrAmbiguousLanguageAliases(string language)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--language", language]));
    }

    [Fact]
    public void LaunchOptions_RequiresLanguageValue()
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--language"]));
    }

    [Fact]
    public void LaunchOptions_BareReportsVerbSelectsDedicatedWindow()
    {
        var options = LaunchOptions.Parse(["reports"]);

        Assert.Equal(LaunchMode.Reports, options.Mode);
        Assert.Empty(options.RemainingArguments);
    }

    [Fact]
    public void LaunchOptions_WindowsStartupSwitchRequestsNotificationAreaLaunch()
    {
        var options = LaunchOptions.Parse(["--start-with-windows"]);

        Assert.True(options.StartWithWindows);
        Assert.Empty(options.RemainingArguments);
    }

    [Fact]
    public void LaunchOptions_ReportsPreservesThemeOverrideForTheNativeShell()
    {
        var options = LaunchOptions.Parse(["reports", "--theme", "dark"]);

        Assert.Equal(LaunchMode.Reports, options.Mode);
        Assert.Equal("dark", options.Theme);
        Assert.Empty(options.RemainingArguments);
    }

    [Theory]
    [InlineData("-cli", "reports")]
    [InlineData("reports", "--cli")]
    public void LaunchOptions_CliSwitchTakesPrecedenceOverReportsVerb(string first, string second)
    {
        var options = LaunchOptions.Parse([first, second]);

        Assert.Equal(LaunchMode.Cli, options.Mode);
        Assert.Equal(["reports"], options.RemainingArguments);
    }

    [Fact]
    public async Task ReportQueryV1_RoundTripsThroughTheWireEnvelope()
    {
        var expected = new ReportQuery(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            "UTC",
            ReportView.HourOfWeek);
        var request = new RuntimeRequestEnvelope(
            RuntimeProtocol.ProtocolVersion,
            Guid.NewGuid(),
            "report.query.v1",
            JsonSerializer.SerializeToElement(expected, RuntimeProtocol.SerializerOptions),
            "it",
            "test");
        await using var stream = new MemoryStream();

        await RuntimeProtocol.WriteAsync(stream, request, CancellationToken.None);
        stream.Position = 0;
        var actualEnvelope = await RuntimeProtocol.ReadAsync<RuntimeRequestEnvelope>(stream, CancellationToken.None);
        var actual = actualEnvelope.Payload.Deserialize<ReportQuery>(RuntimeProtocol.SerializerOptions);

        Assert.Equal("report.query.v1", actualEnvelope.Operation);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ReportSnapshotV4_RoundTripsNullableDailyActivityScores()
    {
        var snapshot = new ReportSnapshot(
            4,
            new ReportRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 2), "UTC", 2),
            new ReportTotals(60, 0, 60, 40, 8, 1),
            [
                new ReportCalendarCell(new DateOnly(2026, 2, 1), 60, 0, 60, 40, 8, 1, true, 62),
                new ReportCalendarCell(new DateOnly(2026, 2, 2), 0, 0, 0, 0, 0, 0, false, null)
            ],
            [],
            [],
            [],
            new ReportDataQuality(true, null, null, 1, 60, 172_800, 60d / 172_800d),
            AiUsageSummary.Empty);
        var response = new RuntimeResponseEnvelope(
            RuntimeProtocol.ProtocolVersion,
            Guid.NewGuid(),
            true,
            "report.loaded",
            "ReportLoaded",
            snapshot,
            []);
        await using var stream = new MemoryStream();

        await RuntimeProtocol.WriteAsync(stream, response, CancellationToken.None);
        stream.Position = 0;
        var actualEnvelope = await RuntimeProtocol.ReadAsync<RuntimeResponseEnvelope>(stream, CancellationToken.None);
        var payload = Assert.IsType<JsonElement>(actualEnvelope.Payload);
        var actual = payload.Deserialize<ReportSnapshot>(RuntimeProtocol.SerializerOptions);

        Assert.NotNull(actual);
        Assert.Equal(4, actual.ContractVersion);
        Assert.Equal(62, actual.Calendar[0].ActivityScore);
        Assert.Null(actual.Calendar[1].ActivityScore);
    }

    [Fact]
    public async Task RuntimeHost_KeepsServingHealthAndCancelsReportWhenClientDisconnects()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, ConcurrentRuntimeProxy>();
        var proxy = (ConcurrentRuntimeProxy)(object)application;
        var installationId = $"runtime-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(3));
        using var reportCancellation = new CancellationTokenSource();
        var reportTask = client.GetReportAsync(
            new ReportQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "UTC"),
            reportCancellation.Token);
        await proxy.ReportStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var health = await client.GetRuntimeHealthAsync(CancellationToken.None);

        Assert.True(health.Succeeded);
        reportCancellation.Cancel();
        var report = await reportTask;
        Assert.False(report.Succeeded);
        Assert.Equal("operation.cancelled", report.Code);
        await proxy.ReportCancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task RuntimeHost_DoesNotCreateASecondApplicationWhenOwnershipIsAlreadyHeld()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, ConcurrentRuntimeProxy>();
        var installationId = $"runtime-ownership-test-{Guid.NewGuid():N}";
        await using var owner = new RuntimeHost(application, installationId);
        Assert.True(owner.TryStart());
        var factoryCalls = 0;
        await using var contender = new RuntimeHost(
            () =>
            {
                factoryCalls++;
                return application;
            },
            installationId);

        Assert.False(contender.TryStart());
        Assert.Equal(0, factoryCalls);
    }

    [Fact]
    public async Task RuntimeHost_HoldsOwnershipUntilFactoryOwnedApplicationIsDisposed()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, BlockingDisposeRuntimeProxy>();
        var proxy = (BlockingDisposeRuntimeProxy)(object)application;
        var installationId = $"runtime-dispose-test-{Guid.NewGuid():N}";
        await using var owner = new RuntimeHost(() => application, installationId);
        Assert.True(owner.TryStart());

        var disposeTask = owner.DisposeAsync().AsTask();
        try
        {
            await proxy.DisposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await using var contender = new RuntimeHost(
                () => throw new InvalidOperationException("A contender must not create an application."),
                installationId);

            Assert.False(contender.TryStart());

            proxy.AllowDispose.TrySetResult();
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(3));

            var successorApplication = DispatchProxy.Create<ITrackMeUpApplication, ConcurrentRuntimeProxy>();
            await using var successor = new RuntimeHost(successorApplication, installationId);
            Assert.True(successor.TryStart());
        }
        finally
        {
            proxy.AllowDispose.TrySetResult();
            await disposeTask;
        }
    }

    [Fact]
    public async Task RuntimeClient_DoesNotCaptureABlockedCallerSynchronizationContext()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, DelayedHealthRuntimeProxy>();
        var installationId = $"runtime-context-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(3));
        using var completed = new ManualResetEventSlim();
        OperationResult<RuntimeHealth>? result = null;
        Exception? failure = null;
        var caller = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            try
            {
                result = client.GetRuntimeHealthAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completed.Set();
            }
        })
        {
            IsBackground = true,
            Name = "TrackMeUp runtime blocked-context test"
        };

        caller.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(5)), "RuntimeClient captured the caller synchronization context and deadlocked.");
        Assert.Null(failure);
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task StartupMutation_WaitsPastTheDefaultRuntimeTimeout()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, DelayedStartupRuntimeProxy>();
        var installationId = $"startup-timeout-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(1));

        var health = await client.GetRuntimeHealthAsync(CancellationToken.None);
        var startup = await client.SetStartupEnabledAsync(true, CancellationToken.None);

        Assert.True(health.Succeeded);
        Assert.True(startup.Succeeded);
        Assert.True(startup.Value);
    }

    [Fact]
    public async Task WorldClockQuery_UsesItsWeatherAwareTimeoutAndKeepsLocalClocksOnWeatherFailure()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, WeatherUnavailableWorldClockRuntimeProxy>();
        var installationId = $"world-clock-timeout-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.Zero);

        var result = await client.GetWorldClocksAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(15), RuntimeClient.WorldClockQueryTimeout);
        Assert.True(result.Succeeded);
        var clock = Assert.Single(result.Value!.Clocks);
        Assert.Equal("london", clock.CityId);
        Assert.Null(clock.Weather);
        Assert.Equal("unavailable", result.Value.WeatherStatus.State);
        Assert.Equal("request-failed", result.Value.WeatherStatus.ReasonCode);
    }

    [Fact]
    public async Task AiModelCatalog_RoundTripsThroughTheRuntimeFacade()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, CatalogRuntimeProxy>();
        var installationId = $"catalog-runtime-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(3));

        var result = await client.GetAiModelCatalogAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        var model = Assert.Single(result.Value.Models);
        Assert.Equal("gpt-test", model.Key);
        Assert.Equal(["auto", "low"], model.SupportedThinkingEfforts);
    }

    [Fact]
    public async Task AiPricingOverview_RoundTripsThroughTheRuntimeFacade()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, PricingRuntimeProxy>();
        var installationId = $"pricing-runtime-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(3));

        var result = await client.GetAiPricingOverviewAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.DisplayedModelCount);
        Assert.Equal(0.0003m, result.Value.EstimatedCostTodayUsd);
        var row = Assert.Single(result.Value.Models);
        Assert.Equal("gpt-test", row.Model);
        Assert.Equal(1.25m, row.InputUsdPerMillionTokens);
        Assert.Equal(10m, row.OutputUsdPerMillionTokens);
    }

    [Fact]
    public async Task AiScreenshotReprocessing_RoundTripsPreviewAndJobCommandsThroughRuntimeFacade()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, AiScreenshotReprocessRuntimeProxy>();
        var installationId = $"ai-reprocess-runtime-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(3));
        var date = new DateOnly(2026, 8, 14);

        var preview = await client.PreviewAiScreenshotReprocessingAsync(
            new AiScreenshotReprocessRequest(date),
            CancellationToken.None);
        var started = await client.StartAiScreenshotReprocessingAsync(
            AiScreenshotReprocessRuntimeProxy.PlanId,
            CancellationToken.None);
        var paused = await client.PauseAiScreenshotReprocessingAsync(
            AiScreenshotReprocessRuntimeProxy.JobId,
            CancellationToken.None);

        Assert.True(preview.Succeeded);
        Assert.Equal(date, preview.Value?.Date);
        Assert.Equal(5, preview.Value?.MissingDescriptionScreenshotCount);
        Assert.True(started.Succeeded);
        Assert.Equal(AiScreenshotReprocessJobStatuses.Running, started.Value?.Status);
        Assert.True(paused.Succeeded);
        Assert.Equal(AiScreenshotReprocessJobStatuses.PauseRequested, paused.Value?.Status);
    }

    [Fact]
    public async Task SearchAvailability_RoundTripsThroughTheRuntimeFacade()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, SearchAvailabilityRuntimeProxy>();
        var installationId = $"search-availability-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(3));

        var result = await client.GetSearchAvailabilityAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(new SearchAvailability(14, 3, true), result.Value);
    }

    [Fact]
    public async Task ApplicationNotifications_RoundTripThroughTheSharedRuntimeFacade()
    {
        var application = DispatchProxy.Create<ITrackMeUpApplication, NotificationRuntimeProxy>();
        var installationId = $"notification-runtime-test-{Guid.NewGuid():N}";
        await using var host = new RuntimeHost(application, installationId);
        Assert.True(host.TryStart());
        await using var client = new RuntimeClient(installationId, TimeSpan.FromSeconds(3));

        var result = await client.DrainApplicationNotificationsAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        var notification = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ApplicationNotification>>(result.Value));
        Assert.Equal(ApplicationNotificationSeverity.Error, notification.Severity);
        Assert.Equal("Notification.AiAnalysisFailed.Message", notification.MessageKey);
    }

    public class ConcurrentRuntimeProxy : DispatchProxy
    {
        public TaskCompletionSource<bool> ReportStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReportCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.GetReportAsync) => WaitForReportCancellationAsync((CancellationToken)args![1]!),
                nameof(ITrackMeUpApplication.GetRuntimeHealthAsync) => Task.FromResult(OperationResult<RuntimeHealth>.Success(
                    "runtime.healthy",
                    "RuntimeHealthy",
                    new RuntimeHealth("test", RuntimeProtocol.ProtocolVersion, "test", true, ["report.query.v1"]))),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }

        private async Task<OperationResult<ReportSnapshot>> WaitForReportCancellationAsync(CancellationToken cancellationToken)
        {
            ReportStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The report cancellation test completed without cancellation.");
            }
            catch (OperationCanceledException)
            {
                ReportCancelled.TrySetResult(true);
                throw;
            }
        }
    }

    public class DelayedHealthRuntimeProxy : DispatchProxy
    {
        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.GetRuntimeHealthAsync) => GetHealthAsync(),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }

        private static async Task<OperationResult<RuntimeHealth>> GetHealthAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
            return OperationResult<RuntimeHealth>.Success(
                "runtime.healthy",
                "RuntimeHealthy",
                new RuntimeHealth("test", RuntimeProtocol.ProtocolVersion, "test", true, ["runtime.health"]));
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        /// <inheritdoc />
        public override void Post(SendOrPostCallback callback, object? state)
        {
            // Deliberately never pump queued callbacks: production IPC must not depend on a UI dispatcher continuation.
        }
    }

    public class CatalogRuntimeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.GetAiModelCatalogAsync) => Task.FromResult(
                    OperationResult<AiModelCatalogSnapshot>.Success(
                        "ai.models.loaded",
                        "AiModelsLoaded",
                        new AiModelCatalogSnapshot(
                            1,
                            [new AiModelDescriptor("gpt-test", ["gpt-test-alias"], "Test", "Test model", "#123456", ["auto", "low"], true, "general", false)]))),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }

    public class PricingRuntimeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.GetAiPricingOverviewAsync) => Task.FromResult(
                    OperationResult<AiPricingOverview>.Success(
                        "ai.pricing.loaded",
                        "AiPricingLoaded",
                        new AiPricingOverview(
                            new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
                            1,
                            1,
                            0.0003m,
                            1,
                            null,
                            0,
                            100,
                            20,
                            120,
                            new DateOnly(2026, 8, 1),
                            new DateOnly(2026, 8, 9),
                            0.0012m,
                            null,
                            [new AiPricingCostRow("gpt-test", 1.25m, 10m)]))),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }

    public class NotificationRuntimeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.DrainApplicationNotificationsAsync) => Task.FromResult(
                    OperationResult<IReadOnlyList<ApplicationNotification>>.Success(
                        "notifications.drained",
                        "ApplicationNotificationsDrained",
                        [new ApplicationNotification(
                            Guid.Parse("638ba5bb-5074-44f5-85da-da172add83d1"),
                            new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
                            ApplicationNotificationSeverity.Error,
                            "Notification.AiAnalysisFailed.Title",
                            "Notification.AiAnalysisFailed.Message",
                            "ai.provider.failed")])),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }

    public class AiScreenshotReprocessRuntimeProxy : DispatchProxy
    {
        public static Guid PlanId { get; } = Guid.Parse("06df0bfa-7180-4431-a1c2-b104f986238f");
        public static Guid JobId { get; } = Guid.Parse("50617e02-0a8d-4eb6-9e29-3458b72d7f1c");

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.PreviewAiScreenshotReprocessingAsync) => Task.FromResult(
                    OperationResult<AiScreenshotReprocessPlan>.Success(
                        "ai.screenshot_reprocess.previewed",
                        "AiScreenshotReprocessPreviewed",
                        new AiScreenshotReprocessPlan(
                            PlanId,
                            DateTimeOffset.UtcNow.AddMinutes(2),
                            ((AiScreenshotReprocessRequest)args![0]!).Date,
                            5,
                            3,
                            5,
                            3,
                            0,
                            0,
                            0,
                            4,
                            20,
                            16,
                            3,
                            5,
                            0.01m,
                            "test-provider",
                            "test-model",
                            true,
                            null,
                            null))),
                nameof(ITrackMeUpApplication.StartAiScreenshotReprocessingAsync) => Task.FromResult(
                    OperationResult<AiScreenshotReprocessJobSnapshot>.Success(
                        "ai.screenshot_reprocess.started",
                        "AiScreenshotReprocessStarted",
                        Job(AiScreenshotReprocessJobStatuses.Running))),
                nameof(ITrackMeUpApplication.PauseAiScreenshotReprocessingAsync) => Task.FromResult(
                    OperationResult<AiScreenshotReprocessJobSnapshot>.Success(
                        "ai.screenshot_reprocess.pause_requested",
                        "AiScreenshotReprocessPauseRequested",
                        Job(AiScreenshotReprocessJobStatuses.PauseRequested))),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }

        private static AiScreenshotReprocessJobSnapshot Job(string status) => new(
            JobId,
            new DateOnly(2026, 8, 14),
            status,
            3,
            5,
            0,
            0,
            3,
            5,
            0,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            DateTimeOffset.UtcNow);
    }

    public class DelayedStartupRuntimeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.GetRuntimeHealthAsync) => Task.FromResult(OperationResult<RuntimeHealth>.Success(
                    "runtime.healthy",
                    "RuntimeHealthy",
                    new RuntimeHealth("test", RuntimeProtocol.ProtocolVersion, "test", true, ["startup.enable"]))),
                nameof(ITrackMeUpApplication.SetStartupEnabledAsync) => CompleteStartupAsync((CancellationToken)args![1]!),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }

        private static async Task<OperationResult<bool>> CompleteStartupAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken);
            return OperationResult<bool>.Success("startup.enabled", "StartupEnabled", true);
        }
    }

    public class WeatherUnavailableWorldClockRuntimeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.GetWorldClocksAsync) => Task.FromResult(
                    OperationResult<WorldClockSnapshot>.Success(
                        "world_clocks.loaded",
                        "WorldClocksLoaded",
                        CreateSnapshot())),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }

        private static WorldClockSnapshot CreateSnapshot()
        {
            var instant = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
            return new WorldClockSnapshot(
                instant,
                [
                    new WorldClockItem(
                        "london",
                        "London",
                        "GB",
                        "GMT Standard Time",
                        instant,
                        true,
                        instant.AddHours(-6),
                        instant.AddHours(6),
                        180d,
                        "Assets/WorldClocks/Skylines/london-summer.png",
                        "summer",
                        new WorldClockAtmosphere("day", [], []),
                        Weather: null)
                ],
                WorldClockSelection.MaximumClocks,
                new WorldClockWeatherStatus(
                    "openweather",
                    "unavailable",
                    "request-failed",
                    1,
                    0));
        }
    }

    public class BlockingDisposeRuntimeProxy : DispatchProxy
    {
        public TaskCompletionSource DisposeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowDispose { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAsyncDisposable.DisposeAsync) => new ValueTask(DisposeCoreAsync()),
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }

        private async Task DisposeCoreAsync()
        {
            DisposeStarted.TrySetResult();
            await AllowDispose.Task;
        }
    }

    public class SearchAvailabilityRuntimeProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ITrackMeUpApplication.GetSearchAvailabilityAsync) => Task.FromResult(
                    OperationResult<SearchAvailability>.Success(
                        "search.availability.loaded",
                        "SearchAvailabilityLoaded",
                        new SearchAvailability(14, 3, true))),
                nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
                "add_RuntimeStateChanged" or "remove_RuntimeStateChanged" => null,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }
}
