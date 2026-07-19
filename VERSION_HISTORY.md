# Version History

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
