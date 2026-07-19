# GpxCut Windows Installer

This document describes the exact MSI build process and what to do in each case.

## Goal

- Quickly build internal test MSI packages (unsigned allowed)
- Build release MSI packages only when signed (optionally enforced by strict mode)

## Prerequisites

- WiX Toolset CLI (`wix`) in `PATH`
- PowerShell 7+
- .NET SDK 8

One-time setup for WiX v7:

```powershell
wix eula accept wix7
```

## Workflow (Always the Same)

1. Publish application

```powershell
pwsh ./scripts/release/publish-gpxcut.ps1
```

2. Generate installer layout

```powershell
pwsh ./scripts/release/create-installer-layout.ps1
```

3. Build MSI (variant depends on target, see below)

MSI output is under `artifacts/installer/win-x64`.

## Variant A: Internal Build (Without Certificate)

Use for local tests, QA, and internal distribution.

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0
```

Behavior:
- Default is `-SignArtifacts $false`
- Build completes and shows a warning for unsigned artifacts

## Variant B: Release Build (With Certificate)

Use for external distribution.

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0 -SignArtifacts $true -PfxPath "C:\certs\gpxcut.pfx" -PfxPassword "<password>"
```

Behavior:
- `GpxCut.App.exe` in the layout and the generated MSI are signed

## Strict Release (Guardrail)

Use this if releases must never be built unsigned:

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0 -StrictRelease $true -SignArtifacts $true -PfxPath "C:\certs\gpxcut.pfx" -PfxPassword "<password>"
```

Rule:
- `-StrictRelease $true` plus `-SignArtifacts $false` intentionally fails

## Important Notes

- Without certificate: use only Variant A
- With certificate: use Variant B or Strict Release
- Installer files are harvested recursively from `artifacts/installer-layout/<runtime>` (excluding `*.pdb` and `*.wixpdb`)
- When calling bool parameters from Bash, prefer PowerShell literals inside a `-Command`

```powershell
pwsh -NoProfile -Command '& ./installer/wix/build-msi.ps1 -Version 0.1.0 -StrictRelease $true'
```

For complete acceptance through delivery, see [RELEASE_CHECKLIST.md](../../RELEASE_CHECKLIST.md).
