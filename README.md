<p align="center">
  <img src="TrackMeUp/Assets/TrackMeUpSquare150Logo.png" alt="TrackMeUp app icon" width="150" />
</p>

# TrackMeUp

[![Build](https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml)

TrackMeUp is a privacy-first Windows activity monitor presented as a compact, positionable Mica player flyout. It measures active and idle time, keyboard press counts, mouse clicks, foreground applications, and document or window context without recording typed content.

## MVP features

- Local activity samples every five seconds with daily application totals.
- Keyboard and mouse event counts; pressed keys and text are never stored.
- Extensible application context providers with dedicated Word and Excel document detection.
- Built-in mappings for Visual Studio Code, Visual Studio, Edge, Chrome, Windows Terminal, and ChatGPT.
- Five flyout positions: bottom-center (default), bottom corners, and top corners.
- A discreet taskbar control: app icon to open the flyout, play/pause for tracking, and a camera-style status LED that is grey when paused and gently red-pulsing while recording.
- Light, dark, and system-following theme support.
- Three-dot menu for App Options, Reports, OpenAI integration, screenshot capture, and About.
- A dedicated Mica Reports window renders bundled Vue, Vuetify, and ECharts views for calendar days, weekday/hour patterns, trends, and applications without a local HTTP server or network dependency.
- Reports supports system, light, and dark themes through the same validated application setting used by the native shell, preserves the selection between launches, and exposes searchable tabular data alongside each chart.
- Options rendered inside the same flyout, including flyout/taskbar-control position, screenshot archive folder, reporting, and OpenAI configuration.
- A scrollable Tools and Diagnostics surface exposes runtime health, system snapshots, capture and AI actions, focus sessions, reports, privacy rules, confirmed retention cleanup, and context plugins through the same application facade used by the CLI.
- Expandable latest-session details with an optional primary-monitor screenshot preview and local-folder shortcut.
- Optional screenshot analysis through OpenAI Responses, Anthropic Messages, or OpenRouter, with one compressed WebP capture per monitor and compact, balanced, or detailed output profiles.
- Configurable OpenAI model and reasoning effort (`auto`, `none`, `low`, `medium`, `high`, `xhigh`, or `max`) with provider-safe fallbacks.
- Local AI-usage telemetry links each provider request to its snapshot with a correlation ID and records sanitized request metadata, token usage, latency, response IDs, and provider-reported cost when available. Prompts, images, headers, and keys are never copied to this telemetry store.
- Optional additional AI instructions, weekly active-hours/break notes, and device context (time zone, Windows/UI and input languages, plus opt-in Windows Location coordinates with provenance) are included only in the analysis snapshot context.
- Screenshots are retained only when explicitly enabled; otherwise temporary WebP images are deleted after analysis.
- Activity history and AI analyses are stored only in the current versioned SQLite database under the current user's local application data directory. A legacy `activity.jsonl` or `analyses.jsonl` file, malformed settings, or incompatible schema fails immediately; TrackMeUp does not migrate, recover, or fall back.
- The OpenAI key is set as the Windows user environment variable `OPENAI_API_KEY`; TrackMeUp does not store it in files, the database, or Credential Locker.
- A daily HTML report can be generated locally from the options flyout.

Current limitation: browser tab titles are available from the active window. Reliable URLs and page-level context require a future opt-in browser extension or a UI Automation provider.

## Extending detailed application tracking

Implement `IActivityContextProvider` in `TrackMeUp/Providers/ActivityContextProviders.cs`, then register it in `ActivityContextProviderRegistry`. Providers translate a process and window title into a stable application name and context; storage, sampling, and dashboard code do not need to change.

## CLI (PowerShell 7)

The supported shell is PowerShell 7 (`pwsh`) only. The packaged MSIX app exposes the `trackmeup.exe` execution alias; the executable starts the same local runtime as the player, then forwards commands through a same-user named pipe.

```powershell
trackmeup.exe -cli
trackmeup.exe reports
trackmeup.exe reports --theme dark
trackmeup.exe -cli status
trackmeup.exe -cli tracking start
trackmeup.exe -cli status --json
trackmeup.exe -cli retention preview
trackmeup.exe -cli /help /config
trackmeup.exe -cli /config list
trackmeup.exe -cli /config get ai.reasoning_effort
trackmeup.exe -cli /config set ai.output_detail compact
trackmeup.exe -cli /ai configure --model gpt-5.6 --reasoning-effort high --output-detail balanced
trackmeup.exe -cli /doctor --json
pwsh -NoProfile -File .\scripts\test-cli.ps1
```

Use `--format rich|plain|json` (`--json` is an alias), `--language en|it|vi|fr|de|es`, `--no-color`, `--no-emoji`, `--no-animation`, `--quiet`, `--yes`, `--timeout`, and `--verbose`. JSON mode writes exactly one ANSI-free document to standard output.

The first command token accepts both `command` and `/command`. Use `/help`, `/help /command`, or `/command --help` for contextual help. `/config list|get|set` exposes the same non-secret, validated settings catalog used by WinUI; internal identity/history fields and secret values are never part of that catalog.

API keys are never accepted as command-line arguments. Use the interactive `trackmeup.exe -cli ai key set` secret prompt; the key is stored only in the selected user environment variable. Screenshots and AI requests are gated by the privacy policy before any capture or provider call. `retention preview` is read-only; `retention run` requires `--yes`.

CLI verification checklist:

- [ ] Run the PowerShell 7 smoke script against an installed package.
- [ ] Confirm `--help`, `--version`, `status --json`, and `doctor --json` return one valid result.
- [ ] Confirm `/help /config`, `/config list`, and a reversible `/config set` round-trip show the same values as WinUI options.
- [ ] Confirm `/ai analyze --no-capture` does not create a screenshot even when screenshot capture is globally enabled.
- [ ] Confirm a CLI invocation does not open a XAML window and does not leave duplicate runtime hosts.

## AI prompt profiles

Screenshot analysis instructions are maintained as individual English `*.prompt.md` files under `prompts/`. `compact` uses low image detail and a 512-token ceiling, `balanced` uses automatic image detail and a 1024-token ceiling, and `detailed` uses high image detail and a 2048-token ceiling. The selected profile also controls OpenAI text verbosity. For OpenAI Responses requests, a non-`auto` reasoning effort is sent explicitly; compatible third-party providers receive only fields supported by their payload format.

The prompt assets are required files copied with published builds. Missing or empty prompt files fail analysis immediately; there is no compiled fallback. They treat screenshot text and local context as untrusted data, prohibit secret/private-data transcription, and instruct the model to distinguish observations from inference.

An optional custom instruction is appended after the built-in profile prompt only when it is non-empty. Active hours and breaks can be configured per weekday using `HH:mm-HH:mm`; they are informational context, never tracking controls. Device location is disabled by default and, when enabled, comes only from the Windows location service with source/status recorded alongside the coordinates—TrackMeUp never uses IP geolocation.

## Repository structure

- `TrackMeUp/` — WinUI 3 app source, app manifest, project files and assets.
- `TrackMeUp.Core/` — domain/application contracts, infrastructure services, and runtime IPC host.
- `TrackMeUp.Presentation/` — UI-neutral view models.
- `TrackMeUp.Cli/` — Spectre.Console CLI frontend and console bootstrap.
- `TrackMeUp.Reports.Web/` — offline Vue, Vuetify, and ECharts report application plus its packaged production bundle.
- `TrackMeUp.*.Tests/` — core, presentation, and CLI tests.
- `scripts/` — PowerShell utility scripts and shared modules.
- `.github/` — governance files, Copilot instructions, and task notes.
- `AGENTS.md` — mandatory agent instructions for any contributor.

## Build and run

Prerequisites:

- Windows 10+ with Visual Studio 2022 or compatible .NET 10 toolchain.
- `dotnet` CLI available.
- Node.js 20.19.0 or newer and `npm` when rebuilding the Reports web bundle.

```powershell
pwsh -NoProfile -File .\scripts\build-reports-web.ps1
pwsh -NoProfile -Command "dotnet restore .\TrackMeUp.slnx"
pwsh -NoProfile -Command "dotnet build .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
pwsh -NoProfile -Command "dotnet test .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
```

Optional builds:

- `dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=ARM64`
- `dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x86`

## Distribution modes

TrackMeUp supports both Windows distribution paths from the same sources:

- **MSIX:** select `Debug` or `Release`, then use the `TrackMeUp (MSIX)` launch profile. This preserves package identity and the Windows App SDK Deployment Manager initialization.
- **Unpackaged:** select `Debug-Unpackaged` or `Release-Unpackaged`, then use `TrackMeUp (Unpackaged)`. Those configurations set `WindowsPackageType` to `None`, so the Windows App SDK bootstrapper is compiled into the app before WinUI is activated.
- **Unpackaged folder publish:** select `Release-Unpackaged` and use the `win-x64`, `win-x86`, or `win-arm64` publish profile. Those builds are self-contained, including the Windows App SDK runtime.

Verification checklist:

- [ ] In Visual Studio, launch `TrackMeUp (MSIX)` with `Debug` and confirm the main window opens.
- [ ] In Visual Studio, launch `TrackMeUp (Unpackaged)` with `Debug-Unpackaged` and confirm the main window opens without `REGDB_E_CLASSNOTREG`.
- [ ] Open Tools and Diagnostics at narrow width, exercise each read-only action, and verify retention cleanup requires the explicit delete confirmation before any file is removed.
- [ ] Open Reports from the three-dot menu and with `trackmeup.exe reports`; verify both use the same running tracker and never create a second runtime.
- [ ] Exercise every report range and view, use Aggiorna to bypass the short-lived range cache, search the accessible table, switch among system/light/dark themes, restart Reports, and confirm the selection persisted through application settings. Simulate a settings-write failure and confirm Vue reports the failure while retaining the previous theme.
- [ ] Configure a blank and a non-empty custom AI instruction; confirm the blank case sends only the built-in prompt and the non-empty case appends exactly one additional-instruction section.
- [ ] Configure weekday active hours with lunch/dinner breaks; confirm an invalid or out-of-window break is rejected and a valid period appears only as informational AI context.
- [ ] Run one successful request through each configured provider and one failed request; confirm the SQLite telemetry row has the snapshot correlation ID, nullable absent fields, and no prompt, image, authorization header, or key.
- [ ] Confirm the report separates provider-reported actual cost from unavailable/partial cost, and that each snapshot's token usage is attributable by provider and origin.
- [ ] With Windows Location disabled, confirm the snapshot contains no coordinates and status `disabled_by_setting`; after explicit Windows permission and opt-in, confirm latitude/longitude include `windows-geolocator` provenance.
- [ ] Place a legacy `activity.jsonl` or `analyses.jsonl` beside local data, or corrupt `appsettings.json`; verify startup fails with a clear unsupported-storage/configuration error instead of importing, recovering, or ignoring it.

## Diagnostics logging

Both distribution modes initialize the same Serilog pipeline. Logs are written to `%LocalAppData%\TrackMeUp\logs\trackmeup-YYYYMMDD.log` and to the attached console when one is available. The rolling file sink keeps at most seven daily files. For a focused debug run, set `TRACKMEUP_LOG_DIRECTORY` before launching Visual Studio to redirect the files to a temporary writable folder.

When a Sentry project is configured, set `TRACKMEUP_SENTRY_DSN` (and optionally `TRACKMEUP_SENTRY_ENVIRONMENT`) in the local launch environment. The `Sentry.Extensions.Logging` provider is enabled only when a structurally valid DSN is present; a malformed value is ignored without disabling console/file logging. It keeps Information logs as breadcrumbs, sends Error/Critical events, disables default PII, and uses a two-second shutdown/flush budget. No DSN or secret is stored in the repository.
- [ ] Run `/runtime health --json` or `/doctor --json` and verify the redacted observability state reports console/file logging, Sentry status, and `sendsDefaultPii: false` without returning the DSN.
- [ ] Launch once with an invalid `TRACKMEUP_SENTRY_DSN` and confirm local console/file logging still works, then run a short CLI command and confirm shutdown completes within the bounded telemetry flush window.
- [ ] Inspect a launch log and confirm it contains the launch mode and architecture, but no command arguments, absolute application path, or raw installation identifier.
- [ ] Point screenshot storage at a directory containing an old unrelated sentinel file; verify retention preview/run includes only TrackMeUp-owned artifact names and preserves the sentinel.
- [ ] Start UI and CLI concurrently with a clean settings directory and verify both resolve the same installation fingerprint and only one runtime owns tracking hooks.
- [ ] Publish each target architecture and launch the unpackaged folder on a clean test machine.

## Visual assets and Store release checks

- The approved icon artwork is stored in `design/branding/trackmeup-icon-reference.png`.
- Regenerate MSIX tiles, splash screens, theme variants, target-size app-list icons, and the executable `.ico` with `python scripts/generate_trackmeup_assets.py`.
- [ ] Before publishing, install an MSIX package and verify the icon in the taskbar, Start app list, context menu, and About window in both light and dark system themes.
- [ ] Verify that the 30-second Mica status toast and each tracking start/pause transition show the correct localized state without starting tracking when `Start tracking on launch` is disabled.
- [ ] Launch with Explorer running, choose each taskbar-control placement, then verify that the icon opens the player and the adjacent control toggles tracking while the LED stays grey when paused and softly pulses red only while recording.
- [ ] Repeat the taskbar-control check at the available DPI/taskbar heights and verify that the transparent control scales uniformly without clipping or introducing a colored container.
- [ ] Restart Explorer during a session and verify that the taskbar control reattaches without creating a second tracking runtime; if a custom shell rejects it, verify that the normal player remains open.

## Governance and automation

- `.github/copilot-instructions.md` and `AGENTS.md` define contribution and agent constraints.
- `.github/tasks/todo.md` is the active task list.

## License

This project uses the MIT license.

## CI

- GitHub Actions workflow: `.github/workflows/build.yml` runs restore and build on supported platforms.

## AI assistance

AI can assist with drafting, review, and implementation suggestions.
The maintainer remains responsible for verification, security decisions, and releases.

The OpenAI integration uses image input through the Responses API. Sending screenshots is always opt-in and requires a user-provided API key.
