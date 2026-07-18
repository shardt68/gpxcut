# GpxCut Windows Installer

Diese Doku beschreibt das konkrete Vorgehen fuer MSI-Builds und was jeweils zu tun ist.

## Ziel

- Interne Test-MSI schnell bauen (unsigniert erlaubt)
- Release-MSI nur signiert bauen (optional durch Strict-Mode erzwungen)

## Voraussetzungen

- WiX Toolset CLI (`wix`) in `PATH`
- PowerShell 7+
- .NET SDK 8

Einmalig bei WiX v7:

```powershell
wix eula accept wix7
```

## Ablauf (immer gleich)

1. Anwendung publishen

```powershell
pwsh ./scripts/release/publish-gpxcut.ps1
```

2. Installer-Layout erzeugen

```powershell
pwsh ./scripts/release/create-installer-layout.ps1
```

3. MSI bauen (Variante je nach Ziel, siehe unten)

MSI-Ausgabe liegt unter `artifacts/installer/win-x64`.

## Variante A: Interner Build (ohne Zertifikat)

Nutzen fuer lokale Tests, QA und interne Verteilung.

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0
```

Verhalten:
- Standard ist `-SignArtifacts $false`
- Build laeuft durch und gibt eine Warnung fuer unsignierte Artefakte aus

## Variante B: Release Build (mit Zertifikat)

Nutzen fuer externe Distribution.

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0 -SignArtifacts $true -PfxPath "C:\certs\gpxcut.pfx" -PfxPassword "<password>"
```

Verhalten:
- `GpxCut.App.exe` im Layout und das erzeugte MSI werden signiert

## Strict Release (Guardrail)

Wenn Releases niemals unsigniert gebaut werden duerfen:

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0 -StrictRelease $true -SignArtifacts $true -PfxPath "C:\certs\gpxcut.pfx" -PfxPassword "<password>"
```

Regel:
- `-StrictRelease $true` plus `-SignArtifacts $false` fuehrt absichtlich zu einem Fehler

## Wichtige Hinweise

- Ohne Zertifikat: nur Variante A verwenden
- Mit Zertifikat: Variante B oder Strict Release verwenden
- Die Installer-Dateien werden rekursiv aus `artifacts/installer-layout/<runtime>` geharvestet (ohne `*.pdb` und `*.wixpdb`)
- Wenn bool-Parameter aus Bash aufgerufen werden, besser PowerShell-Literale in einem `-Command` verwenden

```powershell
pwsh -NoProfile -Command '& ./installer/wix/build-msi.ps1 -Version 0.1.0 -StrictRelease $true'
```

Fuer die komplette Abnahme bis zur Auslieferung siehe [RELEASE_CHECKLIST.md](../../RELEASE_CHECKLIST.md).
