<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-dark-readme-2400x800.png" />
    <source media="(prefers-color-scheme: light)" srcset="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" />
    <img src="design/branding/recall-timeline/output/trackmeup-recall-timeline-banner-theme-light-readme-2400x800.png" alt="TrackMeUp retrieves a page from an earlier moment in a visual workday timeline" width="100%" />
  </picture>
</p>

<h1 align="center">TrackMeUp — Find your way back to what you were doing</h1>

<p align="center"><strong>A Windows activity tracker that keeps your history on your PC by default.</strong></p>

<p align="center">
  Saw something useful, then forgot where? TrackMeUp helps you find it again.
  Search your activity, look through saved screenshots, and see where your time went. No TrackMeUp account needed, and no hidden cloud sync.
</p>

<p align="center">
  <a href="https://umbertogiacobbi.biz/trackmeup/?utm_source=github&amp;utm_medium=referral&amp;utm_campaign=trackmeup&amp;utm_content=readme_product_page"><strong>Product page</strong></a>
  ·
  <a href="#remember-the-moment-not-the-tab"><strong>See what it does</strong></a>
  ·
  <a href="#get-trackmeup"><strong>Build locally</strong></a>
  ·
  <a href="docs/PRIVACY.md"><strong>How your data is handled</strong></a>
</p>

<p align="center">
  <a href="https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml"><img src="https://github.com/umbertotechnopreneur/TrackMeUp/actions/workflows/build.yml/badge.svg?branch=main" alt="Build status" /></a>
  <img src="https://img.shields.io/badge/platform-Windows-0078D4?logo=windows11&amp;logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/status-pre--production-F9665B" alt="Pre-production" />
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-2EA44F" alt="MIT License" /></a>
</p>

> [!IMPORTANT]
> **Beta 1 is coming soon.** We're spending the next two months checking how the app handles AI and private data before sharing the first public beta.

## Remember the moment, not the tab

You know you saw it today. Was it in the browser, a document, or another app? TrackMeUp gives you a way to retrace your steps.

<table>
  <tr>
    <td width="33%">
      <strong>Find it again</strong><br />
      Search your activity, app names, window titles, saved screenshots, text read from images (OCR), and optional AI descriptions in one place.
    </td>
    <td width="33%">
      <strong>Pick up where you left off</strong><br />
      Check what you were working on before a call, a break, or an interruption.
    </td>
    <td width="33%">
      <strong>See where your time went</strong><br />
      Look back at the apps you used, time spent active or away, and daily reports. Compare days to spot patterns.
    </td>
  </tr>
</table>

TrackMeUp counts key presses and mouse clicks to tell active time from idle time. It **doesn't record which keys you press or what you type**. Screenshots are a separate option and can include text visible on screen.

## TrackMeUp in action

These previews use made-up demo data and show the app in English, Italian, and Vietnamese.

<p align="center">
  <img src="docs/images/readme/trackmeup-live-tracking-it.png" alt="TrackMeUp live activity tracking in Italian" width="100%" />
  <br />
  <sub><strong>Live tracking · Italiano</strong> — see elapsed time, key-press and click counts, and recent activity.</sub>
</p>

<table>
  <tr>
    <td width="50%" valign="top">
      <img src="docs/images/readme/trackmeup-captured-moments-en.png" alt="TrackMeUp Captured moments inspector in English" width="100%" />
      <br />
      <sub><strong>Captured moments · English</strong> — open a saved screenshot and browse nearby moments on the timeline.</sub>
    </td>
    <td width="50%" valign="top">
      <img src="docs/images/readme/trackmeup-local-search-vi.png" alt="TrackMeUp local search and OCR in Vietnamese" width="100%" />
      <br />
      <sub><strong>Local search and OCR · Tiếng Việt</strong> — find something by its app, screenshot text, or optional AI description.</sub>
    </td>
  </tr>
  <tr>
    <td width="50%" valign="top">
      <img src="docs/images/readme/trackmeup-activity-history-en.png" alt="TrackMeUp Activity history in English" width="100%" />
      <br />
      <sub><strong>Activity history · English</strong> — see when you were active and check the daily numbers. These aren't productivity scores.</sub>
    </td>
    <td width="50%" valign="top">
      <img src="docs/images/readme/trackmeup-world-clocks-it.png" alt="TrackMeUp World clocks in Italian" width="100%" />
      <br />
      <sub><strong>World clocks · Italiano</strong> — check the time, sun and moon information, and optional weather in different cities.</sub>
    </td>
  </tr>
</table>

## How TrackMeUp works

1. **Track your day.** TrackMeUp saves active and idle time, app and window details, key-press and click counts, and selected system measurements on this PC.
2. **Choose what else to save.** Screenshots, text recognition on your PC, and analysis by an AI provider are separate options. You can use the tracker without them.
3. **Look back when you need to.** Search your history, open a screenshot, browse the timeline, or make a daily report and summary.
4. **Bring history from another installation.** Export a `.tmuarchive` with your history and, if you choose, saved screenshots. You can preview it before confirming an import. Each installation keeps its own identity, name, color, and icon so you can tell where the records came from.

Prefer the terminal? There's also a command-line interface (CLI) for PowerShell and scripts. It controls the same tracker as the desktop app.

### Built for Windows

TrackMeUp uses WinUI and native Windows controls. The desktop app doesn't need a browser running inside it, which helps keep it light. Interactive reports open when you need them and don't require WebView2 to stay running in the background.

The app, reports, and readable CLI output can follow your Windows language. You can also choose English, Italian, French, German, Spanish, Simplified Chinese, Vietnamese, Korean, European Portuguese, or Brazilian Portuguese.

You can choose separate languages for the interface, search, and text recognition (OCR). Search supports all the app's languages. OCR needs a supported Windows language pack installed on your PC. Vietnamese works for the interface and search, but isn't available for Windows OCR.

Search also understands 300 curated concepts in each supported language, with synonyms for documents, messages, meetings, development, spreadsheets, payments, and more. For example, Italian `copia di sicurezza progetto` can also find `backup progetto`. Synonyms work locally and can be turned off in search settings.

## You're in control of your data

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v2-dark-erasure-wave-2400x800.png" />
    <source media="(prefers-color-scheme: light)" srcset="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v3-light-radial-reset-2400x800.png" />
    <img src="design/branding/atomic-nuke/output/trackmeup-atomic-privacy-banner-v3-light-radial-reset-2400x800.png" alt="Privacy has a nuclear option: saved moments disappear into a glowing reset point" width="100%" />
  </picture>
</p>

You choose what TrackMeUp saves, how long it keeps it, and when to delete it.

- **No account needed.** You can use the app without signing up.
- **No hidden sync.** TrackMeUp doesn't upload your activity to its own cloud.
- **Optional extras.** Screenshots, AI assistance, and location sharing stay off until you turn them on.
- **Privacy rules.** Exclude selected apps, windows, or details from tracking.
- **Your schedule.** Choose how many days to keep your history and screenshots.
- **Start fresh.** **Nuclearize everything** deletes this installation's saved data and restarts the app with default settings. It asks you to confirm twice to avoid an accidental reset.

The reset doesn't remove exported or shared files, copies held by other services, API keys in Windows environment variables, or Windows package and certificate settings. Deletion isn't a guarantee that data cannot be recovered from the disk or a backup.

See [how TrackMeUp handles your data](docs/PRIVACY.md) for the full details, including what optional services receive.

## Start with the features you need

You can track your activity without taking screenshots or using AI. Turn on either if it helps you find things later.

| Setup | What it adds | What leaves this PC |
| --- | --- | --- |
| **Activity timeline** | Active and idle time, app and window details, reports | Nothing unless optional diagnostics are enabled |
| **Screenshots and text recognition** | Saved screenshots and text read from them on your PC | Nothing unless optional diagnostics are enabled |
| **AI descriptions** | Descriptions or improved screenshot text from your chosen AI provider | The data included in each enabled provider request, plus optional diagnostics if enabled |

The table covers tracking, screenshots, and AI analysis. Exporting files and
sharing screenshots or logs happen when you choose. World-clock weather and
provider pricing downloads have their own requests, described in the
[privacy guide](docs/PRIVACY.md#what-can-leave-the-pc).

You choose your AI provider and model. API keys are stored in Windows environment variables and sent directly to the selected provider to authenticate requests. Don't put keys in CLI arguments; TrackMeUp won't accept them there.

## Get TrackMeUp

> [!NOTE]
> TrackMeUp is still in development. There isn't a public download yet. For now, you can try it by building from source.

You'll need:

- Windows 10 version 1809 or later.
- PowerShell 7.
- .NET 10 SDK.
- x64 or ARM64.

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

The script builds the reports and writes to a fresh directory under `artifacts/unpackaged/<version>/<platform>/`. Use `-ReleaseVersion X.Y.Z -PublishOutputPath <empty-directory-under-artifacts>` for a deterministic release build. Extract the **entire** portable release ZIP and run `TrackMeUp.exe`; .NET, WinUI, and Windows App SDK are included. Keep the executable together with its DLLs, resources, and subfolders. Data remains in `%LOCALAPPDATA%\TrackMeUp`.

> [!IMPORTANT]
> **Portable limitations:** interactive reports require the separately installed Microsoft Edge WebView2 Runtime. Without it, the reports window shows an initialization error. On-device screenshot OCR requires MSIX package identity and is unavailable in the portable edition; failed OCR is recorded without discarding the screenshot.
>
> Neither feature is initialized just to open the main player, so these limitations are not missing-library requirements for basic startup. A restored reports window can show the same report error during startup. The x64 portable has passed an extracted `--version` launch check; full UI startup on clean x64/ARM64 machines remains a separate release check. See [portable requirements and verification](docs/RELEASING.md#portable-startup-and-feature-limitations).

The same script can create an MSIX package for local installation or an installer. You'll find the package files in `artifacts/packages/` and installers in `artifacts/installers/`.

On Windows, `PackageMsix` and `CreateInstaller` sign the package automatically with the local TrackMeUp test certificate (`CN=umber`). If it is missing, the script creates it in the current user's certificate store, exports the public certificate to `artifacts/certificates/TrackMeUp-Test-Signing.cer`, and trusts it for the current user. To use another certificate already installed in `Cert:\CurrentUser\My`, pass `-PackageCertificateThumbprint <thumbprint>`. The test certificate is intended only for local sideloading; production releases must use a certificate issued for distribution.

The [release preparation guide](docs/RELEASING.md) describes the x64/ARM64 MSIX and portable ZIP workflow, common version, bundled runtimes, and SHA-256 checksums. While distribution certificates are pending, the workflow produces unsigned archives and draft Releases only. MSIX packages require signing before installation; portable executables can be launched directly.

## Using the terminal

See the [practical CLI examples](docs/CLI_EXAMPLES.md) for checking status, controlling tracking, taking screenshots, making reports, previewing cleanup, and using the app in scripts.

Once the package is installed, try:

~~~powershell
trackmeup.exe -cli status
trackmeup.exe -cli tracking start
trackmeup.exe -cli report today
trackmeup.exe -cli ai status
trackmeup.exe -cli retention preview
~~~

Run `trackmeup.exe -cli` with no command in PowerShell 7 to open an interactive menu. From there you can check live activity, control tracking and AI, take screenshots, open reports, troubleshoot, or change settings. It connects to the same tracker as the desktop app.

### CLI switches

Use `trackmeup.exe -cli --help` to see all commands. These shortcuts are handy for everyday use:

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

You can add these options to a command or shortcut:

| Switch | Purpose |
| --- | --- |
| `--format <rich|plain|json>` / `--json` | Select interactive, plain-text, or machine-readable output. |
| `--language <system|en-US|it-IT|fr-FR|de-DE|es-ES|zh-Hans|vi-VN|ko-KR|pt-PT|pt-BR>` | Choose the CLI language. Use the full code shown here; short forms such as `en`, `pt`, and `zh` aren't accepted. |
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

## Working on TrackMeUp

The repository script helps with builds, tests, and packaging:

The `BuildReports` action uses Node.js 24.16.0 (the CI runtime). Install it before rebuilding the reports web assets.

~~~powershell
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action Test -Platform x64 -WarnAsError
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action BuildReports
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action PackageMsix -Platform x64
pwsh -NoProfile -File .\scripts\TrackMeUp.ps1 -Action CreateInstaller -Platform x64
~~~

Want to help? Start with [the contributor guide](CONTRIBUTING.md). The [Windows setup guide](docs/DEVELOPMENT.md) walks you through getting a fresh copy, installing what you need, building and testing on x64, and fixing common setup problems. Use the [manual checks](docs/VALIDATION.md) to check how your changes look and behave.

<details>
<summary>Detailed checks for contributors</summary>

- [ ] Run `pwsh -NoProfile -File ./scripts/Test-FormattingHooks.ps1`: malformed C# indentation, tabs, CRLF line endings, trailing whitespace and a missing final newline must be corrected before commit, while a partially staged file keeps its unstaged bytes and commits only the formatted index content. The fixture must also verify filenames with spaces, staged formatting rules and failure of read-only verification on malformed source.
- [ ] Build the app for x64 and ARM64. Run `pwsh -NoProfile -File ./scripts/TrackMeUp.ps1 -Action Preflight -Platform x86` and verify that parameter validation rejects the unsupported architecture before running the action.
- [ ] Run `pwsh -NoProfile -File ./scripts/Test-ReleasePackaging.ps1`; verify common x64/ARM64 release versions, unchanged local version state, invalid-input rejection, required framework dependency checks, and refusal to install unsigned archives. Before a public release, complete the clean-machine x64/ARM64 installation checks in [RELEASING.md](docs/RELEASING.md).
- [ ] From a checkout without generated `bin/` or `obj/` files, run unsigned `PackageMsix` with an explicit release version for x64 and ARM64. Both first builds must resolve the WebView2 projection and produce packages with matching versions; no preliminary restore or second build should be necessary.
- [ ] Run `pwsh -NoProfile -File ./scripts/Test-PortableReleasePackaging.ps1`. Publish and archive `Release-Unpackaged` for x64 and ARM64 with one explicit version; verify bundled runtimes, matching executable architecture and metadata, and complete payload/archive checksums. Extract each ZIP on a matching clean Windows machine, run `TrackMeUp.exe --version`, and verify the UI opens without installing .NET or Windows App SDK. Check report availability with the WebView2 Runtime and the documented MSIX requirement for on-device OCR.

Search interaction check:

- [ ] Run the prepared synonym regression scenarios: load all 3,000 localized groups; find `backup progetto` with Italian `copia di sicurezza progetto`; preserve unmatched query words; verify multiple replacements, locale isolation, exact-match ranking, literal filenames, and the **Use synonyms** toggle.
- [ ] Open Activity history: verify Calendar is the initial tab, all six month rows are visible or reachable by scrolling, and Week shows Monday–Sunday with all 24 hourly rows. Select cells using mouse and arrow keys; verify side metrics and the selected outline without losing the heat color. Navigate rapidly between weeks and tabs, retry a failed query, check recorded zero versus no data, future dates, and daylight-saving gaps/repeated hours. Resize on a 150% display and check the legend, details and Close remain reachable. Open the selected day’s screenshots.
- [ ] Open local search, move focus to another window, and confirm it remains open without covering it. Enter at least three characters and confirm the local-index status and progress indicator appear until results are available, with no suggestion popup.
- [ ] In local search, select results by mouse and keyboard and verify the preview shows the corresponding screenshot at its original proportions, with its title, source, time, provenance, and highlighted text. Switch rapidly during image loading: an earlier screenshot must never replace the current selection. A missing image must show an explicit unavailable state. Open the selected capture with Open snapshot or Enter. Clear the query or return no results: the previous preview must disappear. With more than 20 matches, verify the displayed/total count is explicit.
- [ ] Check Acrylic behind the search field and results, including Windows transparency disabled and high-contrast mode. Resize and move Search, run another query, reactivate it, and reopen it: its chosen bounds must remain, with results, screenshot, and actions reachable.
- [ ] At 200% text scaling, search status and footer remain visible. The input-edge gradient flows only while active; the field shows no text predictions.
- [ ] While tracking, switch between Spotify and another app: after each sample, verify a 16×16 Windows window icon precedes the matching foreground-app name. A window without an available icon (including a protected or unresponsive window), idle context, and privacy-suppressed activity must show no icon or reserved gap. Icons are live decoration only and must not enter saved activity history.
- [ ] Open About for the first time from a non-primary display and verify that it centers on the player display with version, build date/time, and build commit all visible. Move it and reopen it: the saved placement must take precedence over centering.
- [ ] Trigger informational, success, warning, and error feedback. Confirm every toast uses an opaque severity-colored surface and border, and its timeout bar stays inside the toast frame.
- [ ] Set main-window and World Clocks opacity to 25%, including the Operations surface: toast text, fill, border, and countdown must remain fully opaque. Trigger a removal just before a minute boundary and verify the toast keeps its full timeout through the refresh.
- [ ] Open a standard acknowledgement and a destructive confirmation. Confirm both are WinUI dialogs with localized `OK`/`Annulla` actions, and that dismissing a confirmation does not execute it.
- [ ] Open the main menu and use both import/export commands under App settings. Confirm the main flyout and every submenu retain the shared 320 DIP minimum width at 100–200% display scaling; export opens the `.tmuarchive` destination picker, import opens the archive picker and preview, cancelling either picker changes no data, and a confirmed merge preserves the originating installation labels while skipping duplicate records.
- [ ] Start with retained OCR/AI history that references missing screenshots in the former flat screenshot directory, including deleted raw images whose stored counterparts have already moved into calendar folders. Restart to complete the explicit storage migration; export with and without screenshots, then preview/import into an isolated installation. Verify that text and timestamps survive, only existing images are included, every reference uses the current calendar layout, and a second startup requires no migration. An unknown directory layout or missing capture date must fail without partially changing history.
- [ ] Export synthetic history containing AI analyses and two-monitor OCR screenshots, with and without image files. Preview and import into a separate test installation, reopen the imported analyses, and repeat the import: screenshot references must resolve under the destination storage root and records must not duplicate. Export to a new nested directory through the application facade. If export fails, verify the localized notification identifies the export operation and points to diagnostics.
- [ ] Resize the screenshot schedule from its 620 × 480 DIP minimum to a maximized window, including 200% text scaling. Confirm Morning (00:00–12:00) and Evening (12:00–24:00) keep all seven days aligned on one time ruler, with 24-DIP quarter-hour rows, one shared vertical scrollbar, fixed day headers and visible Save/Cancel actions.
- [ ] In the screenshot schedule, focus and edit capture intervals of 1, 15 and 1440 minutes. Confirm the native NumberBox keeps the complete value visible with its clear button, opens increment/decrement controls in a compact popup and remains readable at 200% text scaling.
- [ ] Select and drag across quarters and adjacent days, including 11:45–12:15 and 23:45–24:00. Switch halves and back: selections, breaks, full-range labels and each half's scroll position must survive; a band crossing noon indicates its continuation. Touch swipes scroll without painting, while taps and keyboard Space toggle one slot. Save and reopen must preserve both halves; Cancel must discard edits, and the work-week preset/Clear all must affect both halves.
- [ ] Queue dialogs from two windows, close the waiting owner, then dismiss the active dialog: the closed owner's request must not appear. Exit with a dialog open and check that dialogs, pending requests, and toast timers are cleared. Check that a tray-hidden owner is restored for a standard dialog and its selected theme is respected.
- [ ] In World Clocks, choose a city and use **Aggiungi un altro**. Confirm the picker stays open, shows the `Orologio aggiunto` toast, removes that city from the choices, and accepts another addition; regular **Aggiungi orologio** should still close the picker.
- [ ] In World Clocks options, move cities up and down. Confirm the first up arrow and last down arrow are disabled, the reference city stays selected, the clock columns update immediately, and the new order survives closing and reopening the app.
- [ ] Check DST in Rome, New York, Sydney, and Ho Chi Minh City: each clock shows its state and, when active, the next end date. In compact layouts, keep the detail in the tooltip and accessible summary.
- [ ] Launch the installed app with no TrackMeUp process running. Confirm the borderless player opens without a startup or title-bar layout exception, tracking resumes according to its saved preference, its caption commands remain clickable, and a World Clocks window still reserves space for its native caption buttons at different display scales. Launch again while the player is open: the existing window must activate, the redirecting process must exit, and only one tracking runtime may remain.
- [ ] Resize the player between its 470 × 240 DIP minimum and a wider/taller window. Confirm the background cannot enlarge the layout, the title-bar commands stay within the right edge, metrics and AI spend reflow in narrow windows, and taller content scrolls vertically. Toggle sections and switch between player/options: manually selected bounds must be retained for each surface; closing and reopening restores the player bounds.
- [ ] During a slow city addition, verify Cancel, Esc, native close, and additional submissions cannot close or mutate the picker concurrently. Shut down during the pending addition: no late toast or control update should target the closed picker.
- [ ] In World Clocks, confirm each city skyline fills its clock column up to the side edges. In a tall window, skyline, atmosphere, and fade must stay anchored together at the bottom, leaving space above once the scene reaches native resolution; widening the column may still scale the scene to fill its width.
- [ ] In World Clocks, search for every European capital and sample the expanded USA, Australia, and Russia groups. Confirm all capitals are selectable and each of those three countries exposes ten supported cities with seasonal skyline artwork.
- [ ] In World Clocks, search for Ferrara, Domegge di Cadore, Bologna, and samples from the added European and South American cities. Confirm every result is selectable and shows the matching summer/winter Urban Wash skyline.
- [ ] In World Clocks with one, two, and three cities, use the title-bar layout icon to switch between the compact widget and detailed comparison. Confirm the compact widget keeps time, weather, skyline, and atmosphere while omitting solar/lunar detail; confirm the window chooses the content-led size, still permits manual resizing, and horizontal scroll begins before columns become unreadable.
- [ ] Resize World Clocks from tall to short with one, two, and twelve cities, including Windows text scaling at 200% and long translated weather labels. Confirm columns fill the available width, time/weather reflow without overlapping, solar/lunar detail and then daylight duration progressively hide, date changes remain visible, and overflow remains scrollable. Enlarge again to reveal detail; the explicit compact choice must never reveal the solar arc.
- [ ] In World Clocks, reach the layout icon with Tab and activate it with Space. Verify the OpenWeather logo floats at the bottom right over the scene, without a full-width footer band or overlap with UTC/daylight text; its localized tooltip and accessible name must identify the attribution. Resize manually, close/reopen, and wait through a minute refresh: the saved bounds must remain. With a custom reference instant, add/remove a city in options and return: content sizing must apply without switching back to live time.
- [ ] Resize World Clocks repeatedly from wide/tall to the 480 × 240 DIP minimum, with two cities and at 100%, 150%, and 200% display scaling, including moving between monitors. Confirm skyline, atmosphere, and fade share the same bottom edge and stay clipped to their column; extra height must increase the space above the scene after its scale limit. In compact mode, no mandatory skyline spacer should prevent further height reduction; overflowing clock text must remain scrollable.
- [ ] Open the reference-instant panel in a narrow/short World Clocks window, then resize while it is open. Confirm date and time share one row. At 200% text scaling and with long translated labels, verify the title, city, date, time, and time-zone text fit or scroll vertically, while Restore now and Apply remain visible and usable. Verify the title uses the selected UI language.
- [ ] In World Clocks, use the globe icon in the title bar to open the separate Acrylic map window. Confirm the clocks contain no embedded map and keep their bounds. The map follows the selected reference instant and shows night, dawn, day, sunset, the photographic Sun and Moon, and selected cities. Use the Moon button to open the separate phase window; verify the phase and illuminated percentage follow the same instant. Close either window independently and reopen it without creating duplicates. Verify localized tooltips and accessible names for both buttons.
- [ ] Put the Moon, map, and World Clocks windows over contrasting desktop content. Check the blurred glass behind text and around the map, including after focusing another window or hiding the title bar. Switch the app theme and verify readable text. Disable Windows transparency or enable high contrast: the system's opaque fallback must remain legible. Restore those settings and confirm Acrylic returns. Close and reopen the windows to check backdrop cleanup and reconnection.
- [ ] Compare Moon text with World Clocks: the phase summary uses the same light display font as clock values, and the reference instant uses the compact text font. Check wrapping in the smallest Moon window and at 200% text scaling.
- [ ] Shrink the map to 192 × 160 DIP and the Moon to 192 × 192 DIP, then enlarge them again. At 100%, 150%, and 200% display scaling, verify that the map's timestamp hides first, title text gives way to the logo, and all native minimize/maximize/close buttons remain visible and clickable when the title bar is shown. Repeat with automatic hiding disabled. Check the two-row compact legend, compact lunar text/tooltips, and restored compact bounds after closing/reopening.
- [ ] In Maintenance, switch between Export, Import, and Installations. Check that an archive preview shows its filename, creation date, separately labeled activity/AI/screenshot totals, screenshot size, and source computers. Open import/export directly from the menu: only the chosen operation should start. Preview, import, export, and installation updates must use the same owned Mica progress dialog with a descriptive title and an indeterminate horizontal bar; completion or failure must close it and restore the owner. Verify narrow windows, 200% text scaling, keyboard navigation, and shutdown during an operation in an isolated test installation.
- [ ] Import an archive with overlapping samples from two PCs across an hour and midnight boundary. Confirm the activity calendar and reports open, shared tracked/active time is counted once, and per-computer attribution remains available. An import failure must require a fresh preview before another attempt.
- [ ] Generate a report or daily digest, preview/run a confirmed retention cleanup, retrieve AI pricing, and save a screenshot copy. Verify reuse of the owned Mica progress component and restoration of the owner after success/failure. Capture/current-activity analysis must keep the original foreground context; indexing and batch reprocessing must retain their detailed progress and cancellation controls.
- [ ] With the world map visible, resize between narrow/tall and wide/short windows at 100%, 150%, and 200% display scaling. Confirm the complete map stays centered at 2:1 with empty margins when needed, continents retain their proportions, and lighting/city/celestial markers stay aligned. Verify the photographic Sun and Moon, their localized tooltips, and the Moon's shadow at new, quarter, full, waxing, and waning reference instants.
- [ ] Leave work windows open, close others, then exit and restart TrackMeUp. Confirm only the previously open work windows return with their saved positions and sizes; the OCR window restores its source screenshot and Licenses restores its About owner. Temporary configuration and confirmation dialogs stay closed. Repeat with the main window hidden in the notification area, an unplugged monitor, and a quick close immediately after opening a window.
- [ ] Move a work window, change an unrelated setting such as the theme, and restart: saved bounds and open/closed states must survive the settings change.
- [ ] In Options → Window, disable automatic title-bar hiding: all currently open windows and newly opened dialogs must keep their title bars visible; restart and confirm the preference persists. Re-enable it and move the mouse outside and back inside every window: after 300 ms of stable hover, the header fades over 150 ms without moving content or resizing the window. Brief crossings must cancel pending transitions. Verify a drag begun during the hide delay, menu interaction, native caption buttons, immediate keyboard access, and first-touch reveal during the show delay without accidentally activating a command. Repeat over report WebView content and owned dialogs. With Windows animations disabled, visibility changes must occur without fading.
- [ ] With automatic hiding enabled, verify only World Clocks, the map, and the Moon have title bars over their content: hiding them leaves no empty top strip and showing them does not move the scene. At 100%, 150%, and 200% scaling, check header readability, native caption targets, keyboard access, and touch reveal. Open World Clocks options or disable automatic hiding: the header must reserve space again so top controls remain usable. Other work windows and dialogs retain their existing layout.
- [ ] Move and resize live World Clocks, map, and Moon windows across a minute boundary: refresh must not be postponed by movement. Minimize and restore them, and switch between a fixed reference instant and live time. Simulate a settings persistence failure in an isolated test installation: saving or closing a window must show an error, keep the window available, and allow a later retry without terminating the app.

Privacy and runtime regression checks:

- [ ] With the player, Search, reports, world clocks, and an owned dialog open, change Windows scaling through 100%, 150%, 200%, and back to 100%; repeat by moving between monitors. Confirm immediate reflow, aligned caption hit targets, usable minimum sizes, and preserved user-sized player bounds. Repeat during a player resize animation, with a maximized report, and with a window hidden/minimized; restore it and check layout. Close a window during a scale change and confirm clean shutdown.
- [ ] Launch TrackMeUp twice from Start or its shortcut, including once while the player is hidden in the notification area. Confirm only one long-lived `TrackMeUp.exe` remains and the existing player is restored. Start the runtime through the CLI first, then launch the player and confirm the background owner becomes the UI process instead of leaving two processes running.
- [ ] With an instance running, launch `reports --theme dark`, a normal player launch, and `--background`: confirm each retains its requested surface and duplicate background launches stay headless. Promote a background instance with `--paused` or `--safe-mode` and verify automatic tracking stays disabled; invalid redirected arguments must fail before activation.
- [ ] Exclude a synthetic process/title/context and verify no activity is stored; disable each detail provider and verify titles/attributes are absent.
- [ ] With an excluded window on another monitor, verify the entire screenshot is blocked. Use only synthetic content for this manual Windows check.
- [ ] Interrupt screenshot deletion after file removal, retry/restart, and verify OCR and active search documents disappear. Retention must also expire OCR whose image is already absent.
- [ ] Hold an AI test provider pending: pause and AI-disable must complete promptly and cancel the request. AI-off/another-provider mode must not download the OpenAI pricing table.
- [ ] During an index update, existing results stay responsive and new captures appear after the next publish.
- [ ] After deletion, retention, or a failed rebuild, search remains consistent; a successful rebuild restores it.

</details>

## More about the project

- [How your data is handled](docs/PRIVACY.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Windows contributor setup and troubleshooting](docs/DEVELOPMENT.md)
- [Practical CLI examples](docs/CLI_EXAMPLES.md)
- [Manual validation guide](docs/VALIDATION.md)
- [Public roadmap](ROADMAP.md)
- [How project decisions are made](GOVERNANCE.md)
- [Changelog](CHANGELOG.md)
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

You can also open About in the app to find logs, report a problem, visit the project website, or check third-party licenses.

## Where to find things in the code

- <code>TrackMeUp/</code> — Windows desktop app and startup code.
- <code>TrackMeUp.Core/</code> — app behavior, storage, screenshots, AI connections, and the shared tracker.
- <code>TrackMeUp.Presentation/</code> — models used by the desktop interface.
- <code>TrackMeUp.Cli/</code> — command-line interface for PowerShell.
- <code>TrackMeUp.Reports.Web/</code> — files used to display local reports.
- <code>TrackMeUp.*.Tests/</code> — automated test projects.
- <code>scripts/TrackMeUp.ps1</code> — script for builds, tests, and packaging.
- <code>docs/</code> — privacy, testing, and development guides.
- <code>store/</code> — Microsoft Store listing and submission files.

## License

Unless a file says otherwise, TrackMeUp's project-authored source code and
documentation are open source under the [MIT License](LICENSE). The license
permits use, modification, distribution, sublicensing, and commercial use,
provided its copyright and permission notice are retained.

First-party C# files carry the concise `SPDX-License-Identifier: MIT` header;
the complete license text remains authoritative here at the repository root.

The MIT License does not grant rights to the TrackMeUp name, logos, wordmarks,
app icons, or other official brand artwork. Forks and redistributions must
follow the [Trademark and Brand Policy](TRADEMARKS.md) and avoid implying that
they are official or endorsed by the TrackMeUp project.

Third-party components, data, and assets retain their own license terms. Review
[Third-Party Notices](THIRD_PARTY_NOTICES.md), the
[asset licensing record](ASSET_LICENSING.md), and any adjacent attribution or
provenance file before redistributing repository material or packaged binaries.

---

<p align="center"><strong>MORE FROM UMBERTO</strong></p>

<h2 align="center">A few other things I'm working on</h2>

<p align="center">
  If you find TrackMeUp useful, you might like these too.
</p>

<table>
  <tr>
    <td width="33%" valign="top">
      <h3>⌨️ PromptMeUp</h3>
      <p><strong>Find the terminal command you need.</strong></p>
      <p>Describe what you want to do. PromptMeUp suggests a command and explains it, so you can check it before running it.</p>
      <p><a href="https://github.com/umbertotechnopreneur/PromptMeUp"><strong>Meet PromptMeUp →</strong></a></p>
    </td>
    <td width="33%" valign="top">
      <h3>🔎 ViewsApp.ai</h3>
      <p><strong>See how different AI models answer.</strong></p>
      <p>Compare what different models say about people, events, and stories. See where they agree and where their answers differ.</p>
      <p><a href="https://www.viewsapp.ai/?utm_source=github&amp;utm_medium=referral&amp;utm_campaign=trackmeup&amp;utm_content=readme_more_views"><strong>Explore Views →</strong></a></p>
    </td>
    <td width="33%" valign="top">
      <h3>🚀 Umberto Giacobbi</h3>
      <p><strong>Have something you'd like to build?</strong></p>
      <p>I'm the developer behind these projects. I also work with teams as a fractional CTO and help turn product ideas into working software.</p>
      <p><a href="https://umbertogiacobbi.biz/?utm_source=github&amp;utm_medium=referral&amp;utm_campaign=trackmeup&amp;utm_content=readme_author_cta"><strong>Let's talk about your next idea →</strong></a></p>
    </td>
  </tr>
</table>
