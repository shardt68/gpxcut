# Version History

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
