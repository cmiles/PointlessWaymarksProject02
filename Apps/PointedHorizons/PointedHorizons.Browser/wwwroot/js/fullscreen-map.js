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
    let recordedPoints = []; // Array of [lon, lat]
    let activeTerrainSourceId = null;

    let onPointerMovedCb = null;

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
        el.style.width = '24px';
        el.style.height = '24px';
        el.style.pointerEvents = 'none';
        el.innerHTML = `
            <div style="position: relative; width: 24px; height: 24px;">
                <div style="position: absolute; top: 0; left: 0; width: 24px; height: 24px; border-radius: 50%; background: rgba(14, 165, 233, 0.4); animation: ph-pulse 2s infinite ease-out;"></div>
                <div style="position: absolute; top: 4px; left: 4px; width: 16px; height: 16px; border-radius: 50%; background: #0284C7; border: 2.5px solid #FFFFFF; box-shadow: 0 1px 4px rgba(0,0,0,0.4);"></div>
            </div>
            <style>
                @keyframes ph-pulse {
                    0% { transform: scale(0.6); opacity: 1; }
                    100% { transform: scale(2.2); opacity: 0; }
                }
            </style>
        `;
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

    window.fullScreenMap.init = function (container, options, onPointerMoved) {
        if (typeof container === 'string') {
            container = document.getElementById(container);
        }
        if (!container) return;

        registerPmtiles();

        options = options || {};
        currentPointerLat = options.lat != null ? options.lat : 32.2226;
        currentPointerLon = options.lon != null ? options.lon : -110.9747;
        const initialZoom = options.zoom != null ? options.zoom : 12;
        onPointerMovedCb = onPointerMoved;

        if (map) {
            try { map.remove(); } catch (e) { }
            map = null;
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
                }
            },
            layers: [
                {
                    id: "topo-layer",
                    type: "raster",
                    source: "topo-source",
                    layout: { visibility: "visible" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "osm-layer",
                    type: "raster",
                    source: "osm-source",
                    layout: { visibility: "none" },
                    paint: { "raster-opacity": 1.0 }
                },
                {
                    id: "satellite-layer",
                    type: "raster",
                    source: "satellite-source",
                    layout: { visibility: "none" },
                    paint: { "raster-opacity": 1.0 }
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
            anchor: 'center'
        });

        // Map Click: Move pointer to clicked position if tracking is not active
        map.on('click', function (e) {
            // Don't move pointer if user clicked on a control or marker
            if (e.originalEvent && e.originalEvent.target && e.originalEvent.target.closest('.maplibregl-control-container, .ph-pointer-marker')) {
                return;
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
            window.fullScreenMap.resize();
        });
    };

    window.fullScreenMap.centerOn = function (lat, lon, zoom) {
        if (!map) return;
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

    window.fullScreenMap.updateLivePosition = function (lat, lon, accuracyM, autoCenter) {
        if (!map) return;
        isLiveTracking = true;

        if (liveMarker) {
            liveMarker.setLngLat([lon, lat]);
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
            map.easeTo({
                center: [lon, lat],
                duration: 600
            });
        }
    };

    window.fullScreenMap.stopLiveTracking = function () {
        isLiveTracking = false;
        if (liveMarker && liveMarker._map) {
            liveMarker.remove();
        }
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

    window.fullScreenMap.addTrackPoint = function (lat, lon) {
        recordedPoints.push([lon, lat]);
        if (isLoaded && map && map.getSource('gps-track-source')) {
            map.getSource('gps-track-source').setData({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: recordedPoints
                }
            });
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
    let currentSelectedBaseLayer = 'topo';

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

        // Anchor layer beneath which PMTiles are rendered (so UI overlays like live accuracy stay on top)
        const anchorLayer = map.getLayer('live-accuracy-fill') ? 'live-accuracy-fill' : undefined;

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

            // Process and add sources defined in styleObj
            const sources = styleObj.sources || {};
            for (const [sId, srcDef] of Object.entries(sources)) {
                try {
                    const sCopy = JSON.parse(JSON.stringify(srcDef));
                    let resolvedUrl = null;

                    // 1. Direct match with an enabled PMTiles layer
                    for (const l of enabledLayers) {
                        const lName = l.fileName.toLowerCase();
                        const lStem = lName.replace(/\.pmtiles$/i, '');
                        if (sCopy.url && typeof sCopy.url === 'string') {
                            if (sCopy.url.toLowerCase().includes(lName) || sCopy.url.toLowerCase().includes(lStem)) {
                                resolvedUrl = `pmtiles://${l.url}`;
                                consumedFileNames.add(l.fileName);
                                break;
                            }
                        }
                        if (sId.toLowerCase() === lStem || sId.toLowerCase().includes(lStem) || lStem.includes(sId.toLowerCase())) {
                            resolvedUrl = `pmtiles://${l.url}`;
                            consumedFileNames.add(l.fileName);
                            break;
                        }
                    }

                    // 2. Type fallback if not matched by name
                    if (!resolvedUrl) {
                        if (sCopy.type === 'vector') {
                            const vec = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && !l.fileName.toLowerCase().includes('hillshade') && !l.fileName.toLowerCase().includes('raster')) ||
                                        enabledLayers.find(l => !l.fileName.toLowerCase().includes('hillshade') && !l.fileName.toLowerCase().includes('raster')) ||
                                        enabledLayers[0];
                            if (vec) {
                                resolvedUrl = `pmtiles://${vec.url}`;
                                consumedFileNames.add(vec.fileName);
                            }
                        } else if (sCopy.type === 'raster') {
                            const ras = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && (l.fileName.toLowerCase().includes('hillshade') || l.fileName.toLowerCase().includes('raster'))) ||
                                        enabledLayers.find(l => l.fileName.toLowerCase().includes('hillshade') || l.fileName.toLowerCase().includes('raster')) ||
                                        enabledLayers[0];
                            if (ras) {
                                resolvedUrl = `pmtiles://${ras.url}`;
                                consumedFileNames.add(ras.fileName);
                            }
                        } else if (sCopy.type === 'raster-dem') {
                            const dem = enabledLayers.find(l => !consumedFileNames.has(l.fileName) && (l.fileName.toLowerCase().includes('terrain') || l.fileName.toLowerCase().includes('dem') || l.fileName.toLowerCase().includes('rgb'))) ||
                                        enabledLayers.find(l => l.fileName.toLowerCase().includes('terrain') || l.fileName.toLowerCase().includes('dem') || l.fileName.toLowerCase().includes('rgb'));
                            if (dem) {
                                resolvedUrl = `pmtiles://${dem.url}`;
                                consumedFileNames.add(dem.fileName);
                            }
                        }
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

            // Add style layers
            for (const lDef of styleObj.layers) {
                if (!lDef || !lDef.id) continue;
                try {
                    const lCopy = JSON.parse(JSON.stringify(lDef));
                    lCopy.layout = lCopy.layout || {};
                    lCopy.layout.visibility = (currentSelectedBaseLayer === 'pmtiles') ? 'visible' : 'none';

                    if (lCopy.source && !map.getSource(lCopy.source)) {
                        continue;
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
    };

    window.fullScreenMap.setBaseLayer = async function (layerName) {
        if (!map || !isLoaded) {
            currentSelectedBaseLayer = layerName;
            return;
        }
        if (layerName === 'pmtiles' && window.fullScreenMap && window.fullScreenMap.isPmTilesSupported === false) {
            layerName = 'topo';
        }
        currentSelectedBaseLayer = layerName;

        const layers = ['topo-layer', 'osm-layer', 'satellite-layer'];
        layers.forEach(id => {
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
            return;
        }

        // Hide PMTiles
        activePmTileLayerIds.forEach(id => {
            if (map.getLayer(id)) {
                map.setLayoutProperty(id, 'visibility', 'none');
            }
        });

        let targetId = 'topo-layer';
        if (layerName === 'streets' || layerName === 'osm') targetId = 'osm-layer';
        else if (layerName === 'satellite') targetId = 'satellite-layer';

        if (map.getLayer(targetId)) {
            map.setLayoutProperty(targetId, 'visibility', 'visible');
        }
    };

    function recreateMap(lat, lon) {
        if (!mapContainerElement) return;
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
            { lat: initLat, lon: initLon, zoom: 12 },
            onPointerMovedCb
        );
        if (pendingPmTilesConfig) {
            window.fullScreenMap.setPmTilesLayers(pendingPmTilesConfig);
        }
        if (currentSelectedBaseLayer) {
            window.fullScreenMap.setBaseLayer(currentSelectedBaseLayer);
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
                zoom: (typeof zoom === 'number' && !isNaN(zoom)) ? zoom : 12,
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
})();

