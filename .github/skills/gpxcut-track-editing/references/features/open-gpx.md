# Feature: Open GPX

## Overview
Load a GPX file (small or very large, 100k–1M points) into memory with streaming-aware parsing to avoid UI freeze.

## Acceptance Criteria
- File dialog opens and user can select a `.gpx` file
- Parsing begins immediately; progress indicator shown
- Async loading with cancellation support
- Invalid/malformed GPX shows helpful error message (file corruption, encoding, schema)
- Metadata (name, description, time range, bounds) extracted and displayed
- Ready for next operation (display, split, etc.) within target time

## Technical Requirements

### Core (GPX Parsing)
- Implement **streaming XmlReader** in `GpxCut.Core` to avoid loading entire file into DOM
- Chunk large point sets (e.g., 25k points per chunk) for memory efficiency
- Preserve unknown GPX extensions as raw XML for roundtrip safety
- Extract and validate:
  - Track/segment hierarchy
  - Point coordinates (lat/lon required)
  - Timestamp, elevation (optional but preserve if present)
  - Custom extensions

### App (WPF ViewModel)
- Command: `OpenFileCommand` → file picker → async parse
- Store result in `CurrentTrack` ViewModel property
- UI state: `IsLoading`, `ErrorMessage`, `TrackMetadata`
- Cancellation: bind to dialog cancellation or explicit stop button

### Error Handling
- File not found
- Invalid XML structure
- Missing track/segment elements
- Encoding issues → suggest UTF-8 or re-encode
- Out-of-memory for extremely large files → suggest splitting offline first

## Design Notes
- Do **not** parse the entire file synchronously on the UI thread
- Use `CancellationToken` for long-running parses
- Emit progress events for UI feedback (e.g., "Loaded 250,000 of 1,000,000 points")
- Keep parsed domain model (Track, Segment, TrackPoint) separate from file format

## Test Strategy
- Small GPX (10 points) → parse, verify metadata
- Large GPX (500k points) → load in < 10s, verify chunking
- Malformed GPX → error message, no crash
- Cancel during load → cleanup, no dangling resources
