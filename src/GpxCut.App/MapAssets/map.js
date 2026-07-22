(() => {
  const cursorProximityPixels = 12;
  const hoverDelayMs = 700;

  const fallbackBasemap = {
    id: "osm-standard",
    name: "OpenStreetMap Standard",
    type: "xyz",
    tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
    tileSize: 256,
    attribution: "&copy; OpenStreetMap contributors"
  };
  const wmsProbeBboxEpsg3857 = "1113194.9079327357,7044436.526761846,1175452.1635191638,7106693.782348276";
  let basemapApplyToken = 0;

  function normalizeBasemapBounds(candidateBounds) {
    if (!candidateBounds || typeof candidateBounds !== "object") {
      return undefined;
    }

    const minLon = candidateBounds.minLon ?? candidateBounds.MinLon;
    const minLat = candidateBounds.minLat ?? candidateBounds.MinLat;
    const maxLon = candidateBounds.maxLon ?? candidateBounds.MaxLon;
    const maxLat = candidateBounds.maxLat ?? candidateBounds.MaxLat;

    if (!Number.isFinite(minLon) || !Number.isFinite(minLat) || !Number.isFinite(maxLon) || !Number.isFinite(maxLat)) {
      return undefined;
    }

    if (minLon >= maxLon || minLat >= maxLat) {
      return undefined;
    }

    return {
      minLon,
      minLat,
      maxLon,
      maxLat
    };
  }

  function isHessenBasemap(config) {
    return typeof config?.id === "string" && config.id.toLowerCase().startsWith("hessen-");
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function toMapMaxBounds(bounds) {
    if (!bounds) {
      return null;
    }

    return [
      [bounds.minLon, bounds.minLat],
      [bounds.maxLon, bounds.maxLat]
    ];
  }

  function normalizeBasemapConfig(candidate) {
    if (!candidate || typeof candidate !== "object") {
      return null;
    }

    const candidateTiles = candidate.tiles ?? candidate.Tiles;
    const tiles = Array.isArray(candidateTiles)
      ? candidateTiles.filter((tile) => typeof tile === "string" && tile.length > 0)
      : [];

    if (tiles.length === 0) {
      return null;
    }

    const candidateTypeRaw = candidate.type ?? candidate.Type;
    const candidateType = typeof candidateTypeRaw === "string"
      ? candidateTypeRaw.toLowerCase()
      : "xyz";
    if (candidateType !== "xyz" && candidateType !== "wmts" && candidateType !== "wms") {
      return null;
    }

    const candidateTileSize = candidate.tileSize ?? candidate.TileSize;
    const candidateAttribution = candidate.attribution ?? candidate.Attribution;
    const candidateId = candidate.id ?? candidate.Id;
    const candidateName = candidate.name ?? candidate.Name;
    const candidateWmtsZoomOffset = candidate.wmtsZoomOffset ?? candidate.WmtsZoomOffset;
    const candidateMinZoom = candidate.minZoom ?? candidate.MinZoom;
    const candidateMaxZoom = candidate.maxZoom ?? candidate.MaxZoom;
    const candidateBounds = candidate.bounds ?? candidate.Bounds;

    const tileSize = Number.isFinite(candidateTileSize) ? candidateTileSize : 256;
    const attribution = typeof candidateAttribution === "string"
      ? candidateAttribution
      : fallbackBasemap.attribution;
    const wmtsZoomOffset = Number.isFinite(candidateWmtsZoomOffset) ? candidateWmtsZoomOffset : 0;
    const minZoom = Number.isFinite(candidateMinZoom) ? candidateMinZoom : undefined;
    const maxZoom = Number.isFinite(candidateMaxZoom) ? candidateMaxZoom : undefined;
    const bounds = normalizeBasemapBounds(candidateBounds);

    return {
      id: typeof candidateId === "string" ? candidateId : fallbackBasemap.id,
      name: typeof candidateName === "string" ? candidateName : fallbackBasemap.name,
      type: candidateType,
      tiles,
      tileSize,
      attribution,
      wmtsZoomOffset,
      minZoom,
      maxZoom,
      bounds
    };
  }

  let activeBasemap = normalizeBasemapConfig(window.__gpxcutBasemapConfig) ?? fallbackBasemap;

  function buildRasterSource(config) {
    const source = {
      type: "raster",
      tiles: config.tiles,
      tileSize: config.tileSize,
      attribution: config.attribution
    };

    if (Number.isFinite(config.minZoom)) {
      source.minzoom = config.minZoom;
    }

    if (Number.isFinite(config.maxZoom)) {
      source.maxzoom = config.maxZoom;
    }

    return source;
  }

  function rewriteWmtsTileUrl(url, config) {
    if (!config || config.type !== "wmts") {
      return url;
    }

    const zoomOffset = Number.isFinite(config.wmtsZoomOffset) ? config.wmtsZoomOffset : 0;
    if (zoomOffset === 0) {
      return url;
    }

    const match = url.match(/\/([0-9]+)\/([0-9]+)\/([0-9]+)(\.[a-zA-Z0-9]+)?(\?.*)?$/);
    if (!match) {
      return url;
    }

    const z = Number.parseInt(match[1], 10);
    if (!Number.isFinite(z)) {
      return url;
    }

    const mappedZ = z + zoomOffset;
    if (!Number.isFinite(mappedZ) || mappedZ < 0) {
      return url;
    }

    const mappedZString = mappedZ.toString().padStart(2, "0");
    return url.replace(
      /\/([0-9]+)\/([0-9]+)\/([0-9]+)(\.[a-zA-Z0-9]+)?(\?.*)?$/,
      `/${mappedZString}/${match[2]}/${match[3]}${match[4] ?? ""}${match[5] ?? ""}`
    );
  }

  async function probeWmsTemplate(template) {
    const probeUrl = template.replace("{bbox-epsg-3857}", wmsProbeBboxEpsg3857);
    const controller = new AbortController();
    const timeoutId = window.setTimeout(() => controller.abort(), 2500);

    try {
      const response = await fetch(probeUrl, {
        method: "GET",
        cache: "no-store",
        signal: controller.signal
      });

      if (!response.ok) {
        return false;
      }

      const contentType = (response.headers.get("content-type") ?? "").toLowerCase();
      return contentType.startsWith("image/");
    } catch {
      return false;
    } finally {
      window.clearTimeout(timeoutId);
    }
  }

  async function resolveBasemapTiles(config) {
    if (!config || config.type !== "wms" || !Array.isArray(config.tiles) || config.tiles.length <= 1) {
      return config.tiles;
    }

    for (const template of config.tiles) {
      if (typeof template !== "string" || !template.includes("{bbox-epsg-3857}")) {
        continue;
      }

      // For WMS templates with yearly layer names, pick the first template that really serves images.
      // This avoids blank maps when one year disappears and a fallback year is available.
      if (await probeWmsTemplate(template)) {
        return [template];
      }
    }

    return [config.tiles[0]];
  }

  function postMapDiagnostic(payload) {
    if (!window.chrome?.webview || !payload || typeof payload !== "object") {
      return;
    }

    window.chrome.webview.postMessage({
      type: "map-diagnostic",
      ...payload
    });
  }

  function applyViewportConstraints(config, jumpIntoConstraints) {
    const useHessenLock = isHessenBasemap(config);

    if (!useHessenLock) {
      map.setMinZoom(0);
      map.setMaxZoom(22);
      map.setMaxBounds(null);
      return;
    }

    const minZoom = Number.isFinite(config.minZoom) ? config.minZoom : 0;
    const maxZoom = Number.isFinite(config.maxZoom) ? config.maxZoom : 22;
    const maxBounds = toMapMaxBounds(config.bounds);

    map.setMinZoom(minZoom);
    map.setMaxZoom(maxZoom);
    map.setMaxBounds(maxBounds);

    if (!jumpIntoConstraints) {
      return;
    }

    const zoom = map.getZoom();
    const targetZoom = clamp(zoom, minZoom, maxZoom);

    let center = map.getCenter();
    let targetLon = center.lng;
    let targetLat = center.lat;

    if (config.bounds) {
      targetLon = clamp(targetLon, config.bounds.minLon, config.bounds.maxLon);
      targetLat = clamp(targetLat, config.bounds.minLat, config.bounds.maxLat);
    }

    const zoomChanged = Math.abs(targetZoom - zoom) > 1e-9;
    const centerChanged = Math.abs(targetLon - center.lng) > 1e-9 || Math.abs(targetLat - center.lat) > 1e-9;
    if (!zoomChanged && !centerChanged) {
      return;
    }

    map.easeTo({
      center: [targetLon, targetLat],
      zoom: targetZoom,
      duration: 320
    });

    postMapDiagnostic({
      category: "viewport-clamped",
      basemapId: config.id,
      zoom: zoom,
      clampedZoom: targetZoom,
      clampedCenter: [targetLon, targetLat]
    });
  }

  async function applyBasemap(config) {
    const applyToken = ++basemapApplyToken;
    const normalized = normalizeBasemapConfig(config);
    if (!normalized) {
      return false;
    }

    const resolvedTiles = await resolveBasemapTiles(normalized);
    if (applyToken !== basemapApplyToken) {
      return false;
    }

    const resolvedConfig = {
      ...normalized,
      tiles: resolvedTiles
    };

    if (map.getLayer("basemap-layer")) {
      map.removeLayer("basemap-layer");
    }

    if (map.getSource("basemap")) {
      map.removeSource("basemap");
    }

    map.addSource("basemap", buildRasterSource(resolvedConfig));

    const beforeId = map.getLayer("track-line") ? "track-line" : undefined;
    map.addLayer(
      {
        id: "basemap-layer",
        type: "raster",
        source: "basemap"
      },
      beforeId
    );

    activeBasemap = resolvedConfig;
    applyViewportConstraints(resolvedConfig, true);
    return true;
  }

  const map = new maplibregl.Map({
    container: "map",
    style: {
      version: 8,
      sources: {
        basemap: buildRasterSource(activeBasemap)
      },
      layers: [
        {
          id: "basemap-layer",
          type: "raster",
          source: "basemap"
        }
      ]
    },
    transformRequest: (url, resourceType) => {
      if (resourceType === "Tile") {
        return {
          url: rewriteWmtsTileUrl(url, activeBasemap)
        };
      }

      return { url };
    },
    center: [8.6821, 50.1109],
    zoom: 8,
    minZoom: isHessenBasemap(activeBasemap) && Number.isFinite(activeBasemap.minZoom) ? activeBasemap.minZoom : 0,
    maxZoom: isHessenBasemap(activeBasemap) && Number.isFinite(activeBasemap.maxZoom) ? activeBasemap.maxZoom : 22,
    maxBounds: isHessenBasemap(activeBasemap) ? toMapMaxBounds(activeBasemap.bounds) : null
  });

  const profilePanel = document.getElementById("profile-panel");
  const profileCanvas = document.getElementById("profile-canvas");
  const profileContext = profileCanvas ? profileCanvas.getContext("2d") : null;

  const emptyTrack = {
    type: "FeatureCollection",
    features: [
      {
        type: "Feature",
        geometry: {
          type: "LineString",
          coordinates: []
        },
        properties: {}
      }
    ]
  };

  const emptyEndpoints = {
    type: "FeatureCollection",
    features: []
  };

  const emptySelection = {
    type: "FeatureCollection",
    features: [
      {
        type: "Feature",
        geometry: {
          type: "LineString",
          coordinates: []
        },
        properties: {}
      }
    ]
  };

  const emptySelectionMarkers = {
    type: "FeatureCollection",
    features: []
  };

  let trackCoordinates = [];
  let pendingChunks = [];
  let isShiftPressed = false;
  let hoverTimerId = null;
  let hoverRequestToken = 0;
  let hoverPopup = null;
  let profileVisible = false;
  let profileMode = "time";
  let profileXAxis = "time";
  let profileYAxis = "elevation";
  let profileXLabel = "Time";
  let profileYLabel = "Elevation (m)";
  const profileChartMargin = {
    left: 68,
    right: 28,
    top: 20,
    bottom: 52
  };
  const selectableLayers = ["track-line", "selection-line", "selection-marker-start", "selection-marker-end"];
  let profilePoints = [];
  let profileSelection = {
    startIndex: null,
    endIndex: null
  };

  function isNearSelectableGeometry(point) {
    const hitFeatures = map.queryRenderedFeatures(
      [
        [point.x - cursorProximityPixels, point.y - cursorProximityPixels],
        [point.x + cursorProximityPixels, point.y + cursorProximityPixels]
      ],
      { layers: selectableLayers }
    );

    return hitFeatures.length > 0;
  }

  window.addEventListener("keydown", (event) => {
    if (event.key === "Shift") {
      isShiftPressed = true;
    }
  });

  window.addEventListener("keyup", (event) => {
    if (event.key === "Shift") {
      isShiftPressed = false;
    }
  });

  window.addEventListener("blur", () => {
    isShiftPressed = false;
  });

  function ensureTrackLayer() {
    if (map.getSource("track")) {
      return;
    }

    map.addSource("track", {
      type: "geojson",
      data: emptyTrack
    });

    map.addLayer({
      id: "track-line",
      type: "line",
      source: "track",
      paint: {
        "line-color": "#FF6B6B",
        "line-width": 3,
        "line-opacity": 0.8
      }
    });
  }

  function ensureEndpointLayer() {
    if (map.getSource("endpoints")) {
      return;
    }

    map.addSource("endpoints", {
      type: "geojson",
      data: emptyEndpoints
    });

    map.addLayer({
      id: "endpoint-start",
      type: "circle",
      source: "endpoints",
      filter: ["==", ["get", "kind"], "start"],
      paint: {
        "circle-color": "#16A34A",
        "circle-radius": 6,
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2
      }
    });

    map.addLayer({
      id: "endpoint-end",
      type: "circle",
      source: "endpoints",
      filter: ["==", ["get", "kind"], "end"],
      paint: {
        "circle-color": "#DC2626",
        "circle-radius": 6,
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2
      }
    });
  }

  function ensureSelectionLayer() {
    if (map.getSource("selection")) {
      return;
    }

    map.addSource("selection", {
      type: "geojson",
      data: emptySelection
    });

    map.addLayer({
      id: "selection-line",
      type: "line",
      source: "selection",
      paint: {
        "line-color": "#F59E0B",
        "line-width": 5,
        "line-opacity": 0.95
      }
    });
  }

  function ensureSelectionMarkerLayer() {
    if (map.getSource("selection-markers")) {
      return;
    }

    map.addSource("selection-markers", {
      type: "geojson",
      data: emptySelectionMarkers
    });

    map.addLayer({
      id: "selection-marker-start",
      type: "circle",
      source: "selection-markers",
      filter: ["==", ["get", "kind"], "start"],
      paint: {
        "circle-color": "#2563EB",
        "circle-radius": 7,
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2
      }
    });

    map.addLayer({
      id: "selection-marker-end",
      type: "circle",
      source: "selection-markers",
      filter: ["==", ["get", "kind"], "end"],
      paint: {
        "circle-color": "#EA580C",
        "circle-radius": 7,
        "circle-stroke-color": "#FFFFFF",
        "circle-stroke-width": 2
      }
    });
  }

  function setCoordinates(coordinates) {
    const src = map.getSource("track");
    if (!src) {
      return;
    }

    trackCoordinates = coordinates;

    src.setData({
      type: "FeatureCollection",
      features: [
        {
          type: "Feature",
          geometry: {
            type: "LineString",
            coordinates
          },
          properties: {}
        }
      ]
    });
  }

  function setEndpoints(features) {
    const src = map.getSource("endpoints");
    if (!src) {
      return;
    }

    src.setData({
      type: "FeatureCollection",
      features
    });
  }

  function setSelectionCoordinates(coordinates) {
    const src = map.getSource("selection");
    if (!src) {
      return;
    }

    src.setData({
      type: "FeatureCollection",
      features: [
        {
          type: "Feature",
          geometry: {
            type: "LineString",
            coordinates
          },
          properties: {}
        }
      ]
    });
  }

  function setSelectionMarkers(features) {
    const src = map.getSource("selection-markers");
    if (!src) {
      return;
    }

    src.setData({
      type: "FeatureCollection",
      features
    });
  }

  function escapeHtml(text) {
    return String(text)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function clearHoverPopup() {
    if (hoverPopup) {
      hoverPopup.remove();
      hoverPopup = null;
    }
  }

  function postHoverClear() {
    if (!window.chrome?.webview) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "map-hover-clear"
    });
  }

  function scheduleHoverRequest(event) {
    if (hoverTimerId) {
      clearTimeout(hoverTimerId);
    }

    const requestToken = ++hoverRequestToken;
    hoverTimerId = setTimeout(() => {
      if (requestToken !== hoverRequestToken) {
        return;
      }

      if (!window.chrome?.webview) {
        return;
      }

      window.chrome.webview.postMessage({
        type: "map-hover",
        lng: event.lngLat.lng,
        lat: event.lngLat.lat
      });
    }, hoverDelayMs);
  }

  function cancelHoverRequest() {
    hoverRequestToken++;
    if (hoverTimerId) {
      clearTimeout(hoverTimerId);
      hoverTimerId = null;
    }
  }

  function formatProfileX(value) {
    if (profileXAxis === "time") {
      const totalSeconds = Math.max(0, value);
      const hours = Math.floor(totalSeconds / 3600);
      const minutes = Math.floor((totalSeconds % 3600) / 60);
      const seconds = Math.floor(totalSeconds % 60);
      return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
    }

    return `${(value / 1000).toFixed(2)} km`;
  }

  function profileXTitle() {
    return profileXLabel;
  }

  function profileYTitle() {
    return profileYLabel;
  }

  function formatProfileY(value) {
    if (profileYAxis === "speed") {
      return `${value.toFixed(1)} km/h`;
    }

    return `${value.toFixed(0)} m`;
  }

  function buildTicks(min, max, tickCount = 6) {
    const ticks = [];
    if (!Number.isFinite(min) || !Number.isFinite(max) || tickCount < 2) {
      return ticks;
    }

    const steps = tickCount - 1;
    for (let i = 0; i <= steps; i++) {
      ticks.push(min + ((max - min) * i) / steps);
    }

    return ticks;
  }

  function applyProfileVisible(visible) {
    profileVisible = !!visible;
    if (!profilePanel) {
      return;
    }

    profilePanel.classList.toggle("hidden", !profileVisible);

    requestAnimationFrame(() => {
      map.resize();
      resizeProfileCanvas();
      drawProfile();
    });
  }

  function applyProfileData(payload) {
    if (!payload || !Array.isArray(payload.points)) {
      profilePoints = [];
      drawProfile();
      return;
    }

    profileMode = payload.mode === "distance" ? "distance" : "time";
    profileXAxis = payload.xAxis === "distance" ? "distance" : "time";
    profileYAxis = payload.yAxis === "speed" ? "speed" : "elevation";
    profileXLabel = typeof payload.xLabel === "string" && payload.xLabel.trim().length > 0 ? payload.xLabel : (profileXAxis === "time" ? "Time" : "Distance");
    profileYLabel = typeof payload.yLabel === "string" && payload.yLabel.trim().length > 0 ? payload.yLabel : (profileYAxis === "speed" ? "Speed (km/h)" : "Elevation (m)");
    profilePoints = payload.points
      .filter((point) => point && Number.isFinite(point.index) && Number.isFinite(point.x) && Number.isFinite(point.y))
      .map((point) => ({
        index: point.index,
        x: point.x,
        y: point.y
      }));

    drawProfile();
  }

  function applyProfileSelection(payload) {
    profileSelection = {
      startIndex: Number.isFinite(payload?.startIndex) ? payload.startIndex : null,
      endIndex: Number.isFinite(payload?.endIndex) ? payload.endIndex : null
    };

    drawProfile();
  }

  function resetProfile() {
    profilePoints = [];
    profileSelection = {
      startIndex: null,
      endIndex: null
    };
    drawProfile();
  }

  function resizeProfileCanvas() {
    if (!profileCanvas || !profilePanel || !profileContext || !profileVisible) {
      return;
    }

    const rect = profilePanel.getBoundingClientRect();
    const width = Math.max(1, Math.floor(rect.width));
    const height = Math.max(1, Math.floor(rect.height));
    const dpr = window.devicePixelRatio || 1;

    profileCanvas.width = Math.floor(width * dpr);
    profileCanvas.height = Math.floor(height * dpr);
    profileCanvas.style.width = `${width}px`;
    profileCanvas.style.height = `${height}px`;
  }

  function resolveSelectionRange() {
    if (profileSelection.startIndex == null && profileSelection.endIndex == null) {
      return null;
    }

    if (profileSelection.startIndex == null) {
      return {
        start: profileSelection.endIndex,
        end: profileSelection.endIndex
      };
    }

    if (profileSelection.endIndex == null) {
      return {
        start: profileSelection.startIndex,
        end: profileSelection.startIndex
      };
    }

    return {
      start: Math.min(profileSelection.startIndex, profileSelection.endIndex),
      end: Math.max(profileSelection.startIndex, profileSelection.endIndex)
    };
  }

  function getNearestSampleByIndex(targetIndex) {
    if (profilePoints.length === 0 || !Number.isFinite(targetIndex)) {
      return null;
    }

    let best = null;
    let bestDelta = Number.POSITIVE_INFINITY;

    for (const sample of profilePoints) {
      const delta = Math.abs(sample.index - targetIndex);
      if (delta < bestDelta) {
        bestDelta = delta;
        best = sample;
      }
    }

    return best;
  }

  function drawProfile() {
    if (!profileVisible || !profileContext || !profileCanvas || !profilePanel) {
      return;
    }

    resizeProfileCanvas();

    const dpr = window.devicePixelRatio || 1;
    const width = profileCanvas.width / dpr;
    const height = profileCanvas.height / dpr;

    profileContext.setTransform(dpr, 0, 0, dpr, 0, 0);
    profileContext.clearRect(0, 0, width, height);

    profileContext.fillStyle = "#f7f7f8";
    profileContext.fillRect(0, 0, width, height);

    const margin = profileChartMargin;

    const chartWidth = Math.max(1, width - margin.left - margin.right);
    const chartHeight = Math.max(1, height - margin.top - margin.bottom);

    profileContext.strokeStyle = "#d1d5db";
    profileContext.lineWidth = 1;
    profileContext.strokeRect(margin.left, margin.top, chartWidth, chartHeight);

    if (profilePoints.length === 0) {
      profileContext.fillStyle = "#6b7280";
      profileContext.font = "12px Segoe UI";
      profileContext.fillText("No profile data available for current mode.", margin.left + 8, margin.top + 20);
      return;
    }

    let minX = profilePoints[0].x;
    let maxX = profilePoints[0].x;
    let minY = profilePoints[0].y;
    let maxY = profilePoints[0].y;

    for (const point of profilePoints) {
      minX = Math.min(minX, point.x);
      maxX = Math.max(maxX, point.x);
      minY = Math.min(minY, point.y);
      maxY = Math.max(maxY, point.y);
    }

    if (maxX - minX < 1e-9) {
      maxX = minX + 1;
    }

    if (maxY - minY < 1e-9) {
      maxY = minY + 1;
    }

    const toCanvasX = (value) => margin.left + ((value - minX) / (maxX - minX)) * chartWidth;
    const toCanvasY = (value) => margin.top + chartHeight - ((value - minY) / (maxY - minY)) * chartHeight;

    const xTicks = buildTicks(minX, maxX, 7);
    const yTicks = buildTicks(minY, maxY, 6);

    profileContext.strokeStyle = "#e5e7eb";
    profileContext.lineWidth = 1;

    for (const yTick of yTicks) {
      const y = toCanvasY(yTick);
      profileContext.beginPath();
      profileContext.moveTo(margin.left, y);
      profileContext.lineTo(margin.left + chartWidth, y);
      profileContext.stroke();
    }

    for (const xTick of xTicks) {
      const x = toCanvasX(xTick);
      profileContext.beginPath();
      profileContext.moveTo(x, margin.top);
      profileContext.lineTo(x, margin.top + chartHeight);
      profileContext.stroke();
    }

    const selection = resolveSelectionRange();
    if (selection) {
      const startSample = getNearestSampleByIndex(selection.start);
      const endSample = getNearestSampleByIndex(selection.end);

      if (startSample && endSample) {
        const startX = Math.min(toCanvasX(startSample.x), toCanvasX(endSample.x));
        const endX = Math.max(toCanvasX(startSample.x), toCanvasX(endSample.x));

        profileContext.fillStyle = "rgba(245, 158, 11, 0.18)";
        profileContext.fillRect(startX, margin.top, Math.max(1, endX - startX), chartHeight);
      }
    }

    profileContext.beginPath();
    profileContext.lineWidth = 1.8;
    profileContext.strokeStyle = "#2563eb";

    for (let idx = 0; idx < profilePoints.length; idx++) {
      const point = profilePoints[idx];
      const x = toCanvasX(point.x);
      const y = toCanvasY(point.y);

      if (idx === 0) {
        profileContext.moveTo(x, y);
      } else {
        profileContext.lineTo(x, y);
      }
    }

    profileContext.stroke();

    if (profileSelection.startIndex != null) {
      const sample = getNearestSampleByIndex(profileSelection.startIndex);
      if (sample) {
        const x = toCanvasX(sample.x);
        profileContext.strokeStyle = "#2563eb";
        profileContext.lineWidth = 1;
        profileContext.beginPath();
        profileContext.moveTo(x, margin.top);
        profileContext.lineTo(x, margin.top + chartHeight);
        profileContext.stroke();
      }
    }

    if (profileSelection.endIndex != null) {
      const sample = getNearestSampleByIndex(profileSelection.endIndex);
      if (sample) {
        const x = toCanvasX(sample.x);
        profileContext.strokeStyle = "#ea580c";
        profileContext.lineWidth = 1;
        profileContext.beginPath();
        profileContext.moveTo(x, margin.top);
        profileContext.lineTo(x, margin.top + chartHeight);
        profileContext.stroke();
      }
    }

    profileContext.fillStyle = "#111827";
    profileContext.font = "11px Segoe UI";
    profileContext.fillText(profileYTitle(), margin.left, margin.top - 2);

    profileContext.textAlign = "center";
    profileContext.fillText(profileXTitle(), margin.left + chartWidth / 2, height - 8);

    profileContext.fillStyle = "#4b5563";
    profileContext.font = "10px Segoe UI";
    profileContext.textBaseline = "middle";

    for (let i = 0; i < yTicks.length; i++) {
      const yTick = yTicks[i];
      const y = toCanvasY(yTick);
      profileContext.textAlign = "right";
      profileContext.fillText(formatProfileY(yTick), margin.left - 8, y);
    }

    profileContext.textBaseline = "alphabetic";
    for (let i = 0; i < xTicks.length; i++) {
      const xTick = xTicks[i];
      const x = toCanvasX(xTick);

      if (i === 0) {
        profileContext.textAlign = "left";
      } else if (i === xTicks.length - 1) {
        profileContext.textAlign = "right";
      } else {
        profileContext.textAlign = "center";
      }

      profileContext.fillText(formatProfileX(xTick), x, margin.top + chartHeight + 16);
    }

    profileContext.textAlign = "left";
  }

  function findNearestSampleByCanvasX(canvasX) {
    if (profilePoints.length === 0 || !profileCanvas) {
      return null;
    }

    const width = profileCanvas.clientWidth;
    const margin = {
      left: profileChartMargin.left,
      right: profileChartMargin.right
    };
    const chartWidth = Math.max(1, width - margin.left - margin.right);

    let minX = profilePoints[0].x;
    let maxX = profilePoints[0].x;
    for (const point of profilePoints) {
      minX = Math.min(minX, point.x);
      maxX = Math.max(maxX, point.x);
    }

    if (maxX - minX < 1e-9) {
      maxX = minX + 1;
    }

    const clampedX = Math.max(margin.left, Math.min(width - margin.right, canvasX));
    const targetValue = minX + ((clampedX - margin.left) / chartWidth) * (maxX - minX);

    let best = null;
    let bestDelta = Number.POSITIVE_INFINITY;
    for (const point of profilePoints) {
      const delta = Math.abs(point.x - targetValue);
      if (delta < bestDelta) {
        bestDelta = delta;
        best = point;
      }
    }

    return best;
  }

  if (profileCanvas) {
    profileCanvas.addEventListener("click", (event) => {
      if (!profileVisible || !window.chrome?.webview) {
        return;
      }

      const rect = profileCanvas.getBoundingClientRect();
      const canvasX = event.clientX - rect.left;
      const nearest = findNearestSampleByCanvasX(canvasX);
      if (!nearest) {
        return;
      }

      window.chrome.webview.postMessage({
        type: "profile-click",
        index: nearest.index,
        shiftKey: !!event.shiftKey || isShiftPressed
      });
    });
  }

  window.addEventListener("resize", () => {
    drawProfile();
  });

  window.gpxcutMap = {
    clearTrack() {
      pendingChunks = [];
      setCoordinates([]);
    },

    clearEndpoints() {
      setEndpoints([]);
    },

    clearSelection() {
      setSelectionCoordinates([]);
      setSelectionMarkers([]);
    },

    clearHoverInfo() {
      clearHoverPopup();
    },

    setHoverInfo(payload) {
      if (!payload || !Array.isArray(payload.coordinate) || payload.coordinate.length !== 2) {
        clearHoverPopup();
        return;
      }

      const lines = Array.isArray(payload.lines) ? payload.lines : [];
      const title = typeof payload.title === "string" ? payload.title : "Punktinfo";

      const contentHtml = `
        <div style="font-family: Segoe UI, sans-serif; font-size: 12px; line-height: 1.3; max-width: 320px;">
          <div style="font-weight: 600; margin-bottom: 4px;">${escapeHtml(title)}</div>
          ${lines.map((line) => `<div>${escapeHtml(line)}</div>`).join("")}
        </div>`;

      clearHoverPopup();
      hoverPopup = new maplibregl.Popup({
        closeButton: false,
        closeOnClick: false,
        offset: 12,
        maxWidth: "360px"
      })
        .setLngLat(payload.coordinate)
        .setHTML(contentHtml)
        .addTo(map);
    },

    addTrackChunk(chunkCoordinates) {
      // Accumulate chunks without re-rendering for each one.
      // Call flushTrackChunks() once all chunks have been added.
      for (let i = 0; i < chunkCoordinates.length; i++) {
        pendingChunks.push(chunkCoordinates[i]);
      }
    },

    flushTrackChunks() {
      if (pendingChunks.length === 0) {
        return;
      }
      // Concatenate once and render once
      const all = trackCoordinates.concat(pendingChunks);
      pendingChunks = [];
      setCoordinates(all);
    },

    setSelectionCoordinates(selectionCoordinates) {
      if (!Array.isArray(selectionCoordinates)) {
        setSelectionCoordinates([]);
        return;
      }

      setSelectionCoordinates(selectionCoordinates);
    },

    setSelectionMarkers(payload) {
      if (!payload || (!Array.isArray(payload.start) && !Array.isArray(payload.end))) {
        setSelectionMarkers([]);
        return;
      }

      const features = [];

      if (Array.isArray(payload.start)) {
        features.push({
          type: "Feature",
          geometry: {
            type: "Point",
            coordinates: payload.start
          },
          properties: { kind: "start" }
        });
      }

      if (Array.isArray(payload.end)) {
        features.push({
          type: "Feature",
          geometry: {
            type: "Point",
            coordinates: payload.end
          },
          properties: { kind: "end" }
        });
      }

      setSelectionMarkers(features);
    },

    setProfileVisible(visible) {
      applyProfileVisible(visible);
    },

    setProfileData(payload) {
      applyProfileData(payload);
    },

    setProfileSelection(payload) {
      applyProfileSelection(payload || {});
    },

    clearProfile() {
      resetProfile();
    },

    setBasemap(payload) {
      void applyBasemap(payload);
    },

    getBasemap() {
      return activeBasemap;
    },

    setEndpoints(payload) {
      if (!payload || !Array.isArray(payload.start) || !Array.isArray(payload.end)) {
        setEndpoints([]);
        return;
      }

      setEndpoints([
        {
          type: "Feature",
          geometry: {
            type: "Point",
            coordinates: payload.start
          },
          properties: { kind: "start" }
        },
        {
          type: "Feature",
          geometry: {
            type: "Point",
            coordinates: payload.end
          },
          properties: { kind: "end" }
        }
      ]);
    },

    fitBounds(bounds) {
      if (!bounds) {
        return;
      }

      const hasAll = ["minLon", "minLat", "maxLon", "maxLat"].every((key) => typeof bounds[key] === "number");
      if (!hasAll) {
        return;
      }

      const fitOptions = {
        padding: 24,
        duration: 350
      };

      if (isHessenBasemap(activeBasemap) && Number.isFinite(activeBasemap.maxZoom)) {
        fitOptions.maxZoom = activeBasemap.maxZoom;
      }

      map.fitBounds(
        [
          [bounds.minLon, bounds.minLat],
          [bounds.maxLon, bounds.maxLat]
        ],
        fitOptions
      );

      if (isHessenBasemap(activeBasemap)) {
        map.once("moveend", () => {
          applyViewportConstraints(activeBasemap, true);
        });
      }
    }
  };

  map.on("load", () => {
    ensureTrackLayer();
    ensureEndpointLayer();
    ensureSelectionLayer();
    ensureSelectionMarkerLayer();
    applyViewportConstraints(activeBasemap, true);
    applyProfileVisible(false);
    if (window.chrome?.webview) {
      window.chrome.webview.postMessage("map-ready");
    }
  });

  map.on("click", (event) => {
    if (!window.chrome?.webview) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "map-click",
      lng: event.lngLat.lng,
      lat: event.lngLat.lat,
      shiftKey: !!event.originalEvent?.shiftKey || isShiftPressed,
      useEndMarker: !!event.originalEvent?.shiftKey || isShiftPressed
    });
  });

  map.on("contextmenu", (event) => {
    if (!window.chrome?.webview) {
      return;
    }

    if (event.originalEvent?.preventDefault) {
      event.originalEvent.preventDefault();
    }

    if (!isNearSelectableGeometry(event.point)) {
      window.chrome.webview.postMessage({
        type: "map-empty-contextmenu",
        x: event.point.x,
        y: event.point.y,
        lng: event.lngLat.lng,
        lat: event.lngLat.lat
      });
      return;
    }

    window.chrome.webview.postMessage({
      type: "map-click",
      lng: event.lngLat.lng,
      lat: event.lngLat.lat,
      shiftKey: true,
      useEndMarker: true
    });
  });

  map.on("mousemove", (event) => {
    const nearSelectableGeometry = isNearSelectableGeometry(event.point);

    // Near selectable geometry show arrow cursor; elsewhere keep pan-hand cursor.
    if (nearSelectableGeometry) {
      map.getCanvas().style.cursor = "default";
      scheduleHoverRequest(event);
      return;
    }

    map.getCanvas().style.cursor = "grab";
    cancelHoverRequest();
    clearHoverPopup();
    postHoverClear();
  });

  map.on("mouseout", () => {
    map.getCanvas().style.cursor = "grab";
    cancelHoverRequest();
    clearHoverPopup();
    postHoverClear();
  });

  map.on("error", (event) => {
    const message = event?.error?.message ?? event?.message ?? "Unknown map error";
    const isBasemapRelated = event?.sourceId === "basemap";
    const hasTileContext = !!event?.tile;

    postMapDiagnostic({
      category: hasTileContext || isBasemapRelated ? "tile-error" : "map-error",
      basemapId: activeBasemap?.id,
      sourceId: event?.sourceId ?? null,
      zoom: map.getZoom(),
      message: String(message)
    });
  });
})();
