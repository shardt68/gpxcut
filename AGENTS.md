# AGENTS.md

## Project Context

This repository builds a Windows desktop editor for GPX tracks with OpenStreetMap visualization.

### Product Goal
- Load and visualize very large GPX tracks (typically 500,000 to 1,000,000 points).
- Enable convenient editing with core operations:
  - Split track at a position
  - Mark and delete a range
  - Save a partial track as a new GPX
- Strictly preserve GPX metadata (time, elevation, extensions) where points remain unchanged.

### Target Platform
- Windows only (MVP and v1)

### Map Strategy
- Display OSM online
- Offline resilience through a local tile cache (MVP: simple HTTP cache with limits)

## Technology Stack (Fixed)

- Language/Runtime: C# on .NET 8
- Desktop UI: WPF (MVVM)
- Map: WebView2 + MapLibre GL JS + OSM tiles
- GPX IO: XML-based streaming parsing/writing in C#
- Tests:
  - Unit tests for editing and GPX roundtrip
  - Performance tests with large datasets

## Guardrails and Assumptions

### Performance Goals
- Reference hardware: office laptop (i5/Ryzen5, 16 GB RAM, integrated GPU)
- Load time for 1,000,000 points: up to 10 seconds is acceptable
- Interaction: at least stable 30 FPS during pan/zoom (MVP target)

### MVP Scope (Binding)
- Open file (GPX)
- Display track on map
- Split operation
- Delete range
- Export partial track
- Solid error handling for broken/invalid GPX

### Not in MVP (Intentionally Moved to v1.1)
- Undo/Redo (prepare architecture only)
- Point drag editing
- Track merge
- Smoothing/filtering
- Additional formats (FIT/TCX)
- Complex offline packaging solution (MBTiles/PMTiles)

## Architecture Principles

### Data Model
- Clearly separate:
  - GPX domain model (track/segment/point/metadata)
  - Render data (simplified geometries, LOD)
  - Editing operations (command objects)
- Keep unknown GPX extensions as raw XML structures so roundtrip remains low-loss.

### Processing Large Tracks
- Chunk-based storage of large point sets (for example, 25k points per chunk, tunable later)
- Segment and range indexes for fast editing access
- Background processing for parsing and precomputation, while the UI thread stays responsive

### Rendering
- Viewport-based drawing
- Zoom-dependent simplification (LOD)
- Incremental updates instead of full redraw on every interaction

### Interop C# <-> JS
- Keep payloads small and chunked
- Avoid frequent chatty calls between .NET and JS
- Define stable DTOs for coordinate and segment data

## Quality Rules

### Correctness
- Every edit operation must be deterministic and testable.
- Exported GPX files must open in third-party tools (for example, QGIS, Garmin BaseCamp).

### Stability
- Errors are reported in a user-understandable way and are never silently swallowed.
- Large files must not permanently block the UI.

### Maintainability
- Clear layer separation:
  - App/UI
  - Core (domain, IO, editing)
  - MapBridge (WebView2/JS)
- No business logic in XAML code-behind except UI-adjacent glue logic.

## Recommended Project Structure

- src/GpxCut.App
  - WPF app, views, view models
- src/GpxCut.Core
  - Domain, GPX IO, editing commands, validation
- src/GpxCut.MapBridge
  - WebView2 interop + MapLibre host assets
- tests/GpxCut.Core.Tests
  - Unit tests for core logic
- tests/GpxCut.Perf
  - Benchmark/performance tests
- docs
  - Architecture, performance, and user documentation

## Skill and Agent Usage in the Project

This section documents when each skill should be used to keep agent work consistent.

### project-setup-info-local
Use when:
- complete initial scaffolding should be generated
- a new subproject (for example, test project, tooling workspace) should be set up
Do not use for:
- single-file edits

### get-search-view-results
Use when:
- VS Code Search view should be used as a source for evaluating already-found matches

### troubleshoot
Use when:
- chat agent behavior, tool selection, or unexpected runtime behavior must be analyzed

### agent-customization
Use when:
- instruction files for agents should be adapted or debugged (AGENTS.md, copilot-instructions, and similar)

### chronicle
Use when:
- session history, standup summaries, or worklog retrospectives are needed

## Detailed Project Plan (Fastest MVP)

## Phase A: Setup and Runnable Core (Day 1-2)

### Goals
- Set up project and make it runnable locally
- Make map visible
- Load first GPX and display as a line

### Work Packages
1. Create solution with 3 projects (App, Core, MapBridge)
2. Create WPF MainWindow with map area and minimal toolbar
3. Integrate WebView2 and load MapLibre host page
4. Connect OSM tiles
5. Implement GPX reader in Core (streaming-oriented)
6. Basic transfer of track points to JS renderer

### Acceptance
- App starts
- Sample GPX loads
- Track is visible on OSM

## Phase B: Core Editing v1 (Day 3-6)

### Goals
- Three core operations working end to end

### Work Packages
1. Implement SplitTrackCommand
2. Implement DeleteRangeCommand
3. Implement SaveSegmentCommand
4. Add UI interactions for selection/marking/split point
5. Implement GPX writer with metadata preservation for unchanged points
6. Add basic input validation (invalid range boundaries, etc.)

### Acceptance
- User can split, delete, and export partial tracks on large datasets
- Export files are valid and readable in third-party tools

## Phase C: Performance Baseline (Day 7-9)

### Goals
- Make large files robust and smooth to use

### Work Packages
1. Finalize chunking in Core
2. Implement viewport-only rendering in JS renderer
3. Add simple LOD rules by zoom level
4. Add background tasks for loading/precomputation
5. Add first tile cache with storage limit

### Acceptance
- Load 1M points in <= 10s on reference hardware (target)
- Pan/zoom feels smooth, target >= 30 FPS

## Phase D: Stabilization and Release Prep (Day 10-12)

### Goals
- Handle error cases
- Create release candidate

### Work Packages
1. Error paths: broken GPX, empty data, cancellation during loading
2. Improve logging (error codes + context)
3. Add unit tests for editing commands
4. Add roundtrip tests for metadata/extensions
5. Add Windows packaging (installer)

### Acceptance
- No critical crash in core workflow
- Installable package is available

## Phase E: Buffer Days and Polish (Day 13-14)

### Goals
- Close remaining tasks
- Complete documentation

### Work Packages
1. UI polish (status indicators, progress, error messages)
2. Short user documentation (load, split, delete, export)
3. Technical documentation (architecture + known limits)
4. Review release checklist

### Acceptance
- MVP is internally release-ready

## Test and Measurement Strategy

### Functional Tests
- GPX with multiple segments
- Split at beginning/middle/end
- Range deletion across segment boundaries
- Partial track export after multiple operations

### Integrity Tests
- GPX roundtrip with time/elevation/extensions
- Compare original vs. export for unchanged points

### Performance Tests
- Datasets: 100k, 500k, 1M points
- Metrics:
  - Parse time
  - Time to first visualization
  - Interaction smoothness during pan/zoom
  - Memory usage peak/steady

## Risks and Countermeasures

1. Risk: WebView2 bridge becomes a bottleneck at 1M points
- Countermeasure: chunked transfer, simplified geometry, visible data only

2. Risk: GPX extensions are lost during writing
- Countermeasure: raw XML passthrough + early roundtrip tests

3. Risk: UI freezes during long operations
- Countermeasure: async pipelines, CancellationToken, progress indicators

4. Risk: OSM tile usage limits
- Countermeasure: caching, evaluate own tile infrastructure later

## v1.1 Backlog (Pre-Prioritized)

1. Undo/Redo as a full command history system
2. Advanced rendering optimization and deeper profiling
3. Extended offline mode (for example, MBTiles/PMTiles)
4. Additional formats FIT/TCX
5. Advanced editing tools (merge, point editing, filters)

## Working Mode for Agents in This Repo

- First verify understanding and scope, then implement.
- Make small, focused commits per work package.
- Run build + tests after each larger step.
- If goals conflict, data integrity has priority over extra features.
- If metadata behavior is uncertain, act conservatively and document explicitly.
