// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using WorkTrail.Application;
using WorkTrail.Services;
using Xunit;

namespace WorkTrail.Presentation.Tests;

public sealed class HardwareSnapshotProjectionTests
{
    [Fact]
    public void CaptureProjection_PreservesBatteryUnitsAndHistoricalSamplingTime()
    {
        var capturedAt = new DateTimeOffset(2026, 9, 12, 10, 30, 0, TimeSpan.Zero);
        var sampledAt = capturedAt.AddSeconds(-30);
        var snapshot = new SystemSnapshot(capturedAt, "partial",
        [
            new("/battery/0", "Portable battery", "Battery", sampledAt,
            [
                new("/battery/0/level/0", "Charge Level", "Level", "%", 72.5),
                new("/battery/0/power/0", "Discharge Rate", "Power", "W", 8.4),
                new("/battery/0/energy/0", "Remaining Capacity", "Energy", "mWh", 43000),
                new("/battery/0/temperature/0", "Temperature", "Temperature", "°C", null)
            ])
        ]);
        var strings = new LocalizationService("it-IT");
        var item = new ScreenshotGalleryItem(capturedAt, "frame.webp", "Editor", "monitor", "manual", HardwareSnapshot: snapshot);

        var state = ScreenshotDetailsProjection.Create(item, strings.Culture, "Schermo", "Manuale", "--", strings.Translate);

        var battery = Assert.Single(state.Hardware.Summary, row => row.Label == "Batteria");
        Assert.Contains("72,5 %", battery.Value, StringComparison.Ordinal);
        Assert.Contains("8,4 W", battery.Value, StringComparison.Ordinal);
        Assert.Contains("43000 mWh", battery.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Temperature", battery.Value, StringComparison.Ordinal);
        Assert.Contains(state.Hardware.Details, line => line.Contains(sampledAt.ToLocalTime().ToString("G", strings.Culture), StringComparison.Ordinal));
        Assert.DoesNotContain(state.Hardware.Details, line => line.Contains("Temperature", StringComparison.Ordinal));
        Assert.Equal(strings.Translate("Hardware.Status.Partial"), state.Hardware.Status);
        Assert.Equal("--", state.CpuUsage);
    }

    [Fact]
    public void Projection_HidesHardwareWhenNoMeasurementsExist()
    {
        var strings = new LocalizationService("en-US");
        var collected = HardwareSnapshotProjection.Create(new SystemSnapshot(DateTimeOffset.UtcNow, "ready", []), strings.Culture, strings.Translate);
        var historical = HardwareSnapshotProjection.Create(null, strings.Culture, strings.Translate);

        Assert.False(collected.HasData);
        Assert.False(historical.HasData);
        Assert.Empty(collected.Summary);
        Assert.Empty(historical.Summary);
        Assert.Empty(historical.Details);
    }

    [Fact]
    public void Projection_DoesNotSumOverlappingPowerSensorsAndShowsStaleStatus()
    {
        var sampledAt = new DateTimeOffset(2026, 9, 12, 10, 30, 0, TimeSpan.Zero);
        var strings = new LocalizationService("en-US");
        var snapshot = new SystemSnapshot(sampledAt.AddMinutes(5), "stale",
        [
            new("/cpu/0", "CPU", "Cpu", sampledAt,
            [
                new("/cpu/0/power/0", "CPU Package", "Power", "W", 20),
                new("/cpu/0/power/1", "CPU Cores", "Power", "W", 15)
            ])
        ]);

        var result = HardwareSnapshotProjection.Create(snapshot, strings.Culture, strings.Translate);

        Assert.Equal(strings.Translate("Hardware.Status.Stale"), result.Status);
        Assert.Contains("CPU Package 20 W", result.Summary[0].Value, StringComparison.Ordinal);
        Assert.DoesNotContain("35 W", result.Summary[0].Value, StringComparison.Ordinal);
        Assert.Contains(result.Details, line => line.Contains("CPU Cores", StringComparison.Ordinal));
    }
}
