# Define target directories
$browserDir = "M:\PointlessWaymarksPublications\PwTrackTrimmer"

Write-Host "`n=== Publishing PwTrackTrimmer Browser ===" -ForegroundColor Cyan

# 3. Clean the Browser directory
if (Test-Path $browserDir) {
    Write-Host "Cleaning publish directory..."
    Get-ChildItem -Path $browserDir -Force -ErrorAction SilentlyContinue | 
        Remove-Item -Recurse -Force
}

# 4. Publish the Browser App
Write-Host "Building and publishing PwTrackTrimmer Browser project..."
dotnet publish PwTrackTrimmer\PwTrackTrimmer.Browser\PwTrackTrimmer.Browser.csproj -c Release -o $browserDir

Write-Host "`nAll publishing steps completed successfully!" -ForegroundColor Green