// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class HardwareTelemetryServiceTests
{
    [Fact]
    public async Task ConcurrentConsumers_ShareOneFrozenRecentReading()
    {
        var clock = new TestClock();
        var reads = 0;
        var sensors = new List<HardwareSensorSnapshot> { new("/cpu/load/0", "CPU Total", "Load", "%", 12) };
        var devices = new List<HardwareDeviceSnapshot> { new("/cpu", "CPU", "Cpu", clock.GetUtcNow(), sensors) };
        await using var service = Create(clock, _ =>
        {
            reads++;
            return ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "partial", devices));
        });

        var results = await Task.WhenAll(service.CaptureAsync(CancellationToken.None).AsTask(), service.CaptureAsync(CancellationToken.None).AsTask());
        sensors.Clear();
        devices.Clear();

        Assert.Equal(1, reads);
        Assert.Same(results[0], results[1]);
        Assert.Equal(12, results[0].Devices[0].Sensors[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<HardwareDeviceSnapshot>)results[0].Devices).Clear());
    }

    [Fact]
    public async Task FailedReading_IsExplicitAndRetriedAfterCooldown()
    {
        var clock = new TestClock();
        var attempts = 0;
        await using var service = Create(clock, _ =>
        {
            attempts++;
            if (attempts == 1) throw new IOException("private machine diagnostic must not enter the snapshot");
            return ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "ready", []));
        });

        var failed = await service.CaptureAsync(CancellationToken.None);
        Assert.Equal("unavailable", failed.Status);
        Assert.Equal("collector-failed", failed.ErrorCode);
        Assert.Empty(failed.Devices);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Same(failed, await service.CaptureAsync(CancellationToken.None));
        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.Equal("ready", (await service.CaptureAsync(CancellationToken.None)).Status);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task InvalidProviderData_IsNotSavedAsAValidReading()
    {
        var clock = new TestClock();
        await using var service = Create(clock, _ => ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "ready",
            [new("/cpu", "CPU", "Cpu", clock.GetUtcNow(), [new("/cpu/temp/0", "CPU Package", "Temperature", "°C", double.NaN)])])));

        var result = await service.CaptureAsync(CancellationToken.None);

        Assert.Equal("unavailable", result.Status);
        Assert.Empty(result.Devices);
    }

    [Fact]
    public async Task Cancellation_DoesNotBecomeAnUnavailableMeasurement()
    {
        var clock = new TestClock();
        await using var service = Create(clock, async token =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CaptureAsync(cancellation.Token).AsTask());
    }

    private static HardwareTelemetryService Create(TestClock clock, Func<CancellationToken, ValueTask<SystemSnapshot>> read) =>
        new(Path.Combine(Path.GetTempPath(), "TrackMeUp.Hardware.UnitTests.exe"), clock, reader: read);

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(TimeSpan duration) => _now += duration;
    }
}
