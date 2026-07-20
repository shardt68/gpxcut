# Feature: Delete Selected Range

## Overview
Mark a contiguous range of trackpoints (start and end coordinate, or time range) for deletion, then remove them while preserving the integrity of the remaining track and all metadata.

## Acceptance Criteria
- User selects two points on map (or enters range coordinates) to mark deletion boundaries
- System highlights the range visually
- Deletion removes all points within range (including boundary points or excluding, per rule)
- Remaining track is valid and contiguous
- Segment structure updated: empty segments removed, remaining segments reindexed
- Metadata preserved on remaining points
- User can undo or try different range

## Technical Requirements

### Core (Editing Command)
- **Command class:** `DeleteRangeCommand`
  - Input: Track, start coordinate/index, end coordinate/index
  - Logic:
    1. Find nearest trackpoints to start and end coords
    2. Validate range (start < end, not entire track)
    3. Collect all points in range [start, end]
    4. Remove points from segments
    5. Cleanup empty segments
    6. Reindex segment positions and metadata
    7. Return `DeleteResult { ModifiedTrack, DeletedPoints, DeletedCount }`
- **Handling split segments:**
  - If deletion spans segment boundary, intelligently merge/update segments
  - Preserve time continuity in remaining points
  - If deletion removes all points in a segment, drop the segment

### App (WPF ViewModel)
- UI states:
  - Normal → SelectingStart → SelectingEnd → Confirming → Deleted
- Show range highlight on map (shade or outline)
- Confirmation dialog: "Delete X points from [time1] to [time2]?"
- Provide before/after statistics (original vs. remaining)
- Command: `DeleteCommand` → execute

### MapBridge (Visual Feedback)
- Draw start and end markers (different colors: blue for start, red for end)
- Shade/highlight the deletion range on the map
- Show count of points to be deleted
- Update highlight as user adjusts end point

## Design Notes
- Range boundaries are **inclusive** or **exclusive**? Define clearly in spec (suggest: inclusive start, exclusive end, like Python slicing)
- Deletion must not break segment time ordering (if time is monotonic)
- Extensions on deleted points are discarded (they belong to those points)
- Consider whether deletion can span multiple segments or only within one

## Error Handling
- Start and end points are the same → reject "Range too small"
- Range covers entire track → reject "Cannot delete entire track"
- Invalid coordinates → validation before deletion
- Start > end → auto-swap or reject

## Test Strategy
- Delete middle section of track → remaining points are contiguous, time ordering preserved
- Delete across segment boundaries → segments correctly merged/updated
- Delete at track start/end (but not entire) → edge case handling
- Large track delete (100k points from 1M) → command completes in < 1s
- Roundtrip: delete range → export → re-import → verify geometry and metadata
- Metadata on remaining points: elevation, time, extensions all preserved
