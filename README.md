<p align="center">
  <img src="TrackMeUp/Assets/TrackMeUpSquare150Logo.png" alt="TrackMeUp app icon" width="150" />
</p>

# TrackMeUp

[![Build](https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml)

TrackMeUp is a local Windows tool for making workdays easier to understand and easier to remember.

We use it internally — I use it myself first — to put some order into the day: what was open, when work was active, which applications were involved, and what a session was about. It is also built around a very ordinary memory problem:

> “I saw a page with a blue-and-red image and a big white headline, but I cannot remember the address.”

TrackMeUp keeps a local timeline and can, when explicitly enabled, turn a screen capture into a short description. That description can help reconstruct the context later. It cannot invent a URL that was never visible: browser titles are available, while reliable page addresses require a future opt-in browser integration.

## A working product, not an MVP

This is not an MVP or a throwaway demo. TrackMeUp is a working internal product with a desktop interface, reports, retention controls, optional AI analysis, a PowerShell CLI, localization, tests, and two Windows distribution paths. It is still evolving, but the product is already useful today and its important behavior is documented below.

## What TrackMeUp does

- Builds a quiet local timeline of active and idle time, applications, window context, key-press counts, and mouse-click counts. It never records what was typed.
- Shows the current state in a small Windows player and a taskbar control. The player stays visible whenever the app starts, including when the taskbar control cannot attach.
- Provides local reports for days, time patterns, trends, and applications.
- Supports focus sessions and local HTML reports.
- Adds extra context for selected applications such as Word, Excel, Visual Studio Code, and browsers. These details can be switched off individually.
- Offers optional screen captures and optional AI descriptions of the current context.
- Lets you configure an in-player snapshot interval and edit weekly working hours in a 30-minute grid. New installations default to every day, 00:00-24:00. Working hours are stored locally and used to gate scheduled snapshots; clearing every block disables the timer until hours are configured again.
- Provides a PowerShell 7 CLI for status, tracking, reports, AI, privacy rules, and retention.
- Keeps the report interface bundled with the app. Reports do not need a local web server or a TrackMeUp cloud account.

## Privacy in plain language

TrackMeUp is local by default. There is no TrackMeUp server receiving your activity, no hidden account, and no silent upload of your workday.

You control the features that can create or send sensitive material:

- **Screen captures:** off by default. When off, TrackMeUp does not create a screenshot for analysis.
- **Capture cleanup:** a manual snapshot can be deleted from the player for 30 seconds after capture; the screenshot gallery also exposes separate screenshot and snapshot-analysis deletion commands in its ellipsis menu.
- **AI analysis:** off by default. When off, no AI service is contacted.
- **Snapshot analysis:** scheduled snapshots are retained locally first and are then offered to AI when it is enabled and configured. Missing keys, cost limits, or provider errors do not remove or hide the retained snapshot. A manual player snapshot waits through the 30-second deletion window; if it is deleted, no AI request is made, and if it remains, the exact retained capture is analyzed once the window expires.
- **Location:** off by default. When enabled, location comes from the Windows Location service and is included only in an AI request.
- **Privacy rules:** can block an application, a window title, or a context hint before capture and before an AI request.
- **Retention:** controls how long local activity, AI results, and retained screenshots remain on this PC.

The activity database, reports, diagnostic logs, and retained screenshots are local files. Their default locations are under the current Windows user profile; screenshot and report folders can be changed explicitly in the app settings. App options includes a visible link to the configured folder that contains retained snapshots and any local debug-image artifacts. Transient screenshots are deleted after analysis when screenshot retention is off, including the normal failure and cancellation paths.

Read the complete, source-backed data-flow and dependency census in [docs/PRIVACY.md](docs/PRIVACY.md).

## Your OpenAI key stays yours

OpenAI is the default AI integration. TrackMeUp uses your own OpenAI API key from the Windows environment on this PC.

The key is not copied into TrackMeUp settings, SQLite, reports, logs, command arguments, command history, or local IPC diagnostics. TrackMeUp has no server through which the key is routed. When AI analysis is enabled and TrackMeUp captures a permitted snapshot, the key is used in the direct HTTPS request to OpenAI — it is authentication for that request, not a TrackMeUp credential.

You can set the key from the app or with the hidden-input CLI prompt:

```powershell
trackmeup.exe -cli ai key set
```

OpenRouter and Anthropic are supported as explicit alternatives. Choosing one uses that service's own key and endpoint; the same local-key rule applies.

## Open source means no surprises

The code is public under the [MIT license](LICENSE). The capture rules, AI request adapters, storage, retention, diagnostics, and optional Sentry integration are all in this repository. You can inspect what is installed and what can make a network request instead of trusting a vague “privacy-first” label.

The dependency census names the direct libraries and their role, including:

- **Serilog and its console/file sinks:** local diagnostics; no remote destination by themselves.
- **Sentry.Extensions.Logging:** optional remote error reporting, disabled unless `TRACKMEUP_SENTRY_DSN` is configured. Default PII is disabled and user/request/server identity is stripped before sending.
- **Microsoft.Data.Sqlite and SQLitePCLRaw:** local activity, analysis, and sanitized AI-usage records.
- **SkiaSharp and System.Drawing.Common:** local image capture, conversion, and watermarking.
- **System.Management and PerformanceCounter:** local Windows/system measurements.
- **Spectre.Console:** the optional PowerShell CLI surface.
- **Vue, Vuetify, ECharts, and Vite:** the bundled offline reports interface.

This is a short summary, not a substitute for the [full inventory](docs/PRIVACY.md) or the project files themselves.

## AI and screenshots: full control

Screenshots and AI are separate choices. You can:

1. track activity without taking screenshots;
2. capture local snapshots without contacting an AI service by leaving AI analysis disabled;
3. analyze every captured snapshot without retaining the image locally;
4. disable both features completely;
5. add privacy rules that block capture and analysis for sensitive work.

AI requests may contain the current application/window context, selected system information, and a screenshot only when the relevant settings allow it. The request goes directly to the AI service selected in the app. TrackMeUp stores small local records of request counts and timing for cost and troubleshooting; it does not store prompts, images, authorization headers, or keys in those records.

AI output is an aid for recall, not a source of truth. The built-in prompts ask the model to separate observation from inference and avoid reproducing secrets or private text.

## Quick start

The supported development and CLI shell is PowerShell 7:

```powershell
pwsh -NoProfile -Command "dotnet restore .\TrackMeUp.slnx"
pwsh -NoProfile -Command "dotnet build .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
pwsh -NoProfile -Command "dotnet test .\TrackMeUp.slnx -p:Platform=x64 -warnaserror"
```

Useful installed-package commands:

```powershell
trackmeup.exe -cli status
trackmeup.exe -cli tracking start
trackmeup.exe -cli report today
trackmeup.exe -cli ai status
trackmeup.exe -cli retention preview
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action TestCli
```

Repository automation uses a single PowerShell 7 entrypoint. Running it without
arguments opens the interactive control center; explicit `-Action` calls are
safe for agents and CI-style terminals.

```powershell
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Preflight
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Build -Platform x64 -WarnAsError
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Test -Platform x64 -WarnAsError
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action BuildReports
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action PackageMsix -Platform x64
```

Screenshots, AI, and retention mutations are guarded by the same privacy and confirmation rules in the app and CLI. API keys are never accepted as command-line arguments.

Screenshot gallery validation checklist: retain captures on two different days, restart the app, open the gallery twice and confirm that only one window is activated, then open it from the `Wayback Machine` flyout entry and confirm that the most recent retained day is selected with its captures visible. Select another day with the floating WinUI `CalendarDatePicker`, verify that a manual capture is labeled `Manuale` while an automatic capture is labeled `Pianificato`, and use the App options link to open the configured snapshot and debug-image folder. In light and dark themes, confirm that the four metadata pills stay centered, show only an icon and compact localized value without clipping, and remain visually separated from the full-width filmstrip. With 0, 1, 2, 6, and at least 500 captures, navigate the Cover Flow repeatedly in both directions using its arrows, left/right keys, mouse wheel, side-card clicks, and pointer drag; confirm continuous 16:9 perspective motion, inertial snap, seamless wrapping past both ends, synchronized metadata/timeline selection, no image flash, no growing working set after 100 wraps, no more than seven realized Cover Flow presenters, and a virtualized filmstrip. Disable Windows animation effects and confirm that navigation becomes immediate and flat while the selected border and keyboard focus remain visible.

Capture deletion validation checklist: take a manual snapshot from the player, confirm that the trash action remains beside the snapshot action, the 30-second progress countdown spans the status area below the running state and timer, and the snapshot action stays disabled until that countdown ends or the pending capture is deleted. Delete it before the countdown expires, confirm that no AI request was made, and confirm that the latest-session preview no longer shows it. Repeat without deleting, close the player if desired, wait for the countdown to expire, and confirm that the same capture is analyzed exactly once. In the screenshot gallery ellipsis menu, confirm that Delete screenshot removes the image artifacts while Delete snapshot removes the associated local snapshot-analysis record.

Window-state validation checklist: close and reopen the reports and screenshot windows on a multi-monitor setup, confirm that their saved size, position, and monitor are restored, then disconnect or resize the saved monitor and confirm that the restored bounds remain inside the current work area. Reopen the compact player separately and confirm that its fixed size and configured player position are not overridden by previously saved window geometry.

Taskbar-control validation checklist: launch the unpackaged x64 app and confirm that the player window remains visible while the logo, play/pause glyph, and recording indicator appear immediately in the taskbar; launch it again to confirm the same startup behavior, restart Explorer and confirm that the control returns, then repeat at 100%, 125%, and 150% display scaling.

Player overflow validation checklist: open the overflow menu from the always-visible ellipsis in light and dark themes, confirm that it floats upward as a popup outside the window rather than covering or scrolling inside the player surface, and confirm that the compact surface contains Reports, Captured moments, App options, OpenAI, screenshot, and About; confirm that Reports lands on today while Captured moments lands on the most recent retained capture day, and that both still allow filtering afterward; toggle screen capture off and on, reopen the menu, and confirm that the persisted switch state matches the app options page. In the screenshot gallery, open its ellipsis menu and confirm that it also floats as a popup outside the gallery window.

Player activity-trend validation checklist: use the player with less than 24 hours of retained monitoring history and confirm that no activity line is shown. After persisted samples cover a full trailing 24-hour window, confirm that the line appears and each point reflects the active-time percentage for its respective hour.

Scheduling validation checklist: on a fresh settings store, confirm that all 48 half-hour blocks are selected for every day. Open Snapshot schedule and confirm that it appears in a separate window matching the active app theme. Set an interval and confirm that the player displays the next-snapshot countdown. Pause tracking and confirm that the countdown freezes; resume tracking and confirm that it continues from the frozen value. Apply the 09:00-18:00 preset, confirm the overwrite dialog, and verify that Monday-Friday are selected while weekends are cleared; repeat with Clear all and its confirmation. Save hours that exclude the current time, including a configured break, and confirm that the compact outside-hours banner appears and its Configure hours link reopens Snapshot schedule; include the current time again and confirm that the banner disappears. Save the empty schedule and confirm that the countdown disappears and the same outside-hours banner remains visible. Select contiguous 30-minute blocks and leave a gap inside them; reopen the schedule window and confirm that the selected blocks and the gap are restored as active time and a break. Confirm that snapshots occur only inside selected working hours and never during a configured break. With AI enabled but its key unavailable, confirm that an eligible scheduled screenshot is still retained and appears after restarting the app.

OpenAI settings validation checklist: open OpenAI configuration and confirm that the model is selected from the catalog-backed combo box, the selected model shows its name, key, description, availability, and accent color, and the thinking-effort choices update to match that model. Confirm that no analysis-interval or duplicate privacy callout is shown. With no key, confirm that the page reports that the key is not set and both OpenAI toggles are disabled; enter an unrecognized value and confirm that it is rejected; set a plausible `sk-` key and confirm that the page reports it as ready, both toggles unlock, and changing either toggle immediately updates the other. With screenshots and AI enabled, capture one manual player snapshot, confirm that no analysis starts during its 30-second delete window, then confirm that exactly one analysis uses that same captured file after the window expires; disable AI and confirm that snapshots remain local and are not analyzed.

## Repository map

- `TrackMeUp/` — Windows desktop app and composition root.
- `TrackMeUp.Core/` — shared application behavior, persistence, capture, AI adapters, and runtime ownership.
- `TrackMeUp.Presentation/` — UI-neutral models used by the desktop surfaces.
- `TrackMeUp.Cli/` — PowerShell-facing CLI.
- `TrackMeUp.Reports.Web/` — source and bundled assets for local reports.
- `TrackMeUp.*.Tests/` — core, presentation, and CLI tests.
- `scripts/TrackMeUp.ps1` — unified PowerShell 7 entrypoint for repository automation and the interactive control center.
- `docs/PRIVACY.md` — plain-language privacy and dependency census.
- `docs/CLI_IMPLEMENTATION_PLAN.md` — internal engineering notes for the CLI and shared application surface.
- `store/` — versioned Microsoft Store copy, public links, screenshot inventory, and Partner Center publishing notes.

## Distribution

The same sources support:

- **MSIX**, with Windows package identity and an app execution alias.
- **Unpackaged Windows builds**, including self-contained x86, x64, and ARM64 publish profiles.

For contributor-facing architecture rules, build matrices, and implementation details, see [AGENTS.md](AGENTS.md) and [docs/CLI_IMPLEMENTATION_PLAN.md](docs/CLI_IMPLEMENTATION_PLAN.md).

## License

TrackMeUp is released under the [MIT license](LICENSE).
