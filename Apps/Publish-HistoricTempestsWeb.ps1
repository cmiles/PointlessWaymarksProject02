# Define target directories
$downloaderDir = "M:\PointlessWaymarksPublications\HistoricTempestsDataDownloader"

Write-Host "=== Publishing HistoricTempests Data Downloader ===" -ForegroundColor Cyan

# 1. Clear the Downloader directory
if (Test-Path $downloaderDir) {
    Write-Host "Cleaning Downloader directory..."
    Remove-Item -Path "$downloaderDir\*" -Recurse -Force -ErrorAction SilentlyContinue
}

# 2. Publish the Downloader App (Self-contained, Single-file)
Write-Host "Building and publishing Downloader project (Single-file exe)..."
dotnet publish HistoricTempests\HistoricTempestsDataDownloader\HistoricTempestsDataDownloader.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o $downloaderDir




# Define target directories
$browserDir = "M:\PointlessWaymarksPublications\HistoricTempests"

Write-Host "`n=== Publishing HistoricTempests Browser ===" -ForegroundColor Cyan

# Clean the Browser directory
if (Test-Path $browserDir) {
    Write-Host "Cleaning publish directory..."
    Get-ChildItem -Path $browserDir -Force -ErrorAction SilentlyContinue | 
        Remove-Item -Recurse -Force
}

# Publish the Browser App
Write-Host "Building and publishing HistoricTempests Browser project..."
dotnet publish HistoricTempests\HistoricTempests.Browser\HistoricTempests.Browser.csproj -c Release -o $browserDir

Write-Host "`nAll publishing steps completed successfully!" -ForegroundColor Green
