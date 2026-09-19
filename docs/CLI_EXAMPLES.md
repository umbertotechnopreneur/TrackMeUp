# CLI examples

Check activity, control tracking, and inspect saved screenshots from PowerShell 7. These
examples use the installed `trackmeup.exe` command and preserve its exit code.
For an unpackaged build, replace it with the quoted executable path after `&`.

The CLI uses the same runtime as the desktop app. Commands can start that runtime when it is absent, applying its saved startup settings. Help and version commands do not connect to the runtime. The expected results below describe the output; they are not output captured from a user's installation.

## Status and diagnostics

Read the current dashboard, runtime health, and combined diagnostics:

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli status --format rich; exit $LASTEXITCODE'
pwsh -NoProfile -Command '& trackmeup.exe -cli runtime health --format plain; exit $LASTEXITCODE'
pwsh -NoProfile -Command '& trackmeup.exe -cli doctor --format plain; exit $LASTEXITCODE'
```

`status` shows tracking and current activity. `runtime health` checks the shared
tracker; `doctor` adds AI, retention, startup, and plugin checks. A complete
diagnostic result uses `doctor.healthy`; a partial result uses `doctor.partial`
and exit code `10`.

For a dashboard refreshed every five seconds:

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli status --watch --interval 5 --format rich; exit $LASTEXITCODE'
```

Press Ctrl+C to stop watching. Intervals must be integers from `1` through `60`, and `--interval` requires `--watch`. JSON supports a single snapshot, so it cannot be combined with `--watch`.

## Start, pause, or toggle tracking

Choose the command for the state change you want; these commands act on the shared runtime:

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli tracking start --format plain; exit $LASTEXITCODE'
pwsh -NoProfile -Command '& trackmeup.exe -cli tracking pause --format plain; exit $LASTEXITCODE'
pwsh -NoProfile -Command '& trackmeup.exe -cli tracking toggle --format plain; exit $LASTEXITCODE'
```

Each successful command returns the resulting dashboard state. `toggle` switches from the current state, so use `start` or `pause` when a script needs a specific outcome. `--start`, `--pause`, and `--toggle` are equivalent shortcuts after `-cli`.

## Check AI setup

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli ai status --format plain; exit $LASTEXITCODE'
```

See whether AI is on, which provider and model are selected, and whether the key
and cost allowance are ready. This command shows no key value and sends no
analysis request.

## Retain a local screenshot

Screenshot capture follows the current AI provider setting. For a local-only example, the following script checks that AI provider features are disabled before capturing. If they are enabled, disable them in the app and rerun this example. Keep them disabled while the command runs. `--keep` retains the screenshot; it does not disable AI analysis.

```powershell
pwsh -NoProfile -Command '
    $providerJson = & trackmeup.exe -cli ai status --json
    $providerExit = $LASTEXITCODE
    if ($providerExit -ne 0) {
        $providerJson
        exit $providerExit
    }
    $provider = ($providerJson -join "`n") | ConvertFrom-Json
    if ($provider.value.enabled) {
        throw "Disable AI provider features before running this local-only capture."
    }
    & trackmeup.exe -cli screenshot capture --mode active-window --keep --format plain
    exit $LASTEXITCODE
'
```

Use a window containing synthetic content for a trial capture. The command returns capture details for the retained local screenshot when screenshot capture is enabled and privacy rules permit it. The active window is the foreground window at capture time, which may be the terminal. `--mode all-screens` selects all displays; omitting `--mode` uses the saved setting.

Inspect the latest retained screenshot's details:

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli screenshot latest --format plain; exit $LASTEXITCODE'
```

## Preview retention before deciding on deletion

Read the configured retention periods, then inspect the candidates:

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli retention status --format plain; exit $LASTEXITCODE'
pwsh -NoProfile -Command '& trackmeup.exe -cli retention preview --json; exit $LASTEXITCODE'
```

The preview deletes nothing. Read its fields as follows:

- `fileCount` counts both expired screenshots and database records, including
  activity, AI, OCR, and saved device readings.
- `totalBytes` estimates the affected content, not the disk space you will regain.
- `paths` lists affected screenshots and, where relevant, the activity database.
  Cleanup removes expired records, not the entire database.

Review both retention periods before running cleanup.

Use command help before confirming deletion with `retention run --yes`:

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli retention --help --format plain; exit $LASTEXITCODE'
```

Calling `retention run` without confirmation fails with `retention.confirmation.required` and exit code `3`. A preview does not freeze the candidate set; a confirmed run evaluates retention again.

## Output formats, locale, and timeout

| Option | Behavior |
| --- | --- |
| `--format rich` | Spectre.Console panels and tables for an interactive terminal. This is the default when output is not redirected. |
| `--format plain` | A localized result line and, when present, an indented JSON representation of its value, without ANSI controls. This is the default when output/error is redirected. |
| `--format json` or `--json` | One JSON result document suitable for a script. |
| `--quiet` | Suppresses successful human-readable results. JSON still emits its result document. |
| `--language it-IT` | Selects the human-readable language. The default is `system`. |
| `--timeout 15` | Sets the default timeout for each shared-runtime request in seconds; accepted values are `1` through `300`, with default `5`. Background-host discovery uses a separate retry window capped at five seconds. This is not a total-duration limit for a multi-request workflow. |

For Italian plain-text status with a longer timeout:

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli status --format plain --language it-IT --timeout 15; exit $LASTEXITCODE'
```

Supported locale choices are `system`, `en-US`, `it-IT`, `fr-FR`, `de-DE`, `es-ES`, `zh-Hans`, `vi-VN`, `ko-KR`, `pt-PT`, and `pt-BR`. Use the full supported tags; values such as `en` or `pt` are rejected.

For automation, check the process result before parsing successful output:

```powershell
pwsh -NoProfile -Command '
    $json = & trackmeup.exe -cli status --json --language en-US --timeout 15
    $cliExit = $LASTEXITCODE
    if ($cliExit -ne 0) {
        $json
        exit $cliExit
    }
    $result = ($json -join "`n") | ConvertFrom-Json
    [pscustomobject]@{
        Code = $result.code
        IsTracking = $result.value.isTracking
    }
    exit 0
'
```

The JSON envelope has stable field names: `succeeded`, `code`, `messageKey`, `value`, and `issues`. Each validation issue has `field`, `code`, and `messageKey`. Locale selection does not rename these fields or stable result codes; human-facing strings inside values can still be localized. Branch on the exit code and `code`, not translated messages. Invalid global options can fail before JSON rendering and write a diagnostic to standard error instead.

| Exit code | Meaning |
| --- | --- |
| `0` | Success. |
| `2` | Unknown command, invalid command arguments, or invalid global options. |
| `3` | Validation or required-value/confirmation failure; includes `ai.configuration.invalid`. |
| `4` | Shared runtime unavailable. |
| `5` | Privacy rules blocked the operation. |
| `6` | AI provider features disabled (`ai.disabled`). |
| `7` | AI cost guardrail blocked the operation. |
| `8` | An operation returned a failure code ending in `.failed`. |
| `9` | Unsupported runtime IPC protocol. |
| `10` | Other application failure, including partial diagnostics. |
| `130` | Command cancellation, including stopping a watch with Ctrl+C. |

## Discover additional commands

```powershell
pwsh -NoProfile -Command '& trackmeup.exe -cli --help --format plain; exit $LASTEXITCODE'
pwsh -NoProfile -Command '& trackmeup.exe -cli /ai --help --format plain; exit $LASTEXITCODE'
pwsh -NoProfile -Command '& trackmeup.exe -cli --version --json; exit $LASTEXITCODE'
```

The leading slash is optional: `/status` and `status` select the same command. Omitting a command opens the interactive command center when rich output and interactive input are available.
