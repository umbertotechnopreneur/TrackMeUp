# Task Archive

## 2026-09-21 — Restrained export action icons

- Added colored icons alongside localized captions for Generate summary, Save preferences and Export. Secondary commands remain text-only. The icons use system colors in high contrast and are excluded from the accessibility tree; the owning buttons retain localized names and tooltips.
- Updated the unexecuted localization regression checks to accept both string content and tagged text children, and to keep icons limited to those three actions. Source changes only; no build, tests or app restart.

## 2026-09-21 — Export button caption initialization

- Added explicit string content to all nine export-window text buttons. The shared localizer previously treated null content as a visual-only command and assigned an accessible label without a visible caption. Existing localization keys and icon-only command behavior remain unchanged.
- Added regression coverage for text-button content and caption/header availability across ten locales. Source-only change at the owner's request: tests, compilation and app restart were not performed. C# formatting and verification passed; runtime acceptance remains in todo.

## 2026-09-21 — Stable player label selector width

- Changed the selector from a content-dependent maximum width to a fixed 155 logical pixels. Existing full-name tooltips remain available. Build and visual acceptance are pending; no tests were run for this adjustment.

## 2026-09-21 — Free quotas and Premium action surfaces

- Implemented serialized Core guards for three Free labels and clocks, screenshot schedule saves and archive export/import execution. Free archive previews remain available. Downgrade preserves existing catalogs and selection; guarded actions use the standard upgrade notice, not a new Store purchase integration.
- Added shared title-bar Premium badges to schedule/report export and the archive page, plus a badge beside Add clock. Removed the label badges, redundant history submenu, manual clock-density command and obsolete localized strings. Added left-aligned label management, colored editor action icons, borderless appearance choices, and the main-menu style for celestial actions.
- Changed snapshot conflict detection to compare JSON content while preserving scalar conflicts and rejecting malformed JSON. The owner's archive was inspected only; no import or data mutation was used for verification.
- Debug x64 unpackaged app build passed with zero warnings/errors; C# formatting and verification passed. Replaced the running MSIX process with the updated local Debug EXE with owner approval. The first focused test run passed 241/252 checks; corrected incomplete fixtures and stale UI contracts compile without warnings, but their repeat execution awaits approval. Remaining automated and manual verification is tracked in todo.

## 2026-09-21 — Activity export workspace

- Added a native three-section Mica export window with date/device filters, saved field preferences, bounded previews and Excel, zipped CSV and typed JSON output. Free users can configure and preview everything; the shared application facade checks Premium before writing. The upgrade message does not implement a Store purchase flow.
- Added optional editable AI summaries from selected saved text, with explicit generation, shared cancellation, provider usage accounting and daily limits. Ordinary previews and exports do not contact an AI provider. Sensitive fields are opt-in; measured activity counters remain separate from screenshot descriptions.
- Added atomic destination writes, CSV formula protection, long-text continuation sheets for Excel, localized controls in all ten languages, IPC operations and technical export guidance.
- The authorized focused checks passed: 85 Core checks on the initial run, the eight affected export checks after fixing their isolated screenshot fixture and receiving approval to rerun them, and four presentation boundary checks. No real-provider calls, installed-app changes, Store packages, commits or pushes were performed. Manual UI and real Excel acceptance remain in todo; preserve the owner's requested unpackaged Debug app for testing.
- Final x64 Debug unpackaged compilation passed with zero warnings/errors, and the repository C# formatter verification passed. The unpackaged executable is a local development artifact, not a commercial distribution package.

## 2026-09-21 — Premium CLI operational refresh

- Added search with filters/pagination, date-range reports, clock-only queries/conversion, hardware readings, screenshot gallery/deletion/migration, archive export/import, AI model/price/connection/reprocessing operations, log opening, access status and double-confirmed reset. All operations use the existing shared facade; celestial and window-only functions remain outside CLI scope.
- Added default previews and explicit confirmation for maintenance. Import/reprocessing use runtime-owned expiring plans; reset reports acceptance separately from asynchronous deletion/relaunch. Export preview describes the request and explicitly warns that confirmed export can replace its destination archive.
- Classified the entire CLI as Premium, including help, version, diagnostics and shell. The router fails closed on unavailable access, denies Free with exit code 11 and rechecks access during shell/watch use. The CLI cannot grant itself Premium; the existing unlicensed production default and desktop Debug simulation remain explicit.
- Updated all ten CLI help locales, feature titles, the visible README callout, command reference, Premium guide and scenario checklist. Documented the evaluated Debug unpackaged launch command and the custom configuration's missing DEBUG symbol.
- x64 Debug CLI/test compilation and formatting verification passed; the authorized CLI suite passed 163/163 tests. No installed-app smoke test, personal-data mutation, provider request, reset, MSIX package, commit or push was performed. Installed runtime acceptance remains in todo.

## 2026-09-21 — Menu placement and label-management dialog

- Moved the three quick actions from the player body to the top level of the More menu, preserving its existing navigation hubs. Reduced the Premium badge and selector corner radius, aligned the badge to the left of the selector and added a narrow-header second row.
- Replaced the inline settings editor with its title, a localized product description and Manage labels. Added a resizable Mica dialog using the shared modal queue, existing facade-backed editor, separate window placement and a scrollable body. Editor actions adapt to translated caption widths and stack when needed. Existing feature-access rules are unchanged; interactive acceptance remains in todo.md.
- Verification: repository C# formatter and Verify passed; the four changed/new XAML files parsed and the three new keys are present in all ten locale catalogs. No automated tests, build or installation performed for this change.

## 2026-09-21 — Central feature access and Debug profiles

- Added the shared feature catalog, runtime-owned license policy, and reusable `FeatureGate` title/badge/access container. Saved labels are the first Premium feature; the badge remains visible in both profiles. Settings mutations are checked inside Core for direct, CLI and IPC callers, including saved-label selection through the taskbar text setting.
- Added a Debug-only main-menu switch between Free and Premium. Its override is runtime-local and is not persisted; downgrade clears a selected saved label while retaining definitions/history. Dashboard events distribute access state to views. Commercial licensing remains an explicit integration boundary with an unlicensed Free default; Store purchase/entitlement integration is not included.
- Debug x64 app compilation and a local compilation without the DEBUG symbol passed with zero warnings/errors. Static assembly metadata confirmed the non-DEBUG Core has no simulation method, override field or debug IPC operation. FeatureAccessPolicyTests compiled without execution; scenarios and extension guidance are in DEVELOPMENT_CHECKLIST.md and PREMIUM_FEATURES.md. Visual acceptance and approved test execution remain pending.

## 2026-09-21 — Data transfer appearance

- Added compact localized tab headers with blue export, green import and violet installation icons, theme/high-contrast brushes and uniform content spacing. Archive commands and preview behavior are unchanged.
- Maintenance reuses the existing thin desktop GlassBackdrop; returning to the player or settings restores their regular desktop Acrylic. System accessibility/transparency policy remains handled by the shared backdrop.
- x64 Debug compilation passed with zero warnings/errors; C# formatting, scoped diff and XAML structure checks passed. No tests or data operations run, no installation or commit; visual acceptance remains pending.

## 2026-09-21 — Separate public documentation from internal planning

- Move the historical screenshot proposal and publication checklist to the owner's private MeUp notes. Split Store account procedures from public listing and validation guidance.
- Remove stale links, record the private-note policy and ignore the reserved local Partner Center metadata export.
- Inspect the documentation diff and moved-note links. No builds, tests, commits, pushes or branches were created.

## 2026-09-21 — Saved labels and player cost visibility

- Added saved label definitions with editable names, 16 icons and eight colors. Core validates create/update/delete/select commands through the existing serialized settings facade. Renaming the active label updates the selection; deleting it clears the selection without rewriting historical activity. Existing taskbar one-off text labels remain supported.
- Added a searchable icon/color picker to main settings and an optional label dropdown to the right of the player timer. Exposed the monthly AI cost toggle in main settings with an explanation of provider amounts versus local estimates; estimates have an explicit player caption. Added strings in all ten locales and scenario checks.
- Local x64 Debug compilation passed with zero warnings/errors. No tests, installation, packaging, commit or push for this change; visual acceptance remains in todo.

## 2026-09-20 — Shutdown lifecycle corrections

- Celestial windows invalidate pending rendering ownership before cancellation and no longer update the loading indicator after closure.
- Shutdown toast dismissal stops timers and completes synchronously, without starting a fade or queueing a dismissal callback. Ordinary dismissal remains animated; unload invalidates pending animation completions.
- Built and signature-verified local x64 Debug 0.0.30; formatting passed and build outputs cleaned. No tests run. These defects are corrected in code, but their relationship to the observed 0.0.27 crash remains unproven because the minidump omits the original stowed exception. Keep debugger reproduction pending.

## 2026-09-20 — Agenda current-time position

- Added an accent marker with localized Now/reference label and city-local timestamp between chronological agenda events. Highlighted ongoing intervals with a border and text label; instantaneous events are not treated as ongoing. Core supplies interval state and marker position.
- Reused the existing live minute refresh and localization keys. x64 Debug build completed with zero warnings/errors; formatter verification passed. No tests run, no installation performed, visual acceptance pending.

## 2026-09-20 — Finish the product README structure

- Put an actual activity preview, source setup, and essential limitations directly after the product description. Move the world-clock preview into the extras section.
- Preserve the detailed contributor scenarios and celestial implementation notes in docs/DEVELOPMENT_CHECKLIST.md and docs/CELESTIAL_DESKTOP.md, with links from the README and validation guide. Update the repository rule for future scenario entries.
- Documentation only; no builds, tests, commits, pushes, or branches were created.

## 2026-09-20 — Apply the shared MeUp presentation

- Shorten the README headline, keep the product purpose first, and add the shared product links and author signature.
- Add the coordinated concept illustration and its exact generation prompt, provenance, and visual style guide. Present sky, local weather, space weather, and world clocks together as optional desktop extras, using the existing real screenshots.
- Changes remain local. No build, test, formatter, linter, commit, push, or branch creation was performed for this update.

## 2026-09-20 — City-local aurora and two-line weather

- Moved current advisories onto individual world-clock items. Aurora eligibility uses approximate magnetic latitude from both coordinates, Kp and local darkness; agenda forecasts use the same geographic gate and bounded darkness samples. Expired advisories are excluded.
- Weather conditions and optional space-weather advisories occupy separate rows, removing the former 132-pixel advisory limit.
- Built, signed and installed local Debug 0.0.27.0 (package status Ok), including the distinct moonset artwork. x64 compilation, formatting and signature verification passed. Cleaned build outputs and superseded task-generated 0.0.24–0.0.26 package directories, retaining the new installer. No test suites were run; visual acceptance remains pending.

## 2026-09-20 — Distinct moonset artwork

- Generated moonset-v1.png with built-in ImageGen using the existing lunar-horizon illustration as a style reference. Preserved original RGBA pixels and recorded prompt/hash in the artwork provenance.
- Moonset now selects the new image; Moonrise retains the original asset. Installation/visual acceptance of the new asset remains pending.

## 2026-09-20 — Compact search and restore celestial views

- Search now opens compact, expands for results and uses the shared thin GlassBackdrop. Lowered its native minimum size so saved expanded bounds no longer dictate an empty opening.
- Removed the visibility-based magnitude-eight validation cutoff: the embedded Proxima Centauri entry at magnitude 11.13 is a valid catalog entry. This previously blocked both celestial views.
- Built, signed, and installed local Debug 0.0.26.0. Confirmed compact Search dimensions and populated Local Sky/Agenda through the installed UI. Formatting passed; no test suites were run. Retained the installer and cleaned build outputs.

## 2026-09-20 — Increase window snapping distance

- Increased snapping from five to ten physical pixels. Near-edge overshoot no longer immediately suppresses snapping; free movement for the remainder of the drag begins beyond ten pixels outside the starting monitor. Updated boundary/corner scenarios and README behavior.
- Compiled Core and its test project successfully without running tests; formatted and verified C# sources. Built, verified the signature, and installed x64 Debug 0.0.25.0. User visual checks remain in todo.md.

## 2026-09-20 — Remove the public roadmap

- Remove ROADMAP.md and its README link. Update the feature request checklist to refer only to existing issues. Changes remain local; no tests or CI were run.

## 2026-09-20 — Repair installed application startup

- Restored 28 missing celestial/weather translations in each of eight catalogs. The installed app failed during LocalizationService initialization before constructing its main window; strict catalog validation remains enabled.
- Verified all ten catalogs contain the same 1,195 keys and matching format placeholders. Built, signed, and installed local x64 Debug 0.0.24.0; cold launch creates a usable main window, minimizing registers the notification-area icon, and a subsequent Start-menu activation restores the same process with a visible foreground window. No test suites, push, or PR were run.


## 2026-09-20 — Clarify product documentation and author voice

- Put activity tracking and search in the first README headline. Simplify product copy and use the solo maintainer's voice in contributor, support, security, privacy, and roadmap documentation.
- Save the product writing preferences in AGENTS.md, including plain English, concrete benefits, and first-person singular author wording.
- Documentation changes only. No builds, tests, formatters, linters, or CI were run. Preserve unrelated work in progress.

This archive tracks completed development tasks for reference, historical alignment, and auditing.
## [2026-09-19] Install the unified UI development MSIX

- [x] At the user's request, build and install a signed local x64 MSIX from `2f2e865`, preserving existing source edits and using the already trusted development certificate. No test suites or CI were run. Update the existing package identity with `ForceUpdateFromAnyVersion` because the two working copies used independent development version counters.
- [x] Verify the package signature, matching source commit and absence of report assets. Windows reports the installed package as `Ok`; all six checked executable/assembly/build-information hashes match the MSIX, and the installed application is running with a main window. Preserve the MSIX and dependencies under `artifacts/packages/local/x64/ui-cleanup-2f2e865/` and clean Release outputs once. Full visual scenario checks remain pending.
## [2026-09-19] Unify desktop UI fixes and retire activity reports

- [x] Combine maintenance/settings and celestial changes on the single existing `codex/clarify-advanced-sensors` branch. Clarify screenshot/AI wording, remove App/PC diagnostics, add responsive privacy actions and accents, load retention criteria automatically on one wrapping row, preserve results across language/theme changes, and give the complete toast frame an opaque theme surface.
- [x] Replace the above-horizon scrollbar with accessible localized arrow controls. Improve constellation contrast and label placement, add the seven-star Little Dipper alongside the complete Big Dipper, and use the requested Italian IIS label. Preserve the existing celestial catalog, orbital markers, window layouts and optional magnetic snapping.
- [x] Remove the interactive and HTML report windows, menu/CLI/facade/runtime routes, digest preferences, Vue/Node sources and build/package/CI integration. Retire only the removed report preferences/window state atomically; preserve activity-calendar aggregation and existing user data/files. Update all ten catalogs and documentation.
- [x] Validate x64 app and test-project compilation with zero warnings/errors; verify C# formatting. The approved full run passed 1,261 of 1,263 tests and all 61 packaging checks. Correct the two identified regressions and pass the separately authorized recheck of 53 settings tests plus the affected presentation test. Required CI and installed-app visual verification remain tracked in todo.md.

## [2026-09-19] Install the enriched celestial development build

- [x] Build and install a locally signed x64 development MSIX 0.0.20.0 over 0.0.16.0, using the existing trusted test certificate. Windows reports `Ok`; the installed executable, Core assembly and reports entry point match the package SHA-256 hashes. Reopen the updated application and retain the validated installer under `artifacts/packages/local/x64/celestial-catalog-20260919-signed/`.
- [x] The initial packaging entry point unexpectedly ran its bundled 60 web-report tests before it could be interrupted; all passed. Finish packaging directly without invoking further test suites. Clean Release outputs; no push or CI was run. Two failed/interrupted package staging directories remain under `artifacts/packages/local/x64/` because cleanup was denied by execution policy.

## [2026-09-19] Enrich the local sky from catalog data

- [x] Move stellar coordinates, schematic constellation links, conjunction-body selection, satellite identities and 24 sky palettes out of C# and into strict embedded JSON. Expand the attributed SIMBAD subset to 76 stars and 16 figures, including all twelve zodiac constellations. Keep city/time-zone data in its existing SQLite catalog rather than duplicating it.
- [x] Add optional ISS/Tiangong projection from recent CelesTrak elements through SGP4. Request it only for Local sky; limit refresh to two hours, stop after non-200 responses, and omit stale, historical or below-horizon markers without disturbing offline ephemerides. Show localized symbols/names, additional bright-star labels, satellite markers and horizon entries.
- [x] Audit the recent celestial changes for embedded catalog facts, update source/license notices and README scenarios. C# formatter and verification passed; x64 Debug app build passed with zero warnings/errors. No test suite, installation, push or CI was run. Installed-app visual validation remains pending.

## [2026-09-19] Replace the celestial rendering with actual screenshots

- [x] Replace the inaccurate generated panorama with four distinct screenshots supplied by the owner: astronomical agenda, local sky, Earth globe and the separate flat day/night map. Copy the original PNGs byte-for-byte, verify their SHA-256 hashes and omit the duplicate globe attachment. Arrange them in a three-column README strip with alternative text and links to the full-size originals; retain the description and optional snap guidance below.
- [x] Replace the obsolete generated image/provenance with an original-screenshot provenance record. The previous rendering remains recoverable at checkpoint `1302c51`. Review the scoped documentation diff, image references and markup only; no app build, tests, installer update or push.

## [2026-09-19] Add the celestial desktop README showcase

- [x] Create and visually inspect a 3:1 ImageGen product strip blending world clocks, Moon, local sky, agenda, globe and the separate flat map. Refine the blue-hour and Moon–planet illustrations, then copy the final PNG without local pixel edits. Record both prompts, dimensions, hash and brand-artwork provenance beside the asset.
- [x] Add a responsive full-width image after the existing gallery, explicit rendering disclosure, alternative text and a description below covering the independent Acrylic windows and optional five-pixel snap/escape behavior. Correct nearby outdated menu/title-bar guidance and clarify that the 35-day horizon applies to conjunctions. Review documentation and local references only; no application build, tests, installer update or push.

## [2026-09-19] Add optional magnetic window snapping

- [x] Add a shared Core geometry engine and owning-thread native registrations through the application facade. Snap visible window edges within five physical pixels to app peers or the monitor work area, with raw cursor anchoring to avoid cumulative drift. Crossing the starting monitor's physical bounds suppresses snap until the next drag; intentional off-screen/edge placement is not clamped back by subsequent DPI layout. The disabled setting leaves native movement untouched. Closed/hidden/minimized/maximized/cloaked peers are excluded, native failures are reported after dragging, and subclass cleanup remains on its owner thread.
- [x] Add enabled-by-default `window.snapping.enabled` with a persisted Settings toggle, immediate application to open windows, accessibility and all ten UI translations. Add geometry/settings unit scenarios and the README manual checklist. Source review, formatting verification and XAML/localization parsing completed; no test suite or push/CI was run. The user requested a local post-implementation checkpoint followed by a signed development MSIX build and in-place update; installer verification is recorded beside the generated package, while visual validation remains in `todo.md`.

## [2026-09-19] Remove sky zoom and keep the scene centered

- [x] Remove the sky zoom slider, gesture handling, synchronization events and obsolete localized labels. Replace the scroll/zoom viewport with a bounded grid and stretch canvas, using the actual viewport center and explicit clipping. The full sky now adapts to window size without panning or magnification. Update the manual scenarios; no test suite was run.
- [x] Shorten the original flat-map menu label to “Mappa giorno/notte” with localized equivalents, without changing its window title or opening tooltip. Collapse lunar phase/date labels and margins below 300 DIP of available width or height, restoring them at larger sizes and retaining accessible descriptions/tooltips. Confirm the shared close/shutdown paths save normal placement plus open/closed state; maximized/minimized state is not persisted.
- [x] Build and install signed x64 development MSIX 0.0.14.0 with the existing trusted certificate. Windows reports `Ok`; all 1,337 checked installed payload files match their MSIX SHA-256 hashes. Formatting, static XAML/localization review and compilation with analyzers passed; no test suite, push or CI was run. Preserve the installer under `artifacts/installers/celestial-compact-0.0.14/`. Include the accumulated celestial UI refinements in the user's requested local checkpoint; installed visual scenarios remain pending.

## [2026-09-19] Simplify the sky surface and coalesce celestial refreshes

- [x] Remove the upper sky card/border, extend its scene to the window edges and place the explanatory text next to the bottom city picker, preserving the full text in its accessible name/tooltip. Add a localized visible Zoom label and synchronize the slider with touch zoom. Replace atlas brush transforms with a fixed clipped image viewport for planetary/event thumbnails.
- [x] Coalesce duplicate live-minute renders from the independent timer and World Clocks while keeping exact explicit instants. Keep same-observer content visible during updates, preserve city-picker items, and distinguish pending/completed render keys; cancel obsolete resize requests without suppressing later work. Leave projection spinner ownership with the celestial view. A read-only check found the installed process responsive and no recent astronomy-related log errors; visual refresh confirmation remains pending. No test suite was executed.
- [x] At the user's request, compile and install signed x64 development MSIX 0.0.13.0 with the existing trusted certificate. Windows reports `Ok`; all 1,337 checked installed payload files match the MSIX hashes. Formatting/static review and compilation with analyzers passed; no branch, commit, push, CI or test suite was run. Clean Release outputs and preserve the installer/dependencies under `artifacts/installers/celestial-sky-0.0.13/`.

## [2026-09-19] Move celestial controls below the views and separate the globe

- [x] Locally remove the new Earth's flat/globe switch and render only the sphere. Move city/zoom controls to the bottom of all three celestial windows and remove their redundant reference date/time, while retaining agenda event dates. Measure the actual globe viewport and preserve the existing full-window flat map and shared disappearing title bar. Clarify the globe menu/window title in all UI locales and remove obsolete switch strings.
- [x] At the user's subsequent request, compile and install signed development MSIX 0.0.12.0 over 0.0.11.0, reusing the existing trusted certificate and unchanged, previously validated report assets. Verify Windows status `Ok` and SHA-256 equality for all 1,337 checked installed payload files. Formatting, XAML/JSON parsing and scoped diff checks passed; no test suite, branch, commit, push or CI was run for this correction. Clean Release outputs and preserve the installer/dependencies under `artifacts/installers/celestial-layout-0.0.12/`. Live visual validation remains in todo.md.

## [2026-09-19] Install the celestial development MSIX

- [x] With explicit authorization for a local signed development package and version alignment, build and install x64 MSIX 0.0.11.0 over 0.0.10.0 using the existing trusted test certificate. No public release or tag was created.
- [x] Verify the source commit, package signature, twelve zodiac PNGs, celestial artwork, Windows package status `Ok`, and SHA-256 equality for all 1,337 checked installed payload files. The packaging report suite passed 60 tests. Preserve the final installer and matching dependencies under `artifacts/installers/celestial-0.0.11/`.
- [x] Complete PR #39 with all required x64/ARM64, lint and reports checks passing, squash merge to `main`, and delete its branch. Clean Release project outputs; installed-app visual scenarios remain open in todo.md. Deletion of the superseded intermediate package was blocked by the execution policy, so it remains under `artifacts/installers/celestial-20260919/`.

## [2026-09-19] Shared astronomical model and celestial desktop windows

- [x] Implement independent local sky, astronomical agenda and Earth windows with existing Acrylic, light typography, accent colors and the shared auto-hiding title bar. Add an astronomy launcher menu and independent saved window placement/session restoration. Preserve the original day/night map surface; only the new Earth window switches flat/globe.
- [x] Replace duplicate approximate solar/lunar calculations with one Core Astronomy Engine model and bounded caches, shared by existing clocks/Moon/maps and new views. Add source-attributed stars, computed events, offline Earth texture rendering, passive UI DTOs and versioned operations on the existing runtime.
- [x] Add computed Moon/planet conjunctions, a source-attributed eight-shower meteor catalog with explicitly approximate peaks, and informational tropical zodiac sectors. Create twelve individual transparent zodiac PNGs, planetary/event artwork and a transparent landscape; record generation prompts and provenance. Render dawn/day/dusk/night with 24 smoothly interpolated palettes driven by actual solar elevation.
- [x] Validate x64 WinUI compilation (zero warnings/errors), 770 Core tests, 372 Presentation tests and 96 CLI tests. All ten localization catalogs have matching keys/placeholders. CI results are recorded on PR #39; installed visual checks remain tracked in todo.md.

## [2026-09-19] Simplify the Italian activity calendar wording

- [x] Replace the recorded-activity label with the user-requested "Uptime e attività", shared by the calendar subtitle and day status. Delivery is tracked in `todo.md`; no behavior changed.

---

## [2026-09-19] Create a local installer and update the installed app

- [x] Build a signed local x64 MSIX from the current workspace at the user's request, preserving existing source edits. The packaging route's explicitly approved report run passed all 60 tests; production report checks and package creation succeeded.
- [x] Update the installed package from 0.0.7.0 to 0.0.9.0 and reopen the updated app. Windows reports `Ok`; SHA-256 verification confirms all 1,317 checked payload files match the installer. Preserve the installer, dependencies, and deployment verification record under `artifacts/installers/local-update-20260919/`.
- [x] Clean the x64 Release build outputs and remove superseded package staging/installer directories only after validating their resolved paths under repository `artifacts/`. No additional .NET test suite or manual UI scenario was run; those pending checks remain in `todo.md`.

---

## [2026-09-19] Place sensor graphs behind compact component rows

- [x] Render each activity trace as a muted background behind identity, value and temperature, removing the separate graph row/column. Keep battery charge in a slim level bar and hide decorative traces in high contrast.
- [x] Share the 148 px narrow / 96 px wide row heights with pagination, so a 476 × 476 logical-pixel track area fits three devices. Preserve missing-sample gaps and accessible collection status.
- [x] Update existing layout scenarios and the README manual checklist. The x64 Debug app build passed with zero warnings/errors; formatting verification and scoped diff review passed. No further local test run or app installation was performed; required PR CI and the installed-app visual check remain pending.

---

## [2026-09-19] Keep the activity calendar closed at startup

- [x] Confirm local `main`, `origin/main`, and the installed app's base commit match `9b31eb1`; the unwanted restore behavior was present on main.
- [x] Classify the activity calendar as a transient dialog and remove its startup opening path. Existing saved open flags no longer reopen it; the menu command and other workspace windows are unchanged.
- [x] Update the existing restoration scenario and README checklist. Formatting and diff review passed. Per the user's instruction, this correction is for the PR only; no further local tests or installation were performed.

---

## [2026-09-19] Bundle PawnIO and clarify advanced-sensor activation

- [x] Bundle the unmodified, signed PawnIO 2.2.0 installer in x64 hardware output, with a pinned download/hash and distribution notice. Builds verify it without execution; ARM64 remains excluded.
- [x] Install missing/older PawnIO from the signed-release installation helper or the explicit sensor activation action. Verify installation, preserve equal/newer versions, report consent cancellation/failure/restart, and retain ongoing setup when its caller closes. Allow setup time in the existing IPC deadline.
- [x] Explain administrator access and the included component in all ten supported languages. Use a wrapped **Install and activate advanced sensors** label with localized tooltip/accessibility text, keep the existing isolated collector, and document offline installation and manual verification.

Validation: x64 Debug build passed with zero warnings/errors; the app output includes the pinned installer with a valid namazso signature and matching SHA-256. The approved run passed 26 focused Core tests, 7 Presentation tests, 23 release-packaging checks, and 40 portable-packaging checks. The local signed Release MSIX was built and installed at the user's request; application/Core/Presentation/PawnIO hashes match the package and Windows reports a healthy installation. Its required reports build also passed 60 web tests. Formatting verification and PowerShell/XML/JSON parsing passed. Driver installation itself still requires real Windows consent; the remaining manual scenario is tracked in `todo.md`.

---

## [2026-09-19] Recover notification-area icons and consolidate shared code

- [x] Verify the live shell before hiding the player and restore lost icons on `TaskbarCreated`. Keep the player available after recovery failure and allow a later retry. Use version-4 selection/context-menu events and standard tooltips.
- [x] Add 11 scenarios using real native window callbacks with simulated shell registration, covering icon loss, repeated recreation, visibility, partial registration failures, retry, disposal, and activation.
- [x] Share native-resource cleanup, persisted installation-ID reads, accessible command labels, panel fade transitions, and screenshot style setters. Record broader AI-telemetry and application-facade follow-ups in the task list.
- [x] Require user approval before tests, including pushes that trigger CI, and prevent unapproved tests in command chains.

Validation: Core passed 707 tests with analyzers and warnings-as-errors. Formatting and verification passed; all six affected screenshot styles retain equivalent effective setters. Presentation passed 362 tests and failed one source-contract assertion referencing an extracted helper; the assertion was updated. Further local tests were stopped at the user's request. At the user's subsequent request, built and installed signed Release x64 MSIX 0.0.5.0 and verified matching application/Core/Presentation assembly hashes. The updated local Presentation suite and manual tray/UI scenarios remain pending in `todo.md`; no performance gain is claimed without measurement.

---

## [2026-09-18] Implement the approved Sensors layout

- [x] Group device identity and capacity on the left, show temperature in a dedicated column, and simplify activity graphs. Retain Acrylic as requested.
- [x] Show explicit unavailable temperatures and localized labels in all ten languages; reflow narrow windows and page devices instead of hiding their identity.
- [x] Remove obsolete capacity-bar presentation data; preserve the battery charge indicator and existing hardware collection.

Validation: 17 focused presentation tests passed; formatting, localization and XAML checks passed. Wide and narrow layouts were inspected on the installed app before the final Acrylic and unavailable-label refinements. The user stopped further UI automation with Escape. Built and installed x64 MSIX 0.0.7.0 and verified all three updated application binary hashes; final refinements were build-verified, not visually rechecked.

---

## [2026-09-18] Show disk space as text

- [x] Replace disk capacity bars with localized free/total space labels while retaining activity graphs and memory capacity bars.
- [x] Keep unavailable capacity explicit instead of substituting an occupancy percentage.

Validation: formatting and verification passed; 17 sensor presentation tests passed. Built and installed x64 MSIX 0.0.4.0, verified installed binary hashes, and checked all four disk labels in the installed Sensors window.

---

## [2026-09-18] Reduce agent context and redundant verification

- [x] Consolidate repository rules in `AGENTS.md`, preserving unique Copilot safety, architecture, licensing, and UI guidance; replace duplicate Copilot instructions with a reference.
- [x] Add scoped context reading, bounded output, concise reporting, and explicit-only delegation rules.
- [x] Skip build and runtime checks for documentation-only changes and clean only build/test output generated by the current task after its last use.

Validation: no builds, automated tests, formatters, or smoke checks were run for these instruction-only changes.

---

## [2026-09-16] Astronomy title-bar overlays and archive reliability

### Completed

- [x] Render auto-hidden title bars over World Clocks, map, and Moon content without a reserved top strip or reflow on hover. Retain the shared delay/fade and dock the header while editing clock options or when auto-hide is disabled. Other work windows and dialogs keep their existing layout.
- [x] Extract reversible overlay geometry into `TitleBarOverlayLayout`; capture header parents/rows after XAML `Loaded`, preserve resources, and bound the first-touch surface to the caption. Place map/Moon notifications below the overlay.
- [x] Diagnose the local export failure logged on September 16 at 00:28: the archive service tried to deserialize current SQLite screenshot path lists as JSON. Share the current store parser across export/import instead.
- [x] Test AI/OCR screenshot references with and without bundled files, remapping into a different installation, null/empty references, and repeated imports. Create an export destination directory before its temporary archive.
- [x] Diagnose the remaining export failure caused by OCR references to deleted raw screenshots left in the former flat directory. Complete the explicit storage migration for missing artifacts, using the same capture's canonical day or registered capture time, and detect metadata-only work. Preserve OCR/AI records and timestamps, reject unknown layouts, and cover export/import with and without surviving images.
- [x] Commit import data, ledger, and search invalidation together. A post-commit cleanup failure is logged without falsely reporting that the committed import failed; recovery retains the durable ledger as its source of truth. Cover rollback and cleanup failures with fault-injection tests.
- [x] Review recent UI/Core changes and fix timer postponement during move/resize, astronomy opening failures, and unhandled window-persistence event failures. Add comments at the layout, retry, and transaction boundaries. Record larger refactoring opportunities in the active task list.

Native hover, touch, theme, and DPI checks remain in the README manual checklist; automated contracts do not replace visual verification.

## [2026-09-15] Expand multilingual search synonyms and phrase matching

### Completed

- [x] Replace the 30-group catalog with 300 concepts per locale: 3,000 localized groups and 6,942 terms across all ten supported search locales.
- [x] Ship a schema-2 manifest and ten per-locale JSON files with stable concept IDs and twelve categories. Validate complete coverage, metadata, term limits, and normalized duplicates with file/group diagnostics.
- [x] Match longest embedded phrases and combine substitutions from original query spans with a deterministic 32-variant default budget.
- [x] Preserve literal paths, filenames, addresses, and URLs; support Unicode boundaries including supplementary Han characters; keep script and Portuguese locale resolution explicit.
- [x] Use the best synonym variant score so documents do not accumulate relevance merely by listing aliases.
- [x] Prepare regression scenarios for catalog failures, multi-span retrieval, locale isolation, punctuation, literal tokens, large-catalog limits, the toggle, and exact-match ranking.
- [x] Document catalog editing and restart behavior; synonym updates require no index rebuild.

### Validation

- Repository formatting hook enabled; Format-Code.ps1 and its -Verify mode passed.
- Solution restore passed for x64.
- Full solution Release-Unpackaged x64 build passed with warnings as errors: zero warnings and zero errors. Test assemblies compiled; automated tests and CLI smoke tests were not executed.
- Static JSON validation confirmed 300 matching concept IDs per locale, two to five terms per group, and no normalized term collisions.
- SHA-256 comparison confirmed that the built app contains the manifest and all ten catalogs unchanged.
- git diff --check passed. Publication through a commit and pull request was requested after implementation validation.
- English and Italian equivalences received an additional semantic review; no independent native-speaker review or runtime latency benchmark was performed.

---

## [2026-06-05] Implement a standalone premium animated CLI identity banner demo in C# using Spectre.Console

### Plan
- [x] Create a minimal root-level project in `UgBannerDemo/` targeting `net10.0`.
- [x] Add Spectre.Console package reference and implement staged async animation in `Program.cs`.
- [x] Implement helper methods for name, copyright, and email reveal with safe Spectre markup composition.
- [x] Add width fallback behavior and strict cursor hide/restore handling with `finally`.
- [x] Validate with build and runtime execution using explicit dotnet executable path.
- [x] Upgrade Spectre.Console to `0.55.2` and refactor animation updates to `AnsiConsole.Live(...)` with typed panel rendering.

### Review
- `UgBannerDemo/UgBannerDemo.csproj`
  - Minimal console app project with `net10.0`, nullable enabled, implicit usings enabled.
  - Uses `Spectre.Console` version `0.55.2`.
- `UgBannerDemo/Program.cs`
  - Implements 5-phase, ~2.8s startup animation in a fixed-width identity plate.
  - Uses `AnsiConsole.Live(...)` for smoother frame updates instead of full-screen clear/redraw loops.
  - Includes scan-line effect, progressive name/copyright reveal, and email reveal with only `hello` bold.
  - Uses dynamic width clamping with fallback for narrow terminals.
  - Adds non-ANSI fallback output for redirected/non-interactive terminals.
  - Ensures cursor visibility is restored in `finally`.

### Validation
- `C:\Program Files\dotnet\dotnet.exe build e:\Tools\UgBannerDemo\UgBannerDemo.csproj` passed.
- `C:\Program Files\dotnet\dotnet.exe run --project e:\Tools\UgBannerDemo\UgBannerDemo.csproj` passed.
- Re-validated after package upgrade and refactor: build/run both passed.
- Final frame contains required lines:
  - `Umberto Giacobbi`
  - `Copyright © 2010-Present Umberto Giacobbi. All rights reserved.`
  - `hello@umbertogiacobbi.biz`

---

## [2026-09-01] Add world-clock loading, empty state, and weather-key feedback

### Plan
- [x] Show a centered localized loading message while world clocks are loading.
- [x] Support an empty clock selection with a clear `+` action that reuses the city picker.
- [x] Mask a configured OpenWeather key without loading or exposing its value.
- [x] Validate a submitted key with OpenWeather before saving and report each outcome inline.
- [x] Version the changed runtime contract and give the provider probe a dedicated IPC timeout.
- [x] Add focused Core and presentation coverage and run x64 validation.

### Review
- Explicitly empty selections are persisted; the last clock can be removed and the first can be added through the same catalog picker used by Options.
- Loading, empty, and populated surfaces are mutually exclusive, localized, and accessible.
- Key presence is represented by a fixed mask only; provider validation never returns or logs the secret.
- Accepted and rate-limited keys are stored in Windows User and Process environment scopes; rejected or unverifiable keys are not stored.
- World-clock snapshot and key-setting IPC operations moved to protocol version 4 so an older runtime fails explicitly instead of returning stale semantics.

### Validation
- `dotnet restore .\TrackMeUp.slnx -p:Platform=x64` passed.
- Solution-wide `dotnet format` exited successfully but reported two WinUI workspace-reference load warnings; targeted Core.Tests and Presentation.Tests format checks passed cleanly.
- `dotnet test .\TrackMeUp.Core.Tests\TrackMeUp.Core.Tests.csproj -p:Platform=x64 --no-restore` passed: 453/453.
- Focused `WorldClockWindowSurfaceContractTests` passed: 12/12.
- `dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64 --no-restore` passed with zero warnings and zero errors.
- The complete Presentation suite passed 169 tests and retained two unrelated failures in pre-existing OCR/AI Options work.
- Signed Release x64 MSIX `TrackMeUp-x64-20260901-090312.msix` installed over version `1.0.776.0`; package `824b187b-e347-4efa-9275-d4c169a4eb9e` is version `1.0.780.0`, x64, and reports status `Ok`. The application was not launched.
