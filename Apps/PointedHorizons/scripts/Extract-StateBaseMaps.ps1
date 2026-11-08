<#
.SYNOPSIS
    Extracts offline vector PMTiles basemaps for any contiguous US State or all states using Protomaps and Docker.

.DESCRIPTION
    Automates extracting state-level PMTiles vector basemaps from daily planet builds (https://build.protomaps.com).
    Uses HTTP range requests through protomaps/go-pmtiles inside Docker so only the data within each state's
    bounding box is downloaded.

.PARAMETER States
    One or more state names or 2-letter postal codes (e.g. -States "Arizona", -States "AZ","UT","CO","NM").
    Use -States "All" to process all 48 contiguous US states + DC.

.PARAMETER OutputDir
    Directory where .pmtiles files will be saved. Defaults to the current directory ($PWD).

.PARAMETER Date
    Protomaps build date in yyyyMMdd format. Defaults to today, automatically falling back to yesterday
    if today's build is not yet published.

.PARAMETER SourceUrl
    Optional custom PMTiles source URL. Overrides the -Date parameter.

.PARAMETER MaxZoom
    Optional maximum zoom level limit (e.g. 14 or 15). By default extracts all available zooms.

.PARAMETER Threads
    Number of parallel download threads. Default is 4.

.PARAMETER Overwrite
    Force re-downloading and overwriting existing .pmtiles files. Otherwise, existing files are skipped.

.PARAMETER DryRun
    Queries the remote archive and calculates tile count / transfer size without downloading.

.PARAMETER Combined
    When specifying multiple states, extracts a single combined bounding-box PMTiles archive rather than separate files.

.PARAMETER List
    Prints all supported states, postal codes, and bounding boxes, then exits.

.EXAMPLE
    .\Extract-StateBaseMaps.ps1 -States "Arizona"
    Extracts arizona_basemap.pmtiles into the current directory.

.EXAMPLE
    .\Extract-StateBaseMaps.ps1 -States "AZ", "UT", "CO", "NM"
    Extracts each of the Four Corners states as individual files.

.EXAMPLE
    .\Extract-StateBaseMaps.ps1 -States "AZ", "UT" -Combined
    Extracts a single combined PMTiles file (az_ut_basemap.pmtiles) covering the union of Arizona and Utah.

.EXAMPLE
    .\Extract-StateBaseMaps.ps1 -States "All" -OutputDir "M:\PointlessWaymarksPublications\pmtiles"
    Extracts all 48 contiguous US states + DC into the specified directory.

.EXAMPLE
    .\Extract-StateBaseMaps.ps1 -States "Washington" -DryRun
    Shows estimated tile count and download size without downloading.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$States,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = $PWD,

    [Parameter(Mandatory = $false)]
    [string]$Date,

    [Parameter(Mandatory = $false)]
    [string]$SourceUrl,

    [Parameter(Mandatory = $false)]
    [int]$MaxZoom = -1,

    [Parameter(Mandatory = $false)]
    [int]$Threads = 4,

    [Parameter(Mandatory = $false)]
    [switch]$Overwrite,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$Combined,

    [Parameter(Mandatory = $false)]
    [switch]$List
)

# -----------------------------------------------------------------------------
# 1. State Bounding Box Lookup Table (48 Contiguous States + DC + AK/HI)
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
    Write-Host "`n=== Supported Contiguous US States (48 States + DC) ===" -ForegroundColor Cyan
    $tableData = foreach ($key in $StateDatabase.Keys) {
        $entry = $StateDatabase[$key]
        if ($entry.Contiguous) {
            [PSCustomObject]@{
                Code       = $key
                State      = $entry.Name
                BBox       = $entry.BBox
            }
        }
    }
    $tableData | Format-Table -AutoSize
    Write-Host "Non-contiguous (Available by code): AK (Alaska), HI (Hawaii)`n" -ForegroundColor DarkGray
    return
}

# -----------------------------------------------------------------------------
# 2. Check Docker Availability
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

# -----------------------------------------------------------------------------
# 3. Resolve Output Directory
# -----------------------------------------------------------------------------
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}
$absOutputDir = (Resolve-Path $OutputDir).Path

# -----------------------------------------------------------------------------
# 4. Resolve Protomaps Source Build URL
# -----------------------------------------------------------------------------
function Resolve-ProtomapsSourceUrl {
    param([string]$explicitUrl, [string]$explicitDate)

    if ($explicitUrl) {
        return $explicitUrl
    }

    if ($explicitDate) {
        return "https://build.protomaps.com/$explicitDate.pmtiles"
    }

    # Check if today's daily build is ready; fallback to yesterday if not yet published
    $todayStr = (Get-Date).ToString("yyyyMMdd")
    $todayUrl = "https://build.protomaps.com/$todayStr.pmtiles"

    try {
        $req = [System.Net.WebRequest]::Create($todayUrl)
        $req.Method = "HEAD"
        $req.Timeout = 4000
        $resp = $req.GetResponse()
        $resp.Close()
        return $todayUrl
    }
    catch {
        $yesterdayStr = (Get-Date).AddDays(-1).ToString("yyyyMMdd")
        return "https://build.protomaps.com/$yesterdayStr.pmtiles"
    }
}

$resolvedSourceUrl = Resolve-ProtomapsSourceUrl -explicitUrl $SourceUrl -explicitDate $Date
Write-Host "Source Build URL: $resolvedSourceUrl" -ForegroundColor Cyan
Write-Host "Output Directory: $absOutputDir" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# 5. Resolve Selected States
# -----------------------------------------------------------------------------
if (-not $States -or $States.Count -eq 0) {
    Write-Host "`nNo state specified. You can specify state names, postal codes, or 'All'." -ForegroundColor Yellow
    Write-Host "Example: .\Extract-StateBaseMaps.ps1 -States Arizona" -ForegroundColor Yellow
    Write-Host "Example: .\Extract-StateBaseMaps.ps1 -States AZ, UT, CO, NM" -ForegroundColor Yellow
    Write-Host "Example: .\Extract-StateBaseMaps.ps1 -States All" -ForegroundColor Yellow
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

        # Try match by 2-letter Code
        if ($StateDatabase.Contains($trimmed.ToUpperInvariant())) {
            $foundKey = $trimmed.ToUpperInvariant()
        }
        else {
            # Try match by Full Name (case-insensitive)
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
    Write-Host "Error: No valid states selected for extraction." -ForegroundColor Red
    return
}

# -----------------------------------------------------------------------------
# 6. Extraction Execution
# -----------------------------------------------------------------------------

# Helper function to sanitize name into snake_case filename
function Get-SnakeCaseName ([string]$str) {
    return ($str.ToLowerInvariant() -replace '[^a-z0-9]+', '_').Trim('_')
}

$results = [System.Collections.Generic.List[PSCustomObject]]::new()
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

if ($Combined -and $selectedEntries.Count -gt 1) {
    # Combine bounding boxes into a single envelope
    $minLons = @(); $minLats = @(); $maxLons = @(); $maxLats = @()
    $nameParts = @()
    foreach ($item in $selectedEntries) {
        $pts = $item.BBox.Split(',') | ForEach-Object { [double]$_ }
        $minLons += $pts[0]; $minLats += $pts[1]; $maxLons += $pts[2]; $maxLats += $pts[3]
        $nameParts += $item.Code.ToLowerInvariant()
    }

    $combinedBbox = "{0},{1},{2},{3}" -f `
        (($minLons | Measure-Object -Minimum).Minimum), `
        (($minLats | Measure-Object -Minimum).Minimum), `
        (($maxLons | Measure-Object -Maximum).Maximum), `
        (($maxLats | Measure-Object -Maximum).Maximum)

    $combinedName = ($nameParts -join "_") + "_basemap.pmtiles"
    $destFile = Join-Path $absOutputDir $combinedName

    Write-Host "`n=== Extracting Combined Region: $($selectedEntries.Name -join ', ') ===" -ForegroundColor Cyan
    Write-Host "Bounding Box: $combinedBbox"
    Write-Host "Target File : $combinedName"

    $dockerArgs = @(
        "run", "--rm",
        "-v", "${absOutputDir}:/data",
        "protomaps/go-pmtiles", "extract",
        $resolvedSourceUrl,
        "/data/$combinedName",
        "--bbox=$combinedBbox",
        "--download-threads=$Threads"
    )
    if ($MaxZoom -gt 0) { $dockerArgs += "--maxzoom=$MaxZoom" }
    if ($DryRun) { $dockerArgs += "--dry-run" }

    if ((Test-Path $destFile) -and -not $Overwrite -and -not $DryRun) {
        Write-Host "Skipping: $combinedName already exists (use -Overwrite to replace)." -ForegroundColor Yellow
        $results.Add([PSCustomObject]@{ Target = $combinedName; Status = "Skipped (Exists)"; SizeMB = [math]::Round((Get-Item $destFile).Length / 1MB, 1) })
    }
    else {
        & docker @dockerArgs
        if ($LASTEXITCODE -eq 0) {
            $size = if (Test-Path $destFile) { [math]::Round((Get-Item $destFile).Length / 1MB, 1) } else { 0 }
            $results.Add([PSCustomObject]@{ Target = $combinedName; Status = "Success"; SizeMB = $size })
        }
        else {
            $results.Add([PSCustomObject]@{ Target = $combinedName; Status = "Failed"; SizeMB = 0 })
        }
    }
}
else {
    # Extract each state individually
    $totalCount = $selectedEntries.Count
    $idx = 0

    foreach ($item in $selectedEntries) {
        $idx++
        $snakeName = (Get-SnakeCaseName $item.Name) + "_basemap.pmtiles"
        $destFile = Join-Path $absOutputDir $snakeName

        Write-Host "`n[$idx/$totalCount] Extracting $($item.Name) ($($item.Code))..." -ForegroundColor Cyan
        Write-Host "  Bounding Box: $($item.BBox)" -ForegroundColor DarkGray
        Write-Host "  Target File : $snakeName" -ForegroundColor DarkGray

        if ((Test-Path $destFile) -and -not $Overwrite -and -not $DryRun) {
            $existingMB = [math]::Round((Get-Item $destFile).Length / 1MB, 1)
            Write-Host "  -> Skipping: Already exists ($existingMB MB). Pass -Overwrite to re-download." -ForegroundColor Yellow
            $results.Add([PSCustomObject]@{ State = $item.Name; Code = $item.Code; File = $snakeName; Status = "Skipped (Exists)"; SizeMB = $existingMB })
            continue
        }

        $dockerArgs = @(
            "run", "--rm",
            "-v", "${absOutputDir}:/data",
            "protomaps/go-pmtiles", "extract",
            $resolvedSourceUrl,
            "/data/$snakeName",
            "--bbox=$($item.BBox)",
            "--download-threads=$Threads"
        )
        if ($MaxZoom -gt 0) { $dockerArgs += "--maxzoom=$MaxZoom" }
        if ($DryRun) { $dockerArgs += "--dry-run" }

        & docker @dockerArgs

        if ($LASTEXITCODE -eq 0) {
            if ($DryRun) {
                Write-Host "  -> Dry-run completed." -ForegroundColor Green
                $results.Add([PSCustomObject]@{ State = $item.Name; Code = $item.Code; File = $snakeName; Status = "Dry-Run OK"; SizeMB = "-" })
            }
            elseif (Test-Path $destFile) {
                $mb = [math]::Round((Get-Item $destFile).Length / 1MB, 1)
                Write-Host "  -> Successfully extracted ($mb MB)." -ForegroundColor Green
                $results.Add([PSCustomObject]@{ State = $item.Name; Code = $item.Code; File = $snakeName; Status = "Created"; SizeMB = $mb })
            }
            else {
                $results.Add([PSCustomObject]@{ State = $item.Name; Code = $item.Code; File = $snakeName; Status = "Success"; SizeMB = "-" })
            }
        }
        else {
            Write-Host "  -> Error: Docker extraction failed for $($item.Name)." -ForegroundColor Red
            $results.Add([PSCustomObject]@{ State = $item.Name; Code = $item.Code; File = $snakeName; Status = "Failed"; SizeMB = 0 })
        }
    }
}

$stopwatch.Stop()
$elapsed = $stopwatch.Elapsed

# -----------------------------------------------------------------------------
# 7. Final Summary Table
# -----------------------------------------------------------------------------
Write-Host "`n=== Extraction Summary (Elapsed: $($elapsed.ToString('mm\:ss')) ) ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize
Write-Host "Files saved in: $absOutputDir`n" -ForegroundColor DarkGray
