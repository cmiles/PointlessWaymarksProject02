# Pointed Horizons PMTiles Layer Generation

This folder contains scripts to generate offline vector basemaps, vector contour lines, raster hillshades, and 3D Terrain-RGB elevation layers for Pointed Horizons.

---

## 1. Vector Base Map Extraction (`Extract-StateBaseMaps.ps1`)
Extracts OpenStreetMap vector basemaps directly from daily planet builds on `build.protomaps.com` using Docker and HTTP range requests.

```powershell
# Extract a single state:
.\Extract-StateBaseMaps.ps1 -States "Arizona"
.\Extract-StateBaseMaps.ps1 -States "UT"

# Extract multiple states as separate files:
.\Extract-StateBaseMaps.ps1 -States "AZ", "UT", "CO", "NM"

# Extract a single combined PMTiles file for multiple states:
.\Extract-StateBaseMaps.ps1 -States "AZ", "UT" -Combined

# Extract all 48 Contiguous US States (+ DC):
.\Extract-StateBaseMaps.ps1 -States "All"

# Preview tile count and download size without transferring:
.\Extract-StateBaseMaps.ps1 -States "Washington" -DryRun

# List all state codes and bounding boxes:
.\Extract-StateBaseMaps.ps1 -List
```

---

## 2. Topography & 3D Terrain Generation (`ContoursAndShading.ps1`)
Generates 3 offline layers from SRTM 1-arc-second `.hgt` DEM files in `M:\PointlessWaymarksPublications\dem`:
1. `[state]_contours.pmtiles`: 40-ft (12.192m) vector contour lines (zooms 10–13)
2. `[state]_hillshade.pmtiles`: Web Mercator raster hillshade (zooms 0–12)
3. `[state]_terrain_rgb.pmtiles`: Mapbox Terrain-RGB encoded elevation for 3D terrain perspective (zooms 5–12)

All intermediate files are processed in container memory/overlay storage (`/tmp`) and cleaned up immediately. The `dem` directory is mounted read-only (`:ro`).

```powershell
# Generate all 3 layers for a state:
.\ContoursAndShading.ps1 -States "Utah"
.\ContoursAndShading.ps1 -States "CO"

# Generate all 3 layers for multiple states:
.\ContoursAndShading.ps1 -States "AZ", "UT", "CO", "NM"

# Generate all 3 layers for all contiguous US states:
.\ContoursAndShading.ps1 -States "All"

# Generate only specific layers:
.\ContoursAndShading.ps1 -States "California" -Layers "Hillshade", "TerrainRgb"

# Preview what would be generated:
.\ContoursAndShading.ps1 -States "Nevada" -DryRun

# List all state codes and bounding boxes:
.\ContoursAndShading.ps1 -List
```

---

## 3. Universal MapLibre Style (`topo_style.json`)

`topo_style.json` is a state-agnostic MapLibre style that works with **any state or custom basemap**:

- **Generic Source IDs**:
  - `base`: Vector basemap (OSM roads, landcover, water, places, POIs from `Extract-StateBaseMaps.ps1`)
  - `hillshade`: Raster hillshade tiles from `ContoursAndShading.ps1`
  - `contours`: Vector contour lines with 40ft interval & elevation labels from `ContoursAndShading.ps1`
  - `terrain`: Raster-DEM tiles with Mapbox RGB encoding for 3D terrain mesh

- **Dynamic Source Binding in Pointed Horizons**:
  When you enable any state's files (e.g. `utah_basemap.pmtiles`, `utah_contours.pmtiles`, `utah_hillshade.pmtiles`, `utah_terrain_rgb.pmtiles`), Pointed Horizons automatically matches them to these generic sources and auto-reconciles vector layer names.

- **How to Use**:
  1. Open Pointed Horizons.
  2. Open the hamburger menu and click **Configure PMTiles & Offline Storage**.
  3. Under **MapLibre Style JSON**, select `topo_style.json`.
  4. Enable the PMTiles files for whatever state(s) you are viewing.
  5. Click **Apply PMTiles** and choose the **PMTiles** base layer in the map controls.

---

## 4. DEM Peak & Highpoint Extraction (`Extract-DemPeaks.ps1`)

Extracts named mountain peaks, summits, volcanoes, and highpoints from OpenStreetMap (Overpass API) matching the exact 1°×1° bounding boxes of your SRTM DEM `.hgt` tiles, and writes `[TileKey].peaks.json` alongside each tile.

- **Automatic DEM Elevation Sampling**: If an OSM node is missing an `ele` tag, elevation is read directly from the local `.hgt` binary raster.
- **Custom / Local Overpass Server**: Pass `-OverpassServer "http://localhost:12345"` to run against a local instance at full speed without throttle delays or public server rate limits.
- **Failover & Resilience**: Automatically tries multiple public Overpass API mirrors with backoff when no custom server is specified.
- **Filtering**: Supports filtering by individual tiles, state names, or minimum elevation.

```powershell
# Use a local Overpass instance (runs at full speed with 0s throttle delay):
.\Extract-DemPeaks.ps1 -OverpassServer "http://localhost:12345"
.\Extract-DemPeaks.ps1 -States "Arizona" -OverpassServer "http://localhost:8080"

# Extract peaks for a specific tile:
.\Extract-DemPeaks.ps1 -Tiles "N32W111"

# Extract peaks for multiple tiles:
.\Extract-DemPeaks.ps1 -Tiles "N32W111", "N32W110", "N31W111"

# Extract peaks for all DEM tiles covering a state:
.\Extract-DemPeaks.ps1 -States "Arizona"
.\Extract-DemPeaks.ps1 -States "AZ", "UT", "CO", "NM"

# Process all .hgt DEM tiles in the directory (skips already-extracted files):
.\Extract-DemPeaks.ps1

# Force re-extract and overwrite existing .peaks.json files:
.\Extract-DemPeaks.ps1 -States "AZ" -Overwrite

# List all discovered DEM tiles and check whether peaks JSON is present:
.\Extract-DemPeaks.ps1 -List

# Preview tile bounding boxes without sending API requests:
.\Extract-DemPeaks.ps1 -States "Utah" -DryRun
```


