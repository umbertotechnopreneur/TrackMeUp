// SPDX-License-Identifier: MIT

using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using TrackMeUp.Services;

namespace TrackMeUp.Hardware;

internal sealed class LibreHardwareTelemetryCollector : IDisposable
{
    private readonly bool _advanced;
    private readonly Dictionary<IHardware, CachedDeviceReading> _deviceCache = new();
    private Computer? _computer;

    internal LibreHardwareTelemetryCollector(bool advanced)
    {
        _advanced = advanced;
        DriverStatus = ReadDriverStatus(advanced);
        PawnIo.IsDriverAccessEnabled = advanced && DriverStatus == "active";
    }

    internal string DriverStatus { get; }

    internal async Task<SystemSnapshot> SampleAsync(HardwareSamplingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var started = DateTimeOffset.UtcNow;
        if (_computer is null)
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true,
                IsBatteryEnabled = true,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsPsuEnabled = false,
                IsPowerMonitorEnabled = false
            };
            _computer.Open();
            // Delta-based load and throughput sensors need a baseline before the first snapshot.
            foreach (var hardware in _computer.Hardware.Where(hardware => hardware.HardwareType != HardwareType.Storage))
            {
                try { Update(hardware); }
                catch { /* The actual sample reports device failures independently after warm-up. */ }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }

        var devices = new List<HardwareDeviceSnapshot>();
        var hadError = false;
        foreach (var hardware in _computer.Hardware)
        {
            if (_deviceCache.TryGetValue(hardware, out var cached)
                && !profile.IsSampleDue(hardware.HardwareType.ToString(), cached.SampledAt, DateTimeOffset.UtcNow))
            {
                // A profile change reuses device readings until due under the new rate, including failures.
                // Cached values retain their original timestamps; reusing them never suggests a fresh read.
                devices.AddRange(cached.Devices);
                hadError |= cached.HadError;
                continue;
            }
            var versions = TrackMeUpSensorAccess.BeginUpdate(hardware);
            var deviceError = false;
            try { Update(hardware); }
            catch { deviceError = true; /* This device remains explicitly null/partial; other devices are independent. */ }
            var current = new List<HardwareDeviceSnapshot>();
            var sampledAt = DateTimeOffset.UtcNow;
            AppendDevice(hardware, current, versions, sampledAt);
            devices.AddRange(current);
            _deviceCache[hardware] = new CachedDeviceReading(sampledAt, current.AsReadOnly(), deviceError);
            hadError |= deviceError;
        }
        var usable = devices.Sum(device => device.Sensors.Count(sensor => sensor.Value is not null));
        var missing = devices.Any(device => device.Sensors.Any(sensor => sensor.Value is null));
        var status = usable == 0 ? "unavailable" : hadError || missing || DriverStatus != "active" ? "partial" : "ready";
        var snapshot = new SystemSnapshot(DateTimeOffset.UtcNow, status, Array.AsReadOnly(devices.ToArray()),
            DriverStatus, hadError ? "device-read-failed" : null, started);
        SystemSnapshotValidator.Validate(snapshot);
        return snapshot;
    }

    private void AppendDevice(IHardware hardware, List<HardwareDeviceSnapshot> devices, IReadOnlyDictionary<ISensor, long> versions, DateTimeOffset sampledAt)
    {
        var readings = new List<HardwareSensorSnapshot>();
        foreach (var sensor in hardware.Sensors)
        {
            var staticMetadata = hardware.HardwareType == HardwareType.Battery
                && (sensor.SensorType == SensorType.Energy && sensor.Index is 0 or 1 || sensor.SensorType == SensorType.Level && sensor.Index == 1)
                || hardware.HardwareType == HardwareType.GpuAmd && sensor.SensorType == SensorType.SmallData && sensor.Name == "GPU Memory Total";
            var raw = TrackMeUpSensorAccess.ReadUpdatedValue(sensor, versions, staticMetadata);
            double? value = raw is { } number && float.IsFinite(number) ? number : null;
            if (hardware.HardwareType == HardwareType.Cpu && (!_advanced || DriverStatus != "active") && sensor.SensorType != SensorType.Load)
                value = null;
            readings.Add(new HardwareSensorSnapshot(sensor.Identifier.ToString(), sensor.Name,
                sensor.SensorType.ToString(), Unit(sensor.SensorType), value));
        }
        devices.Add(new HardwareDeviceSnapshot(hardware.Identifier.ToString(), hardware.Name, hardware.HardwareType.ToString(),
            sampledAt, Array.AsReadOnly(readings.ToArray())));
        foreach (var child in hardware.SubHardware) AppendDevice(child, devices, versions, sampledAt);
    }

    private static void Update(IHardware hardware)
    {
        hardware.Update();
        foreach (var child in hardware.SubHardware) Update(child);
    }

    private static string Unit(SensorType type) => type switch
    {
        SensorType.Voltage => "V",
        SensorType.Current => "A",
        SensorType.Power => "W",
        SensorType.Clock => "MHz",
        SensorType.Temperature => "°C",
        SensorType.Load or SensorType.Control or SensorType.Level or SensorType.Humidity => "%",
        SensorType.Frequency => "Hz",
        SensorType.Fan => "RPM",
        SensorType.Flow => "L/h",
        SensorType.Factor => "1",
        SensorType.Data => "GiB",
        SensorType.SmallData => "MiB",
        SensorType.Throughput => "B/s",
        SensorType.TimeSpan => "s",
        SensorType.Timing => "ns",
        SensorType.Energy => "mWh",
        SensorType.Noise => "dBA",
        SensorType.Conductivity => "µS/cm",
        _ => throw new InvalidDataException("Unsupported sensor unit.")
    };

    private static string ReadDriverStatus(bool advanced)
    {
        if (!PawnIo.IsInstalled) return "not-installed";
        if (!advanced) return "available";
        try
        {
            using var handle = File.OpenHandle(@"\\?\GLOBALROOT\Device\PawnIO", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            return "active";
        }
        catch (UnauthorizedAccessException) { return "access-denied"; }
        catch (IOException) { return "blocked"; }
    }

    /// <summary>Closes library device handles without changing any hardware controls.</summary>
    public void Dispose()
    {
        Program.BeginNativeOperation();
        _computer?.Close();
    }

    private sealed record CachedDeviceReading(DateTimeOffset SampledAt, IReadOnlyList<HardwareDeviceSnapshot> Devices, bool HadError);
}
