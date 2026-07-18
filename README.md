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
