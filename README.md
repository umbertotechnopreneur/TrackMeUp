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
- Shows the current state in a small Windows player and a taskbar control.
- Provides local reports for days, time patterns, trends, and applications.
- Supports focus sessions and local HTML reports.
- Adds extra context for selected applications such as Word, Excel, Visual Studio Code, and browsers. These details can be switched off individually.
- Offers optional screen captures and optional AI descriptions of the current context.
- Provides a PowerShell 7 CLI for status, tracking, reports, AI, privacy rules, and retention.
- Keeps the report interface bundled with the app. Reports do not need a local web server or a TrackMeUp cloud account.

## Privacy in plain language

TrackMeUp is local by default. There is no TrackMeUp server receiving your activity, no hidden account, and no silent upload of your workday.

You control the features that can create or send sensitive material:

- **Screen captures:** off by default. When off, TrackMeUp does not create a screenshot for analysis.
- **AI analysis:** off by default. When off, no AI service is contacted.
- **Automatic AI analysis:** off by default. When off, analysis happens only when you ask for it.
- **Location:** off by default. When enabled, location comes from the Windows Location service and is included only in an AI request.
- **Privacy rules:** can block an application, a window title, or a context hint before capture and before an AI request.
- **Retention:** controls how long local activity, AI results, and retained screenshots remain on this PC.

The activity database, reports, diagnostic logs, and retained screenshots are local files. Their default locations are under the current Windows user profile; screenshot and report folders can be changed explicitly in the app settings. Transient screenshots are deleted after analysis when screenshot retention is off, including the normal failure and cancellation paths.

Read the complete, source-backed data-flow and dependency census in [docs/PRIVACY.md](docs/PRIVACY.md).

## Your OpenAI key stays yours

OpenAI is the default AI integration. TrackMeUp uses your own OpenAI API key from the Windows environment on this PC.

The key is not copied into TrackMeUp settings, SQLite, reports, logs, command arguments, command history, or local IPC diagnostics. TrackMeUp has no server through which the key is routed. When you explicitly request an OpenAI analysis, the key is used in the direct HTTPS request to OpenAI — it is authentication for that request, not a TrackMeUp credential.

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
2. take a one-off local screen capture without asking an AI service to analyze it;
3. ask for an AI description without retaining the screenshot;
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
pwsh -NoProfile -File .\scripts\test-cli.ps1
```

Screenshots, AI, and retention mutations are guarded by the same privacy and confirmation rules in the app and CLI. API keys are never accepted as command-line arguments.

Screenshot gallery validation checklist: open the gallery twice and confirm that only one window is activated, select another day with the floating WinUI `CalendarDatePicker`, and verify that a manual capture is labeled `Manuale` while an automatic capture is labeled `Pianificato`.

## Repository map

- `TrackMeUp/` — Windows desktop app and composition root.
- `TrackMeUp.Core/` — shared application behavior, persistence, capture, AI adapters, and runtime ownership.
- `TrackMeUp.Presentation/` — UI-neutral models used by the desktop surfaces.
- `TrackMeUp.Cli/` — PowerShell-facing CLI.
- `TrackMeUp.Reports.Web/` — source and bundled assets for local reports.
- `TrackMeUp.*.Tests/` — core, presentation, and CLI tests.
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
