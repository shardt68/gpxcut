(() => {
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
      shiftKey: !!event.originalEvent?.shiftKey
    });
  });
})();
