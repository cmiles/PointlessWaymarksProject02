# Define target directory
$browserDir = "M:\PointlessWaymarksPublications\PointedHorizons"

Write-Host "`n=== Publishing PointedHorizons Browser ===" -ForegroundColor Cyan

# Clean the Browser directory
if (Test-Path $browserDir) {
    Write-Host "Cleaning publish directory..."
    Get-ChildItem -Path $browserDir -Force -ErrorAction SilentlyContinue | 
        Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $browserDir | Out-Null
}

# Publish the Browser App
Write-Host "Building and publishing PointedHorizons Browser project..."
dotnet publish .\PointedHorizons\PointedHorizons.Browser\PointedHorizons.Browser.csproj -c Release -o $browserDir

Write-Host "`nAll publishing steps completed successfully!" -ForegroundColor Green

