// SPDX-License-Identifier: MIT

using System.Globalization;
using TrackMeUp.Services;

namespace TrackMeUp.Presentation;

/// <summary>Contains an already-formatted hardware category summary.</summary>
public sealed record HardwareSummaryRow(string Label, string Value);

/// <summary>Contains inert hardware text rendered by capture and diagnostics surfaces.</summary>
public sealed record HardwareSnapshotViewState(
    string Status,
    string CollectedAt,
    string DriverStatus,
    IReadOnlyList<HardwareSummaryRow> Summary,
    IReadOnlyList<string> Details,
    bool HasData = false);

/// <summary>Formats captured sensor DTOs without querying hardware or estimating missing measurements.</summary>
public static class HardwareSnapshotProjection
{
    /// <summary>Projects a historical or current collection using supplied localized labels.</summary>
    public static HardwareSnapshotViewState Create(
        SystemSnapshot? snapshot,
        CultureInfo culture,
        Func<string, string> translate)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(translate);
        var rows = new List<HardwareSummaryRow>();
        foreach (var category in new[] { "Cpu", "Gpu", "Memory", "Storage", "Battery", "Network" })
        {
            var devices = snapshot?.Devices.Where(device => IsCategory(device.Kind, category)).ToArray() ?? [];
            if (!devices.Any(device => device.Sensors.Any(sensor => sensor.Value.HasValue)))
            {
                continue;
            }

            var missing = translate("Common.NotAvailable");
            var values = devices.Select(device => $"{device.Name}: {Summarize(device, culture, missing)}");
            rows.Add(new HardwareSummaryRow(
                translate("Hardware.Category." + category),
                string.Join("\n", values)));
        }

        if (snapshot is null)
        {
            // Historical captures without telemetry never receive a live read or a placeholder hardware section.
            return new HardwareSnapshotViewState(string.Empty, string.Empty, string.Empty, rows, []);
        }

        var details = new List<string>();
        foreach (var device in snapshot.Devices)
        {
            var sensors = device.Sensors.Where(sensor => sensor.Value.HasValue).ToArray();
            if (sensors.Length == 0)
            {
                continue;
            }

            details.Add($"{device.Name} · {device.Kind} · {string.Format(culture, translate("Hardware.DeviceUpdated"), device.SampledAt.ToLocalTime().ToString("G", culture))}");
            foreach (var sensor in sensors)
            {
                details.Add($"  {sensor.Name} · {sensor.Kind}: {FormatSensor(sensor, culture, translate("Common.NotAvailable"))}");
            }
        }

        var status = TranslateStatus(snapshot.Status, translate);
        if (snapshot.ErrorCode is { Length: > 0 } code)
        {
            status += $" ({code})";
        }

        return new HardwareSnapshotViewState(
            status,
            string.Format(culture, translate("Hardware.CollectedAt"), snapshot.Timestamp.ToLocalTime().ToString("G", culture)),
            $"{translate("Hardware.Advanced.Label")}: {TranslateStatus(snapshot.DriverStatus, translate)}",
            rows,
            details,
            details.Count > 0);
    }

    private static bool IsCategory(string kind, string category) => category == "Gpu"
        ? kind is "GpuNvidia" or "GpuAmd" or "GpuIntel"
        : string.Equals(kind, category, StringComparison.Ordinal);

    private static string Summarize(HardwareDeviceSnapshot device, CultureInfo culture, string missing)
    {
        var preferredKinds = device.Kind switch
        {
            "Battery" => new[] { "Level", "Power", "Energy", "TimeSpan", "Temperature" },
            "Memory" => ["Load", "Data", "SmallData"],
            "Storage" => ["Temperature", "Load", "Throughput", "Level"],
            "Network" => ["Throughput", "Load"],
            _ => ["Load", "Temperature", "Clock", "Power"]
        };
        var sensors = preferredKinds.SelectMany(kind => device.Sensors
            .Where(sensor => sensor.Kind == kind && sensor.Value.HasValue)
            .OrderByDescending(sensor => sensor.Name is "CPU Total" or "GPU Core" or "Remaining Capacity" ? 3
                : sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("Full Charged", StringComparison.OrdinalIgnoreCase) ? 2
                : sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(sensor => sensor.Id, StringComparer.Ordinal)
            .Take(kind is "Data" or "Throughput" or "Energy" ? 2 : 1)).ToArray();
        if (sensors.Length == 0)
        {
            sensors = device.Sensors
                .Where(sensor => sensor.Value.HasValue)
                .OrderBy(sensor => sensor.Id, StringComparer.Ordinal)
                .Take(2)
                .ToArray();
        }
        return sensors.Length == 0
            ? missing
            : string.Join(" · ", sensors.Select(sensor => $"{sensor.Name} {FormatSensor(sensor, culture, missing)}"));
    }

    private static string FormatSensor(HardwareSensorSnapshot sensor, CultureInfo culture, string missing) =>
        sensor.Value is { } value
            ? $"{value.ToString("0.##", culture)} {sensor.Unit}".TrimEnd()
            : missing;

    private static string TranslateStatus(string status, Func<string, string> translate) => status switch
    {
        "ready" => translate("Hardware.Status.Available"),
        "disabled" => translate("Hardware.Status.Disabled"),
        "partial" => translate("Hardware.Status.Partial"),
        "unavailable" => translate("Common.NotAvailable"),
        "error" => translate("Hardware.Status.Error"),
        "starting" => translate("Hardware.Status.Starting"),
        "stale" => translate("Hardware.Status.Stale"),
        "not-installed" => translate("Hardware.Status.NotInstalled"),
        "available" => translate("Hardware.Status.Installed"),
        "active" => translate("Hardware.Status.Active"),
        "access-denied" => translate("Hardware.Status.AccessDenied"),
        "blocked" => translate("Hardware.Status.Blocked"),
        "unsupported" or "unsupported-architecture" => translate("Hardware.Status.Unsupported"),
        _ => status
    };
}
