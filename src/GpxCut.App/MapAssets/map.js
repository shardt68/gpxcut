(() => {
  const cursorProximityPixels = 12;
  const hoverDelayMs = 700;

  const map = new maplibregl.Map({
    container: "map",
    style: {
      version: 8,
      sources: {
        osm: {
          type: "raster",
          tiles: [
            "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
          ],
          tileSize: 256,
          attribution: "&copy; OpenStreetMap contributors"
        }
      },
      layers: [
        {
          id: "osm",
          type: "raster",
          source: "osm"
        }
      ]
    },
    center: [8.6821, 50.1109],
    zoom: 8
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
  let profilePoints = [];
  let profileSelection = {
    startIndex: null,
    endIndex: null
  };

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

      map.fitBounds(
        [
          [bounds.minLon, bounds.minLat],
          [bounds.maxLon, bounds.maxLat]
        ],
        { padding: 24, duration: 350 }
      );
    }
  };

  map.on("load", () => {
    ensureTrackLayer();
    ensureEndpointLayer();
    ensureSelectionLayer();
    ensureSelectionMarkerLayer();
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

    window.chrome.webview.postMessage({
      type: "map-click",
      lng: event.lngLat.lng,
      lat: event.lngLat.lat,
      shiftKey: true,
      useEndMarker: true
    });
  });

  map.on("mousemove", (event) => {
    const hitFeatures = map.queryRenderedFeatures(
      [
        [event.point.x - cursorProximityPixels, event.point.y - cursorProximityPixels],
        [event.point.x + cursorProximityPixels, event.point.y + cursorProximityPixels]
      ],
      {
        layers: ["track-line", "selection-line", "selection-marker-start", "selection-marker-end"]
      }
    );

    // Near selectable geometry show arrow cursor; elsewhere keep pan-hand cursor.
    if (hitFeatures.length > 0) {
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
})();
