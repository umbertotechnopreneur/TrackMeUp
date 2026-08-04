# TrackMeUp

TrackMeUp is a Windows WinUI 3 desktop application with a repository utility script layer for local workflows.

## Repository structure

- `TrackMeUp/` — WinUI 3 app source, app manifest, project files and assets.
- `scripts/` — PowerShell utility scripts and shared modules.
- `.github/` — governance files, Copilot instructions, and task notes.
- `AGENTS.md` — mandatory agent instructions for any contributor.

## Build and run

Prerequisites:

- Windows 10+ with Visual Studio 2022 or compatible .NET 8 toolchain.
- `dotnet` CLI available.

```powershell
dotnet restore .\TrackMeUp\TrackMeUp.csproj
dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x64
```

Optional builds:

- `dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=ARM64`
- `dotnet build .\TrackMeUp\TrackMeUp.csproj -p:Platform=x86`

## Governance and automation

- `.github/copilot-instructions.md` and `AGENTS.md` define contribution and agent constraints.
- `.github/tasks/todo.md` is the active task list.

## License

This project uses the MIT license.

## CI

- GitHub Actions workflow: `.github/workflows/build.yml` runs restore and build on supported platforms.

## AI assistance

AI can assist with drafting, review, and implementation suggestions.
The maintainer remains responsible for verification, security decisions, and releases.
