# TrackMeUp Privacy Policy

**Last updated: September 2, 2026**

This policy describes the data behavior of the current TrackMeUp application
and source repository. TrackMeUp is a local-first Windows app designed to make
workdays easier to understand and remember. It creates local context that can
help recover an activity later; it does not promise to recover information,
such as a URL, that was never visible or recorded.

Material changes to these data flows should be reflected here before a public
release. Privacy questions can be sent to **hello@umbertogiacobbi.biz**.

## The short version

- Activity history is stored locally on the Windows PC by default. It can leave
  the PC only through an explicit export or sharing action, or an integration
  the user or operator enables as described below.
- Typed text is never recorded. TrackMeUp records counts, not the content of keys or clicks.
- Screenshots are off by default and can be disabled completely.
- AI is off by default and can be disabled completely.
- When AI analysis is enabled, scheduled snapshots are analyzed immediately after capture. A manual player snapshot waits through its 30-second deletion window; deleting it prevents the AI request, while an undeleted capture is analyzed once the window expires.
- The selected AI provider credential is read from the Windows environment on this PC and is not copied into TrackMeUp settings or history.
- TrackMeUp does not require a cloud account, and the current implementation does not send data to a TrackMeUp-operated cloud service. Its direct network and sharing flows are listed below.
- World-clock time, sun, moon, and lunar-phase data are calculated locally from a bundled city catalog. Current weather is enabled by default, but a missing environment-only `TRACKMEUP_OPENWEATHER_API_KEY` produces a local setup-required state and no network request. When configured, only the selected cities' coordinates are sent for the live current projection, never for a converted reference instant.
- Purely informational and confirmation prompts use owned native Windows message boxes. Windows supplies their localized standard buttons; the prompts do not create a TrackMeUp dialog-history record or add any network flow.
- Sentry is optional. It sends diagnostics only when an operator explicitly configures a Sentry DSN.
- The project-authored source code is open source under the MIT License, and the direct dependency list is public and inspectable.

## What is collected locally

| Data | Default | Where it goes | User control |
| --- | --- | --- | --- |
| Active/idle periods | Tracking is user-controlled | Local SQLite history | Pause or stop tracking; retention settings |
| Application and window context | Captured for the active context | Local SQLite history | Disable individual application detail providers; add privacy rules |
| Key-press and mouse-click counts | Collected while tracking | Local SQLite history | Pause or stop tracking |
| Typed text, key values, and click targets | Never collected | Nowhere | Not applicable |
| Screenshots | Off | Nowhere unless explicitly captured | `screenshots.enabled`, one-off capture controls |
| AI analysis result | Only after AI is enabled and requested | Local SQLite history | Disable AI; delete through retention controls |
| AI request usage | Created for local cost and troubleshooting | Local SQLite history | Retention controls; it excludes prompts, images, headers, and keys |
| Device measurements | Used for local reports and optional AI context | Local records and, only when AI is enabled, the selected provider request | Disable AI; location is a separate opt-in |
| Windows location | Off | Only the selected AI request when enabled | Windows permission plus TrackMeUp setting |
| Selected world-clock city IDs | Four initial cities | Local settings JSON only | Open the in-window options surface to add or remove clocks; maximum four |
| Current world-clock weather | On; no request until `TRACKMEUP_OPENWEATHER_API_KEY` is available | The on/off preference is stored in local settings; the key remains in the fixed Windows user/process environment variable; valid OpenWeather observations are revalidated after 12 minutes and retained only in process memory for at most 45 minutes | Turn weather off in the in-window options surface, or use a converted reference instant; remove the Windows user variable and restart to clear the current process copy |
| Diagnostic logs | Local logging is enabled for troubleshooting | `%LOCALAPPDATA%\TrackMeUp\logs` | Use the local log directory setting; delete local logs normally |
| Portable data archive | Created only on explicit export | The `.tmuarchive` path selected by the user | Preview the destination and keep or delete the file normally |

Window titles and document names can be sensitive. Privacy rules block by process name, window-title text, or context hint before activity is persisted: excluded observations leave no activity sample or input aggregates. Disabling a detail provider retains process identity and counts, but not its title, context, or attributes. Privacy evaluation still uses transient raw metadata so disabling details cannot bypass exclusions.

Screenshot checks cover the foreground and every visible, non-minimized, non-cloaked top-level window intersecting the capture rectangle, including other monitors. An excluded window rejects the entire capture; required metadata or window-enumeration failures stop acquisition. Checks run around pixel acquisition, before encoding, and again before an AI request. Occluded windows may conservatively block a capture. Desktop composition is not atomic with these checks: rapidly changing windows remain an OS-level race, not a guarantee of redaction. Disable screenshots when that limitation is unacceptable. Pause cancels pending live AI work; disabling AI prevents new requests and cancels pending live analysis without waiting for a slow provider. Cancellation cannot recall data already transmitted.

The three-dot **World clocks** options action opens a panel over the clock canvas rather than a second settings window. Its Desktop Acrylic, light/dark theme resources, transparency fallback, and composited city imagery are presentation-only and do not change the data or network boundaries above.

## What can leave the PC

The current implementation does not send activity history to a
TrackMeUp-operated server.

Data can be transferred outside TrackMeUp, and potentially outside the PC,
only through an explicit user action or an enabled integration:

1. **Portable archive export.** From Operations, the user can create a private `.tmuarchive` containing the selected local history and, when included, retained screenshots. It contains installation provenance (machine name, friendly name, color, and icon) so records remain attributable after merge. It excludes settings, API keys, cached provider pricing, reprocessing jobs, diagnostics, and derived search indexes. Creating the archive writes it to the path the user selects; TrackMeUp does not upload it. The user can then keep, copy, or transfer that file. Import validates and previews the archive before a separately confirmed idempotent merge.
2. **AI provider request.** When AI is enabled and an analysis is requested, TrackMeUp sends the selected local context, system context, and screenshots allowed by the settings directly to the selected provider. The default provider is OpenAI at `https://api.openai.com/v1/responses`. OpenRouter and Anthropic are explicit alternatives.
3. **Screenshot sharing.** When the user chooses to share a retained screenshot, TrackMeUp opens the Windows Share UI with that file. The user chooses the receiving app or destination; TrackMeUp does not select or upload to a recipient automatically.
4. **Redacted log sharing.** When the user chooses **Report a problem**, TrackMeUp creates a bounded copy of the current application log, removes known private paths and secrets, and opens the Windows Share UI. Redaction reduces exposure but cannot guarantee that future diagnostic text contains no sensitive context, so the user should review what they share.
5. **Optional current weather.** The current-weather preference is on for clean settings. A missing `TRACKMEUP_OPENWEATHER_API_KEY` sends nothing and exposes a non-secret localized setup-required state. Once a key is available, only the live current world-clock projection sends the latitude and longitude of each of the one to four selected cities directly to OpenWeather's Current Weather endpoint. Responses contribute temperature, condition, and observation time only while valid, are revalidated after 12 minutes, expire after at most 45 minutes, remain in process memory only, and are not written to settings, SQLite, reports, diagnostics, or IPC history. The window keeps linked OpenWeather attribution visible whenever provider weather is shown. A stale response or provider failure leaves every local clock working and may continue showing the last still-valid observation during that bounded window. Historical and future reference-instant conversions never issue a current-weather request.
6. **Optional Sentry diagnostics.** If `TRACKMEUP_SENTRY_DSN` is set, Sentry receives configured error events and breadcrumbs. It is not active by default.
7. **Provider pricing download.** Only while AI is enabled and OpenAI is selected, the runtime may download its public pricing table from `https://developers.openai.com/api/docs/pricing.md` when the local daily cache is stale. The request contains no API key, activity, OCR, or screenshot, but exposes ordinary connection metadata such as IP address to the server. AI-disabled and other-provider configurations do not start this request.

After data is sent to an AI provider, OpenWeather, Sentry, or an app selected through the
Windows Share UI, that recipient's privacy and retention terms apply.
TrackMeUp cannot remove copies held by those recipients.

Reports are generated from local data. The bundled reports interface does not start a local HTTP server and does not contact a TrackMeUp service.

## API keys

For OpenAI, TrackMeUp reads `OPENAI_API_KEY` from the Windows process, user, or machine environment. The app's key prompt writes the value to the Windows user and current process environment so it can be used now and after the next launch. The Windows user environment is persistent OS-managed storage outside TrackMeUp settings and history.

TrackMeUp does not write the key to settings, SQLite, reports, logs, command-line arguments, command history, IPC diagnostics, or tests. It is used in the HTTPS authorization header for the direct provider request. The key is not routed through a TrackMeUp server; the selected provider receives it as authentication for the requested analysis.

The same rule applies to `OPENROUTER_API_KEY` and `ANTHROPIC_API_KEY` when those providers are selected.

Optional current weather uses `TRACKMEUP_OPENWEATHER_API_KEY`, read from the Windows process, user, or machine environment. The **World clocks** options panel can set a plausible value without a restart: it passes the secret once to the local same-user runtime, which writes only the fixed Windows user and current-process environment scopes. TrackMeUp does not copy the value into settings, SQLite, logs, command-line arguments, command history, tests, or IPC diagnostics; the process-scope copy makes new requests use the key immediately and the user-scope copy makes it available to later launches. The request goes directly to `https://api.openweathermap.org/data/2.5/weather`. OpenWeather requires the key in the HTTPS query, so TrackMeUp constructs that URI only for the direct request and never logs, persists elsewhere, returns through IPC diagnostics, or places the URI or key in an exception message. Turning weather off stops new requests immediately. Removing the Windows user variable and restarting clears the current process copy; all local clock and astronomy behavior remains available. The person supplying the key remains responsible for the data, attribution, redistribution, and usage terms of their selected OpenWeather plan.

## Dependency census

The following are the direct product and build packages relevant to data behavior at the time of this review. Test-only packages are recorded in [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md). Transitive packages are resolved by the normal NuGet/npm lock and restore process; they are not treated as invisible product behavior.

### Windows app and shared services

| Package | Version | Role and network behavior |
| --- | ---: | --- |
| `Serilog` | 4.4.0 | Local logging pipeline. No network destination by itself. |
| `Serilog.Extensions.Logging` | 10.0.0 | Connects Serilog to the .NET logging abstraction. |
| `Serilog.Sinks.Console` | 6.1.1 | Writes diagnostics to the local console when available. |
| `Serilog.Sinks.File` | 7.0.0 | Writes rolling diagnostics under the local app-data directory. |
| `Sentry.Extensions.Logging` | 6.9.0 | Optional remote diagnostics. Active only with `TRACKMEUP_SENTRY_DSN`; default PII is disabled and identity fields are cleared before sending. |
| `Microsoft.Data.Sqlite` | 10.0.10 | Local SQLite persistence for activity, analyses, and sanitized AI usage. |
| `SQLitePCLRaw.lib.e_sqlite3` | 2.1.12 | SQLite native engine used by the local store. |
| `SkiaSharp` | 4.151.0 | Local image conversion and rendering. |
| `SkiaSharp.NativeAssets.Win32` | 4.151.0 | Windows native assets for SkiaSharp. |
| `System.Drawing.Common` | 10.0.10 | Local screen-pixel acquisition before WebP encoding. |
| `System.Management` | 10.0.10 | Reads local Windows/system information. |
| `System.Diagnostics.PerformanceCounter` | 10.0.10 | Reads local performance counters. |
| `Microsoft.WindowsAppSDK` | 2.3.1 | Windows desktop UI and platform integration. |
| `Microsoft.Windows.SDK.BuildTools` | 10.0.28000.2526 | Windows build-time APIs and metadata. |
| `Microsoft.Extensions.DependencyInjection` / logging packages | 10.0.10 | Application wiring and logging abstractions; no product analytics. |
| `Lucene.Net`, `Lucene.Net.Analysis.Common` | 4.8.0-beta00018 | Local full-text indexing and analysis; no network service. |

### CLI and reports

| Package | Version | Role and network behavior |
| --- | ---: | --- |
| `Spectre.Console` | 0.57.2 | Local terminal presentation. |
| `vue` | 3.5.40 | Bundled reports UI. |
| `vuetify` | 4.1.7 | Bundled reports components and styling. |
| `echarts` | 6.1.0 | Bundled local charts. |
| `vue-echarts` | 8.0.1 | Vue integration for local charts. |
| `@mdi/js` | 7.4.47 | Bundled SVG icon paths. |
| `vite`, `@vitejs/plugin-vue`, `vite-plugin-vuetify`, `typescript`, `vue-tsc` | Pinned in `package.json` | Build and type-check tooling; not runtime services. |

### AI providers

TrackMeUp does not hide an AI SDK behind the product. The adapters use .NET `HttpClient` and the provider endpoints are visible in `TrackMeUp.Core/Application/SettingsCatalog.cs` and the provider decoder files:

- OpenAI Responses API: `https://api.openai.com/v1/responses`
- OpenRouter chat completions: `https://openrouter.ai/api/v1/chat/completions`
- Anthropic Messages API: `https://api.anthropic.com/v1/messages`

Changing provider, endpoint, model, thinking effort, screenshot retention, or whether AI analysis is enabled is an explicit setting. When AI analysis is enabled, every permitted scheduled snapshot is analyzed as part of that capture, while a manual player snapshot is analyzed only after its deletion window expires. API keys are never accepted as command-line arguments.

## Sentry and Serilog, without vague wording

Serilog is the local logging library. Its console and file sinks do not send anything over the network. Local logs are retained as rolling daily files, with a maximum of 15 files and 15 days in the current implementation.

Sentry is different: it is a possible remote destination, but it is not enabled by default. An operator must provide `TRACKMEUP_SENTRY_DSN`. When enabled, the application configures Sentry to:

- send Information-level breadcrumbs and Error/Critical events;
- disable default PII;
- clear user, request, and server identity fields before send;
- redact paths, secrets, tokens, authorization text, DSNs, and raw installation identifiers from diagnostic text;
- stop trying after a bounded two-second shutdown/flush window.

For a local or managed Windows installation, set the DSN and environment at user scope, then restart TrackMeUp so the new process inherits them:

```powershell
[Environment]::SetEnvironmentVariable('TRACKMEUP_SENTRY_DSN', 'https://PUBLIC_KEY@HOST/PROJECT_ID', [EnvironmentVariableTarget]::User)
[Environment]::SetEnvironmentVariable('TRACKMEUP_SENTRY_ENVIRONMENT', 'production', [EnvironmentVariableTarget]::User)
```

Use a Sentry project DSN here, never a Sentry authentication token. The DSN is a public client routing identifier rather than an account credential, so any value configured automatically for every installed client is necessarily discoverable in that installation. TrackMeUp keeps it out of source and persisted application settings; an installer or device-management policy may set the same user-scope variables when centralized deployment is required. Invalid DSN or environment values keep the remote sink disabled and surface an `invalid` observability status rather than silently relabeling events. Setting `TRACKMEUP_SENTRY_DSN` to `null` at user scope and restarting disables the remote sink again.

The source is the final authority. An error message added in the future must still be reviewed for sensitive content before it is logged or sent to an optional remote destination.

## Retention and deletion

TrackMeUp exposes a read-only retention preview before deletion. A confirmed retention run removes only TrackMeUp-owned records and screenshot artifacts that match its ownership rules. It does not recursively delete arbitrary files from a selected folder.

The default local retention period is 30 days for activity data and 30 days for retained screenshots. Settings can change those periods, including setting them to zero. Temporary screenshots are cleaned after analysis when retention is disabled, or when a manual capture is deleted during its player deletion window.

Local OCR expires under data retention using its extraction timestamp, even after its image has been removed; screenshot interval telemetry expires using capture time. The preview includes these database records. Screenshot deletion records a local pending-deletion intent before removing files. Failures remain retryable, and pending operations are completed on the next runtime startup; paths must still validate under the configured screenshot root. A completed deletion/retention operation has also committed and published changes to the searchable index. These are logical deletions, not promises of forensic erasure from SQLite pages, Lucene segments, backups, or storage media.

Search uses one local source index (`lucene-v4`), updated once per second. Queries use the last completed snapshot; deletion and retention publish changes before success. Indexing failures require a manual rebuild. Same-user IPC is limited to four requests and does not grant remote or cross-user access.

The separately confirmed atomic reset removes the current installation's
validated TrackMeUp application-data directory and TrackMeUp-owned screenshot
artifacts, then relaunches the app. Retention and atomic reset do not remove
exported archives, files already handed to another app through Windows Share,
AI provider or Sentry copies, API keys stored in Windows environment variables,
or Windows package and certificate state.

## How to audit this yourself

Start from these files:

- `TrackMeUp.Core/Infrastructure/Services/OpenAiAnalysisService.cs` — AI and screenshot gates, cleanup, and local result persistence.
- `TrackMeUp.Core/Infrastructure/Services/LocalStore.cs` — environment-variable key lookup and local storage access.
- `TrackMeUp.Core/Infrastructure/Services/WorldClockWeatherService.cs` — optional coordinate-only Current Weather requests, freshness checks, cache, and non-secret diagnostics.
- `TrackMeUp/Runtime/LoggingBootstrapper.cs` — Serilog and optional Sentry configuration.
- `TrackMeUp.Core/Application/ObservabilityConfiguration.cs` — optional Sentry environment configuration.
- `TrackMeUp.Core/Application/SettingsCatalog.cs` — provider endpoints and user-facing settings.
- `TrackMeUp/TrackMeUp.csproj`, `TrackMeUp.Core/TrackMeUp.Core.csproj`, `TrackMeUp.Cli/TrackMeUp.Cli.csproj`, and `TrackMeUp.Reports.Web/package.json` — direct dependency inventory.

The project-authored repository source is open source under the
[MIT License](../LICENSE), so these claims can be checked against the code.
TrackMeUp marks and brand assets are governed separately by
[`TRADEMARKS.md`](../TRADEMARKS.md), and third-party components retain the
terms recorded in [`THIRD_PARTY_NOTICES.md`](../THIRD_PARTY_NOTICES.md) or in
asset-specific notices.
