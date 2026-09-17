// SPDX-License-Identifier: MIT

using System;
using System.IO;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class HardwareTelemetryModelTests
{
    [Fact]
    public void Validation_PreservesZeroAndMissingValuesIncludingBattery()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new SystemSnapshot(now, "partial",
        [
            new("/battery/0", "Battery", "Battery", now,
            [
                new("/battery/0/level/0", "Charge Level", "Level", "%", 0),
                new("/battery/0/power/0", "Discharge Rate", "Power", "W", null)
            ])
        ]);

        SystemSnapshotValidator.Validate(snapshot);
        Assert.Equal(0, snapshot.Devices[0].Sensors[0].Value);
        Assert.Null(snapshot.Devices[0].Sensors[1].Value);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validation_RejectsNonFiniteReadings(double value)
    {
        var snapshot = HardwareTestData.Snapshot(DateTimeOffset.UtcNow, cpu: value);
        Assert.Throws<InvalidDataException>(() => SystemSnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void Validation_RejectsDuplicateDeviceIdentityAndInvalidTime()
    {
        var snapshot = HardwareTestData.Snapshot(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidDataException>(() => SystemSnapshotValidator.Validate(
            snapshot with { Devices = [snapshot.Devices[0], snapshot.Devices[0]] }));
        Assert.Throws<InvalidDataException>(() => SystemSnapshotValidator.Validate(
            snapshot with { CollectionStartedAt = snapshot.Timestamp.AddSeconds(1) }));
    }

    [Fact]
    public void UsageProjection_DoesNotSumOverlappingGpuEnginesOrUseStaleSamples()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new SystemSnapshot(now, "ready",
        [
            new("/gpu/0", "GPU 1", "GpuNvidia", now,
            [
                new("/gpu/0/load/0", "GPU Core", "Load", "%", 60),
                new("/gpu/0/load/1", "GPU Memory Controller", "Load", "%", 90)
            ]),
            new("/gpu/1", "GPU 2", "GpuAmd", now,
                [new("/gpu/1/load/0", "GPU Core", "Load", "%", 40)])
        ]);

        Assert.Equal(60, HardwareUsageProjection.Read(snapshot).Gpu);
        Assert.Null(HardwareUsageProjection.Read(snapshot with { Status = "stale" }).Gpu);
    }
}
