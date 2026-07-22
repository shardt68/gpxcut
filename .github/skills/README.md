# Agent Skills

This directory contains Agent Skills for the GpxCut project, following the specification:
https://agentskills.io/specification

Skills are centralized here in `.github/skills/` for team visibility and knowledge sharing.

## Structure

Each skill lives in its own folder and must contain at least `SKILL.md`.

Example:

- `.github/skills/<skill-name>/SKILL.md`
- `.github/skills/<skill-name>/scripts/` (optional)
- `.github/skills/<skill-name>/references/` (optional)
- `.github/skills/<skill-name>/assets/` (optional)

## Available Skills

- **gpxcut-track-editing/** - GPX track manipulation, metadata preservation, rendering strategy
- **gpxcut-map-source-access/** - Access patterns and integration workflow for map data sources (XYZ, WMTS-derived, WMS, ArcGIS REST)

## Validation

Use the reference validator:

```bash
skills-ref validate ./.github/skills/<skill-name>
```

## Documentation Language Policy

- The documentation language for all project docs and skill docs is English.
- New or updated Markdown documentation under `.github/skills/` must be written in English.
- If existing documentation is in another language, translate it to English when touching it.

## Knowledge Management

For detailed guidance on what goes into skills vs. other documentation, see:
- [`../../copilot-instructions.md`](../../copilot-instructions.md) - Agent & Copilot conventions
- [`../DEVELOPMENT.md`](../DEVELOPMENT.md) - Development & release procedures
