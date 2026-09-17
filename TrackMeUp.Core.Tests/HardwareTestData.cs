// SPDX-License-Identifier: MIT

using System;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Services;

namespace TrackMeUp.Core.Tests;

internal static class HardwareTestData
{
    internal static SystemSnapshot Snapshot(DateTimeOffset timestamp, double? cpu = 25, double? gpu = 15) => new(
        timestamp,
        "ready",
        [
            new("/cpu/0", "Test CPU", "Cpu", timestamp,
                [new("/cpu/0/load/0", "CPU Total", "Load", "%", cpu)]),
            new("/gpu/0", "Test GPU", "GpuNvidia", timestamp,
                [new("/gpu/0/load/0", "GPU Core", "Load", "%", gpu)])
        ]);
}

internal sealed class FakeHardwareTelemetryService : IHardwareTelemetryService
{
    internal SystemSnapshot? Snapshot { get; set; }
    internal int CaptureCount { get; private set; }
    internal bool IsTracking { get; private set; }
    internal bool AdvancedEnabled { get; private set; }

    public ValueTask<SystemSnapshot> CaptureAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CaptureCount++;
        return ValueTask.FromResult(Snapshot ?? HardwareTestData.Snapshot(DateTimeOffset.UtcNow));
    }

    public ValueTask SetTrackingAsync(bool isTracking, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsTracking = isTracking;
        return ValueTask.CompletedTask;
    }

    public Task EnableAdvancedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AdvancedEnabled = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
