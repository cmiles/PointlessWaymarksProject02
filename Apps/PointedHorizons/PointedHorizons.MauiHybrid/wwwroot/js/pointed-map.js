// Leaflet Map with Observer Pin, Draggable Direction Arrow & FOV Sector for PointedHorizons
(function () {
    window.pointedMap = window.pointedMap || {};

    let map = null;
    let observerMarker = null;
    let arrowHandleMarker = null;
    let directionLine = null;
    let fovSectorPolygon = null;
    let arrowHeadPolygon = null;

    let currentLat = 32.4431;
    let currentLon = -110.7884; // Default Mount Lemmon, AZ
    let currentBearing = 270;   // Looking West towards Tucson / Catalina valleys
    let currentFov = 90;

    let onLocationPickedCb = null;
    let onDirectionChangedCb = null;

    let isInteractingWithHandle = false;
    let isInteractingWithObserver = false;

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
        // At zoom 10 -> ~6km, zoom 14 -> ~0.5km, zoom 8 -> ~25km
        return Math.max(0.2, 6000 / Math.pow(2, zoom));
    }

    function updateArrowAndSector(updateHandlePos = true) {
        if (!map || !observerMarker) return;

        const distKm = getArrowDistanceKm();
        const tipPos = projectLatLng(currentLat, currentLon, distKm, currentBearing);

        // 1. Update Direction Arrow Line (Starts strictly from the center of observer marker)
        if (!directionLine) {
            directionLine = L.polyline([[currentLat, currentLon], tipPos], {
                color: '#0284C7',
                weight: 3.5,
                opacity: 0.9,
                interactive: false
            }).addTo(map);
        } else {
            directionLine.setLatLngs([[currentLat, currentLon], tipPos]);
        }

        // 2. Update Arrowhead polygon
        const leftWing = projectLatLng(tipPos[0], tipPos[1], distKm * 0.22, normalizeAngle(currentBearing + 155));
        const rightWing = projectLatLng(tipPos[0], tipPos[1], distKm * 0.22, normalizeAngle(currentBearing - 155));

        if (!arrowHeadPolygon) {
            arrowHeadPolygon = L.polygon([tipPos, leftWing, rightWing], {
                color: '#0284C7',
                fillColor: '#38BDF8',
                fillOpacity: 0.95,
                weight: 1.5,
                interactive: false
            }).addTo(map);
        } else {
            arrowHeadPolygon.setLatLngs([tipPos, leftWing, rightWing]);
        }

        // 3. Update Draggable Rotation Handle Marker at arrow tip
        if (!arrowHandleMarker) {
            const handleIcon = L.divIcon({
                className: 'pointed-arrow-handle',
                html: `
                    <div style="
                        box-sizing:border-box; width:28px; height:28px; margin:0;
                        background:#0284C7; border:3px solid #FFFFFF; border-radius:50%;
                        display:flex; align-items:center; justify-content:center;
                        box-shadow:0 3px 8px rgba(0,0,0,0.45); cursor:grab;">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#FFFFFF" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/>
                        </svg>
                    </div>`,
                iconSize: [28, 28],
                iconAnchor: [14, 14]
            });

            arrowHandleMarker = L.marker(tipPos, {
                icon: handleIcon,
                draggable: true,
                zIndexOffset: 1500
            }).addTo(map);

            arrowHandleMarker.on('mousedown touchstart pointerdown', function (e) {
                isInteractingWithHandle = true;
                L.DomEvent.stopPropagation(e);
            });

            arrowHandleMarker.on('click', function (e) {
                isInteractingWithHandle = true;
                L.DomEvent.stopPropagation(e);
                setTimeout(() => {
                    isInteractingWithHandle = false;
                }, 300);
            });

            arrowHandleMarker.on('dragstart', function (e) {
                isInteractingWithHandle = true;
                L.DomEvent.stopPropagation(e);
            });

            arrowHandleMarker.on('drag', function (e) {
                isInteractingWithHandle = true;
                const pos = e.target.getLatLng();
                currentBearing = Math.round(computeBearing(currentLat, currentLon, pos.lat, pos.lng));
                updateArrowAndSector(false); // Don't call setLatLng on handle while dragging

                if (onDirectionChangedCb) {
                    onDirectionChangedCb(currentBearing);
                }
            });

            arrowHandleMarker.on('dragend', function (e) {
                const pos = e.target.getLatLng();
                currentBearing = Math.round(computeBearing(currentLat, currentLon, pos.lat, pos.lng));
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
            arrowHandleMarker.setLatLng(tipPos);
        }

        // 4. Update Field Of View Translucent Sector Wedge
        const sectorPoints = [[currentLat, currentLon]];
        const halfFov = currentFov / 2;
        const steps = 16;
        for (let i = 0; i <= steps; i++) {
            const angle = normalizeAngle((currentBearing - halfFov) + (i / steps) * currentFov);
            sectorPoints.push(projectLatLng(currentLat, currentLon, distKm * 1.5, angle));
        }

        if (!fovSectorPolygon) {
            fovSectorPolygon = L.polygon(sectorPoints, {
                color: '#38BDF8',
                weight: 1.5,
                opacity: 0.7,
                fillColor: '#38BDF8',
                fillOpacity: 0.18,
                dashArray: '3, 4',
                interactive: false
            }).addTo(map);
        } else {
            fovSectorPolygon.setLatLngs(sectorPoints);
        }
    }

    window.pointedMap.init = function (container, options, onLocationPicked, onDirectionChanged) {
        if (!container) return;

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

        // Ensure Leaflet assets icon path
        L.Icon.Default.imagePath = 'applibs/leaflet/images/';

        const conusBounds = L.latLngBounds(
            L.latLng(CONUS_SOUTH, CONUS_WEST),
            L.latLng(CONUS_NORTH, CONUS_EAST)
        );

        map = L.map(container, {
            center: [currentLat, currentLon],
            zoom: 11,
            minZoom: 4,
            maxZoom: 18,
            maxBounds: conusBounds,
            maxBoundsViscosity: 1.0,
            zoomControl: true,
            gestureHandling: true
        });

        // Base Tile Layers
        const osmLayer = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap contributors'
        }).addTo(map);

        const topoLayer = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 18,
            attribution: 'Tiles © Esri'
        });

        const satLayer = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 18,
            attribution: 'Tiles © Esri'
        });

        L.control.layers({
            "OpenStreetMap": osmLayer,
            "Topographic (Esri)": topoLayer,
            "Satellite (Esri)": satLayer
        }, null, { position: 'topright' }).addTo(map);

        // Observer Pin Icon - Perfectly Centered on Coordinate
        const observerIcon = L.divIcon({
            className: 'pointed-observer-pin',
            html: `
                <div style="
                    box-sizing:border-box; width:26px; height:26px; margin:0;
                    background:#DC2626; border:3px solid #FFFFFF; border-radius:50%;
                    display:flex; align-items:center; justify-content:center;
                    box-shadow:0 3px 8px rgba(0,0,0,0.45); cursor:move;">
                    <div style="width:7px; height:7px; background:#FFFFFF; border-radius:50%;"></div>
                </div>`,
            iconSize: [26, 26],
            iconAnchor: [13, 13]
        });

        observerMarker = L.marker([currentLat, currentLon], {
            icon: observerIcon,
            draggable: true,
            zIndexOffset: 1000
        }).addTo(map);

        observerMarker.on('mousedown touchstart pointerdown', function (e) {
            isInteractingWithObserver = true;
            L.DomEvent.stopPropagation(e);
        });

        observerMarker.on('click', function (e) {
            isInteractingWithObserver = true;
            L.DomEvent.stopPropagation(e);
            setTimeout(() => {
                isInteractingWithObserver = false;
            }, 300);
        });

        observerMarker.on('dragstart', function (e) {
            isInteractingWithObserver = true;
            L.DomEvent.stopPropagation(e);
        });

        observerMarker.on('drag', function (e) {
            isInteractingWithObserver = true;
            const pos = e.target.getLatLng();
            const [cLat, cLon] = clampToConus(pos.lat, pos.lng);
            currentLat = cLat;
            currentLon = cLon;
            e.target.setLatLng([currentLat, currentLon]);
            updateArrowAndSector(true);
            if (onLocationPickedCb) {
                onLocationPickedCb(currentLat, currentLon);
            }
        });

        observerMarker.on('dragend', function (e) {
            setTimeout(() => {
                isInteractingWithObserver = false;
            }, 300);
        });

        // Click anywhere on map to reposition observer (ONLY if not interacting with handle or marker)
        map.on('click', function (e) {
            if (isInteractingWithHandle || isInteractingWithObserver) {
                return;
            }
            const [cLat, cLon] = clampToConus(e.latlng.lat, e.latlng.lng);
            currentLat = cLat;
            currentLon = cLon;
            observerMarker.setLatLng([currentLat, currentLon]);
            updateArrowAndSector(true);
            if (onLocationPickedCb) {
                onLocationPickedCb(currentLat, currentLon);
            }
        });

        map.on('zoomend', function () {
            updateArrowAndSector(true);
        });

        updateArrowAndSector(true);

        // Invalidate map size on window/container resize
        if (window.ResizeObserver && !container._mapObserver) {
            container._mapObserver = new ResizeObserver(() => {
                if (map) map.invalidateSize();
            });
            container._mapObserver.observe(container);
        }
    };

    window.pointedMap.setLocation = function (lat, lon, panTo) {
        const [cLat, cLon] = clampToConus(lat, lon);
        currentLat = cLat;
        currentLon = cLon;
        if (observerMarker) {
            observerMarker.setLatLng([currentLat, currentLon]);
        }
        if (panTo && map) {
            map.panTo([currentLat, currentLon]);
        }
        updateArrowAndSector(true);
    };

    window.pointedMap.setDirection = function (bearing) {
        currentBearing = normalizeAngle(bearing);
        updateArrowAndSector(true);
    };

    window.pointedMap.setFieldOfView = function (fov) {
        currentFov = fov;
        updateArrowAndSector(true);
    };

    window.pointedMap.invalidateSize = function () {
        if (map) {
            map.invalidateSize();
        }
    };
})();
