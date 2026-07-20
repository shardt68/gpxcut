# Version History

## 0.1.17 - 2026-07-20

### Type
- Bug fix + Infrastructure

### Added
- Performance testing infrastructure for large datasets (100k-1M points)
  - `TestGpxGenerator` for synthetic GPX generation with configurable point counts
  - `dotnet run --project tests/GpxCut.Perf -- generate <points> <path>` command
  - Benchmark command for parse time and script generation analysis
  - Automated `benchmark-performance.ps1` script with quick/full/clean modes
- Performance test files and documentation in [scripts/dev/README.md](scripts/dev/README.md)

### Fixed
- **WebView2 process crash on 1M+ points (OOM in JavaScript)**
  - Root cause: `addTrackChunk()` was calling `setCoordinates()` on every chunk, causing O(n²) temporary arrays in JavaScript and 100 redundant MapLibre renders
  - Solution: Accumulate chunks with `Array.push`, render once via `flushTrackChunks()`
  - Result: 1M point files now load without crashes
- **WebView2 timeout errors during Delete/Zoom operations**
  - Progressive rendering now automatically activates for all script batches >20 scripts
  - Executes scripts in batches of 10 with 10ms delays to prevent timeout
  - Applies to track rendering, selection updates, profile updates, and all map operations

### Changed
- `ExecuteScriptsAsync()` now intelligently switches between direct and progressive rendering
- All script executions go through automatic threshold detection (no manual checks needed)

### Performance Baseline (Debug)
- 100k points: 1.7s load + render
- 500k points: 4.4s load + render  
- 1M points: 7.5s load + render (all within MVP 10s target)

### Notes
- Test GPX files are deterministic (seed=42) for reproducible testing
- Progressive rendering uses 10ms delays to allow WebView2 event processing
- Small datasets (<20 scripts) use direct rendering for minimal latency

## 0.1.16 - 2026-07-19

### Type
- Feature

### Added
- Neue Profilmodi fuer Geschwindigkeit: Speed over Time und Speed over Distance.
- Dynamische Profilachsen-Beschriftung fuer Hoehe/Geschwindigkeit und Zeit/Distanz.

### Changed
- Geschwindigkeitsprofil wird aus Distanz und Zeitdifferenz (Haversine-basiert) berechnet.
- Profil-Rendering im Map-Canvas auf modusabhaengige Achsen-/Werteformatierung erweitert.

### Notes
- Geschwindigkeitsdaten werden als Rohdaten ohne Glaettung dargestellt.

## 0.1.15 - 2026-07-19

### Type
- Feature

### Added
- Optionales Hoehenprofil als einblendbares Panel unter der Karte.
- Zwei Profilmodi: Hoehe ueber Zeit und Hoehe ueber Strecke.
- Bidirektionale Synchronisierung der Auswahl zwischen Karte und Diagramm.

### Changed
- Erweiterte WebView2-Bridge fuer Profilsteuerung (Sichtbarkeit, Daten, Selektion).
- Map-Host um Profil-Canvas und Renderinglogik erweitert.

### Notes
- Profil ist standardmaessig ausgeblendet und kann bei geladenem Track aktiviert werden.

## 0.1.14 - 2026-07-19

### Type
- Bug fix

### Fixed
- "Open with" file association now correctly loads the GPX file on startup. Previously, the file was silently discarded because `OpenTrackFileAsync` was called before the map was fully initialized and the `map-ready` signal was received.

### Changed
- Deferred file loading to the `map-ready` event handler to ensure map initialization completes before loading the GPX file.
- Updated GitHub Actions workflow to use softprops/action-gh-release@v3.

## 0.1.1 - 2026-07-19

### Type
- Process and release automation documentation update

### Added
- GitHub release workflow configuration at `.github/workflows/release.yml`.
- Required release flow documentation in `RELEASE_CHECKLIST.md`.
- Skill-level release requirement documentation in `skills/README.md`.
- Mandatory release execution steps in `skills/gpxcut-track-editing/SKILL.md`.

### Release Flow (Required)
1. Commit changes.
2. Merge to `master`.
3. Tag merged commit with `v*` (example: `v0.1.1`).
4. Push tag to trigger GitHub Actions release workflow.
5. Download MSI from GitHub Release assets.
