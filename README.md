# gpxcut

A Windows desktop editor for loading, visualizing, and editing very large GPX tracks.

## Project Overview

`gpxcut` is aimed at users who need to reliably edit long GPS recordings (for example, 500,000 to 1,000,000 points)
without losing metadata.

The focus is a high-performance workflow for three core operations:

- Split a track at a position
- Mark and delete a range
- Export a partial track as a new GPX file

## Usage

For practical end-user workflows, see [USER_GUIDE.md](USER_GUIDE.md).

Typical workflow:

1. Set two boundary points on the track.
2. Inspect the highlighted in-between section.
3. Move boundary points to refine selection.
4. Delete or export the selected section.
5. Use split to divide a track into two independent parts.

## MVP Goals

- Open a GPX file and display it on an OSM map
- Make core operations (Split, Delete Range, Export Segment) usable end to end
- Provide robust error handling for invalid or broken GPX files
- Preserve time, elevation, and GPX extensions for unmodified points

## Technical Approach

- Platform: Windows
- Language/Runtime: C# on .NET 8
- UI: WPF (MVVM)
- Map: WebView2 + MapLibre GL JS + OSM tiles
- GPX IO: XML streaming for large files

## Status

Early project phase: architecture, base structure, and MVP implementation are being built.

## Development Environment

A step-by-step guide for installation, verification, and troubleshooting is available in [DEVELOPMENT_SETUP.md](DEVELOPMENT_SETUP.md).

## Build and Run Locally

Prerequisites:

- .NET SDK 8
- PowerShell 7+
- Microsoft Edge WebView2 Runtime

Manual:

```powershell
dotnet build -c Debug
dotnet test -c Debug --no-build
dotnet run --project src/GpxCut.App/GpxCut.App.csproj -c Debug
```

Using scripts:

```powershell
pwsh ./scripts/dev/build-debug.ps1
pwsh ./scripts/dev/test-debug.ps1
pwsh ./scripts/dev/run-app.ps1
pwsh ./scripts/dev/start-dev.ps1
```

All in one (Build + tests + start):

```powershell
pwsh ./scripts/dev/start-dev.ps1
```

Options:

```powershell
pwsh ./scripts/dev/start-dev.ps1 -SkipTests
pwsh ./scripts/dev/start-dev.ps1 -SkipBuild
```

Combined validation (Build + tests):

```powershell
pwsh ./skills/gpxcut-track-editing/scripts/validate-solution.ps1
```

## Windows Release (WIP)

Initial scripts for reproducible Windows artifacts are available.

```powershell
pwsh ./scripts/release/publish-gpxcut.ps1
pwsh ./scripts/release/create-installer-layout.ps1
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0
```

Release rule:

- Internal build without certificate: use the standard command above
- External release: build with `-SignArtifacts $true`
- Optional hard enforcement: `-StrictRelease $true` (blocks unsigned builds)

A complete step-by-step guide is available in [installer/wix/README.md](installer/wix/README.md).

A complete release checklist is available in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).
