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

  window.gpxcutMap = {
    clearTrack() {
      setCoordinates([]);
    },

    addTrackChunk(chunkCoordinates) {
      setCoordinates(trackCoordinates.concat(chunkCoordinates));
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
    if (window.chrome?.webview) {
      window.chrome.webview.postMessage("map-ready");
    }
  });
})();
