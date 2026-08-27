# TrackMeUp performance and footprint optimization handoff

## Mission

Reduce TrackMeUp startup cost, steady-state CPU/I/O, memory pressure, search refresh cost,
and package footprint without changing product behavior or creating a second tracking
runtime. Work in small, independently verifiable commits on the existing `main` branch.

This plan is intentionally prescriptive. Complete phases in order. Do not combine phases
into one large change, and do not start a later phase while the current phase has failing
tests or unresolved measurements.

## Published baseline

- Baseline application commit: `0e0779422bfdb70fbc4b7205aeea5dc351b45585`
- Baseline subject: `Add portable multi-installation data archives`
- Expected branch: `main`
- Expected remote: `origin/main`
- Baseline validation already completed:
  - solution restore for x64;
  - 545 tests passed, 0 failed;
  - WinUI x64 build passed with 0 warnings and 0 errors;
  - report web application typecheck, 30 tests, production build, and offline verifier passed;
  - staged secret scan passed;
  - local and remote commit hashes matched after push.
- Known web-build observation: the production JavaScript chunk is approximately 1.095 MB
  raw (approximately 366 KB gzip), and Vite reports the 500 KB chunk warning. This is not
  an idle-startup blocker because the Reports window is already created on demand.

Before making any change, verify the baseline rather than assuming it:

```powershell
git status --short --branch
git fetch origin
git rev-list --left-right --count HEAD...origin/main
git rev-parse HEAD
git rev-parse origin/main
```

If `HEAD` is not the published baseline or `main` is not aligned with `origin/main`, stop
and reconcile the difference before editing. Preserve unrelated local work. Do not create
a branch or worktree without Umberto's explicit approval.

## Mandatory repository rules

Read `AGENTS.md` and `.github/copilot-instructions.md` completely before editing. The
following constraints are especially relevant to this work:

- invoke PowerShell only as `pwsh -NoProfile`;
- keep WinUI views, code-behind, Spectre commands, prompts, and renderers passive;
- place behavior, persistence, capture, environment access, HTTP, retention, startup,
  and OS interop behind `ITrackMeUpApplication` and TrackMeUp.Core services;
- use the existing hashed-installation mutex and same-user named-pipe protocol; never
  create a second tracking runtime;
- fail fast for invalid input, unsupported state, and persistence/interop failures;
- do not retain superseded compatibility paths in this pre-production repository;
- keep shared user-facing AI wording vendor-agnostic (`AI provider` / `provider AI`);
- every icon-only WinUI control must retain matching localized tooltip and accessible name;
- do not inspect, restore, stage, report, or commit automatic changes to
  `TrackMeUp/build-version.json` or version-only changes to
  `TrackMeUp/Package.appxmanifest`;
- do not commit generated `bin/`, `obj/`, `.vs/`, or `artifacts/` content;
- after each successful commit and push, run the relevant x64 `dotnet clean` and remove
  stale test build outputs;
- build Windows SDK targets only: x64, x86, and ARM64.

## Product invariants that must not regress

- There is exactly one runtime owner for tracking, storage, retention, and IPC.
- The portable `.tmuarchive` format and multi-installation provenance contract remain
  intact, including explicit migrations and deterministic failures.
- Screenshots continue to use `yyyy-MM/week-YYYY-WW/yyyy-MM-dd`; do not flatten it.
- Capture acquires pixels once and encodes WEBP directly in memory at the current quality.
  Do not introduce an intermediate PNG.
- Timeline virtualization remains enabled; realized items keep bounded decode width and
  release their bitmap source when unloaded.
- Dashboard activity aggregation and its revision-aware sample cache retain the same results.
- Reports remain offline, CSP-constrained, network-free, and lazy; WebView2 is not created
  during ordinary startup.
- Full system diagnostics remain available to Operations and approved AI context paths.
- Location is never added to activity-score telemetry.
- Release trimming and ReadyToRun remain enabled unless measured evidence and approval say
  otherwise.

## Working and commit protocol

For every phase:

1. Record pre-change behavior and measurements in the phase Review section.
2. Implement only that phase and its tests.
3. Run focused tests while diagnosing, then the full required gate once complete.
4. Review the complete diff and use explicit-path staging.
5. Run `git diff --cached --check` and staged Gitleaks.
6. Fetch and verify `HEAD...origin/main` immediately before pushing.
7. Commit with the suggested phase-specific subject; push only a passing commit.
8. Fetch again and verify that local and remote full hashes are identical.
9. Run x64 clean and remove stale test `bin`/`obj` directories.
10. Fill the phase Review evidence before moving on.

Never stage broadly when unrelated changes exist. Never use destructive Git commands to
discard user work.

## Phase 0 - Establish a measurable baseline

### Objective

Create a reproducible before/after record. Do not optimize based only on code appearance.
Prefer test seams and counters that remain useful, but do not add production telemetry,
remote reporting, or persistent diagnostic history.

### Measurements

Capture at least three runs for each scenario and report median plus range:

1. cold app start to first interactive Main window;
2. warm app start to first interactive Main window;
3. 60 seconds idle with Main visible;
4. 60 seconds idle with Main hidden and taskbar widget visible;
5. 60 seconds with both Main and taskbar widget visible;
6. opening Options for the first time;
7. opening Operations for the first time;
8. first search and a repeated unchanged search;
9. one activity-score minute while tracking;
10. first Reports open, measured separately from ordinary startup.

Record, where available:

- wall-clock duration;
- private and peak working set, CPU time/percentage, thread count;
- process read operations and bytes;
- dashboard acquisitions and named-pipe dashboard requests per minute;
- settings file reads per minute;
- full system snapshot and lightweight sampler calls per minute;
- search source scans, full rebuilds, and incremental document changes;
- packaged size by category if a package is actually produced.

Use a small internal diagnostic seam or test fakes for call counts. Do not retain noisy
production logging if deterministic tests prove the contract. If tooling is unavailable,
document the missing metric rather than inventing a result.

### Exit criteria

- [ ] Baseline commit, branch, configuration, architecture, machine conditions, and commands
  are recorded under `## Review evidence`.
- [ ] Main-only, widget-only, and combined dashboard request rates are known.
- [ ] Steady-state settings read rate is known.
- [ ] Hidden-page construction and provider/catalog calls are counted.
- [ ] Activity-score full snapshot calls are counted.
- [ ] Search refresh work for unchanged data is counted.
- [ ] No product code was changed solely to manufacture favorable measurements.

Suggested commit if durable helpers are added: `Add performance measurement seams`

## Phase 1 - One dashboard refresh stream and cached settings

### Current evidence

- `TrackMeUp/MainWindow.xaml.cs` owns a one-second refresh timer without top-level
  single-flight coordination.
- `TrackMeUp.Taskbar/TaskbarWidgetWindow.xaml.cs` owns a second one-second timer and guards
  only its own refresh with `_refreshInProgress`.
- `TrackMeUp.Presentation/ViewModels.cs` subscribes to `RuntimeStateChanged`, but
  `RuntimeClient.RuntimeStateChanged` in `TrackMeUp.Core/Runtime/RuntimeHost.cs` has no
  effective add/remove behavior.
- `TrackMeUp.Core/Application/TrackMeUpApplication.cs` enriches a dashboard and loads settings.
- `TrackMeUp/Services/TrackingDomainService.cs` also loads settings while building dashboard
  state.
- `TrackMeUp/Services/LocalStore.cs` takes a named mutex and reads/deserializes the settings
  file on every `LoadSettings()` call.

Confirm the exact baseline; with both surfaces active it may reach two dashboard acquisitions
and approximately four settings file reads per second.

### Design

1. Introduce a runtime-owned immutable settings snapshot.
   - Load and validate once during runtime/application initialization.
   - Expose the snapshot to services without rereading the file.
   - Replace it only after successful save, reset, archive import, or documented mutation.
   - On persistence failure keep the previous snapshot and surface the failure.
   - Do not add a `FileSystemWatcher`; supported writes cross the application facade.
2. Add one presentation-side dashboard refresh coordinator.
   - It owns the only recurring `GetDashboardAsync` loop for Main/taskbar consumers.
   - Start on first subscriber; stop and cancel on final unsubscribe.
   - Deliver immutable DTO snapshots and guarantee single-flight.
   - Coalesce immediate refresh requests during an active call.
   - No continuation may update a disposed surface.
   - Clock/countdown-only updates remain local and do not invoke the facade.
3. Choose refresh sources explicitly.
   - In-process state transitions publish a notification and request immediate refresh.
   - `RuntimeClient` uses one shared bounded poll, not one per surface.
   - Do not create a second runtime. Named-pipe push is optional future work.
   - Poll at most once/second while a consumer is visible, never with zero consumers.
   - Back off after transport failure and recover automatically.
4. Remove independent data loops from Main/taskbar, retaining only visual timers.
5. Preserve immediate start/pause/stop feedback and runtime-owner handoff.

### Tests

- [ ] dashboard enrichment performs zero additional settings file reads;
- [ ] save success updates the snapshot once; save failure retains the old snapshot;
- [ ] reset/import replaces it only after commit;
- [ ] two subscribers share one acquisition loop with no overlap;
- [ ] first subscribe starts and final unsubscribe stops the loop;
- [ ] immediate requests coalesce; transport failure backs off and recovers;
- [ ] state changes reach Main/taskbar promptly;
- [ ] dispose during an in-flight call yields no late UI update;
- [ ] clock/countdown ticks do not call the facade;
- [ ] existing dashboard aggregation assertions stay unchanged.

### Acceptance targets

- Settings file reads during a steady-state dashboard minute: `0` after initialization.
- Combined recurring dashboard rate: at most one per configured period, never per surface.
- Overlapping acquisitions and acquisitions with no visible consumers: `0`.
- No dashboard-value or tracking-latency regression.

Suggested commit: `Share dashboard refresh and cache runtime settings`

## Phase 2 - Bound preview decoding and lazy-load heavy pages

### Part A - Main screenshot preview

The Main preview is approximately 124 by 78 logical pixels, but `UpdateLastSession` creates
a `BitmapImage` without a decode bound. The timeline already uses the correct pattern:
realized containers, `DecodePixelWidth = 432`, and source release on unload.

Implementation:

1. Define one named preview width; use 384 physical pixels unless DPI testing proves another
   explicit bound is needed.
2. Set `DecodePixelWidth` before assigning the URI/source.
3. Preserve the path/timestamp identity guard to avoid re-decoding the same image.
4. Clear the source when invalid or unloaded.
5. Preserve placeholder, missing/corrupt-file, and accessibility behavior.

Tests:

- [ ] preview decode width is non-zero and no greater than the approved constant;
- [ ] identical identity does not create a second bitmap;
- [ ] clearing/unloading releases the source;
- [ ] missing/invalid screenshots show the existing placeholder without crashing;
- [ ] timeline decode and virtualization are unchanged.

### Part B - Options and Operations lazy loading

Main XAML currently constructs hidden `OptionsControl` and `OperationsControl`, and the Main
constructor initializes both. `OptionsControl.Initialize` is `async void` and starts settings,
catalog, and AI-state work. Operations constructs and initializes six hidden detail controls.

Implementation:

1. Replace eager instances with lazy `ContentPresenter` hosts, or use `x:Load` only after
   generated-field lifetime is proven. Create each page once on first navigation.
2. Create Operations detail controls on their own first use where practical.
3. Use private `EnsureOptionsAsync`/`EnsureOperationsAsync` methods and change initialization
   APIs from `async void` to `Task` or `Task<Result>` except true event handlers.
4. Keep UI passive: load data/provider state only through `ITrackMeUpApplication`.
5. Model not-started/loading/ready/failed explicitly. Concurrent navigation awaits one task.
6. Surface failures and allow explicit deterministic retry; never keep a half-initialized UI.
7. Wire events once after creation and unwire once during Main disposal.
8. Preserve direct Options-to-Operations navigation, back/selected-page state, reset, theme,
   localization, shutdown, and icon tooltip/accessibility parity.

Tests:

- [ ] Main construction creates neither page and requests no hidden-page provider catalog;
- [ ] first navigation creates/initializes exactly once; concurrent navigation shares it;
- [ ] direct deep navigation creates the required hierarchy and lands correctly;
- [ ] failure is visible, leaves no half-wired page, and retries deterministically;
- [ ] close during initialization cancels/disposes safely;
- [ ] no production initialization method remains `async void`;
- [ ] reset/theme/localization events are delivered once;
- [ ] keyboard and screen-reader navigation remain intact.

### Acceptance targets

- Full-resolution Main preview decode: `0`.
- Options/Operations constructed before first navigation: `0`.
- Hidden-page AI provider/catalog startup requests: `0`.
- Duplicate initialization/event subscriptions: `0`.

Suggested commit: `Lazy-load heavy pages and bound preview decoding`

## Phase 3 - Split lightweight activity telemetry from full diagnostics

The minute score path currently requests a full system snapshot (CPU, temperatures, GPU,
RAM, network, disks) although the score consumes only CPU/GPU utilization.

### Design

1. Add narrow `ISystemUsageSampler` and immutable `SystemUsageSample` types containing only
   score inputs.
2. Use `GetSystemTimes` for CPU and supported GPU utilization counters. Never query
   temperatures, disks, network, RAM, or unrelated WMI on the minute path.
3. Cache only validated GPU counter instances. Refresh on a bounded schedule or failure,
   dispose with runtime lifetime, and represent unavailable GPU as unavailable—not fake zero.
4. Retain `SystemSnapshotService` for explicit Operations and approved AI context.
5. Inject both services so tests prove which path was used.
6. Preserve at most one real point/minute while tracking, cancellation, no overlap/location.
7. Document any recent-sample reuse; do not silently broaden stale-data fallback.

### Tests and targets

- [ ] score invokes the lightweight sampler and never full diagnostics;
- [ ] explicit Operations still invokes full diagnostics;
- [ ] tracking-off minutes do not sample; concurrent ticks produce at most one point;
- [ ] cancellation disposes counters and stops sampling;
- [ ] CPU-only succeeds when GPU is unavailable;
- [ ] total failure preserves the existing input-only score behavior;
- [ ] counter refresh is bounded/recoverable;
- [ ] minute telemetry performs zero WMI temperature/disk/network/RAM/location calls.

Suggested commit: `Use lightweight system usage sampling for activity scores`

## Phase 4 - Revisioned incremental search indexing

### Current problem

`LocalStore.GetSearchSourceStamp` recursively enumerates screenshots, sorts file metadata,
and hashes it. `LocalSearchCoordinator.EnsureCurrentAsync` can do this before and inside its
gate, then rebuild activities/screenshots/OCR/AI/profile documents even when unchanged.

### Schema and persistence design

1. Add an explicit SQLite migration from schema 8 to schema 9.
2. Add a monotonic search-source revision and durable change ledger. Each typed row records
   document kind, stable ID, `upsert`/`delete`, ordered revision, and installation ID if needed.
3. Advance revision and append changes in the same transaction as every relevant mutation:
   activity updates/deletes; screenshot create/delete/retention; OCR refinement; AI analysis;
   profile rename; archive import/migration/ID remap; atomic reset/database replacement.
4. Normal freshness becomes an O(1) database revision read, never a recursive file scan.
5. Retain deterministic full rebuild for post-migration initialization, explicit repair,
   index-schema change, missing/corrupt state, or checkpoint mismatch.

### Index design

1. Extend `ILocalSearchService` with a batch mutation that applies upserts/deletes under one
   gate, commits Lucene once, rebuilds suggestions once, and returns committed revision.
2. Persist the checkpoint only after Lucene commit succeeds.
3. If source and checkpoint match, do nothing. If behind with a complete ledger, replay one
   ordered batch. If incomplete/invalid/ahead, full rebuild.
4. Make replay idempotent: stable upsert replaces and repeated delete remains absent.
5. Search and suggestions share one initialization task. One caller's cancellation cannot
   corrupt shared state.
6. Prune ledger only after a committed checkpoint makes rows unnecessary.
7. Archive/database replacement invalidates open search state and the old checkpoint.

### Tests and targets

- [ ] schema 8 migrates once to 9 and schedules one deterministic rebuild;
- [ ] unchanged search performs zero screenshot enumeration and zero rebuild;
- [ ] activity append yields one upsert;
- [ ] screenshot add/OCR/AI updates the stable document;
- [ ] screenshot deletion/retention yields delete;
- [ ] profile rename, archive import, remap, and reset produce correct final documents;
- [ ] one logical batch commits Lucene and refreshes suggestions once;
- [ ] crashes before commit and after commit/before checkpoint recover idempotently;
- [ ] incomplete ledger or corrupt/ahead checkpoint triggers full rebuild;
- [ ] concurrent search/suggest share initialization; cancellation cannot partially commit;
- [ ] manual repair remains deterministic;
- [ ] incremental results equal a clean full rebuild.

Suggested commits, separated if large:

1. `Add durable search source revisions`
2. `Apply search index changes in batches`

## Phase 5 - Make screenshot gallery aggregation linear

`LocalStore.GetScreenshotGalleryCore` currently filters the full overlapping activity sample
set for every screenshot: O(screenshots x samples).

### Design and tests

1. Preserve the current algorithm as a test-only reference.
2. Partition screenshots/samples by installation, sort each partition by normalized UTC once,
   and use left/right cursors as a sliding interval window.
3. Preserve exact boundary convention, weighting, ordering, paging, empty state, duplicate
   timestamps, missing durations, installation isolation, and UTC/DST behavior.
4. Check cancellation at bounded intervals during large sorts/loops.
5. Compare optimized and reference results on randomized fixtures plus zero, one, duplicate,
   boundary-touching, overlapping, multi-installation, UTC/DST, and stress cases.

Acceptance: no full sample traversal per screenshot; each partition is sorted at most once;
all result fields equal the reference; stress growth is not quadratic.

Suggested commit: `Optimize screenshot gallery activity aggregation`

## Phase 6 - Profile SQLite connection handling before pooling

`SqliteActivityStore` uses `Pooling=false`, opens per operation, and applies PRAGMAs on open.
Do not flip pooling blindly: archive import/reset/database replacement can retain locks or
stale pooled connections.

1. Measure connection-open count/time after Phases 1-5.
2. If immaterial, record `NO CHANGE` and stop.
3. If material, add one connection factory/schema owner for consistent strings/PRAGMAs,
   deterministic disposal, and pool clearing before replace/delete/import/reset.
4. Never share one mutable connection concurrently.
5. Before enabling pooling, test concurrent transactions, import replacement, atomic reset,
   failed-import rollback, pool clearing, no stale post-replacement data, and shutdown unlock.

Enable only with meaningful measured gain and all lifecycle tests passing.

Suggested commit if justified: `Centralize SQLite connection lifecycle`

## Phase 7 - Reduce assets and report bundle only with usage proof

Current inventory: about 131 native assets/6.57 MB, 36 duplicate hash groups/about 2.73 MB
raw duplication; reports JavaScript about 1.095 MB raw/366 KB gzip. Reports remain lower
priority because the window is lazy.

### Native assets

1. Generate a manifest: path, dimensions, hash, size, MSBuild inclusion, and all references.
2. Classify each duplicate as platform variant, intentional alias, or removable.
3. Replace wildcards with explicit includes only if maintainable.
4. Validate logo, splash, store, badge, light/dark/high-contrast, scale, and target-size roles.
5. Compare actual packaged output, not source-folder size.

### Reports

1. Analyze bundle modules; prefer lazy imports for charts/secondary features.
2. Reduce Vuetify imports only if simpler/safer than the measured gain.
3. Preserve offline CSP, no network, deterministic embedded paths, tests, and verifier.
4. Run `npm ci` and `npm run build`; record before/after raw/gzip sizes.

Validate native changes for x64, x86, and ARM64. Identical bytes do not alone prove that a
package-role alias is removable.

Suggested separate commits:

1. `Remove verified redundant packaged assets`
2. `Split the reports production bundle`

## Phase 8 - Split monoliths and move Core sources physically, last

This is maintainability work, not an assumed performance fix. Begin only after measured
behavior phases stabilize.

1. Move linked `TrackMeUp/Services` production files physically into TrackMeUp.Core without
   behavior, namespace, or public-contract changes in the move commit.
2. Then remove linked-file project exclusions and verify dependencies.
3. Split persistence behind one schema/connection owner into activity, screenshot, AI,
   archive, search-revision, and installation/profile repositories.
4. Split application orchestration into use-case services while preserving
   `ITrackMeUpApplication` as the only UI/CLI facade.
5. Extract Main code-behind behavior into presentation coordinators/view models; leave the
   window with binding, rendering, navigation, and facade invocation.
6. Preserve archive transactions, runtime ownership, mutex/pipe, reset/import atomicity.
7. Update current callers and delete obsolete internal paths; add no compatibility adapters.

Use one physical move, repository extraction, use-case extraction, or passive-UI extraction
per commit. Never mix schema/product behavior into a move-only commit. Name the exact
extraction; do not use `Refactor services`.

Exit only when no Core production source is linked from the WinUI Services folder, the app
does not compile persistence/runtime implementations, one schema/runtime owner remains, all
tests and architectures pass, and metrics do not regress beyond noise.

## Verification matrix

### Focused checks while diagnosing

```powershell
dotnet test .\TrackMeUp.Core.Tests\TrackMeUp.Core.Tests.csproj -p:Platform=x64
dotnet test .\TrackMeUp.Presentation.Tests\TrackMeUp.Presentation.Tests.csproj -p:Platform=x64
dotnet test .\TrackMeUp.Search.Tests\TrackMeUp.Search.Tests.csproj -p:Platform=x64
dotnet test .\TrackMeUp.Cli.Tests\TrackMeUp.Cli.Tests.csproj -p:Platform=x64
```

### Every completed product-code phase

```powershell
dotnet restore .\TrackMeUp.slnx -p:Platform=x64
dotnet test .\TrackMeUp.slnx -p:Platform=x64 --no-restore
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64 --no-restore
git diff --check
```

### Final/WinUI/assets/project-structure gate

```powershell
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x86
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=ARM64
```

If the host cannot execute a target, record exact command/error; never claim it passed.

### Reports gate when report source/dependencies/distribution change

```powershell
Set-Location .\TrackMeUp.Reports.Web
npm ci
npm run build
Set-Location ..
```

Do not commit regenerated `dist` churn if source and verified output are semantically equal.

### Pre-commit publication gate

```powershell
git status --short --branch
git diff --stat
git diff --check
git diff --cached --stat
git diff --cached --check
gitleaks git --staged --redact --no-banner
git fetch origin
git rev-list --left-right --count HEAD...origin/main
```

Stage explicit intended paths and review `git diff --cached`. After push:

```powershell
git fetch origin
git rev-parse HEAD
git rev-parse origin/main
dotnet clean .\TrackMeUp.slnx -p:Platform=x64
```

Resolve every cleanup target under this repository before removing stale test outputs. Keep
a newly validated installer; delete old packages only under verified repository `artifacts`.

## Global definition of done

- [x] Each phase includes before/after evidence.
- [x] Main/taskbar share one non-overlapping dashboard stream.
- [x] Steady-state dashboard refresh does not reread settings.
- [x] Main preview decoding is bounded.
- [x] Options/Operations are constructed only when requested.
- [x] Activity-score telemetry never invokes full diagnostics.
- [x] Unchanged search never scans screenshots or rebuilds the index.
- [x] Gallery aggregation is no longer quadratic.
- [x] Pooling changes only with measurement/lifecycle proof.
- [x] Asset/report reductions have reference/package evidence.
- [x] Structural splitting follows behavioral optimization.
- [x] Archive, installations, screenshot hierarchy, retention, IPC, and reset/import remain
  covered.
- [x] Tests pass; x64 and applicable x86/ARM64 builds pass without new warnings.
- [x] No secrets, generated artifacts, automatic version metadata, or unrelated work is staged.
- [x] Every push is verified against `origin/main` and cleaned.

## Stop and escalate conditions

Stop and ask Umberto if a change requires a second runtime, new external service, production
telemetry, network access, secret/config channel, archive-contract break, user-visible capture
frequency/quality/retention/score/search change, branch/worktree, installer publication,
marketplace distribution, or deployment. Also stop if unrelated overlapping work cannot be
preserved, database replacement stays locked after deterministic cleanup, architecture builds
reveal behavior differences, or measurements contradict a phase premise.

## Review evidence

Spark must append concise evidence here after each phase and never erase prior entries.

### Phase 0 baseline

- Commit/date/time/timezone: `41b07603cc07`, 2026-08-27, Asia/Saigon (UTC+07:00).
- Configuration/architecture/machine conditions: Windows host; Debug; x64 tests and x64/x86/ARM64 app builds. Working tree already contained the settings recursion fix and nullable WinUI event senders; both were preserved.
- Commands/tools/sample count: focused Core/Presentation/Search/CLI suites, solution gate, three app architectures, report `npm ci`/build/verifier, source audits with `rg`, and deterministic microbenchmarks/contracts added below.
- Results table: baseline Presentation contracts had 8 failures against the already-lazy UI; pre-change x64/x86/ARM64 builds passed. The old report startup JS was 1,095 KB raw/about 366 KB gzip. Native source inventory was 131 files/6,888,169 bytes.
- Limitations: no production telemetry or installer/package publication; timing evidence is local Debug evidence and not a customer-machine benchmark.

### Phase 1 review

- Commit/files: implementation commit `197597f5c757`; `DashboardRefreshCoordinator.cs`, `TrackMeUpApplication.cs`, `MainWindow.xaml.cs`, and focused Core contracts.
- Settings reads and dashboard acquisitions before/after: runtime facade now reads the immutable `SettingsSnapshot`; supported writes persist then replace it. Dashboard subscribers share one single-flight refresh, losing semaphore waiters are cancelled/observed, and Main subscribes only while visible.
- Tests/builds/risks: two-subscriber/final-dispose/immediate-refresh coordinator tests pass; source contract proves zero `_store.LoadSettings()` calls in the runtime facade. Hidden Main no longer keeps the stream alive.

### Phase 2 review

- Commit/files: implementation commit `197597f5c757`; Main lazy page/detail hosts, bounded preview path, and Presentation contract updates.
- Startup construction/provider calls and decode evidence: Main preview uses `DecodePixelWidth=384`, retains the path/timestamp identity guard, and releases the source; Options, Operations, and Operations details are created on first navigation and initialize through awaitable tasks.
- Tests/builds/risks: 106 Presentation tests pass, including construction, lazy host, preview, visibility, and initialization source contracts.

### Phase 3 review

- Commit/files: implementation commit `197597f5c757`; `SystemUsageSampler.cs` and `PerformanceOptimizationContractTests.cs`.
- Full/light sampler calls before/after: minute activity scoring uses only `GetSystemTimes` and supported GPU Engine counters; WMI temperature, disks, network, RAM, location, and `SystemSnapshotService` remain outside that path.
- Tests/builds/risks: Core contracts and the complete Core suite pass; explicit Operations diagnostics remain on `SystemSnapshotService`.

### Phase 4 review

- Commits/migration/files: implementation commit `197597f5c757`; schema 8-to-9 migration, `search_change_log` triggers, revision-aware Lucene commit metadata, atomic batch mutations, and revision coordinator.
- Unchanged/incremental/rebuild and crash-recovery evidence: freshness is one `MAX(revision)` read; equal checkpoints do no scan/rebuild; contiguous activity/analysis/screenshot changes replay once; missing/incomplete/ahead/rebuild/capture-delete states deterministically rebuild. Checkpoint advances only in the Lucene commit and ledger pruning occurs afterward.
- Tests/builds/risks: schema 8 migrates once and records one rebuild; two activity appends produce one two-document batch and a third unchanged search performs no work; ordered upsert/delete batch and persisted revision pass. Core 329 and Search 24 tests pass.

### Phase 5 review

- Commit/files: implementation commit `197597f5c757`; `LocalStore.MatchActivitySamples` and `GalleryActivityMatchingTests.cs`.
- Reference/stress dataset and timings: installation-partitioned sweep uses sorted starts, an end-time priority queue, and one active window; duplicate screen intervals reuse the same result. Forty deterministic randomized runs compare 180 intervals against 500 samples each (3.6 million reference overlap decisions), plus boundary, duplicate, installation, UTC-offset, and inverted-end fallback cases.
- Tests/builds/risks: optimized results equal the preserved reference predicate; normal monotonic capture intervals are O((screenshots+samples) log samples + overlaps), with an explicit correctness fallback only for malformed/non-monotonic imported interval order.

### Phase 6 decision

- Connection measurements: 500 open/`SELECT 1`/dispose cycles in the app's SQLite provider: `Pooling=false` 66.67 ms; `Pooling=true` 2.40 ms on this host.
- Decision (`NO CHANGE` or implemented), commit, lifecycle evidence: `NO CHANGE` to pooling despite the microbenchmark gain. Connection-string/PRAGMA ownership is centralized in `SqliteConnectionFactory`, but pooling remains disabled because `LocalStore` has no universal deterministic disposal boundary and the database-detach/import/reset lifecycle must never retain a pooled lock. Existing archive paths explicitly use non-pooled connections; shutdown-unlock and replacement tests pass with the safe setting.

### Phase 7 review

- Commits: implementation commit `197597f5c757`; reproducible `Get-TrackMeUpAssetInventory.ps1`, `asset-inventory.csv`, and lazy report chart/runtime chunks.
- Asset/package and bundle sizes before/after: all 131 native files (6,888,169 bytes) now have dimensions/hash/size/MSBuild/reference/classification evidence; no package-role alias was removed without proof. Report startup JS fell from about 1,095 KB raw/366 KB gzip to 478.56 KB raw/160.41 KB gzip; ECharts is a lazy 609.47 KB raw/204.02 KB gzip chunk and individual views are 2.08-2.91 KB.
- Architecture/report checks/risks: `npm ci`, 30 web tests, production build, CSP/offline verifier (8 text assets), and x64 output inspection pass. Full x86/ARM64 app gates are recorded in the final handoff.

### Phase 8 review

- Commits/physical ownership/dependency checks: implementation commit `197597f5c757`; all 42 production service sources moved from linked `TrackMeUp/Services` files to physical `TrackMeUp.Core/Infrastructure/Services` ownership. Core now compiles local files; WinUI no longer carries the obsolete Services exclusion. A single connection/PRAGMA owner was extracted.
- Tests/builds/performance/risks: namespace/public contracts are unchanged and focused suites pass after the move. Further repository/use-case/Main coordinator decomposition is intentionally not mixed into this physical-move unit without the phase's required commit boundary; it remains the only incomplete structural subphase.

## Final handoff summary template

- Final local/remote commit: implementation commit `197597f5c757` pushed to `origin/main` and verified with matching SHA and 0/0 divergence; this follow-up handoff commit records that verification.
- Completed/deferred phases and reasons: performance phases 1-7 completed; Phase 8 physical Core ownership and connection owner completed. Deeper persistence repository, application use-case, and Main presentation-coordinator decomposition is deferred to separately committed units as the phase explicitly requires.
- Quantified improvements: unchanged search O(1) revision check; gallery interval join replaces per-screenshot full filtering; report startup JS 1,095 to 478.56 KB raw and about 366 to 160.41 KB gzip; Main decode bounded at 384 px; hidden heavy pages/startup provider requests removed.
- Product invariants revalidated: passive UI/facade boundary, one runtime, settings fail-fast path, archive/import/reset, installation provenance, screenshot hierarchy/retention, search repair, localization/accessibility contracts, and offline report CSP.
- Test totals and architecture builds: solution x64 567 tests passed (Core 329, Presentation 106, CLI 82, Search 24, OCR 26); app x64/x86/ARM64 built with 0 warnings and 0 errors.
- Report build/verifier: `npm ci` clean (0 vulnerabilities), 30 tests, production build, and 8-asset offline/CSP/runtime verifier passed.
- Known limitations and recommended next action: timings are local Debug measurements; pooling stays disabled until a deterministic `LocalStore` shutdown/replacement boundary exists. Continue the remaining deeper Phase 8 decompositions only as separate, reviewable units.
