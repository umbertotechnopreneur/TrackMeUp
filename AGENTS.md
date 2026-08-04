# TrackMeUp repository instructions

These instructions apply to all changes in this repository.

- Read this file and `.github/copilot-instructions.md` before editing.
- Keep changes scoped and minimal.
- Preserve unrelated changes in the working tree unless explicitly requested.
- Use `pwsh -NoProfile` for PowerShell commands and script validation.
- Do not commit credentials, secrets, `.env`, keys, or private machine paths.
- Ignore generated artifacts in source control (`bin/`, `obj/`, `artifacts/`, `.vs/`).
- For this repository, the source layout is:
  - `TrackMeUp/` for the app code.
  - `scripts/` for utility scripts.
  - `.github/` for governance and AI context.

Quick commands:

```powershell
git status
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=ARM64
```