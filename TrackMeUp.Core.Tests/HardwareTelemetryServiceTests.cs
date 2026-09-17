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
    public async Task FatalCollectorError_RemainsExplicitUntilRetryCooldownExpires()
    {
        var clock = new TestClock();
        var attempts = 0;
        await using var service = Create(clock, _ =>
        {
            attempts++;
            return ValueTask.FromResult(attempts == 1
                ? new SystemSnapshot(clock.GetUtcNow(), "error", [], "blocked", "sensor-read-failed")
                : new SystemSnapshot(clock.GetUtcNow(), "partial", [], "available"));
        });

        var failed = await service.CaptureAsync(CancellationToken.None);
        Assert.Equal("error", failed.Status);
        Assert.Equal("sensor-read-failed", failed.ErrorCode);
        Assert.Equal("blocked", failed.DriverStatus);
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Same(failed, await service.CaptureAsync(CancellationToken.None));
        Assert.Equal(1, attempts);

        clock.Advance(TimeSpan.FromSeconds(1));
        var retried = await service.CaptureAsync(CancellationToken.None);
        Assert.Equal("partial", retried.Status);
        Assert.Equal("available", retried.DriverStatus);
        Assert.Null(retried.ErrorCode);
        Assert.Equal(2, attempts);
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

    [Fact]
    public async Task DisabledCollection_DoesNotReadAndRetainsTrackingIntent()
    {
        var clock = new TestClock();
        var reads = 0;
        await using var service = Create(clock, _ =>
        {
            reads++;
            return ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "partial", []));
        });
        await service.ConfigureAsync(new(Enabled: false), CancellationToken.None);
        await service.SetTrackingAsync(true, CancellationToken.None);

        var disabled = await service.CaptureAsync(CancellationToken.None);
        SystemSnapshotValidator.Validate(disabled);
        Assert.Equal("disabled", disabled.Status);
        Assert.Equal("disabled", disabled.DriverStatus);
        Assert.Empty(disabled.Devices);
        Assert.Equal(0, reads);

        await service.ConfigureAsync(new(), CancellationToken.None);
        Assert.Equal(1, reads);
        await service.ConfigureAsync(new(Enabled: false), CancellationToken.None);
        Assert.Equal("disabled", (await service.CaptureAsync(CancellationToken.None)).Status);
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task ProfileChange_UsesNewSharedCacheInterval()
    {
        var clock = new TestClock();
        var reads = 0;
        await using var service = Create(clock, _ =>
        {
            reads++;
            return ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "partial", [], "blocked", "device-read-failed"));
        });
        await service.ConfigureAsync(new(SamplingProfile: "slow"), CancellationToken.None);
        var slow = await service.CaptureAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Same(slow, await service.CaptureAsync(CancellationToken.None));

        await service.ConfigureAsync(new(SamplingProfile: "fastest"), CancellationToken.None);
        var fastest = await service.CaptureAsync(CancellationToken.None);
        Assert.Equal(2, reads);
        Assert.Equal("blocked", fastest.DriverStatus);
        Assert.Equal("device-read-failed", fastest.ErrorCode);
        clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Same(fastest, await service.CaptureAsync(CancellationToken.None));
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await service.CaptureAsync(CancellationToken.None);
        Assert.Equal(3, reads);
    }

    [Fact]
    public async Task ProfileChange_DoesNotCancelAnInFlightCollectorRead()
    {
        var clock = new TestClock();
        var pending = new TaskCompletionSource<SystemSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        CancellationToken readerToken = default;
        await using var service = Create(clock, token =>
        {
            readerToken = token;
            return ++reads == 1 ? new ValueTask<SystemSnapshot>(pending.Task)
                : ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "partial", []));
        });
        await service.SetTrackingAsync(true, CancellationToken.None);

        var changing = service.ConfigureAsync(new(SamplingProfile: "fastest"), CancellationToken.None).AsTask();
        Assert.False(changing.IsCompleted);
        Assert.False(readerToken.IsCancellationRequested);
        pending.SetResult(new SystemSnapshot(clock.GetUtcNow(), "partial", []));
        await changing;
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task AdvancedPreference_DoesNotRequestElevationOrCollect()
    {
        var clock = new TestClock();
        var reads = 0;
        await using var service = Create(clock, _ =>
        {
            reads++;
            return ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "partial", [], "available"));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnableAdvancedAsync(CancellationToken.None));
        await service.ConfigureAsync(new(UseAdvancedSensors: true), CancellationToken.None);
        Assert.Equal(0, reads);
        Assert.Equal("available", (await service.CaptureAsync(CancellationToken.None)).DriverStatus);
        await service.ConfigureAsync(new(Enabled: false, UseAdvancedSensors: true), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnableAdvancedAsync(CancellationToken.None));
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task InvalidConfiguration_IsRejectedWithoutChangingTheService()
    {
        var clock = new TestClock();
        await using var service = Create(clock, _ => ValueTask.FromResult(new SystemSnapshot(clock.GetUtcNow(), "partial", [])));
        await service.ConfigureAsync(new(Enabled: false), CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfigureAsync(new(SamplingProfile: "invalid"), CancellationToken.None).AsTask());
        Assert.Equal("disabled", (await service.CaptureAsync(CancellationToken.None)).Status);
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
