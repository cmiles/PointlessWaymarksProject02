// Full Screen Interactive MapLibre GL JS Map for PointedHorizons
// Independent of Horizon View map - supports live GPS tracking, track recording, and pointer placement
(function () {
    window.fullScreenMap = window.fullScreenMap || {};

    let map = null;
    let mapContainerElement = null;
    let pendingPmTilesConfig = null;
    let pointerMarker = null;
    let liveMarker = null;
    let isLoaded = false;

    let currentPointerLat = 32.2226;
    let currentPointerLon = -110.9747;
    let isLiveTracking = false;
    let isGpsActive = false;
    let isRecordingActive = false;
    let recordedPoints = []; // Array of [lon, lat, ele]
    let allImportedTracks = []; // Array of { id, name, color, isVisible, minLon, maxLon, minLat, maxLat, coordinates }
    let activeTerrainSourceId = null;

    // Reset to North state & GPS auto-center coordination
    let isResettingNorth = false;
    let pendingAutoCenterCoords = null;
    let resetNorthTimeoutId = null;
    let activeResetNorthCompleteCb = null;

    let onPointerMovedCb = null;
    let onTrackClickedCb = null;
    let onEditWaypointCb = null;

    let currentSelectedBaseLayer = 'topo';
    try {
        const _saved = localStorage.getItem('pointedhorizons_last_map_layer');
        if (_saved) currentSelectedBaseLayer = _saved;
    } catch (e) { }

    // Elevation Profile Card & Interactive Scrubbing State
    let isMetricUnits = false;
    let isDarkMode = true;
    let profileCardElement = null;
    let profileMapMarker = null;
    let currentProfileTrack = null;
    let currentProfilePoints = []; // Array of [distKm, eleM, lat, lon]

    // Route Drawing & Trail Snapping State
    let isRouteDrawingActive = false;
    let snapToTrails = true;
    let routeWaypoints = []; // Array of { lat, lon }
    let routeLegs = []; // Array of Array of [lon, lat]
    let routeMarkers = []; // Array of maplibregl.Marker instances
    let onRouteUpdatedCb = null; // Callback: (distanceKm, pointCount, wpCount, isRouting)
    let isRoutingInProgress = false;
    let isRouteClickPending = false;

    // Register PMTiles protocol on maplibregl if available
    function registerPmtiles() {
        if (typeof window !== 'undefined' && window.pmtiles && window.maplibregl) {
            try {
                if (!window.fullScreenMap._pmtilesRegistered) {
                    const protocol = new window.pmtiles.Protocol();
                    window.maplibregl.addProtocol("pmtiles", protocol.tile);
                    window.fullScreenMap.pmtilesProtocol = protocol;
                    window.fullScreenMap._pmtilesRegistered = true;
                }
            } catch (e) {
                console.warn("[FullScreenMap] PMTiles registration:", e);
            }
        }
    }
    registerPmtiles();

    function createPointerMarkerElement() {
        const el = document.createElement('div');
        el.className = 'ph-pointer-marker';
        el.style.width = '32px';
        el.style.height = '42px';
        el.style.cursor = 'pointer';
        el.style.pointerEvents = 'auto';
        el.innerHTML = `
            <svg viewBox="0 0 32 42" width="32" height="42" fill="none" xmlns="http://www.w3.org/2000/svg" style="filter: drop-shadow(0 2px 4px rgba(0,0,0,0.35));">
                <path d="M16 0C7.163 0 0 7.163 0 16c0 12 16 26 16 26s16-14 16-26C32 7.163 24.837 0 16 0z" fill="#0284C7"/>
                <path d="M16 2C8.268 2 2 8.268 2 16c0 10.5 14 23 14 23s14-12.5 14-23C30 8.268 23.732 2 16 2z" fill="#0EA5E9"/>
                <circle cx="16" cy="16" r="6" fill="#FFFFFF"/>
                <circle cx="16" cy="16" r="3" fill="#0369A1"/>
            </svg>
        `;
        return el;
    }

    function createLiveLocationMarkerElement() {
        const el = document.createElement('div');
        el.className = 'ph-live-location-marker';
        el.style.width = '32px';
        el.style.height = '32px';
        el.style.pointerEvents = 'none';
        el.innerHTML = `
            <div style="position: relative; width: 32px; height: 32px; display: flex; align-items: center; justify-content: center;">
                <!-- Live GPS Pulse Ring (Active only when GPS is ON) -->
                <div class="ph-live-pulse" style="position: absolute; top: 0; left: 0; width: 32px; height: 32px; border-radius: 50%; background: rgba(14, 165, 233, 0.4); animation: ph-pulse 2s infinite ease-out; display: none;"></div>

                <!-- Live GPS Bearing / Direction Triangle (Active only when GPS is ON) -->
                <div class="ph-live-triangle" style="position: relative; width: 24px; height: 24px; display: none; align-items: center; justify-content: center; transform-origin: 50% 50%;">
                    <svg viewBox="0 0 24 24" width="24" height="24" style="display: block; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.5));">
                        <path d="M12 2 L21 21 L12 17 L3 21 Z" fill="#0284C7" stroke="#FFFFFF" stroke-width="2" stroke-linejoin="round"/>
                    </svg>
                </div>

                <!-- Static Location Dot (Active only when GPS is OFF) -->
                <div class="ph-static-dot" style="position: relative; width: 18px; height: 18px; border-radius: 50%; background: #0284C7; border: 2.5px solid #FFFFFF; box-shadow: 0 2px 5px rgba(0,0,0,0.45); display: flex; align-items: center; justify-content: center;">
                    <div style="width: 6px; height: 6px; border-radius: 50%; background: #FFFFFF; opacity: 0.9;"></div>
                </div>
            </div>
            <style>
                @keyframes ph-pulse {
                    0% { transform: scale(0.6); opacity: 1; }
                    100% { transform: scale(2.0); opacity: 0; }
                }
            </style>
        `;
        return el;
    }

    function updateLocationMarkerMode() {
        if (!liveMarker) return;
        const el = liveMarker.getElement();
        if (!el) return;

        const pulseEl = el.querySelector('.ph-live-pulse');
        const triEl = el.querySelector('.ph-live-triangle');
        const staticEl = el.querySelector('.ph-static-dot');

        if (isGpsActive) {
            if (pulseEl) pulseEl.style.display = 'block';
            if (triEl) triEl.style.display = 'flex';
            if (staticEl) staticEl.style.display = 'none';
        } else {
            if (pulseEl) pulseEl.style.display = 'none';
            if (triEl) triEl.style.display = 'none';
            if (staticEl) staticEl.style.display = 'flex';
        }
    }

    function createWaypointMarkerElement(index, isStart, isEnd) {
        const el = document.createElement('div');
        el.className = 'ph-route-marker';
        el.style.width = '24px';
        el.style.height = '24px';
        el.style.borderRadius = '50%';
        el.style.backgroundColor = isStart ? '#10B981' : (isEnd ? '#EF4444' : '#8B5CF6');
        el.style.border = '2px solid #FFFFFF';
        el.style.boxShadow = '0 2px 6px rgba(0,0,0,0.45)';
        el.style.display = 'flex';
        el.style.alignItems = 'center';
        el.style.justifyContent = 'center';
        el.style.color = '#FFFFFF';
        el.style.fontWeight = 'bold';
        el.style.fontSize = '11px';
        el.style.cursor = 'pointer';
        el.style.userSelect = 'none';
        el.innerText = index.toString();
        return el;
    }

    // Creates polygon points for a circle around lat/lon of given radius in meters
    function createAccuracyCircle(lat, lon, radiusMeters) {
        if (!radiusMeters || radiusMeters <= 0) return [[]];
        const points = 36;
        const coords = [];
        const km = radiusMeters / 1000.0;
        const degLat = km / 111.32;
        const degLon = km / (111.32 * Math.cos((lat * Math.PI) / 180.0));

        for (let i = 0; i <= points; i++) {
            const theta = (i / points) * (2 * Math.PI);
            const pLat = lat + degLat * Math.sin(theta);
            const pLon = lon + degLon * Math.cos(theta);
            coords.push([pLon, pLat]);
        }
        return [coords];
    }

    function cancelResetNorth() {
        isResettingNorth = false;
        pendingAutoCenterCoords = null;
        if (resetNorthTimeoutId) {
            clearTimeout(resetNorthTimeoutId);
            resetNorthTimeoutId = null;
        }
        if (activeResetNorthCompleteCb && map) {
            try {
                map.off('moveend', activeResetNorthCompleteCb);
                map.off('rotateend', activeResetNorthCompleteCb);
            } catch (e) { }
            activeResetNorthCompleteCb = null;
        }
    }

    function performResetNorth(options, eventData, resetPitch) {
        if (!map) return;

        cancelResetNorth();

        const currentBearing = map.getBearing();
        const currentPitch = map.getPitch();

        // Check if bearing is already at North (and pitch flat if requested)
        const needsBearingReset = Math.abs(currentBearing) > 0.05;
        const needsPitchReset = resetPitch && Math.abs(currentPitch) > 0.05;

        if (!needsBearingReset && !needsPitchReset) {
            try {
                if (map.getBearing() !== 0) map.setBearing(0);
                if (resetPitch && map.getPitch() !== 0) map.setPitch(0);
            } catch (e) { }
            isResettingNorth = false;
            pendingAutoCenterCoords = null;
            return map;
        }

        isResettingNorth = true;

        const duration = (options && typeof options.duration === 'number') ? options.duration : 800;

        let hasCompleted = false;
        const onComplete = () => {
            if (hasCompleted) return;
            hasCompleted = true;

            if (resetNorthTimeoutId) {
                clearTimeout(resetNorthTimeoutId);
                resetNorthTimeoutId = null;
            }

            try {
                map.off('moveend', onComplete);
                map.off('rotateend', onComplete);
            } catch (e) { }
            activeResetNorthCompleteCb = null;

            // Ensure exact 0 alignment
            try {
                if (Math.abs(map.getBearing()) > 0.001) {
                    map.setBearing(0);
                }
                if (resetPitch && Math.abs(map.getPitch()) > 0.001) {
                    map.setPitch(0);
                }
            } catch (e) { }

            isResettingNorth = false;

            // If a GPS auto-center update arrived during rotation, ease to it now that we are upright facing North
            if (pendingAutoCenterCoords && isLiveTracking && isGpsActive) {
                const coords = pendingAutoCenterCoords;
                pendingAutoCenterCoords = null;
                try {
                    map.easeTo({
                        center: coords,
                        duration: 500
                    });
                } catch (e) { }
            } else {
                pendingAutoCenterCoords = null;
            }
        };

        activeResetNorthCompleteCb = onComplete;
        map.once('rotateend', onComplete);
        map.once('moveend', onComplete);

        // Fallback safety timeout (duration + 300ms) to guarantee completion even if events are suppressed
        resetNorthTimeoutId = setTimeout(onComplete, duration + 300);

        const easeOpts = Object.assign({
            bearing: 0,
            duration: duration,
            essential: true
        }, options);

        if (resetPitch) {
            easeOpts.pitch = 0;
        }

        try {
            map.easeTo(easeOpts, eventData);
        } catch (e) {
            onComplete();
        }
        return map;
    }

    function setupResetToNorthHandler() {
        if (!map) return;

        // Override map instance reset methods so any internal MapLibre or programmatic invocation uses our protected handler
        map.resetNorth = function (options, eventData) {
            performResetNorth(options, eventData, false);
            return map;
        };

        map.resetNorthPitch = function (options, eventData) {
            performResetNorth(options, eventData, true);
            return map;
        };

        // Also intercept direct DOM clicks on the compass button at the capture phase
        const attachCompassClickListener = () => {
            const container = map.getContainer ? map.getContainer() : mapContainerElement;
            if (container) {
                const compassBtn = container.querySelector('.maplibregl-ctrl-compass');
                if (compassBtn && !compassBtn._phResetNorthHooked) {
                    compassBtn._phResetNorthHooked = true;
                    compassBtn.addEventListener('click', function (e) {
                        e.preventDefault();
                        e.stopPropagation();
                        performResetNorth({ duration: 800 }, { originalEvent: e }, true);
                    }, { capture: true });
                }
            }
        };

        attachCompassClickListener();
        map.on('load', attachCompassClickListener);

        const syncCompassOrientation = () => {
            const container = map.getContainer ? map.getContainer() : mapContainerElement;
            if (container) {
                const compassIcon = container.querySelector('.maplibregl-ctrl-compass .maplibregl-ctrl-icon');
                if (compassIcon) {
                    const b = map.getBearing();
                    const p = map.getPitch();
                    compassIcon.style.transform = `rotate(${-b}deg) rotateX(${p}deg)`;
                }
            }
        };

        map.on('rotate', syncCompassOrientation);
        map.on('pitch', syncCompassOrientation);
        map.on('move', syncCompassOrientation);
        map.on('load', syncCompassOrientation);
        syncCompassOrientation();

        // If the user manually drags the map with mouse or touches screen, cancel the reset lock
        map.on('dragstart', function (e) {
            if (e && e.originalEvent && isResettingNorth) {
                cancelResetNorth();
            }
        });
    }

    window.fullScreenMap.init = function (container, options, onPointerMoved, onTrackClicked, onEditWaypoint) {
        if (typeof container === 'string') {
            container = document.getElementById(container);
        }
        if (!container) return;

        registerPmtiles();

        options = options || {};
        if (options.baseLayer) {
            currentSelectedBaseLayer = options.baseLayer;
            try { localStorage.setItem('pointedhorizons_last_map_layer', currentSelectedBaseLayer); } catch (e) { }
        }
        currentPointerLat = options.lat != null ? options.lat : 32.2226;
        currentPointerLon = options.lon != null ? options.lon : -110.9747;
        const initialZoom = options.zoom != null ? options.zoom : 13;
        onPointerMovedCb = onPointerMoved;
        if (onTrackClicked) onTrackClickedCb = onTrackClicked;
        if (onEditWaypoint) onEditWaypointCb = onEditWaypoint;
        if (options.isMetric != null) isMetricUnits = options.isMetric === true;
        if (options.isDark != null) isDarkMode = options.isDark === true;

        profileCardElement = null;
        profileMapMarker = null;

        ensurePulseStyle();

        if (map) {
            try { map.remove(); } catch (e) { }
            map = null;
        }

        function isBaseLayerVisible(id) {
            if (currentSelectedBaseLayer === 'streets' || currentSelectedBaseLayer === 'osm') {
                return id === 'osm-layer';
            } else if (currentSelectedBaseLayer === 'satellite') {
                return id === 'satellite-layer';
            } else if (currentSelectedBaseLayer === 'quads') {
                return id === 'quads-layer';
            } else if (currentSelectedBaseLayer === 'opentopo') {
                return id === 'opentopo-layer';
            } else if (currentSelectedBaseLayer === 'hybrid') {
                return id === 'satellite-layer' || id === 'hybrid-reference-layer';
            } else if (currentSelectedBaseLayer === 'pmtiles') {
                return false;
            } else {
                return id === 'topo-layer';
            }
        }

        const style = {
            version: 8,
            glyphs: "https://demotiles.maplibre.org/font/{fontstack}/{range}.pbf",
            sources: {
                "topo-source": {
                    type: "raster",
                    tiles: [
                        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}"
                    ],
                    tileSize: 256,
                    attribution: "Tiles © Esri",
                    maxzoom: 18
                },
                "osm-source": {
                    type: "raster",
                    tiles: [
                        "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
                    ],
                    tileSize: 256,
                    attribution: "© OpenStreetMap contributors",
                    maxzoom: 19
                },
                "satellite-source": {
                    type: "raster",
                    tiles: [
                        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}"
                    ],
                    tileSize: 256,
                    attribution: "Tiles © Esri",
                    maxzoom: 18
                },
                "quads-source": {
                    type: "raster",
                    tiles: [
                        "https://services.arcgisonline.com/ArcGIS/rest/services/USA_Topo_Maps/MapServer/tile/{z}/{y}/{x}"
                    ],
                    tileSize: 256,
                    attribution: "USGS / Esri",
                    maxzoom: 15
                },
                "opentopo-source": {
                    type: "raster",
                    tiles: [
                        "https://a.tile.opentopomap.org/{z}/{x}/{y}.png",
                        "https://b.tile.opentopomap.org/{z}/{x}/{y}.png",
                        "https://c.tile.opentopomap.org/{z}/{x}/{y}.png"
                    ],
                    tileSize: 256,
                    attribution: "© OpenTopoMap (CC-BY-SA)",
                    maxzoom: 17
                },
                "hybrid-reference-source": {
                    type: "raster",
                    tiles: [
                        "https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}"
                    ],
                    tileSize: 256,
                    attribution: "Esri Labels",
                    maxzoom: 18
                },
                "padus-source": {
                    type: "raster",
                    tiles: [
                        "https://gis.blm.gov/arcgis/rest/services/lands/BLM_Natl_SMA_Cached_without_PriUnk/MapServer/tile/{z}/{y}/{x}"
                    ],
                    tileSize: 256,
                    attribution: "BLM / USFS / NPS / State Lands",
                    maxzoom: 18
                },
                "gps-track-source": {
                    type: "geojson",
                    data: {
                        type: "Feature",
                        geometry: {
                            type: "LineString",
                            coordinates: recordedPoints
                        }
                    }
                },
                "live-accuracy-source": {
                    type: "geojson",
                    data: {
                        type: "Feature",
                        geometry: {
                            type: "Polygon",
                            coordinates: [[]]
                        }
                    }
                },
                "imported-tracks-source": {
                    type: "geojson",
                    data: {
                        type: "FeatureCollection",
                        features: []
                    }
                },
                "imported-waypoints-source": {
                    type: "geojson",
                    data: {
                        type: "FeatureCollection",
                        features: []
                    }
                },
                "route-draft-line-source": {
                    type: "geojson",
                    data: {
                        type: "Feature",
                        geometry: {
                            type: "LineString",
                            coordinates: []
                        }
                    }
                }
            },
            layers: [
                {
                    id: "topo-layer",
                    type: "raster",
                    source: "topo-source",
                    layout: { visibility: isBaseLayerVisible("topo-layer") ? "visible" : "none" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "osm-layer",
                    type: "raster",
                    source: "osm-source",
                    layout: { visibility: isBaseLayerVisible("osm-layer") ? "visible" : "none" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "satellite-layer",
                    type: "raster",
                    source: "satellite-source",
                    layout: { visibility: isBaseLayerVisible("satellite-layer") ? "visible" : "none" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "quads-layer",
                    type: "raster",
                    source: "quads-source",
                    layout: { visibility: isBaseLayerVisible("quads-layer") ? "visible" : "none" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "opentopo-layer",
                    type: "raster",
                    source: "opentopo-source",
                    layout: { visibility: isBaseLayerVisible("opentopo-layer") ? "visible" : "none" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "hybrid-reference-layer",
                    type: "raster",
                    source: "hybrid-reference-source",
                    layout: { visibility: isBaseLayerVisible("hybrid-reference-layer") ? "visible" : "none" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "padus-layer",
                    type: "raster",
                    source: "padus-source",
                    layout: { visibility: "none" },
                    paint: { "raster-opacity": 0.35 }
                },
                {
                    id: "imported-tracks-casing",
                    type: "line",
                    source: "imported-tracks-source",
                    filter: ["!=", ["get", "isRoute"], true],
                    paint: {
                        "line-color": "#000000",
                        "line-width": 4.5,
                        "line-opacity": 0.45
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "imported-tracks-line",
                    type: "line",
                    source: "imported-tracks-source",
                    filter: ["!=", ["get", "isRoute"], true],
                    paint: {
                        "line-color": ["coalesce", ["get", "color"], "#3B82F6"],
                        "line-width": 3.0,
                        "line-opacity": 0.95
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "imported-routes-casing",
                    type: "line",
                    source: "imported-tracks-source",
                    filter: ["==", ["get", "isRoute"], true],
                    paint: {
                        "line-color": "#000000",
                        "line-width": 5.0,
                        "line-opacity": 0.5
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "imported-routes-line",
                    type: "line",
                    source: "imported-tracks-source",
                    filter: ["==", ["get", "isRoute"], true],
                    paint: {
                        "line-color": ["coalesce", ["get", "color"], "#F59E0B"],
                        "line-width": 3.2,
                        "line-opacity": 0.95,
                        "line-dasharray": [2.5, 1.5]
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "imported-tracks-hit",
                    type: "line",
                    source: "imported-tracks-source",
                    paint: {
                        "line-color": "#000000",
                        "line-width": 20.0,
                        "line-opacity": 0.001
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "imported-waypoints-casing",
                    type: "circle",
                    source: "imported-waypoints-source",
                    paint: {
                        "circle-radius": 7,
                        "circle-color": "#FFFFFF",
                        "circle-opacity": 0.95
                    }
                },
                {
                    id: "imported-waypoints-circle",
                    type: "circle",
                    source: "imported-waypoints-source",
                    paint: {
                        "circle-radius": 5,
                        "circle-color": ["coalesce", ["get", "color"], "#EF4444"],
                        "circle-stroke-width": 1.5,
                        "circle-stroke-color": "#FFFFFF"
                    }
                },
                {
                    id: "imported-waypoints-label",
                    type: "symbol",
                    source: "imported-waypoints-source",
                    layout: {
                        "text-field": ["get", "name"],
                        "text-size": 11,
                        "text-offset": [0, 1.2],
                        "text-anchor": "top",
                        "text-optional": true
                    },
                    paint: {
                        "text-color": "#1E293B",
                        "text-halo-color": "#FFFFFF",
                        "text-halo-width": 1.5
                    }
                },
                {
                    id: "imported-waypoints-hit",
                    type: "circle",
                    source: "imported-waypoints-source",
                    paint: {
                        "circle-radius": 18,
                        "circle-color": "#000000",
                        "circle-opacity": 0.001
                    }
                },
                {
                    id: "route-draft-casing",
                    type: "line",
                    source: "route-draft-line-source",
                    paint: {
                        "line-color": "#FFFFFF",
                        "line-width": 6.5,
                        "line-opacity": 0.85
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "route-draft-line",
                    type: "line",
                    source: "route-draft-line-source",
                    paint: {
                        "line-color": "#8B5CF6",
                        "line-width": 4.5,
                        "line-opacity": 0.95
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "live-accuracy-fill",
                    type: "fill",
                    source: "live-accuracy-source",
                    paint: {
                        "fill-color": "#0284C7",
                        "fill-opacity": 0.15
                    }
                },
                {
                    id: "live-accuracy-line",
                    type: "line",
                    source: "live-accuracy-source",
                    paint: {
                        "line-color": "#0284C7",
                        "line-width": 1.5,
                        "line-opacity": 0.5
                    }
                },
                {
                    id: "gps-track-casing",
                    type: "line",
                    source: "gps-track-source",
                    paint: {
                        "line-color": "#FFFFFF",
                        "line-width": 6.5,
                        "line-opacity": 0.6
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "gps-track-layer",
                    type: "line",
                    source: "gps-track-source",
                    paint: {
                        "line-color": "#EF4444",
                        "line-width": 4.5,
                        "line-opacity": 0.9,
                        "line-dasharray": [1, 0]
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                },
                {
                    id: "gps-track-hit",
                    type: "line",
                    source: "gps-track-source",
                    paint: {
                        "line-color": "#000000",
                        "line-width": 22.0,
                        "line-opacity": 0.001
                    },
                    layout: {
                        "line-cap": "round",
                        "line-join": "round"
                    }
                }
            ]
        };

        mapContainerElement = container;

        map = new window.maplibregl.Map({
            container: container,
            style: style,
            center: [currentPointerLon, currentPointerLat],
            zoom: initialZoom,
            minZoom: 3,
            maxZoom: 18,
            maxPitch: 85,
            pitchWithRotate: true,
            dragRotate: true,
            touchPitch: true,
            attributionControl: true
        });

        // Add standard navigation controls (zoom in/out, compass)
        map.addControl(new window.maplibregl.NavigationControl({
            showCompass: true,
            visualizePitch: true
        }), 'bottom-right');

        // Setup Reset to North protection against GPS auto-center interruption
        setupResetToNorthHandler();

        // Create Pointer Marker
        const pEl = createPointerMarkerElement();
        pointerMarker = new window.maplibregl.Marker({
            element: pEl,
            anchor: 'bottom',
            draggable: true
        })
            .setLngLat([currentPointerLon, currentPointerLat])
            .addTo(map);

        pointerMarker.on('dragend', function () {
            const lngLat = pointerMarker.getLngLat();
            currentPointerLat = lngLat.lat;
            currentPointerLon = lngLat.lng;
            if (onPointerMovedCb) {
                onPointerMovedCb(currentPointerLat, currentPointerLon);
            }
        });

        // Create Live Location Marker (hidden initially)
        const lEl = createLiveLocationMarkerElement();
        liveMarker = new window.maplibregl.Marker({
            element: lEl,
            anchor: 'center',
            rotationAlignment: 'map'
        });

        // Track & Route layers cursor detection
        const trackLayers = ['imported-tracks-hit', 'imported-tracks-line', 'imported-routes-line', 'gps-track-hit', 'gps-track-layer'];
        trackLayers.forEach(layerId => {
            map.on('mouseenter', layerId, () => {
                if (!isRouteDrawingActive) map.getCanvas().style.cursor = 'pointer';
            });
            map.on('mouseleave', layerId, () => {
                if (!isRouteDrawingActive) map.getCanvas().style.cursor = '';
            });
        });

        // Waypoint layers cursor detection
        const waypointLayers = ['imported-waypoints-hit', 'imported-waypoints-circle', 'imported-waypoints-label'];
        waypointLayers.forEach(layerId => {
            map.on('mouseenter', layerId, () => {
                if (!isRouteDrawingActive) map.getCanvas().style.cursor = 'pointer';
            });
            map.on('mouseleave', layerId, () => {
                if (!isRouteDrawingActive) map.getCanvas().style.cursor = '';
            });
        });

        // Map Click: Handle waypoint click, track selection, route drawing, or move pointer
        map.on('click', function (e) {
            // Don't process if user clicked on a control, marker, or profile card
            if (e.originalEvent && e.originalEvent.target && e.originalEvent.target.closest('.maplibregl-control-container, .ph-pointer-marker, .ph-route-marker, #ph-elevation-profile-card, .maplibregl-popup')) {
                return;
            }

            if (isRouteDrawingActive) {
                handleRouteMapClick(e.lngLat.lat, e.lngLat.lng);
                return;
            }

            // Check if user clicked on a waypoint feature (with 10px buffer for responsive targeting)
            const wptBbox = [[e.point.x - 10, e.point.y - 10], [e.point.x + 10, e.point.y + 10]];
            const waypointFeatures = map.queryRenderedFeatures(wptBbox, {
                layers: ['imported-waypoints-hit', 'imported-waypoints-circle', 'imported-waypoints-label']
            });

            if (waypointFeatures && waypointFeatures.length > 0) {
                showWaypointPopup(waypointFeatures[0]);
                return;
            }

            // Check if user clicked on a track or route feature (with 8px bounding buffer)
            const bbox = [[e.point.x - 8, e.point.y - 8], [e.point.x + 8, e.point.y + 8]];
            const trackFeatures = map.queryRenderedFeatures(bbox, {
                layers: ['imported-tracks-hit', 'imported-tracks-line', 'imported-routes-line', 'gps-track-hit', 'gps-track-layer']
            });

            if (trackFeatures && trackFeatures.length > 0) {
                const feat = trackFeatures[0];
                if (feat.layer && feat.layer.id.startsWith('gps-track')) {
                    window.fullScreenMap.showElevationProfile('active_gps_track');
                    return;
                } else if (feat.properties && feat.properties.id) {
                    const trackId = feat.properties.trackId || feat.properties.id;
                    window.fullScreenMap.showElevationProfile(trackId);
                    if (onTrackClickedCb) onTrackClickedCb(trackId);
                    return;
                }
            }

            if (!isLiveTracking) {
                currentPointerLat = e.lngLat.lat;
                currentPointerLon = e.lngLat.lng;
                pointerMarker.setLngLat([currentPointerLon, currentPointerLat]);
                if (onPointerMovedCb) {
                    onPointerMovedCb(currentPointerLat, currentPointerLon);
                }
            }
        });

        // Defensive check: if camera ever penetrates terrain into NaN coordinates, auto-recover
        map.on('move', function () {
            const center = map.getCenter();
            if (!center || isNaN(center.lat) || isNaN(center.lng)) {
                console.warn("[FullScreenMap] Detected NaN center during move, auto-recovering...");
                recoverMapState(null, null, null);
            }
        });

        // Dynamic viewport filtering for imported GPX/FIT tracks
        map.on('moveend', function () {
            refreshImportedTracksInView();
        });

        map.on('load', async function () {
            isLoaded = true;
            if (pendingPmTilesConfig) {
                try {
                    await window.fullScreenMap.setPmTilesLayers(pendingPmTilesConfig);
                } catch (e) {
                    console.warn("[FullScreenMap] Error applying pending PMTiles:", e);
                }
            }
            if (currentSelectedBaseLayer) {
                window.fullScreenMap.setBaseLayer(currentSelectedBaseLayer);
            }
            if (map.getLayer('padus-layer')) {
                map.setLayoutProperty('padus-layer', 'visibility', isPadUsVisible ? 'visible' : 'none');
            }
            refreshImportedTracksInView();
            window.fullScreenMap.resize();
        });
    };

    window.fullScreenMap.centerOn = function (lat, lon, zoom) {
        if (!map) return;
        cancelResetNorth();
        const targetZoom = zoom || map.getZoom();
        map.flyTo({
            center: [lon, lat],
            zoom: Math.max(targetZoom, 13),
            essential: true
        });
    };

    window.fullScreenMap.centerOnCurrentLocation = function (lat, lon, movePointer) {
        if (!map) return;
        window.fullScreenMap.centerOn(lat, lon, 14);

        if (liveMarker) {
            liveMarker.setLngLat([lon, lat]);
            updateLocationMarkerMode();
            if (!liveMarker._map) {
                liveMarker.addTo(map);
            }
        }

        if (movePointer) {
            currentPointerLat = lat;
            currentPointerLon = lon;
            if (pointerMarker) {
                pointerMarker.setLngLat([lon, lat]);
            }
            if (onPointerMovedCb) {
                onPointerMovedCb(lat, lon);
            }
        }
    };

    window.fullScreenMap.setPointerLocation = function (lat, lon) {
        currentPointerLat = lat;
        currentPointerLon = lon;
        if (pointerMarker) {
            pointerMarker.setLngLat([lon, lat]);
        }
    };

    window.fullScreenMap.setGpsActive = function (active) {
        isGpsActive = !!active;
        isLiveTracking = !!active;
        updateLocationMarkerMode();

        if (!isGpsActive && isLoaded && map && map.getSource('live-accuracy-source')) {
            map.getSource('live-accuracy-source').setData({
                type: "Feature",
                geometry: {
                    type: "Polygon",
                    coordinates: [[]]
                }
            });
        }
    };

    window.fullScreenMap.updateLivePosition = function (lat, lon, accuracyM, autoCenter, heading) {
        if (!map) return;
        if (!isGpsActive) return; // Discard any updates if GPS receiver is turned off
        isLiveTracking = true;

        if (liveMarker) {
            liveMarker.setLngLat([lon, lat]);
            if (heading != null && !isNaN(heading)) {
                if (typeof liveMarker.setRotation === 'function') {
                    liveMarker.setRotation(heading);
                } else {
                    const tri = liveMarker.getElement().querySelector('.ph-live-triangle');
                    if (tri) {
                        tri.style.transform = `rotate(${heading}deg)`;
                    }
                }
            }
            updateLocationMarkerMode();
            if (!liveMarker._map) {
                liveMarker.addTo(map);
            }
        }

        // Update accuracy circle
        if (isLoaded && map.getSource('live-accuracy-source')) {
            const circleCoords = createAccuracyCircle(lat, lon, accuracyM);
            map.getSource('live-accuracy-source').setData({
                type: "Feature",
                geometry: {
                    type: "Polygon",
                    coordinates: circleCoords
                }
            });
        }

        if (autoCenter) {
            if (isResettingNorth) {
                // Reset-to-north animation is active: defer camera centering until the map reaches True North
                pendingAutoCenterCoords = [lon, lat];
            } else {
                map.easeTo({
                    center: [lon, lat],
                    duration: 600
                });
            }
        }
    };

    window.fullScreenMap.stopLiveTracking = function () {
        cancelResetNorth();
        isGpsActive = false;
        isLiveTracking = false;
        updateLocationMarkerMode();

        if (isLoaded && map && map.getSource('live-accuracy-source')) {
            map.getSource('live-accuracy-source').setData({
                type: "Feature",
                geometry: {
                    type: "Polygon",
                    coordinates: [[]]
                }
            });
        }
    };

    window.fullScreenMap.resetNorth = function (resetPitch) {
        performResetNorth({ duration: 800 }, null, resetPitch !== false);
    };

    window.fullScreenMap.addTrackPoint = function (lat, lon, ele) {
        recordedPoints.push([lon, lat, ele || 0]);
        if (isLoaded && map && map.getSource('gps-track-source')) {
            map.getSource('gps-track-source').setData({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: recordedPoints
                }
            });
        }
        if (profileCardElement && profileCardElement.style.display !== 'none' && currentProfileTrack && currentProfileTrack.id === 'active_gps_track') {
            window.fullScreenMap.showElevationProfile('active_gps_track');
        }
    };

    window.fullScreenMap.clearTrack = function () {
        recordedPoints = [];
        if (isLoaded && map && map.getSource('gps-track-source')) {
            map.getSource('gps-track-source').setData({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: []
                }
            });
        }
        if (profileCardElement && profileCardElement.style.display !== 'none' && currentProfileTrack && currentProfileTrack.id === 'active_gps_track') {
            window.fullScreenMap.hideElevationProfile();
        }
    };

    window.fullScreenMap.setRecordingState = function (active) {
        isRecordingActive = !!active;
        if (!isRecordingActive) {
            if (profileCardElement && profileCardElement.style.display !== 'none' && currentProfileTrack && currentProfileTrack.id === 'active_gps_track') {
                window.fullScreenMap.hideElevationProfile();
            }
        }
        if (profileCardElement) {
            profileCardElement.style.bottom = isRecordingActive ? '64px' : '16px';
        }
    };

    window.fullScreenMap.displayLoadedTrack = function (coords) {
        if (!coords || !Array.isArray(coords) || coords.length === 0) return;
        recordedPoints = coords.slice();
        if (isLoaded && map && map.getSource('gps-track-source')) {
            map.getSource('gps-track-source').setData({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: recordedPoints
                }
            });
        }

        // Fit map view bounds around the loaded track
        if (map && recordedPoints.length > 0) {
            let minLon = recordedPoints[0][0], maxLon = recordedPoints[0][0];
            let minLat = recordedPoints[0][1], maxLat = recordedPoints[0][1];
            for (let i = 1; i < recordedPoints.length; i++) {
                const pt = recordedPoints[i];
                if (pt[0] < minLon) minLon = pt[0];
                if (pt[0] > maxLon) maxLon = pt[0];
                if (pt[1] < minLat) minLat = pt[1];
                if (pt[1] > maxLat) maxLat = pt[1];
            }

            if (Math.abs(maxLon - minLon) < 0.0001 && Math.abs(maxLat - minLat) < 0.0001) {
                map.flyTo({ center: [minLon, minLat], zoom: 14 });
            } else {
                map.fitBounds([
                    [minLon, minLat],
                    [maxLon, maxLat]
                ], { padding: 50, maxZoom: 16 });
            }
        }
    };

    let configuredPmTiles = [];
    let activePmTileLayerIds = [];
    let activePmTileSourceIds = [];
    let isPadUsVisible = false;

    function clearPmTilesLayers() {
        if (!map) return;
        activePmTileLayerIds.forEach(id => {
            if (map.getLayer(id)) {
                try { map.removeLayer(id); } catch (e) { }
            }
        });
        activePmTileLayerIds = [];

        activePmTileSourceIds.forEach(sid => {
            if (map.getSource(sid)) {
                try { map.removeSource(sid); } catch (e) { }
            }
        });
        activePmTileSourceIds = [];

        for (let i = 0; i < 30; i++) {
            const sid = `fs-pmtiles-source-${i}`;
            if (map.getSource(sid)) {
                try { map.removeSource(sid); } catch (e) { }
            }
        }

        if (map && typeof map.getTerrain === 'function' && map.getTerrain()) {
            try { map.setTerrain(null); } catch (e) { }
        }
        if (map && typeof map.setSky === 'function') {
            try { map.setSky(null); } catch (e) { }
        }
        activeTerrainSourceId = null;
    }

    window.fullScreenMap.setPmTilesLayers = async function (config) {
        if (window.fullScreenMap && window.fullScreenMap.isPmTilesSupported === false) {
            clearPmTilesLayers();
            return;
        }

        let layers = [];
        let styleJson = null;
        let styleFileName = null;
        let styleUrl = null;
        let tileServerBase = 'http://127.0.0.1:18234/';

        if (Array.isArray(config)) {
            layers = config;
        } else if (config && typeof config === 'object') {
            layers = config.layers || [];
            styleJson = config.styleJson || null;
            styleFileName = config.styleFileName || null;
            styleUrl = config.styleUrl || null;
            tileServerBase = config.tileServerBase || tileServerBase;
        }

        configuredPmTiles = layers;
        pendingPmTilesConfig = config;
        if (!map || !isLoaded) {
            console.info("[FullScreenMap] setPmTilesLayers saved as pending (map not loaded yet)");
            return;
        }
        clearPmTilesLayers();

        const enabledLayers = configuredPmTiles.filter(l => l.isEnabled !== false);
        if (enabledLayers.length === 0) return;

        // Try to parse style JSON if a style file was selected
        let styleObj = null;
        if (styleFileName) {
            if (styleJson && typeof styleJson === 'string' && styleJson.trim().length > 0) {
                try {
                    styleObj = JSON.parse(styleJson);
                } catch (e) {
                    console.warn("[FullScreenMap] Failed to parse styleJson string:", e);
                }
            }
            if (!styleObj && styleUrl) {
                try {
                    const resp = await fetch(styleUrl);
                    if (resp.ok) {
                        const text = await resp.text();
                        if (text && text.trim().length > 0) {
                            styleObj = JSON.parse(text);
                        }
                    }
                } catch (e) {
                    console.warn("[FullScreenMap] Failed to fetch or parse styleUrl:", e);
                }
            }
        }

        // Anchor layer beneath which PMTiles are rendered (so UI overlays like live accuracy, drawn route, imported tracks, and PAD-US public lands overlay stay on top)
        const anchorLayer = map.getLayer('padus-layer')
            ? 'padus-layer'
            : (map.getLayer('imported-tracks-casing')
                ? 'imported-tracks-casing'
                : (map.getLayer('route-draft-casing')
                    ? 'route-draft-casing'
                    : (map.getLayer('live-accuracy-fill') ? 'live-accuracy-fill' : undefined)));

        // Track PMTile files consumed by style sources
        const consumedFileNames = new Set();

        if (styleObj && styleObj.layers && styleObj.layers.length > 0) {
            console.info("[FullScreenMap] Applying custom PMTiles style file:", styleFileName);

            // Register glyphs & sprite if specified in the style
            if (styleObj.glyphs && map.style) {
                try {
                    map.style.glyphs = styleObj.glyphs;
                    if (map.style.stylesheet) map.style.stylesheet.glyphs = styleObj.glyphs;
                    if (map.style.glyphManager && typeof map.style.glyphManager.setURL === 'function') {
                        map.style.glyphManager.setURL(styleObj.glyphs);
                    }
                } catch (e) { }
            }
            if (styleObj.sprite && map.style) {
                try {
                    map.style.sprite = styleObj.sprite;
                    if (map.style.stylesheet) map.style.stylesheet.sprite = styleObj.sprite;
                } catch (e) { }
            }

            // If style defines a zoom level (e.g. zoom: 13 for trail visibility),
            // respect it if current map zoom is below style zoom and map is not live-tracking
            if (typeof styleObj.zoom === 'number') {
                if (map && map.getZoom() < styleObj.zoom && !isLiveTracking) {
                    map.setZoom(styleObj.zoom);
                }
            }

            // Track matched PMTiles files and discovered vector layers for each source ID
            const sourceMatchedLayer = {};
            const sourceVectorLayers = {};

            // Process and add sources defined in styleObj
            const sources = styleObj.sources || {};
            for (const [sId, srcDef] of Object.entries(sources)) {
                try {
                    const sCopy = JSON.parse(JSON.stringify(srcDef));
                    let resolvedUrl = null;
                    let matchedLayer = null;

                    // 1. Direct match with an enabled PMTiles layer
                    for (const l of enabledLayers) {
                        const lName = l.fileName.toLowerCase();
                        const lStem = lName.replace(/\.pmtiles$/i, '');
                        if (sCopy.url && typeof sCopy.url === 'string') {
                            if (sCopy.url.toLowerCase().includes(lName) || sCopy.url.toLowerCase().includes(lStem)) {
                                resolvedUrl = `pmtiles://${l.url}`;
                                matchedLayer = l;
                                consumedFileNames.add(l.fileName);
                                break;
                            }
                        }
                        if (sId.toLowerCase() === lStem || sId.toLowerCase().includes(lStem) || lStem.includes(sId.toLowerCase())) {
                            resolvedUrl = `pmtiles://${l.url}`;
                            matchedLayer = l;
                            consumedFileNames.add(l.fileName);
                            break;
                        }
                    }

                    // 2. Semantic role matching (universal style resolution across states)
                    if (!resolvedUrl) {
                        const sIdLower = sId.toLowerCase();
                        const sUrlLower = (sCopy.url || '').toLowerCase();
                        const isDemRole = sCopy.type === 'raster-dem' || sIdLower.includes('terrain') || sIdLower.includes('dem') || sIdLower.includes('rgb') || sUrlLower.includes('terrain') || sUrlLower.includes('dem') || sUrlLower.includes('rgb');
                        const isHillshadeRole = sCopy.type === 'raster' || sIdLower.includes('hillshade') || sIdLower.includes('shade') || sUrlLower.includes('hillshade') || sUrlLower.includes('shade');
                        const isContourRole = (sCopy.type === 'vector' && (sIdLower.includes('contour') || sIdLower.includes('elevation') || sUrlLower.includes('contour') || sUrlLower.includes('elevation')));
                        const isBaseRole = (sCopy.type === 'vector' && !isContourRole);

                        if (isDemRole) {
                            matchedLayer = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && (l.fileName.toLowerCase().includes('terrain') || l.fileName.toLowerCase().includes('dem') || l.fileName.toLowerCase().includes('rgb'))) ||
                                enabledLayers.find(l => l.fileName.toLowerCase().includes('terrain') || l.fileName.toLowerCase().includes('dem') || l.fileName.toLowerCase().includes('rgb'));
                        } else if (isHillshadeRole) {
                            matchedLayer = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && (l.fileName.toLowerCase().includes('hillshade') || l.fileName.toLowerCase().includes('shade'))) ||
                                enabledLayers.find(l => l.fileName.toLowerCase().includes('hillshade') || l.fileName.toLowerCase().includes('shade'));
                        } else if (isContourRole) {
                            matchedLayer = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && l.fileName.toLowerCase().includes('contour')) ||
                                enabledLayers.find(l => l.fileName.toLowerCase().includes('contour'));
                        } else if (isBaseRole) {
                            matchedLayer = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && (l.fileName.toLowerCase().includes('base') || l.fileName.toLowerCase().includes('osm') || l.fileName.toLowerCase().includes('map')) && !l.fileName.toLowerCase().includes('contour') && !l.fileName.toLowerCase().includes('hillshade')) ||
                                enabledLayers.find(l => !consumedFileNames.has(l.fileName) && !l.fileName.toLowerCase().includes('contour') && !l.fileName.toLowerCase().includes('hillshade') && !l.fileName.toLowerCase().includes('raster') && !l.fileName.toLowerCase().includes('terrain') && !l.fileName.toLowerCase().includes('dem')) ||
                                enabledLayers.find(l => !l.fileName.toLowerCase().includes('contour') && !l.fileName.toLowerCase().includes('hillshade') && !l.fileName.toLowerCase().includes('raster'));
                        }

                        if (matchedLayer) {
                            resolvedUrl = `pmtiles://${matchedLayer.url}`;
                            consumedFileNames.add(matchedLayer.fileName);
                        }
                    }

                    // 3. Fallback by raw type if still unresolved
                    if (!resolvedUrl) {
                        if (sCopy.type === 'vector') {
                            const vec = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && !l.fileName.toLowerCase().includes('hillshade') && !l.fileName.toLowerCase().includes('raster')) ||
                                enabledLayers.find(l => !l.fileName.toLowerCase().includes('hillshade') && !l.fileName.toLowerCase().includes('raster')) ||
                                enabledLayers[0];
                            if (vec) {
                                resolvedUrl = `pmtiles://${vec.url}`;
                                matchedLayer = vec;
                                consumedFileNames.add(vec.fileName);
                            }
                        } else if (sCopy.type === 'raster') {
                            const ras = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && (l.fileName.toLowerCase().includes('hillshade') || l.fileName.toLowerCase().includes('raster'))) ||
                                enabledLayers.find(l => l.fileName.toLowerCase().includes('hillshade') || l.fileName.toLowerCase().includes('raster')) ||
                                enabledLayers[0];
                            if (ras) {
                                resolvedUrl = `pmtiles://${ras.url}`;
                                matchedLayer = ras;
                                consumedFileNames.add(ras.fileName);
                            }
                        } else if (sCopy.type === 'raster-dem') {
                            const dem = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && (l.fileName.toLowerCase().includes('terrain') || l.fileName.toLowerCase().includes('dem') || l.fileName.toLowerCase().includes('rgb'))) ||
                                enabledLayers.find(l => l.fileName.toLowerCase().includes('terrain') || l.fileName.toLowerCase().includes('dem') || l.fileName.toLowerCase().includes('rgb'));
                            if (dem) {
                                resolvedUrl = `pmtiles://${dem.url}`;
                                matchedLayer = dem;
                                consumedFileNames.add(dem.fileName);
                            }
                        }
                    }

                    if (matchedLayer) {
                        sourceMatchedLayer[sId] = matchedLayer;
                    }

                    // For vector sources, inspect metadata to discover vector layers (e.g. 'contours' vs 'az_contours' vs 'ut_contours')
                    if (sCopy.type === 'vector' && matchedLayer && window.pmtiles) {
                        try {
                            const p = new window.pmtiles.PMTiles(matchedLayer.url);
                            const meta = await p.getMetadata();
                            let vl = meta && (meta.vector_layers || (meta.tilestats && meta.tilestats.layers));
                            if (typeof vl === 'string') {
                                try { vl = JSON.parse(vl); } catch (e) { }
                            }
                            if (Array.isArray(vl) && vl.length > 0) {
                                sourceVectorLayers[sId] = vl.map(v => (typeof v === 'string') ? v : (v.id || v.layer || v.name)).filter(Boolean);
                            }
                        } catch (metaErr) { }
                    }

                    if (resolvedUrl) {
                        sCopy.url = resolvedUrl;
                        delete sCopy.tiles;
                    } else if (sCopy.url) {
                        let cleanUrl = sCopy.url.replace(/^pmtiles:\/\//i, '');
                        if (!cleanUrl.startsWith('http://') && !cleanUrl.startsWith('https://')) {
                            sCopy.url = `pmtiles://${tileServerBase}pmtiles/${cleanUrl}`;
                        } else {
                            sCopy.url = `pmtiles://${cleanUrl}`;
                        }
                        delete sCopy.tiles;
                    }

                    if (sCopy.type === 'raster' && !sCopy.tileSize) {
                        sCopy.tileSize = 256;
                    }

                    if (sCopy.type === 'raster-dem') {
                        if (!sCopy.tileSize) sCopy.tileSize = 256;
                        if (!sCopy.encoding) sCopy.encoding = 'mapbox';
                        activeTerrainSourceId = sId;
                    }

                    if (!map.getSource(sId)) {
                        map.addSource(sId, sCopy);
                        activePmTileSourceIds.push(sId);
                    }
                } catch (srcErr) {
                    console.warn("[FullScreenMap] Error adding style source:", sId, srcErr);
                }
            }

            // Apply sky from style root if specified
            if (styleObj.sky && typeof map.setSky === 'function') {
                try {
                    map.setSky(styleObj.sky);
                } catch (skyErr) {
                    console.warn("[FullScreenMap] Error applying root sky property:", skyErr);
                }
            }

            const validMapLibreLayerTypes = ['fill', 'line', 'symbol', 'circle', 'heatmap', 'fill-extrusion', 'raster', 'hillshade', 'background'];

            // Add style layers
            for (const lDef of styleObj.layers) {
                if (!lDef || !lDef.id) continue;
                try {
                    const lCopy = JSON.parse(JSON.stringify(lDef));
                    lCopy.layout = lCopy.layout || {};
                    lCopy.layout.visibility = (currentSelectedBaseLayer === 'pmtiles') ? 'visible' : 'none';

                    // If layer is type 'sky', apply it via map.setSky instead of attempting map.addLayer
                    if (lCopy.type === 'sky') {
                        if (typeof map.setSky === 'function') {
                            try {
                                map.setSky(lCopy.paint || {});
                            } catch (skyErr) {
                                console.warn("[FullScreenMap] Error applying sky layer paint via setSky:", skyErr);
                            }
                        }
                        continue;
                    }

                    // Skip any layer types unsupported by MapLibre (e.g. Mapbox custom types like 'model', 'slot', etc.)
                    if (!validMapLibreLayerTypes.includes(lCopy.type)) {
                        console.warn("[FullScreenMap] Skipping unsupported layer type:", lCopy.id, lCopy.type);
                        continue;
                    }

                    // All MapLibre layers except 'background' require an existing source
                    if (lCopy.type !== 'background') {
                        if (!lCopy.source || !map.getSource(lCopy.source)) {
                            continue;
                        }
                    }

                    // Automatically reconcile vector source-layer if needed (e.g. "az_contours" vs "contours" vs "ut_contours")
                    if (lCopy['source-layer'] && lCopy.source && sourceVectorLayers[lCopy.source]) {
                        const availableVectorLayers = sourceVectorLayers[lCopy.source];
                        if (availableVectorLayers.length > 0 && !availableVectorLayers.includes(lCopy['source-layer'])) {
                            if (availableVectorLayers.length === 1) {
                                lCopy['source-layer'] = availableVectorLayers[0];
                            } else {
                                const matchingVl = availableVectorLayers.find(v => v.toLowerCase() === lCopy['source-layer'].toLowerCase()) ||
                                    availableVectorLayers.find(v => v.toLowerCase().includes('contour') && lCopy['source-layer'].toLowerCase().includes('contour'));
                                if (matchingVl) {
                                    lCopy['source-layer'] = matchingVl;
                                }
                            }
                        }
                    }

                    // Remove unsupported paint properties that cause MapLibre validator errors
                    if (lCopy.paint && lCopy.paint['raster-blend-mode']) {
                        delete lCopy.paint['raster-blend-mode'];
                    }

                    // Auto-fix empty operator in filter if present
                    if (Array.isArray(lCopy.filter) && lCopy.filter[0] === '') {
                        lCopy.filter[0] = '==';
                    }

                    // Ensure glyphs are available before adding any symbol layer with text-field
                    if (lCopy.layout && lCopy.layout['text-field']) {
                        const glyphUrl = (styleObj && styleObj.glyphs) || "https://demotiles.maplibre.org/font/{fontstack}/{range}.pbf";
                        if (!map.style.stylesheet) map.style.stylesheet = {};
                        if (!map.style.stylesheet.glyphs) map.style.stylesheet.glyphs = glyphUrl;
                        if (!map.style.glyphs) map.style.glyphs = glyphUrl;
                        if (map.style.glyphManager && typeof map.style.glyphManager.setURL === 'function') {
                            map.style.glyphManager.setURL(glyphUrl);
                        }
                    }

                    if (map.getLayer(lCopy.id)) {
                        map.removeLayer(lCopy.id);
                    }

                    map.addLayer(lCopy, anchorLayer);
                    activePmTileLayerIds.push(lCopy.id);
                } catch (lErr) {
                    console.warn("[FullScreenMap] Error adding style layer:", lDef.id, lErr);
                }
            }

            // Apply 3D terrain from style if defined, or from detected raster-dem source
            if (styleObj.terrain && styleObj.terrain.source && map.getSource(styleObj.terrain.source)) {
                activeTerrainSourceId = styleObj.terrain.source;
                if (map.getPitch() > 20) {
                    try {
                        map.setTerrain({
                            source: styleObj.terrain.source,
                            exaggeration: styleObj.terrain.exaggeration != null ? styleObj.terrain.exaggeration : 1.25
                        });
                        console.info("[FullScreenMap] 3D Terrain enabled from style:", styleObj.terrain.source);
                    } catch (tErr) {
                        console.warn("[FullScreenMap] Error applying style terrain:", tErr);
                    }
                }
            } else if (activeTerrainSourceId && map.getSource(activeTerrainSourceId)) {
                if (map.getPitch() > 20) {
                    try {
                        map.setTerrain({ source: activeTerrainSourceId, exaggeration: 1.25 });
                        console.info("[FullScreenMap] 3D Terrain enabled from source:", activeTerrainSourceId);
                    } catch (tErr) {
                        console.warn("[FullScreenMap] Error applying terrain source:", tErr);
                    }
                }
            }
        }

        // Render any remaining enabled PMTiles layers that weren't consumed by the style
        // (or all of them if no style file was selected or style had no layers)
        const unconsumedLayers = enabledLayers.filter(l => !consumedFileNames.has(l.fileName));

        for (let i = 0; i < unconsumedLayers.length; i++) {
            const l = unconsumedLayers[i];
            const sourceId = `fs-pmtiles-source-${i}`;
            const layerId = `fs-pmtiles-layer-${i}`;

            try {
                const p = new window.pmtiles.PMTiles(l.url);
                const header = await p.getHeader();
                const isVector = (header.tileType === 1);

                if (isVector) {
                    if (!map.getSource(sourceId)) {
                        map.addSource(sourceId, {
                            type: 'vector',
                            url: `pmtiles://${l.url}`
                        });
                        activePmTileSourceIds.push(sourceId);
                    }

                    let meta = null;
                    try { meta = await p.getMetadata(); } catch (e) { }

                    let vectorLayerNames = [];
                    if (meta) {
                        let rawVl = meta.vector_layers;
                        if (typeof rawVl === 'string') {
                            try { rawVl = JSON.parse(rawVl); } catch (e) { }
                        }
                        if (!rawVl && meta.json) {
                            try {
                                const parsedJson = (typeof meta.json === 'string') ? JSON.parse(meta.json) : meta.json;
                                rawVl = parsedJson.vector_layers || parsedJson.layers;
                            } catch (e) { }
                        }
                        if (!rawVl && meta.tilestats && meta.tilestats.layers) {
                            rawVl = meta.tilestats.layers;
                        }

                        if (Array.isArray(rawVl) && rawVl.length > 0) {
                            rawVl.forEach(v => {
                                const id = (typeof v === 'string') ? v : (v.id || v.layer || v.name);
                                if (id && !vectorLayerNames.includes(id)) {
                                    vectorLayerNames.push(id);
                                }
                            });
                        }
                    }

                    // Fallback to name/stem if vector_layers was not found in metadata
                    if (vectorLayerNames.length === 0) {
                        const stem = (l.fileName || '').replace(/\.pmtiles$/i, '');
                        const candidates = [];
                        if (meta && meta.name) candidates.push(meta.name);
                        if (stem) candidates.push(stem);
                        if (stem.includes('contour')) candidates.push('az_contours', 'contours', 'contour');
                        candidates.forEach(c => {
                            if (c && !vectorLayerNames.includes(c)) vectorLayerNames.push(c);
                        });
                        if (vectorLayerNames.length === 0) {
                            vectorLayerNames.push('default');
                        }
                    }

                    vectorLayerNames.forEach((layerName, idx) => {
                        const vlId = `${layerId}-${layerName}-${idx}`;
                        if (!map.getLayer(vlId)) {
                            try {
                                map.addLayer({
                                    id: vlId,
                                    source: sourceId,
                                    'source-layer': layerName,
                                    type: 'line',
                                    paint: {
                                        'line-color': '#475569',
                                        'line-width': 1.5,
                                        'line-opacity': l.opacity != null ? l.opacity : 1.0
                                    },
                                    layout: {
                                        visibility: currentSelectedBaseLayer === 'pmtiles' ? 'visible' : 'none'
                                    }
                                }, anchorLayer);
                                activePmTileLayerIds.push(vlId);
                            } catch (lErr) {
                                console.warn("[FullScreenMap] Could not add layer for source-layer:", layerName, lErr);
                            }
                        }
                    });
                } else {
                    if (!map.getSource(sourceId)) {
                        map.addSource(sourceId, {
                            type: 'raster',
                            url: `pmtiles://${l.url}`,
                            tileSize: 256
                        });
                        activePmTileSourceIds.push(sourceId);
                    }

                    if (!map.getLayer(layerId)) {
                        map.addLayer({
                            id: layerId,
                            source: sourceId,
                            type: 'raster',
                            paint: {
                                'raster-opacity': l.opacity != null ? l.opacity : 1.0
                            },
                            layout: {
                                visibility: currentSelectedBaseLayer === 'pmtiles' ? 'visible' : 'none'
                            }
                        }, anchorLayer);
                        activePmTileLayerIds.push(layerId);
                    }
                }
            } catch (err) {
                console.warn(`[FullScreenMap] Skipping PMTiles layer "${l.fileName}":`, err.message || err);
            }
        }

        ensurePadUsLayerPosition();
        if (map.getLayer('padus-layer')) {
            map.setLayoutProperty('padus-layer', 'visibility', isPadUsVisible ? 'visible' : 'none');
            map.setPaintProperty('padus-layer', 'raster-opacity', 0.35);
        }
    };

    function ensurePadUsLayerPosition() {
        if (!map || !map.getLayer('padus-layer')) return;
        const trackAnchor = map.getLayer('imported-tracks-casing')
            ? 'imported-tracks-casing'
            : (map.getLayer('route-draft-casing')
                ? 'route-draft-casing'
                : (map.getLayer('live-accuracy-fill') ? 'live-accuracy-fill' : undefined));
        try {
            if (trackAnchor && map.getLayer(trackAnchor)) {
                map.moveLayer('padus-layer', trackAnchor);
            } else {
                map.moveLayer('padus-layer');
            }
        } catch (e) {
            console.warn("[FullScreenMap] Error positioning padus-layer:", e);
        }
    }

    window.fullScreenMap.setPadUsVisible = function (visible) {
        isPadUsVisible = !!visible;
        if (map && map.getLayer('padus-layer')) {
            map.setLayoutProperty('padus-layer', 'visibility', isPadUsVisible ? 'visible' : 'none');
            map.setPaintProperty('padus-layer', 'raster-opacity', 0.35);
            if (isPadUsVisible) {
                ensurePadUsLayerPosition();
            }
        }
    };

    window.fullScreenMap.setBaseLayer = async function (layerName) {
        if (!map || !isLoaded) {
            currentSelectedBaseLayer = layerName;
            try { localStorage.setItem('pointedhorizons_last_map_layer', layerName); } catch (e) { }
            return;
        }
        if (layerName === 'pmtiles' && window.fullScreenMap && window.fullScreenMap.isPmTilesSupported === false) {
            layerName = 'topo';
        }
        currentSelectedBaseLayer = layerName;
        try { localStorage.setItem('pointedhorizons_last_map_layer', layerName); } catch (e) { }

        const baseRasterLayers = [
            'topo-layer',
            'osm-layer',
            'satellite-layer',
            'quads-layer',
            'opentopo-layer',
            'hybrid-reference-layer'
        ];
        baseRasterLayers.forEach(id => {
            if (map.getLayer(id)) {
                map.setLayoutProperty(id, 'visibility', 'none');
            }
        });

        if (layerName === 'pmtiles') {
            // If PMTiles layers have not been loaded yet, load them now!
            if (activePmTileLayerIds.length === 0 && pendingPmTilesConfig) {
                try {
                    await window.fullScreenMap.setPmTilesLayers(pendingPmTilesConfig);
                } catch (e) {
                    console.warn("[FullScreenMap] Error loading pending PMTiles in setBaseLayer:", e);
                }
            }

            activePmTileLayerIds.forEach(id => {
                if (map.getLayer(id)) {
                    map.setLayoutProperty(id, 'visibility', 'visible');
                }
            });
            ensurePadUsLayerPosition();
            if (map.getLayer('padus-layer')) {
                map.setLayoutProperty('padus-layer', 'visibility', isPadUsVisible ? 'visible' : 'none');
            }
            return;
        }

        // Hide PMTiles
        activePmTileLayerIds.forEach(id => {
            if (map.getLayer(id)) {
                map.setLayoutProperty(id, 'visibility', 'none');
            }
        });

        if (layerName === 'streets' || layerName === 'osm') {
            if (map.getLayer('osm-layer')) map.setLayoutProperty('osm-layer', 'visibility', 'visible');
        } else if (layerName === 'satellite') {
            if (map.getLayer('satellite-layer')) map.setLayoutProperty('satellite-layer', 'visibility', 'visible');
        } else if (layerName === 'quads') {
            if (map.getLayer('quads-layer')) map.setLayoutProperty('quads-layer', 'visibility', 'visible');
        } else if (layerName === 'opentopo') {
            if (map.getLayer('opentopo-layer')) map.setLayoutProperty('opentopo-layer', 'visibility', 'visible');
        } else if (layerName === 'hybrid') {
            if (map.getLayer('satellite-layer')) map.setLayoutProperty('satellite-layer', 'visibility', 'visible');
            if (map.getLayer('hybrid-reference-layer')) map.setLayoutProperty('hybrid-reference-layer', 'visibility', 'visible');
        } else {
            if (map.getLayer('topo-layer')) map.setLayoutProperty('topo-layer', 'visibility', 'visible');
        }
    };

    function recreateMap(lat, lon) {
        if (!mapContainerElement) return;
        cancelResetNorth();
        try {
            if (map) {
                try { map.remove(); } catch (e) { }
                map = null;
            }
        } catch (e) { }

        try {
            mapContainerElement.innerHTML = '';
        } catch (e) { }

        isLoaded = false;
        const initLat = (typeof lat === 'number' && !isNaN(lat)) ? lat : currentPointerLat;
        const initLon = (typeof lon === 'number' && !isNaN(lon)) ? lon : currentPointerLon;

        window.fullScreenMap.init(
            mapContainerElement,
            { lat: initLat, lon: initLon, zoom: 13 },
            onPointerMovedCb
        );
        if (pendingPmTilesConfig) {
            window.fullScreenMap.setPmTilesLayers(pendingPmTilesConfig);
        }
        if (currentSelectedBaseLayer) {
            window.fullScreenMap.setBaseLayer(currentSelectedBaseLayer);
        }
        if (allImportedTracks && allImportedTracks.length > 0) {
            refreshImportedTracksInView();
        }
        if (routeWaypoints && routeWaypoints.length > 0) {
            updateRouteDisplay();
        }
        if (isRouteDrawingActive && map && map.getCanvas()) {
            map.getCanvas().style.cursor = 'crosshair';
        }
    }

    function recoverMapState(centerLon, centerLat, zoom) {
        let targetLon = (typeof centerLon === 'number' && !isNaN(centerLon)) ? centerLon : currentPointerLon;
        let targetLat = (typeof centerLat === 'number' && !isNaN(centerLat)) ? centerLat : currentPointerLat;
        if (typeof targetLon !== 'number' || isNaN(targetLon) || typeof targetLat !== 'number' || isNaN(targetLat)) {
            targetLon = -110.9747;
            targetLat = 32.2226;
        }

        if (!map) {
            recreateMap(targetLat, targetLon);
            return;
        }

        try {
            // 1. Immediately halt any active momentum / ease animations
            map.stop();

            // 2. Unload the 3D elevation mesh
            try { map.setTerrain(null); } catch (e) { }

            // 3. Force jump to safe top-down 2D
            map.jumpTo({
                center: [targetLon, targetLat],
                zoom: (typeof zoom === 'number' && !isNaN(zoom)) ? zoom : 13,
                pitch: 0,
                bearing: 0
            });

            // 4. If map transform state is still degenerate or NaN, recreate the map
            const center = map.getCenter();
            const pitch = map.getPitch();
            if (!center || isNaN(center.lat) || isNaN(center.lng) || isNaN(pitch) || pitch !== 0) {
                console.warn("[FullScreenMap] Map transform state corrupt after jumpTo; recreating map");
                recreateMap(targetLat, targetLon);
                return;
            }

            // 5. Force canvas resize and repaint
            setTimeout(function () {
                try {
                    map.resize();
                    map.triggerRepaint();
                } catch (e) {
                    recreateMap(targetLat, targetLon);
                }
            }, 50);
        } catch (err) {
            console.warn("[FullScreenMap] Error in recoverMapState, recreating map:", err);
            recreateMap(targetLat, targetLon);
        }
    }

    window.fullScreenMap.toggle3d = function (force2d) {
        if (!map) {
            recreateMap(currentPointerLat, currentPointerLon);
            return false;
        }
        cancelResetNorth();
        let currentPitch = map.getPitch();
        let center = map.getCenter();
        const isDegenerate = isNaN(currentPitch) || !center || isNaN(center.lat) || isNaN(center.lng);
        if (isDegenerate) {
            console.warn("[FullScreenMap] Map is degenerate in toggle3d; recreating clean 2D map");
            recreateMap(currentPointerLat, currentPointerLon);
            return false;
        }

        const isCurrently3d = (force2d === true) ? true : (force2d === false ? false : (currentPitch > 20));

        if (isCurrently3d) {
            // Toggling 3D OFF: remove terrain mesh and ease cleanly to flat 2D
            recoverMapState(currentPointerLon, currentPointerLat, null);
            return false;
        } else {
            // Toggling 3D ON: enable terrain and pitch
            if (activeTerrainSourceId && map.getSource(activeTerrainSourceId)) {
                try {
                    map.setTerrain({ source: activeTerrainSourceId, exaggeration: 1.25 });
                } catch (e) { }
            }
            map.easeTo({
                pitch: 65,
                duration: 800
            });
            return true;
        }
    };

    function escapeHtml(text) {
        if (!text) return "";
        return String(text)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    let currentWaypointPopup = null;

    function showWaypointPopup(feature) {
        if (!map || !feature) return;

        if (currentWaypointPopup) {
            try { currentWaypointPopup.remove(); } catch (e) { }
            currentWaypointPopup = null;
        }

        const props = feature.properties || {};
        const coords = feature.geometry && feature.geometry.coordinates;
        if (!coords || coords.length < 2) return;

        const lon = coords[0];
        const lat = coords[1];
        const name = props.name || "Waypoint";
        const desc = props.desc || "";
        const ele = (props.ele !== undefined && props.ele !== null && props.ele !== "" && !isNaN(props.ele))
            ? Number(props.ele)
            : null;
        const time = props.time || "";

        let html = '<div class="ph-waypoint-popup-content">';
        html += '<div class="ph-wpt-header">';
        html += '<span class="ph-wpt-icon">📌</span>';
        html += '<span class="ph-wpt-title">' + escapeHtml(name) + '</span>';
        html += '</div>';

        if (desc) {
            html += '<div class="ph-wpt-desc">' + escapeHtml(desc) + '</div>';
        }

        html += '<div class="ph-wpt-meta">';
        html += '<div class="ph-wpt-coord">' + lat.toFixed(5) + '°, ' + lon.toFixed(5) + '°</div>';

        if (ele !== null) {
            const eleM = Math.round(ele);
            const eleFt = Math.round(ele * 3.28084);
            html += '<div class="ph-wpt-ele">⛰️ ' + eleM + ' m (' + eleFt + ' ft)</div>';
        }

        if (time) {
            html += '<div class="ph-wpt-time">🕒 ' + escapeHtml(time) + '</div>';
        }

        html += '</div>';

        const trackIdStr = String(props.trackId || '');
        const wptIdStr = String(props.id || '');
        html += '<div class="ph-wpt-actions">';
        html += '<button type="button" class="ph-wpt-edit-btn" onclick="window.fullScreenMap.onEditWaypointClicked(\'' + escapeHtml(trackIdStr) + '\', \'' + escapeHtml(wptIdStr) + '\')">✏️ Edit Waypoint</button>';
        html += '</div>';

        html += '</div>';

        currentWaypointPopup = new window.maplibregl.Popup({
            closeButton: true,
            closeOnClick: true,
            offset: 12,
            className: 'ph-waypoint-custom-popup ' + (isDarkMode ? 'ph-dark' : 'ph-light'),
            maxWidth: '320px'
        })
            .setLngLat([lon, lat])
            .setHTML(html)
            .addTo(map);
    }

    window.fullScreenMap.setEditWaypointCallback = function (cb) {
        onEditWaypointCb = cb;
    };

    window.fullScreenMap.onEditWaypointClicked = function (trackId, waypointId) {
        if (currentWaypointPopup) {
            try { currentWaypointPopup.remove(); } catch (e) { }
            currentWaypointPopup = null;
        }
        if (onEditWaypointCb) {
            onEditWaypointCb(trackId, waypointId);
        }
    };

    function refreshImportedTracksInView() {
        if (!map || !isLoaded) return;
        const source = map.getSource('imported-tracks-source');
        const wptSource = map.getSource('imported-waypoints-source');
        if (!source) return;

        if (!allImportedTracks || allImportedTracks.length === 0) {
            source.setData({
                type: "FeatureCollection",
                features: []
            });
            if (wptSource) {
                wptSource.setData({
                    type: "FeatureCollection",
                    features: []
                });
            }
            return;
        }

        let bounds;
        try {
            bounds = map.getBounds();
        } catch (e) {
            return;
        }
        if (!bounds) return;

        const west = bounds.getWest();
        const east = bounds.getEast();
        const south = bounds.getSouth();
        const north = bounds.getNorth();

        // 30% viewport buffer to eliminate popping during pan/zoom
        const padLon = Math.abs(east - west) * 0.3;
        const padLat = Math.abs(north - south) * 0.3;
        const minViewLon = Math.min(west, east) - padLon;
        const maxViewLon = Math.max(west, east) + padLon;
        const minViewLat = Math.min(south, north) - padLat;
        const maxViewLat = Math.max(south, north) + padLat;

        const visibleFeatures = [];
        const visibleWaypoints = [];

        for (let i = 0; i < allImportedTracks.length; i++) {
            const t = allImportedTracks[i];
            if (!t || t.isVisible === false) continue;

            // Fast bounding box intersection test against buffered viewport
            const overlaps = (t.maxLon >= minViewLon && t.minLon <= maxViewLon &&
                t.maxLat >= minViewLat && t.minLat <= maxViewLat);
            if (!overlaps) continue;

            // 1. Regular Track Line
            if (t.coordinates && t.coordinates.length >= 2) {
                visibleFeatures.push({
                    type: "Feature",
                    properties: {
                        id: t.id,
                        trackId: t.id,
                        name: t.name || t.fileName || "Track",
                        color: t.color || "#3B82F6",
                        isRoute: false
                    },
                    geometry: {
                        type: "LineString",
                        coordinates: t.coordinates
                    }
                });
            }

            // 2. Routes (<rte>)
            if (t.routes && t.routes.length > 0) {
                for (let r = 0; r < t.routes.length; r++) {
                    const rte = t.routes[r];
                    if (rte && rte.coordinates && rte.coordinates.length >= 2) {
                        visibleFeatures.push({
                            type: "Feature",
                            properties: {
                                id: rte.id || (t.id + '_rte_' + r),
                                trackId: t.id,
                                name: rte.name || "Route",
                                desc: rte.desc || "",
                                color: t.color || "#F59E0B",
                                isRoute: true
                            },
                            geometry: {
                                type: "LineString",
                                coordinates: rte.coordinates
                            }
                        });
                    }
                }
            }

            // 3. Waypoints (<wpt>)
            if (t.waypoints && t.waypoints.length > 0) {
                for (let w = 0; w < t.waypoints.length; w++) {
                    const wpt = t.waypoints[w];
                    if (!wpt || typeof wpt.lat !== 'number' || typeof wpt.lon !== 'number') continue;
                    if (wpt.lon >= minViewLon && wpt.lon <= maxViewLon &&
                        wpt.lat >= minViewLat && wpt.lat <= maxViewLat) {
                        visibleWaypoints.push({
                            type: "Feature",
                            properties: {
                                id: wpt.id || (t.id + '_wpt_' + w),
                                trackId: t.id,
                                name: wpt.name || "Waypoint",
                                desc: wpt.desc || "",
                                sym: wpt.sym || "",
                                ele: (wpt.ele !== undefined && wpt.ele !== null) ? wpt.ele : null,
                                time: wpt.time || "",
                                color: t.color || "#EF4444"
                            },
                            geometry: {
                                type: "Point",
                                coordinates: [wpt.lon, wpt.lat]
                            }
                        });
                    }
                }
            }
        }

        source.setData({
            type: "FeatureCollection",
            features: visibleFeatures
        });

        if (wptSource) {
            wptSource.setData({
                type: "FeatureCollection",
                features: visibleWaypoints
            });
        }
    }

    window.fullScreenMap.setAvailableTracks = function (tracksData) {
        if (typeof tracksData === 'string') {
            try {
                allImportedTracks = JSON.parse(tracksData);
            } catch (e) {
                console.warn("[FullScreenMap] Failed to parse tracksData JSON:", e);
                allImportedTracks = [];
            }
        } else if (Array.isArray(tracksData)) {
            allImportedTracks = tracksData;
        } else {
            allImportedTracks = [];
        }
        refreshImportedTracksInView();
    };

    window.fullScreenMap.zoomToTrack = function (minLon, minLat, maxLon, maxLat, trackId) {
        if (!map) return;
        cancelResetNorth();
        if (typeof minLon !== 'number' || typeof minLat !== 'number' ||
            typeof maxLon !== 'number' || typeof maxLat !== 'number' ||
            isNaN(minLon) || isNaN(minLat) || isNaN(maxLon) || isNaN(maxLat)) {
            return;
        }

        if (trackId && allImportedTracks) {
            for (let i = 0; i < allImportedTracks.length; i++) {
                if (allImportedTracks[i].id === trackId) {
                    allImportedTracks[i].isVisible = true;
                    break;
                }
            }
        }

        if (Math.abs(maxLon - minLon) < 0.0001 && Math.abs(maxLat - minLat) < 0.0001) {
            map.flyTo({
                center: [minLon, minLat],
                zoom: 14,
                essential: true
            });
        } else {
            map.fitBounds([
                [minLon, minLat],
                [maxLon, maxLat]
            ], {
                padding: { top: 60, bottom: 60, left: 60, right: 60 },
                maxZoom: 16,
                duration: 900
            });
        }

        setTimeout(refreshImportedTracksInView, 950);
    };

    function computeHaversineDistanceKm(coords) {
        if (!coords || coords.length < 2) return 0;
        let dist = 0;
        for (let i = 0; i < coords.length - 1; i++) {
            const p1 = coords[i];
            const p2 = coords[i + 1];
            const dLat = (p2[1] - p1[1]) * Math.PI / 180;
            const dLon = (p2[0] - p1[0]) * Math.PI / 180;
            const lat1 = p1[1] * Math.PI / 180;
            const lat2 = p2[1] * Math.PI / 180;
            const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                Math.sin(dLon / 2) * Math.sin(dLon / 2) * Math.cos(lat1) * Math.cos(lat2);
            const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
            dist += 6371 * c;
        }
        return dist;
    }

    async function routeBetweenPoints(p1, p2) {
        if (!snapToTrails) {
            return [[p1.lon, p1.lat, p1.ele || null], [p2.lon, p2.lat, p2.ele || null]];
        }

        try {
            const url = `https://brouter.de/brouter?lonlats=${p1.lon.toFixed(6)},${p1.lat.toFixed(6)}|${p2.lon.toFixed(6)},${p2.lat.toFixed(6)}&profile=hiking-mountain&alternativeidx=0&format=geojson`;
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 7000);

            const response = await fetch(url, { signal: controller.signal });
            clearTimeout(timeoutId);

            if (response.ok) {
                const data = await response.json();
                if (data && data.features && data.features.length > 0 &&
                    data.features[0].geometry && data.features[0].geometry.coordinates &&
                    data.features[0].geometry.coordinates.length > 0) {
                    return data.features[0].geometry.coordinates.map(c => [
                        c[0],
                        c[1],
                        (c.length > 2 && typeof c[2] === 'number') ? c[2] : null
                    ]);
                }
            }
        } catch (e) {
            console.warn("[FullScreenMap] Trail routing request failed or timed out, falling back to direct line:", e);
        }

        // Direct line fallback
        return [[p1.lon, p1.lat, p1.ele || null], [p2.lon, p2.lat, p2.ele || null]];
    }

    function updateRouteDisplay() {
        if (!map) return;

        // 1. Refresh Waypoint Markers
        routeMarkers.forEach(m => {
            try { m.remove(); } catch (e) { }
        });
        routeMarkers = [];

        for (let i = 0; i < routeWaypoints.length; i++) {
            const wp = routeWaypoints[i];
            const el = createWaypointMarkerElement(i + 1, i === 0, i === routeWaypoints.length - 1);
            const m = new window.maplibregl.Marker({
                element: el,
                anchor: 'center'
            }).setLngLat([wp.lon, wp.lat]).addTo(map);
            routeMarkers.push(m);
        }

        // 2. Refresh GeoJSON Line
        const allCoords = [];
        for (const leg of routeLegs) {
            for (const pt of leg) {
                if (allCoords.length === 0 || allCoords[allCoords.length - 1][0] !== pt[0] || allCoords[allCoords.length - 1][1] !== pt[1]) {
                    allCoords.push(pt);
                }
            }
        }

        if (map.getSource('route-draft-line-source')) {
            map.getSource('route-draft-line-source').setData({
                type: 'Feature',
                geometry: {
                    type: 'LineString',
                    coordinates: allCoords
                }
            });
        }
    }

    function notifyRouteUpdated() {
        if (!onRouteUpdatedCb) return;

        let allCoords = [];
        for (const leg of routeLegs) {
            for (const pt of leg) {
                if (allCoords.length === 0 || allCoords[allCoords.length - 1][0] !== pt[0] || allCoords[allCoords.length - 1][1] !== pt[1]) {
                    allCoords.push(pt);
                }
            }
        }

        const distKm = computeHaversineDistanceKm(allCoords);

        let climbM = 0;
        let descentM = 0;
        let hasElevation = false;

        for (let i = 1; i < allCoords.length; i++) {
            const e1 = allCoords[i - 1][2];
            const e2 = allCoords[i][2];
            if (typeof e1 === 'number' && typeof e2 === 'number' && !isNaN(e1) && !isNaN(e2)) {
                hasElevation = true;
                const diff = e2 - e1;
                if (diff > 0.6) climbM += diff;
                else if (diff < -0.6) descentM += Math.abs(diff);
            }
        }

        onRouteUpdatedCb(distKm, allCoords.length, routeWaypoints.length, isRoutingInProgress, climbM, descentM, hasElevation);
    }

    async function handleRouteMapClick(lat, lon) {
        if (isRouteClickPending) return;
        isRouteClickPending = true;

        try {
            const newWp = { lat, lon };
            if (routeWaypoints.length === 0) {
                routeWaypoints.push(newWp);
                updateRouteDisplay();
                notifyRouteUpdated();
                return;
            }

            const prevWp = routeWaypoints[routeWaypoints.length - 1];
            isRoutingInProgress = true;
            notifyRouteUpdated();

            const legCoords = await routeBetweenPoints(prevWp, newWp);
            routeWaypoints.push(newWp);
            routeLegs.push(legCoords);
        } finally {
            isRoutingInProgress = false;
            isRouteClickPending = false;
            updateRouteDisplay();
            notifyRouteUpdated();
        }
    }

    window.fullScreenMap.startRouteDrawing = function (onRouteUpdated) {
        isRouteDrawingActive = true;
        onRouteUpdatedCb = onRouteUpdated;
        if (map && map.getCanvas()) {
            map.getCanvas().style.cursor = 'crosshair';
        }
        updateRouteDisplay();
        notifyRouteUpdated();
    };

    window.fullScreenMap.exitRouteDrawing = function () {
        isRouteDrawingActive = false;
        if (map && map.getCanvas()) {
            map.getCanvas().style.cursor = '';
        }
    };

    window.fullScreenMap.isRouteDrawing = function () {
        return isRouteDrawingActive;
    };

    window.fullScreenMap.setRouteSnapToTrails = function (enabled) {
        snapToTrails = enabled === true;
    };

    window.fullScreenMap.isRouteSnapToTrails = function () {
        return snapToTrails;
    };

    window.fullScreenMap.undoLastRoutePoint = function () {
        if (routeWaypoints.length > 0) {
            routeWaypoints.pop();
            if (routeLegs.length > 0) {
                routeLegs.pop();
            }
            updateRouteDisplay();
            notifyRouteUpdated();
        }
    };

    window.fullScreenMap.reverseRoute = function () {
        if (routeWaypoints.length > 1) {
            routeWaypoints.reverse();
            routeLegs = routeLegs.reverse().map(leg => leg.slice().reverse());
            updateRouteDisplay();
            notifyRouteUpdated();
        }
    };

    window.fullScreenMap.clearRoute = function () {
        routeWaypoints = [];
        routeLegs = [];
        updateRouteDisplay();
        notifyRouteUpdated();
    };

    window.fullScreenMap.getRouteCoordinatesJson = function () {
        const allCoords = [];
        for (const leg of routeLegs) {
            for (const pt of leg) {
                if (allCoords.length === 0 || allCoords[allCoords.length - 1][0] !== pt[0] || allCoords[allCoords.length - 1][1] !== pt[1]) {
                    allCoords.push(pt);
                }
            }
        }
        return JSON.stringify(allCoords);
    };

    window.fullScreenMap.getRouteWaypointsJson = function () {
        return JSON.stringify(routeWaypoints.map(wp => [wp.lon, wp.lat]));
    };

    window.fullScreenMap.resetView = function () {
        // Guaranteed 100% recovery by tearing down and creating a fresh map instance
        recreateMap(currentPointerLat, currentPointerLon);
    };

    window.fullScreenMap.is3d = function () {
        return map ? (map.getPitch() > 25) : false;
    };

    window.fullScreenMap.resize = function () {
        if (map) {
            setTimeout(function () {
                try { map.resize(); } catch (e) { }
            }, 100);
        }
    };

    // =========================================================================
    // Elevation Profile Floating Card & Interactive Profile Scrubbing
    // =========================================================================

    function ensurePulseStyle() {
        if (!document.getElementById('ph-profile-marker-style')) {
            const style = document.createElement('style');
            style.id = 'ph-profile-marker-style';
            style.textContent = `
                @keyframes ph-pulse {
                    0% { transform: scale(1); }
                    50% { transform: scale(1.35); }
                    100% { transform: scale(1); }
                }
                .ph-waypoint-custom-popup .maplibregl-popup-content {
                    padding: 10px 14px;
                    border-radius: 10px;
                    box-shadow: 0 4px 20px rgba(0,0,0,0.3);
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
                }
                .ph-waypoint-custom-popup.ph-dark .maplibregl-popup-content {
                    background: #1e293b;
                    color: #f8fafc;
                    border: 1px solid rgba(255,255,255,0.15);
                }
                .ph-waypoint-custom-popup.ph-dark .maplibregl-popup-tip {
                    border-top-color: #1e293b;
                }
                .ph-waypoint-custom-popup.ph-light .maplibregl-popup-content {
                    background: #ffffff;
                    color: #0f172a;
                    border: 1px solid rgba(0,0,0,0.12);
                }
                .ph-waypoint-custom-popup.ph-light .maplibregl-popup-tip {
                    border-top-color: #ffffff;
                }
                .ph-waypoint-custom-popup .maplibregl-popup-close-button {
                    color: #94a3b8;
                    font-size: 18px;
                    padding: 4px 8px;
                    right: 4px;
                    top: 4px;
                    border: none;
                    background: transparent;
                    cursor: pointer;
                }
                .ph-waypoint-custom-popup.ph-dark .maplibregl-popup-close-button:hover {
                    color: #ffffff;
                }
                .ph-waypoint-custom-popup.ph-light .maplibregl-popup-close-button:hover {
                    color: #0f172a;
                }
                .ph-wpt-header {
                    display: flex;
                    align-items: center;
                    gap: 6px;
                    font-size: 14px;
                    font-weight: 700;
                    margin-bottom: 6px;
                    padding-right: 18px;
                    word-break: break-word;
                }
                .ph-wpt-desc {
                    font-size: 12px;
                    line-height: 1.45;
                    margin-bottom: 8px;
                    word-break: break-word;
                    white-space: pre-wrap;
                }
                .ph-waypoint-custom-popup.ph-dark .ph-wpt-desc {
                    color: #cbd5e1;
                }
                .ph-waypoint-custom-popup.ph-light .ph-wpt-desc {
                    color: #475569;
                }
                .ph-wpt-meta {
                    display: flex;
                    flex-direction: column;
                    gap: 3px;
                    font-size: 11px;
                    border-top: 1px solid;
                    padding-top: 6px;
                    margin-top: 6px;
                }
                .ph-waypoint-custom-popup.ph-dark .ph-wpt-meta {
                    border-top-color: rgba(255,255,255,0.12);
                    color: #94a3b8;
                }
                .ph-waypoint-custom-popup.ph-light .ph-wpt-meta {
                    border-top-color: rgba(0,0,0,0.1);
                    color: #64748b;
                }
                .ph-wpt-actions {
                    display: flex;
                    justify-content: flex-end;
                    margin-top: 8px;
                    padding-top: 6px;
                    border-top: 1px solid rgba(128,128,128,0.2);
                }
                .ph-wpt-edit-btn {
                    background: #0284c7;
                    color: #ffffff;
                    border: none;
                    border-radius: 6px;
                    padding: 5px 12px;
                    font-size: 11.5px;
                    font-weight: 600;
                    cursor: pointer;
                    display: inline-flex;
                    align-items: center;
                    gap: 4px;
                    transition: background 0.15s ease;
                }
                .ph-wpt-edit-btn:hover {
                    background: #0369a1;
                }

                /* Navigation controls (Zoom In/Out, Bearing Compass): standard 29px on larger screens, 58px on smaller screens */
                .maplibregl-ctrl-group {
                    opacity: 0.88 !important;
                    transition: opacity 0.2s ease !important;
                }
                .maplibregl-ctrl-group:hover {
                    opacity: 1.0 !important;
                }
                @media (min-width: 768px) {
                    .maplibregl-ctrl-group {
                        border-radius: 6px !important;
                    }
                    .maplibregl-ctrl-group button {
                        width: 29px !important;
                        height: 29px !important;
                    }
                    .maplibregl-ctrl-zoom-in .maplibregl-ctrl-icon,
                    .maplibregl-ctrl-zoom-out .maplibregl-ctrl-icon {
                        transform: scale(1.0) !important;
                        transform-origin: center center !important;
                    }
                }
                @media (max-width: 767px) {
                    .maplibregl-ctrl-group {
                        border-radius: 8px !important;
                    }
                    .maplibregl-ctrl-group button {
                        width: 58px !important;
                        height: 58px !important;
                    }
                    .maplibregl-ctrl-zoom-in .maplibregl-ctrl-icon,
                    .maplibregl-ctrl-zoom-out .maplibregl-ctrl-icon {
                        transform: scale(1.8) !important;
                        transform-origin: center center !important;
                    }
                }

                /* Prominent, crisp compass needle pointing to True North */
                .maplibregl-ctrl button.maplibregl-ctrl-compass .maplibregl-ctrl-icon {
                    background-image: url("data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' width='29' height='29' viewBox='0 0 29 29'%3E%3Cpolygon points='14.5,3.5 19,14 14.5,11.5 10,14' fill='%23EF4444'/%3E%3Cpolygon points='14.5,25.5 19,15 14.5,17.5 10,15' fill='%2394A3B8'/%3E%3Ccircle cx='14.5' cy='14.5' r='1.8' fill='%231E293B'/%3E%3C/svg%3E") !important;
                    background-position: center !important;
                    background-repeat: no-repeat !important;
                    background-size: contain !important;
                    transform-origin: center center !important;
                    transition: transform 0.08s ease-out;
                }
                @media (max-width: 767px) {
                    .maplibregl-ctrl button.maplibregl-ctrl-compass .maplibregl-ctrl-icon {
                        background-size: 38px 38px !important;
                    }
                }
            `;
            document.head.appendChild(style);
        }
    }

    function hexToRgba(hex, alpha) {
        if (!hex || typeof hex !== 'string') return `rgba(59, 130, 246, ${alpha})`;
        let c = hex.replace('#', '');
        if (c.length === 3) c = c[0] + c[0] + c[1] + c[1] + c[2] + c[2];
        if (c.length === 6) {
            const r = parseInt(c.substring(0, 2), 16);
            const g = parseInt(c.substring(2, 4), 16);
            const b = parseInt(c.substring(4, 6), 16);
            return `rgba(${r}, ${g}, ${b}, ${alpha})`;
        }
        return `rgba(59, 130, 246, ${alpha})`;
    }

    function createElevationProfileUI() {
        if (profileCardElement || !mapContainerElement) return;

        const bottomOffset = isRecordingActive ? '64px' : '16px';
        const card = document.createElement('div');
        card.id = 'ph-elevation-profile-card';
        card.style.cssText = `
            position: absolute;
            bottom: ${bottomOffset};
            left: 50%;
            transform: translateX(-50%);
            width: calc(100% - 32px);
            max-width: 760px;
            background: rgba(18, 22, 31, 0.94);
            color: #F8FAFC;
            border: 1px solid rgba(255, 255, 255, 0.18);
            border-radius: 12px;
            padding: 10px 14px 10px 14px;
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.45);
            backdrop-filter: blur(8px);
            -webkit-backdrop-filter: blur(8px);
            z-index: 25;
            display: none;
            flex-direction: column;
            box-sizing: border-box;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            user-select: none;
            transition: bottom 0.2s ease;
        `;

        card.innerHTML = `
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 6px;">
                <div style="display: flex; align-items: center; gap: 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">
                    <span id="ph-ep-color-dot" style="display: inline-block; width: 10px; height: 10px; border-radius: 50%; background: #3B82F6; flex-shrink: 0;"></span>
                    <span id="ph-ep-title" style="font-size: 13px; font-weight: 700; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">Track Profile</span>
                    <span id="ph-ep-badge" style="font-size: 10px; font-weight: 600; padding: 2px 6px; border-radius: 4px; background: rgba(255,255,255,0.12); color: #94A3B8;">GPX</span>
                </div>
                <button id="ph-ep-close-btn" style="background: rgba(255,255,255,0.1); border: 1px solid rgba(255,255,255,0.2); color: #F8FAFC; border-radius: 6px; width: 24px; height: 24px; font-size: 12px; cursor: pointer; display: flex; align-items: center; justify-content: center; line-height: 1; padding: 0;" title="Close elevation profile">✕</button>
            </div>
            <div id="ph-ep-stats-row" style="display: flex; flex-wrap: wrap; gap: 12px; font-size: 11px; margin-bottom: 8px; color: #CBD5E1;">
                <div><span class="ph-ep-label" style="color: #94A3B8;">Distance:</span> <b id="ph-ep-dist">0.0 km</b></div>
                <div><span class="ph-ep-label" style="color: #94A3B8;">Climb:</span> <b id="ph-ep-climb" style="color: #10B981;">+0 m</b></div>
                <div><span class="ph-ep-label" style="color: #94A3B8;">Descent:</span> <b id="ph-ep-descent" style="color: #F87171;">-0 m</b></div>
                <div><span class="ph-ep-label" style="color: #94A3B8;">Elev:</span> <b id="ph-ep-range">--</b></div>
            </div>
            <div style="position: relative; width: 100%; height: 110px;">
                <canvas id="ph-ep-canvas" style="width: 100%; height: 110px; display: block;"></canvas>
                <div id="ph-ep-tooltip" style="position: absolute; display: none; pointer-events: none; background: rgba(15, 23, 42, 0.92); border: 1px solid rgba(255,255,255,0.25); border-radius: 6px; padding: 3px 8px; font-size: 10px; font-weight: 600; color: #F8FAFC; white-space: nowrap; transform: translate(-50%, -100%); margin-top: -6px; box-shadow: 0 4px 12px rgba(0,0,0,0.3); z-index: 5;"></div>
            </div>
        `;

        mapContainerElement.appendChild(card);
        profileCardElement = card;

        const closeBtn = card.querySelector('#ph-ep-close-btn');
        if (closeBtn) {
            closeBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                window.fullScreenMap.hideElevationProfile();
            });
        }

        // Prevent click events on profile card from propagating to map
        card.addEventListener('click', (e) => e.stopPropagation());
        card.addEventListener('dblclick', (e) => e.stopPropagation());
        card.addEventListener('mousedown', (e) => e.stopPropagation());

        setupProfileCanvasEvents();
        window.fullScreenMap.setTheme(isDarkMode);
    }

    function setupProfileCanvasEvents() {
        if (!profileCardElement) return;
        const canvas = profileCardElement.querySelector('#ph-ep-canvas');
        const tooltip = profileCardElement.querySelector('#ph-ep-tooltip');
        if (!canvas) return;

        function handleHover(clientX) {
            if (!currentProfilePoints || currentProfilePoints.length < 2) return;
            const rect = canvas.getBoundingClientRect();
            const relX = clientX - rect.left;
            const padLeft = 44;
            const padRight = 14;
            const graphWidth = rect.width - padLeft - padRight;
            if (graphWidth <= 0) return;

            const clampedX = Math.max(padLeft, Math.min(rect.width - padRight, relX));
            const progress = (clampedX - padLeft) / graphWidth;
            const totalDist = currentProfileTrack.distance || currentProfilePoints[currentProfilePoints.length - 1][0];
            const targetDist = progress * totalDist;

            // Find nearest profile point by distance
            let nearestIdx = 0;
            let minDiff = Infinity;
            for (let i = 0; i < currentProfilePoints.length; i++) {
                const diff = Math.abs(currentProfilePoints[i][0] - targetDist);
                if (diff < minDiff) {
                    minDiff = diff;
                    nearestIdx = i;
                }
            }

            const pt = currentProfilePoints[nearestIdx]; // [distKm, eleM, lat, lon]
            drawElevationProfileCanvas(nearestIdx);

            // Update Map Marker
            if (map && pt.length >= 4 && !isNaN(pt[2]) && !isNaN(pt[3])) {
                updateProfileMapMarker(pt[2], pt[3]);
            }

            // Update Tooltip
            if (tooltip) {
                const distStr = isMetricUnits ? `${pt[0].toFixed(2)} km` : `${(pt[0] * 0.621371).toFixed(2)} mi`;
                const eleStr = isMetricUnits ? `${Math.round(pt[1])} m` : `${Math.round(pt[1] * 3.28084)} ft`;
                tooltip.innerHTML = `📍 ${distStr} • ⛰️ ${eleStr}`;
                tooltip.style.left = `${clampedX}px`;
                tooltip.style.top = `18px`;
                tooltip.style.display = 'block';
            }
        }

        canvas.addEventListener('mousemove', (e) => {
            handleHover(e.clientX);
        });

        canvas.addEventListener('touchmove', (e) => {
            if (e.touches && e.touches.length > 0) {
                handleHover(e.touches[0].clientX);
            }
        }, { passive: true });

        const clearHover = () => {
            drawElevationProfileCanvas(-1);
            if (tooltip) tooltip.style.display = 'none';
            removeProfileMapMarker();
        };

        canvas.addEventListener('mouseleave', clearHover);
        canvas.addEventListener('touchend', clearHover);
    }

    function updateProfileMapMarker(lat, lon) {
        if (!map) return;
        if (!profileMapMarker) {
            const el = document.createElement('div');
            el.className = 'ph-profile-scrub-marker';
            el.style.cssText = `
                width: 16px;
                height: 16px;
                border-radius: 50%;
                background: #FFFFFF;
                border: 3px solid #3B82F6;
                box-shadow: 0 0 12px #3B82F6, 0 0 4px rgba(0,0,0,0.6);
                transform: translate(-50%, -50%);
                pointer-events: none;
                animation: ph-pulse 1.2s infinite;
            `;
            profileMapMarker = new window.maplibregl.Marker({
                element: el,
                anchor: 'center'
            });
        }
        const color = (currentProfileTrack && currentProfileTrack.color) || '#3B82F6';
        profileMapMarker.getElement().style.borderColor = color;
        profileMapMarker.getElement().style.boxShadow = `0 0 12px ${color}, 0 0 4px rgba(0,0,0,0.6)`;
        profileMapMarker.setLngLat([lon, lat]).addTo(map);
    }

    function removeProfileMapMarker() {
        if (profileMapMarker) {
            try { profileMapMarker.remove(); } catch (e) { }
            profileMapMarker = null;
        }
    }

    function generateProfileFromCoordinates(coords) {
        if (!coords || coords.length < 2) return [];
        const result = [];
        let cumDist = 0;
        const sampleCount = Math.min(coords.length, 250);
        const step = (coords.length - 1) / (sampleCount - 1);

        for (let i = 0; i < sampleCount; i++) {
            const idx = Math.min(coords.length - 1, Math.round(i * step));
            const c = coords[idx];
            if (idx > 0) {
                const prev = coords[idx - 1];
                cumDist += computeHaversineDistanceKm([[prev[0], prev[1]], [c[0], c[1]]]);
            }
            const ele = (c.length > 2 && typeof c[2] === 'number') ? c[2] : 0;
            result.push([cumDist, ele, c[1], c[0]]);
        }
        return result;
    }

    function buildActiveGpsTrackProfile() {
        const pts = recordedPoints || [];
        const profile = pts.length >= 2 ? generateProfileFromCoordinates(pts) : [];
        let climb = 0, descent = 0, minE = Infinity, maxE = -Infinity;
        for (let i = 0; i < profile.length; i++) {
            const e = profile[i][1];
            if (e > 0) {
                if (e < minE) minE = e;
                if (e > maxE) maxE = e;
                if (i > 0) {
                    const diff = e - profile[i - 1][1];
                    if (diff > 0.6) climb += diff;
                    else if (diff < -0.6) descent += Math.abs(diff);
                }
            }
        }
        const totalDist = profile.length > 0 ? profile[profile.length - 1][0] : 0;
        return {
            id: 'active_gps_track',
            name: 'Recording Live Track',
            format: 'GPS (Live)',
            color: '#EF4444',
            distance: totalDist,
            climb: climb,
            descent: descent,
            minElevation: (minE === Infinity) ? 0 : minE,
            maxElevation: (maxE === -Infinity) ? 0 : maxE,
            profile: profile
        };
    }

    function drawElevationProfileCanvas(hoverIdx) {
        if (!profileCardElement) return;
        const canvas = profileCardElement.querySelector('#ph-ep-canvas');
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const rect = canvas.getBoundingClientRect();
        const dpr = window.devicePixelRatio || 1;
        const w = rect.width;
        const h = rect.height;

        if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) {
            canvas.width = Math.round(w * dpr);
            canvas.height = Math.round(h * dpr);
        }

        ctx.resetTransform();
        ctx.scale(dpr, dpr);
        ctx.clearRect(0, 0, w, h);

        if (!currentProfilePoints || currentProfilePoints.length < 2) {
            ctx.fillStyle = isDarkMode ? '#94A3B8' : '#64748B';
            ctx.font = '11px sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText('No elevation points recorded for this track', w / 2, h / 2);
            return;
        }

        const padLeft = 44;
        const padRight = 14;
        const padTop = 14;
        const padBottom = 20;
        const plotW = w - padLeft - padRight;
        const plotH = h - padTop - padBottom;

        if (plotW <= 0 || plotH <= 0) return;

        const totalDist = currentProfileTrack.distance || currentProfilePoints[currentProfilePoints.length - 1][0];
        let minEle = Infinity;
        let maxEle = -Infinity;

        for (const p of currentProfilePoints) {
            const e = p[1];
            if (e < minEle) minEle = e;
            if (e > maxEle) maxEle = e;
        }

        if (minEle === Infinity) { minEle = 0; maxEle = 100; }
        const eleRange = (maxEle - minEle) || 10;
        const plotMinEle = Math.max(0, minEle - eleRange * 0.08);
        const plotMaxEle = maxEle + eleRange * 0.08;
        const plotEleRange = plotMaxEle - plotMinEle;

        const trackColor = (currentProfileTrack && currentProfileTrack.color) || '#3B82F6';

        // 1. Horizontal grid lines & Y-axis labels
        const gridSteps = 3;
        ctx.textAlign = 'right';
        ctx.font = '9.5px sans-serif';
        for (let i = 0; i <= gridSteps; i++) {
            const ratio = i / gridSteps;
            const y = padTop + plotH * (1 - ratio);
            const eleM = plotMinEle + plotEleRange * ratio;
            const eleLabel = isMetricUnits ? `${Math.round(eleM)}m` : `${Math.round(eleM * 3.28084)}ft`;

            ctx.strokeStyle = isDarkMode ? 'rgba(255, 255, 255, 0.10)' : 'rgba(0, 0, 0, 0.09)';
            ctx.lineWidth = 1;
            ctx.setLineDash([3, 3]);
            ctx.beginPath();
            ctx.moveTo(padLeft, y);
            ctx.lineTo(w - padRight, y);
            ctx.stroke();

            ctx.fillStyle = isDarkMode ? '#94A3B8' : '#64748B';
            ctx.fillText(eleLabel, padLeft - 6, y + 3);
        }
        ctx.setLineDash([]);

        // 2. Bottom X-axis ticks & labels
        ctx.textAlign = 'center';
        const distSteps = Math.min(5, Math.max(2, Math.floor(plotW / 80)));
        for (let i = 0; i <= distSteps; i++) {
            const ratio = i / distSteps;
            const x = padLeft + plotW * ratio;
            const dVal = totalDist * ratio;
            const dLabel = isMetricUnits ? `${dVal.toFixed(1)}k` : `${(dVal * 0.621371).toFixed(1)}m`;

            ctx.fillStyle = isDarkMode ? '#94A3B8' : '#64748B';
            ctx.fillText(dLabel, x, h - 4);
        }

        // 3. Compute plot coordinates
        const plotPoints = [];
        for (let i = 0; i < currentProfilePoints.length; i++) {
            const p = currentProfilePoints[i];
            const dRatio = totalDist > 0 ? (p[0] / totalDist) : 0;
            const x = padLeft + plotW * Math.min(1, Math.max(0, dRatio));
            const eRatio = plotEleRange > 0 ? ((p[1] - plotMinEle) / plotEleRange) : 0.5;
            const y = padTop + plotH * (1 - Math.min(1, Math.max(0, eRatio)));
            plotPoints.push({ x, y });
        }

        // 4. Gradient Fill Area
        const gradient = ctx.createLinearGradient(0, padTop, 0, padTop + plotH);
        gradient.addColorStop(0, hexToRgba(trackColor, 0.42));
        gradient.addColorStop(1, hexToRgba(trackColor, 0.02));

        ctx.fillStyle = gradient;
        ctx.beginPath();
        ctx.moveTo(plotPoints[0].x, padTop + plotH);
        ctx.lineTo(plotPoints[0].x, plotPoints[0].y);
        for (let i = 1; i < plotPoints.length; i++) {
            ctx.lineTo(plotPoints[i].x, plotPoints[i].y);
        }
        ctx.lineTo(plotPoints[plotPoints.length - 1].x, padTop + plotH);
        ctx.closePath();
        ctx.fill();

        // 5. Profile Stroke Line
        ctx.strokeStyle = trackColor;
        ctx.lineWidth = 2.4;
        ctx.lineJoin = 'round';
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(plotPoints[0].x, plotPoints[0].y);
        for (let i = 1; i < plotPoints.length; i++) {
            ctx.lineTo(plotPoints[i].x, plotPoints[i].y);
        }
        ctx.stroke();

        // 6. Hover Crosshair & Scrubbing Dot
        if (hoverIdx >= 0 && hoverIdx < plotPoints.length) {
            const hPt = plotPoints[hoverIdx];

            ctx.strokeStyle = isDarkMode ? 'rgba(255, 255, 255, 0.65)' : 'rgba(0, 0, 0, 0.55)';
            ctx.lineWidth = 1.2;
            ctx.setLineDash([3, 3]);
            ctx.beginPath();
            ctx.moveTo(hPt.x, padTop);
            ctx.lineTo(hPt.x, padTop + plotH);
            ctx.stroke();
            ctx.setLineDash([]);

            ctx.fillStyle = hexToRgba(trackColor, 0.35);
            ctx.beginPath();
            ctx.arc(hPt.x, hPt.y, 8, 0, Math.PI * 2);
            ctx.fill();

            ctx.fillStyle = '#FFFFFF';
            ctx.strokeStyle = trackColor;
            ctx.lineWidth = 2.5;
            ctx.beginPath();
            ctx.arc(hPt.x, hPt.y, 4, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();
        }
    }

    window.fullScreenMap.showElevationProfile = function (trackIdOrObj) {
        createElevationProfileUI();
        if (!profileCardElement) return;

        let track = null;
        if (typeof trackIdOrObj === 'string') {
            if (trackIdOrObj === 'active_gps_track') {
                track = buildActiveGpsTrackProfile();
            } else if (allImportedTracks) {
                track = allImportedTracks.find(t => t.id === trackIdOrObj);
            }
        } else if (trackIdOrObj && typeof trackIdOrObj === 'object') {
            track = trackIdOrObj;
        }

        if (!track) return;
        currentProfileTrack = track;

        if (track.profile && Array.isArray(track.profile) && track.profile.length > 1) {
            currentProfilePoints = track.profile;
        } else if (track.coordinates && Array.isArray(track.coordinates) && track.coordinates.length > 1) {
            currentProfilePoints = generateProfileFromCoordinates(track.coordinates);
        } else {
            currentProfilePoints = [];
        }

        const titleEl = profileCardElement.querySelector('#ph-ep-title');
        const colorDot = profileCardElement.querySelector('#ph-ep-color-dot');
        const badgeEl = profileCardElement.querySelector('#ph-ep-badge');
        const distEl = profileCardElement.querySelector('#ph-ep-dist');
        const climbEl = profileCardElement.querySelector('#ph-ep-climb');
        const descentEl = profileCardElement.querySelector('#ph-ep-descent');
        const rangeEl = profileCardElement.querySelector('#ph-ep-range');

        const trackName = track.name || track.fileName || "Track";
        const color = track.color || "#3B82F6";
        const dist = track.distance || (currentProfilePoints.length > 0 ? currentProfilePoints[currentProfilePoints.length - 1][0] : 0);
        const climb = track.climb || 0;
        const descent = track.descent || 0;
        const minE = track.minElevation || 0;
        const maxE = track.maxElevation || 0;

        if (titleEl) titleEl.textContent = trackName;
        if (colorDot) colorDot.style.background = color;

        const isLiveGps = track.id === 'active_gps_track';
        if (badgeEl) {
            badgeEl.textContent = track.format || "GPX";
            badgeEl.style.display = isLiveGps ? 'none' : 'inline-block';
        }

        if (distEl) {
            distEl.textContent = isMetricUnits ? `${dist.toFixed(2)} km` : `${(dist * 0.621371).toFixed(2)} mi`;
        }
        if (climbEl) {
            climbEl.textContent = isMetricUnits ? `+${Math.round(climb)} m` : `+${Math.round(climb * 3.28084)} ft`;
        }
        if (descentEl) {
            descentEl.textContent = isMetricUnits ? `-${Math.round(descent)} m` : `-${Math.round(descent * 3.28084)} ft`;
        }
        if (rangeEl) {
            if (minE > 0 || maxE > 0) {
                rangeEl.textContent = isMetricUnits
                    ? `${Math.round(minE)} m – ${Math.round(maxE)} m`
                    : `${Math.round(minE * 3.28084)} ft – ${Math.round(maxE * 3.28084)} ft`;
            } else {
                rangeEl.textContent = "--";
            }
        }

        profileCardElement.style.bottom = isRecordingActive ? '64px' : '16px';
        profileCardElement.style.display = 'flex';
        window.fullScreenMap.setTheme(isDarkMode);
        setTimeout(() => {
            drawElevationProfileCanvas(-1);
        }, 30);
    };

    window.fullScreenMap.toggleElevationProfile = function (trackIdOrObj) {
        createElevationProfileUI();
        const targetId = typeof trackIdOrObj === 'string' ? trackIdOrObj : (trackIdOrObj && trackIdOrObj.id);
        if (profileCardElement && profileCardElement.style.display !== 'none' && currentProfileTrack && currentProfileTrack.id === targetId) {
            window.fullScreenMap.hideElevationProfile();
        } else {
            window.fullScreenMap.showElevationProfile(trackIdOrObj);
        }
    };

    window.fullScreenMap.hideElevationProfile = function () {
        if (profileCardElement) {
            profileCardElement.style.display = 'none';
        }
        removeProfileMapMarker();
        currentProfileTrack = null;
        currentProfilePoints = [];
    };

    window.fullScreenMap.setIsMetric = function (metric) {
        isMetricUnits = metric === true;
        if (profileCardElement && profileCardElement.style.display !== 'none' && currentProfileTrack) {
            window.fullScreenMap.showElevationProfile(currentProfileTrack);
        }
    };

    window.fullScreenMap.setTheme = function (isDark) {
        isDarkMode = isDark === true;
        if (profileCardElement) {
            profileCardElement.style.background = isDarkMode ? 'rgba(15, 23, 42, 0.94)' : 'rgba(255, 255, 255, 0.96)';
            profileCardElement.style.color = isDarkMode ? '#F8FAFC' : '#0F172A';
            profileCardElement.style.border = isDarkMode ? '1px solid rgba(255, 255, 255, 0.18)' : '1px solid rgba(0, 0, 0, 0.14)';
            profileCardElement.style.boxShadow = isDarkMode ? '0 10px 30px rgba(0, 0, 0, 0.45)' : '0 10px 30px rgba(0, 0, 0, 0.15)';

            const closeBtn = profileCardElement.querySelector('#ph-ep-close-btn');
            if (closeBtn) {
                closeBtn.style.background = isDarkMode ? 'rgba(255, 255, 255, 0.10)' : 'rgba(0, 0, 0, 0.06)';
                closeBtn.style.border = isDarkMode ? '1px solid rgba(255, 255, 255, 0.20)' : '1px solid rgba(0, 0, 0, 0.14)';
                closeBtn.style.color = isDarkMode ? '#F8FAFC' : '#0F172A';
            }

            const badge = profileCardElement.querySelector('#ph-ep-badge');
            if (badge) {
                badge.style.background = isDarkMode ? 'rgba(255, 255, 255, 0.12)' : 'rgba(0, 0, 0, 0.08)';
                badge.style.color = isDarkMode ? '#94A3B8' : '#475569';
            }

            const statsRow = profileCardElement.querySelector('#ph-ep-stats-row');
            if (statsRow) {
                statsRow.style.color = isDarkMode ? '#CBD5E1' : '#334155';
            }

            const labels = profileCardElement.querySelectorAll('.ph-ep-label');
            labels.forEach(l => {
                l.style.color = isDarkMode ? '#94A3B8' : '#64748B';
            });

            const distEl = profileCardElement.querySelector('#ph-ep-dist');
            if (distEl) distEl.style.color = isDarkMode ? '#F8FAFC' : '#0F172A';

            const climbEl = profileCardElement.querySelector('#ph-ep-climb');
            if (climbEl) climbEl.style.color = isDarkMode ? '#34D399' : '#059669';

            const descentEl = profileCardElement.querySelector('#ph-ep-descent');
            if (descentEl) descentEl.style.color = isDarkMode ? '#F87171' : '#DC2626';

            const rangeEl = profileCardElement.querySelector('#ph-ep-range');
            if (rangeEl) rangeEl.style.color = isDarkMode ? '#F8FAFC' : '#0F172A';

            const tooltip = profileCardElement.querySelector('#ph-ep-tooltip');
            if (tooltip) {
                tooltip.style.background = isDarkMode ? 'rgba(15, 23, 42, 0.95)' : 'rgba(255, 255, 255, 0.98)';
                tooltip.style.color = isDarkMode ? '#F8FAFC' : '#0F172A';
                tooltip.style.border = isDarkMode ? '1px solid rgba(255, 255, 255, 0.25)' : '1px solid rgba(0, 0, 0, 0.18)';
                tooltip.style.boxShadow = isDarkMode ? '0 4px 12px rgba(0,0,0,0.4)' : '0 4px 12px rgba(0,0,0,0.15)';
            }

            if (profileCardElement.style.display !== 'none') {
                drawElevationProfileCanvas(-1);
            }
        }
    };

    window.fullScreenMap.getMapCenterJson = function () {
        if (map) {
            const c = map.getCenter();
            return JSON.stringify({ lat: c.lat, lng: c.lng });
        }
        return JSON.stringify({ lat: currentPointerLat, lng: currentPointerLon });
    };

    window.fullScreenMap.getPointerCoordinatesJson = function () {
        return JSON.stringify({ lat: currentPointerLat, lng: currentPointerLon });
    };
})();

