using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Application;
using TrackMeUp.Runtime;
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
        var options = LaunchOptions.Parse(["-cli", "--language", "it", "status"]);

        Assert.Equal(LaunchMode.Cli, options.Mode);
        Assert.Equal("it", options.Language);
        Assert.Equal(["status"], options.RemainingArguments);
    }

    [Fact]
    public void LaunchOptions_BareReportsVerbSelectsDedicatedWindow()
    {
        var options = LaunchOptions.Parse(["reports"]);

        Assert.Equal(LaunchMode.Reports, options.Mode);
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
}
