using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace TrackMeUp.Services;

/// <summary>
/// Captures machine telemetry used by AI analysis and local reporting.
/// </summary>
public sealed class SystemSnapshotService
{
    private const long BytesPerMegabyte = 1024L * 1024L;

    private readonly Dictionary<string, NetworkTrafficSample> _networkSamples = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _previousNetworkAt;
    private ulong _cpuIdlePrevious;
    private ulong _cpuKernelPrevious;
    private ulong _cpuUserPrevious;
    private bool _hasCpuHistory;

    /// <summary>
    /// Captures a full telemetry point for the current machine.
    /// </summary>
    public SystemSnapshot Capture()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new SystemSnapshot(
            now,
            CaptureCpuUsagePercent(),
            ReadCpuTemperature(),
            ReadGpuTemperature(),
            ReadGpuUsagePercent(),
            MemoryUsedMb(),
            MemoryTotalMb(),
            ReadGpuMemoryUsedMb(),
            ReadNetworkState(now),
            ReadDiskState());

        return snapshot;
    }

    /// <summary>
    /// Reads CPU usage percentage from OS kernel statistics.
    /// </summary>
    private int CaptureCpuUsagePercent()
    {
        if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return 0;
        }

        var idle = FromFileTime(idleTime);
        var kernel = FromFileTime(kernelTime);
        var user = FromFileTime(userTime);

        if (!_hasCpuHistory)
        {
            _cpuIdlePrevious = idle;
            _cpuKernelPrevious = kernel;
            _cpuUserPrevious = user;
            _hasCpuHistory = true;
            return 0;
        }

        var idleDelta = idle - _cpuIdlePrevious;
        var kernelDelta = kernel - _cpuKernelPrevious;
        var userDelta = user - _cpuUserPrevious;
        var totalDelta = kernelDelta + userDelta;

        _cpuIdlePrevious = idle;
        _cpuKernelPrevious = kernel;
        _cpuUserPrevious = user;

        if (totalDelta <= 0)
        {
            return 0;
        }

        var busyPercent = (1.0 - (idleDelta / (double)totalDelta)) * 100.0;
        if (busyPercent < 0)
        {
            return 0;
        }

        if (busyPercent > 100)
        {
            return 100;
        }

        return (int)Math.Round(busyPercent);
    }

    /// <summary>
    /// Converts a FILETIME structure into an unsigned tick counter.
    /// </summary>
    private static ulong FromFileTime(NativeMethods.FileTime time)
        => ((ulong)time.HighDateTime << 32) | (uint)time.LowDateTime;

    /// <summary>
    /// Reads CPU temperature if exposed by ACPI thermal zones.
    /// </summary>
    private static int? ReadCpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            foreach (var item in searcher.Get().OfType<ManagementObject>())
            {
                if (item["CurrentTemperature"] is null)
                {
                    continue;
                }

                var celsius = ConvertTemperatureToCelsius(item["CurrentTemperature"]!);
                if (celsius is >= -20 and <= 200)
                {
                    return celsius;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>
    /// Reads first available GPU temperature exposed via WMI.
    /// </summary>
    private static int? ReadGpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (var item in searcher.Get().OfType<ManagementObject>())
            {
                if (item["CurrentTemperature"] is null)
                {
                    continue;
                }

                var celsius = ConvertTemperatureToCelsius(item["CurrentTemperature"]!);
                if (celsius is >= -20 and <= 200)
                {
                    return celsius;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    /// <summary>
    /// Converts ACPI-style temperatures to Celsius.
    /// </summary>
    private static int? ConvertTemperatureToCelsius(object temperatureValue)
    {
        try
        {
            var value = Convert.ToInt32(temperatureValue);
            var tenthsKelvin = value > 1000 ? value / 10.0 : value;
            return (int)Math.Round(tenthsKelvin - 273.15);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads GPU utilization from performance counters.
    /// </summary>
    private static int? ReadGpuUsagePercent()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
            {
                return null;
            }

            var category = new PerformanceCounterCategory("GPU Engine");
            var totalUsage = 0d;

            foreach (var instance in category.GetInstanceNames())
            {
                if (!IsGpuUsableInstance(instance))
                {
                    continue;
                }

                try
                {
                    using var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                    totalUsage += counter.NextValue();
                }
                catch
                {
                }
            }

            var bounded = Math.Max(0d, Math.Min(100d, totalUsage));
            return (int)Math.Round(bounded);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps only GPU engine counter instances linked to render/compute pipelines.
    /// </summary>
    private static bool IsGpuUsableInstance(string instanceName)
    {
        return instanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)
            || instanceName.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase)
            || instanceName.Contains("engtype_Copy", StringComparison.OrdinalIgnoreCase)
            || instanceName.Contains("engtype_VideoDecode", StringComparison.OrdinalIgnoreCase)
            || instanceName.Contains("engtype_VideoEncode", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads dedicated GPU memory from performance counters when available.
    /// </summary>
    private static int? ReadGpuMemoryUsedMb()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Adapter Memory"))
            {
                return null;
            }

            var category = new PerformanceCounterCategory("GPU Adapter Memory");
            var totalUsage = 0d;

            foreach (var instance in category.GetInstanceNames())
            {
                using var counter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instance, true);
                var bytes = counter.NextValue();
                if (!double.IsNaN(bytes) && !double.IsInfinity(bytes) && bytes > 0)
                {
                    totalUsage += bytes;
                }
            }

            if (totalUsage <= 0)
            {
                return null;
            }

            return (int)Math.Round(totalUsage / BytesPerMegabyte);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads total RAM in MB.
    /// </summary>
    private static long MemoryTotalMb()
    {
        var memoryStatus = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref memoryStatus))
        {
            return 0;
        }

        return (long)(memoryStatus.TotalPhysicalMemory / BytesPerMegabyte);
    }

    /// <summary>
    /// Reads used RAM in MB.
    /// </summary>
    private static long MemoryUsedMb()
    {
        var memoryStatus = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref memoryStatus))
        {
            return 0;
        }

        return (long)((memoryStatus.TotalPhysicalMemory - memoryStatus.AvailablePhysicalMemory) / BytesPerMegabyte);
    }

    /// <summary>
    /// Reads network rates by comparing the active interface samples with the previous run.
    /// </summary>
    private NetworkSnapshotState ReadNetworkState(DateTimeOffset now)
    {
        // Keep snapshots only for active interfaces; ignore loopback/tunnel adapters.
        var sample = ReadNetworkTrafficBytes();
        var elapsedSeconds = _previousNetworkAt is null
            ? 0d
            : Math.Max(1d, (now - _previousNetworkAt.Value).TotalSeconds);

        long upload = 0;
        long download = 0;

        foreach (var current in sample)
        {
            if (!_networkSamples.TryGetValue(current.Key, out var previous))
            {
                continue;
            }

            var uploadDelta = current.Value.BytesSent - previous.BytesSent;
            var downloadDelta = current.Value.BytesReceived - previous.BytesReceived;
            if (uploadDelta > 0)
            {
                upload += uploadDelta;
            }

            if (downloadDelta > 0)
            {
                download += downloadDelta;
            }
        }

        var uploadPerSecond = elapsedSeconds > 0 ? (long)Math.Max(0, upload / elapsedSeconds) : 0;
        var downloadPerSecond = elapsedSeconds > 0 ? (long)Math.Max(0, download / elapsedSeconds) : 0;

        _networkSamples.Clear();
        foreach (var entry in sample)
        {
            _networkSamples[entry.Key] = entry.Value;
        }

        _previousNetworkAt = now;
        return new NetworkSnapshotState(uploadPerSecond, downloadPerSecond);
    }

    /// <summary>
    /// Reads network RX/TX byte counters for active adapters.
    /// </summary>
    private static Dictionary<string, NetworkTrafficSample> ReadNetworkTrafficBytes()
    {
        var result = new Dictionary<string, NetworkTrafficSample>(StringComparer.OrdinalIgnoreCase);
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var stats = networkInterface.GetIPv4Statistics();
            result[networkInterface.Id] = new NetworkTrafficSample(stats.BytesReceived, stats.BytesSent);
        }

        return result;
    }

    /// <summary>
    /// Reads fixed local disks status.
    /// </summary>
    private static IReadOnlyList<DiskSnapshotState> ReadDiskState()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(drive => new DiskSnapshotState(
                    drive.Name,
                    drive.DriveFormat,
                    drive.TotalSize,
                    drive.TotalFreeSpace))
                .ToList();
        }
        catch
        {
            return Array.Empty<DiskSnapshotState>();
        }
    }
}

internal readonly record struct NetworkTrafficSample(long BytesReceived, long BytesSent);
