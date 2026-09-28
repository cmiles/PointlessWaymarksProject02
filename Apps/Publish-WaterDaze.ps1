# Define target directories
$downloaderDir = "M:\PointlessWaymarksPublications\WaterDazeDataDownloader"
$browserDir = "M:\PointlessWaymarksPublications\WaterDaze"

Write-Host "=== Publishing WaterDaze Data Downloader ===" -ForegroundColor Cyan

# 1. Clear the Downloader directory
if (Test-Path $downloaderDir) {
    Write-Host "Cleaning Downloader directory..."
    Remove-Item -Path "$downloaderDir\*" -Recurse -Force -ErrorAction SilentlyContinue
}

# 2. Publish the Downloader App (Self-contained, Single-file)
Write-Host "Building and publishing Downloader project (Single-file exe)..."
dotnet publish WaterDaze\WaterDazeDataDownloader\WaterDazeDataDownloader.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $downloaderDir


Write-Host "`n=== Publishing WaterDaze Browser ===" -ForegroundColor Cyan

# 3. Clean the Browser directory, skipping the wwwroot\UsgsData folder
if (Test-Path $browserDir) {
    Write-Host "Cleaning Browser directory (skipping wwwroot\UsgsData)..."
    
    # Step A: Delete everything in the root EXCEPT the wwwroot folder
    Get-ChildItem -Path $browserDir -ErrorAction SilentlyContinue | 
        Where-Object { $_.Name -ne 'wwwroot' } | 
        Remove-Item -Recurse -Force

    # Step B: Delete everything inside wwwroot EXCEPT the UsgsData folder
    $wwwrootPath = Join-Path $browserDir "wwwroot"
    if (Test-Path $wwwrootPath) {
        Get-ChildItem -Path $wwwrootPath -ErrorAction SilentlyContinue | 
            Where-Object { $_.Name -ne 'UsgsData' } | 
            Remove-Item -Recurse -Force
    }
}

# 4. Publish the Browser App
Write-Host "Building and publishing Browser project..."
dotnet publish WaterDaze\WaterDaze.Browser\WaterDaze.Browser.csproj -c Release -o $browserDir

Write-Host "`nAll publishing steps completed successfully!" -ForegroundColor Green