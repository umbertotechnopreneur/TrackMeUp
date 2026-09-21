// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware;

/// <summary>Prevents cached upstream values from being reported as fresh after a failed update.</summary>
public static class WorkTrailSensorAccess
{
    /// <summary>Records assignment counters without discarding static capacities used by subsequent calculations.</summary>
    public static IReadOnlyDictionary<ISensor, long> BeginUpdate(IHardware hardware)
    {
        var versions = new Dictionary<ISensor, long>();
        Capture(hardware, versions);
        return versions;
    }

    /// <summary>Returns a value only if assigned during this poll, or explicitly classified as static device metadata.</summary>
    public static float? ReadUpdatedValue(ISensor sensor, IReadOnlyDictionary<ISensor, long> versions, bool staticMetadata)
    {
        if (sensor is not Sensor value) return null;
        return staticMetadata || !versions.TryGetValue(sensor, out var before) || value.WorkTrailUpdateSequence != before ? sensor.Value : null;
    }

    private static void Capture(IHardware hardware, Dictionary<ISensor, long> versions)
    {
        foreach (var sensor in hardware.Sensors)
        {
            sensor.ValuesTimeWindow = System.TimeSpan.Zero;
            if (sensor is Sensor value) versions[sensor] = value.WorkTrailUpdateSequence;
        }
        foreach (var child in hardware.SubHardware) Capture(child, versions);
    }
}
