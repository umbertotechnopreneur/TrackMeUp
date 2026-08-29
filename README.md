<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-dark-readme-2400x800.png" />
    <source media="(prefers-color-scheme: light)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" />
    <img src="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" alt="TrackMeUp retrieves a page from an earlier moment in a visual workday timeline" width="100%" />
  </picture>
</p>

<h1 align="center">TrackMeUp</h1>

<p align="center"><strong>Your workday, searchable. Your history, local by default.</strong></p>

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
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-2EA44F" alt="MIT License" /></a>
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
4. **Merge installations explicitly.** A private `.tmuarchive` can carry SQLite history and retained screenshots between installations; preview and confirmation happen before merge, while machine identity plus a friendly name, color, and icon preserve provenance.

The desktop experience is paired with a PowerShell-friendly CLI, so the same application behavior is available to people and automation without creating a second tracking runtime.

The desktop app, reports, and human-readable CLI output can follow the Windows language or use `en-US`, `it-IT`, `fr-FR`, `de-DE`, `es-ES`, `zh-Hans`, `vi-VN`, `ko-KR`, `pt-PT`, or `pt-BR`. European and Brazilian Portuguese use separate product catalogs.

Display, search, and OCR languages are configured independently. Search supports every product locale; OCR offers only the corresponding Windows recognizer choices and requires the selected language pack to be installed. Vietnamese remains available for the interface and search but is not offered as a Windows OCR language.

## Privacy you can act on

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v2-dark-erasure-wave-2400x800.png" />
    <source media="(prefers-color-scheme: light)" srcset="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v3-light-radial-reset-2400x800.png" />
    <img src="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v3-light-radial-reset-2400x800.png" alt="Privacy has a nuclear option: permanently erase TrackMeUp-owned local data from this installation" width="100%" />
  </picture>
</p>

Local-first is the default, not a premium setting:

- No TrackMeUp cloud account is required.
- No hidden synchronization pipeline uploads your activity.
- Screenshots, AI analysis, and location sharing are off by default.
- Privacy rules can exclude selected applications, window titles, and context details.
- Retention policies control how long activity, analysis records, and screenshots remain.
- **Atomic nuke** permanently removes TrackMeUp-owned local data, screenshots, settings, reports, logs, search indexes, and metadata from the current installation, then restarts the app clean. It cannot retract exported data or copies already sent through integrations you enabled.

For the complete data-flow and dependency inventory, see [Privacy and Data Flow](docs/PRIVACY.md).

## Choose the recall you want

Screenshots and AI are separate choices. Start with the smallest footprint that solves your problem and enable more context only when it earns its place.

| Setup | What it adds | What leaves this PC |
| --- | --- | --- |
| **Activity timeline** | Active and idle periods, application and window context, reports | Nothing unless optional diagnostics are enabled |
| **Visual recall** | Local screenshots and on-device OCR | Nothing unless optional diagnostics are enabled |
| **AI-assisted recall** | Optional descriptions or OCR refinement from your configured AI provider | Only the data included in an enabled provider request, plus optional diagnostics if enabled |

This table describes background product behavior. Export, Windows sharing, and
redacted-log sharing send only the material you explicitly choose.

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

The same utility can create a sideloadable MSIX or installer when release packaging is required. Package layouts are written to `artifacts/packages/`; final installers are written to `artifacts/installers/`.

On Windows, `PackageMsix` and `CreateInstaller` sign the package automatically with the local TrackMeUp test certificate (`CN=umber`). If it is missing, the script creates it in the current user's certificate store, exports the public certificate to `artifacts/certificates/TrackMeUp-Test-Signing.cer`, and trusts it for the current user. To use another certificate already installed in `Cert:\CurrentUser\My`, pass `-PackageCertificateThumbprint <thumbprint>`. The test certificate is intended only for local sideloading; production releases must use a certificate issued for distribution.

## Power users and contributors

Installed-package CLI examples:

~~~powershell
trackmeup.exe -cli status
trackmeup.exe -cli tracking start
trackmeup.exe -cli report today
trackmeup.exe -cli ai status
trackmeup.exe -cli retention preview
~~~

Run `trackmeup.exe -cli` with no command in PowerShell 7 to open the interactive Spectre.Console command center. It shows the live local dashboard and offers tracking, AI, screenshot, report, diagnostics, settings, and desktop-app actions. The CLI always talks to the same shared TrackMeUp runtime as the desktop app; it does not start a second tracker or automate the graphical UI.

### CLI switches

Use `trackmeup.exe -cli --help` for the complete command reference. The following quick switches expand to their documented command and are convenient for daily use:

| Switch | Equivalent command | Purpose |
| --- | --- | --- |
| `--status` | `status` | Show the live tracking dashboard. |
| `--start` | `tracking start` | Start activity tracking. |
| `--pause` | `tracking pause` | Pause activity tracking. |
| `--toggle` | `tracking toggle` | Toggle activity tracking. |
| `--ai-on` | `ai enable` | Enable the configured AI provider. |
| `--ai-off` | `ai disable` | Disable AI analysis. |
| `--capture` | `screenshot capture` | Capture a privacy-checked screenshot. |
| `--report` | `report today` | Generate today's activity report. |
| `--doctor` | `doctor` | Run read-only diagnostics. |
| `--help` | `help` | Show help without connecting to the runtime. |
| `--version` | `version` | Show CLI and protocol versions without connecting to the runtime. |

Global output and safety switches can be combined with a command or quick switch:

| Switch | Purpose |
| --- | --- |
| `--format <rich|plain|json>` / `--json` | Select interactive, plain-text, or machine-readable output. |
| `--language <system|en-US|it-IT|fr-FR|de-DE|es-ES|zh-Hans|vi-VN|ko-KR|pt-PT|pt-BR>` | Select the CLI display locale. Language-only legacy values such as `en`, `pt`, and `zh` are rejected. |
| `--quiet`, `--verbose` | Reduce successful output or add diagnostics in plain mode. |
| `--yes` | Explicitly confirm a command that requires confirmation. |
| `--timeout <1-300>` | Set the shared-runtime connection timeout in seconds. |

Examples:

~~~powershell
trackmeup.exe -cli
trackmeup.exe -cli --ai-on
trackmeup.exe -cli --status --format json
trackmeup.exe -cli /ai --help
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

- [ ] On the player, the left world-time rail sits directly on the visible window material without an outer card and shows the current local sun or calculated lunar phase, illumination, sunrise/sunset and moonrise/moonset; expanding a city reveals its 24-hour detail, pointer hover and keyboard focus expose localized detail/remove actions, search can add a capital, removal persists, and the add command disappears at four clocks then returns after removal.
- [ ] In PowerShell 7, run `trackmeup.exe -cli` with no command, use the interactive command center to refresh the dashboard and open help, then exit without starting a second tracking runtime.
- [ ] In PowerShell 7, confirm CLI help lists only supported global switches and that redirected and JSON output remain ANSI-free, with JSON producing exactly one valid document.
- [ ] In PowerShell 7, confirm malformed CLI input such as `status --watch --interval 0`, a missing option value, an unknown switch, or an unterminated quoted value exits with code 2 without invoking the requested application operation.
- [ ] With a clean settings file, the acrylic four-profile chooser opens once; applying a profile persists AI, screenshot, local-retention, and Windows-startup choices together.
- [ ] The four Quick Setup profile cards are fully visible without vertical scrolling, and the main window has a 20-DIP margin below its measured content.
- [ ] From the main-window menu, **Quick Setup** reopens with the current AI/screenshot combination selected and reapplies a different profile without restarting the app.
- [ ] With the latest-session section open, an automatic screenshot replaces the placeholder with the focused-monitor preview without collapsing or reopening the section.
- [ ] With the main window visible, a frame-analysis failure shows a subtle single-layer Acrylic banner and never opens a modal dialog.
- [ ] Trigger informational, success, warning, and error banners in light and dark themes: each keeps its text and accessible severity while using the same neutral translucent surface, theme foreground icon, one-pixel theme border, and neutral timeout indicator.
- [ ] In each supported non-English locale, open Local search/OCR, Operations, and the AI provider connection test: every heading, action, result banner, and dialog control is localized, with no raw result codes shown to the user.
- [ ] A persisted language-only locale such as `it` makes settings loading fail without rewriting the file, and settings patches accept boolean values only as lowercase `true` or `false` (rejecting aliases such as `1`, `yes`, and `on`).
- [ ] In AI options, the daily visual-provider-request quota remains visible before and after the limit is reached; it counts AI OCR refinement plus successful and failed visual-analysis attempts while excluding connection tests, its expander accepts and persists only whole values from 0 through 400 (default 20), and it refreshes used versus configured capacity, accessible progress, and limit state.
- [ ] With fresh settings, monthly AI spend stays hidden in the player and no spend summary is requested; enabling **Show monthly AI spend in the player** reveals and refreshes it, while disabling the option collapses it immediately.
- [ ] Take a manual snapshot near the end of its deletion window: the localized delete label may trim, but the complete `mm:ss` countdown and the accessible delete action remain visible and correct.
- [ ] From **Activity > Activity history**, recorded days are marked in the rolling twelve-month calendar; selecting a day shows its exact 0–100 activity-intensity score and the active, idle, tracked, keyboard, and mouse totals, while a date without samples remains explicitly marked as no data.
- [ ] From **Activity > Activity history**, double-click a calendar day or select it and choose **Explore screenshots**; the shared **Captured moments** window opens on that exact local date, including its explicit empty state when no screenshots were retained.
- [ ] From a selected calendar day, open **Complete missing AI descriptions**: the Acrylic preflight shows the exact screenshot, acquisition, maximum-request, exclusion, and current-quota counts without contacting the provider; starting creates only the quota-bounded work shown, progress reports completed and remaining screenshots/acquisitions, Pause stops before the next provider request, Close leaves the durable job running, and reopening the calendar restores the active job.
- [ ] With AI descriptions and OCR refinement enabled, an incomplete OCR-provider response is recorded as a failed refinement but the raw OCR remains available and the visual screenshot description is still requested and saved.
- [ ] With a full day of retained captures, opening **Captured moments** keeps pointer and window interaction responsive while the cancellable gallery projection loads in the background; each capture still shows only the activity from its own interval.
- [ ] In **Captured moments**, open the native date picker, select a different populated day and then an empty day, and verify that the large date, capture count, and gallery all reload to the selected date while the picker is disabled during loading.
- [ ] Start with owned screenshot files in the legacy flat directory: before tracking begins, the non-dismissible **Data migration** progress window appears, every raw/stored monitor artifact moves to `yyyy-MM/week-YYYY-WW/yyyy-MM-dd`, OCR and AI references still open the new paths, and a migration failure leaves tracking and the refresh timer stopped.
- [ ] Enable **Start with Windows** and restart both package types: the MSIX must expose and enable its `TrackMeUpStartup` Windows startup task, recognize rich startup activation, and open in the notification area; the unpackaged build must keep an exact HKCU Run command for its current executable with `--start-with-windows` and repair stale paths without toggling the setting off and on.
- [ ] While the shared runtime is completing a scheduled OCR/AI capture, launch a second desktop UI: Windows-startup reconciliation waits for the serialized mutation instead of reporting `runtime.unavailable`; a genuine startup-registration failure uses one owned standard Windows warning with its native **OK** action.
- [ ] In the player title bar, reach the localized world-clock toggle by keyboard and use it to collapse and restore the left rail: the window contracts and expands with the selected flyout anchor preserved, the accessible label reflects the next action, screen readers announce the localized world-clock landmark without a duplicate visual title, and the one-minute refresh remains stopped while the rail is hidden.
- [ ] With the taskbar widget enabled, restart Explorer and verify the old transparent widget window closes before its replacement attaches; immediately exit during widget startup and confirm shutdown completes without a dispatcher timeout or a lingering widget process.
- [ ] With the shared runtime already owned by a tray or startup process, launch a second desktop UI and verify that it paints completely and remains interactive; open and close **Captured moments** and confirm that window placement is saved before native teardown without terminating either process.
- [ ] Open **Activity report** with OCR refinement usage in the selected range and verify that the report renders instead of rejecting the valid `ocr.refinement` usage origin.
- [ ] In the screenshot inspector, the activity band adapts to the retained time range with a minimum four-hour window and five readable ticks, groups simultaneous multi-monitor captures, bottom-aligns activity bars on a visible baseline, anchors the selected half-hour label to its highlight, and stays synchronized with the full-width filmstrip at every window size and DPI scale; the header and detail pane report the real active privacy-rule count, while unavailable privacy state remains explicitly unknown.
- [ ] With a large retained screenshot history, opening local search remains responsive and its availability summary appears without loading OCR, AI, activity, or thumbnail metadata for every capture.
- [ ] Starting search-index reconstruction first paints the complete Mica progress window, then performs indexing without freezing its progress indicators or Cancel action.
- [ ] While tracking a large activity history, keep the main dashboard open through several system samples and one screenshot: counters continue updating while repeated one-second refreshes do not rescan SQLite history or make pointer input stutter.
- [ ] Starting TrackMeUp while tracking is disabled shows one Windows notification explaining that the app started paused and is not recording.
- [ ] When an OS or file-system screenshot capture fails, a Windows notification shows the localized failure title and the captured exception details.
- [ ] If activity hooks cannot start or screenshot storage drops below 512 MiB, TrackMeUp shows a toast with actionable failure details; if Windows sign-in startup genuinely cannot initialize, it shows the owned standard Windows warning described above.
- [ ] In the screenshot inspector, the selected image contains no viewer toolbar, metadata chip, hover chrome, or other interactive overlay; the full-width filmstrip remains independently available below it.
- [ ] In light, dark, and high-contrast themes, one borderless transparent WinUI `CommandBar` sits in the header between the large date and the privacy/date controls; date, time, and foreground app are plain toolbar content without chip backgrounds or shadows, actions use native dynamic overflow, and destructive actions remain critically red.
- [ ] Capture a new all-displays and active-window screenshot and verify each retained WebP contains only captured pixels: no host, timestamp, capture ID, monitor label, or other TrackMeUp text is burned into the image; historical labeled files remain unchanged.
- [ ] In the screenshot filmstrip, each thumbnail is visibly larger and shows its installation color/icon beside a small gray clock and regular-weight gray time; changing selection animates only the preview to exactly 1.2x with a coral chrome/glow and at least four layout pixels of safety, without trimming its image or label, and keeps it centered through first/last capture, resize, rapid navigation, and container recycling.
- [ ] In **Operations > Installations and data transfer**, rename an installation and choose among all 16 accent colors and 16 icons, export SQLite history with retained screenshots, preview the archive on a separate installation, then confirm the merge twice: the first import adds the source data with visible provenance and the second remains idempotent without duplicating records or files.
- [ ] After merging two installations with activity on the same day, the activity calendar uses their distinct accent markers without double-counting overlapping tracked time; selecting the day lists both friendly/machine names with their icons, and **Captured moments** repeats the same provenance in the header, filmstrip, details, and search results.
- [ ] In **Captured moments**, open or close snapshot details, close and reopen the inspector, and visit a temporarily empty day: the saved sidebar preference returns as soon as captures are available again.
- [ ] In **Captured moments**, a snapshot with OCR shows a localized foreground-only action with the external-window icon; opening it reuses one OCR window, selecting another snapshot and opening again replaces the text, and snapshots without OCR do not show the action.
- [ ] In **Captures and AI descriptions**, actions use explicit verbs, the latest capture occupies one ellipsized filename line with its full path in the tooltip, and progress stays aligned with the transparent Mica header without adding card backgrounds.
- [ ] In **Reports**, today, selected-date digest, and reports-folder actions appear as three separated sections; automatic opening is explained, the date follows the app language, and each returned path stays on one ellipsized line with its full value in the tooltip.
- [ ] In **Keep or delete data**, load current criteria, preview eligible deletions, and permanent deletion appear in order as separate sections; preview removes nothing, deletion still requires confirmation, and folder/candidate paths remain compact with full-path tooltips.
- [ ] In dark mode, the selected screenshot has no visible frame, border, internal padding, or overlaid controls; it keeps a clear theme-aware elevation, readable header/sidebar text, and the native calendar picker retains the localized **Select date** accessible label.
- [ ] In the Store listing workflow, push, pull request, and manual dispatch all validate the versioned listing without authenticating to Partner Center or mutating a Store submission.

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
- [Asset licensing and provenance](ASSET_LICENSING.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)
- [Trademark and brand policy](TRADEMARKS.md)
- [Publication checklist](PUBLICATION_CHECKLIST.md)

The About window provides quick access to the log folder, issue tracker, product links, and the runtime third-party license inventory.

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

Unless a file says otherwise, TrackMeUp's project-authored source code and
documentation are open source under the [MIT License](LICENSE). The license
permits use, modification, distribution, sublicensing, and commercial use,
provided its copyright and permission notice are retained.

The MIT License does not grant rights to the TrackMeUp name, logos, wordmarks,
app icons, or other official brand artwork. Forks and redistributions must
follow the [Trademark and Brand Policy](TRADEMARKS.md) and avoid implying that
they are official or endorsed by the TrackMeUp project.

Third-party components, data, and assets retain their own license terms. Review
[Third-Party Notices](THIRD_PARTY_NOTICES.md), the
[asset licensing record](ASSET_LICENSING.md), and any adjacent attribution or
provenance file before redistributing repository material or packaged binaries.
