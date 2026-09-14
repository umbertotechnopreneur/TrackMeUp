# TrackMeUp Privacy Policy

**Last updated: September 14, 2026**

TrackMeUp helps you look back at your workday by saving activity on your Windows
PC. This page explains what it saves, what can leave your PC, and how to delete
it. The app can help you find something you've seen, but it can't recover
information it never saw or recorded, such as a hidden URL.

If we change how the app handles data, we'll update this page before a public
release. For privacy questions, email **hello@umbertogiacobbi.biz**.

## The short version

- Your activity history stays on this PC by default. It can leave when you
  export or share it, or when you or the person managing the installation turns
  on one of the integrations described below.
- TrackMeUp counts key presses and clicks. It doesn't record which keys you press or what you type. Optional screenshots can include text visible on screen.
- Screenshots and AI are both off by default. You can leave either off entirely.
- With AI enabled, scheduled screenshots are analyzed right after capture. A screenshot taken from the player waits 30 seconds, giving you time to delete it before anything is sent. If you keep it, analysis starts after that window closes.
- Your AI provider key is read from Windows environment variables on this PC. It isn't copied into TrackMeUp settings or history.
- You don't need a TrackMeUp cloud account. The app doesn't send your data to a TrackMeUp-operated cloud service. Requests to other services and sharing options are listed below.
- World clocks calculate time, sun, moon, and moon phases on your PC using the included city list. Weather is on by default, but sends nothing until `TRACKMEUP_OPENWEATHER_API_KEY` is set in the Windows environment. With a key, requests include only the selected cities' coordinates and only show current weather. Looking at a past or future time doesn't request weather.
- Information and confirmation messages use native Windows message boxes with buttons in the Windows language. TrackMeUp doesn't save a history of those dialogs or make network requests for them.
- Sentry diagnostics are optional. They are sent only if the person managing the installation sets a Sentry DSN, which tells the app which Sentry project to use.
- You can read the app's source code under the MIT License and inspect the list of packages it uses.

## What is collected locally

| Data | Default | Where it goes | What you can do |
| --- | --- | --- | --- |
| Active/idle periods | You control tracking | Local SQLite history | Pause or stop tracking; choose how long to keep data |
| Application and window context | Collected for the active app and window | Local SQLite history | Turn off individual app detail providers; add privacy rules |
| Key-press and mouse-click counts | Collected while tracking | Local SQLite history | Pause or stop tracking |
| Typed text, key values, and click targets | Never collected | Nowhere | Not applicable |
| Screenshots | Off | Nowhere unless explicitly captured | `screenshots.enabled`, one-off capture controls |
| AI analysis result | Only after AI is enabled and requested | Local SQLite history | Turn AI off; delete results through retention controls |
| AI request usage | Saved to track costs and troubleshoot | Local SQLite history | Choose how long to keep it; usage records don't include prompts, images, headers, or keys |
| Device measurements | Used for local reports and optional AI context | Local records and, only when AI is enabled, the selected provider request | Turn AI off; sharing your location needs a separate opt-in |
| Windows location | Off | Only the selected AI request when enabled | Windows permission plus TrackMeUp setting |
| Selected world-clock city IDs | Four initial cities | Local settings JSON only | Add or remove cities in World clocks options; maximum four |
| Current world-clock weather | On; no request until `TRACKMEUP_OPENWEATHER_API_KEY` is available | The on/off setting stays local; the key stays in the Windows environment. Weather stays in memory, is checked again after 12 minutes, and expires after at most 45 minutes | Turn weather off or view a past or future time. To clear the key, remove the Windows user variable and restart |
| Diagnostic logs | Local logging is on for troubleshooting | `%LOCALAPPDATA%\TrackMeUp\logs` | Change the log folder in settings; delete local logs normally |
| Portable data archive | Created only when you export | The `.tmuarchive` path you choose | Preview where it will be saved, then keep or delete the file normally |

Window titles and document names can contain private information. Privacy rules
can exclude activity by process name, window-title text, or a context hint before
anything is saved. Excluded activity leaves no activity record or key-press and
click counts. If you turn off an app detail provider, TrackMeUp still saves the
process identity and counts, but leaves out the title, context, and other details.
It still checks those details in memory against your privacy rules, so turning
off a detail provider doesn't bypass an exclusion.

### How screenshots are checked

TrackMeUp checks the foreground window and other visible top-level windows that
overlap the capture area, including on other monitors. Minimized windows and
windows hidden by Windows ("cloaked") are left out. If any checked window matches
an exclusion, the whole screenshot is blocked. Capture also stops if the app
can't list the windows or read information needed for the checks.

These checks run around the moment pixels are captured, before the image is
encoded, and again before an AI request. A window covered by another window can
still block a capture. Windows can also change between checks, so this isn't a
guarantee that every excluded window will stay out of every image. If you need
that guarantee, keep screenshots off.

Pausing cancels pending live AI work. Turning AI off also prevents new requests
and cancels pending live analysis without waiting for a slow provider. It can't
take back data that's already been sent.

The three-dot **World clocks** button opens options in the same window. Themes,
transparency, and city artwork only affect how it looks; they don't change what
is saved or sent.

## What can leave the PC

TrackMeUp doesn't send activity history to its own server.

Here are the ways data can leave the app, and possibly your PC. Each needs an
action from you or an enabled integration:

1. **Exporting an archive.** From Operations, you can save a `.tmuarchive` with selected history and, if you choose, saved screenshots. It includes the machine name, friendly name, color, and icon so you can tell where imported records came from. It leaves out settings, API keys, cached provider prices, reprocessing jobs, diagnostics, and search indexes. TrackMeUp saves the archive where you choose and doesn't upload it. You can then keep, copy, or send the file yourself. Import checks the archive and shows a preview before asking you to confirm the merge. Importing the same records again doesn't create duplicates.
2. **Asking an AI provider for analysis.** With AI enabled, an analysis request sends the activity details, system information, and screenshots allowed by your settings directly to your chosen AI provider. OpenAI is the default, using `https://api.openai.com/v1/responses`. You can choose OpenRouter or Anthropic instead.
3. **Sharing a screenshot.** When you share a saved screenshot, TrackMeUp opens Windows Share with that file. You choose the receiving app or destination. TrackMeUp doesn't choose a recipient or upload it automatically.
4. **Sharing logs.** **Report a problem** makes a size-limited copy of the current app log, removes known private paths and secrets, and opens Windows Share. This cleanup can't guarantee that every piece of sensitive information is removed, especially from future log messages. Review the file before sharing it.
5. **Checking current weather.** Weather is on by default in new settings. Without `TRACKMEUP_OPENWEATHER_API_KEY`, the app shows that setup is needed and sends nothing. With a key, World clocks sends the latitude and longitude of your one to four selected cities directly to OpenWeather's Current Weather endpoint. It uses the temperature, conditions, and observation time. Observations are checked again after 12 minutes and expire after at most 45 minutes. They stay in memory and aren't saved to settings, SQLite, reports, diagnostics, or IPC history. A link crediting OpenWeather appears whenever its weather is shown. If a request fails or returns old data, the clocks keep working; the last valid observation may remain visible until it expires. Looking at a past or future time never requests current weather.
6. **Sending optional diagnostics to Sentry.** If `TRACKMEUP_SENTRY_DSN` is set, Sentry receives the configured error events and breadcrumbs (short records of events leading up to an error). This is off by default.
7. **Downloading provider prices.** With AI enabled and OpenAI selected, TrackMeUp may download the public price list from `https://developers.openai.com/api/docs/pricing.md` when its daily cached copy is out of date. The request includes no API key, activity, OCR text, or screenshot. The server still sees normal connection details such as your IP address. This download doesn't run with AI off or another AI provider selected.

Once data reaches an AI provider, OpenWeather, Sentry, or an app you choose in
Windows Share, that service's privacy and data-retention terms apply. TrackMeUp
can't delete copies held by those services.

Reports are built from data on your PC. The included report viewer doesn't start a local HTTP server or contact a TrackMeUp service.

## API keys

For OpenAI, TrackMeUp reads `OPENAI_API_KEY` from Windows environment variables
for the process, user, or machine. When you enter a key in the app, it saves it
in the Windows user environment and makes it available to the running app.
Windows keeps the user value for later launches, outside TrackMeUp's settings
and history.

TrackMeUp doesn't put the key in settings, SQLite, reports, logs, command-line
arguments, command history, IPC diagnostics, or tests. It sends the key directly
to the selected provider in the HTTPS authorization header to authenticate the
analysis request. It doesn't pass through a TrackMeUp server.

The same rule applies to `OPENROUTER_API_KEY` and `ANTHROPIC_API_KEY` when those providers are selected.

Weather uses `TRACKMEUP_OPENWEATHER_API_KEY`, also read from the Windows process,
user, or machine environment. You can enter a key in **World clocks** options
without restarting. After checking its format, the app passes it once to the
local shared tracker running as the same Windows user. That tracker writes it
only to the fixed Windows user and current-process environment variable. New
requests can use it immediately, and later launches can read the saved user value.

The weather key isn't copied into settings, SQLite, logs, command-line arguments,
command history, tests, or IPC diagnostics. Requests go directly to
`https://api.openweathermap.org/data/2.5/weather`. OpenWeather requires the key in
the HTTPS query. TrackMeUp builds that address only for the request and doesn't
save or log it elsewhere, return it through IPC diagnostics, or include it or
the key in error messages.

Turning weather off stops new requests immediately. Removing the Windows user
variable and restarting clears the running app's copy. Local clocks and sun and
moon calculations keep working. The person supplying the key is responsible
for following their OpenWeather plan's data, attribution, redistribution, and
usage terms.

## Packages and services the app uses

These are the packages used directly by the app and its build tools that matter
for how data is handled, as recorded at the time of review. Packages used only
for tests are listed in [Third-Party Notices](../THIRD_PARTY_NOTICES.md).
Dependencies of these packages are resolved through NuGet/npm lock files and
package restore; they also need to be considered when checking data behavior.

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

TrackMeUp makes AI requests with .NET `HttpClient`. You can find the provider
addresses in `TrackMeUp.Core/Application/SettingsCatalog.cs` and the provider
decoder files:

- OpenAI Responses API: `https://api.openai.com/v1/responses`
- OpenRouter chat completions: `https://openrouter.ai/api/v1/chat/completions`
- Anthropic Messages API: `https://api.anthropic.com/v1/messages`

You choose the provider, endpoint, model, thinking effort, how long screenshots
are kept, and whether AI analysis is on. With AI on, permitted scheduled
screenshots are analyzed as they're taken. Screenshots taken from the player
wait until their deletion window ends. API keys aren't accepted in command-line
arguments.

## Local logs and optional error reports

Serilog writes local logs to the console and files. Those outputs don't send
anything over the network. The app keeps daily log files, up to 15 files and
15 days.

Sentry can receive error reports over the network. It's off by default and needs
`TRACKMEUP_SENTRY_DSN` to be set. When enabled, the app configures Sentry to:

- send Information-level breadcrumbs and Error/Critical events;
- disable the default collection of personally identifiable information (PII);
- clear user, request, and server identity fields before sending;
- redact paths, secrets, tokens, authorization text, DSNs, and raw installation identifiers from diagnostic text;
- spend no more than two seconds sending pending diagnostics when the app shuts down.

To set it up on a local or managed Windows installation, run this in PowerShell
7 to save the DSN and environment for your Windows user. Then restart TrackMeUp
so it picks them up:

```powershell
[Environment]::SetEnvironmentVariable('TRACKMEUP_SENTRY_DSN', 'https://PUBLIC_KEY@HOST/PROJECT_ID', [EnvironmentVariableTarget]::User)
[Environment]::SetEnvironmentVariable('TRACKMEUP_SENTRY_ENVIRONMENT', 'production', [EnvironmentVariableTarget]::User)
```

Use a Sentry project DSN, never a Sentry authentication token. The DSN tells the
client where to send reports; it isn't an account password, and someone can
find it on a PC where it's configured. TrackMeUp keeps it out of source code and
saved app settings. An installer or device-management policy can set the same
user variables for managed installations.

An invalid DSN or environment keeps remote reporting off and shows an `invalid`
diagnostics status. The app won't silently send events under a different label.
To turn Sentry off again, set `TRACKMEUP_SENTRY_DSN` to `null` for the Windows
user and restart the app.

You can check the exact behavior in the source. New error messages still need
to be reviewed for private information before they are logged or sent to an
optional remote service.

## How long data stays, and how to delete it

You can preview what a retention cleanup will remove before confirming it.
Cleanup deletes only records and screenshots that TrackMeUp identifies as its
own. It doesn't delete everything in a folder just because you selected it.

By default, activity data and saved screenshots are kept for 30 days. You can
change either period in settings, including setting it to zero. When screenshot
retention is off, temporary images are deleted after analysis. A manual capture
is also removed if you delete it during the player's deletion window.

Text read from screenshots (OCR) expires based on when it was extracted, even
if the image has already been deleted. Measurements associated with a screenshot
expire based on capture time. Both appear in the cleanup preview.

Before deleting a screenshot file, TrackMeUp records that the deletion is
pending. If it fails, the app can retry and finishes pending work the next time
the tracker starts. It still checks that each path belongs under the configured
screenshot folder. Deletion and cleanup only report success after search has
also been updated.

This removes data from use in the app. It doesn't guarantee that someone can't
recover traces from SQLite database pages, Lucene index files, backups, or the disk.

Search uses one local index (`lucene-v4`), updated once per second. Queries read
the last completed update. Deletion and cleanup update search before reporting
success. If indexing fails, you need to rebuild the index manually. Communication
between local processes (IPC) is limited to four requests and is available only
to the same Windows user, with no remote or cross-user access.

The full reset asks for separate confirmation, checks the current installation's
TrackMeUp data folder, removes it along with TrackMeUp-owned screenshots, and
restarts the app. Neither retention cleanup nor the full reset removes exported
archives, files already shared through Windows Share, copies held by an AI
provider or Sentry, API keys in Windows environment variables, or Windows package
and certificate settings.

## Want to check the code?

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
