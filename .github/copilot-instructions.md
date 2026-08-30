---
name: TrackMeUp Baseline
description: Core repository rules for TrackMeUp, a Windows desktop app with repository scripts.
alwaysApply: true
---

# TrackMeUp Baseline

- This repository is a **Windows-first workspace** with:
  - `TrackMeUp/`: WinUI 3 desktop app (`net10.0-windows10.0.19041.0`, x86/x64/ARM64).
  - `scripts/`: project utility scripts adapted from local `PowerShell` templates.
  - `.github/`: instruction and workflow context for AI assistants.

## Required reading

- Read `AGENTS.md` and this file before making changes.

## Repository guardrails

- Support PowerShell 7 only: invoke every PowerShell command through `pwsh -NoProfile` (the supported equivalent of `--noprofile`); do not use Windows PowerShell 5.1 or bare `powershell`/`pwsh`.
- Avoid PowerShell quoting errors: prefer `pwsh -NoProfile -File <script.ps1>` for scripts and `pwsh -NoProfile -Command '<single-quoted command>'` for short commands; pass arguments as arrays or explicit parameters, do not build nested shell strings, and escape embedded quotes for the receiving command instead of relying on PowerShell interpolation.
- This repository is pre-production: do not add backward-compatibility layers for superseded contracts, persisted artifacts, filenames, or APIs unless explicitly requested. Prefer the clean current design and make migrations explicit.
- Fail fast on invalid input, unsupported state, missing required configuration, and persistence or interop failures; do not silently normalize, ignore, or fall back unless the fallback is part of the documented product behavior.
- Do not preserve legacy code or superseded contracts for compatibility. Remove obsolete code paths, adapters, fallbacks, and persisted settings when a feature is replaced; unsupported legacy input must fail fast.
- Do not commit credentials, private secrets, machine paths, or secrets.
- Start every first-party C# source file with `// SPDX-License-Identifier: MIT`; preserve original notices in generated or third-party files.
- Project-authored software and documentation are MIT-licensed. Keep the canonical license text in `LICENSE`; do not add distribution or commercial-use restrictions to it.
- Treat the TrackMeUp name, logos, app icons, and project-authored brand artwork separately under `TRADEMARKS.md`, and preserve all third-party license and attribution notices.
- Keep unrelated working-tree changes untouched.
- Never create a Git branch or worktree unless the user explicitly asks for it or approves it first.
- Exclude generated artifacts (`bin/`, `obj/`, `artifacts/`, `.vs/`) from commits.
- Ignore automatic version metadata changes in `TrackMeUp/build-version.json` and version-only updates in `TrackMeUp/Package.appxmanifest`: do not inspect, restore, report, stage, or commit them unless the user explicitly asks to manage the application version.
- Prefer explicit, scoped edits and minimal churn.
- Be economical with verification: run checks primarily once the task is complete, not after every intermediate step. Repeat or add an earlier check only when it is needed to diagnose a failure, unblock the work, or prevent a risky mistake.
- After every successful commit and push, run the relevant `dotnet clean` (x64 by default) and remove stale test build outputs. When an installer is produced, keep the newly validated installer and delete older generated installer/package artifacts only after resolving and verifying their paths under this repository's `artifacts/` directory.
- Keep product wording vendor-agnostic: user-facing shared AI features must say "AI provider" ("provider AI" in Italian). Name OpenAI, OpenRouter, Anthropic, or another vendor only when the UI refers to a selected provider or genuinely vendor-specific behavior such as its endpoint, model, or pricing.
- WinUI views/code-behind and Spectre.Console commands/renderers are passive presentation code; they call `ITrackMeUpApplication` and never construct infrastructure services or perform I/O, process, registry, environment, HTTP, capture, hook, or persistence operations.
- Every icon-only WinUI button or toggle must have a localized tooltip and the same localized accessible name; never rely on the glyph alone.
- Keep runtime ownership singular through the hashed-installation mutex and same-user versioned named pipe. All persistence mutations stay serialized in the application layer.
- Never place API keys in command arguments, settings, history, logs, redacted IPC diagnostics, or test snapshots. Use the environment-variable secret flow.
- For PowerShell scripts, prefer dry runs for destructive actions.
- Add XML doc comments (`///`) to public/protected methods, and include 1-2 clear inline comment lines for critical runtime paths (I/O, process/OS interop, and external calls).
- For any service/monitoring logic, include explicit comments describing failure behavior and fallback path.
- For Screenshot UI work, break the window into reusable components and keep data/business logic in models/services; UI should remain passive and only render/interact. **Avoid duplicate big titles, avoid card wrappers around controls, and emphasize a translucent Mica/Acrylic look.**
- For code quality checks before shipping changes, include:
  - method-level XML docs for new/modified public APIs
  - explicit fallback comments in exception/guard clauses
  - language separation between UI strings and business logic
  - at least one unit/integration scenario checklist entry in `README` if behavior changed

## Build and validation

- WinUI app build command:
  ```powershell
  dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64
  ```
- For script edits, prefer parser checks before runtime execution.

## Task notes

- `.github/tasks/todo.md` is the active work list.
- `.github/tasks/archive.md` stores closed items
