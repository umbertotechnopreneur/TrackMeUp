# Hardware telemetry

TrackMeUp uses one application-owned `HardwareTelemetryService` and one isolated
LibreHardwareMonitor collector. The shared `SystemSnapshot` model is used by
capture, persistence, diagnostics, screenshot details, CLI output and AI context.
By default each capture stores its available snapshot even when AI is disabled.
Reanalysis uses that saved snapshot; it does not substitute the machine's current
readings. Activity interval averages remain separate from capture-time readings.

## Settings and saved data

The shared settings catalog exposes four independent preferences, with changes
applied without an application restart:

| Public key | Default | Meaning |
| --- | --- | --- |
| `sensors.enabled` | `true` | Enable hardware sensor collection |
| `sensors.advanced` | `false` | Opt into the optional advanced-sensor helper, subject to explicit administrator consent |
| `sensors.save_snapshots` | `true` | Save raw hardware readings with future captures |
| `sensors.sampling_profile` | `normal` | Select `slow`, `normal`, `fast` or `fastest` polling |

Disabling sensors stops collection without clearing the advanced, save or profile
preferences; those preferences are inactive while sensors are disabled. Unknown
sampling profile identifiers are rejected, including when loaded from settings.

With snapshot saving disabled, a fresh reading can still be used in memory for
immediate AI analysis, but raw sensor readings are not retained in the capture
database or in stored AI results. Deferred analysis and reanalysis cannot recover
an unsaved reading and do not poll current hardware as a substitute. Existing
saved snapshots remain unchanged; disabling saving is not a historical-data
deletion action. Interval CPU/GPU usage aggregates are a separate data stream and
are not controlled by the raw-snapshot saving preference.

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

Tracking enables a shared polling loop using the selected profile. Other
consumers reuse its recent immutable snapshot or request a bounded on-demand
reading. Intervals are in seconds:

| Profile | CPU/GPU | Memory | Battery | Storage | Network |
| --- | ---: | ---: | ---: | ---: | ---: |
| `slow` | 10 | 30 | 60 | 120 | 10 |
| `normal` (default) | 2 | 10 | 30 | 60 | 2 |
| `fast` | 1 | 5 | 10 | 30 | 1 |
| `fastest` | 0.5 | 2 | 5 | 30 | 0.5 |

Faster polling increases collector work and does not change the screenshot
schedule. Reused readings retain their original device timestamp. Missing or
failed telemetry remains explicit and does not prevent screenshot capture.

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

## Optional PawnIO

Standard mode explicitly disables PawnIO access, including when the application
was started with elevated privileges. TrackMeUp neither downloads nor installs a
kernel driver. Install the [official signed PawnIO distribution](https://pawnio.eu/)
separately if desired, then use the UI's advanced-sensor action to request Windows
administrator consent for the sensor helper. The application UI remains at its
existing privilege level. Missing prerequisites are reported before requesting
consent; a failed or disconnected advanced session does not trigger automatic UAC
prompts. No fan, voltage, clock or power-limit controls are exposed.

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
