using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenSilver;
using PwTrackTrimmer.Models;
using PwTrackTrimmer.ViewModels;

namespace PwTrackTrimmer.Controls
{
    public partial class MapControl : UserControl
    {
        private bool _isMapInitialized;
        private bool _isMapInitializing;
        private bool _showWaypoints = false;

        private TrackTrimmerViewModel ViewModel => DataContext as TrackTrimmerViewModel;

        public MapControl()
        {
            this.InitializeComponent();
            this.Loaded += MapControl_Loaded;
            this.MapContainer.Loaded += MapContainer_Loaded;
            this.SizeChanged += MapControl_SizeChanged;
            this.DataContextChanged += MapControl_DataContextChanged;
            this.KeyDown += MapControl_KeyDown;
        }

        private void MapControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeMap();
        }

        private void MapContainer_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeMap();
        }

        private void MapControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isMapInitialized)
            {
                Interop.ExecuteJavaScriptVoid(@"
                    if (window.leafletMapInstance) {
                        window.leafletMapInstance.invalidateSize();
                    }
                ");
            }
        }

        private void MapControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is TrackTrimmerViewModel oldVm)
            {
                oldVm.TrackLoaded -= Vm_TrackLoaded;
                oldVm.TrackDataChanged -= Vm_TrackDataChanged;
                oldVm.SelectionChanged -= Vm_SelectionChanged;
                oldVm.TrimChanged -= Vm_TrimChanged;
            }

            if (e.NewValue is TrackTrimmerViewModel newVm)
            {
                newVm.TrackLoaded += Vm_TrackLoaded;
                newVm.TrackDataChanged += Vm_TrackDataChanged;
                newVm.SelectionChanged += Vm_SelectionChanged;
                newVm.TrimChanged += Vm_TrimChanged;
                if (_isMapInitialized)
                {
                    RefreshTrackOnMap(fitBounds: true);
                }
            }
        }

        private void Vm_TrackLoaded()
        {
            RefreshTrackOnMap(fitBounds: true);
        }

        private void Vm_TrackDataChanged()
        {
            RefreshTrackOnMap(fitBounds: false);
        }

        private void Vm_SelectionChanged()
        {
            UpdateSelectedPoint(panToMarker: true);
        }

        private void Vm_TrimChanged()
        {
            RefreshTrackOnMap(fitBounds: false);
        }

        private void InitializeMap()
        {
            if (_isMapInitialized || _isMapInitializing) return;

            object div = Interop.GetDiv(MapContainer);
            if (div == null)
            {
                Dispatcher.BeginInvoke(InitializeMap);
                return;
            }

            string containerId = null;
            if (div is CSHTML5.Internal.INTERNAL_HtmlDomElementReference domRef)
            {
                containerId = domRef.UniqueIdentifier;
            }

            if (string.IsNullOrEmpty(containerId))
            {
                Dispatcher.BeginInvoke(InitializeMap);
                return;
            }

            _isMapInitializing = true;

            Action<int, double, double> onMoved = (idx, lat, lon) =>
            {
                Dispatcher.BeginInvoke(() => ViewModel?.MovePoint(idx, lat, lon));
            };

            Action<int, bool> onSelected = (idx, isShift) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (ViewModel?.Document != null && idx >= 0 && idx < ViewModel.Document.Points.Count)
                    {
                        ViewModel.SelectPointOrRange(idx, isShift);
                    }
                });
            };

            Action<int> onDeleted = (idx) =>
            {
                Dispatcher.BeginInvoke(() => ViewModel?.DeletePointAt(idx));
            };

            Action onDeleteSelected = () =>
            {
                Dispatcher.BeginInvoke(() => ViewModel?.DeleteSelectedPoint());
            };

            Action<int> onSetTrimStart = (idx) =>
            {
                Dispatcher.BeginInvoke(() => ViewModel?.SetTrimStart(idx));
            };

            Action<int> onSetTrimEnd = (idx) =>
            {
                Dispatcher.BeginInvoke(() => ViewModel?.SetTrimEnd(idx));
            };

            Action onReady = () =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    _isMapInitialized = true;
                    _isMapInitializing = false;
                    RefreshTrackOnMap(fitBounds: true);
                });
            };

            string initScript = @"
                (function(containerId, onMovedCb, onSelectedCb, onDeletedCb, onReadyCb, onDeleteSelectedCb, onSetTrimStartCb, onSetTrimEndCb) {
                    var styleId = 'pw-leaflet-custom-styles';
                    if (!document.getElementById(styleId)) {
                        var s = document.createElement('style');
                        s.id = styleId;
                        s.textContent = '.custom-selected-pin { background: transparent !important; border: none !important; } ' +
                                        '.pw-waypoint-bubble { pointer-events: auto !important; cursor: default; } ' +
                                        '.pw-trim-btn { cursor: pointer; border: none; border-radius: 4px; padding: 4px 10px; font-size: 11px; font-weight: 600; box-shadow: 0 1px 2px rgba(0,0,0,0.2); transition: opacity 0.15s, transform 0.1s; } ' +
                                        '.pw-trim-btn:hover { opacity: 0.9; } ' +
                                        '.pw-trim-btn:active { transform: scale(0.96); }';
                        document.head.appendChild(s);
                    }

                    if (typeof L === 'undefined' && !document.querySelector('script[src*=""leaflet.js""]')) {
                        var lCss = document.createElement('link');
                        lCss.rel = 'stylesheet';
                        lCss.href = 'applibs/leaflet/leaflet.css';
                        document.head.appendChild(lCss);

                        var lScript = document.createElement('script');
                        lScript.src = 'applibs/leaflet/leaflet.js';
                        document.head.appendChild(lScript);
                    }

                    window.pwInitLeafletAttempts = 0;
                    function tryInit() {
                        try {
                            if (typeof L === 'undefined') {
                                window.pwInitLeafletAttempts++;
                                if (window.pwInitLeafletAttempts < 80) {
                                    setTimeout(tryInit, 50);
                                }
                                return;
                            }

                            var container = document.getElementById(containerId);
                            if (!container || !container.isConnected) {
                                window.pwInitLeafletAttempts++;
                                if (window.pwInitLeafletAttempts < 80) {
                                    setTimeout(tryInit, 50);
                                }
                                return;
                            }

                            var w = container.clientWidth;
                            var h = container.clientHeight;
                            if (w < 30 || h < 30) {
                                window.pwInitLeafletAttempts++;
                                if (window.pwInitLeafletAttempts < 80) {
                                    setTimeout(tryInit, 50);
                                }
                                return;
                            }

                            if (window.leafletMapInstance) {
                                try { window.leafletMapInstance.remove(); } catch(e) {}
                            }

                            // Configure local leaflet default icon assets so no external requests are made
                            try {
                                delete L.Icon.Default.prototype._getIconUrl;
                                L.Icon.Default.mergeOptions({
                                    iconRetinaUrl: 'applibs/leaflet/images/marker-icon-2x.png',
                                    iconUrl: 'applibs/leaflet/images/marker-icon.png',
                                    shadowUrl: 'applibs/leaflet/images/marker-shadow.png'
                                });
                            } catch(e) {}

                            var mapDiv = container.querySelector('.pw-leaflet-div');
                            if (!mapDiv) {
                                mapDiv = document.createElement('div');
                                mapDiv.className = 'pw-leaflet-div';
                                mapDiv.style.position = 'absolute';
                                mapDiv.style.left = '0px';
                                mapDiv.style.top = '0px';
                                mapDiv.style.width = '100%';
                                mapDiv.style.height = '100%';
                                mapDiv.style.overflow = 'hidden';
                                mapDiv.style.zIndex = '1';
                                container.appendChild(mapDiv);
                            }

                            var map = L.map(mapDiv, {
                                zoomControl: true,
                                attributionControl: false,
                                maxZoom: 22,
                                preferCanvas: true
                            }).setView([37.9, -122.6], 12);

                            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                                maxZoom: 22,
                                maxNativeZoom: 19
                            }).addTo(map);

                            window.leafletMapInstance = map;
                            window.pwMapCallbacks = {
                                onMoved: onMovedCb,
                                onSelected: function(idx, isShift) {
                                    if (onSelectedCb) {
                                        onSelectedCb(idx, !!isShift);
                                    }
                                },
                                onDeleted: onDeletedCb,
                                onDeleteSelected: onDeleteSelectedCb,
                                onSetTrimStart: onSetTrimStartCb,
                                onSetTrimEnd: onSetTrimEndCb
                            };

                            if (!window._pwMapKeyDownAttached) {
                                window._pwMapKeyDownAttached = true;
                                window.addEventListener('keydown', function(e) {
                                    if (e.key === 'Delete' || e.key === 'Del' || e.keyCode === 46) {
                                        var el = document.activeElement;
                                        if (el) {
                                            var tag = (el.tagName || '').toLowerCase();
                                            if (tag === 'input' || tag === 'textarea' || el.isContentEditable) {
                                                return;
                                            }
                                        }
                                        if (window.pwMapCallbacks && window.pwMapCallbacks.onDeleteSelected) {
                                            window.pwMapCallbacks.onDeleteSelected();
                                            e.preventDefault();
                                        }
                                    }
                                });
                            }

                            window.pwTrackLayers = {
                                activePolyline: null,
                                trimmedStartPolyline: null,
                                trimmedEndPolyline: null,
                                startMarker: null,
                                endMarker: null,
                                selectedMarker: null,
                                selectedRangePolyline: null,
                                rangeStartMarker: null,
                                rangeEndMarker: null,
                                waypointsLayer: null
                            };

                            if (window.ResizeObserver && !container._hasResizeObserver) {
                                container._hasResizeObserver = true;
                                var ro = new ResizeObserver(function() {
                                    if (window.leafletMapInstance) {
                                        window.leafletMapInstance.invalidateSize();
                                    }
                                });
                                ro.observe(container);
                            }

                            setTimeout(function() {
                                if (map) map.invalidateSize();
                            }, 100);
                            setTimeout(function() {
                                if (map) map.invalidateSize();
                            }, 300);

                            if (onReadyCb) {
                                onReadyCb();
                            }
                        } catch (err) {
                            console.error('Error initializing map:', err);
                        }
                    }

                    tryInit();
                })($0, $1, $2, $3, $4, $5, $6, $7);
            ";

            Interop.ExecuteJavaScriptVoid(initScript, containerId, onMoved, onSelected, onDeleted, onReady, onDeleteSelected, onSetTrimStart, onSetTrimEnd);
        }

        public void RefreshTrackOnMap(bool fitBounds = false)
        {
            if (!_isMapInitialized) return;
            if (ViewModel?.Document == null || ViewModel.Document.Points.Count == 0) return;

            var points = ViewModel.Document.Points;
            int trimStart = Math.Max(0, Math.Min(ViewModel.TrimStartIndex, points.Count - 1));
            int trimEnd = Math.Max(0, Math.Min(ViewModel.TrimEndIndex, points.Count - 1));

            // Fast coordinate string builder
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                if (i > 0) sb.Append(",");
                sb.AppendFormat(CultureInfo.InvariantCulture, "[{0:F6},{1:F6}]", pt.Latitude, pt.Longitude);
            }
            sb.Append("]");

            string jsonCoords = sb.ToString();
            string showWpt = _showWaypoints ? "true" : "false";
            string autoFitJs = fitBounds ? "true" : "false";

            string updateScript = $@"
                (function() {{
                    var map = window.leafletMapInstance;
                    if (!map || typeof L === 'undefined') return;
                    try {{ map.invalidateSize(); }} catch(e) {{}}

                    var allCoords = {jsonCoords};
                    window.pwAllTrackCoords = allCoords;
                    var trimStart = {trimStart};
                    var trimEnd = {trimEnd};
                    var showPoints = {showWpt};
                    var autoFit = {autoFitJs};
                    var layers = window.pwTrackLayers;
                    if (!layers || !allCoords || allCoords.length === 0) return;

                    // Clean previous layers
                    if (layers.activePolyline) {{ try {{ map.removeLayer(layers.activePolyline); }} catch(e){{}} }}
                    if (layers.trimmedStartPolyline) {{ try {{ map.removeLayer(layers.trimmedStartPolyline); }} catch(e){{}} }}
                    if (layers.trimmedEndPolyline) {{ try {{ map.removeLayer(layers.trimmedEndPolyline); }} catch(e){{}} }}
                    if (layers.startMarker) {{ try {{ map.removeLayer(layers.startMarker); }} catch(e){{}} }}
                    if (layers.endMarker) {{ try {{ map.removeLayer(layers.endMarker); }} catch(e){{}} }}
                    if (layers.selectedMarker) {{ try {{ map.removeLayer(layers.selectedMarker); }} catch(e){{}} layers.selectedMarker = null; }}
                    if (layers.selectedRangePolyline) {{ try {{ map.removeLayer(layers.selectedRangePolyline); }} catch(e){{}} layers.selectedRangePolyline = null; }}
                    if (layers.rangeStartMarker) {{ try {{ map.removeLayer(layers.rangeStartMarker); }} catch(e){{}} layers.rangeStartMarker = null; }}
                    if (layers.rangeEndMarker) {{ try {{ map.removeLayer(layers.rangeEndMarker); }} catch(e){{}} layers.rangeEndMarker = null; }}

                    if (layers.waypointsLayer) {{
                        try {{ map.removeLayer(layers.waypointsLayer); }} catch(e){{}}
                        layers.waypointsLayer = null;
                    }}
                    if (layers.sampleMarkers) {{
                        for (var i = 0; i < layers.sampleMarkers.length; i++) {{
                            try {{ map.removeLayer(layers.sampleMarkers[i]); }} catch(e){{}}
                        }}
                    }}
                    layers.sampleMarkers = [];

                    var activeCoords = allCoords.slice(trimStart, trimEnd + 1);
                    var trimStartCoords = trimStart > 0 ? allCoords.slice(0, trimStart + 1) : [];
                    var trimEndCoords = trimEnd < allCoords.length - 1 ? allCoords.slice(trimEnd) : [];

                    // Render Active Polyline (High performance single SVG path)
                    if (activeCoords.length > 1) {{
                        layers.activePolyline = L.polyline(activeCoords, {{
                            color: '#2563EB',
                            weight: 4,
                            opacity: 0.9,
                            smoothFactor: 1.2
                        }}).addTo(map);

                        // Clicking anywhere on track polyline selects nearest point (or range if Shift held)
                        layers.activePolyline.on('click', function(e) {{
                            var isShift = !!(e.originalEvent && e.originalEvent.shiftKey);
                            var clicked = e.latlng;
                            var bestIdx = trimStart;
                            var bestDist = 1e9;
                            for (var i = trimStart; i <= trimEnd; i++) {{
                                var d = Math.pow(allCoords[i][0] - clicked.lat, 2) + Math.pow(allCoords[i][1] - clicked.lng, 2);
                                if (d < bestDist) {{
                                    bestDist = d;
                                    bestIdx = i;
                                }}
                            }}
                            if (window.pwMapCallbacks && window.pwMapCallbacks.onSelected) {{
                                window.pwMapCallbacks.onSelected(bestIdx, isShift);
                            }}
                        }});
                    }}

                    // Render Trimmed Polylines (Dashed Gray)
                    if (trimStartCoords.length > 1) {{
                        layers.trimmedStartPolyline = L.polyline(trimStartCoords, {{
                            color: '#94A3B8',
                            weight: 3,
                            dashArray: '5, 8',
                            opacity: 0.6
                        }}).addTo(map);
                    }}

                    if (trimEndCoords.length > 1) {{
                        layers.trimmedEndPolyline = L.polyline(trimEndCoords, {{
                            color: '#94A3B8',
                            weight: 3,
                            dashArray: '5, 8',
                            opacity: 0.6
                        }}).addTo(map);
                    }}

                    // Start Milestone Marker (Green)
                    if (allCoords.length > 0) {{
                        layers.startMarker = L.circleMarker(allCoords[0], {{
                            radius: 8,
                            fillColor: '#10B981',
                            color: '#FFFFFF',
                            weight: 2,
                            fillOpacity: 1
                        }}).addTo(map).bindTooltip('<b>START</b>');

                        layers.startMarker.on('click', function(ev) {{
                            L.DomEvent.stopPropagation(ev);
                            var isShift = !!(ev.originalEvent && ev.originalEvent.shiftKey);
                            if (window.pwMapCallbacks && window.pwMapCallbacks.onSelected) {{
                                window.pwMapCallbacks.onSelected(0, isShift);
                            }}
                        }});

                        // Finish Milestone Marker (Red)
                        layers.endMarker = L.circleMarker(allCoords[allCoords.length - 1], {{
                            radius: 8,
                            fillColor: '#EF4444',
                            color: '#FFFFFF',
                            weight: 2,
                            fillOpacity: 1
                        }}).addTo(map).bindTooltip('<b>FINISH</b>');

                        layers.endMarker.on('click', function(ev) {{
                            L.DomEvent.stopPropagation(ev);
                            var isShift = !!(ev.originalEvent && ev.originalEvent.shiftKey);
                            if (window.pwMapCallbacks && window.pwMapCallbacks.onSelected) {{
                                window.pwMapCallbacks.onSelected(allCoords.length - 1, isShift);
                            }}
                        }});
                    }}

                    // If user toggles waypoints, render all trackpoints using high-performance canvas layer group
                    if (showPoints && allCoords.length > 0) {{
                        var group = L.layerGroup();
                        for (var i = 0; i < allCoords.length; i++) {{
                            // Points 0 and allCoords.length - 1 already have startMarker and endMarker
                            if (i === 0 || i === allCoords.length - 1) continue;

                            var isTrimmed = (i < trimStart || i > trimEnd);
                            var m = L.circleMarker(allCoords[i], {{
                                radius: isTrimmed ? 3 : 4,
                                fillColor: isTrimmed ? '#94A3B8' : '#38BDF8',
                                color: isTrimmed ? '#64748B' : '#0284C7',
                                weight: 1,
                                fillOpacity: isTrimmed ? 0.6 : 0.9,
                                interactive: true
                            }});

                            (function(idx) {{
                                m.on('click', function(ev) {{
                                    L.DomEvent.stopPropagation(ev);
                                    var isShift = !!(ev.originalEvent && ev.originalEvent.shiftKey);
                                    if (window.pwMapCallbacks && window.pwMapCallbacks.onSelected) {{
                                        window.pwMapCallbacks.onSelected(idx, isShift);
                                    }}
                                }});
                            }})(i);

                            group.addLayer(m);
                        }}
                        group.addTo(map);
                        layers.waypointsLayer = group;
                    }}

                    // Auto-fit bounds safely only when explicit auto-fit requested
                    if (autoFit && allCoords.length > 1) {{
                        try {{
                            var b = L.latLngBounds(allCoords);
                            if (b && b.isValid && b.isValid()) {{
                                map.fitBounds(b, {{ padding: [30, 30] }});
                            }}
                        }} catch(e) {{}}
                    }}
                }})();
            ";

            Interop.ExecuteJavaScriptVoid(updateScript);
            UpdateSelectedPoint(panToMarker: false);
        }

        public void UpdateSelectedPoint(bool panToMarker = false)
        {
            if (ViewModel == null || ViewModel.Document == null) return;

            bool isRange = ViewModel.IsRangeSelected;
            int startIdx = ViewModel.SelectionStartIndex;
            int endIdx = ViewModel.SelectionEndIndex;

            if (startIdx < 0 || endIdx < 0)
            {
                Interop.ExecuteJavaScriptVoid(@"
                    (function() {
                        var map = window.leafletMapInstance;
                        var layers = window.pwTrackLayers;
                        if (!layers || !map) return;
                        if (layers.selectedMarker) {
                            try { map.removeLayer(layers.selectedMarker); } catch(e){}
                            layers.selectedMarker = null;
                        }
                        if (layers.selectedRangePolyline) {
                            try { map.removeLayer(layers.selectedRangePolyline); } catch(e){}
                            layers.selectedRangePolyline = null;
                        }
                        if (layers.rangeStartMarker) {
                            try { map.removeLayer(layers.rangeStartMarker); } catch(e){}
                            layers.rangeStartMarker = null;
                        }
                        if (layers.rangeEndMarker) {
                            try { map.removeLayer(layers.rangeEndMarker); } catch(e){}
                            layers.rangeEndMarker = null;
                        }
                    })();
                ");
                return;
            }

            if (isRange)
            {
                string script = string.Format(CultureInfo.InvariantCulture, @"
                    (function() {{
                        var map = window.leafletMapInstance;
                        var layers = window.pwTrackLayers;
                        var allCoords = window.pwAllTrackCoords;
                        if (!map || !layers || !allCoords || typeof L === 'undefined') return;

                        if (layers.selectedMarker) {{
                            try {{ map.removeLayer(layers.selectedMarker); }} catch(e){{}}
                            layers.selectedMarker = null;
                        }}
                        if (layers.selectedRangePolyline) {{
                            try {{ map.removeLayer(layers.selectedRangePolyline); }} catch(e){{}}
                            layers.selectedRangePolyline = null;
                        }}
                        if (layers.rangeStartMarker) {{
                            try {{ map.removeLayer(layers.rangeStartMarker); }} catch(e){{}}
                            layers.rangeStartMarker = null;
                        }}
                        if (layers.rangeEndMarker) {{
                            try {{ map.removeLayer(layers.rangeEndMarker); }} catch(e){{}}
                            layers.rangeEndMarker = null;
                        }}

                        var s = {0};
                        var e = {1};
                        if (s >= 0 && e < allCoords.length && s <= e) {{
                            var rangeCoords = allCoords.slice(s, e + 1);
                            if (rangeCoords.length > 0) {{
                                layers.selectedRangePolyline = L.polyline(rangeCoords, {{
                                    color: '#38BDF8',
                                    weight: 7,
                                    opacity: 0.95
                                }}).addTo(map);

                                var iconStart = L.divIcon({{
                                    className: 'custom-selected-pin',
                                    html: '<div style=""width:12px;height:12px;border-radius:6px;background:#38BDF8;border:2px solid #FFFFFF;box-shadow:0 0 8px rgba(56,189,248,0.8);""></div>',
                                    iconSize: [12, 12],
                                    iconAnchor: [6, 6]
                                }});
                                var iconEnd = L.divIcon({{
                                    className: 'custom-selected-pin',
                                    html: '<div style=""width:12px;height:12px;border-radius:6px;background:#F59E0B;border:2px solid #FFFFFF;box-shadow:0 0 8px rgba(245,158,11,0.8);""></div>',
                                    iconSize: [12, 12],
                                    iconAnchor: [6, 6]
                                }});

                                layers.rangeStartMarker = L.marker(rangeCoords[0], {{ icon: iconStart }}).addTo(map);
                                layers.rangeStartMarker.bindTooltip('<b>Range Start: #' + (s + 1) + '</b>');

                                layers.rangeEndMarker = L.marker(rangeCoords[rangeCoords.length - 1], {{ icon: iconEnd }}).addTo(map);
                                layers.rangeEndMarker.bindTooltip('<b>Range End: #' + (e + 1) + '</b>');

                                if ({2}) {{
                                    try {{
                                        var b = layers.selectedRangePolyline.getBounds();
                                        if (b && b.isValid && b.isValid()) {{
                                            map.fitBounds(b, {{ padding: [60, 60], maxZoom: 17 }});
                                        }}
                                    }} catch(err) {{}}
                                }}
                            }}
                        }}
                    }})();
                ", startIdx, endIdx, panToMarker ? "true" : "false");

                Interop.ExecuteJavaScriptVoid(script);
            }
            else
            {
                var pt = ViewModel.Document.Points[startIdx];
                string panToJs = panToMarker ? "true" : "false";

                string script = string.Format(CultureInfo.InvariantCulture, @"
                    (function() {{
                        var map = window.leafletMapInstance;
                        var layers = window.pwTrackLayers;
                        if (!map || !layers || typeof L === 'undefined') return;

                        if (layers.selectedRangePolyline) {{
                            try {{ map.removeLayer(layers.selectedRangePolyline); }} catch(e){{}}
                            layers.selectedRangePolyline = null;
                        }}
                        if (layers.rangeStartMarker) {{
                            try {{ map.removeLayer(layers.rangeStartMarker); }} catch(e){{}}
                            layers.rangeStartMarker = null;
                        }}
                        if (layers.rangeEndMarker) {{
                            try {{ map.removeLayer(layers.rangeEndMarker); }} catch(e){{}}
                            layers.rangeEndMarker = null;
                        }}

                        if (layers.selectedMarker) {{
                            try {{ map.removeLayer(layers.selectedMarker); }} catch(e){{}}
                        }}

                        var icon = L.divIcon({{
                            className: 'custom-selected-pin',
                            html: '<div style=""width:16px;height:16px;border-radius:8px;background:#F59E0B;border:3px solid #FFFFFF;box-shadow:0 0 10px rgba(245,158,11,0.8);cursor:move;""></div>',
                            iconSize: [16, 16],
                            iconAnchor: [8, 8]
                        }});

                        var markerPos = [{0:F6}, {1:F6}];
                        layers.selectedMarker = L.marker(markerPos, {{
                            icon: icon,
                            draggable: true
                        }}).addTo(map);

                        var bubbleHtml = '<div style=""font-family:system-ui,-apple-system,sans-serif;font-size:12px;text-align:center;line-height:1.4;min-width:110px;"">' +
                            '<b>Point #{2}</b><br/>' +
                            '<span style=""color:#475569;font-size:11px;"">{3}</span>' +
                            '<div style=""display:flex;gap:6px;margin-top:6px;justify-content:center;"">' +
                                '<button type=""button"" class=""pw-trim-btn"" onclick=""event.stopPropagation();if(window.pwMapCallbacks&&window.pwMapCallbacks.onSetTrimStart)window.pwMapCallbacks.onSetTrimStart({5});"" title=""Set trim start to Point #{2}"" style=""background:{7};color:#ffffff;"">{8}</button>' +
                                '<button type=""button"" class=""pw-trim-btn"" onclick=""event.stopPropagation();if(window.pwMapCallbacks&&window.pwMapCallbacks.onSetTrimEnd)window.pwMapCallbacks.onSetTrimEnd({5});"" title=""Set trim end to Point #{2}"" style=""background:{9};color:#ffffff;"">{10}</button>' +
                            '</div>' +
                        '</div>';

                        layers.selectedMarker.bindTooltip(bubbleHtml, {{
                            interactive: true,
                            permanent: true,
                            direction: 'top',
                            offset: [0, -10],
                            className: 'pw-waypoint-bubble'
                        }}).openTooltip();

                        var tt = layers.selectedMarker.getTooltip();
                        if (tt && tt._container) {{
                            L.DomEvent.disableClickPropagation(tt._container);
                            L.DomEvent.disableScrollPropagation(tt._container);
                        }}

                        layers.selectedMarker.on('dragend', function(e) {{
                            var pos = e.target.getLatLng();
                            if (window.pwMapCallbacks && window.pwMapCallbacks.onMoved) {{
                                window.pwMapCallbacks.onMoved({5}, pos.lat, pos.lng);
                            }}
                        }});

                        if ({4}) {{
                            try {{
                                if (!map.getBounds().contains(markerPos)) {{
                                    map.panTo(markerPos);
                                }}
                            }} catch(e) {{}}
                        }}
                    }})();
                ", pt.Latitude, pt.Longitude, pt.Index + 1, $"{pt.DistanceMilesFormatted} · {pt.ElevationFormatted}", panToJs, pt.Index, 0,
                    pt.Index == ViewModel.TrimStartIndex ? "#047857" : "#059669",
                    pt.Index == ViewModel.TrimStartIndex ? "✓ Start" : "Start",
                    pt.Index == ViewModel.TrimEndIndex ? "#B91C1C" : "#DC2626",
                    pt.Index == ViewModel.TrimEndIndex ? "✓ End" : "End");

                Interop.ExecuteJavaScriptVoid(script);
            }
        }

        private void BtnFitBounds_Click(object sender, RoutedEventArgs e)
        {
            Interop.ExecuteJavaScriptVoid(@"
                (function() {
                    var map = window.leafletMapInstance;
                    var layers = window.pwTrackLayers;
                    if (map && layers && layers.activePolyline) {
                        try {
                            var b = layers.activePolyline.getBounds();
                            if (b && b.isValid && b.isValid()) {
                                map.fitBounds(b, { padding: [40, 40] });
                            }
                        } catch(e) {}
                    }
                })();
            ");
        }

        private void BtnTogglePoints_Click(object sender, RoutedEventArgs e)
        {
            _showWaypoints = !_showWaypoints;
            BtnTogglePoints.Content = _showWaypoints ? "Hide Waypoints" : "Show Waypoints";
            RefreshTrackOnMap(fitBounds: false);
        }

        private void MapControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (ViewModel?.SelectedPoint != null)
                {
                    ViewModel.DeleteSelectedPoint();
                    e.Handled = true;
                }
            }
        }
    }
}
