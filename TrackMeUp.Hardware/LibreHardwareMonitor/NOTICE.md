# LibreHardwareMonitor telemetry integration

This helper uses LibreHardwareMonitor 0.9.6, source commit
`3d331e3370efb858411f19511373eff65a218701`, under the Mozilla Public License 2.0.

- Original source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701
- License: https://www.mozilla.org/MPL/2.0/
- The source archive is verified against SHA-256
  `5A83EE3F504A85EFB6AFEE4112447E60CACA1B7EC2E2D71F4651570B8B9B4230`.
- The build substitutes the accompanying MPL-licensed `MemoryGroup.cs` to expose
  OS memory counters without enumerating DIMM SPD/SMBus. DIMM sensor and
  RAMSPDToolkit driver sources and the RAMSPDToolkit package are excluded.
- The build applies small, source-checked patches: the PawnIO wrapper requires
  explicit opt-in and reports failed operations as failures; sensor assignments
  carry a sequence counter so cached dynamic values are not relabeled as fresh.
  The exact patches are in scripts/Restore-LibreHardwareMonitorSource.ps1. The
  first-party TrackMeUpSensorAccess.cs adapter is MIT-licensed. Static battery
  capacities and AMD VRAM capacity retain their documented metadata semantics.
- When low-level access is unavailable, AMD CPUs use the upstream GenericCpu
  implementation for load sensors, avoiding constructors that require PawnIO.
- NVIDIA utilization, memory, power and D3D load sensors use separate index ranges.
  This prevents duplicate identifiers, including on GPUs with missing utilization
  domains. The modified `NvidiaGpu.cs` is included with the helper's source notices.
- All other upstream sources retain their original notices. The build definition
  and replacement source live in TrackMeUp.Hardware/LibreHardwareMonitor.
- No PawnIO driver or installer is included, downloaded at runtime, or installed.
  Optional advanced readings use the separately installed official signed PawnIO
  distribution: https://pawnio.eu/ . PawnIO has its own license and IOCTL exception:
  https://github.com/namazso/PawnIO#license .
- Embedded upstream PawnIO modules retain their original licenses; upstream
  module sources: https://github.com/namazso/PawnIO.Modules .

This build disables motherboard, external controllers and PSU discovery. That is
not a universal guarantee against vendor GPU-internal I2C operations. It never
invokes fan, voltage, clock, power-limit or other control setters.
