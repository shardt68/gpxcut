---
name: gpxcut-track-editing
description: Guide an agent to work on GPX track loading, visualization, split, range deletion, and segment export for the gpxcut Windows desktop app. Use when implementing or reviewing Core, App, or MapBridge work for the MVP scope.
license: MIT
compatibility: Windows-focused .NET 8/WPF project; may require dotnet SDK and internet access for docs.
metadata:
  author: gpxcut
  version: "0.1.0"
---

# Gpxcut Track Editing Skill

Use this skill for implementation and review tasks around the gpxcut MVP.

## Activation Cues

Use this skill when the task mentions one or more of:

- GPX parsing or writing
- split track operations
- delete range operations
- export selected segment
- preserving timestamps, elevation, or GPX extensions
- performance with very large tracks (100k to 1M points)

## Workflow

1. Confirm scope and map the request to one of the project layers:
   - App (WPF/MVVM)
   - Core (domain, IO, editing commands)
   - MapBridge (WebView2 and JS interop)
2. Keep business logic out of UI code-behind except UI glue.
3. Prefer deterministic command-like editing behavior in Core.
4. Preserve unmodified GPX metadata and unknown extensions.
5. For large files, prefer streaming/chunk-aware approaches.
6. After changes, run build and tests relevant to touched areas.
7. Keep all project documentation updates in English.

## Quality Gates

- Correctness: operations are deterministic and testable.
- Stability: errors are surfaced with actionable messages.
- Performance: avoid full redraws and chatty C#-JS bridging.
- Integrity: exported GPX should remain valid in third-party tools.
- Documentation: all added or edited documentation is written in English.

## References

See [project reference](references/REFERENCE.md) for overall architecture.

- [Software Architecture](references/software-architecture.md)

**Feature specifications** (detailed acceptance criteria, technical requirements, test strategy):
- [Open GPX](references/features/open-gpx.md)
- [Display Track on Map](references/features/display-track.md)
- [Split Track at Position](references/features/split-track.md)
- [Delete Selected Range](references/features/delete-range.md)
- [Export Selected Part as GPX](references/features/export-segment.md)

## Scripts

Optional helper script examples live in:

- scripts/validate-solution.ps1
