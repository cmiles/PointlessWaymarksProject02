using System.Windows;

namespace PwTrackTrimmer.Services
{
    /// <summary>
    /// Provides reusable scroll isolation and two-finger gesture support (including continuous 60fps
    /// pinch-to-zoom and two-finger pan) for Leaflet maps embedded inside scrollable OpenSilver /
    /// Photino (WebView2) / WebAssembly host containers.
    ///
    /// Key capabilities:
    /// 1. Mobile & Windows Desktop Touchscreen Two-Finger Gestures: Unified multi-touch engine that
    ///    simultaneously pans and continuously pinch-zooms the Leaflet map around the gesture midpoint
    ///    at 60fps with zero latency, while locking the host app's vertical scrollbar.
    /// 2. Mobile / Touchscreen Single-Finger Scroll: Single-finger swipe scrolls the host page vertically,
    ///    displaying a sleek guidance badge ("✌️ Use two fingers to move the map").
    /// 3. Desktop / Trackpad / Mouse Wheel: Trackpad two-finger scroll and mouse wheel over the map zoom
    ///    the map with smoothed delta accumulation without scrolling the parent ScrollViewer.
    /// 4. Cross-Platform Input Support: Fully supports both mobile TouchEvents and Windows WebView2
    ///    PointerEvents (crucial for Photino on Windows touchscreens).
    /// </summary>
    public static class LeafletScrollIsolationHelper
    {
        /// <summary>
        /// Prevents OpenSilver's routed MouseWheel event from bubbling up the XAML visual tree
        /// to any ancestor ScrollViewer. Call this in your MapControl constructor.
        /// </summary>
        public static void ProtectControl(FrameworkElement mapControl, params FrameworkElement[] childContainers)
        {
            if (mapControl != null)
            {
                mapControl.MouseWheel += (sender, e) => { e.Handled = true; };
            }

            if (childContainers != null)
            {
                foreach (var container in childContainers)
                {
                    if (container != null)
                    {
                        container.MouseWheel += (sender, e) => { e.Handled = true; };
                    }
                }
            }
        }

        /// <summary>
        /// Returns the JavaScript source code defining 'window.pwLeafletGestureIsolation.install(map, container, options)'.
        /// Include this in your map initialization script.
        /// </summary>
        public static string GetJavaScriptDefinition()
        {
            return @"
                window.pwLeafletGestureIsolation = window.pwLeafletGestureIsolation || {
                    install: function(map, container, options) {
                        if (!map || !container) return null;
                        options = options || {};
                        var badgeText = options.message || 'Use two fingers to move the map';
                        var badgeIcon = options.icon || '✌️';
                        var wheelThreshold = options.wheelThreshold || 65;

                        // Enable smooth fractional zooming on map
                        if (map.options && (map.options.zoomSnap === undefined || map.options.zoomSnap > 0.25)) {
                            map.options.zoomSnap = 0;
                        }

                        // 1. Inject Styles
                        var styleId = 'pw-gesture-isolation-styles';
                        if (!document.getElementById(styleId)) {
                            var st = document.createElement('style');
                            st.id = styleId;
                            st.textContent = 
                                '.pw-gesture-overlay { position: absolute; inset: 0; background: rgba(15, 23, 42, 0.65); ' +
                                '-webkit-backdrop-filter: blur(2px); backdrop-filter: blur(2px); display: flex; ' +
                                'align-items: center; justify-content: center; z-index: 999; opacity: 0; ' +
                                'pointer-events: none; transition: opacity 0.25s ease; } ' +
                                '.pw-gesture-overlay.active { opacity: 1; } ' +
                                '.pw-gesture-message { background: #1E293B; border: 1.5px solid #38BDF8; color: #F8FAFC; ' +
                                'border-radius: 8px; padding: 10px 18px; font-size: 13px; font-weight: 600; display: flex; ' +
                                'align-items: center; gap: 8px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.45); transform: scale(0.95); ' +
                                'transition: transform 0.2s ease; user-select: none; } ' +
                                '.pw-gesture-overlay.active .pw-gesture-message { transform: scale(1); }';
                            document.head.appendChild(st);
                        }

                        // 2. Inject Overlay DOM
                        var overlay = container.querySelector('.pw-gesture-overlay');
                        if (!overlay) {
                            overlay = document.createElement('div');
                            overlay.className = 'pw-gesture-overlay';
                            overlay.innerHTML = '<div class=""pw-gesture-message""><span>' + badgeIcon + '</span><span>' + badgeText + '</span></div>';
                            container.appendChild(overlay);
                        }

                        var overlayTimeout = null;
                        function showOverlay() {
                            if (!overlay) return;
                            overlay.classList.add('active');
                            if (overlayTimeout) clearTimeout(overlayTimeout);
                            overlayTimeout = setTimeout(function() { overlay.classList.remove('active'); }, 1500);
                        }
                        function hideOverlay() {
                            if (overlayTimeout) clearTimeout(overlayTimeout);
                            if (overlay) overlay.classList.remove('active');
                        }

                        // 3. Scroll Parent Discovery & Locking
                        var cachedScrollParent = null;
                        function getScrollParent() {
                            if (cachedScrollParent && cachedScrollParent.isConnected) return cachedScrollParent;
                            var p = container.parentElement;
                            while (p && p !== document.body && p !== document.documentElement) {
                                var s = window.getComputedStyle(p);
                                var oy = s.overflowY || s.overflow;
                                if ((oy === 'auto' || oy === 'scroll' || (oy === 'hidden' && p.hasAttribute('data-pw-prev-overflow'))) &&
                                    (p.scrollHeight > p.clientHeight || p.hasAttribute('data-pw-prev-overflow'))) {
                                    cachedScrollParent = p;
                                    return p;
                                }
                                p = p.parentElement;
                            }
                            cachedScrollParent = document.querySelector('#RootScrollViewer') ||
                                                 document.querySelector('[id*=""RootScrollViewer""]') ||
                                                 document.querySelector('.opensilver-scrollviewer') ||
                                                 document.scrollingElement || document.documentElement;
                            return cachedScrollParent;
                        }

                        var isTwoFingerActive = false;
                        var lockedScrollTop = null;
                        var lastPanMidpoint = null;
                        var gestureStartDist = null;
                        var gestureStartZoom = null;
                        var lastMoveTime = 0;

                        function startTwoFinger() {
                            if (isTwoFingerActive) return;
                            isTwoFingerActive = true;
                            hideOverlay();

                            var sp = getScrollParent();
                            if (sp) {
                                lockedScrollTop = sp.scrollTop;
                                if (!sp.hasAttribute('data-pw-prev-overflow')) {
                                    sp.setAttribute('data-pw-prev-overflow', sp.style.overflowY || 'auto');
                                }
                                sp.style.overflowY = 'hidden';

                                if (!sp._pwScrollLockAttached) {
                                    sp._pwScrollLockAttached = true;
                                    sp.addEventListener('scroll', function() {
                                        if (isTwoFingerActive && lockedScrollTop !== null && Math.abs(sp.scrollTop - lockedScrollTop) > 0.5) {
                                            sp.scrollTop = lockedScrollTop;
                                        }
                                    }, { passive: false });
                                }
                            }
                            container.style.touchAction = 'none';
                        }

                        function stopTwoFinger() {
                            if (!isTwoFingerActive) return;
                            isTwoFingerActive = false;
                            lockedScrollTop = null;
                            lastPanMidpoint = null;
                            gestureStartDist = null;
                            gestureStartZoom = null;

                            var sp = getScrollParent();
                            if (sp && sp.hasAttribute('data-pw-prev-overflow')) {
                                sp.style.overflowY = sp.getAttribute('data-pw-prev-overflow');
                                sp.removeAttribute('data-pw-prev-overflow');
                            }
                            container.style.touchAction = '';

                            if (map.dragging && !map.dragging.enabled()) {
                                map.dragging.enable();
                            }
                        }

                        // 4. Unified Pan & Smooth Continuous Pinch-Zoom
                        function handleTwoFingerMove(p1, p2) {
                            var now = (window.performance && window.performance.now) ? window.performance.now() : Date.now();
                            if (now - lastMoveTime < 6) return; // Coalesce redundant events within 6ms
                            lastMoveTime = now;

                            var mid = {
                                x: (p1.clientX + p2.clientX) / 2,
                                y: (p1.clientY + p2.clientY) / 2
                            };
                            var dist = Math.hypot(p2.clientX - p1.clientX, p2.clientY - p1.clientY);

                            // A. Pan by midpoint motion
                            if (lastPanMidpoint) {
                                var dx = mid.x - lastPanMidpoint.x;
                                var dy = mid.y - lastPanMidpoint.y;
                                if (dx !== 0 || dy !== 0) {
                                    map.panBy([-dx, -dy], { animate: false, noMoveStart: true });
                                }
                            }
                            lastPanMidpoint = mid;

                            // B. Continuous Fractional Zoom (Logarithmic 1:1 scale)
                            if (gestureStartDist !== null && gestureStartDist > 10 && dist > 10) {
                                var scale = dist / gestureStartDist;
                                var targetZoom = gestureStartZoom + Math.log2(scale);
                                var minZ = map.getMinZoom();
                                var maxZ = map.getMaxZoom();

                                if (targetZoom >= minZ && targetZoom <= maxZ) {
                                    try {
                                        var containerRect = container.getBoundingClientRect();
                                        var containerPt = L.point(mid.x - containerRect.left, mid.y - containerRect.top);
                                        var midLatLng = map.containerPointToLatLng(containerPt);
                                        map.setZoomAround(midLatLng, targetZoom, { animate: false });
                                    } catch (err) {
                                        // Ignore edge projection boundaries
                                    }
                                }
                            }

                            // Keep scroll parent firmly clamped
                            var sp = getScrollParent();
                            if (sp && lockedScrollTop !== null && Math.abs(sp.scrollTop - lockedScrollTop) > 0.5) {
                                sp.scrollTop = lockedScrollTop;
                            }
                        }

                        function isOverMap(e) {
                            if (!container) return false;
                            if (container === e.target || container.contains(e.target)) return true;
                            if (e.clientX !== undefined && e.clientY !== undefined) {
                                var r = container.getBoundingClientRect();
                                return (e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom);
                            }
                            return false;
                        }

                        // 5. Touch Events Stream (Mobile WebAssembly / iOS / Android)
                        var touchStartPos = null;

                        function onTouchStart(e) {
                            if (!e.touches) return;
                            if (e.touches.length >= 2) {
                                startTwoFinger();
                                var p1 = e.touches[0];
                                var p2 = e.touches[1];
                                lastPanMidpoint = { x: (p1.clientX + p2.clientX) / 2, y: (p1.clientY + p2.clientY) / 2 };
                                gestureStartDist = Math.hypot(p2.clientX - p1.clientX, p2.clientY - p1.clientY);
                                gestureStartZoom = map.getZoom();
                                e.preventDefault(); e.stopPropagation(); e.stopImmediatePropagation();
                            } else if (e.touches.length === 1) {
                                stopTwoFinger();
                                touchStartPos = { x: e.touches[0].clientX, y: e.touches[0].clientY };
                                if (map.dragging && map.dragging.enabled()) {
                                    map.dragging.disable();
                                }
                            }
                        }

                        function onTouchMove(e) {
                            if (!e.touches) return;
                            if (e.touches.length >= 2) {
                                startTwoFinger();
                                handleTwoFingerMove(e.touches[0], e.touches[1]);
                                e.preventDefault(); e.stopPropagation(); e.stopImmediatePropagation();
                            } else if (e.touches.length === 1 && touchStartPos) {
                                var dx = Math.abs(e.touches[0].clientX - touchStartPos.x);
                                var dy = Math.abs(e.touches[0].clientY - touchStartPos.y);
                                if (dx > 10 || dy > 10) showOverlay();
                            }
                        }

                        function onTouchEnd(e) {
                            if (!e.touches || e.touches.length < 2) {
                                stopTwoFinger();
                                touchStartPos = (e.touches && e.touches.length === 1) ? { x: e.touches[0].clientX, y: e.touches[0].clientY } : null;
                            } else if (e.touches.length >= 2) {
                                var p1 = e.touches[0];
                                var p2 = e.touches[1];
                                lastPanMidpoint = { x: (p1.clientX + p2.clientX) / 2, y: (p1.clientY + p2.clientY) / 2 };
                                gestureStartDist = Math.hypot(p2.clientX - p1.clientX, p2.clientY - p1.clientY);
                                gestureStartZoom = map.getZoom();
                            }
                        }

                        // 6. Pointer Events Stream (Windows Photino / WebView2 Desktop Touchscreens)
                        var activePointers = new Map();

                        function onPointerDown(e) {
                            if (e.pointerType === 'touch' && isOverMap(e)) {
                                activePointers.set(e.pointerId, { clientX: e.clientX, clientY: e.clientY });
                                if (activePointers.size >= 2) {
                                    startTwoFinger();
                                    var pts = Array.from(activePointers.values());
                                    lastPanMidpoint = { x: (pts[0].clientX + pts[1].clientX) / 2, y: (pts[0].clientY + pts[1].clientY) / 2 };
                                    gestureStartDist = Math.hypot(pts[1].clientX - pts[0].clientX, pts[1].clientY - pts[0].clientY);
                                    gestureStartZoom = map.getZoom();
                                    e.preventDefault(); e.stopPropagation(); e.stopImmediatePropagation();
                                }
                            }
                        }

                        function onPointerMove(e) {
                            if (e.pointerType === 'touch' && activePointers.has(e.pointerId)) {
                                activePointers.set(e.pointerId, { clientX: e.clientX, clientY: e.clientY });
                                if (activePointers.size >= 2) {
                                    startTwoFinger();
                                    var pts = Array.from(activePointers.values());
                                    handleTwoFingerMove(pts[0], pts[1]);
                                    e.preventDefault(); e.stopPropagation(); e.stopImmediatePropagation();
                                }
                            }
                        }

                        function onPointerUp(e) {
                            if (e.pointerType === 'touch') {
                                activePointers.delete(e.pointerId);
                                if (activePointers.size < 2) stopTwoFinger();
                            }
                        }

                        // 7. Wheel Handler (Mouse Wheel & Trackpad Two-Finger Zoom with Delta Smoothing)
                        var accumulatedWheelDelta = 0;
                        function onWheel(e) {
                            if (isOverMap(e)) {
                                e.preventDefault(); e.stopPropagation(); e.stopImmediatePropagation();
                                var sp = getScrollParent();
                                if (sp && lockedScrollTop !== null && Math.abs(sp.scrollTop - lockedScrollTop) > 0.5) {
                                    sp.scrollTop = lockedScrollTop;
                                }
                                if (map) {
                                    accumulatedWheelDelta += e.deltaY;
                                    if (accumulatedWheelDelta <= -wheelThreshold) {
                                        map.zoomIn(1);
                                        accumulatedWheelDelta = 0;
                                    } else if (accumulatedWheelDelta >= wheelThreshold) {
                                        map.zoomOut(1);
                                        accumulatedWheelDelta = 0;
                                    }
                                }
                            }
                        }

                        // 8. Event Listener Attachment
                        var opts = { capture: true, passive: false };

                        container.addEventListener('touchstart', onTouchStart, opts);
                        container.addEventListener('touchmove', onTouchMove, opts);
                        container.addEventListener('touchend', onTouchEnd, opts);
                        container.addEventListener('touchcancel', onTouchEnd, opts);
                        container.addEventListener('wheel', onWheel, opts);

                        container.addEventListener('pointerdown', onPointerDown, opts);
                        container.addEventListener('pointermove', onPointerMove, opts);
                        container.addEventListener('pointerup', onPointerUp, opts);
                        container.addEventListener('pointercancel', onPointerUp, opts);

                        window.addEventListener('pointerdown', onPointerDown, opts);
                        window.addEventListener('pointermove', onPointerMove, opts);
                        window.addEventListener('pointerup', onPointerUp, opts);
                        window.addEventListener('pointercancel', onPointerUp, opts);
                        window.addEventListener('wheel', onWheel, opts);

                        // 9. Teardown / Cleanup Handle
                        return {
                            destroy: function() {
                                container.removeEventListener('touchstart', onTouchStart, opts);
                                container.removeEventListener('touchmove', onTouchMove, opts);
                                container.removeEventListener('touchend', onTouchEnd, opts);
                                container.removeEventListener('touchcancel', onTouchEnd, opts);
                                container.removeEventListener('wheel', onWheel, opts);

                                container.removeEventListener('pointerdown', onPointerDown, opts);
                                container.removeEventListener('pointermove', onPointerMove, opts);
                                container.removeEventListener('pointerup', onPointerUp, opts);
                                container.removeEventListener('pointercancel', onPointerUp, opts);

                                window.removeEventListener('pointerdown', onPointerDown, opts);
                                window.removeEventListener('pointermove', onPointerMove, opts);
                                window.removeEventListener('pointerup', onPointerUp, opts);
                                window.removeEventListener('pointercancel', onPointerUp, opts);
                                window.removeEventListener('wheel', onWheel, opts);

                                if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
                            }
                        };
                    }
                };
            ";
        }
    }
}
