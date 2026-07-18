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
  let isShiftPressed = false;
  let hoverTimerId = null;
  let hoverRequestToken = 0;
  let hoverPopup = null;

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

  window.gpxcutMap = {
    clearTrack() {
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
      setCoordinates(trackCoordinates.concat(chunkCoordinates));
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
