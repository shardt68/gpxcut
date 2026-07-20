# Feature: Split Track at Position

## Overview
Divide a track into two parts at a user-selected point or coordinate, creating two separate segments while preserving all metadata and extensions on unmodified points.

## Acceptance Criteria
- User clicks on map to select split point (or enters coordinate)
- System finds nearest track point to split position
- Track splits into Part A (before) and Part B (after)
- Both parts remain valid GPX structures with original metadata
- Split point itself assigned to one part (deterministic rule)
- User can view, export, or edit both parts independently
- Undo/redo infrastructure prepared (not implemented in MVP)

## Technical Requirements

### Core (Editing Command)
- **Command class:** `SplitTrackCommand` (idempotent, reversible pattern)
  - Input: Track, split coordinate (or index)
  - Logic:
    1. Find nearest trackpoint to split coord within tolerance (e.g., 10m)
    2. Validate split point exists and is not at segment boundary
    3. Create Part A: all segments/points up to split point
    4. Create Part B: split point onwards
    5. Preserve segment structure and metadata
    6. Return `SplitResult { PartA, PartB }`
- **Metadata preservation:**
  - Copy original segment times, elevations, extensions to new segments
  - Mark extension data as "inherited from original" for audit trails
  - If extensions reference specific points, adjust indices

### App (WPF ViewModel / UI)
- UI interaction: map click → show split marker on map
- Confirmation dialog: show split point details (lat/lon, closest timestamp, distance)
- Command: `SplitCommand` → execute → show both parts in sidebar
- Allow user to undo or retry split
- State machine: Normal → SelectingSplit → Confirming → Split

### MapBridge (Visual Feedback)
- Draw split marker at clicked position
- Show distance to nearest actual trackpoint
- Highlight selected trackpoint on map
- Preview line split (optional: show both resulting paths)

## Design Notes
- Tolerance for "nearest point" must be configurable (user feedback)
- Split at exact segment boundary is a special case (might reject or split within segment)
- Store original track reference for undo/redo (future)
- **Do not modify** the Track object in-place; return new instances

## Error Handling
- Split point too close to track start/end → reject with "Cannot split at boundary"
- No trackpoints within tolerance → show "No trackpoints near split location"
- Invalid coordinate → validation before split attempt

## Test Strategy
- Split a 3-segment track in the middle → verify 2 parts have correct geometry and time ranges
- Split at segment boundary → deterministic behavior (e.g., always includes boundary point in Part B)
- Split with metadata (elevation, time) → verify preservation in both parts
- Large track (500k points) split → command completes in < 1s
- Roundtrip: split → export both parts → re-import → verify integrity
