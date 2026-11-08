<#
.SYNOPSIS
    Generates offline vector contours, raster hillshade, and 3D Terrain-RGB PMTiles for any or all contiguous US states.

.DESCRIPTION
    Extracts elevation and topography from SRTM 1-arc-second (.hgt) DEM files in M:\PointlessWaymarksPublications\dem
    using Docker (jimmyrocks/gdaltippe:latest).
    Produces 3 distinct PMTiles datasets per state:
      1. [state]_contours.pmtiles    (40-ft / 12.192m vector contour lines, zoom 10-13)
      2. [state]_hillshade.pmtiles   (Web Mercator raster hillshade, zoom 0-12)
      3. [state]_terrain_rgb.pmtiles (Mapbox Terrain-RGB encoded elevation for 3D terrain mesh, zoom 5-12)

    All intermediate files (.vrt, .geojson, .tif, .mbtiles) are kept strictly in container /tmp memory/overlay storage
    and cleaned up immediately. The DEM source directory is mounted read-only (:ro), ensuring no temporary files
    are written to M:\PointlessWaymarksPublications\dem or left in M:\PointlessWaymarksPublications\pmtiles.

.PARAMETER States
    One or more state names or 2-letter postal codes (e.g. -States "Arizona", -States "AZ","UT","CO","NM").
    Use -States "All" to process all 48 contiguous US states + DC.

.PARAMETER DemDir
    Source directory containing .hgt DEM files. Defaults to "M:\PointlessWaymarksPublications\dem".

.PARAMETER OutputDir
    Destination directory where .pmtiles will be saved. Defaults to current directory ($PWD).

.PARAMETER Layers
    Subset of layers to generate: "All", "Contours", "Hillshade", "TerrainRgb". Default is "All".

.PARAMETER ContourIntervalMeters
    Interval between contour lines in meters. Default is 12.192m (40 feet).

.PARAMETER MaxZoom
    Maximum zoom level for Hillshade and Terrain-RGB raster PMTiles. Default is 12.

.PARAMETER Overwrite
    Force re-generating layers even if [state]_*.pmtiles already exists. Otherwise existing files are skipped.

.PARAMETER DryRun
    Validates state bounding boxes and checks output paths without running the full GDAL pipeline.

.PARAMETER List
    Lists all supported contiguous US states, postal codes, and bounding boxes.

.EXAMPLE
    .\ContoursAndShading.ps1 -States "Arizona"
    Generates arizona_contours.pmtiles, arizona_hillshade.pmtiles, and arizona_terrain_rgb.pmtiles.

.EXAMPLE
    .\ContoursAndShading.ps1 -States "AZ", "UT", "CO", "NM"
    Generates all 3 layers for the Four Corners states.

.EXAMPLE
    .\ContoursAndShading.ps1 -States "California" -Layers "Hillshade", "TerrainRgb"
    Generates only hillshade and 3D terrain-rgb layers for California, skipping contours.

.EXAMPLE
    .\ContoursAndShading.ps1 -States "All"
    Batches contour, hillshade, and terrain-rgb generation for all 48 contiguous US states.

.EXAMPLE
    .\ContoursAndShading.ps1 -List
    Displays all state codes, full names, and bounding boxes.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$States,

    [Parameter(Mandatory = $false)]
    [string]$DemDir = "M:\PointlessWaymarksPublications\dem",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = $PWD,

    [Parameter(Mandatory = $false)]
    [ValidateSet("All", "Contours", "Hillshade", "TerrainRgb", "Terrain-RGB")]
    [string[]]$Layers = @("All"),

    [Parameter(Mandatory = $false)]
    [double]$ContourIntervalMeters = 12.192,

    [Parameter(Mandatory = $false)]
    [int]$MaxZoom = 12,

    [Parameter(Mandatory = $false)]
    [switch]$Overwrite,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$List
)

# -----------------------------------------------------------------------------
# 1. State Bounding Box Database (48 Contiguous States + DC + AK/HI)
# Format: minLon,minLat,maxLon,maxLat (West, South, East, North)
# -----------------------------------------------------------------------------
$StateDatabase = [ordered]@{
    "AL" = @{ Name = "Alabama";              BBox = "-88.4732,30.2233,-84.8891,35.0080"; Contiguous = $true }
    "AZ" = @{ Name = "Arizona";              BBox = "-114.8166,31.3322,-109.0452,37.0043"; Contiguous = $true }
    "AR" = @{ Name = "Arkansas";             BBox = "-94.6179,33.0041,-89.6448,36.4996"; Contiguous = $true }
    "CA" = @{ Name = "California";           BBox = "-124.4820,32.5288,-114.1312,42.0095"; Contiguous = $true }
    "CO" = @{ Name = "Colorado";             BBox = "-109.0603,36.9924,-102.0415,41.0034"; Contiguous = $true }
    "CT" = @{ Name = "Connecticut";          BBox = "-73.7278,40.9801,-71.7870,42.0506"; Contiguous = $true }
    "DE" = @{ Name = "Delaware";             BBox = "-75.7887,38.4510,-75.0489,39.8390"; Contiguous = $true }
    "DC" = @{ Name = "District of Columbia"; BBox = "-77.1198,38.7916,-76.9094,38.9955"; Contiguous = $true }
    "FL" = @{ Name = "Florida";              BBox = "-87.6349,24.5231,-80.0314,31.0008"; Contiguous = $true }
    "GA" = @{ Name = "Georgia";              BBox = "-85.6052,30.3579,-80.8397,35.0007"; Contiguous = $true }
    "ID" = @{ Name = "Idaho";                BBox = "-117.2430,41.9881,-111.0436,49.0011"; Contiguous = $true }
    "IL" = @{ Name = "Illinois";             BBox = "-91.5131,36.9703,-87.4948,42.5085"; Contiguous = $true }
    "IN" = @{ Name = "Indiana";              BBox = "-88.0978,37.7717,-84.7846,41.7606"; Contiguous = $true }
    "IA" = @{ Name = "Iowa";                 BBox = "-96.6397,40.3756,-90.1401,43.5011"; Contiguous = $true }
    "KS" = @{ Name = "Kansas";               BBox = "-102.0517,36.9930,-94.5884,40.0032"; Contiguous = $true }
    "KY" = @{ Name = "Kentucky";             BBox = "-89.5715,36.4971,-81.9648,39.1475"; Contiguous = $true }
    "LA" = @{ Name = "Louisiana";            BBox = "-94.0431,28.9286,-88.8170,33.0195"; Contiguous = $true }
    "ME" = @{ Name = "Maine";                BBox = "-71.0839,42.9778,-66.9499,47.4597"; Contiguous = $true }
    "MD" = @{ Name = "Maryland";             BBox = "-79.4877,37.9117,-75.0489,39.7230"; Contiguous = $true }
    "MA" = @{ Name = "Massachusetts";        BBox = "-73.5081,41.2380,-69.9284,42.8866"; Contiguous = $true }
    "MI" = @{ Name = "Michigan";             BBox = "-90.4181,41.6961,-82.4135,48.2617"; Contiguous = $true }
    "MN" = @{ Name = "Minnesota";            BBox = "-97.2392,43.4994,-89.4918,49.3844"; Contiguous = $true }
    "MS" = @{ Name = "Mississippi";          BBox = "-91.6550,30.1739,-88.0979,34.9961"; Contiguous = $true }
    "MO" = @{ Name = "Missouri";             BBox = "-95.7747,35.9957,-89.0988,40.6136"; Contiguous = $true }
    "MT" = @{ Name = "Montana";              BBox = "-116.0500,44.3582,-104.0396,49.0014"; Contiguous = $true }
    "NE" = @{ Name = "Nebraska";             BBox = "-104.0535,39.9999,-95.3083,43.0017"; Contiguous = $true }
    "NV" = @{ Name = "Nevada";               BBox = "-120.0057,35.0019,-114.0396,42.0022"; Contiguous = $true }
    "NH" = @{ Name = "New Hampshire";        BBox = "-72.5572,42.6970,-70.7027,45.3054"; Contiguous = $true }
    "NJ" = @{ Name = "New Jersey";           BBox = "-75.5596,38.9285,-73.9025,41.3574"; Contiguous = $true }
    "NM" = @{ Name = "New Mexico";           BBox = "-109.0502,31.3323,-103.0020,37.0002"; Contiguous = $true }
    "NY" = @{ Name = "New York";             BBox = "-79.7622,40.4961,-71.8562,45.0159"; Contiguous = $true }
    "NC" = @{ Name = "North Carolina";        BBox = "-84.3219,33.8423,-75.4606,36.5881"; Contiguous = $true }
    "ND" = @{ Name = "North Dakota";          BBox = "-104.0489,45.9351,-96.5545,49.0005"; Contiguous = $true }
    "OH" = @{ Name = "Ohio";                 BBox = "-84.8202,38.4032,-80.5187,41.9775"; Contiguous = $true }
    "OK" = @{ Name = "Oklahoma";             BBox = "-103.0026,33.6158,-94.4307,37.0022"; Contiguous = $true }
    "OR" = @{ Name = "Oregon";               BBox = "-124.5662,41.9918,-116.4635,46.2920"; Contiguous = $true }
    "PA" = @{ Name = "Pennsylvania";         BBox = "-80.5199,39.7198,-74.6895,42.2699"; Contiguous = $true }
    "RI" = @{ Name = "Rhode Island";         BBox = "-71.8628,41.1463,-71.1206,42.0188"; Contiguous = $true }
    "SC" = @{ Name = "South Carolina";       BBox = "-83.3532,32.0346,-78.5420,35.2154"; Contiguous = $true }
    "SD" = @{ Name = "South Dakota";         BBox = "-104.0579,42.4796,-96.4365,45.9455"; Contiguous = $true }
    "TN" = @{ Name = "Tennessee";            BBox = "-90.3103,34.9829,-81.6469,36.6781"; Contiguous = $true }
    "TX" = @{ Name = "Texas";                BBox = "-106.6456,25.8373,-93.5083,36.5007"; Contiguous = $true }
    "UT" = @{ Name = "Utah";                 BBox = "-114.0529,36.9979,-109.0411,42.0016"; Contiguous = $true }
    "VT" = @{ Name = "Vermont";              BBox = "-73.4377,42.7269,-71.4646,45.0167"; Contiguous = $true }
    "VA" = @{ Name = "Virginia";             BBox = "-83.6754,36.5407,-75.2423,39.4660"; Contiguous = $true }
    "WA" = @{ Name = "Washington";           BBox = "-124.7626,45.5435,-116.9160,49.0025"; Contiguous = $true }
    "WV" = @{ Name = "West Virginia";        BBox = "-82.6447,37.2015,-77.7195,40.6388"; Contiguous = $true }
    "WI" = @{ Name = "Wisconsin";            BBox = "-92.8881,42.4919,-86.8054,47.0806"; Contiguous = $true }
    "WY" = @{ Name = "Wyoming";              BBox = "-111.0569,40.9947,-104.0522,45.0059"; Contiguous = $true }
    "AK" = @{ Name = "Alaska";               BBox = "-179.1489,51.2142,-129.9742,71.3652"; Contiguous = $false }
    "HI" = @{ Name = "Hawaii";               BBox = "-160.2471,18.9104,-154.8068,22.2355"; Contiguous = $false }
}

# -----------------------------------------------------------------------------
# Handle -List switch
# -----------------------------------------------------------------------------
if ($List) {
    Write-Host "`n=== Supported Contiguous US States for DEM Processing ===" -ForegroundColor Cyan
    $tableData = foreach ($key in $StateDatabase.Keys) {
        $entry = $StateDatabase[$key]
        if ($entry.Contiguous) {
            [PSCustomObject]@{
                Code  = $key
                State = $entry.Name
                BBox  = $entry.BBox
            }
        }
    }
    $tableData | Format-Table -AutoSize
    Write-Host "Non-contiguous (Available by code): AK (Alaska), HI (Hawaii)`n" -ForegroundColor DarkGray
    return
}

# -----------------------------------------------------------------------------
# 2. Host Directory Validation & Cleanup Helpers
# -----------------------------------------------------------------------------
if (-not (Test-Path $DemDir)) {
    Write-Host "Error: DEM directory not found at '$DemDir'." -ForegroundColor Red
    return
}
$absDemDir = (Resolve-Path $DemDir).Path

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$absOutputDir = (Resolve-Path $OutputDir).Path

# Safety function: Cleans up any stray intermediate files on the host
function Cleanup-HostTempFiles {
    param([string]$Dir)
    if (Test-Path $Dir) {
        Get-ChildItem -Path $Dir -Include "*.vrt", "*.tif", "*.tiff", "*.geojson", "*.mbtiles" -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch '\.pmtiles$' } |
            ForEach-Object {
                try {
                    Write-Host "  -> Removing lingering temporary file on host: $($_.Name)" -ForegroundColor DarkGray
                    Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
                } catch { }
            }
    }
}

# Clean any previously abandoned temp files before starting
Cleanup-HostTempFiles $absOutputDir

# -----------------------------------------------------------------------------
# 3. Check Docker Availability
# -----------------------------------------------------------------------------
try {
    $null = & docker info --format '{{.ServerVersion}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not running."
    }
}
catch {
    Write-Host "Error: Docker is either not installed or Docker Desktop is not running." -ForegroundColor Red
    Write-Host "Please start Docker Desktop and run this script again." -ForegroundColor Yellow
    return
}

$dockerImg = "jimmyrocks/gdaltippe:latest"

# -----------------------------------------------------------------------------
# 4. Resolve Selected States
# -----------------------------------------------------------------------------
if (-not $States -or $States.Count -eq 0) {
    Write-Host "`nNo state specified. You can specify state names, postal codes, or 'All'." -ForegroundColor Yellow
    Write-Host "Example: .\ContoursAndShading.ps1 -States Arizona" -ForegroundColor Yellow
    Write-Host "Example: .\ContoursAndShading.ps1 -States AZ, UT, CO, NM" -ForegroundColor Yellow
    Write-Host "Example: .\ContoursAndShading.ps1 -States All" -ForegroundColor Yellow
    Write-Host "Use -List to see all states and bounding boxes.`n" -ForegroundColor DarkGray
    return
}

# Flatten input list (handles comma-separated strings like "AZ, UT" or space-separated tokens)
$flattenedStates = @()
foreach ($s in $States) {
    if ($s.Contains(",")) {
        $parts = $s.Split(',')
        foreach ($p in $parts) {
            if (-not [string]::IsNullOrWhiteSpace($p)) { $flattenedStates += $p.Trim() }
        }
    }
    elseif (-not [string]::IsNullOrWhiteSpace($s)) {
        $flattenedStates += $s.Trim()
    }
}

# Expand "ALL" or "CONTIGUOUS"
$isAll = $false
foreach ($s in $flattenedStates) {
    if ($s.ToUpperInvariant() -in @("ALL", "CONTIGUOUS", "CONUS", "LOWER48")) {
        $isAll = $true
        break
    }
}

$selectedEntries = [System.Collections.Generic.List[PSCustomObject]]::new()

if ($isAll) {
    foreach ($key in $StateDatabase.Keys) {
        $entry = $StateDatabase[$key]
        if ($entry.Contiguous) {
            $selectedEntries.Add([PSCustomObject]@{
                Code = $key
                Name = $entry.Name
                BBox = $entry.BBox
            })
        }
    }
}
else {
    foreach ($item in $flattenedStates) {
        $trimmed = $item.Trim()
        $foundKey = $null

        if ($StateDatabase.Contains($trimmed.ToUpperInvariant())) {
            $foundKey = $trimmed.ToUpperInvariant()
        }
        else {
            foreach ($k in $StateDatabase.Keys) {
                if ($StateDatabase[$k].Name.Equals($trimmed, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $foundKey = $k
                    break
                }
            }
        }

        if ($foundKey) {
            $entry = $StateDatabase[$foundKey]
            $selectedEntries.Add([PSCustomObject]@{
                Code = $foundKey
                Name = $entry.Name
                BBox = $entry.BBox
            })
        }
        else {
            Write-Host "Warning: Unrecognized state '$trimmed'. Skipping (use -List to view all states)." -ForegroundColor Yellow
        }
    }
}

if ($selectedEntries.Count -eq 0) {
    Write-Host "Error: No valid states selected for processing." -ForegroundColor Red
    return
}

# Resolve active layer toggles
$doAllLayers = $Layers -contains "All"
$wantContours = $doAllLayers -or ($Layers -contains "Contours")
$wantHillshade = $doAllLayers -or ($Layers -contains "Hillshade")
$wantTerrainRgb = $doAllLayers -or ($Layers -contains "TerrainRgb") -or ($Layers -contains "Terrain-RGB")

function Get-SnakeCaseName ([string]$str) {
    return ($str.ToLowerInvariant() -replace '[^a-z0-9]+', '_').Trim('_')
}

Write-Host "`n=== Pointed Horizons DEM Layer Generation ===" -ForegroundColor Cyan
Write-Host "DEM Directory    : $absDemDir (Mounted :ro)" -ForegroundColor DarkGray
Write-Host "Output Directory : $absOutputDir" -ForegroundColor DarkGray
Write-Host "Layers Requested : $(@(if ($wantContours){'Contours'} if ($wantHillshade){'Hillshade'} if ($wantTerrainRgb){'Terrain-RGB'}) -join ', ')" -ForegroundColor DarkGray
Write-Host "Contour Interval : $ContourIntervalMeters m (40 ft)" -ForegroundColor DarkGray
Write-Host "Max Raster Zoom  : $MaxZoom" -ForegroundColor DarkGray

$results = [System.Collections.Generic.List[PSCustomObject]]::new()
$totalCount = $selectedEntries.Count
$stateIdx = 0
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    foreach ($item in $selectedEntries) {
        $stateIdx++
        $stateName = $item.Name
        $stateCode = $item.Code
        $snake = Get-SnakeCaseName $stateName

        $contoursFile = Join-Path $absOutputDir "${snake}_contours.pmtiles"
        $hillshadeFile = Join-Path $absOutputDir "${snake}_hillshade.pmtiles"
        $terrainRgbFile = Join-Path $absOutputDir "${snake}_terrain_rgb.pmtiles"

        # Check what needs to be generated based on existing files and -Overwrite
        $needContours = $wantContours -and ($Overwrite -or -not (Test-Path $contoursFile))
        $needHillshade = $wantHillshade -and ($Overwrite -or -not (Test-Path $hillshadeFile))
        $needTerrainRgb = $wantTerrainRgb -and ($Overwrite -or -not (Test-Path $terrainRgbFile))

        Write-Host "`n[$stateIdx/$totalCount] Processing $stateName ($stateCode)..." -ForegroundColor Cyan
        Write-Host "  Bounding Box: $($item.BBox)" -ForegroundColor DarkGray

        if (-not $needContours -and -not $needHillshade -and -not $needTerrainRgb) {
            Write-Host "  -> All requested layers already exist. Skipping (use -Overwrite to re-generate)." -ForegroundColor Yellow
            $results.Add([PSCustomObject]@{ State = $stateName; Code = $stateCode; Contours = (Test-Path $contoursFile); Hillshade = (Test-Path $hillshadeFile); TerrainRgb = (Test-Path $terrainRgbFile); Status = "Skipped (Exists)" })
            continue
        }

        if ($DryRun) {
            Write-Host "  -> [Dry-Run] Would generate:" -ForegroundColor Green
            if ($needContours)   { Write-Host "     - ${snake}_contours.pmtiles" }
            if ($needHillshade)  { Write-Host "     - ${snake}_hillshade.pmtiles" }
            if ($needTerrainRgb) { Write-Host "     - ${snake}_terrain_rgb.pmtiles" }
            $results.Add([PSCustomObject]@{ State = $stateName; Code = $stateCode; Contours = $needContours; Hillshade = $needHillshade; TerrainRgb = $needTerrainRgb; Status = "Dry-Run OK" })
            continue
        }

        # Parse BBox into minLon minLat maxLon maxLat
        $pts = $item.BBox.Split(',') | ForEach-Object { [double]$_ }
        $minLon = $pts[0]; $minLat = $pts[1]; $maxLon = $pts[2]; $maxLat = $pts[3]

        $stateTimer = [System.Diagnostics.Stopwatch]::StartNew()

        # Execute container pipeline using /tmp for intermediate data and /out for destination
        $dockerArgs = @(
            "run", "--rm",
            "-v", "${absDemDir}:/dems:ro",
            "-v", "${absOutputDir}:/out",
            "-e", "STATE_NAME=$snake",
            "-e", "STATE_CODE=$($stateCode.ToLowerInvariant())",
            "-e", "MIN_LON=$minLon",
            "-e", "MIN_LAT=$minLat",
            "-e", "MAX_LON=$maxLon",
            "-e", "MAX_LAT=$maxLat",
            "-e", "DO_CONTOURS=$(if ($needContours){1}else{0})",
            "-e", "DO_HILLSHADE=$(if ($needHillshade){1}else{0})",
            "-e", "DO_TERRAIN_RGB=$(if ($needTerrainRgb){1}else{0})",
            "-e", "CONTOUR_INTERVAL=$ContourIntervalMeters",
            "-e", "MAX_ZOOM=$MaxZoom",
            $dockerImg, "bash", "-c", @'
set -e

echo "  [Step 1/4] Building Virtual Raster (VRT) clipped to ${STATE_NAME}..."
gdalbuildvrt -te $MIN_LON $MIN_LAT $MAX_LON $MAX_LAT /tmp/${STATE_NAME}_dem.vrt /dems/*.hgt

if ! grep -q '/dems/' /tmp/${STATE_NAME}_dem.vrt; then
    echo "  ERROR: No overlapping .hgt files found in /dems for bounding box ($MIN_LON, $MIN_LAT, $MAX_LON, $MAX_LAT)."
    rm -f /tmp/${STATE_NAME}_dem.vrt
    exit 2
fi

# A. Contours Layer
if [ "$DO_CONTOURS" = "1" ]; then
    echo "  [Step 2/4] Generating ${CONTOUR_INTERVAL}m Contours..."
    gdal_contour -a ele -i $CONTOUR_INTERVAL /tmp/${STATE_NAME}_dem.vrt /tmp/${STATE_NAME}_contours.geojson
    
    echo "             Packaging into vector MBTiles with Tippecanoe..."
    tippecanoe --force -o /tmp/${STATE_NAME}_contours.mbtiles \
        -l "contours" \
        -Z10 -z13 \
        --simplify-only-low-zooms \
        --no-tile-size-limit \
        --no-simplification-of-shared-nodes \
        --coalesce \
        --reorder \
        /tmp/${STATE_NAME}_contours.geojson
    rm -f /tmp/${STATE_NAME}_contours.geojson
    
    echo "             Converting to vector PMTiles..."
    pmtiles convert --force /tmp/${STATE_NAME}_contours.mbtiles /out/${STATE_NAME}_contours.pmtiles
    rm -f /tmp/${STATE_NAME}_contours.mbtiles
    echo "             -> Saved /out/${STATE_NAME}_contours.pmtiles"
else
    echo "  [Step 2/4] Contours skipped."
fi

# B. Hillshade Layer
if [ "$DO_HILLSHADE" = "1" ]; then
    echo "  [Step 3/4] Generating Raster Hillshade..."
    gdaldem hillshade /tmp/${STATE_NAME}_dem.vrt /tmp/${STATE_NAME}_hillshade_4326.tif -s 111120
    
    echo "             Warping hillshade to Web Mercator (EPSG:3857)..."
    gdalwarp -t_srs EPSG:3857 -r bilinear -multi /tmp/${STATE_NAME}_hillshade_4326.tif /tmp/${STATE_NAME}_hillshade_3857.tif
    rm -f /tmp/${STATE_NAME}_hillshade_4326.tif
    
    echo "             Converting to raster PMTiles (MaxZoom ${MAX_ZOOM})..."
    gdal_translate -of PMTiles -co MAXZOOM=$MAX_ZOOM /tmp/${STATE_NAME}_hillshade_3857.tif /out/${STATE_NAME}_hillshade.pmtiles
    rm -f /tmp/${STATE_NAME}_hillshade_3857.tif
    echo "             -> Saved /out/${STATE_NAME}_hillshade.pmtiles"
else
    echo "  [Step 3/4] Hillshade skipped."
fi

# C. 3D Terrain-RGB Layer
if [ "$DO_TERRAIN_RGB" = "1" ]; then
    echo "  [Step 4/4] Generating 3D Terrain-RGB..."
    gdalwarp -t_srs EPSG:3857 -r bilinear -multi \
        -co COMPRESS=DEFLATE -co TILED=YES \
        /tmp/${STATE_NAME}_dem.vrt /tmp/${STATE_NAME}_dem_3857.tif
    
    echo "             Encoding elevation to Mapbox Terrain-RGB..."
    python3 -c "
import os
import numpy as np
from osgeo import gdal

gdal.UseExceptions()

state = os.environ.get('STATE_NAME', 'state')
src_path = f'/tmp/{state}_dem_3857.tif'
out_path = f'/tmp/{state}_terrain_rgb.tif'

src = gdal.Open(src_path)
band = src.GetRasterBand(1)
xsize, ysize = src.RasterXSize, src.RasterYSize

driver = gdal.GetDriverByName('GTiff')
out = driver.Create(out_path, xsize, ysize, 3, gdal.GDT_Byte, 
                    options=['COMPRESS=DEFLATE', 'TILED=YES'])
out.SetGeoTransform(src.GetGeoTransform())
out.SetProjection(src.GetProjection())

block_size = 2048
for y in range(0, ysize, block_size):
    h = min(block_size, ysize - y)
    for x in range(0, xsize, block_size):
        w = min(block_size, xsize - x)
        elev = band.ReadAsArray(x, y, w, h).astype(np.float32)
        elev[elev < -10000] = 0
        v = np.clip(np.round((elev + 10000.0) * 10.0), 0, 16777215).astype(np.uint32)
        
        out.GetRasterBand(1).WriteArray(((v >> 16) & 0xFF).astype(np.uint8), x, y)
        out.GetRasterBand(2).WriteArray(((v >> 8) & 0xFF).astype(np.uint8), x, y)
        out.GetRasterBand(3).WriteArray((v & 0xFF).astype(np.uint8), x, y)
src = None
out = None
"
    rm -f /tmp/${STATE_NAME}_dem_3857.tif
    
    echo "             Packaging into MBTiles raster archive (Zoom ${MAX_ZOOM})..."
    gdal_translate -of MBTiles /tmp/${STATE_NAME}_terrain_rgb.tif /tmp/${STATE_NAME}_terrain_rgb.mbtiles
    rm -f /tmp/${STATE_NAME}_terrain_rgb.tif
    
    echo "             Adding pyramid overviews (Zooms 5-11)..."
    gdaladdo -r nearest /tmp/${STATE_NAME}_terrain_rgb.mbtiles 2 4 8 16 32 64 128
    
    echo "             Converting MBTiles to raster PMTiles..."
    pmtiles convert --force /tmp/${STATE_NAME}_terrain_rgb.mbtiles /out/${STATE_NAME}_terrain_rgb.pmtiles
    rm -f /tmp/${STATE_NAME}_terrain_rgb.mbtiles
    echo "             -> Saved /out/${STATE_NAME}_terrain_rgb.pmtiles"
else
    echo "  [Step 4/4] Terrain-RGB skipped."
fi

# Cleanup VRT
rm -f /tmp/${STATE_NAME}_dem.vrt
'@
        )

        & docker @dockerArgs

        $stateTimer.Stop()
        $stateSec = [math]::Round($stateTimer.Elapsed.TotalSeconds, 1)

        # Host-level cleanup safeguard
        Cleanup-HostTempFiles $absOutputDir

        $cOk = Test-Path $contoursFile
        $hOk = Test-Path $hillshadeFile
        $tOk = Test-Path $terrainRgbFile

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  -> Completed $stateName in $stateSec s" -ForegroundColor Green
            $results.Add([PSCustomObject]@{ State = $stateName; Code = $stateCode; Contours = $cOk; Hillshade = $hOk; TerrainRgb = $tOk; Status = "Success" })
        }
        else {
            Write-Host "  -> Error during processing for $stateName (Exit code: $LASTEXITCODE)" -ForegroundColor Red
            $results.Add([PSCustomObject]@{ State = $stateName; Code = $stateCode; Contours = $cOk; Hillshade = $hOk; TerrainRgb = $tOk; Status = "Failed" })
        }
    }
}
finally {
    # Always ensure temporary host files are cleaned up if aborted
    Cleanup-HostTempFiles $absOutputDir
}

$totalStopwatch.Stop()
$totalElapsed = $totalStopwatch.Elapsed

# -----------------------------------------------------------------------------
# 5. Final Summary Table
# -----------------------------------------------------------------------------
Write-Host "`n=== DEM Processing Summary (Total Elapsed: $($totalElapsed.ToString('mm\:ss')) ) ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize
Write-Host "Files saved in: $absOutputDir`n" -ForegroundColor DarkGray
