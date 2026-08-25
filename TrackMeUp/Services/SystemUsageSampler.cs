using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrackMeUp.Services;

/// <summary>Contains only the CPU/GPU utilization values used by the live activity score.</summary>
public sealed record SystemUsageSample(
    DateTimeOffset Timestamp,
    int? CpuUsagePercent,
    int? GpuUsagePercent);

/// <summary>Samples lightweight system utilization without collecting full diagnostics.</summary>
public interface ISystemUsageSampler : IAsyncDisposable
{
    /// <summary>Captures one CPU/GPU utilization sample, or null when all supported counters are unavailable.</summary>
    ValueTask<SystemUsageSample?> CaptureAsync(CancellationToken cancellationToken);
}

/// <summary>Uses kernel CPU times and cached GPU engine counters for score telemetry.</summary>
public sealed class SystemUsageSampler : ISystemUsageSampler
{
    private static readonly TimeSpan GpuRefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GpuRetryInterval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly List<PerformanceCounter> _gpuCounters = [];
    private DateTimeOffset _nextGpuRefreshAt = DateTimeOffset.MinValue;
    private ulong _cpuIdlePrevious;
    private ulong _cpuKernelPrevious;
    private ulong _cpuUserPrevious;
    private bool _hasCpuHistory;
    private bool _disposed;

    /// <inheritdoc />
    public async ValueTask<SystemUsageSample?> CaptureAsync(CancellationToken cancellationToken)
    {
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        await _captureGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => CaptureCore(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _captureGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_stateGate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                DisposeGpuCounters();
            }
        }
        finally
        {
            _captureGate.Release();
            _captureGate.Dispose();
        }
    }

    private SystemUsageSample? CaptureCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cpu = CaptureCpuUsagePercent();
        cancellationToken.ThrowIfCancellationRequested();
        var gpu = CaptureGpuUsagePercent();
        if (cpu is null && gpu is null)
        {
            return null;
        }

        return new SystemUsageSample(DateTimeOffset.UtcNow, cpu, gpu);
    }

    private int? CaptureCpuUsagePercent()
    {
        if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return null;
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

        if (idle < _cpuIdlePrevious || kernel < _cpuKernelPrevious || user < _cpuUserPrevious)
        {
            _cpuIdlePrevious = idle;
            _cpuKernelPrevious = kernel;
            _cpuUserPrevious = user;
            return null;
        }

        var idleDelta = idle - _cpuIdlePrevious;
        var totalDelta = (kernel - _cpuKernelPrevious) + (user - _cpuUserPrevious);
        _cpuIdlePrevious = idle;
        _cpuKernelPrevious = kernel;
        _cpuUserPrevious = user;
        if (totalDelta == 0)
        {
            return null;
        }

        var busyPercent = (1d - (idleDelta / (double)totalDelta)) * 100d;
        return (int)Math.Round(Math.Clamp(busyPercent, 0d, 100d));
    }

    private int? CaptureGpuUsagePercent()
    {
        lock (_stateGate)
        {
            if (DateTimeOffset.UtcNow >= _nextGpuRefreshAt || _gpuCounters.Count == 0)
            {
                RefreshGpuCounters();
            }

            if (_gpuCounters.Count == 0)
            {
                return null;
            }

            var totalUsage = 0d;
            var hasValue = false;
            var refreshNeeded = false;
            foreach (var counter in _gpuCounters)
            {
                try
                {
                    var value = counter.NextValue();
                    if (!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0)
                    {
                        totalUsage += value;
                        hasValue = true;
                    }
                }
                catch
                {
                    refreshNeeded = true;
                }
            }

            if (refreshNeeded)
            {
                _nextGpuRefreshAt = DateTimeOffset.UtcNow.Add(GpuRetryInterval);
            }

            return hasValue
                ? (int)Math.Round(Math.Clamp(totalUsage, 0d, 100d))
                : null;
        }
    }

    private void RefreshGpuCounters()
    {
        var now = DateTimeOffset.UtcNow;
        var candidates = new List<PerformanceCounter>();
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
            {
                DisposeGpuCounters();
                _nextGpuRefreshAt = now.Add(GpuRetryInterval);
                return;
            }

            var category = new PerformanceCounterCategory("GPU Engine");
            foreach (var instance in category.GetInstanceNames())
            {
                if (!IsGpuUsableInstance(instance))
                {
                    continue;
                }

                var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                try
                {
                    _ = counter.NextValue();
                    candidates.Add(counter);
                }
                catch
                {
                    counter.Dispose();
                }
            }

            DisposeGpuCounters();
            _gpuCounters.AddRange(candidates);
            _nextGpuRefreshAt = now.Add(_gpuCounters.Count == 0 ? GpuRetryInterval : GpuRefreshInterval);
        }
        catch
        {
            foreach (var counter in candidates)
            {
                counter.Dispose();
            }

            _nextGpuRefreshAt = now.Add(GpuRetryInterval);
        }
    }

    private void DisposeGpuCounters()
    {
        foreach (var counter in _gpuCounters)
        {
            counter.Dispose();
        }

        _gpuCounters.Clear();
    }

    private static bool IsGpuUsableInstance(string instanceName) =>
        instanceName.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)
        || instanceName.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase)
        || instanceName.Contains("engtype_Copy", StringComparison.OrdinalIgnoreCase)
        || instanceName.Contains("engtype_VideoDecode", StringComparison.OrdinalIgnoreCase)
        || instanceName.Contains("engtype_VideoEncode", StringComparison.OrdinalIgnoreCase);

    private static ulong FromFileTime(NativeMethods.FileTime time) =>
        ((ulong)time.HighDateTime << 32) | (uint)time.LowDateTime;

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct FileTime
        {
            internal readonly int LowDateTime;
            internal readonly int HighDateTime;
        }
    }
}
