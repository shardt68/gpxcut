# Reference

## Project Goal

Build a Windows desktop editor for very large GPX tracks with OSM visualization.

## MVP Operations

Each MVP operation has a dedicated feature specification:

1. **[Open GPX](features/open-gpx.md)** – Load GPX file with streaming parser, handle large files without UI freeze
2. **[Display Track on Map](features/display-track.md)** – Render track on OSM with viewport-aware LOD and smooth pan/zoom
3. **[Split Track at Position](features/split-track.md)** – Divide track at user-selected point while preserving metadata
4. **[Delete Selected Range](features/delete-range.md)** – Remove contiguous range of points with segment cleanup
5. **[Export Selected Part as GPX](features/export-segment.md)** – Save segment as valid GPX file compatible with external tools

## Architecture Boundaries

- `src/GpxCut.App`: WPF UI and ViewModels
- `src/GpxCut.Core`: domain model, GPX IO, editing commands
- `src/GpxCut.MapBridge`: WebView2 and MapLibre interop

## Engineering Priorities

1. Data integrity over feature breadth.
2. Responsive UI during long-running work.
3. Streaming/chunk-oriented processing for large tracks.
4. Roundtrip-safe metadata and extension handling.

## Suggested Validation

- Build the solution.
- Run unit tests for Core editing behavior.
- Verify exported GPX opens in external tools.
