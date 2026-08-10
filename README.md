<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-dark-readme-2400x800.png" />
    <source media="(prefers-color-scheme: light)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" />
    <img src="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" alt="TrackMeUp retrieves a page from an earlier moment in a visual workday timeline" width="100%" />
  </picture>
</p>

<h1 align="center">TrackMeUp</h1>

<p align="center"><strong>Your workday, searchable. Your data, local.</strong></p>

<p align="center">
  A private workday memory for Windows. Recover lost context, search captured moments,
  and understand how your day unfolded—without a TrackMeUp account or hidden cloud sync.
</p>

<p align="center">
  <a href="#remember-the-moment-not-the-tab"><strong>Explore the product</strong></a>
  ·
  <a href="#get-trackmeup"><strong>Build locally</strong></a>
  ·
  <a href="docs/PRIVACY.md"><strong>Read the privacy model</strong></a>
</p>

<p align="center">
  <a href="https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml"><img src="https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml/badge.svg?branch=main" alt="Build status" /></a>
  <img src="https://img.shields.io/badge/platform-Windows-0078D4?logo=windows11&amp;logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/status-pre--production-F9665B" alt="Pre-production" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-2E7D32" alt="MIT license" /></a>
</p>

## Remember the moment, not the tab

TrackMeUp is built for the moment when you know you saw something today, but cannot remember which app, window, or part of the day it belonged to.

<table>
  <tr>
    <td width="33%">
      <strong>Find it again</strong><br />
      Search activity, applications, window titles, screenshots, local OCR, and optional AI descriptions from one compact recall surface.
    </td>
    <td width="33%">
      <strong>Resume faster</strong><br />
      Reconstruct what was active before an interruption instead of rebuilding context from browser history and open tabs.
    </td>
    <td width="33%">
      <strong>See the shape of your day</strong><br />
      Compare active time, idle periods, applications, activity signals, daily reports, and longer-term trends.
    </td>
  </tr>
</table>

TrackMeUp does **not** store the content of what you type. It retains only non-content activity signals, such as input counts, needed to distinguish active work from idle time.

## How TrackMeUp works

1. **Observe locally.** TrackMeUp records time, active or idle state, application and window context, input counts, and selected system telemetry on this PC.
2. **Enrich only when you choose.** Screenshots, on-device OCR, and AI-provider analysis are independent options rather than prerequisites.
3. **Recall on demand.** Search your local history, inspect the original snapshot, review the timeline, or generate a daily report and digest.

The desktop experience is paired with a PowerShell-friendly CLI, so the same application behavior is available to people and automation without creating a second tracking runtime.

## Privacy you can act on

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v2-dark-erasure-wave-2400x800.png" />
    <source media="(prefers-color-scheme: light)" srcset="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v3-light-radial-reset-2400x800.png" />
    <img src="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v3-light-radial-reset-2400x800.png" alt="Privacy has a nuclear option: permanently erase every trace TrackMeUp has recorded" width="100%" />
  </picture>
</p>

Local-first is the default, not a premium setting:

- No TrackMeUp cloud account is required.
- No hidden synchronization pipeline uploads your activity.
- Screenshots, AI analysis, and location sharing are off by default.
- Privacy rules can exclude selected applications, window titles, and context details.
- Retention policies control how long activity, analysis records, and screenshots remain.
- **Atomic nuke** permanently removes TrackMeUp-owned data, screenshots, settings, reports, logs, search indexes, and metadata, then restarts the app clean.

For the complete data-flow and dependency inventory, see [Privacy and Data Flow](docs/PRIVACY.md).

## Choose the recall you want

Screenshots and AI are separate choices. Start with the smallest footprint that solves your problem and enable more context only when it earns its place.

| Setup | What it adds | What leaves this PC |
| --- | --- | --- |
| **Activity timeline** | Active and idle periods, application and window context, reports | Nothing |
| **Visual recall** | Local screenshots and on-device OCR | Nothing |
| **AI-assisted recall** | Optional descriptions or OCR refinement from your configured AI provider | Only the data included in an explicit enabled provider request |

Provider keys stay local and are never accepted through command-line arguments. Shared AI features remain provider-agnostic; you choose the supported provider and model that fit your workflow.

## Get TrackMeUp

> [!NOTE]
> TrackMeUp is currently pre-production. Public binary releases are not published yet; the supported early-access path is to build it from source.

Requirements:

- Windows 10 version 1809 or later.
- PowerShell 7.
- .NET 10 SDK.
- x64, x86, or ARM64.

~~~powershell
git clone https://github.com/umbertotechnopreneur/TrackMeUp.git
Set-Location .\TrackMeUp
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Preflight
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Build -Platform x64 -WarnAsError
~~~

To produce a self-contained unpackaged build:

~~~powershell
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action PublishUnpackaged -Platform x64
~~~

The same utility can create a sideloadable MSIX or installer when release packaging is required.

## Power users and contributors

Installed-package CLI examples:

~~~powershell
trackmeup.exe -cli status
trackmeup.exe -cli tracking start
trackmeup.exe -cli report today
trackmeup.exe -cli ai status
trackmeup.exe -cli retention preview
~~~

Repository automation:

~~~powershell
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Test -Platform x64 -WarnAsError
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action BuildReports
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action PackageMsix -Platform x64
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action CreateInstaller -Platform x64
~~~

Quick Setup validation checklist:

- [ ] With a clean settings file, the acrylic four-profile chooser opens once; applying a profile persists AI, screenshot, local-retention, and Windows-startup choices together.
- [ ] From the main-window menu, **Quick Setup** reopens with the current AI/screenshot combination selected and reapplies a different profile without restarting the app.

Start with [CONTRIBUTING.md](CONTRIBUTING.md), then use the [manual validation guide](docs/VALIDATION.md) for behavior and visual acceptance checks.

## Project documentation

- [Privacy and data flow](docs/PRIVACY.md)
- [Manual validation guide](docs/VALIDATION.md)
- [CLI implementation plan](docs/CLI_IMPLEMENTATION_PLAN.md)
- [Security policy](SECURITY.md)
- [Support](SUPPORT.md)
- [Code of conduct](CODE_OF_CONDUCT.md)
- [AI contribution policy](AI_CONTRIBUTION_POLICY.md)
- [IP provenance](IP_PROVENANCE.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)
- [Publication checklist](PUBLICATION_CHECKLIST.md)

## Repository map

- <code>TrackMeUp/</code> — Windows desktop app and composition root.
- <code>TrackMeUp.Core/</code> — application behavior, persistence, capture, AI adapters, and runtime ownership.
- <code>TrackMeUp.Presentation/</code> — UI-neutral models for desktop surfaces.
- <code>TrackMeUp.Cli/</code> — PowerShell-facing CLI.
- <code>TrackMeUp.Reports.Web/</code> — local reports web assets.
- <code>TrackMeUp.*.Tests/</code> — automated test projects.
- <code>scripts/TrackMeUp.ps1</code> — shared automation entrypoint.
- <code>docs/</code> — privacy, validation, and implementation documentation.
- <code>store/</code> — Store copy and release-support material.

## License

TrackMeUp is available under the [MIT license](LICENSE).
