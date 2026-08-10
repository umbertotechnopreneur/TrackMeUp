<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-dark-readme-2400x800.png" />
    <source media="(prefers-color-scheme: light)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" />
    <img src="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" alt="TrackMeUp retrieves a page from an earlier moment in a visual workday timeline" width="100%" />
  </picture>
</p>

# TrackMeUp

[![Build](https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml)

TrackMeUp is a local Windows app that helps you remember what happened during your workday.

If you have ever thought, "I know I saw it today, but where was it?", TrackMeUp is built for exactly that moment.

## Quick Navigation

- [What You Get](#what-you-get)
- [Privacy and Control](#privacy-and-control)
- [AI Provider and Screenshots](#ai-provider-and-screenshots)
- [Quick Start](#quick-start)
- [Open-Source Governance Docs](#open-source-governance-docs)
- [Repository Map](#repository-map)

## What You Get

TrackMeUp focuses on practical day recall, not productivity theater.

- A local timeline of active and idle work periods.
- Application and window context to reconstruct sessions.
- Optional screenshots for visual memory.
- Optional AI descriptions for faster context recall.
- Daily and trend reports, available locally.
- A desktop UI plus a PowerShell CLI.

It does not record what you typed.

## Why People Use It

- To resume interrupted tasks faster.
- To rebuild context before meetings.
- To remember browser/page moments when title memory is stronger than URL memory.
- To keep workday evidence local on the same PC.

TrackMeUp is already a working product used internally, and now prepared for open-source collaboration.

## Privacy and Control

TrackMeUp is local-first by default.

- No TrackMeUp cloud account is required.
- No hidden sync pipeline uploads your activity.
- Screenshots are off by default.
- AI analysis is off by default.
- Location sharing is off by default.

You can configure privacy rules to block capture or analysis for selected apps, titles, or context hints.

Retention is configurable for activity, analysis records, and screenshots, so data lifecycle stays under your control.

For the full data-flow and dependency inventory, read [docs/PRIVACY.md](docs/PRIVACY.md).

## AI Provider and Screenshots

Screenshots and AI are separate choices. You can use either, both, or neither.

Common setups:

1. Activity tracking only.
2. Local screenshots without AI requests.
3. AI analysis on captured screenshots.
4. Full disable for both features.

AI requests are sent directly to the selected AI provider when enabled.

Keys stay local and are never accepted via command-line arguments.

OpenAI is the default integration, with explicit alternatives such as OpenRouter and Anthropic.

## Quick Start

The supported shell for development and CLI automation is PowerShell 7.

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
```

## Repository Automation

Use the shared PowerShell 7 entrypoint:

```powershell
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Preflight
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Build -Platform x64 -WarnAsError
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Test -Platform x64 -WarnAsError
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action BuildReports
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action PackageMsix -Platform x64
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action CreateInstaller -Platform x64
```

Screenshot viewer validation checklist: open 16:9, portrait, and ultrawide captures and confirm that the selected image covers the active viewport at 100%, starts centered, and exposes its overflow with a left-button drag. Use the mouse wheel over different image points and confirm that zoom follows the pointer; also confirm click-drag, touch, and trackpad navigation through 500%. Verify that the frosted command rail and metadata fade in only while the pointer or keyboard focus is inside the image area, that the grouped colored icons expose localized tooltips, and that no overflow menu remains in the title bar. Open the details sidebar, resize it with drag and keyboard input, and confirm that it never exceeds 50% of the available width. Repeat in light, dark, High Contrast, and with Windows transparency effects disabled to confirm that the rail, metadata, sidebar, and full-width filmstrip remain readable through native Acrylic or its system fallback.

Settings tools navigation checklist: from App options, open each of the five local-data links and confirm that screen captures and AI, reports, privacy, data retention, and app details each appear as a separate scrollable page. In Extra app details, confirm that all plugins load on entry without Refresh, each row has one switch reflecting the saved state, successful changes persist, and failed changes restore the previous switch state. Confirm that Privacy is presented as a title and description followed by a textual link with a right chevron, and that Back returns directly to App options. When Tools and diagnostics is opened from the main menu, open one focused page and confirm that Back first returns to the tools overview and then to the player. Open the AI provider connection test and confirm that the taller dialog shows the complete fake terminal without clipping.

Screen captures and AI checklist: confirm that this page exposes only Latest screen capture and Open folder for retained captures; it must not expose a manual Capture screen now action, capture-mode selector, retention, or watermark controls. Confirm that describing the current context can still request a fresh capture only through its explicit consent checkbox.

Central banner validation checklist: trigger status banners from the screenshot window, tools overview, and each focused tools page, then confirm that one fixed frosted banner overlays the content without moving it. Verify the rapid 80 ms fade in and fade out, the smoothly draining 3 px icon-coral line, the automatic close after 10 seconds, and manual close during fade-in. Repeatedly close a banner, replace banner A with banner B during fade-out, and unload the host; each path must dismiss exactly once and a replacement must restart the full countdown. Repeat in light, dark, High Contrast, with Windows transparency effects disabled, and with Windows animation effects disabled.

About window validation checklist: open About in light, dark, and system theme modes and confirm that the panoramic artwork matches the effective app theme, including after a live Windows theme change in system mode. Verify that version, build date, Git commit, product links, diagnostics actions, and the close action remain visible and keyboard accessible at 100%, 150%, and 200% display scaling.

Search and keyboard shortcuts validation checklist: keep the main window focused, press `Ctrl+Shift+P`, and confirm that the fixed-light local snapshot search opens as a narrow, title-free command palette with focus in the vertically centered query field and all existing text selected. Move the pointer to each connected monitor and repeat the shortcut, confirming that the compact window is centered in the pointer's monitor work area, uses at most 64% of its width, and never exceeds 960 logical pixels. Confirm that the window cannot be resized, minimized, or maximized and that clicking outside closes it. Enter at least three characters and confirm that suggestions use compact single-line rows with a coral marker, Markdown-free text, and a weighted confidence badge. Type rapidly while suggestions and results update, move the pointer across the window, and confirm that the UI remains responsive while index refresh and Lucene work execute in the background; a thin indeterminate coral-gold-violet-blue-cyan glow must remain directly below the query box until every overlapping suggestion or search request has completed or cancelled, fading from transparent to opaque across roughly the first and last 3% of its width so both ends blend into the Acrylic surface. Pause for 700 ms and confirm that the window grows according to the number of results without exceeding 78% of the monitor work-area height; the virtualized list must show a compact 260 x 146 snapshot thumbnail with a soft resting elevation, a stronger shadow on pointer hover without a translation-access exception, and the entire image visible at its original aspect ratio, followed by the highlighted matching passage, timestamp, active window, clicks, and available CPU/GPU telemetry, while unavailable historical telemetry displays an em dash. Clear the query or run a query with no matches and confirm that the window returns to its command-palette height. Select a result and confirm that the snapshot inspector opens on that exact capture. Open the application menu and confirm that the primary actions show their `Ctrl+Shift+...` shortcuts and invoke the same commands as their menu items. From Search and OCR settings, open Rebuild search indexes and confirm that the full Acrylic window starts indeterminate progress for both results and suggestions, Cancel stops the operation safely, the previous committed indexes remain usable after cancellation or failure, and successful completion reports the indexed document count.

## Open-Source Governance Docs

This repository includes open-source governance and contribution policies tailored for TrackMeUp:

- [CONTRIBUTING.md](CONTRIBUTING.md)
- [SECURITY.md](SECURITY.md)
- [SUPPORT.md](SUPPORT.md)
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- [AI_CONTRIBUTION_POLICY.md](AI_CONTRIBUTION_POLICY.md)
- [IP_PROVENANCE.md](IP_PROVENANCE.md)
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
- [NOTICE.md](NOTICE.md)
- [PUBLICATION_CHECKLIST.md](PUBLICATION_CHECKLIST.md)

## Repository Map

- `TrackMeUp/` - Windows desktop app and composition root.
- `TrackMeUp.Core/` - application behavior, persistence, capture, AI adapters, runtime ownership.
- `TrackMeUp.Presentation/` - UI-neutral models for desktop surfaces.
- `TrackMeUp.Cli/` - PowerShell-facing CLI.
- `TrackMeUp.Reports.Web/` - local reports web assets.
- `TrackMeUp.*.Tests/` - test projects.
- `scripts/TrackMeUp.ps1` - shared automation entrypoint.
- `docs/PRIVACY.md` - privacy and dependency census.
- `docs/CLI_IMPLEMENTATION_PLAN.md` - internal CLI engineering notes.
- `store/` - Store copy and release support material.

## License

TrackMeUp is released under the [MIT license](LICENSE).
