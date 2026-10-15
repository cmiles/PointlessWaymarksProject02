# Define target directories
$browserDir = "M:\PointlessWaymarksPublications\PwImageMetadataViewer"

Write-Host "`n=== Publishing PwImageMetadataViewer Browser ===" -ForegroundColor Cyan

# Clean the Browser directory
if (Test-Path $browserDir) {
    Write-Host "Cleaning publish directory..."
    Get-ChildItem -Path $browserDir -Force -ErrorAction SilentlyContinue | 
        Remove-Item -Recurse -Force
}

# Publish the Browser App
Write-Host "Building and publishing PwImageMetadataViewer Browser project..."
dotnet publish PwImageMetadataViewer\PwImageMetadataViewer.Browser\PwImageMetadataViewer.Browser.csproj -c Release -o $browserDir

Write-Host "`nAll publishing steps completed successfully!" -ForegroundColor Green
