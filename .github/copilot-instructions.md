---
name: TrackMeUp Baseline
description: Core repository rules for TrackMeUp, a Windows desktop app with repository scripts.
alwaysApply: true
---

# TrackMeUp Baseline

- This repository is a **Windows-first workspace** with:
  - `TrackMeUp/`: WinUI 3 desktop app (`net8.0-windows10.0.19041.0`, x86/x64/ARM64).
  - `scripts/`: project utility scripts adapted from local `PowerShell` templates.
  - `.github/`: instruction and workflow context for AI assistants.

## Required reading

- Read `AGENTS.md` and this file before making changes.

## Repository guardrails

- Use `pwsh -NoProfile` for shell and script runs.
- Do not commit credentials, private secrets, machine paths, or secrets.
- Keep unrelated working-tree changes untouched.
- Exclude generated artifacts (`bin/`, `obj/`, `artifacts/`, `.vs/`) from commits.
- Prefer explicit, scoped edits and minimal churn.
- WinUI views/code-behind and Spectre.Console commands/renderers are passive presentation code; they call `ITrackMeUpApplication` and never construct infrastructure services or perform I/O, process, registry, environment, HTTP, capture, hook, or persistence operations.
- Keep runtime ownership singular through the hashed-installation mutex and same-user versioned named pipe. All persistence mutations stay serialized in the application layer.
- Never place API keys in command arguments, settings, history, logs, redacted IPC diagnostics, or test snapshots. Use the environment-variable secret flow.
- For PowerShell scripts, prefer dry runs for destructive actions.
- Add XML doc comments (`///`) to public/protected methods, and include 1-2 clear inline comment lines for critical runtime paths (I/O, process/OS interop, and external calls).
- For any service/monitoring logic, include explicit comments describing failure behavior and fallback path.
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
- `.github/tasks/archive.md` stores closed items.
