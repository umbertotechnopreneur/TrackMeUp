# Task Archive

This archive tracks completed development tasks for reference, historical alignment, and auditing.

---

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
