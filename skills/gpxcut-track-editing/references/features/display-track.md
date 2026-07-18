# Feature: Display Track on Map

## Overview
Render the loaded GPX track on an interactive OSM map using WebView2 + MapLibre GL JS with viewport-aware LOD (level-of-detail) simplification.

## Acceptance Criteria
- Track appears as a polyline on the OSM map
- Map pans and zooms smoothly (target ≥30 FPS)
- Track bounds auto-fitted on first load
- Large tracks (1M points) respond without UI freeze
- Zoom-dependent detail: full resolution when zoomed in, simplified when zoomed out
- Track color/style consistent and visible over map tiles

## Technical Requirements

### MapBridge (C# Interop Layer)
- **Render pipeline:**
  - Receive Track/Segment data from Core
  - Chunk data for transfer (e.g., 10k points per payload to avoid bridge saturation)
  - Calculate viewport bounds for current zoom level
  - Send only visible/near-visible points to JS
- **LOD Strategy:**
  - Zoom < 5: sample 1 in 20 points
  - Zoom 5–10: sample 1 in 5 points
  - Zoom > 10: full resolution
  - Dynamically rebuild simplified geometry on zoom changes

### JS/MapLibre (webview Host)
- Initialize map at track center
- Add GeoJSON layer for track polyline
- Listen to map events: `move`, `zoom` → notify C# of viewport changes
- Render track updates incrementally (don't redraw entire map)
- Style: track color (#FF6B6B), stroke width (3px), opacity (0.8)

### Performance Targets
- 1M point track: initial load < 10s
- Pan/zoom latency < 200ms
- Frame rate ≥30 FPS during interaction

## Architecture Notes
- Do **not** send all 1M points at once to JS
- Keep bridge communication event-driven (viewport-change → send visible chunk)
- Maintain a segment index in Core for fast "points in viewport" lookup
- Consider quadtree or grid for spatial partitioning if needed later

## Error Handling
- Empty track → show message "No track points to display"
- Invalid bounds → fallback to world view
- Tile server unavailable → show cached tiles or blank map (graceful degradation)

## Test Strategy
- Small track (10 points) → appears on map, bounds correct
- Large track (500k points) → renders, zoom/pan responsive
- Extreme zoom in/out → no visual artifacts, responsive
- Verify bridge transfer payload sizes (should be chunked, not monolithic)
