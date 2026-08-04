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
- For PowerShell scripts, prefer dry runs for destructive actions.

## Build and validation

- WinUI app build command:
  ```powershell
  dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64
  ```
- For script edits, prefer parser checks before runtime execution.

## Task notes

- `.github/tasks/todo.md` is the active work list.
- `.github/tasks/archive.md` stores closed items.