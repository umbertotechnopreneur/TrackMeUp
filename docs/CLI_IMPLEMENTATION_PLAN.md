# TrackMeUp CLI: product and implementation notes

The CLI lets you check your workday, control tracking, generate reports, and
manage settings from PowerShell 7. It uses the same app services as the desktop
UI, so privacy rules and data stay consistent.

This guide replaces the original rollout plan with a shorter overview. For
ready-to-use commands, output formats, and exit codes, see
[CLI examples](CLI_EXAMPLES.md). For project boundaries, see
[architecture](ARCHITECTURE.md). Supported build targets are x64 and ARM64.

## Two ways to work

- Run `trackmeup.exe -cli` for the interactive command center.
- Run `trackmeup.exe -cli <command>` for a single action or a script.

Use PowerShell 7 with `pwsh -NoProfile`. Help and version work without connecting
to the tracker. Other commands connect to the shared runtime and may start it
with the saved startup settings if it is absent.

Closing a one-shot command does not stop tracking. Use `tracking pause` when
you want to pause the shared tracker.

## What the CLI covers

| Goal | Commands |
| --- | --- |
| See current activity | `status`, `status --watch`, `session last`, `session today` |
| Control tracking | `tracking start`, `tracking pause`, `tracking toggle` |
| Check device readings | `system snapshot` |
| Work with screenshots | `screenshot capture`, `screenshot latest`, `screenshot open-folder` |
| Manage AI | `ai status`, `ai enable`, `ai disable`, `ai configure`, `ai key set`, `ai analyze` |
| Review the day | `report today`, `report digest` |
| Control saved data | `privacy ...`, `retention status`, `retention preview`, `retention run` |
| Adjust settings | `config ...`, `plugins ...`, `startup ...` |
| Get help or diagnose a problem | `--help`, `--version`, `runtime health`, `doctor`, `about` |

Command and option names stay in English. Prompts, descriptions, and messages
follow the selected app language. A leading slash is optional: `/status` and
`status` select the same command. `settings` aliases `config`; `diagnostics`
aliases `doctor`.

This is a capability overview, not a promise that every desktop control has an
identical CLI command. Check command help for the available options.

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
- Keep private paths, window context, screenshot content, keys, and raw requests
  out of diagnostics. Stop cancelled work consistently without creating another
  tracker.
- Clean up temporary images on success, failure, and cancellation when retention
  is disabled. Preserve the approved behavior of retained images.

See [Privacy](PRIVACY.md) for storage, sharing, and deletion behavior.

## Shared implementation

`ITrackMeUpApplication` is the entry point for app behavior. WinUI and CLI code
collect input, call it, and display results. Storage, HTTP, environment access,
capture, Windows integration, and business validation belong in Core services.

One installation-specific mutex selects the tracking process. Other processes
running as the same Windows user connect through a versioned named pipe. Use a
hash of the installation ID in object names, reject unsupported protocol input,
and serialize data changes in the application layer.

The runtime owns settings and local history. Settings updates must be atomic;
invalid input or unsupported stored state must fail clearly. Keep installation
identity on records that need it for reports across computers, without exposing
it unnecessarily in diagnostics.

Use typed requests and shared results with stable codes, localizable message
keys, and validation issues. The public settings catalog defines which keys can
be read or changed; internal fields and secret values are not general settings.
Do not duplicate business models or validation in CLI presentation code.

Current entry points:

- [Application contracts](../TrackMeUp.Core/Application/Contracts.cs)
- [CLI router](../TrackMeUp.Cli/CliRouter.cs)
- [CLI output](../TrackMeUp.Cli/CliOutput.cs)
- [Runtime host](../TrackMeUp.Core/Runtime/RuntimeHost.cs)
- [Public settings](../TrackMeUp.Core/Application/SettingsCatalog.cs)

## Acceptance checks for CLI changes

Verify the behavior affected by the change:

- Interactive and one-shot commands reach the same shared operation.
- Desktop and CLI tracking changes affect one runtime and the same saved data.
- Invalid commands, privacy blocks, unavailable AI, cost limits, timeouts, and
  cancellation return the documented result and exit code.
- Rich, plain, and JSON output remain readable and safe to consume; help and
  version do not open a XAML window or start tracking.
- Secret prompts hide input; output and logs contain no keys or private payloads.
- UTF-8 and all ten app languages work, including both Portuguese variants.
- An installed MSIX exposes the execution alias; portable use points to the
  executable directly. Build evidence and installed-app evidence stay separate.

Use [development](DEVELOPMENT.md) for the build/test workflow and
[manual validation](VALIDATION.md) for desktop checks. Follow the current
[repository rules](../AGENTS.md) rather than the superseded rollout phases.
