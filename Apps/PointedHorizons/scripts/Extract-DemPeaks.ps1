<#
.SYNOPSIS
    Extracts named peaks, summits, and highpoints matching 1°x1° SRTM/DEM .hgt tiles
    and saves them as [TileKey].peaks.json for PointedHorizons horizon rendering.

.DESCRIPTION
    Scans a DEM directory containing 1°x1° .hgt (or .zip) elevation tiles, calculates
    the exact 1°x1° geographic bounding box for each tile, queries OpenStreetMap's
    Overpass API for all named summits and volcanoes within that box, and produces
    a structured [TileKey].peaks.json file alongside the DEM file.

    Features:
      - Supports individual tiles (-Tiles "N32W111"), states (-States "AZ", "UT"), or all tiles in DemDir.
      - Automatically extracts elevation from the binary .hgt file if the OSM node lacks an 'ele' tag.
      - Multi-mirror Overpass API support with automatic fallback and retry if a server is busy.
      - Idempotent: skips existing .peaks.json files unless -Overwrite is specified.
      - Deduplicates peaks sharing identical names within close proximity.

.PARAMETER DemDir
    Directory containing .hgt or .zip DEM files. Defaults to "M:\PointlessWaymarksPublications\dem".

.PARAMETER OutputDir
    Directory where [TileKey].peaks.json files will be saved. Defaults to $DemDir.

.PARAMETER OverpassServer
    Optional custom Overpass API endpoint URL (e.g. "http://localhost:12345" or "http://localhost:8080/api/interpreter").
    Ideal for local Overpass instances. When specified, only this server is queried, and ThrottleDelaySeconds
    automatically defaults to 0 unless explicitly specified.

.PARAMETER Tiles
    One or more specific tile keys to process (e.g. -Tiles "N32W111", "N32W110").

.PARAMETER States
    One or more US state names or 2-letter postal codes (e.g. -States "AZ", -States "Arizona","Utah").
    Filters tiles to those covering the selected state(s).

.PARAMETER MinElevationM
    Optional minimum summit elevation threshold in meters (default is 0, extracting all named peaks).

.PARAMETER ThrottleDelaySeconds
    Delay in seconds between API calls to be polite to public Overpass servers (default is 2, or 0 when using local -OverpassServer).

.PARAMETER Overwrite
    Force re-extracting even if [TileKey].peaks.json already exists.

.PARAMETER DryRun
    Previews the tiles and bounding boxes that would be processed without making API calls.

.PARAMETER List
    Lists all DEM tiles found in DemDir and their current .peaks.json status.

.EXAMPLE
    .\Extract-DemPeaks.ps1
    Processes all .hgt DEM files found in M:\PointlessWaymarksPublications\dem using public Overpass mirrors.

.EXAMPLE
    .\Extract-DemPeaks.ps1 -OverpassServer "http://localhost:12345"
    Processes all tiles using a local Overpass instance with zero throttling delay.

.EXAMPLE
    .\Extract-DemPeaks.ps1 -Tiles "N32W111", "N32W110" -OverpassServer "http://127.0.0.1:8080"
    Extracts summits for specific DEM tiles using a local instance.

.EXAMPLE
    .\Extract-DemPeaks.ps1 -States "Arizona"
    Extracts summits for all DEM tiles covering Arizona.

.EXAMPLE
    .\Extract-DemPeaks.ps1 -List
    Lists all DEM tiles in the directory and indicates whether .peaks.json is present.
#>

[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$DemDir = "M:\PointlessWaymarksPublications\dem",

    [Parameter(Mandatory = $false)]
    [string]$OutputDir,

    [Parameter(Mandatory = $false)]
    [Alias("OverpassUrl", "OverpassEndpoint", "Server", "Url")]
    [string]$OverpassServer,

    [Parameter(Mandatory = $false)]
    [string[]]$Tiles,

    [Parameter(Mandatory = $false)]
    [string[]]$States,

    [Parameter(Mandatory = $false)]
    [double]$MinElevationM = 0.0,

    [Parameter(Mandatory = $false)]
    [int]$ThrottleDelaySeconds = 2,

    [Parameter(Mandatory = $false)]
    [switch]$Overwrite,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$List
)

$ErrorActionPreference = "Stop"

# If a URL was passed positionally into DemDir, redirect it to OverpassServer
if ($DemDir -match '^https?://' -or $DemDir -match '^localhost(:\d+)?') {
    if ([string]::IsNullOrWhiteSpace($OverpassServer)) {
        $OverpassServer = $DemDir
    }
    $DemDir = "M:\PointlessWaymarksPublications\dem"
}

# Default OutputDir to DemDir if not specified
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = $DemDir
}

# Configure Overpass server endpoint(s)
if (![string]::IsNullOrWhiteSpace($OverpassServer)) {
    $serverClean = $OverpassServer.Trim().TrimEnd('/')
    if ($serverClean -notmatch '^https?://') {
        $serverClean = "http://$serverClean"
    }
    if ($serverClean -notmatch '/(api/)?interpreter$') {
        $serverClean = "$serverClean/api/interpreter"
    }
    $OverpassMirrors = @($serverClean)

    # When using a custom / local server, default ThrottleDelaySeconds to 0 if not explicitly supplied
    if (!$PSBoundParameters.ContainsKey('ThrottleDelaySeconds')) {
        $ThrottleDelaySeconds = 0
    }
}
else {
    # Default Overpass API public endpoint mirrors with automatic fallback
    $OverpassMirrors = @(
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter"
    )
}

# Reference US State Bounding Boxes for -States filtering: [South, West, North, East]
$StateBounds = @{
    "AL" = @{ Name = "Alabama";        Box = @(30.13, -88.47, 35.01, -84.89) }
    "AZ" = @{ Name = "Arizona";        Box = @(31.33, -114.82, 37.00, -109.04) }
    "AR" = @{ Name = "Arkansas";       Box = @(33.00, -94.62, 36.50, -89.64) }
    "CA" = @{ Name = "California";     Box = @(32.53, -124.41, 42.01, -114.13) }
    "CO" = @{ Name = "Colorado";       Box = @(36.99, -109.06, 41.01, -102.04) }
    "CT" = @{ Name = "Connecticut";    Box = @(40.98, -73.73, 42.05, -71.79) }
    "DE" = @{ Name = "Delaware";       Box = @(38.45, -75.79, 39.84, -75.05) }
    "DC" = @{ Name = "District of Columbia"; Box = @(38.79, -77.12, 38.99, -76.91) }
    "FL" = @{ Name = "Florida";        Box = @(24.52, -87.63, 31.00, -80.03) }
    "GA" = @{ Name = "Georgia";        Box = @(30.36, -85.61, 35.00, -80.84) }
    "ID" = @{ Name = "Idaho";          Box = @(41.99, -117.24, 49.00, -111.04) }
    "IL" = @{ Name = "Illinois";       Box = @(36.97, -91.51, 42.51, -87.50) }
    "IN" = @{ Name = "Indiana";        Box = @(37.77, -88.10, 41.76, -84.78) }
    "IA" = @{ Name = "Iowa";           Box = @(40.38, -96.64, 43.50, -90.14) }
    "KS" = @{ Name = "Kansas";         Box = @(36.99, -102.05, 40.00, -94.59) }
    "KY" = @{ Name = "Kentucky";       Box = @(36.50, -89.57, 39.15, -81.96) }
    "LA" = @{ Name = "Louisiana";      Box = @(28.93, -94.04, 33.02, -88.82) }
    "ME" = @{ Name = "Maine";          Box = @(43.06, -71.08, 47.46, -66.95) }
    "MD" = @{ Name = "Maryland";       Box = @(37.89, -79.49, 39.72, -75.05) }
    "MA" = @{ Name = "Massachusetts";  Box = @(41.24, -73.51, 42.89, -69.93) }
    "MI" = @{ Name = "Michigan";       Box = @(41.69, -90.42, 48.31, -82.12) }
    "MN" = @{ Name = "Minnesota";      Box = @(43.50, -97.24, 49.38, -89.49) }
    "MS" = @{ Name = "Mississippi";    Box = @(30.17, -91.66, 35.00, -88.10) }
    "MO" = @{ Name = "Missouri";       Box = @(35.99, -95.77, 40.61, -89.10) }
    "MT" = @{ Name = "Montana";        Box = @(44.36, -116.05, 49.00, -104.04) }
    "NE" = @{ Name = "Nebraska";       Box = @(40.00, -104.05, 43.00, -95.31) }
    "NV" = @{ Name = "Nevada";         Box = @(35.00, -120.01, 42.00, -114.04) }
    "NH" = @{ Name = "New Hampshire";  Box = @(42.70, -72.56, 45.31, -70.70) }
    "NJ" = @{ Name = "New Jersey";     Box = @(38.93, -75.56, 41.36, -73.89) }
    "NM" = @{ Name = "New Mexico";     Box = @(31.33, -109.05, 37.00, -103.00) }
    "NY" = @{ Name = "New York";       Box = @(40.50, -79.76, 45.02, -71.86) }
    "NC" = @{ Name = "North Carolina"; Box = @(33.84, -84.32, 36.59, -75.46) }
    "ND" = @{ Name = "North Dakota";   Box = @(45.93, -104.05, 49.00, -96.55) }
    "OH" = @{ Name = "Ohio";           Box = @(38.40, -84.82, 42.33, -80.52) }
    "OK" = @{ Name = "Oklahoma";       Box = @(33.62, -103.00, 37.00, -94.43) }
    "OR" = @{ Name = "Oregon";         Box = @(41.99, -124.57, 46.29, -116.46) }
    "PA" = @{ Name = "Pennsylvania";   Box = @(39.72, -80.52, 42.27, -74.69) }
    "RI" = @{ Name = "Rhode Island";   Box = @(41.15, -71.86, 42.02, -71.12) }
    "SC" = @{ Name = "South Carolina"; Box = @(32.05, -83.35, 35.22, -78.54) }
    "SD" = @{ Name = "South Dakota";   Box = @(42.48, -104.06, 45.94, -96.44) }
    "TN" = @{ Name = "Tennessee";      Box = @(34.98, -90.31, 36.68, -81.65) }
    "TX" = @{ Name = "Texas";          Box = @(25.84, -106.65, 36.50, -93.51) }
    "UT" = @{ Name = "Utah";           Box = @(36.99, -114.05, 42.00, -109.04) }
    "VT" = @{ Name = "Vermont";        Box = @(42.73, -73.44, 45.02, -71.50) }
    "VA" = @{ Name = "Virginia";       Box = @(36.54, -83.68, 39.47, -75.24) }
    "WA" = @{ Name = "Washington";     Box = @(45.54, -124.76, 49.00, -116.92) }
    "WV" = @{ Name = "West Virginia";  Box = @(37.20, -82.64, 40.64, -77.72) }
    "WI" = @{ Name = "Wisconsin";      Box = @(42.49, -92.89, 47.08, -86.81) }
    "WY" = @{ Name = "Wyoming";        Box = @(40.99, -111.06, 45.01, -104.05) }
}

function Resolve-TileBounds([string]$tileKey) {
    $clean = [System.IO.Path]::GetFileNameWithoutExtension($tileKey).ToUpperInvariant().Trim()
    if ($clean.Length -ne 7) { return $null }

    $ns = $clean[0]
    $ew = $clean[3]
    if (($ns -ne 'N' -and $ns -ne 'S') -or ($ew -ne 'E' -and $ew -ne 'W')) { return $null }

    [int]$latVal = 0
    [int]$lonVal = 0
    if (![int]::TryParse($clean.Substring(1, 2), [ref]$latVal) -or
        ![int]::TryParse($clean.Substring(4, 3), [ref]$lonVal)) {
        return $null
    }

    $south = if ($ns -eq 'S') { -$latVal } else { $latVal }
    $west  = if ($ew -eq 'W') { -$lonVal } else { $lonVal }

    return [PSCustomObject]@{
        TileKey = $clean
        South   = [double]$south
        North   = [double]($south + 1.0)
        West    = [double]$west
        East    = [double]($west + 1.0)
    }
}

function Test-TileIntersectsBox([PSCustomObject]$bounds, [double[]]$box) {
    # Box format: South, West, North, East
    $bSouth = $box[0]; $bWest = $box[1]; $bNorth = $box[2]; $bEast = $box[3]
    return !($bounds.North -le $bSouth -or $bounds.South -ge $bNorth -or
             $bounds.East  -le $bWest  -or $bounds.West  -ge $bEast)
}

function Get-ElevationFromHgtStream([string]$hgtPath, [PSCustomObject]$bounds, [double]$lat, [double]$lon) {
    try {
        if (![System.IO.File]::Exists($hgtPath)) { return $null }
        $fileInfo = [System.IO.FileInfo]::new($hgtPath)
        $len = $fileInfo.Length

        # Standard SRTM grid dimensions: 1-arcsec (3601x3601 = 25,934,402 bytes) or 3-arcsec (1201x1201 = 2,884,802 bytes)
        $gridSize = if ($len -ge 25000000) { 3601 } elseif ($len -ge 2800000) { 1201 } else { return $null }

        $row = [int][Math]::Round(($bounds.North - $lat) * ($gridSize - 1))
        $col = [int][Math]::Round(($lon - $bounds.West) * ($gridSize - 1))

        if ($row -lt 0) { $row = 0 } elseif ($row -ge $gridSize) { $row = $gridSize - 1 }
        if ($col -lt 0) { $col = 0 } elseif ($col -ge $gridSize) { $col = $gridSize - 1 }

        $offset = [long]($row * $gridSize + $col) * 2
        if ($offset + 2 -gt $len) { return $null }

        $fs = [System.IO.File]::OpenRead($hgtPath)
        try {
            $fs.Seek($offset, [System.IO.SeekOrigin]::Begin) | Out-Null
            $b0 = $fs.ReadByte()
            $b1 = $fs.ReadByte()
            if ($b0 -lt 0 -or $b1 -lt 0) { return $null }

            # Big-endian signed 16-bit integer
            $rawVal = ($b0 -shl 8) -bor $b1
            if ($rawVal -ge 32768) { $rawVal -= 65536 }

            # SRTM void values are -32768
            if ($rawVal -le -500) { return $null }
            return [double]$rawVal
        }
        finally {
            $fs.Dispose()
        }
    }
    catch {
        return $null
    }
}

function Invoke-OverpassQueryWithFallback([string]$query, [string[]]$mirrors) {
    $lastError = $null
    foreach ($mirror in $mirrors) {
        for ($attempt = 1; $attempt -le 2; $attempt++) {
            try {
                $headers = @{ "User-Agent" = "PointedHorizons-DemPeakExtractor/1.0 (https://github.com/)" }
                $resp = Invoke-RestMethod -Uri $mirror `
                                          -Method Post `
                                          -Body $query `
                                          -Headers $headers `
                                          -ContentType "application/x-www-form-urlencoded; charset=utf-8" `
                                          -TimeoutSec 40

                # In PowerShell, $collection -ne $null acts as a filter on empty arrays.
                # Use $null -ne on the left to correctly handle valid responses with 0 peaks.
                if ($null -ne $resp -and ($null -ne $resp.elements -or $resp.PSObject.Properties.Match('elements').Count -gt 0)) {
                    return $resp
                }

                if ($resp.remark) {
                    $lastError = [Exception]::new($resp.remark)
                }
            }
            catch {
                $lastError = $_
                Write-Verbose "[$mirror] Attempt $attempt failed: $($_.Exception.Message). Backing off..."
                Start-Sleep -Seconds 1
            }
        }
    }
    $errMsg = if ($lastError.Exception.Message) { $lastError.Exception.Message } elseif ($lastError) { "$lastError" } else { "No response from server" }
    throw "All Overpass API mirrors failed. Last error: $errMsg"
}

# Ensure OutputDir exists
if (![System.IO.Directory]::Exists($OutputDir)) {
    [System.IO.Directory]::CreateDirectory($OutputDir) | Out-Null
}

# Discover candidate tiles
$discoveredTiles = @{}

if ($DemDir -and [System.IO.Directory]::Exists($DemDir)) {
    $foundFiles = Get-ChildItem -Path $DemDir -File | Where-Object {
        $_.Extension -ieq ".hgt" -or $_.Extension -ieq ".zip"
    }

    foreach ($f in $foundFiles) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
        if ($baseName.EndsWith(".hgt", [System.StringComparison]::OrdinalIgnoreCase)) {
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($baseName)
        }
        $tb = Resolve-TileBounds $baseName
        if ($tb) {
            $discoveredTiles[$tb.TileKey] = [PSCustomObject]@{
                TileKey  = $tb.TileKey
                Bounds   = $tb
                FilePath = $f.FullName
            }
        }
    }
}

# If specific tiles were requested explicitly, add them even if not yet on disk
if ($Tiles) {
    $explicitMap = @{}
    $tileTokens = @()
    foreach ($entry in $Tiles) {
        if ($entry -match ',') {
            $tileTokens += ($entry -split ',') | ForEach-Object { $_.Trim(' "''') }
        } else {
            $tileTokens += $entry.Trim(' "''')
        }
    }

    foreach ($t in $tileTokens) {
        $clean = $t.ToUpperInvariant().Trim()
        $tb = Resolve-TileBounds $clean
        if ($tb) {
            $hgtPath = Join-Path $DemDir "$clean.hgt"
            $explicitMap[$clean] = [PSCustomObject]@{
                TileKey  = $clean
                Bounds   = $tb
                FilePath = if ([System.IO.File]::Exists($hgtPath)) { $hgtPath } else { $null }
            }
        }
    }
    $discoveredTiles = $explicitMap
}

# Filter by selected states if -States specified
if ($States) {
    $stateTokens = @()
    foreach ($entry in $States) {
        if ($entry -match ',') {
            $stateTokens += ($entry -split ',') | ForEach-Object { $_.Trim(' "''') }
        } else {
            $stateTokens += $entry.Trim(' "''')
        }
    }

    $stateBoxes = @()
    foreach ($st in $stateTokens) {
        $stUpper = $st.ToUpperInvariant().Trim()
        $matched = $StateBounds[$stUpper]
        if (!$matched) {
            # Check by full name
            $matched = $StateBounds.Values | Where-Object { $_.Name -ieq $st } | Select-Object -First 1
        }
        if ($matched) {
            $stateBoxes += ,$matched.Box
        } else {
            Write-Warning "Unrecognized state: '$st'"
        }
    }

    if ($stateBoxes.Count -gt 0) {
        $filtered = @{}
        foreach ($key in $discoveredTiles.Keys) {
            $item = $discoveredTiles[$key]
            $inState = $false
            foreach ($box in $stateBoxes) {
                if (Test-TileIntersectsBox $item.Bounds $box) {
                    $inState = $true
                    break
                }
            }
            if ($inState) {
                $filtered[$key] = $item
            }
        }
        $discoveredTiles = $filtered
    }
}

$sortedTiles = $discoveredTiles.Values | Sort-Object -Property TileKey

if ($List) {
    Write-Host "`nDiscovered DEM Tiles ($($sortedTiles.Count) total):" -ForegroundColor Cyan
    Write-Host ("{0,-10} {1,-34} {2,-12} {3}" -f "TileKey", "Bounding Box (S, W, N, E)", "DEM File", "Peaks JSON Status") -ForegroundColor Yellow
    Write-Host ("-" * 80)
    foreach ($item in $sortedTiles) {
        $b = $item.Bounds
        $bboxStr = "{0:F1}, {1:F1} to {2:F1}, {3:F1}" -f $b.South, $b.West, $b.North, $b.East
        $jsonPath = Join-Path $OutputDir "$($item.TileKey).peaks.json"
        $status = if ([System.IO.File]::Exists($jsonPath)) {
            $cnt = (Get-Content $jsonPath -Raw | ConvertFrom-Json).peakCount
            "Present ($cnt peaks)"
        } else {
            "Missing"
        }
        $hasDem = if ($item.FilePath) { "Yes" } else { "No" }
        Write-Host ("{0,-10} {1,-34} {2,-12} {3}" -f $item.TileKey, $bboxStr, $hasDem, $status)
    }
    return
}

if (@($sortedTiles).Count -eq 0) {
    Write-Warning "No DEM tiles found to process. Check -DemDir or provide -Tiles."
    return
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " PointedHorizons DEM Peak & Highpoint Extractor" -ForegroundColor Cyan
Write-Host " Tiles to process : $(@($sortedTiles).Count)" -ForegroundColor White
Write-Host " Output Directory : $OutputDir" -ForegroundColor White
Write-Host " Overpass Server  : $(if ($OverpassServer) { $OverpassMirrors[0] } else { 'Public mirrors (with automatic fallback)' })" -ForegroundColor White
Write-Host " Throttle Delay   : $ThrottleDelaySeconds second(s)" -ForegroundColor White
Write-Host " Min Elevation    : $MinElevationM meters" -ForegroundColor White
Write-Host " Overwrite        : $Overwrite" -ForegroundColor White
Write-Host " DryRun           : $DryRun" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

$successCount = 0
$skippedCount = 0
$errorCount = 0

foreach ($item in $sortedTiles) {
    $tileKey = $item.TileKey
    $bounds = $item.Bounds
    $outputFile = Join-Path $OutputDir "$tileKey.peaks.json"

    if (!$Overwrite -and [System.IO.File]::Exists($outputFile)) {
        Write-Host "[$tileKey] peaks.json already exists. Skipping (use -Overwrite to replace)." -ForegroundColor DarkGray
        $skippedCount++
        continue
    }

    if ($DryRun) {
        Write-Host "[DryRun] Would query $($tileKey): South=$($bounds.South), West=$($bounds.West), North=$($bounds.North), East=$($bounds.East)" -ForegroundColor Cyan
        continue
    }

    Write-Host "[$tileKey] Fetching peaks [S:$($bounds.South) W:$($bounds.West) N:$($bounds.North) E:$($bounds.East)]..." -ForegroundColor Yellow

    # Overpass QL query: bounding box format is (south, west, north, east)
    $query = @"
[out:json][timeout:35];
(
  node["natural"="peak"]($($bounds.South),$($bounds.West),$($bounds.North),$($bounds.East));
  node["natural"="volcano"]($($bounds.South),$($bounds.West),$($bounds.North),$($bounds.East));
);
out body;
"@

    try {
        $result = Invoke-OverpassQueryWithFallback $query $OverpassMirrors
        $rawPeaks = [System.Collections.Generic.List[PSCustomObject]]::new()

        $elements = @($result.elements)
        foreach ($el in $elements) {
                $name = $el.tags.name
                if ([string]::IsNullOrWhiteSpace($name)) { continue }

                $pLat = [double]$el.lat
                $pLon = [double]$el.lon
                $pEle = 0.0

                # 1. Parse 'ele' tag from OSM if provided
                if ($el.tags.ele) {
                    $rawEle = $el.tags.ele.ToString().Trim().Replace(",", "")
                    if ($rawEle -match '^(-?\d+(\.\d+)?)') {
                        $pEle = [double]$Matches[1]
                        if ($rawEle.ToLower().Contains("ft") -or $rawEle.ToLower().Contains("'")) {
                            $pEle = $pEle * 0.3048 # convert feet to meters
                        }
                    }
                }

                # 2. If 'ele' missing or 0, sample directly from the local .hgt elevation raster
                if ($pEle -le 0.0 -and $item.FilePath -and [System.IO.File]::Exists($item.FilePath)) {
                    $hgtSample = Get-ElevationFromHgtStream $item.FilePath $bounds $pLat $pLon
                    if ($hgtSample -ne $null -and $hgtSample -gt 0) {
                        $pEle = $hgtSample
                    }
                }

                # Filter by minimum elevation if specified
                if ($pEle -lt $MinElevationM) { continue }

                $rawPeaks.Add([PSCustomObject]@{
                    name = $name.Trim()
                    lat  = [Math]::Round($pLat, 5)
                    lon  = [Math]::Round($pLon, 5)
                    ele  = [Math]::Round($pEle, 1)
                })
            }

        # Deduplicate peaks with identical names within close proximity (approx 1 km)
        $uniquePeaks = [System.Collections.Generic.List[PSCustomObject]]::new()
        foreach ($group in ($rawPeaks | Group-Object -Property name)) {
            $sortedGroup = $group.Group | Sort-Object -Property ele -Descending
            foreach ($cand in $sortedGroup) {
                $existsNear = $false
                foreach ($existing in $uniquePeaks) {
                    if ($existing.name -eq $cand.name) {
                        $dLat = [Math]::Abs($existing.lat - $cand.lat)
                        $dLon = [Math]::Abs($existing.lon - $cand.lon)
                        if ($dLat -lt 0.015 -and $dLon -lt 0.015) {
                            $existsNear = $true
                            break
                        }
                    }
                }
                if (!$existsNear) {
                    $uniquePeaks.Add($cand)
                }
            }
        }

        $finalPeaks = $uniquePeaks | Sort-Object -Property ele -Descending

        $payload = [PSCustomObject]@{
            tile         = $tileKey
            bounds       = [PSCustomObject]@{
                south = $bounds.South
                north = $bounds.North
                west  = $bounds.West
                east  = $bounds.East
            }
            extractedUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            peakCount    = @($finalPeaks).Count
            peaks        = @($finalPeaks)
        }

        $jsonStr = $payload | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($outputFile, $jsonStr, [System.Text.Encoding]::UTF8)

        Write-Host "[$tileKey] Extracted $($finalPeaks.Count) summits -> $([System.IO.Path]::GetFileName($outputFile))" -ForegroundColor Green
        $successCount++

        # Polite delay between Overpass queries
        if ($ThrottleDelaySeconds -gt 0) {
            Start-Sleep -Seconds $ThrottleDelaySeconds
        }
    }
    catch {
        Write-Warning "[$tileKey] Error querying peaks: $_"
        $errorCount++
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host " Extraction Finished" -ForegroundColor Cyan
Write-Host " Success : $successCount" -ForegroundColor Green
Write-Host " Skipped : $skippedCount" -ForegroundColor DarkGray
Write-Host " Errors  : $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "DarkGray" })
Write-Host "==========================================================" -ForegroundColor Cyan
