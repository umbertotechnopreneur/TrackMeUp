# Task Archive

This archive tracks completed development tasks for reference, historical alignment, and auditing.

## [2026-09-19] Install the celestial development MSIX

- [x] With explicit authorization for a local signed development package and version alignment, build and install x64 MSIX 0.0.11.0 over 0.0.10.0 using the existing trusted test certificate. No public release or tag was created.
- [x] Verify the source commit, package signature, twelve zodiac PNGs, celestial artwork, Windows package status `Ok`, and SHA-256 equality for all 1,337 checked installed payload files. The packaging report suite passed 60 tests. Preserve the final installer and matching dependencies under `artifacts/installers/celestial-0.0.11/`.
- [x] Complete PR #39 with all required x64/ARM64, lint and reports checks passing, squash merge to `main`, and delete its branch. Clean Release project outputs; installed-app visual scenarios remain open in todo.md. Deletion of the superseded intermediate package was blocked by the execution policy, so it remains under `artifacts/installers/celestial-20260919/`.

## [2026-09-19] Shared astronomical model and celestial desktop windows

- [x] Implement independent local sky, astronomical agenda and Earth windows with existing Acrylic, light typography, accent colors and the shared auto-hiding title bar. Add an astronomy launcher menu and independent saved window placement/session restoration. Preserve the original day/night map surface; only the new Earth window switches flat/globe.
- [x] Replace duplicate approximate solar/lunar calculations with one Core Astronomy Engine model and bounded caches, shared by existing clocks/Moon/maps and new views. Add source-attributed stars, computed events, offline Earth texture rendering, passive UI DTOs and versioned operations on the existing runtime.
- [x] Add computed Moon/planet conjunctions, a source-attributed eight-shower meteor catalog with explicitly approximate peaks, and informational tropical zodiac sectors. Create twelve individual transparent zodiac PNGs, planetary/event artwork and a transparent landscape; record generation prompts and provenance. Render dawn/day/dusk/night with 24 smoothly interpolated palettes driven by actual solar elevation.
- [x] Validate x64 WinUI compilation (zero warnings/errors), 770 Core tests, 372 Presentation tests and 96 CLI tests. All ten localization catalogs have matching keys/placeholders. CI results are recorded on PR #39; installed visual checks remain tracked in todo.md.

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
