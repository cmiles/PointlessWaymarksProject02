# Leaflet Scroll Isolation & Gesture Handling in OpenSilver

This guide documents everything required to successfully host and interact with a **Leaflet** map inside an **OpenSilver** application (WebAssembly or Photino Desktop) that features scrollable containers (e.g., `ScrollViewer`, page scrollbars), especially on **mobile and responsive layouts** where the Silverlight vertical scroll bar is active.

---

## 1. The Challenge: Maps Inside Scrollable OpenSilver Apps

When an interactive Leaflet map is embedded in a scrollable OpenSilver application, several input collisions occur out of the box:

1. **Scroll Entrapment (Desktop)**: When a user scrolls down a long page using their mouse wheel, the cursor enters the map area and the map zooms in/out instead of scrolling the host page.
2. **Touch Entrapment (Mobile & Touchscreens)**: When a user swipes to scroll through the page on mobile or tablet, swiping over the map pans the map instead of scrolling the page.
3. **Double Scrolling Conflict**: When zooming with `Ctrl + Wheel` or two-finger trackpad gestures, OpenSilver's routed `MouseWheel` event can bubble up to ancestor OpenSilver `ScrollViewer` controls, causing the parent page to scroll *at the same time* the map is zooming or being scrolled.
4. **Touchscreen Pan & Zoom Collision**: When a user uses two fingers to pan or pinch-zoom the map, OpenSilver's built-in `ScrollViewer.PanHelper` can treat the touch gesture as a drag on the `ScrollViewer`, moving the Silverlight vertical scroll bar while the user attempts to interact with the map.

### The Solution Architecture

To eliminate these conflicts cleanly without breaking Leaflet's multi-touch engine, the setup combines three cooperative layers:

- **`leaflet-gesture-handling`**: The standard Leaflet plugin providing Google Maps-style gesture guidance:
  - Requires **two fingers** to pan/zoom on touchscreens (shows a localized `"Use two fingers to move the map"` badge on single-finger drag while allowing the page to scroll).
  - Requires **Ctrl + Scroll** (or **⌘ + Scroll** on macOS) to zoom on desktop (shows a localized `"Use ctrl + scroll to zoom the map"` badge on unmodified wheel while allowing the page to scroll).
  - Ships with built-in translations for 50+ languages.
- **`L.Control.Locate`**: The standard Leaflet geolocation control providing a self-contained GPS/location button and heading orientation display.
- **`LeafletScrollIsolationHelper`**: A C# and JavaScript helper bridging OpenSilver's routed event system with DOM scroll parent isolation:
  - **OpenSilver Touch Panning Isolation (`AllowScrollOnTouchMove = false`)**: Sets `AllowScrollOnTouchMove = false` in C# on `MapControl` and `MapContainer`. OpenSilver's `ScrollViewer.PanHelper` checks `((UIElement)e.OriginalSource).AllowScrollOnTouchMove`; when false, `PanHelper` immediately cancels without moving the Silverlight vertical scroll bar.
  - **OpenSilver Routed Wheel Protection**: Intercepts OpenSilver routed `MouseWheel` events over the map and marks `e.Handled = true`. This prevents two-finger trackpad scrolling and mouse wheel events over the map from scrolling ancestor `ScrollViewer` controls (such as `RootScrollViewer`).
  - **Preserving Leaflet DOM Event Bubbling**: Real Leaflet attaches active drag (`L.Draggable`) and pinch-zoom (`TouchZoom`) handlers directly to `document`. `LeafletScrollIsolationHelper` preserves DOM `touchstart` and `touchmove` bubbling to `document`, ensuring that all two-finger map gestures fire and execute smoothly without freezing.
  - **Host Viewport Lock**: During active multi-touch gestures, temporarily locks the ancestor DOM scroll container's `overflow` and scroll position as an additional guard against native browser scroll chaining on mobile viewports.

---

## 2. Directory Structure & Required Libraries

Store the Leaflet distribution and plugin modules under `wwwroot/applibs` in your OpenSilver host projects (`PwTrackTrimmer.Browser/wwwroot` and `PwTrackTrimmer.Photino/wwwroot`):

```text
wwwroot/
├── applibs/
│   ├── leaflet/
│   │   ├── leaflet.css
│   │   ├── leaflet.js
│   │   └── images/
│   │       ├── marker-icon.png
│   │       ├── marker-icon-2x.png
│   │       └── marker-shadow.png
│   ├── leaflet-gesture-handling.min.css
│   ├── leaflet-gesture-handling.min.js
│   ├── leaflet-gesture-handling.min.js.map
│   ├── L.Control.Locate.min.css
│   ├── L.Control.Locate.min.js
│   └── L.Control.Locate.min.js.map
└── index.html
```

### Project File Configuration (`.csproj`)

Ensure your OpenSilver host projects bundle the `wwwroot` folder:

```xml
<ItemGroup>
    <Content Include="wwwroot\**">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

---

## 3. Host HTML Setup (`index.html`)

Include the stylesheets and scripts in `index.html`. **Order matters**: `leaflet.js` must be loaded *before* `leaflet-gesture-handling.min.js` and `L.Control.Locate.min.js`.

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <title>OpenSilver Map Application</title>

    <!-- 1. Leaflet Core CSS -->
    <link rel="stylesheet" href="applibs/leaflet/leaflet.css" />

    <!-- 2. Leaflet Gesture Handling CSS -->
    <link rel="stylesheet" href="applibs/leaflet-gesture-handling.min.css" />

    <!-- 3. Leaflet Locate Control CSS -->
    <link rel="stylesheet" href="applibs/L.Control.Locate.min.css" />

    <!-- 4. Leaflet Core Script -->
    <script src="applibs/leaflet/leaflet.js"></script>

    <!-- 5. Leaflet Gesture Handling Script (Must be after leaflet.js) -->
    <script src="applibs/leaflet-gesture-handling.min.js"></script>

    <!-- 6. Leaflet Locate Control Script (Must be after leaflet.js) -->
    <script src="applibs/L.Control.Locate.min.js"></script>
</head>
<body>
    <div id="opensilver-root" style="position: relative; width: 100%; height: 100%; overflow: hidden;"></div>
</body>
</html>
```

---

## 4. XAML Control Setup

Create an OpenSilver `UserControl` hosting a `Border` or `Grid` for the map:

```xml
<UserControl x:Class="MyNamespace.MapControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Background="#0B0F19">
        <!-- Container where the Leaflet map DIV is mounted -->
        <Border x:Name="MapContainer" Background="#0F172A" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" />
    </Grid>
</UserControl>
```

---

## 5. C# Code-Behind Implementation

### Step 5.1: Wire Event Protection

In the `MapControl` constructor, call `LeafletScrollIsolationHelper.ProtectControl(this, MapContainer)`:

```csharp
using System.Windows.Controls;
using PwTrackTrimmer.Services;

namespace MyNamespace
{
    public partial class MapControl : UserControl
    {
        public MapControl()
        {
            this.InitializeComponent();

            this.Loaded += (s, e) => InitializeMap();
            this.MapContainer.Loaded += (s, e) => InitializeMap();
            this.SizeChanged += (s, e) => InvalidateMapSize();

            // Configures OpenSilver routed MouseWheel and touch pan isolation:
            // 1. Sets AllowScrollOnTouchMove = false so OpenSilver PanHelper does not pan the ScrollViewer
            // 2. Marks e.Handled = true on MouseWheel so trackpad two-finger scroll does not scroll the app
            LeafletScrollIsolationHelper.ProtectControl(this, MapContainer);
        }

        private void InvalidateMapSize()
        {
            OpenSilver.Interop.ExecuteJavaScriptVoid(@"
                if (window.leafletMapInstance) {
                    window.leafletMapInstance.invalidateSize();
                }
            ");
        }
    }
}
```

### Step 5.2: Map Initialization Script

Initialize Leaflet with `gestureHandling: true` and install `LeafletScrollIsolationHelper`:

```csharp
private void InitializeMap()
{
    if (_isMapInitialized) return;

    object div = OpenSilver.Interop.GetDiv(MapContainer);
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

    string initScript = LeafletScrollIsolationHelper.GetJavaScriptDefinition() + @"
        (function(containerId) {
            function ensureCss(href) {
                var links = document.getElementsByTagName('link');
                for (var i = 0; i < links.length; i++) {
                    if (links[i].href && links[i].href.indexOf(href) !== -1) return;
                }
                var link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = href;
                document.head.appendChild(link);
            }
            function ensureScript(src) {
                var scripts = document.getElementsByTagName('script');
                for (var i = 0; i < scripts.length; i++) {
                    if (scripts[i].src && scripts[i].src.indexOf(src) !== -1) return;
                }
                var s = document.createElement('script');
                s.src = src;
                document.head.appendChild(s);
            }

            ensureCss('applibs/leaflet/leaflet.css');
            ensureCss('applibs/leaflet-gesture-handling.min.css');
            ensureCss('applibs/L.Control.Locate.min.css');

            if (typeof L === 'undefined') {
                ensureScript('applibs/leaflet/leaflet.js');
            }
            ensureScript('applibs/leaflet-gesture-handling.min.js');
            ensureScript('applibs/L.Control.Locate.min.js');

            var attempts = 0;
            function tryInit() {
                if (typeof L === 'undefined') {
                    if (++attempts < 160) setTimeout(tryInit, 50);
                    return;
                }

                var container = document.getElementById(containerId);
                if (!container || !container.isConnected || container.clientWidth < 30 || container.clientHeight < 30) {
                    if (++attempts < 160) setTimeout(tryInit, 50);
                    return;
                }

                if (window.leafletMapInstance) {
                    try { window.leafletMapInstance.remove(); } catch(e) {}
                }

                var mapDiv = container.querySelector('.pw-leaflet-div');
                if (!mapDiv) {
                    mapDiv = document.createElement('div');
                    mapDiv.className = 'pw-leaflet-div';
                    mapDiv.style.position = 'absolute';
                    mapDiv.style.inset = '0';
                    mapDiv.style.overflow = 'hidden';
                    mapDiv.style.zIndex = '1';
                    container.appendChild(mapDiv);
                }

                // 1. Initialize Leaflet Map with gestureHandling: true
                var map = L.map(mapDiv, {
                    zoomControl: true,
                    attributionControl: false,
                    maxZoom: 22,
                    zoomSnap: 0,
                    preferCanvas: true,
                    gestureHandling: true
                }).setView([37.9, -122.6], 12);

                // 2. Install LeafletScrollIsolationHelper
                if (window.pwLeafletGestureIsolation) {
                    window.pwLeafletGestureIsolation.install(map, container);
                }

                // 3. Add L.Control.Locate
                if (L.control && L.control.locate) {
                    try {
                        L.control.locate({
                            position: 'topleft',
                            icon: 'leaflet-control-locate-location-arrow',
                            iconLoading: 'leaflet-control-locate-spinner',
                            strings: {
                                title: 'Show my location'
                            },
                            locateOptions: {
                                maxZoom: 18,
                                enableHighAccuracy: true
                            }
                        }).addTo(map);
                    } catch(err) {
                        console.warn('Locate control initialization failed:', err);
                    }
                }

                // 4. Add Tile Layer
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 22,
                    maxNativeZoom: 19
                }).addTo(map);

                window.leafletMapInstance = map;

                if (window.ResizeObserver && !container._hasResizeObserver) {
                    container._hasResizeObserver = true;
                    var ro = new ResizeObserver(function() {
                        if (window.leafletMapInstance) window.leafletMapInstance.invalidateSize();
                    });
                    ro.observe(container);
                }

                setTimeout(function() { if (map) map.invalidateSize(); }, 100);
            }

            tryInit();
        })($0);
    ";

    OpenSilver.Interop.ExecuteJavaScriptVoid(initScript, containerId);
    _isMapInitialized = true;
}
```

---

## 6. How `LeafletScrollIsolationHelper` Operates Under the Hood

### OpenSilver PanHelper & `AllowScrollOnTouchMove`

OpenSilver does not implement touchscreen scrolling using standard browser document overflow. Instead:
1. When a user drags fingers on a touchscreen, the browser emits pointer events. OpenSilver's host translates them into routed `MouseMove` events (`IsTouchEvent == true`).
2. OpenSilver's internal `ScrollViewer.PanHelper` inspects `((UIElement)e.OriginalSource).AllowScrollOnTouchMove`:
   ```csharp
   private void HandleMouseMove(object sender, MouseEventArgs e)
   {
       if (!this._isLeftButtonPressed) return;
       if (!((UIElement)e.OriginalSource).AllowScrollOnTouchMove)
       {
           this.Cancel();
           return;
       }
       // pans the ScrollViewer...
   }
   ```
3. By setting `mapControl.AllowScrollOnTouchMove = false` and `container.AllowScrollOnTouchMove = false`, OpenSilver's `PanHelper` immediately aborts any panning originating from the map.

### Preserving Leaflet's `document` Event Listeners

A critical architectural detail of Leaflet is that `L.Draggable` and `TouchZoom` register their active drag/zoom handlers on `document`:
```javascript
L.DomEvent.on(document, 'touchmove', this._onMove, this);
L.DomEvent.on(document, 'touchend', this._onUp, this);
```
**Important Rule**: Never call `e.stopPropagation()` on `touchstart` or `touchmove` at the map container level. Doing so prevents events from bubbling to `document`, completely freezing Leaflet's 2-finger panning and pinch-zooming.
Instead, allow DOM touch events to bubble naturally to `document` so Leaflet operates unimpeded, while relying on C#'s `AllowScrollOnTouchMove = false` to stop OpenSilver from moving the Silverlight scroll bar.

### Two-Finger Trackpad Scrolling & Wheel Protection

Laptops with precision touchpads emit DOM `wheel` events (`ctrlKey == false`, `deltaY != 0`) when two fingers scroll over the touchpad.
1. In C#: `ProtectControl` sets `e.Handled = true` on `MapControl` and `MapContainer` for routed `MouseWheel` events. OpenSilver's `ScrollViewer.OnMouseWheel` checks `if (e.Handled) return;`, completely preventing the Silverlight vertical scroll bar from moving.
2. In JavaScript: `onWheel(e)` calls `e.stopPropagation()`, preventing the wheel event from bubbling to `#opensilver-root` or triggering native browser scroll chaining.

---

## 7. Geolocation (`L.Control.Locate`) Notes & Permissions

- **WebAssembly**: The Geolocation API requires a **Secure Context** (`https://` or `http://localhost`). Browsers will silently deny geolocation if served over plain `http://` on non-localhost origins.
- **Photino / WebView2 (Windows Desktop)**: Photino hosts the page via WebView2 (`custom://app` or `http://localhost`). Ensure Windows location permissions are enabled in Windows Settings -> *Privacy & security* -> *Location*.
- **Compass & Device Orientation**: On mobile devices with orientation sensors, `L.Control.Locate` displays a heading arrow indicator indicating the user's travel direction.

---

## 8. Automated Testing on Mobile & Responsive Screen Sizes

An automated test suite simulates OpenSilver running at mobile screen sizes (< 850px width breakpoint, e.g., 500x600 viewport) where `RootScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto` is active and the content height triggers the Silverlight vertical scroll bar.

### Running the Automated Tests

From PowerShell:
```powershell
.\Tests\Run-LeafletScrollIsolationTests.ps1
```

Or using Node.js directly:
```bash
node Tests/LeafletScrollIsolationTests.js
```

### Verified Test Scenarios

| Test | Interaction Scenario | Expected Behavior |
|:-----|:---------------------|:------------------|
| **1** | Mobile Layout Trigger | Viewport < 850px triggers Silverlight vertical scroll bar (`Auto`, scrollable) |
| **2a** | Two-finger pan on Leaflet map | Reaches Leaflet `document` listener (`L.Draggable`); map moves smoothly |
| **2b** | Two-finger pan on Leaflet map | Silverlight vertical scroll bar does NOT move (`offset = 0`) |
| **3** | Trackpad two-finger scroll over map | Wheel event over map does NOT move Silverlight vertical scroll bar |
| **4** | Ctrl + Mouse wheel over map | Desktop map zoom isolated from OpenSilver; scroll bar does NOT move |
| **5** | Single-finger touch drag inside map | Does NOT move Silverlight vertical scroll bar |
| **6** | Touch drag OUTSIDE the map (page/panel) | DOES scroll the OpenSilver app normally (`offset > 0`) |
| **7** | Teardown (`destroy()`) | All event listeners cleanly unbind; natural scrolling resumes |

---

## 9. Summary Checklist for Any OpenSilver App

| Step | Action | Checked |
|:-----|:-------|:-------:|
| **1** | Copy `leaflet`, `leaflet-gesture-handling`, and `L.Control.Locate` files to `wwwroot/applibs` | ✅ |
| **2** | Add stylesheets and scripts in proper order to `wwwroot/index.html` | ✅ |
| **3** | Ensure `<Content Include="wwwroot\**" CopyToOutputDirectory="Always" />` is in `.csproj` | ✅ |
| **4** | Call `LeafletScrollIsolationHelper.ProtectControl(this, MapContainer)` in control constructor | ✅ |
| **5** | Pass `gestureHandling: true` in `L.map(...)` options | ✅ |
| **6** | Call `window.pwLeafletGestureIsolation.install(map, container)` in map initialization | ✅ |
| **7** | Add `L.control.locate({...}).addTo(map)` for geolocation support | ✅ |
| **8** | Listen to `SizeChanged` and `ResizeObserver` to call `map.invalidateSize()` | ✅ |
| **9** | Run `node Tests/LeafletScrollIsolationTests.js` to verify all automated tests pass | ✅ |
