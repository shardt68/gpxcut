# Feature: Export Selected Segment as GPX

## Overview
Save a selected part of the track (after split or delete operations) as a new, valid GPX file that can be opened in external tools (QGIS, Garmin BaseCamp, etc.).

## Acceptance Criteria
- User selects a track part (or uses result from split/delete)
- File save dialog opens
- User specifies filename and location
- New GPX file created with correct XML structure
- Exported file is valid GPX (validates against schema if needed)
- Opens without errors in external tools
- Metadata, timestamps, elevations preserved
- Extensions preserved (even if not understood by external tool)
- File size reasonable for selected data

## Technical Requirements

### Core (GPX Writing)
- **Streaming XmlWriter** to avoid memory bloat on large segments
  - Write header: `<?xml version="1.0" encoding="UTF-8"?>` + namespaces
  - Write metadata: track name, description, author, bounds, time range (calculated from points)
  - Write segments and points with full precision (lat/lon 8 decimals, elevation, time)
  - Write extensions as-is (raw XML passthrough from original)
  - Write footer and close file
- **Metadata synthesis:**
  - If original track had `<name>`, derive export name from segment info or user input
  - Calculate bounds from exported points (min/max lat/lon)
  - Calculate time range (first to last timestamp)
  - Generate auto-metadata if original missing
- **Integrity checks:**
  - All required GPX fields present (lat, lon at minimum)
  - Coordinates in valid ranges (-90 to 90 lat, -180 to 180 lon)
  - Time values in ISO 8601 format
  - No NaN or Inf values

### App (WPF ViewModel)
- Command: `ExportCommand` → file dialog → async write
- UI state: IsExporting, ProgressPercent, ExportStatus
- Success message: "Exported N points to [filename]"
- Error handling: disk full, permission denied, invalid path

### MapBridge (optional)
- Visual confirmation: highlight exported range on map
- Show export progress as points are written

## Design Notes
- **Do not** modify original track during export
- Export is **read-only** operation; original remains for further editing
- Extensions should be preserved verbatim; if export tool doesn't understand them, that's OK (they won't break)
- Consider compression or format variants in v1.1 (e.g., GPX 1.0 vs 1.1 compatibility)

## Error Handling
- File already exists → prompt overwrite
- Disk full → "Cannot write file: insufficient disk space"
- Invalid path → "Invalid file path"
- Write failure → retry or cancel
- No points to export → "Selected range is empty"

## Test Strategy
- Export 100-point segment → file valid GPX, opens in QGIS
- Export 500k-point segment → file valid, write completes in reasonable time
- Exported file with metadata and extensions → re-import, compare with original (roundtrip test)
- Export segment after split operation → verify split boundary points handled correctly
- Export segment after delete operation → verify remaining points intact
- Large export (1M points) → monitor file size, write time, memory usage
- Verify external tool compatibility (ideally test in Garmin BaseCamp, Google Earth, etc.)

## Validation Script
- Consider adding automated GPX validator to CI/CD (e.g., xmllint or dedicated GPX schema validator)
- Include roundtrip test: original → split → export → re-import → compare
