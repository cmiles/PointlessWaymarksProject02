// MapLibre GL JS Map with Observer Pin, Draggable Direction Arrow & FOV Sector for PointedHorizons
// Prepared with first-class PMTiles protocol support
(function () {
    window.pointedMap = window.pointedMap || {};

    let map = null;
    let mapContainerElement = null;
    let pendingPmTilesConfig = null;
    let observerMarker = null;
    let arrowHandleMarker = null;
    let farthestVisibleDot = null;
    let isLoaded = false;

    let currentLat = 32.4431;
    let currentLon = -110.7884; // Default Mount Lemmon, AZ
    let currentBearing = 270;   // Looking West towards Tucson / Catalina valleys
    let currentFov = 90;

    let onLocationPickedCb = null;
    let onDirectionChangedCb = null;

    let isInteractingWithHandle = false;
    let isInteractingWithObserver = false;
    let pendingFarthestUpdate = null;
    let activeTerrainSourceId = null;

    // Register PMTiles protocol on maplibregl if available
    function registerPmtiles() {
        if (typeof window !== 'undefined' && window.pmtiles && window.maplibregl) {
            try {
                if (!window.pointedMap._pmtilesRegistered) {
                    const protocol = new window.pmtiles.Protocol();
                    window.maplibregl.addProtocol("pmtiles", protocol.tile);
                    window.pointedMap.pmtilesProtocol = protocol;
                    window.pointedMap._pmtilesRegistered = true;
                }
            } catch (e) {
                console.warn("PMTiles protocol registration:", e);
            }
        }
    }
    registerPmtiles();

    // Generous optimistic constant for human line-of-sight limit on Earth
    const SIGHT_LIMIT_KM = 300.0;

    // Contiguous United States (CONUS) approximate bounding box with small buffer
    const CONUS_SOUTH = 24.0;
    const CONUS_NORTH = 50.5;
    const CONUS_WEST = -126.0;
    const CONUS_EAST = -66.0;

    function clampToConus(lat, lon) {
        const cLat = Math.max(CONUS_SOUTH, Math.min(CONUS_NORTH, lat));
        const cLon = Math.max(CONUS_WEST, Math.min(CONUS_EAST, lon));
        return [cLat, cLon];
    }

    function toRad(deg) { return (deg * Math.PI) / 180; }
    function toDeg(rad) { return (rad * 180) / Math.PI; }

    function normalizeAngle(a) {
        a = a % 360;
        if (a < 0) a += 360;
        return a;
    }

    // Projects a destination lat/lon given start point, distance (km), and bearing (degrees)
    function projectLatLng(lat, lon, distanceKm, bearingDeg) {
        const R = 6371; // Earth radius in km
        const dByR = distanceKm / R;
        const bRad = toRad(bearingDeg);
        const latRad = toRad(lat);
        const lonRad = toRad(lon);

        const lat2 = Math.asin(Math.sin(latRad) * Math.cos(dByR) + Math.cos(latRad) * Math.sin(dByR) * Math.cos(bRad));
        const lon2 = lonRad + Math.atan2(Math.sin(bRad) * Math.sin(dByR) * Math.cos(latRad), Math.cos(dByR) - Math.sin(latRad) * Math.sin(lat2));

        return [toDeg(lat2), toDeg(lon2)];
    }

    // Generates dense points along a great circle ray to eliminate Mercator projection curvature divergence
    function generateRayPath(lat, lon, distanceKm, bearingDeg, stepKm = 2.0) {
        const pts = [[lat, lon]];
        const steps = Math.ceil(distanceKm / stepKm);
        for (let i = 1; i <= steps; i++) {
            const d = Math.min(i * stepKm, distanceKm);
            pts.push(projectLatLng(lat, lon, d, bearingDeg));
        }
        return pts;
    }

    // Computes bearing in degrees from point A to point B
    function computeBearing(lat1, lon1, lat2, lon2) {
        const y = Math.sin(toRad(lon2 - lon1)) * Math.cos(toRad(lat2));
        const x = Math.cos(toRad(lat1)) * Math.sin(toRad(lat2)) -
                  Math.sin(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.cos(toRad(lon2 - lon1));
        return normalizeAngle(toDeg(Math.atan2(y, x)));
    }

    // Dynamic distance for visual arrow handle based on zoom
    function getArrowDistanceKm() {
        if (!map) return 6;
        const zoom = map.getZoom();
        return Math.max(0.2, 6000 / Math.pow(2, zoom));
    }

    function clearFarthestVisibleLine() {
        pendingFarthestUpdate = null;
        if (farthestVisibleDot) {
            farthestVisibleDot.remove();
            farthestVisibleDot = null;
        }
        if (map && isLoaded && map.getSource('farthest-line-source')) {
            map.getSource('farthest-line-source').setData({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: []
                }
            });
        }
    }

    function updateArrowAndSector(updateHandlePos = true) {
        if (!map || !observerMarker) return;

        const distKm = getArrowDistanceKm();
        const tipPos = projectLatLng(currentLat, currentLon, distKm, currentBearing); // [lat, lon]

        // 1. Update Direction of View Line (Dashed sight limit line across 300 km)
        const sightPoints = generateRayPath(currentLat, currentLon, SIGHT_LIMIT_KM, currentBearing, 2.0);
        const sightCoords = sightPoints.map(pt => [pt[1], pt[0]]); // [lng, lat]

        if (isLoaded && map.getSource('direction-line-source')) {
            map.getSource('direction-line-source').setData({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: sightCoords
                }
            });
        }

        // 2. Update Arrowhead polygon behind the handle
        const leftWing = projectLatLng(tipPos[0], tipPos[1], distKm * 0.22, normalizeAngle(currentBearing + 155));
        const rightWing = projectLatLng(tipPos[0], tipPos[1], distKm * 0.22, normalizeAngle(currentBearing - 155));
        const arrowCoords = [[
            [tipPos[1], tipPos[0]],
            [leftWing[1], leftWing[0]],
            [rightWing[1], rightWing[0]],
            [tipPos[1], tipPos[0]]
        ]];

        if (isLoaded && map.getSource('arrowhead-source')) {
            map.getSource('arrowhead-source').setData({
                type: "Feature",
                geometry: {
                    type: "Polygon",
                    coordinates: arrowCoords
                }
            });
        }

        // 3. Update Draggable Rotation Handle Marker at arrow tip
        if (!arrowHandleMarker) {
            const handleEl = document.createElement('div');
            handleEl.className = 'pointed-arrow-handle';
            handleEl.style.boxSizing = 'border-box';
            handleEl.style.width = '32px';
            handleEl.style.height = '32px';
            handleEl.style.margin = '0';
            handleEl.style.background = '#0284C7';
            handleEl.style.border = '3px solid #FFFFFF';
            handleEl.style.borderRadius = '50%';
            handleEl.style.display = 'flex';
            handleEl.style.alignItems = 'center';
            handleEl.style.justifyContent = 'center';
            handleEl.style.boxShadow = '0 3px 8px rgba(0,0,0,0.45)';
            handleEl.style.cursor = 'grab';
            handleEl.style.pointerEvents = 'auto';
            handleEl.innerHTML = `
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#FFFFFF" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="pointer-events:none;">
                    <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/>
                </svg>`;

            handleEl.addEventListener('mousedown', function () {
                isInteractingWithHandle = true;
                clearFarthestVisibleLine();
            });
            handleEl.addEventListener('touchstart', function () {
                isInteractingWithHandle = true;
                clearFarthestVisibleLine();
            });

            arrowHandleMarker = new window.maplibregl.Marker({
                element: handleEl,
                draggable: true
            })
            .setLngLat([tipPos[1], tipPos[0]])
            .addTo(map);

            arrowHandleMarker.on('dragstart', function () {
                isInteractingWithHandle = true;
                clearFarthestVisibleLine();
                handleEl.style.cursor = 'grabbing';
            });

            arrowHandleMarker.on('drag', function () {
                isInteractingWithHandle = true;
                clearFarthestVisibleLine();
                const pos = arrowHandleMarker.getLngLat();
                currentBearing = Math.round(computeBearing(currentLat, currentLon, pos.lat, pos.lng) * 10) / 10;
                updateArrowAndSector(false); // Don't call setLngLat on handle while dragging

                if (onDirectionChangedCb) {
                    onDirectionChangedCb(currentBearing);
                }
            });

            arrowHandleMarker.on('dragend', function () {
                handleEl.style.cursor = 'grab';
                const pos = arrowHandleMarker.getLngLat();
                currentBearing = Math.round(computeBearing(currentLat, currentLon, pos.lat, pos.lng) * 10) / 10;
                updateArrowAndSector(true);

                if (onDirectionChangedCb) {
                    onDirectionChangedCb(currentBearing);
                }

                // Prevent trailing click events from moving observer location
                setTimeout(() => {
                    isInteractingWithHandle = false;
                }, 300);
            });
        } else if (updateHandlePos) {
            arrowHandleMarker.setLngLat([tipPos[1], tipPos[0]]);
        }

        // 4. Update Field Of View Translucent Sector Wedge
        const sectorPoints = [[currentLon, currentLat]];
        const halfFov = currentFov / 2;
        const steps = 16;
        for (let i = 0; i <= steps; i++) {
            const angle = normalizeAngle((currentBearing - halfFov) + (i / steps) * currentFov);
            const p = projectLatLng(currentLat, currentLon, distKm * 1.5, angle);
            sectorPoints.push([p[1], p[0]]); // [lng, lat]
        }
        sectorPoints.push([currentLon, currentLat]);

        if (isLoaded && map.getSource('fov-source')) {
            map.getSource('fov-source').setData({
                type: "Feature",
                geometry: {
                    type: "Polygon",
                    coordinates: [sectorPoints]
                }
            });
        }
    }

    let configuredPmTiles = [];
    let activePmTileLayerIds = [];
    let activePmTileSourceIds = [];
    let selectedBaseLayer = 'osm';

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
            const sid = `pmtiles-source-${i}`;
            if (map.getSource(sid)) {
                try { map.removeSource(sid); } catch (e) { }
            }
        }

        if (map && typeof map.getTerrain === 'function' && map.getTerrain()) {
            try { map.setTerrain(null); } catch (e) { }
        }
        activeTerrainSourceId = null;
    }

    window.pointedMap.setPmTilesLayers = async function (config) {
        if (window.pointedMap && window.pointedMap.isPmTilesSupported === false) {
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
            console.info("[PointedMap] setPmTilesLayers saved as pending (map not loaded yet)");
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
                    console.warn("[PointedMap] Failed to parse styleJson string:", e);
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
                    console.warn("[PointedMap] Failed to fetch or parse styleUrl:", e);
                }
            }
        }

        // Anchor layer beneath which PMTiles are rendered (so UI overlays and PAD-US public lands overlay stay on top)
        const anchorLayer = map.getLayer('padus-layer')
            ? 'padus-layer'
            : (map.getLayer('fov-fill-layer') ? 'fov-fill-layer' : undefined);

        // Track PMTile files consumed by style sources
        const consumedFileNames = new Set();

        if (styleObj && styleObj.layers && styleObj.layers.length > 0) {
            console.info("[PointedMap] Applying custom PMTiles style file:", styleFileName);

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
                    console.warn("[PointedMap] Error adding style source:", sId, srcErr);
                }
            }

            // Add style layers
            for (const lDef of styleObj.layers) {
                if (!lDef || !lDef.id) continue;
                try {
                    const lCopy = JSON.parse(JSON.stringify(lDef));
                    lCopy.layout = lCopy.layout || {};
                    lCopy.layout.visibility = (selectedBaseLayer === 'pmtiles') ? 'visible' : 'none';

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
                    console.warn("[PointedMap] Error adding style layer:", lDef.id, lErr);
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
                        console.info("[PointedMap] 3D Terrain enabled from style:", styleObj.terrain.source);
                    } catch (tErr) {
                        console.warn("[PointedMap] Error applying style terrain:", tErr);
                    }
                }
            } else if (activeTerrainSourceId && map.getSource(activeTerrainSourceId)) {
                if (map.getPitch() > 20) {
                    try {
                        map.setTerrain({ source: activeTerrainSourceId, exaggeration: 1.25 });
                        console.info("[PointedMap] 3D Terrain enabled from source:", activeTerrainSourceId);
                    } catch (tErr) {
                        console.warn("[PointedMap] Error applying terrain source:", tErr);
                    }
                }
            }
        }

        // Render any remaining enabled PMTiles layers that weren't consumed by the style
        // (or all of them if no style file was selected or style had no layers)
        const unconsumedLayers = enabledLayers.filter(l => !consumedFileNames.has(l.fileName));

        for (let i = 0; i < unconsumedLayers.length; i++) {
            const l = unconsumedLayers[i];
            const sourceId = `pmtiles-source-${i}`;
            const layerId = `pmtiles-layer-${i}`;

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
                                        visibility: selectedBaseLayer === 'pmtiles' ? 'visible' : 'none'
                                    }
                                }, anchorLayer);
                                activePmTileLayerIds.push(vlId);
                            } catch (lErr) {
                                console.warn("[PointedMap] Could not add layer for source-layer:", layerName, lErr);
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
                                visibility: selectedBaseLayer === 'pmtiles' ? 'visible' : 'none'
                            }
                        }, anchorLayer);
                        activePmTileLayerIds.push(layerId);
                    }
                }
            } catch (err) {
                console.warn(`[PointedMap] Skipping PMTiles layer "${l.fileName}":`, err.message || err);
            }
        }

        ensurePadUsLayerPosition();
        if (map.getLayer('padus-layer')) {
            map.setLayoutProperty('padus-layer', 'visibility', isPadUsVisible ? 'visible' : 'none');
            map.setPaintProperty('padus-layer', 'raster-opacity', 0.35);
        }
    };

    let isPadUsVisible = false;

    function ensurePadUsLayerPosition() {
        if (!map || !map.getLayer('padus-layer')) return;
        const anchor = map.getLayer('fov-fill-layer') ? 'fov-fill-layer' : undefined;
        try {
            if (anchor && map.getLayer(anchor)) {
                map.moveLayer('padus-layer', anchor);
            } else {
                map.moveLayer('padus-layer');
            }
        } catch (e) {
            console.warn("[PointedMap] Error positioning padus-layer:", e);
        }
    }

    window.pointedMap.setPadUsVisible = function (visible) {
        isPadUsVisible = !!visible;
        if (map && map.getLayer('padus-layer')) {
            map.setLayoutProperty('padus-layer', 'visibility', isPadUsVisible ? 'visible' : 'none');
            map.setPaintProperty('padus-layer', 'raster-opacity', 0.35);
            if (isPadUsVisible) {
                ensurePadUsLayerPosition();
            }
        }
    };

    async function setBaseLayer(layerKey) {
        if (!map || !isLoaded) {
            selectedBaseLayer = layerKey;
            return;
        }
        if (layerKey === 'streets') layerKey = 'osm';
        if (layerKey === 'pmtiles' && window.pointedMap && window.pointedMap.isPmTilesSupported === false) {
            layerKey = 'osm';
        }
        selectedBaseLayer = layerKey;

        const baseRasterLayers = [
            'osm-layer',
            'topo-layer',
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

        if (layerKey === 'pmtiles') {
            if (activePmTileLayerIds.length === 0 && pendingPmTilesConfig) {
                try {
                    await window.pointedMap.setPmTilesLayers(pendingPmTilesConfig);
                } catch (e) {
                    console.warn("[PointedMap] Error loading pending PMTiles in setBaseLayer:", e);
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

        if (layerKey === 'osm') {
            if (map.getLayer('osm-layer')) map.setLayoutProperty('osm-layer', 'visibility', 'visible');
        } else if (layerKey === 'topo') {
            if (map.getLayer('topo-layer')) map.setLayoutProperty('topo-layer', 'visibility', 'visible');
        } else if (layerKey === 'satellite') {
            if (map.getLayer('satellite-layer')) map.setLayoutProperty('satellite-layer', 'visibility', 'visible');
        } else if (layerKey === 'quads') {
            if (map.getLayer('quads-layer')) map.setLayoutProperty('quads-layer', 'visibility', 'visible');
        } else if (layerKey === 'opentopo') {
            if (map.getLayer('opentopo-layer')) map.setLayoutProperty('opentopo-layer', 'visibility', 'visible');
        } else if (layerKey === 'hybrid') {
            if (map.getLayer('satellite-layer')) map.setLayoutProperty('satellite-layer', 'visibility', 'visible');
            if (map.getLayer('hybrid-reference-layer')) map.setLayoutProperty('hybrid-reference-layer', 'visibility', 'visible');
        }
    }

    window.pointedMap.setBaseLayer = setBaseLayer;

    // Custom Layer Switcher Control for MapLibre
    class LayerSwitcherControl {
        onAdd(mapInstance) {
            this._map = mapInstance;
            this._container = document.createElement('div');
            this._container.className = 'maplibregl-ctrl maplibregl-ctrl-group pointed-layer-ctrl';
            this._container.style.position = 'relative';
            this._container.style.backgroundColor = '#FFFFFF';
            this._container.style.borderRadius = '6px';
            this._container.style.boxShadow = '0 2px 6px rgba(0,0,0,0.25)';
            this._container.style.fontFamily = 'Inter, -apple-system, sans-serif';
            this._container.style.fontSize = '12px';
            this._container.style.color = '#1E293B';

            const btn = document.createElement('button');
            btn.type = 'button';
            btn.title = 'Switch Map Layer';
            btn.style.width = '32px';
            btn.style.height = '32px';
            btn.style.border = 'none';
            btn.style.background = 'transparent';
            btn.style.cursor = 'pointer';
            btn.style.display = 'flex';
            btn.style.alignItems = 'center';
            btn.style.justifyContent = 'center';
            btn.innerHTML = `
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#1E293B" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polygon points="12 2 2 7 12 12 22 7 12 2"></polygon>
                    <polyline points="2 17 12 22 22 17"></polyline>
                    <polyline points="2 12 12 17 22 12"></polyline>
                </svg>`;

            const panel = document.createElement('div');
            panel.className = 'pointed-layer-panel';
            panel.style.display = 'none';
            panel.style.position = 'absolute';
            panel.style.top = '0';
            panel.style.right = '36px';
            panel.style.backgroundColor = '#FFFFFF';
            panel.style.borderRadius = '6px';
            panel.style.boxShadow = '0 4px 12px rgba(0,0,0,0.2)';
            panel.style.padding = '8px 12px';
            panel.style.minWidth = '175px';
            panel.style.whiteSpace = 'nowrap';
            panel.style.zIndex = '1000';

            const stopProp = (e) => e.stopPropagation();
            panel.addEventListener('mousedown', stopProp);
            panel.addEventListener('touchstart', stopProp);
            panel.addEventListener('click', stopProp);

            const isPmTilesSupported = (window.pointedMap && window.pointedMap.isPmTilesSupported !== false);

            const layerOptions = [
                { key: 'osm', label: 'OpenStreetMap' },
                { key: 'topo', label: 'Topographic (Esri)' },
                { key: 'satellite', label: 'Satellite (Esri)' }
            ];

            if (isPmTilesSupported) {
                layerOptions.push({ key: 'pmtiles', label: 'PMTiles (Offline)' });
            }

            layerOptions.forEach(item => {
                const label = document.createElement('label');
                label.style.display = 'flex';
                label.style.alignItems = 'center';
                label.style.cursor = 'pointer';
                label.style.margin = '4px 0';

                const radio = document.createElement('input');
                radio.type = 'radio';
                radio.name = 'pointed-base-layer';
                radio.value = item.key;
                radio.checked = item.key === selectedBaseLayer;
                radio.style.marginRight = '8px';

                radio.addEventListener('change', () => {
                    if (radio.checked) {
                        selectedBaseLayer = item.key;
                        setBaseLayer(selectedBaseLayer);
                        if (item.key === 'pmtiles' && (!configuredPmTiles || configuredPmTiles.length === 0 || !configuredPmTiles.some(x => x.isEnabled))) {
                            if (window.pointedMap.onConfigurePmTilesRequested) {
                                window.pointedMap.onConfigurePmTilesRequested();
                            }
                        }
                    }
                });

                label.appendChild(radio);
                label.appendChild(document.createTextNode(item.label));
                panel.appendChild(label);
            });

            if (isPmTilesSupported) {
                // "Configure PMTiles..." button inside panel
                const configBtn = document.createElement('button');
                configBtn.type = 'button';
                configBtn.innerText = '⚙️ Configure PMTiles...';
                configBtn.style.display = 'block';
                configBtn.style.width = '100%';
                configBtn.style.marginTop = '8px';
                configBtn.style.padding = '5px 8px';
                configBtn.style.fontSize = '11px';
                configBtn.style.fontWeight = '600';
                configBtn.style.color = '#0284C7';
                configBtn.style.background = '#F0F9FF';
                configBtn.style.border = '1px solid #BAE6FD';
                configBtn.style.borderRadius = '5px';
                configBtn.style.cursor = 'pointer';
                configBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    togglePanel(false);
                    if (window.pointedMap.onConfigurePmTilesRequested) {
                        window.pointedMap.onConfigurePmTilesRequested();
                    }
                });
                panel.appendChild(configBtn);
            }

            const togglePanel = (show) => {
                panel.style.display = show ? 'block' : 'none';
            };

            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                togglePanel(panel.style.display === 'none');
            });

            this._container.addEventListener('mouseenter', () => togglePanel(true));
            this._container.addEventListener('mouseleave', () => togglePanel(false));

            this._container.appendChild(btn);
            this._container.appendChild(panel);
            return this._container;
        }

        onRemove() {
            if (this._container && this._container.parentNode) {
                this._container.parentNode.removeChild(this._container);
            }
            this._map = undefined;
        }
    }

    window.pointedMap.init = function (container, options, onLocationPicked, onDirectionChanged) {
        if (!container) return;

        registerPmtiles();

        onLocationPickedCb = onLocationPicked;
        onDirectionChangedCb = onDirectionChanged;

        if (options) {
            if (options.lat != null && options.lon != null) {
                const [cLat, cLon] = clampToConus(options.lat, options.lon);
                currentLat = cLat;
                currentLon = cLon;
            }
            if (options.bearing != null) currentBearing = options.bearing;
            if (options.fov != null) currentFov = options.fov;
        }

        container.innerHTML = '';
        container.style.position = 'relative';
        container.style.width = '100%';
        container.style.height = '100%';

        isLoaded = false;

        const mapStyle = {
            version: 8,
            glyphs: "https://demotiles.maplibre.org/font/{fontstack}/{range}.pbf",
            sources: {
                "osm-source": {
                    type: "raster",
                    tiles: [
                        "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
                    ],
                    tileSize: 256,
                    attribution: "© OpenStreetMap contributors",
                    maxzoom: 19
                },
                "topo-source": {
                    type: "raster",
                    tiles: [
                        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}"
                    ],
                    tileSize: 256,
                    attribution: "Tiles © Esri",
                    maxzoom: 18
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
                "fov-source": {
                    type: "geojson",
                    data: {
                        type: "Feature",
                        geometry: {
                            type: "Polygon",
                            coordinates: [[]]
                        }
                    }
                },
                "arrowhead-source": {
                    type: "geojson",
                    data: {
                        type: "Feature",
                        geometry: {
                            type: "Polygon",
                            coordinates: [[]]
                        }
                    }
                },
                "direction-line-source": {
                    type: "geojson",
                    data: {
                        type: "Feature",
                        geometry: {
                            type: "LineString",
                            coordinates: []
                        }
                    }
                },
                "farthest-line-source": {
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
                    id: "osm-layer",
                    type: "raster",
                    source: "osm-source",
                    layout: { visibility: "visible" }
                },
                {
                    id: "topo-layer",
                    type: "raster",
                    source: "topo-source",
                    layout: { visibility: "none" }
                },
                {
                    id: "satellite-layer",
                    type: "raster",
                    source: "satellite-source",
                    layout: { visibility: "none" }
                },
                {
                    id: "quads-layer",
                    type: "raster",
                    source: "quads-source",
                    layout: { visibility: "none" }
                },
                {
                    id: "opentopo-layer",
                    type: "raster",
                    source: "opentopo-source",
                    layout: { visibility: "none" }
                },
                {
                    id: "hybrid-reference-layer",
                    type: "raster",
                    source: "hybrid-reference-source",
                    layout: { visibility: "none" }
                },
                {
                    id: "padus-layer",
                    type: "raster",
                    source: "padus-source",
                    layout: { visibility: "none" },
                    paint: { "raster-opacity": 0.35 }
                },
                {
                    id: "fov-fill-layer",
                    type: "fill",
                    source: "fov-source",
                    paint: {
                        "fill-color": "#38BDF8",
                        "fill-opacity": 0.18
                    }
                },
                {
                    id: "fov-line-layer",
                    type: "line",
                    source: "fov-source",
                    paint: {
                        "line-color": "#38BDF8",
                        "line-width": 1.5,
                        "line-opacity": 0.7,
                        "line-dasharray": [2, 2]
                    }
                },
                {
                    id: "direction-line-layer",
                    type: "line",
                    source: "direction-line-source",
                    paint: {
                        "line-color": "#0284C7",
                        "line-width": 2.5,
                        "line-opacity": 0.45,
                        "line-dasharray": [3, 3]
                    }
                },
                {
                    id: "farthest-line-layer",
                    type: "line",
                    source: "farthest-line-source",
                    paint: {
                        "line-color": "#00BFFF",
                        "line-width": 3.5,
                        "line-opacity": 1.0
                    }
                },
                {
                    id: "arrowhead-fill-layer",
                    type: "fill",
                    source: "arrowhead-source",
                    paint: {
                        "fill-color": "#38BDF8",
                        "fill-opacity": 0.95
                    }
                },
                {
                    id: "arrowhead-line-layer",
                    type: "line",
                    source: "arrowhead-source",
                    paint: {
                        "line-color": "#0284C7",
                        "line-width": 1.5
                    }
                }
            ]
        };

        mapContainerElement = container;

        map = new window.maplibregl.Map({
            container: container,
            style: mapStyle,
            center: [currentLon, currentLat],
            zoom: 11,
            minZoom: 4,
            maxZoom: 18,
            maxPitch: 85,
            maxBounds: [[CONUS_WEST, CONUS_SOUTH], [CONUS_EAST, CONUS_NORTH]],
            pitchWithRotate: true,
            dragRotate: true,
            touchPitch: true
        });

        // Navigation control (zoom buttons, compass, bottom right)
        if (!document.getElementById('ph-map-nav-ctrl-styles')) {
            const navStyle = document.createElement('style');
            navStyle.id = 'ph-map-nav-ctrl-styles';
            navStyle.textContent = `
                @media (min-width: 768px) {
                    .maplibregl-ctrl-group {
                        border-radius: 6px !important;
                    }
                    .maplibregl-ctrl-group button {
                        width: 29px !important;
                        height: 29px !important;
                    }
                    .maplibregl-ctrl button .maplibregl-ctrl-icon {
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
                    .maplibregl-ctrl button .maplibregl-ctrl-icon {
                        transform: scale(1.8) !important;
                        transform-origin: center center !important;
                    }
                }
            `;
            document.head.appendChild(navStyle);
        }
        map.addControl(new window.maplibregl.NavigationControl({ showCompass: true, visualizePitch: true }), 'bottom-right');

        // Observer Pin Icon - Centered on Coordinate
        const observerEl = document.createElement('div');
        observerEl.className = 'pointed-observer-pin';
        observerEl.style.boxSizing = 'border-box';
        observerEl.style.width = '28px';
        observerEl.style.height = '28px';
        observerEl.style.margin = '0';
        observerEl.style.background = '#DC2626';
        observerEl.style.border = '3px solid #FFFFFF';
        observerEl.style.borderRadius = '50%';
        observerEl.style.display = 'flex';
        observerEl.style.alignItems = 'center';
        observerEl.style.justifyContent = 'center';
        observerEl.style.boxShadow = '0 3px 8px rgba(0,0,0,0.45)';
        observerEl.style.cursor = 'move';
        observerEl.style.pointerEvents = 'auto';
        observerEl.innerHTML = '<div style="width:7px; height:7px; background:#FFFFFF; border-radius:50%; pointer-events:none;"></div>';

        observerEl.addEventListener('mousedown', function () {
            isInteractingWithObserver = true;
            clearFarthestVisibleLine();
        });
        observerEl.addEventListener('touchstart', function () {
            isInteractingWithObserver = true;
            clearFarthestVisibleLine();
        });

        observerMarker = new window.maplibregl.Marker({
            element: observerEl,
            draggable: true
        })
        .setLngLat([currentLon, currentLat])
        .addTo(map);

        observerMarker.on('dragstart', function () {
            isInteractingWithObserver = true;
            clearFarthestVisibleLine();
            observerEl.style.cursor = 'grabbing';
        });

        observerMarker.on('drag', function () {
            isInteractingWithObserver = true;
            clearFarthestVisibleLine();
            const pos = observerMarker.getLngLat();
            const [cLat, cLon] = clampToConus(pos.lat, pos.lng);
            currentLat = cLat;
            currentLon = cLon;
            observerMarker.setLngLat([currentLon, currentLat]);
            updateArrowAndSector(true);
            if (onLocationPickedCb) {
                onLocationPickedCb(currentLat, currentLon);
            }
        });

        observerMarker.on('dragend', function () {
            observerEl.style.cursor = 'move';
            setTimeout(() => {
                isInteractingWithObserver = false;
            }, 300);
        });

        // Click anywhere on map to reposition observer (ONLY if not interacting with handle or marker)
        map.on('click', function (e) {
            if (isInteractingWithHandle || isInteractingWithObserver) {
                return;
            }
            clearFarthestVisibleLine();
            const [cLat, cLon] = clampToConus(e.lngLat.lat, e.lngLat.lng);
            currentLat = cLat;
            currentLon = cLon;
            if (observerMarker) {
                observerMarker.setLngLat([currentLon, currentLat]);
            }
            updateArrowAndSector(true);
            if (onLocationPickedCb) {
                onLocationPickedCb(currentLat, currentLon);
            }
        });

        map.on('zoom', function () {
            updateArrowAndSector(true);
        });

        // Defensive check: if camera ever penetrates terrain into NaN coordinates, auto-recover
        map.on('move', function () {
            const center = map.getCenter();
            if (!center || isNaN(center.lat) || isNaN(center.lng)) {
                console.warn("[PointedMap] Detected NaN center during move, auto-recovering...");
                recoverMapState(null, null, null);
            }
        });

        map.on('load', async function () {
            isLoaded = true;
            updateArrowAndSector(true);

            if (pendingPmTilesConfig) {
                try {
                    await window.pointedMap.setPmTilesLayers(pendingPmTilesConfig);
                } catch (e) {
                    console.warn("[PointedMap] Error applying pending PMTiles:", e);
                }
            }
            if (selectedBaseLayer) {
                window.pointedMap.setBaseLayer(selectedBaseLayer);
            }
            if (map.getLayer('padus-layer')) {
                map.setLayoutProperty('padus-layer', 'visibility', isPadUsVisible ? 'visible' : 'none');
            }

            if (pendingFarthestUpdate) {
                window.pointedMap.setFarthestVisibleLine(
                    pendingFarthestUpdate.targetLat,
                    pendingFarthestUpdate.targetLon,
                    pendingFarthestUpdate.distKm,
                    pendingFarthestUpdate.elevM,
                    pendingFarthestUpdate.bearing
                );
                pendingFarthestUpdate = null;
            }
        });

        // Invalidate map size on window/container resize
        if (window.ResizeObserver && !container._mapObserver) {
            container._mapObserver = new ResizeObserver(() => {
                if (map) map.resize();
            });
            container._mapObserver.observe(container);
        }
    };

    window.pointedMap.clearFarthestVisibleLine = clearFarthestVisibleLine;

    window.pointedMap.setFarthestVisibleLine = function (targetLat, targetLon, distKm, elevM, bearing) {
        if (!map) return;
        clearFarthestVisibleLine();

        if (distKm == null || distKm < 1.0) {
            return;
        }

        if (!isLoaded) {
            pendingFarthestUpdate = { targetLat, targetLon, distKm, elevM, bearing };
            return;
        }

        const sightBearing = (bearing != null && !isNaN(bearing)) ? bearing : currentBearing;

        // Generate dense points along the exact same ray and bearing as the dashed direction line
        // so that the solid line and dashed line are 100% coincident with zero divergence.
        // generateRayPath(lat, lon, distanceKm, bearingDeg, stepKm)
        const solidPoints = generateRayPath(currentLat, currentLon, distKm, sightBearing, 2.0);
        const endPt = solidPoints[solidPoints.length - 1]; // [lat, lon]
        const lineCoords = solidPoints.map(pt => [pt[1], pt[0]]); // [lng, lat]

        // Update GeoJSON source for farthest visible line
        if (map.getSource('farthest-line-source')) {
            map.getSource('farthest-line-source').setData({
                type: "Feature",
                geometry: {
                    type: "LineString",
                    coordinates: lineCoords
                }
            });
        }

        // Draw small dot at the end, exactly on the line
        const dotEl = document.createElement('div');
        dotEl.className = 'pointed-farthest-dot';
        dotEl.style.boxSizing = 'border-box';
        dotEl.style.width = '12px';
        dotEl.style.height = '12px';
        dotEl.style.margin = '0';
        dotEl.style.background = '#00BFFF';
        dotEl.style.border = '2px solid #FFFFFF';
        dotEl.style.borderRadius = '50%';
        dotEl.style.boxShadow = '0 2px 6px rgba(0,0,0,0.5)';
        dotEl.style.cursor = 'pointer';

        const stopDotProp = (e) => e.stopPropagation();
        dotEl.addEventListener('mousedown', stopDotProp);
        dotEl.addEventListener('touchstart', stopDotProp);
        dotEl.addEventListener('click', stopDotProp);

        const distMiles = (distKm * 0.621371).toFixed(1);
        const elevFt = Math.round(elevM * 3.28084);
        const popupContent = `
            <div style="font-size:12px; line-height:1.45; color:#0F172A; font-family:Inter,sans-serif; padding:2px;">
                <div style="font-weight:700; color:#0284C7; margin-bottom:2px;">Farthest Visible Terrain Feature</div>
                <div>Distance: <b>${distKm.toFixed(1)} km</b> (${distMiles} mi)</div>
                <div>Elevation: <b>${Math.round(elevM)} m</b> (${elevFt} ft)</div>
            </div>`;

        const popup = new window.maplibregl.Popup({ offset: [0, -8], closeButton: false, closeOnClick: false })
            .setHTML(popupContent);

        dotEl.addEventListener('mouseenter', () => popup.addTo(map));
        dotEl.addEventListener('mouseleave', () => popup.remove());

        farthestVisibleDot = new window.maplibregl.Marker({ element: dotEl })
            .setLngLat([endPt[1], endPt[0]])
            .setPopup(popup)
            .addTo(map);
    };

    window.pointedMap.setLocation = function (lat, lon, panTo) {
        clearFarthestVisibleLine();
        const [cLat, cLon] = clampToConus(lat, lon);
        currentLat = cLat;
        currentLon = cLon;
        if (observerMarker) {
            observerMarker.setLngLat([currentLon, currentLat]);
        }
        if (panTo && map) {
            map.panTo([currentLon, currentLat]);
        }
        updateArrowAndSector(true);
    };

    window.pointedMap.setDirection = function (bearing) {
        clearFarthestVisibleLine();
        currentBearing = normalizeAngle(bearing);
        updateArrowAndSector(true);
    };

    window.pointedMap.setFieldOfView = function (fov) {
        currentFov = fov;
        updateArrowAndSector(true);
    };

    window.pointedMap.invalidateSize = function () {
        if (map) {
            map.resize();
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
        const initLat = (typeof lat === 'number' && !isNaN(lat)) ? lat : currentLat;
        const initLon = (typeof lon === 'number' && !isNaN(lon)) ? lon : currentLon;

        window.pointedMap.init(
            mapContainerElement,
            { lat: initLat, lon: initLon, bearing: currentBearing, fov: currentFov },
            onLocationPickedCb,
            onDirectionChangedCb
        );
        if (pendingPmTilesConfig) {
            window.pointedMap.setPmTilesLayers(pendingPmTilesConfig);
        }
        if (selectedBaseLayer) {
            window.pointedMap.setBaseLayer(selectedBaseLayer);
        }
    }

    function recoverMapState(centerLon, centerLat, zoom) {
        let targetLon = (typeof centerLon === 'number' && !isNaN(centerLon)) ? centerLon : currentLon;
        let targetLat = (typeof centerLat === 'number' && !isNaN(centerLat)) ? centerLat : currentLat;
        if (typeof targetLon !== 'number' || isNaN(targetLon) || typeof targetLat !== 'number' || isNaN(targetLat)) {
            targetLon = -110.7884;
            targetLat = 32.4431;
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
                zoom: (typeof zoom === 'number' && !isNaN(zoom)) ? zoom : 11,
                pitch: 0,
                bearing: 0
            });

            // 4. If map state is still degenerate or NaN, recreate the map
            const center = map.getCenter();
            const pitch = map.getPitch();
            if (!center || isNaN(center.lat) || isNaN(center.lng) || isNaN(pitch) || pitch !== 0) {
                console.warn("[PointedMap] Recovering via recreateMap due to NaN transform state");
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
            console.warn("[PointedMap] Error in recoverMapState, recreating map:", err);
            recreateMap(targetLat, targetLon);
        }
    }

    window.pointedMap.toggle3d = function (force2d) {
        if (!map) {
            recreateMap(currentLat, currentLon);
            return false;
        }
        let currentPitch = map.getPitch();
        let center = map.getCenter();
        const isDegenerate = isNaN(currentPitch) || !center || isNaN(center.lat) || isNaN(center.lng);
        if (isDegenerate) {
            console.warn("[PointedMap] Map is degenerate in toggle3d; recreating clean 2D map");
            recreateMap(currentLat, currentLon);
            return false;
        }

        const isCurrently3d = (force2d === true) ? true : (force2d === false ? false : (currentPitch > 20));

        try {
            map.dragRotate.enable();
            map.touchPitch.enable();
        } catch (e) { }

        if (isCurrently3d) {
            // Toggling 3D OFF: unload elevation mesh and reset to flat 2D
            recoverMapState(currentLon, currentLat, null);
            return false;
        } else {
            // Toggling 3D ON: enable terrain, and pitch along azimuth
            if (activeTerrainSourceId && map.getSource(activeTerrainSourceId)) {
                try {
                    map.setTerrain({ source: activeTerrainSourceId, exaggeration: 1.25 });
                } catch (e) { }
            }
            map.easeTo({
                pitch: 65,
                bearing: currentBearing,
                duration: 800
            });
            return true;
        }
    };

    window.pointedMap.resetView = function () {
        // Guaranteed 100% recovery by tearing down and creating a fresh map instance
        recreateMap(currentLat, currentLon);
    };

    window.pointedMap.is3d = function () {
        return map ? (map.getPitch() > 25) : false;
    };
})();
