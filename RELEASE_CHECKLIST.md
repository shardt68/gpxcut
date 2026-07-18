# GpxCut Release-Checkliste (Windows)

Diese Checkliste beschreibt, was fuer einen veroeffentlichungsfaehigen Build erledigt sein muss.

## 1. Vorbereitung

- Versionsnummer festlegen (Beispiel: 0.1.0)
- Changelog/Release Notes vorbereiten
- Sicherstellen, dass lokal keine kritischen offenen Aenderungen mehr ausstehen

## 2. Qualitaets-Gates

```powershell
dotnet build -c Release
dotnet test -c Release
```

Abnahme:
- Build erfolgreich
- Tests erfolgreich

## 3. Artefakte erzeugen

```powershell
pwsh ./scripts/release/publish-gpxcut.ps1
pwsh ./scripts/release/create-installer-layout.ps1
```

Abnahme:
- Layout unter `artifacts/installer-layout/win-x64` vorhanden
- `GpxCut.App.exe` vorhanden

## 4. MSI bauen

### Interner Build (ohne Zertifikat)

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0
```

### Externer Release (mit Zertifikat, empfohlen mit Strict-Mode)

```powershell
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0 -StrictRelease $true -SignArtifacts $true -PfxPath "C:\certs\gpxcut.pfx" -PfxPassword "<password>"
```

Abnahme:
- MSI liegt unter `artifacts/installer/win-x64`
- Dateiname entspricht Version und Runtime

## 5. Signatur pruefen (Release)

```powershell
Get-AuthenticodeSignature ./artifacts/installer-layout/win-x64/GpxCut.App.exe
Get-AuthenticodeSignature ./artifacts/installer/win-x64/GpxCut-0.1.0-win-x64.msi
```

Abnahme:
- `Status` ist `Valid`

## 6. Installer Smoke-Test

- MSI auf sauberem Windows-System installieren
- App starten
- GPX oeffnen
- Karte sichtbar und bedienbar
- Split/Delete/Export kurz antesten
- Deinstallation pruefen

## 7. Auslieferungs-Artefakte sammeln

- MSI
- SHA256-Checksumme
- Release Notes

Checksumme erzeugen:

```powershell
Get-FileHash ./artifacts/installer/win-x64/GpxCut-0.1.0-win-x64.msi -Algorithm SHA256
```

## 8. Freigabe

- Interne Freigabe dokumentiert
- Artefakte und Notes veroeffentlicht
- Tag im Repository gesetzt (falls genutzt)
