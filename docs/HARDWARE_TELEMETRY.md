# Device readings

See how your computer was running when a screenshot was taken: processor and
graphics load, memory use, battery, storage, and network activity. Available
readings are saved with captures by default, even with AI off. Later AI analysis
uses those saved readings, so it describes the computer at capture time.

Availability varies by device. Missing readings are shown as unavailable, never
as zero. Native ARM64 sensor collection is currently unsupported; the ARM64 app
itself remains supported.

## Settings and saved data

Choose what to collect and save. Changes take effect without restarting:

| Setting key | Default | What it does |
| --- | --- | --- |
| `sensors.enabled` | `true` | Enable hardware sensor collection |
| `sensors.advanced` | `false` | Restore the advanced-sensor helper at startup when PawnIO is installed, subject to Windows administrator consent |
| `sensors.save_snapshots` | `true` | Save raw hardware readings with future captures |
| `sensors.sampling_profile` | `normal` | Select `slow`, `normal`, `fast` or `fastest` polling |

Turning sensors off stops collection and keeps your other sensor preferences for
next time. Unsupported sampling profiles are rejected.

With snapshot saving off, immediate AI analysis can still use a fresh reading,
but it is not kept in capture records or saved AI results. Later analysis cannot
recover it. Existing snapshots remain saved. CPU/GPU averages over activity
intervals are separate and are not controlled by this setting.

## Available readings

Availability depends on the device, firmware, vendor driver and privileges.
An absent sensor or null reading is unavailable, not zero.

| Category | Readings when available |
| --- | --- |
| CPU | Load, core/package temperatures, clocks, component power and voltage |
| GPU | Load, temperatures, clocks, VRAM, component power, voltage and fan speed |
| Memory | Physical and virtual memory usage and availability; no DIMM SPD discovery |
| Storage | Temperatures, activity, throughput, capacity and supported health counters |
| Battery | Charge level, degradation, design/full/remaining capacity, charge/discharge rate, voltage, temperature and estimated remaining time |
| Network | Upload/download throughput and counters |

Power readings use **W**. Battery energy/capacity uses **mWh**; memory sizes use
**GiB/MiB**. Component power readings can overlap, so their sum is not whole-PC
wall consumption. This feature does not calculate cumulative PC energy or cost.
Motherboard, external-controller, PSU and external-power-monitor groups are
disabled. Vendor GPU APIs may still perform internal I2C operations; this is not
a universal guarantee that no bus access occurs inside a vendor driver.

## Sampling and availability

Choose how often readings refresh while tracking. Faster profiles increase
sensor work without changing the screenshot schedule. Intervals are in seconds:

| Profile | CPU/GPU | Memory | Battery | Storage | Network |
| --- | ---: | ---: | ---: | ---: | ---: |
| `slow` | 10 | 30 | 60 | 120 | 10 |
| `normal` (default) | 2 | 10 | 30 | 60 | 2 |
| `fast` | 1 | 5 | 10 | 30 | 1 |
| `fastest` | 0.5 | 2 | 5 | 30 | 0.5 |

Unavailable readings do not prevent screenshot capture. Reused readings keep
their original time so they are not mistaken for a fresh measurement.

### Details for contributors

One Core `HardwareTelemetryService` shares readings from an isolated
LibreHardwareMonitor collector through `SystemSnapshot`. Capture,
diagnostics, the CLI, and AI use this same model.

`CollectionStartedAt` and `Timestamp` describe the collection window;
`HardwareDeviceSnapshot.SampledAt` describes each device's polling time. They are
not guarantees of a sensor's internal conversion time. Fresh-assignment tracking
prevents unchanged cached dynamic values from being restamped after a failed
library update; static capacities retain their metadata semantics.

The contract supports `ready`, `partial`, `unavailable`, `unsupported`, `error`,
`starting`, `stale` and `disabled` states, with a separate driver status and optional bounded
error code. A partial snapshot remains useful. Native ARM64 telemetry currently
returns explicit `unsupported` status before initializing the collector; ARM64
application builds remain supported.

## Advanced sensors

Some readings need the optional PawnIO driver and administrator consent. Standard
mode leaves driver access off, even if TrackMeUp was started as administrator.
The app never changes fan speed, voltage, clocks, or power limits.

The advanced-sensor action installs the bundled [official signed PawnIO
distribution](https://pawnio.eu/) if needed, then starts the helper. When both
sensor preferences are saved as enabled, the next app startup automatically
starts the helper if a compatible PawnIO version is already installed. There is
no Settings button to press again. Windows can still request administrator
consent for the helper; the main app keeps its existing privileges.

Startup never installs or upgrades the driver. Disabled preferences, unsupported
architectures and missing/older drivers do not trigger elevation. Cancelled
consent or a failed startup attempt is reported as unavailable telemetry, then
basic readings can resume after the ten-second cooldown. That session never
requests consent again automatically; the explicit advanced-sensor action can
retry. A new app session can make one new startup attempt.

The helper uses same-user pipe permissions, reciprocal process-ID checks,
versioned messages and a 512 KiB frame limit. It contains no tracking runtime.

## Pinned source build

LibreHardwareMonitor 0.9.6 is pinned to commit
`3d331e3370efb858411f19511373eff65a218701`. Its archive SHA-256 is
`5A83EE3F504A85EFB6AFEE4112447E60CACA1B7EC2E2D71F4651570B8B9B4230`.
The build downloads and verifies this archive under
`TrackMeUp.Hardware/LibreHardwareMonitor/obj/upstream`; subsequent builds reuse
that cache. No source download occurs at runtime. A checksum or patch mismatch
fails the build.

The small audited source changes separate RAM usage from upstream SPD probing,
make driver access opt-in, preserve CPU load without PawnIO and identify fresh
sensor assignments. The [integration notice](../TrackMeUp.Hardware/LibreHardwareMonitor/NOTICE.md)
records the changes and licenses. The helper includes upstream license notices,
modified upstream sources and the patch script under `HardwareLicenses`.

## Developer verification

Run these commands from the repository root in PowerShell 7:

```powershell
dotnet build .\TrackMeUp.Hardware\TrackMeUp.Hardware.csproj -c Release -p:Platform=x64
dotnet test .\TrackMeUp.Core.Tests\TrackMeUp.Core.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~HardwareTelemetry"
pwsh -NoProfile -File .\scripts\Test-HardwareTelemetry.ps1 -HelperPath .\TrackMeUp.Hardware\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\TrackMeUp.Hardware.exe
```

The smoke test uses ordinary privileges, reads three bounded snapshots and checks
per-category timing across a live profile change. It prints aggregate counts only
and does not install a driver or request elevation. Check
the [manual verification checklist](../README.md) for capture persistence,
battery presentation, reanalysis, import/export and advanced-mode scenarios.
