using System;
using System.Linq;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class ActivityScoreServiceTests
{
    [Fact]
    public void GetState_UsesThirtyMinuteBucketsAndKeepsInputDominant()
    {
        var service = new ActivityScoreService();
        var windowEnd = new DateTimeOffset(2026, 8, 9, 10, 30, 0, TimeSpan.Zero);
        var windowStart = windowEnd.AddMinutes(-(ActivityScoreService.WindowMinutes - 1));

        for (var index = 0; index < ActivityScoreService.WindowMinutes; index++)
        {
            service.RecordSample(Sample(windowStart.AddMinutes(index), keys: 1, clicks: 2));
        }

        service.RecordSystemSnapshot(Snapshot(windowEnd, cpu: 100, gpu: 100));
        var state = service.GetState(15, windowEnd);

        Assert.Equal(ActivityScoreService.WindowMinutes, state.Minutes.Count);
        Assert.Equal(15, state.SnapshotIntervalMinutes);
        Assert.Equal(15, state.PreviousSnapshotInterval.KeyPresses);
        Assert.Equal(30, state.PreviousSnapshotInterval.MouseClicks);
        Assert.Equal(15, state.LatestSnapshotInterval.KeyPresses);
        Assert.Equal(30, state.LatestSnapshotInterval.MouseClicks);
        Assert.True(state.CurrentScore > 20);
    }

    [Fact]
    public void GetState_CpuAndGpuContributeMuchLessThanInput()
    {
        var windowEnd = new DateTimeOffset(2026, 8, 9, 10, 30, 0, TimeSpan.Zero);
        var telemetryOnly = new ActivityScoreService();
        telemetryOnly.RecordSystemSnapshot(Snapshot(windowEnd, cpu: 100, gpu: 100));

        var inputDriven = new ActivityScoreService();
        inputDriven.RecordSample(Sample(windowEnd, keys: 40, clicks: 8));

        Assert.True(inputDriven.GetState(15, windowEnd).CurrentScore > telemetryOnly.GetState(15, windowEnd).CurrentScore);
    }

    [Fact]
    public void CalculateIntervalActivityIndex_UsesPersistedTelemetryWithSharedWeights()
    {
        var intervalEnd = new DateTimeOffset(2026, 8, 9, 10, 30, 0, TimeSpan.Zero);
        var samples = Enumerable.Range(0, 15)
            .Select(index => Sample(intervalEnd.AddMinutes(-index), keys: 10, clicks: 2))
            .ToArray();

        var index = ActivityScoreService.CalculateIntervalActivityIndex(
            samples,
            intervalMinutes: 15,
            cpuUsagePercent: 50,
            gpuUsagePercent: 50);

        // Average input contributes 12.5 points, active time 0.67, CPU 2, and GPU 1.
        Assert.Equal(16, index);
    }

    [Fact]
    public void BuildScreenshotIntervalTelemetry_AveragesOnlyPointsAfterPreviousCapture()
    {
        var service = new ActivityScoreService();
        var previousCapture = new DateTimeOffset(2026, 8, 9, 10, 0, 30, TimeSpan.Zero);
        service.RecordSystemSnapshot(Snapshot(previousCapture, cpu: 90, gpu: 90));
        service.RecordSystemSnapshot(Snapshot(previousCapture.AddMinutes(1), cpu: 20, gpu: 40));
        service.RecordSystemSnapshot(Snapshot(previousCapture.AddMinutes(2), cpu: 40, gpu: 60));

        var telemetry = service.BuildScreenshotIntervalTelemetry(previousCapture, previousCapture.AddMinutes(3));

        Assert.Equal(30, telemetry.CpuUsagePercent);
        Assert.Equal(50, telemetry.GpuUsagePercent);
    }

    private static ActivitySample Sample(DateTimeOffset timestamp, long keys, long clicks) => new(
        timestamp, 5, "active", "test", "Test", "Test", "Test", "installation", keys, clicks);

    private static SystemSnapshot Snapshot(DateTimeOffset timestamp, int cpu, int gpu) => new(
        timestamp, cpu, null, null, gpu, 0, 0, null, new NetworkSnapshotState(0, 0), Array.Empty<DiskSnapshotState>());
}
