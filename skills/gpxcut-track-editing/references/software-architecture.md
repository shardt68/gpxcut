# Software Architecture for gpxcut

## Scope

This architecture targets the MVP and near-term v1.1 roadmap for a Windows-only GPX editor that must handle very large tracks (up to about 1,000,000 points) with stable interactivity.

## Architectural Style

Use a layered, hexagonal-inspired architecture with clear bounded responsibilities:

1. Presentation Layer (`GpxCut.App`)
2. Application Layer (`GpxCut.Core.Application` namespace)
3. Domain Layer (`GpxCut.Core.Domain` namespace)
4. Infrastructure Layer (`GpxCut.Core.Infrastructure` + `GpxCut.MapBridge`)

The effective project boundaries remain:

- `src/GpxCut.App` for UI and orchestration entry points
- `src/GpxCut.Core` for domain logic, use cases, and GPX IO services
- `src/GpxCut.MapBridge` for map host and WebView2 interop

## Why this architecture fits

- Data integrity first: domain rules are isolated from UI and map technology.
- Performance under large datasets: parsing, indexing, and editing are stream/chunk oriented.
- Responsive UX: long-running work is asynchronous and cancellable.
- Evolvability: undo/redo and additional formats can be added without rewriting UI.

## Quality Attribute Priorities

1. Correctness and deterministic editing
2. Data roundtrip integrity (metadata and unknown extensions)
3. Runtime performance (load time and interactive rendering)
4. Reliability and graceful error handling
5. Maintainability via strict separation of concerns

## Component Model

### 1) Presentation Layer (`GpxCut.App`)

Responsibilities:

- WPF Views and ViewModels (MVVM)
- User interaction state machines (select split point, select delete range)
- Progress, cancellation, and error presentation
- Calls application use cases only (no business logic in code-behind)

Key contracts:

- `ITrackEditingUseCases`
- `ITrackQueryService`
- `IMapViewportService`

### 2) Application Layer (`GpxCut.Core.Application`)

Responsibilities:

- Orchestrate use cases: open, display-prep, split, delete, export
- Transaction-like command execution for edits
- Map domain objects to DTOs for App and MapBridge
- Coordinate cancellation, validation, and logging scopes

Key patterns:

- Use-case services (`OpenGpxUseCase`, `SplitTrackUseCase`, etc.)
- Command results with explicit success/failure
- Pipeline behaviors for validation and timing

### 3) Domain Layer (`GpxCut.Core.Domain`)

Responsibilities:

- Core entities/value objects: Track, Segment, TrackPoint, Metadata, ExtensionPayload
- Editing commands and rules (deterministic, side-effect controlled)
- Invariants (coordinate bounds, point ordering, non-empty constraints where required)

Rules:

- No dependency on WPF, WebView2, MapLibre, or file system
- Edit commands return new immutable snapshots or copy-on-write structures

### 4) Infrastructure Layer

#### `GpxCut.Core.Infrastructure`

Responsibilities:

- GPX streaming reader/writer (`XmlReader`/`XmlWriter`)
- Raw extension passthrough handling
- Optional local tile/http cache support contracts
- Repository-like adapters for persisted documents

#### `GpxCut.MapBridge`

Responsibilities:

- WebView2 host bootstrap
- C# <-> JS message bridge with chunked payloads
- Viewport event handling and LOD render update triggers

Rules:

- No domain decisions in JS bridge; only rendering and interaction telemetry

## Data and Processing Architecture

### GPX Ingestion

- Stream parse input GPX file, never DOM-load complete large files.
- Build chunked point storage (target chunk size configurable, start with 25k).
- Keep unknown extensions as raw XML fragments bound to point/segment scope.

### Editing Model

- Represent edits as explicit commands:
  - `SplitTrackCommand`
  - `DeleteRangeCommand`
  - `ExportSegmentCommand` (read-only extraction + write)
- Commands operate on indexed track representation.
- Deterministic boundary policy must be explicit (for example: start inclusive, end exclusive for range operations).

### Query and Indexing

- Maintain segment index and point offset index for fast nearest-point and range lookup.
- Add optional spatial index abstraction (`ISpatialIndex`) for viewport and nearest searches.
- Defer heavy index builds until needed, but cache after first build.

### Rendering Pipeline

- App requests render DTOs from Core query services.
- MapBridge sends only viewport-relevant, LOD-filtered chunks to JS.
- JS updates existing GeoJSON sources incrementally.

## Concurrency and Threading

- UI thread: View + command trigger only.
- Background workers: parse, index build, heavy edit computation, export write.
- Every long-running use case accepts `CancellationToken`.
- Marshal minimal state back to UI thread.

## Error Handling and Observability

- Structured error model with categories:
  - Validation
  - Parsing
  - IO
  - Interop
  - Unexpected
- User-friendly messages at App layer, detailed diagnostics in logs.
- Add operation metrics:
  - parse duration
  - points loaded
  - edit latency
  - render payload size

## Recommended Internal Namespaces

Inside `GpxCut.Core`:

- `GpxCut.Core.Domain`
- `GpxCut.Core.Application`
- `GpxCut.Core.Infrastructure.Gpx`
- `GpxCut.Core.Infrastructure.Indexing`
- `GpxCut.Core.Contracts`

Inside `GpxCut.MapBridge`:

- `GpxCut.MapBridge.Hosting`
- `GpxCut.MapBridge.Interop`
- `GpxCut.MapBridge.Dto`

## MVP Sequence Overview

1. Open file in App
2. Run `OpenGpxUseCase` (stream parse + chunk store + metadata)
3. Build minimal initial viewport render payload
4. Send payload via MapBridge to MapLibre
5. User executes split/delete
6. Use case applies command in Core and returns updated track snapshot
7. App refreshes map with incremental viewport payload
8. Export writes selected track subset with streaming writer

## Test Architecture

### Unit tests (`tests/GpxCut.Core.Tests`)

- Domain invariants
- Split/Delete deterministic behavior
- Metadata/extension preservation

### Integration tests (`tests/GpxCut.Core.Tests` or separate project later)

- Parse -> edit -> export -> re-import roundtrip
- Error-path behavior for malformed GPX

### Performance tests (`tests/GpxCut.Perf`)

- 100k, 500k, 1M point datasets
- Parse time, memory peak, command latency, payload volume

## Evolution Path (v1.1+)

Designed extension points:

- Undo/Redo via command history over immutable snapshots
- Additional formats (FIT/TCX) via new infrastructure adapters
- Offline map improvements via advanced tile cache provider
- More advanced spatial index implementation without UI changes

## Architecture Decision Summary

For this product profile, the best fit is:

- Layered architecture with hexagonal boundaries
- Command-oriented editing in domain/application core
- Streaming IO + chunked data + indexed queries
- Thin UI and thin map bridge around a strong deterministic core

This combination directly supports the most critical constraints: very large files, responsive map interaction, and metadata-safe GPX roundtrip behavior.