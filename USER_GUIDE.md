# GpxCut User Guide

This guide explains the practical editing workflows for typical GPX cleanup tasks.

## What You Can Do

- Open a GPX track and inspect it on the map.
- Set two boundary points and preview the path between them in a different color.
- Move the selected boundary points to refine the selection.
- Delete the selected middle section.
- Export the selected middle section as a new GPX.
- Split a track at a selected position.

## Mouse and Keyboard Controls

### Mouse

- Left click on track: set or move the start boundary marker.
- Shift + left click on track: set or move the end boundary marker.
- Right click on track: set or move the end boundary marker (shortcut for Shift selection).
- Hover near track/selection: show point info popup.

### Keyboard

- Left Arrow: move start marker one point backward.
- Right Arrow: move start marker one point forward.
- Shift + Left Arrow: move end marker one point backward.
- Shift + Right Arrow: move end marker one point forward.
- Ctrl + Left Arrow: move selected marker backward by 10 points.
- Ctrl + Right Arrow: move selected marker forward by 10 points.
- Ctrl + S: save current track changes.
- Delete: delete the currently selected range.

Notes:

- Without Shift, arrow keys control the start marker.
- With Shift, arrow keys control the end marker.
- Ctrl changes the step size from 1 point to 10 points.

## Basic Workflow: Select, Adjust, Then Apply

1. Open your GPX file.
2. Click the first boundary point on the track.
3. Set the second boundary point using Shift + left click (or right click) on the track.
4. Verify the highlighted in-between section.
5. Adjust the boundary points until the selection is exact.
6. Apply one of these actions:
   - Delete selected section
   - Export selected section

## Moving Boundary Points Precisely

After both boundary points are set, refine them precisely:

1. Select the boundary marker you want to move.
2. Use arrow keys to move the marker along the track.
3. Use Shift + arrow keys if you want to move the end marker instead of the start marker.
4. Use Ctrl + arrow keys for faster jumps (10 points per key press).
5. Repeat until the highlighted section matches exactly what you want to remove or export.

Tip: Use this for very short noisy parts where mouse-only selection is too coarse.

## Practical Scenario: Remove Pause Noise "Stars"

Long recordings can contain dense noisy clusters during pauses (often looking like small "stars" around one place).

Recommended cleanup flow:

1. Identify the incoming path and outgoing path around the noisy cluster.
2. Set boundary point A on the incoming path.
3. Set boundary point B on the outgoing path (Shift + left click or right click).
4. Use arrow keys (and Shift + arrow for the end marker) to move A and B toward the center transition points.
5. Confirm the highlighted section covers only the noisy cluster.
6. Delete the selection.

Result: the pause artifact is removed while the useful route before and after remains.

## Practical Scenario: Extract a Specific Subroute

1. Set boundary point A at the intended start of the subroute.
2. Set boundary point B at the intended end.
3. Fine-tune both points.
4. Export the highlighted section as a new GPX file.

Result: you get a clean standalone GPX for exactly that route part.

## Split Track at Position

Use split when you want two separate track parts instead of deleting or exporting one middle range.

1. Open the track.
2. Select the split position on the track.
3. Confirm the split action.
4. Review resulting Part A (before split) and Part B (after split).
5. Continue editing or export either part.

Use split when:

- You want to separate a long recording into logical legs.
- You want to keep both sides as independent parts.

## Choosing the Right Action

- Delete range: remove unwanted section and keep one cleaned track.
- Export range: keep original track and create a separate GPX of the selected section.
- Split track: create two independent parts at one position.

## Basemap Selection

You can switch the map background using the `Basemap` selector in the top toolbar.

- The app lists only basemap entries that are valid in the layer policy file.
- If a configured source is missing or invalid, GpxCut falls back to OpenStreetMap Standard.
- Some imagery layers may be region-specific (for example USA-focused datasets).
- The selected basemap is remembered for the next app start.
- Hover the basemap selector to see license and cache/offline policy hints.
- If a layer is marked `[No cache]`, GpxCut asks for explicit confirmation before activating it.
- A policy info panel below the toolbar shows the active layer's license, cache, and offline rules.
- Additional open options now include OpenTopoMap and official basemap.de layers for Germany.
- NRW DOP aerial imagery is available in the basemap selector via WMTS zoom mapping (`wmtsZoomOffset`).

## Notes

- Keep a backup of original files before destructive edits.
- For very large tracks, allow time for map updates after changing selections.
- If the highlighted section looks wrong, adjust boundaries before applying an action.