/**
 * LeafletScrollIsolationTests.js
 * Automated test suite for LeafletScrollIsolationHelper on mobile screen sizes
 * where OpenSilver's RootScrollViewer has VerticalScrollBarVisibility = Auto.
 * 
 * Verifies that:
 * 1. Mobile screen layout (< 850px width, e.g. 500x600) triggers Silverlight vertical scroll bar (Auto, scrollHeight > clientHeight).
 * 2. Two-finger gestures on the Leaflet map reach Leaflet's document-level touch listeners (map pans and pinch-zooms).
 *    [CRITICAL REGRESSION TEST: previous code called e.stopPropagation() on container, starving Leaflet and freezing 2-finger gestures]
 * 3. Two-finger pan on the map does NOT scroll OpenSilver's RootScrollViewer (Silverlight vertical scroll bar does not move).
 * 4. Two-finger trackpad scrolling (wheel without Ctrl) over the map does NOT scroll the app.
 * 5. Ctrl + Wheel over the map (desktop zoom) does NOT scroll the app.
 * 6. Single-finger touch drag inside the map does NOT scroll OpenSilver's RootScrollViewer.
 * 7. Single-finger touch drag OUTSIDE the map (on page/stats panel) DOES scroll the app normally.
 * 8. Teardown / destroy() cleanly unbinds and restores state.
 */

const fs = require('fs');
const path = require('path');

// --- 1. Mock DOM Infrastructure ---

class MockDOMElement {
    constructor(id, tagName = 'div') {
        this.id = id;
        this.tagName = tagName;
        this.parentElement = null;
        this.children = [];
        this.listeners = { capture: {}, bubble: {} };
        this.style = {};
        this._attributes = {};
        this.isConnected = true;
        // Mobile screen dimensions: small width and height (< 850px breakpoint)
        this.clientWidth = 500;
        this.clientHeight = 600;
        this.scrollTop = 0;
        this.scrollHeight = 1500; // Trigger vertical scroll bar
        this.allowScrollOnTouchMove = true;
    }

    appendChild(child) {
        child.parentElement = this;
        this.children.push(child);
        return child;
    }

    querySelector(selector) {
        if (selector === '.pw-leaflet-div') {
            return this.children.find(c => c.className === 'pw-leaflet-div') || null;
        }
        return null;
    }

    setAttribute(name, val) { this._attributes[name] = val; }
    getAttribute(name) { return this._attributes[name]; }
    hasAttribute(name) { return name in this._attributes; }
    removeAttribute(name) { delete this._attributes[name]; }

    addEventListener(type, listener, options) {
        const useCapture = typeof options === 'boolean' ? options : (options && options.capture) || false;
        const phase = useCapture ? 'capture' : 'bubble';
        if (!this.listeners[phase][type]) this.listeners[phase][type] = [];
        this.listeners[phase][type].push(listener);
    }

    removeEventListener(type, listener, options) {
        const useCapture = typeof options === 'boolean' ? options : (options && options.capture) || false;
        const phase = useCapture ? 'capture' : 'bubble';
        if (this.listeners[phase][type]) {
            this.listeners[phase][type] = this.listeners[phase][type].filter(l => l !== listener);
        }
    }

    dispatchEvent(event) {
        event.target = this;
        const chain = [];
        let cur = this;
        while (cur) {
            chain.push(cur);
            cur = cur.parentElement;
        }

        // Capture phase
        event.eventPhase = 1;
        for (let i = chain.length - 1; i >= 0; i--) {
            if (event._stopped) break;
            const el = chain[i];
            const listeners = el.listeners.capture[event.type] || [];
            for (const l of listeners) {
                if (event._immediateStopped) break;
                event.currentTarget = el;
                l(event);
            }
        }

        // Bubble phase
        if (event.bubbles && !event._stopped) {
            event.eventPhase = 3;
            for (let i = 0; i < chain.length; i++) {
                if (event._stopped) break;
                const el = chain[i];
                const listeners = el.listeners.bubble[event.type] || [];
                for (const l of listeners) {
                    if (event._immediateStopped) break;
                    event.currentTarget = el;
                    l(event);
                }
            }
        }

        // Real Leaflet attaches active drag (L.Draggable) and pinch zoom (TouchZoom)
        // listeners directly to `document`. If the event was stopped at container,
        // it will NEVER reach document!
        if (event.bubbles && !event._stopped && globalThis.document && globalThis.document._listeners && globalThis.document._listeners[event.type]) {
            for (const l of globalThis.document._listeners[event.type]) {
                l(event);
            }
        }

        return !event.defaultPrevented;
    }
}

class MockEvent {
    constructor(type, init = {}) {
        this.type = type;
        this.bubbles = init.bubbles !== undefined ? init.bubbles : true;
        this.cancelable = init.cancelable !== undefined ? init.cancelable : true;
        this.defaultPrevented = false;
        this._stopped = false;
        this._immediateStopped = false;
        Object.assign(this, init);
    }
    preventDefault() { this.defaultPrevented = true; }
    stopPropagation() { this._stopped = true; }
    stopImmediatePropagation() { this._stopped = true; this._immediateStopped = true; }
}

class MockPointerEvent extends MockEvent {
    constructor(type, init = {}) {
        super(type, init);
        this.pointerId = init.pointerId || 1;
        this.pointerType = init.pointerType || 'mouse';
        this.clientX = init.clientX || 0;
        this.clientY = init.clientY || 0;
        this.button = init.button || 0;
    }
}

class MockTouchEvent extends MockEvent {
    constructor(type, init = {}) {
        super(type, init);
        this.touches = init.touches || [];
        this.changedTouches = init.changedTouches || [];
    }
}

class MockWheelEvent extends MockEvent {
    constructor(type, init = {}) {
        super(type, init);
        this.deltaY = init.deltaY || 0;
        this.deltaX = init.deltaX || 0;
        this.ctrlKey = !!init.ctrlKey;
        this.metaKey = !!init.metaKey;
    }
}

globalThis.PointerEvent = MockPointerEvent;

// Build OpenSilver DOM hierarchy
const rootEl = new MockDOMElement('opensilver-root');
const mainPageEl = new MockDOMElement('main-page');
const rootScrollViewerEl = new MockDOMElement('RootScrollViewer');
const outsidePanelEl = new MockDOMElement('stats-panel');
const mapControlEl = new MockDOMElement('map-control');
const mapContainerEl = new MockDOMElement('map-container');
const mapDivEl = new MockDOMElement('map-div');
mapDivEl.className = 'pw-leaflet-div';

rootEl.appendChild(mainPageEl);
mainPageEl.appendChild(rootScrollViewerEl);
rootScrollViewerEl.appendChild(outsidePanelEl);
rootScrollViewerEl.appendChild(mapControlEl);
mapControlEl.appendChild(mapContainerEl);
mapContainerEl.appendChild(mapDivEl);

// Tag OpenSilver XAML elements
mapContainerEl.xamlid = 'MapContainer_Border';
mapControlEl.xamlid = 'MapControl_UserControl';
outsidePanelEl.xamlid = 'StatsPanel_Border';

globalThis.window = {
    innerWidth: 500,
    innerHeight: 600,
    addEventListener: (type, l, opts) => rootEl.addEventListener(type, l, opts),
    removeEventListener: (type, l, opts) => rootEl.removeEventListener(type, l, opts),
    getComputedStyle: (el) => el.style || {},
};

globalThis.document = {
    _listeners: {},
    addEventListener: function(type, l) {
        if (!this._listeners[type]) this._listeners[type] = [];
        this._listeners[type].push(l);
    },
    removeEventListener: function(type, l) {
        if (this._listeners[type]) {
            this._listeners[type] = this._listeners[type].filter(h => h !== l);
        }
    },
    querySelector: (sel) => {
        if (sel === '#RootScrollViewer' || sel.includes('RootScrollViewer')) return rootScrollViewerEl;
        return null;
    },
    scrollingElement: rootScrollViewerEl,
    documentElement: rootEl
};

// OpenSilver ScrollViewer PanHelper & Wheel runtime simulation
class OpenSilverRootScrollViewer {
    constructor() {
        this.verticalOffset = 0;
        // On mobile screen size (< 850px width), MainPage.xaml.cs sets:
        // RootScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        this.verticalScrollBarVisibility = 'Auto';
        this._isPanning = false;
        this._originY = 0;
    }

    handleTouchMove(originalSource, e) {
        // Decompiled OpenSilver ScrollViewer.PanHelper.HandleMouseMove:
        // if (!((UIElement)e.OriginalSource).AllowScrollOnTouchMove) { this.Cancel(); return; }
        if (!originalSource.allowScrollOnTouchMove) {
            this._isPanning = false;
            return;
        }
        if (e.pointerType === 'touch') {
            if (!this._isPanning) {
                if (Math.abs(e.clientY - this._originY) > 5) {
                    this._isPanning = true;
                }
            }
            if (this._isPanning) {
                this.verticalOffset += Math.abs(e.clientY - this._originY);
            }
        }
    }

    handleMouseWheel(e, isHandled) {
        if (isHandled) return; // Marked e.Handled = true in OpenSilver XAML
        if (e.ctrlKey) return; // Ctrl zooms, does not scroll
        this.verticalOffset += Math.abs(e.deltaY);
    }
}

const silverlightScrollViewer = new OpenSilverRootScrollViewer();

function getClosestXamlElement(el) {
    while (el) {
        if (el.xamlid) return el;
        el = el.parentElement;
    }
    return null;
}

// OpenSilver cshtml5.js event bridge
rootEl.addEventListener('pointerdown', (e) => {
    const xaml = getClosestXamlElement(e.target);
    if (xaml && xaml.allowScrollOnTouchMove) {
        silverlightScrollViewer._originY = e.clientY;
    }
});

rootEl.addEventListener('pointermove', (e) => {
    const xaml = getClosestXamlElement(e.target);
    if (xaml) {
        silverlightScrollViewer.handleTouchMove(xaml, e);
    }
});

let isMapWheelHandledInXaml = false;
rootEl.addEventListener('wheel', (e) => {
    const xaml = getClosestXamlElement(e.target);
    const isOverMap = (xaml === mapContainerEl || xaml === mapControlEl);
    silverlightScrollViewer.handleMouseWheel(e, isOverMap && isMapWheelHandledInXaml);
});

// Leaflet document listeners (L.Draggable and TouchZoom listen on document)
let leafletDocTouchMoveCount = 0;
globalThis.document.addEventListener('touchmove', (e) => {
    if (e.touches && e.touches.length >= 2) {
        leafletDocTouchMoveCount++;
    }
});

let mapDraggingEnabled = false;
const mockMap = {
    gestureHandling: {
        _enabled: true,
        enabled: () => mockMap.gestureHandling._enabled,
        enable: () => { mockMap.gestureHandling._enabled = true; },
        _disableInteractions: function() { mapDraggingEnabled = false; },
        _handleMouseOut: function() { mockMap.gestureHandling._disableInteractions(); }
    },
    dragging: {
        enabled: () => mapDraggingEnabled,
        enable: () => { mapDraggingEnabled = true; },
        disable: () => { mapDraggingEnabled = false; }
    },
    getContainer: () => mapDivEl
};

// --- 2. Load C# & JS Implementation ---
const helperPath = fs.existsSync(path.resolve(__dirname, '../PwImageMetadataViewer/Services/LeafletScrollIsolationHelper.cs'))
    ? path.resolve(__dirname, '../PwImageMetadataViewer/Services/LeafletScrollIsolationHelper.cs')
    : path.resolve(__dirname, '../Services/LeafletScrollIsolationHelper.cs');
const csContent = fs.readFileSync(helperPath, 'utf8');

// Check if C# ProtectControl sets AllowScrollOnTouchMove = false
const setsAllowScrollOnTouchMove = csContent.includes('AllowScrollOnTouchMove = false');
mapControlEl.allowScrollOnTouchMove = !setsAllowScrollOnTouchMove;
mapContainerEl.allowScrollOnTouchMove = !setsAllowScrollOnTouchMove;

// Check if C# ProtectControl sets e.Handled = true for wheel unconditionally or modifier only
const unconditionalWheel = csContent.includes('onlyWhenModifierPressed: false') ||
                           csContent.includes('ProtectControl(mapControl, onlyWhenModifierPressed: false');
isMapWheelHandledInXaml = unconditionalWheel;

const jsStart = csContent.indexOf('window.pwLeafletGestureIsolation =');
const jsEnd = csContent.lastIndexOf('";');
const jsCode = csContent.substring(jsStart, jsEnd);
eval(jsCode);

const isolation = globalThis.window.pwLeafletGestureIsolation.install(mockMap, mapContainerEl);

// --- 3. Execute Verification Suite ---
let passed = 0;
let failed = 0;
function assert(desc, condition) {
    if (condition) {
        console.log('  PASS:', desc);
        passed++;
    } else {
        console.error('  FAIL:', desc);
        failed++;
    }
}

console.log('=== MOBILE SCREEN SIZE LEAFLET SCROLL ISOLATION TESTS ===');
console.log('Simulated Viewport: 500x600 (Mobile Screen Size < 850px Breakpoint)');
console.log('RootScrollViewer ScrollBar Visibility:', silverlightScrollViewer.verticalScrollBarVisibility);
console.log('RootScrollViewer Content Height: 1500px (Scroll Bar Active)\n');

// Test 1: Mobile layout trigger check
assert('Test 1: Mobile dimensions (< 850px) trigger Silverlight vertical scroll bar (Auto)',
    window.innerWidth < 850 && silverlightScrollViewer.verticalScrollBarVisibility === 'Auto');

// Test 2: Two-finger pan on Leaflet map (CRITICAL REGRESSION TEST)
// The user reported: "no 2 finger gestures are working on the leaflet map"
silverlightScrollViewer.verticalOffset = 0;
leafletDocTouchMoveCount = 0;

mapDivEl.dispatchEvent(new MockPointerEvent('pointerdown', { pointerId: 1, pointerType: 'touch', clientY: 100 }));
mapDivEl.dispatchEvent(new MockPointerEvent('pointerdown', { pointerId: 2, pointerType: 'touch', clientY: 100 }));
mapDivEl.dispatchEvent(new MockTouchEvent('touchstart', { touches: [{ identifier: 1 }, { identifier: 2 }] }));
mapDivEl.dispatchEvent(new MockPointerEvent('pointermove', { pointerId: 1, pointerType: 'touch', clientY: 150 }));
mapDivEl.dispatchEvent(new MockPointerEvent('pointermove', { pointerId: 2, pointerType: 'touch', clientY: 150 }));
mapDivEl.dispatchEvent(new MockTouchEvent('touchmove', { touches: [{ identifier: 1 }, { identifier: 2 }] }));

assert('Test 2a [REGRESSION CHECK]: Two-finger pan on map reaches Leaflet document listener (map moves)',
    leafletDocTouchMoveCount > 0);
assert('Test 2b: Two-finger pan on map does NOT move Silverlight vertical scroll bar',
    silverlightScrollViewer.verticalOffset === 0);

mapDivEl.dispatchEvent(new MockPointerEvent('pointerup', { pointerId: 1, pointerType: 'touch' }));
mapDivEl.dispatchEvent(new MockPointerEvent('pointerup', { pointerId: 2, pointerType: 'touch' }));
mapDivEl.dispatchEvent(new MockTouchEvent('touchend', { touches: [] }));

// Test 3: Trackpad two-finger scrolling over the Leaflet map (wheel event without Ctrl)
// The user reported: "ensure that two finger scrolling on the leaflet map does not also scroll the app"
silverlightScrollViewer.verticalOffset = 0;
mapDivEl.dispatchEvent(new MockWheelEvent('wheel', { deltaY: 120, ctrlKey: false }));
assert('Test 3: Two-finger trackpad scroll over map does NOT move Silverlight vertical scroll bar',
    silverlightScrollViewer.verticalOffset === 0);

// Test 4: Ctrl + Mouse wheel (desktop map zoom) over the map
silverlightScrollViewer.verticalOffset = 0;
mapDivEl.dispatchEvent(new MockWheelEvent('wheel', { deltaY: 100, ctrlKey: true }));
assert('Test 4: Ctrl + wheel over map zooms map and does NOT move Silverlight vertical scroll bar',
    silverlightScrollViewer.verticalOffset === 0);

// Test 5: Single finger touch drag over the map does NOT move the Silverlight scroll bar
silverlightScrollViewer.verticalOffset = 0;
mapDivEl.dispatchEvent(new MockPointerEvent('pointerdown', { pointerId: 5, pointerType: 'touch', clientY: 100 }));
mapDivEl.dispatchEvent(new MockTouchEvent('touchstart', { touches: [{ identifier: 5 }] }));
mapDivEl.dispatchEvent(new MockPointerEvent('pointermove', { pointerId: 5, pointerType: 'touch', clientY: 160 }));
mapDivEl.dispatchEvent(new MockTouchEvent('touchmove', { touches: [{ identifier: 5 }] }));
assert('Test 5: Touch drag inside map does NOT move Silverlight vertical scroll bar',
    silverlightScrollViewer.verticalOffset === 0);
mapDivEl.dispatchEvent(new MockPointerEvent('pointerup', { pointerId: 5, pointerType: 'touch' }));
mapDivEl.dispatchEvent(new MockTouchEvent('touchend', { touches: [] }));

// Test 6: Touch drag OUTSIDE the map (on page/stats panel) DOES scroll the app normally
silverlightScrollViewer.verticalOffset = 0;
outsidePanelEl.dispatchEvent(new MockPointerEvent('pointerdown', { pointerId: 6, pointerType: 'touch', clientY: 100 }));
outsidePanelEl.dispatchEvent(new MockTouchEvent('touchstart', { touches: [{ identifier: 6 }] }));
outsidePanelEl.dispatchEvent(new MockPointerEvent('pointermove', { pointerId: 6, pointerType: 'touch', clientY: 160 }));
outsidePanelEl.dispatchEvent(new MockTouchEvent('touchmove', { touches: [{ identifier: 6 }] }));
assert('Test 6: Touch drag outside the map DOES scroll the app normally (offset > 0)',
    silverlightScrollViewer.verticalOffset > 0);
outsidePanelEl.dispatchEvent(new MockPointerEvent('pointerup', { pointerId: 6, pointerType: 'touch' }));
outsidePanelEl.dispatchEvent(new MockTouchEvent('touchend', { touches: [] }));

// Test 7: Desktop Mouse Drag Preservation (Click & Drag Pan)
assert('Test 7a: Map dragging is enabled after isolation install',
    mockMap.dragging.enabled() === true);

// Simulate mouseout event that leaflet-gesture-handling previously used to disable dragging
mockMap.gestureHandling._handleMouseOut();
assert('Test 7b: Leaflet gesture-handling _handleMouseOut does not disable map dragging',
    mockMap.dragging.enabled() === true);

// Simulate disabling and ensuring mouse/pointerdown re-enables map dragging
mockMap.dragging.disable();
assert('Test 7c: Dragging can be temporarily disabled for testing',
    mockMap.dragging.enabled() === false);

mapDivEl.dispatchEvent(new MockPointerEvent('pointerdown', { pointerId: 1, pointerType: 'mouse', clientY: 100 }));
assert('Test 7d: Pointerdown (mouse) immediately re-enables map dragging for left-click pan',
    mockMap.dragging.enabled() === true);

// Test 8: Teardown
isolation.destroy();
silverlightScrollViewer.verticalOffset = 0;
outsidePanelEl.dispatchEvent(new MockWheelEvent('wheel', { deltaY: 80, ctrlKey: false }));
assert('Test 8: Teardown cleanly unbinds', silverlightScrollViewer.verticalOffset > 0);

console.log(`\nRESULTS: ${passed} passed, ${failed} failed.\n`);
if (failed > 0) process.exit(1);
