# App Libs

This directory (`wwwroot/applibs`) contains application-level third-party libraries and added resources that are not part of the standard OpenSilver distribution (which resides in `wwwroot/libs`).

## Contents
- `exiftool/`: WebAssembly distribution of ExifTool (`zeroperl-mqcadjqm.wasm` and `index.js`).
- `leaflet/`: Leaflet map library (`leaflet.js`, `leaflet.css`, and marker `images/`).
- `leaflet-gesture-handling.min.js`, `leaflet-gesture-handling.min.css`: Leaflet gesture handling plugin for two-finger mobile panning/zooming and Ctrl+wheel desktop zooming.
- `L.Control.Locate.min.js`, `L.Control.Locate.min.css`: Leaflet geolocation control for GPS positioning and heading orientation.

All custom non-OpenSilver web resources should be placed in this folder or its subdirectories.
