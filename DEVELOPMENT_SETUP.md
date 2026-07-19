# Development Environment Setup (Windows)

This guide describes how to set up the `gpxcut` development environment on Windows.
It is optimized for external contributors and follows a winget-first approach.

## 1. Goal and Scope

### Target Audience
- External contributors (primary)
- Internal team (secondary)

### In Scope
- Local development environment for build, test, and skill validation
- Access to project resources and reference documents

### Out of Scope
- End-user installer and release distribution
- Non-Windows platforms (MVP is Windows-only)

## 2. Required and Optional Tools

## Required Tools
- Git
- .NET SDK 8
- Visual Studio Code
- PowerShell 7+
- Microsoft Edge WebView2 Runtime

## Optional/Conditional
- `skills-ref` validator (for skill validation)
- Node.js (only if additional frontend/asset tooling is needed in the MapBridge workflow)

## 3. Installation (Winget-First)

Run the following commands in PowerShell:

```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.8 -e
winget install --id Microsoft.VisualStudioCode -e
winget install --id Microsoft.PowerShell -e
winget install --id Microsoft.EdgeWebView2Runtime -e
```

If `winget` is not available, install the tools manually from the corresponding vendor websites.

## 4. Verify Versions and Installation

```powershell
git --version
dotnet --info
dotnet --list-sdks
pwsh --version
```

Optional:

```powershell
skills-ref --version
node --version
npm --version
```

## 5. Clone Repository and Start Locally

```powershell
git clone <REPO_URL>
cd gpxcut
```

## 6. Project Validation

### Standard Path (Existing Script)

```powershell
pwsh ./skills/gpxcut-track-editing/scripts/validate-solution.ps1
```

The script currently runs:
- `dotnet build -c Debug`
- `dotnet test -c Debug --no-build`

### Manual Fallback

```powershell
dotnet build -c Debug
dotnet test -c Debug --no-build
```

## 6a. Start App Locally

Manual:

```powershell
dotnet run --project src/GpxCut.App/GpxCut.App.csproj -c Debug
```

By script:

```powershell
pwsh ./scripts/dev/run-app.ps1
```

## 6b. Developer Helper Scripts

- Build: `pwsh ./scripts/dev/build-debug.ps1`
- Tests: `pwsh ./scripts/dev/test-debug.ps1`
- Start app: `pwsh ./scripts/dev/run-app.ps1`
- All in one: `pwsh ./scripts/dev/start-dev.ps1`
- Combined build+tests: `pwsh ./skills/gpxcut-track-editing/scripts/validate-solution.ps1`

Examples for `start-dev.ps1`:

```powershell
pwsh ./scripts/dev/start-dev.ps1
pwsh ./scripts/dev/start-dev.ps1 -SkipTests
pwsh ./scripts/dev/start-dev.ps1 -SkipBuild
```

## 7. What Counts as "Setup Successful"

The environment is considered correctly set up when:
- All required tools are installed
- `dotnet --list-sdks` shows an 8.x SDK
- Build and tests run without errors
- Skill structure is present and (if used) `skills-ref validate` runs successfully

## 8. Project Resources and Reading Order

Recommended onboarding order for new contributors:

1. [README.md](README.md) - project overview and goals
2. [AGENTS.md](AGENTS.md) - architecture principles, scope, working mode
3. [skills/README.md](skills/README.md) - skill structure and validator guidance
4. [skills/gpxcut-track-editing/SKILL.md](skills/gpxcut-track-editing/SKILL.md) - workflow and quality gates
5. [skills/gpxcut-track-editing/references/REFERENCE.md](skills/gpxcut-track-editing/references/REFERENCE.md) - architectural boundaries and priorities
6. Feature specifications under [skills/gpxcut-track-editing/references/features](skills/gpxcut-track-editing/references/features)

## 9. Troubleshooting (Common Issues)

### Wrong or Missing .NET SDK
- Symptom: build fails with SDK errors
- Check: `dotnet --list-sdks`
- Fix: install .NET SDK 8 and start a new terminal

### Missing WebView2 Runtime
- Symptom: map view does not start correctly
- Fix: install Microsoft Edge WebView2 Runtime

### PowerShell Blocks Script Execution
- Symptom: `validate-solution.ps1` is not allowed to run
- Check/Fix (CurrentUser):

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

### Proxy/Firewall Blocks Package Sources or OSM Tiles
- Symptom: installs or map tiles fail
- Fix: configure proxy settings correctly for `winget`, Git, and system networking

## 10. Reproducibility (Optional, Nice to Have)

Recommended next steps:
- Create an optional bootstrap script for tool checks and preflight
- Create an optional diagnostics script for local consistency checks before commits
- Keep documentation updated with every toolchain change

## 11. Maintenance Process for This Doc

- Update this file whenever .NET or the toolchain is upgraded
- Extend installation and verification sections when new required tools are added
- Always run local build+test checks after changing setup steps
