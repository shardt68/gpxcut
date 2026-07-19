# GpxCut Release Checklist (Windows)

This checklist describes what must be completed for a release-ready build.

## 1. Preparation

- Define version number (example: 0.1.0)
- **Update [VERSION_HISTORY.md](VERSION_HISTORY.md)** with new section for this version at the top, including:
  - Release date
  - Type (Feature/Bug fix/Maintenance)
  - Detailed description of changes
- Prepare changelog/release notes
- Ensure there are no critical unresolved local changes

## 2. Quality Gates

```powershell
dotnet build -c Release
dotnet test -c Release
```

Acceptance:
- Build successful
- Tests successful

## 3. Generate Artifacts

```powershell
pwsh ./scripts/release/publish-gpxcut.ps1
pwsh ./scripts/release/create-installer-layout.ps1
```

Acceptance:
- Layout exists under `artifacts/installer-layout/win-x64`
- `GpxCut.App.exe` exists

## 4. Build MSI

WiX version note:
- CI pins WiX `6.*` in [.github/workflows/release.yml](.github/workflows/release.yml) for reproducible builds.
- Local machines may have WiX 7 installed; this is supported for local packaging.
- If local commands fail with a 6 vs 7 version conflict, see [installer/wix/WIX_VERSIONS.md](installer/wix/WIX_VERSIONS.md).

### Internal Build (Without Certificate)

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0
```

### External Release (With Certificate, Strict Mode Recommended)

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0 -StrictRelease $true -SignArtifacts $true -PfxPath "C:\certs\gpxcut.pfx" -PfxPassword "<password>"
```

Acceptance:
- MSI is located under `artifacts/installer/win-x64`
- Filename matches version and runtime

## 5. Verify Signature (Release)

```powershell
Get-AuthenticodeSignature ./artifacts/installer-layout/win-x64/GpxCut.App.exe
Get-AuthenticodeSignature ./artifacts/installer/win-x64/GpxCut-0.1.0-win-x64.msi
```

Acceptance:
- `Status` is `Valid`

## 6. Installer Smoke Test

- Install MSI on a clean Windows system
- Start app
- Open GPX
- Confirm map is visible and usable
- Quickly test Split/Delete/Export
- Verify uninstallation

## 7. Collect Delivery Artifacts

- MSI
- SHA256 checksum
- Release Notes

Generate checksum:

```powershell
Get-FileHash ./artifacts/installer/win-x64/GpxCut-0.1.0-win-x64.msi -Algorithm SHA256
```

## 8. Release

- Internal approval documented
- Artifacts and notes published
- Repository tag created (if used)
