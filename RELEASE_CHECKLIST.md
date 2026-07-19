# GpxCut Release Checklist (Windows)

This checklist describes what must be completed for a release-ready build.

## 1. Preparation

- Define version number (example: 0.1.0)
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

## 9. GitHub Release Automation (Required Flow)

When a release is requested for download via GitHub, use this exact flow:

1. Commit release-related changes on the working branch.
2. Merge the branch into `master` (or `main`, if used).
3. Create and push a semantic tag on the merged commit (example: `v0.1.1`).
4. The workflow in `.github/workflows/release.yml` runs automatically on tag push (`v*`).
5. The workflow builds and uploads `GpxCut-<version>-win-x64.msi` as a GitHub Release asset.

Verification:

- Check GitHub Actions for a successful `Release` workflow run.
- Confirm the MSI is available on the corresponding GitHub Release page.
