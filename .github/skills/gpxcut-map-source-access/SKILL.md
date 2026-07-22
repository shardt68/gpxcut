---
name: gpxcut-map-source-access
description: Guide an agent to integrate and troubleshoot map data sources in GpxCut, including XYZ, WMTS-derived templates, WMS GetMap templates, ArcGIS REST tile/export services, licensing metadata, and provider-specific quirks.
license: MIT
compatibility: Windows-focused .NET 8/WPF project; requires WebView2/MapLibre path and network access for endpoint validation.
metadata:
  author: gpxcut
  version: "0.1.0"
---

# GpxCut Map Source Access Skill

Use this skill when adding, validating, or debugging map background sources in GpxCut.

## Activation Cues

Use this skill when the task mentions one or more of:

- add a new country basemap/orthophoto source
- WMS, WMTS, XYZ, ArcGIS REST, or tile URL templates
- layer-policies.json entries
- attribution, license, cache policy, offline constraints
- CRS/projection issues (EPSG:3857 vs local CRS)
- source-specific failures (401, 404, 500, timeout)

## Source-of-Truth and Runtime Path

1. Source policy is defined in `src/GpxCut.App/MapAssets/layer-policies.json`.
2. Runtime map behavior is implemented in `src/GpxCut.App/MapAssets/map.js`.
3. Basemap selection/UI plumbing is in `src/GpxCut.App/MainWindow.xaml.cs`.

Practical flow:

1. Add or update a layer object in `layer-policies.json`.
2. Ensure `map.type` is one of `xyz`, `wmts`, or `wms`.
3. Use URL templates that match existing runtime placeholders and rewriting behavior.
4. Validate endpoint responses (status code and image MIME) before enabling usage.

## Supported Access Forms

### 1. XYZ (direct tile)

When to use:

- Provider exposes standard Web Mercator tile path by zoom/x/y.

Template pattern:

- `https://host/path/{z}/{x}/{y}.png`
- Some providers use `.../{z}/{y}/{x}` (row/col order differs).

Notes:

- Confirm placeholder order from provider docs/capabilities.
- Verify a sample tile returns image content (`image/png`, `image/jpeg`, ...).

### 2. WMTS-derived XYZ path

When to use:

- Provider offers WMTS but stable access is easiest via tile URL template.

Template pattern:

- `https://host/.../{z}/{x}/{y}.jpg`
- Or provider-specific matrix path with row/col order differences.

Project-specific behavior:

- `map.js` supports per-layer `wmtsZoomOffset` for providers whose matrix level numbering differs from standard XYZ zoom.

Notes:

- Validate tile matrix mapping and min/max zoom.
- Wrong zoom offset commonly appears as 404 for all tiles.

### 3. WMS GetMap template

When to use:

- Provider is WMS-first or exposes stable WMS endpoint.

Template pattern in GpxCut:

- Include the placeholder `{bbox-epsg-3857}` and fixed `WIDTH=256&HEIGHT=256`.
- Example shape:
  `...SERVICE=WMS&REQUEST=GetMap&VERSION=1.1.1&LAYERS=...&FORMAT=image/png&SRS=EPSG:3857&BBOX={bbox-epsg-3857}&WIDTH=256&HEIGHT=256`

Project-specific behavior:

- `map.js` probes WMS templates and can pick a working fallback if multiple template candidates are provided.

Notes:

- For WMS 1.3.0, CRS axis-order pitfalls may exist with some services.
- Keep request parameters explicit and tested.

### 4. ArcGIS REST tile service

When to use:

- Provider exposes cached `MapServer/tile/{z}/{y}/{x}`.

Template pattern:

- `https://host/.../MapServer/tile/{z}/{y}/{x}`

Notes:

- Verify metadata contains `singleFusedMapCache: true` and `tileInfo`.
- Even with cache metadata, direct tile paths can still be blocked or inconsistent.

### 5. ArcGIS REST export service (dynamic map image)

When to use:

- Tile path is unavailable or unreliable, but export works.

Template pattern used in GpxCut:

- `.../MapServer/export?bbox={bbox-epsg-3857}&bboxSR=3857&imageSR=3857&size=256,256&format=png&transparent=false&f=image`

Notes:

- This is integrated as `map.type: "wms"` in policy because runtime supports `{bbox-epsg-3857}` template substitution in that path.
- Confirm latency and availability; dynamic export can be slower than cached tiles.

## Required Policy Metadata per Layer

Each layer entry should include:

- `id`, `name`, `enabledByDefault`
- `attribution`
- `licenseName`, `licenseUrl`
- `commercialUseAllowed`
- `cacheAllowed`, `cacheMode`, `minTtlDays`, `offlinePrefetchAllowed`
- `notes`, `reviewedAt`
- `map` object (`type`, `tiles`, `tileSize`, optional zoom/bounds)

## Endpoint Validation Checklist

Before final integration:

1. Metadata reachable (if applicable):
   - ArcGIS REST `?f=pjson`
   - WMS `GetCapabilities`
2. Live image response test with production-like parameters:
   - HTTP 200
   - MIME starts with `image/`
3. CRS check:
   - Prefer EPSG:3857 compatibility for current app path.
4. Stability check:
   - no repeated timeouts, no auth-gated response.
5. Policy compliance check:
   - license/attribution/commercial/caching fields are explicit.

## Common Provider-Specific Pitfalls

- Auth-gated public-looking endpoints (`401 Unauthorized`).
- Endpoint exists but returns `400 Bad Request` without provider-specific parameters.
- WMTS row/col order confusion (`{x}/{y}` vs `{y}/{x}`).
- Non-standard tile matrix numbering (requires `wmtsZoomOffset`).
- ArcGIS metadata reachable but `tile`/`export` path times out.
- Service variant mismatch (example: one service key fails while another official key works).

## Recommended Integration Procedure

1. Discover official service through official catalog/API where possible.
2. Pick the simplest stable form:
   - cached tile endpoint first,
   - otherwise validated export endpoint.
3. Add layer in `layer-policies.json` with complete compliance metadata.
4. Run JSON parse validation.
5. Smoke-test in app (layer selection, visible map, no blank tiles).
6. Record findings and caveats in repo memory/documentation.

## Quality Gates

- Correctness: selected source really renders images with the configured template.
- Stability: no known auth or timeout failure mode in normal use.
- Compliance: attribution/license/cache restrictions are explicit and user-visible.
- Maintainability: notes include why a specific endpoint variant was chosen.

## References

- `src/GpxCut.App/MapAssets/layer-policies.json`
- `src/GpxCut.App/MapAssets/map.js`
- `src/GpxCut.App/MainWindow.xaml.cs`
