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

Screenshot viewer validation checklist: open 16:9, portrait, and ultrawide captures and confirm that the selected image covers the active viewport at 100%, starts centered, and exposes its overflow with a left-button drag. Zoom to 500% and confirm that click-drag, wheel, touch, and trackpad navigation remain usable with hidden scrollbars. In light, dark, and high-contrast themes, and with Windows transparency effects disabled, confirm that the zoom rail, metadata chips, and full-width filmstrip remain readable over the image through native Acrylic or its system fallback.

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
