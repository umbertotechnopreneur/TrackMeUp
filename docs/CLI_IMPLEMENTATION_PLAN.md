# WorkTrail CLI: product and implementation notes

The Premium CLI lets you search history, query reports, control tracking, inspect
screenshots, transfer data and manage AI operations from PowerShell 7. It uses the
same app services as the desktop UI. Free has no CLI access, including help,
version and the interactive shell.

This guide replaces the original rollout plan with a shorter overview. For
ready-to-use commands, output formats, and exit codes, see
[CLI examples](CLI_EXAMPLES.md). For project boundaries, see
[architecture](ARCHITECTURE.md). Supported build targets are x64 and ARM64.

## Two ways to work

- Run `worktrail.exe -cli` for the interactive command center.
- Run `worktrail.exe -cli <command>` for a single action or a script.

Use PowerShell 7 with `pwsh -NoProfile`. Every command connects to the shared
runtime to verify Premium, including help and version. Connecting may start the
runtime with its saved startup settings if it is absent. Free returns
`cli.premium.required` and exit code `11`; failed access verification blocks execution.

Closing a one-shot command does not stop tracking. Use `tracking pause` when
you want to pause the shared tracker.

## What the CLI covers

| Goal | Commands |
| --- | --- |
| See current activity | `status`, `status --watch`, `session last`, `session today` |
| Control tracking | `tracking start`, `tracking pause`, `tracking toggle` |
| Search local history | `search status`, `search query` with filters and pagination |
| Read activity reports | `report --from ... --to ... --timezone ... [--view ...]` |
| Read or convert city times | `world-clock list`, `world-clock cities`, `world-clock convert` |
| Check device readings | `system snapshot`, `hardware snapshot` |
| Work with screenshots | `screenshot capture`, `screenshot latest`, `screenshot open-folder`, `screenshots gallery`, `screenshots delete` |
| Transfer data | `data export`, `data import preview`, `data import run` |
| Manage AI | `ai status`, `ai enable`, `ai disable`, `ai configure`, `ai key set`, `ai analyze`, `ai models`, `ai test`, `ai pricing`, `ai reprocess ...` |
| Control saved data | `privacy ...`, `retention status`, `retention preview`, `retention run` |
| Adjust settings | `config ...`, `plugins ...`, `startup ...` |
| Get help or diagnose a problem | `--help`, `--version`, `runtime health`, `doctor`, `about` |
| Inspect support and access state | `logs open`, `logs open-folder`, `access status` |
| Reset local data | `reset preview`, `reset run --yes --confirm DELETE-ALL-DATA` |

Command and option names stay in English. Prompts, descriptions, and messages
follow the selected app language. A leading slash is optional: `/status` and
`status` select the same command. `settings` aliases `config`; `diagnostics`
aliases `doctor`; `screenshots` aliases `screenshot`.

This is a capability overview, not a promise that every desktop control has an
identical CLI command. Check command help for the available options.

Celestial views, maps, window placement and native share dialogs are outside the
CLI scope. World clocks return city/time data. Hardware reads do not enable
advanced telemetry or install prerequisites.

The runtime currently starts Free because the commercial license source is not
connected. Development testing can use the existing runtime-owned Debug simulation
from the desktop menu. The CLI cannot grant itself access. See [Premium access](PREMIUM_FEATURES.md).

## Clear terminal output

Use rich output for an interactive dashboard, plain output for readable text,
and JSON for automation. Redirected output should stay free of animations and
terminal color codes. Progress must reflect real work.

JSON uses stable English field names and result codes. Scripts should check the
exit code before parsing a successful result and should never depend on translated
messages. See the complete [output and exit-code reference](CLI_EXAMPLES.md#output-formats-locale-and-timeout).

Keep narrow terminals readable, respect reduced motion, and handle missing emoji
support. Escape external text before passing it to Spectre.Console markup.
Interactive and one-shot commands must use the same router and validation.

## Privacy and user control

- Show AI configuration status without revealing the key. Collect keys with a
  hidden prompt and save them only through the approved Windows environment flow.
  Never accept a secret as a command argument or retain it in command history.
- Apply screenshot, privacy, AI, and cost controls in shared services, before
  capturing or sending data. `--keep` retains a screenshot; it does not turn AI off.
- Preview cleanup before deleting data. Destructive commands require their
  documented confirmation; a preview does not freeze the later deletion set.
- Screenshot deletion previews by default; `--yes` executes after
  a fresh service read. Export previews the request, not filesystem availability
  or record counts; confirmed export can replace the destination archive.
- Import and historical AI processing require a runtime-issued preview plan ID.
  Expired plans are not silently replaced. AI connection tests, reprocessing start
  and resume require `--yes` because they can contact the AI provider and incur cost.
- Reset requires `--yes --confirm DELETE-ALL-DATA` in both scripted and interactive
  commands. Preview describes scope without stopping tracking. `app.reset.accepted`
  confirms acceptance only: the runtime owner deletes and relaunches after responding.
- Keep private paths, window context, screenshot content, keys, and raw requests
  out of diagnostics. Stop cancelled work consistently without creating another
  tracker.
- Clean up temporary images on success, failure, and cancellation when retention
  is disabled. Preserve the approved behavior of retained images.

See [Privacy](PRIVACY.md) for storage, sharing, and deletion behavior.

## Shared implementation

`IWorkTrailApplication` is the entry point for app behavior. WinUI and CLI code
collect input, call it, and display results. Storage, HTTP, environment access,
capture, Windows integration, and business validation belong in Core services.

One installation-specific mutex selects the tracking process. Other processes
running as the same Windows user connect through a versioned named pipe. Use a
hash of the installation ID in object names, reject unsupported protocol input,
and serialize data changes in the application layer.

The runtime owns settings and local history. Settings updates must be atomic;
invalid input or unsupported stored state must fail clearly. Keep installation
identity on records that need it for activity aggregation across computers, without exposing
it unnecessarily in diagnostics.

Use typed requests and shared results with stable codes, localizable message
keys, and validation issues. The public settings catalog defines which keys can
be read or changed; internal fields and secret values are not general settings.
Do not duplicate business models or validation in CLI presentation code.

Current entry points:

- [Application contracts](../WorkTrail.Core/Application/Contracts.cs)
- [CLI router](../WorkTrail.Cli/CliRouter.cs)
- [CLI output](../WorkTrail.Cli/CliOutput.cs)
- [Runtime host](../WorkTrail.Core/Runtime/RuntimeHost.cs)
- [Public settings](../WorkTrail.Core/Application/SettingsCatalog.cs)

## Acceptance checks for CLI changes

Verify the behavior affected by the change:

- Interactive and one-shot commands reach the same shared operation.
- Desktop and CLI tracking changes affect one runtime and the same saved data.
- Invalid commands, privacy blocks, unavailable AI, cost limits, timeouts, and
  cancellation return the documented result and exit code.
- Rich, plain, and JSON output remain readable and safe to consume. Free, unavailable
  access state and a downgrade during watch or shell must block further data reads.
  Help and version also require the runtime connection and Premium access.
- Secret prompts hide input; output and logs contain no keys or private payloads.
- UTF-8 and all ten app languages work, including both Portuguese variants.
- An installed MSIX exposes the execution alias; portable use points to the
  executable directly. Build evidence and installed-app evidence stay separate.

Use [development](DEVELOPMENT.md) for the build/test workflow and
[manual validation](VALIDATION.md) for desktop checks. Follow the current
[repository rules](../AGENTS.md) rather than the superseded rollout phases.
