# gpxcut

Ein Windows-Desktop-Editor zum Laden, Visualisieren und Bearbeiten sehr grosser GPX-Tracks.

## Projektueberblick

`gpxcut` richtet sich an Anwender, die lange GPS-Aufzeichnungen (z. B. 500.000 bis 1.000.000 Punkte)
zuverlaessig bearbeiten wollen, ohne Metadaten zu verlieren.

Der Fokus liegt auf einem performanten Workflow fuer drei Kernoperationen:

- Track an einer Position splitten
- Bereich markieren und loeschen
- Teiltrack als neue GPX-Datei exportieren

## Ziele fuer das MVP

- GPX-Datei oeffnen und auf einer OSM-Karte anzeigen
- Kernoperationen (Split, Delete Range, Export Segment) Ende-zu-Ende nutzbar
- Robustes Fehlerhandling bei ungueltigen oder defekten GPX-Dateien
- Erhalt von Zeit, Hoehe und GPX-Extensions fuer unveraenderte Punkte

## Technischer Ansatz

- Plattform: Windows
- Sprache/Runtime: C# auf .NET 8
- UI: WPF (MVVM)
- Karte: WebView2 + MapLibre GL JS + OSM-Tiles
- GPX-IO: XML-Streaming fuer grosse Dateien

## Status

Fruehe Projektphase: Architektur, Basisstruktur und MVP-Implementierung werden aufgebaut.

## Entwicklungsumgebung

Eine Schritt-fuer-Schritt Anleitung fuer Installation, Verifikation und Troubleshooting findest du in [DEVELOPMENT_SETUP.md](DEVELOPMENT_SETUP.md).

## Lokal bauen und starten

Voraussetzungen:

- .NET SDK 8
- PowerShell 7+
- Microsoft Edge WebView2 Runtime

Manuell:

```powershell
dotnet build -c Debug
dotnet test -c Debug --no-build
dotnet run --project src/GpxCut.App/GpxCut.App.csproj -c Debug
```

Mit Skripten:

```powershell
pwsh ./scripts/dev/build-debug.ps1
pwsh ./scripts/dev/test-debug.ps1
pwsh ./scripts/dev/run-app.ps1
pwsh ./scripts/dev/start-dev.ps1
```

All-in-One (Build + Tests + Start):

```powershell
pwsh ./scripts/dev/start-dev.ps1
```

Optionen:

```powershell
pwsh ./scripts/dev/start-dev.ps1 -SkipTests
pwsh ./scripts/dev/start-dev.ps1 -SkipBuild
```

Kombinierte Validierung (Build + Tests):

```powershell
pwsh ./skills/gpxcut-track-editing/scripts/validate-solution.ps1
```

## Windows Release (WIP)

Erste Skripte fuer reproduzierbare Windows-Artefakte sind vorhanden.

```powershell
pwsh ./scripts/release/publish-gpxcut.ps1
pwsh ./scripts/release/create-installer-layout.ps1
pwsh ./installer/wix/build-msi.ps1 -Version 0.1.0
```

Release-Regel:

- Interner Build ohne Zertifikat: Standard-Aufruf oben
- Externer Release: mit `-SignArtifacts $true` bauen
- Optional hart erzwingen: `-StrictRelease $true` (blockiert unsignierte Builds)

Vollstaendige Schritt-fuer-Schritt Doku steht in [installer/wix/README.md](installer/wix/README.md).

Eine komplette Freigabe-Checkliste steht in [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).
