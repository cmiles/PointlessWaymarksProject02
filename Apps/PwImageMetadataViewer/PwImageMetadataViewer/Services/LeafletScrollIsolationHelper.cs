using System.Windows;
using System.Windows.Input;

namespace PwImageMetadataViewer.Services
{
    /// <summary>
    /// Provides integration and scroll isolation for Leaflet maps using the 'leaflet-gesture-handling'
    /// module within scrollable OpenSilver / Photino (WebView2) / WebAssembly host containers.
    ///
    /// Key capabilities:
    /// 1. Gesture Handling Integration: Works seamlessly with 'leaflet-gesture-handling' for two-finger
    ///    mobile map panning/pinch-zooming and Ctrl+wheel desktop zooming, displaying localized guidance badges.
    /// 2. OpenSilver Touch Panning Isolation: Sets AllowScrollOnTouchMove = false on the map control and child
    ///    containers so OpenSilver's PanHelper does not move the host ScrollViewer (Silverlight vertical scroll bar)
    ///    during map interactions.
    /// 3. OpenSilver Routed Wheel Event Protection: Intercepts OpenSilver routed MouseWheel events over the map
    ///    so two-finger trackpad scrolling and mouse wheel events do not scroll the parent ScrollViewer.
    /// 4. Leaflet Event Bubble Preservation: Preserves DOM touchstart and touchmove bubbling to 'document' so
    ///    Leaflet's L.Draggable and TouchZoom document listeners receive all multi-touch gestures cleanly.
    /// 5. Host Viewport Lock: During active multi-touch gestures, temporarily locks the ancestor DOM scroll
    ///    container to prevent any viewport jitter or browser-native scroll chaining on small/mobile screen sizes.
    /// </summary>
    public static class LeafletScrollIsolationHelper
    {
        /// <summary>
        /// Configures OpenSilver routed MouseWheel and touch pan isolation on the map control and optional child containers.
        /// By default, disables touch panning (AllowScrollOnTouchMove = false) and intercepts all wheel events over the map
        /// so two-finger gestures and trackpad scrolling on the map never scroll the Silverlight vertical scroll bar.
        /// </summary>
        /// <param name="mapControl">The primary MapControl or container hosting the Leaflet map.</param>
        /// <param name="childContainers">Optional child elements inside the map control to protect.</param>
        public static void ProtectControl(FrameworkElement mapControl, params FrameworkElement[] childContainers)
        {
            ProtectControl(mapControl, onlyWhenModifierPressed: false, childContainers);
        }

        /// <summary>
        /// Configures OpenSilver routed MouseWheel and touch pan isolation on the map control and optional child containers.
        /// </summary>
        /// <param name="mapControl">The primary MapControl or container hosting the Leaflet map.</param>
        /// <param name="onlyWhenModifierPressed">
        /// When true, only marks e.Handled = true when Ctrl or Cmd is pressed.
        /// When false (default / recommended), unconditionally marks e.Handled = true for all mouse wheel events over the control,
        /// ensuring trackpad two-finger scrolling over the map never scrolls the host ScrollViewer.
        /// </param>
        /// <param name="childContainers">Optional child elements inside the map control to protect.</param>
        public static void ProtectControl(FrameworkElement mapControl, bool onlyWhenModifierPressed, params FrameworkElement[] childContainers)
        {
            if (mapControl != null)
            {
                mapControl.AllowScrollOnTouchMove = false;
                AttachWheelHandler(mapControl, onlyWhenModifierPressed);
            }

            if (childContainers != null)
            {
                foreach (var container in childContainers)
                {
                    if (container != null)
                    {
                        container.AllowScrollOnTouchMove = false;
                        AttachWheelHandler(container, onlyWhenModifierPressed);
                    }
                }
            }
        }

        private static void AttachWheelHandler(FrameworkElement element, bool onlyWhenModifierPressed)
        {
            if (element == null) return;

            element.MouseWheel += (sender, e) =>
            {
                if (!onlyWhenModifierPressed)
                {
                    e.Handled = true;
                    return;
                }

                // In modifier-only mode, Ctrl+Wheel or Cmd+Wheel zooms the map.
                var modifiers = Keyboard.Modifiers;
                bool isModifierPressed = (modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
                                         (modifiers & ModifierKeys.Windows) == ModifierKeys.Windows;

                if (isModifierPressed)
                {
                    e.Handled = true;
                }
            };
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

                        // 1. Ensure leaflet-gesture-handling is enabled on the map instance
                        if (map.gestureHandling) {
                            if (!map.gestureHandling.enabled()) {
                                map.gestureHandling.enable();
                            }
                            // Prevent leaflet-gesture-handling from disabling mouse dragging on mouseout.
                            // On desktop, mouse dragging must always be enabled so users can pan the map with left-click drag.
                            // Touch devices are already cleanly handled by gesture-handling's _handleTouch (requiring two fingers).
                            map.gestureHandling._handleMouseOut = function() {
                                // Do not disable dragging on mouseout - prevents premature drag disabling on desktop
                            };
                            var origRemoveHooks = map.gestureHandling.removeHooks;
                            map.gestureHandling.removeHooks = function() {
                                this._enableInteractions = function() {};
                                if (origRemoveHooks) origRemoveHooks.call(this);
                            };
                        }

                        // Ensure map dragging is enabled for mouse interactions
                        if (map.dragging && !map.dragging.enabled()) {
                            map.dragging.enable();
                        }

                        var mapEl = (map.getContainer && map.getContainer()) || container;

                        // Ensure mouse drag starts with dragging enabled on mousedown/pointerdown
                        function onPointerDown(e) {
                            if (!map || !map.dragging) return;
                            var c = (map.getContainer && map.getContainer()) || map._container;
                            if (!c) return;
                            if (!e.touches && (e.pointerType === 'mouse' || !e.pointerType)) {
                                if (!map.dragging.enabled()) {
                                    map.dragging.enable();
                                }
                            }
                        }
                        mapEl.addEventListener('pointerdown', onPointerDown, { capture: true });
                        mapEl.addEventListener('mousedown', onPointerDown, { capture: true });
                        if (container !== mapEl) {
                            container.addEventListener('pointerdown', onPointerDown, { capture: true });
                            container.addEventListener('mousedown', onPointerDown, { capture: true });
                        }

                        // 2. Discover parent scroll container in OpenSilver
                        var cachedScrollParent = null;
                        function getScrollParent() {
                            if (cachedScrollParent && cachedScrollParent.isConnected) return cachedScrollParent;
                            if (options && options.scrollParent && options.scrollParent.isConnected) {
                                cachedScrollParent = options.scrollParent;
                                return cachedScrollParent;
                            }
                            if (window.pwMainScrollViewerElement && window.pwMainScrollViewerElement.isConnected) {
                                cachedScrollParent = window.pwMainScrollViewerElement;
                                return cachedScrollParent;
                            }
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
                            cachedScrollParent = (options && options.scrollParent) ||
                                                 window.pwMainScrollViewerElement ||
                                                 document.querySelector('#RootScrollViewer') ||
                                                 document.querySelector('[id*=""RootScrollViewer""]') ||
                                                 document.querySelector('#MainScrollViewer') ||
                                                 document.querySelector('[id*=""MainScrollViewer""]') ||
                                                 document.querySelector('.opensilver-scrollviewer') ||
                                                 document.scrollingElement || document.documentElement;
                            return cachedScrollParent;
                        }

                        // 3. Wheel Isolation Guard:
                        // Stops wheel events over the map from propagating to parent DOM scroll containers or OpenSilver root,
                        // preventing trackpad two-finger scrolling or mouse wheel from moving the Silverlight vertical scroll bar.
                        function onWheel(e) {
                            e.stopPropagation();
                        }

                        // 4. Multi-Touch Isolation Guard:
                        // Real Leaflet attaches active drag and pinch-zoom handlers to 'document' (L.Draggable and TouchZoom).
                        // Therefore, we MUST NOT call e.stopPropagation() on touchstart or touchmove on container,
                        // as doing so starves Leaflet of document touch events and completely freezes 2-finger gestures.
                        // OpenSilver touch panning is cleanly prevented in C# via AllowScrollOnTouchMove = false.
                        // As an additional guard against native browser scroll chaining on small/mobile viewports,
                        // we lock the DOM scroll parent during active multi-touch gestures.
                        var isTwoFingerActive = false;
                        var lockedScrollTop = null;

                        function startTwoFingerLock() {
                            if (!isTwoFingerActive) {
                                isTwoFingerActive = true;
                            }
                            var sp = getScrollParent();
                            if (sp) {
                                lockedScrollTop = sp.scrollTop;
                                if (!sp.hasAttribute('data-pw-prev-overflow')) {
                                    sp.setAttribute('data-pw-prev-overflow', sp.style.overflowY || 'auto');
                                }
                                sp.style.overflowY = 'hidden';
                            }
                        }

                        function stopTwoFingerLock() {
                            if (!isTwoFingerActive) return;
                            isTwoFingerActive = false;
                            lockedScrollTop = null;
                            var sp = getScrollParent();
                            if (sp && sp.hasAttribute('data-pw-prev-overflow')) {
                                sp.style.overflowY = sp.getAttribute('data-pw-prev-overflow');
                                sp.removeAttribute('data-pw-prev-overflow');
                            }
                        }

                        function onTouchStart(e) {
                            if (!e.touches) return;
                            if (e.touches.length >= 2) {
                                startTwoFingerLock();
                            } else {
                                stopTwoFingerLock();
                            }
                            // Important: Do NOT call e.stopPropagation() so events bubble to document for Leaflet.
                        }

                        function onTouchMove(e) {
                            if (!e.touches) return;
                            if (e.touches.length >= 2 || isTwoFingerActive) {
                                startTwoFingerLock();
                                var sp = getScrollParent();
                                if (sp && lockedScrollTop !== null && Math.abs(sp.scrollTop - lockedScrollTop) > 0.5) {
                                    sp.scrollTop = lockedScrollTop;
                                }
                            }
                            // Important: Do NOT call e.stopPropagation() so events bubble to document for Leaflet.
                        }

                        function onTouchEnd(e) {
                            if (!e.touches || e.touches.length < 2) {
                                stopTwoFingerLock();
                            }
                        }

                        container.addEventListener('wheel', onWheel, { passive: false });
                        container.addEventListener('touchstart', onTouchStart, { passive: true });
                        container.addEventListener('touchmove', onTouchMove, { passive: true });
                        container.addEventListener('touchend', onTouchEnd, { passive: true });
                        container.addEventListener('touchcancel', onTouchEnd, { passive: true });

                        window.addEventListener('touchend', onTouchEnd, { passive: true });
                        window.addEventListener('touchcancel', onTouchEnd, { passive: true });

                        // 5. Teardown / Cleanup Handle
                        return {
                            destroy: function() {
                                mapEl.removeEventListener('pointerdown', onPointerDown, { capture: true });
                                mapEl.removeEventListener('mousedown', onPointerDown, { capture: true });
                                if (container !== mapEl) {
                                    container.removeEventListener('pointerdown', onPointerDown, { capture: true });
                                    container.removeEventListener('mousedown', onPointerDown, { capture: true });
                                }
                                container.removeEventListener('wheel', onWheel);
                                container.removeEventListener('touchstart', onTouchStart);
                                container.removeEventListener('touchmove', onTouchMove);
                                container.removeEventListener('touchend', onTouchEnd);
                                container.removeEventListener('touchcancel', onTouchEnd);
                                window.removeEventListener('touchend', onTouchEnd);
                                window.removeEventListener('touchcancel', onTouchEnd);
                                stopTwoFingerLock();
                            }
                        };
                    }
                };
            ";
        }
    }
}
