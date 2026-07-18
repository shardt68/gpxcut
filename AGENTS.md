# AGENTS.md

## Projektkontext

Dieses Repository baut einen Windows-Desktop-Editor fuer GPX-Tracks mit OpenStreetMap-Visualisierung.

### Produktziel
- Sehr grosse GPX-Tracks (typisch 500.000 bis 1.000.000 Punkte) laden und visualisieren.
- Komfortables Bearbeiten mit Kernoperationen:
  - Track an Position zerschneiden
  - Bereich markieren und loeschen
  - Teiltrack als neue GPX speichern
- Strikte Erhaltung von GPX-Metadaten (Zeit, Hoehe, Extensions), soweit Punkte unveraendert bleiben.

### Zielplattform
- Nur Windows (MVP und v1)

### Kartenstrategie
- OSM online anzeigen
- Offline-Resilienz via lokalem Tile-Cache (MVP: einfacher HTTP-Cache mit Limit)

## Technologiestack (festgelegt)

- Sprache/Runtime: C# auf .NET 8
- Desktop-UI: WPF (MVVM)
- Karte: WebView2 + MapLibre GL JS + OSM-Tiles
- GPX IO: XML-basiertes Streaming Parsing/Writing in C#
- Tests:
  - Unit-Tests fuer Editing und GPX-Roundtrip
  - Performance-Tests mit grossen Datensaetzen

## Leitplanken und Praemissen

### Performanceziele
- Referenzhardware: Office-Laptop (i5/Ryzen5, 16 GB RAM, integrierte GPU)
- Ladezeit 1.000.000 Punkte: bis 10 Sekunden akzeptabel
- Interaktion: mindestens 30 FPS stabil bei Pan/Zoom (MVP-Ziel)

### MVP-Scope (verbindlich)
- Datei oeffnen (GPX)
- Track auf Karte anzeigen
- Split-Operation
- Bereich loeschen
- Teiltrack exportieren
- Solides Fehlerhandling bei defekten/ungueltigen GPX

### Nicht im MVP (bewusst verschoben nach v1.1)
- Undo/Redo (nur Architektur vorbereiten)
- Punkt-Drag-Editing
- Track-Merge
- Glaettung/Filter
- Zusatzformate (FIT/TCX)
- Komplexe Offline-Packaging-Loesung (MBTiles/PMTiles)

## Architekturprinzipien

### Datenmodell
- Trenne klar zwischen:
  - GPX-Domainmodell (Track/Segment/Punkt/Metadaten)
  - Renderdaten (vereinfachte Geometrien, LOD)
  - Editieroperationen (Command-Objekte)
- Halte unbekannte GPX-Extensions als raw XML-Strukturen, damit Roundtrip verlustarm bleibt.

### Verarbeitung grosser Tracks
- Chunk-basierte Speicherung grosser Punktmengen (z. B. 25k Punkte pro Chunk, spaeter tunebar)
- Segment- und Bereichsindizes fuer schnelle Editierzugriffe
- Hintergrundverarbeitung fuer Parsing und Vorberechnung, UI-Thread bleibt responsiv

### Rendering
- Viewport-basiertes Zeichnen
- Zoomabhaengige Vereinfachung (LOD)
- Inkrementelles Update statt Voll-Redraw bei jeder Interaktion

### Interop C# <-> JS
- Halte Payloads klein und chunked
- Vermeide haeufige Chatty-Calls zwischen .NET und JS
- Definiere stabile DTOs fuer Koordinaten- und Segmentdaten

## Qualitaetsregeln

### Korrektheit
- Jede Editieroperation muss deterministisch und testbar sein.
- Exportierte GPX-Dateien muessen in Dritttools oeffnen (z. B. QGIS, Garmin BaseCamp).

### Stabilitaet
- Fehler werden benutzerverstaendlich gemeldet, nie still geschluckt.
- Grosse Dateien duerfen die UI nicht dauerhaft blockieren.

### Wartbarkeit
- Klare Schichtentrennung:
  - App/UI
  - Core (Domain, IO, Editing)
  - MapBridge (WebView2/JS)
- Keine Businesslogik in XAML Code-Behind ausser UI-nahe Glue-Logik.

## Empfohlene Projektstruktur

- src/GpxCut.App
  - WPF App, Views, ViewModels
- src/GpxCut.Core
  - Domain, GPX IO, Editing Commands, Validierung
- src/GpxCut.MapBridge
  - WebView2 Interop + MapLibre Host Assets
- tests/GpxCut.Core.Tests
  - Unit Tests fuer Core-Logik
- tests/GpxCut.Perf
  - Benchmark-/Performance-Tests
- docs
  - Architektur-, Performance- und User-Dokumentation

## Skills- und Agent-Nutzung im Projekt

Diese Rubrik dokumentiert, wann welcher Skill genutzt werden soll, damit die Agent-Arbeit konsistent bleibt.

### project-setup-info-local
Nutzen wenn:
- komplettes Initial-Scaffolding erzeugt werden soll
- neues Subprojekt (z. B. Testprojekt, Tooling-Workspace) aufgesetzt wird
Nicht nutzen fuer:
- einzelne Datei-Edits

### get-search-view-results
Nutzen wenn:
- VS Code Search View als Quelle fuer bereits gefundene Treffer ausgewertet werden soll

### troubleshoot
Nutzen wenn:
- Verhalten des Chat-Agenten, Tool-Auswahl oder unerwartete Laufzeit analysiert werden muss

### agent-customization
Nutzen wenn:
- Instruktionsdateien fuer Agenten angepasst oder debuggt werden (AGENTS.md, copilot-instructions usw.)

### chronicle
Nutzen wenn:
- Session-Historie, Standup-Auswertungen oder Worklog-Rueckblicke benoetigt werden

## Detaillierter Projektplan (Fastest MVP)

## Phase A: Setup und lauffaehiger Kern (Tag 1-2)

### Ziele
- Projekt aufsetzen und lokal startbar machen
- Karte sichtbar
- Erstes GPX laden und als Linie anzeigen

### Arbeitspakete
1. Solution mit 3 Projekten anlegen (App, Core, MapBridge)
2. WPF MainWindow mit Kartenflaeche und minimaler Toolbar
3. WebView2 integrieren und MapLibre Hostseite laden
4. OSM-Tiles anbinden
5. GPX-Reader im Core implementieren (streaming-orientiert)
6. Basistransfer der Trackpunkte an JS-Renderer

### Abnahme
- App startet
- Beispiel-GPX wird geladen
- Track ist auf OSM sichtbar

## Phase B: Kern-Editing v1 (Tag 3-6)

### Ziele
- Drei Kernoperationen Ende-zu-Ende

### Arbeitspakete
1. SplitTrackCommand implementieren
2. DeleteRangeCommand implementieren
3. SaveSegmentCommand implementieren
4. UI-Interaktionen fuer Auswahl/Markierung/Splitpunkt
5. GPX-Writer mit Metadaten-Erhalt fuer unveraenderte Punkte
6. Grundlegende Validierung der Eingaben (ungueltige Bereichsgrenzen etc.)

### Abnahme
- Nutzer kann auf grossen Tracks splitten, loeschen, teil-exportieren
- Exportdateien sind gueltig und in Fremdtools lesbar

## Phase C: Performance-Baseline (Tag 7-9)

### Ziele
- Grossdateien robust und fluessig nutzbar

### Arbeitspakete
1. Chunking im Core finalisieren
2. Viewport-Only Rendering im JS-Renderer
3. Einfache LOD-Regeln je Zoomstufe
4. Hintergrund-Tasks fuer Laden/Vorberechnung
5. Erster Tile-Cache mit Speicherlimit

### Abnahme
- 1M Punkte laden in <= 10s auf Referenzhardware (Ziel)
- Pan/Zoom subjektiv fluessig, Ziel >= 30 FPS

## Phase D: Stabilisierung und Release Prep (Tag 10-12)

### Ziele
- Fehlerfaelle abfangen
- Release-Kandidat erstellen

### Arbeitspakete
1. Fehlerpfade: defekte GPX, Leerdaten, Abbruch waehrend Laden
2. Logging verbessern (Fehlercodes + Kontext)
3. Unit-Tests fuer Editing-Kommandos
4. Roundtrip-Tests fuer Metadaten/Extensions
5. Packaging fuer Windows (Installer)

### Abnahme
- Kein kritischer Crash in Kernworkflow
- Installierbares Paket vorhanden

## Phase E: Puffertage und Feinschliff (Tag 13-14)

### Ziele
- Restarbeiten schliessen
- Dokumentation vervollstaendigen

### Arbeitspakete
1. UI-Feinschliff (Statusanzeigen, Progress, Fehlermeldungen)
2. Kurze User-Doku (Laden, Split, Loeschen, Export)
3. Technische Doku (Architektur + bekannte Limits)
4. Release-Checkliste durchgehen

### Abnahme
- MVP intern releasefaehig

## Test- und Messstrategie

### Funktionaltests
- GPX mit mehreren Segmenten
- Split am Anfang/Mitte/Ende
- Bereichsloeschung ueber Segmentgrenzen
- Teiltrack-Export nach mehreren Operationen

### Integritaetstests
- GPX Roundtrip mit Zeit/Hoehe/Extensions
- Vergleich Original vs. Export fuer unveraenderte Punkte

### Performance-Tests
- Datensaetze: 100k, 500k, 1M Punkte
- Messwerte:
  - Parsezeit
  - Zeit bis erste Visualisierung
  - Interaktionsfluessigkeit beim Pan/Zoom
  - Speicherverbrauch Peak/steady

## Risiko- und Gegenmassnahmen

1. Risiko: WebView2-Bridge wird bei 1M Punkten zum Flaschenhals
- Gegenmassnahme: chunked Transfer, vereinfachte Geometrie, nur sichtbare Daten

2. Risiko: GPX-Extensions gehen beim Schreiben verloren
- Gegenmassnahme: raw XML passthrough + Roundtrip-Tests frueh aufsetzen

3. Risiko: UI freeze bei langen Operationen
- Gegenmassnahme: async pipelines, CancellationToken, Fortschrittsanzeige

4. Risiko: OSM-Tile-Nutzungslimits
- Gegenmassnahme: Caching, spaeter eigene Tile-Infrastruktur pruefen

## v1.1 Backlog (vorpriorisiert)

1. Undo/Redo als vollwertiges Command-History-System
2. Erweiterte Renderoptimierung und tieferes Profiling
3. Erweiterter Offline-Modus (z. B. MBTiles/PMTiles)
4. Zusatzformate FIT/TCX
5. Erweiterte Editierwerkzeuge (Merge, Punktbearbeitung, Filter)

## Arbeitsmodus fuer Agenten im Repo

- Erst Verstaendnis und Scope pruefen, dann implementieren.
- Kleine, gezielte Commits pro Arbeitspaket.
- Nach jedem groesseren Schritt Build + Tests laufen lassen.
- Bei Zielkonflikten hat Datenintegritaet Vorrang vor Zusatzfeatures.
- Bei Unsicherheit ueber Metadatenverhalten konservativ handeln und explizit dokumentieren.
