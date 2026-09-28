dotnet publish .\PointlessWaymarks.SiteViewer\PointlessWaymarks.SiteViewerMaui\PointlessWaymarks.SiteViewerMaui.csproj -f net10.0-android -c Release -o M:\PointlessWaymarksPublications

# 1. Define your variables
$targetDir = "M:\PointlessWaymarksPublications"
$appId = "com.pointlesswaymarks.siteviewermaui"

# Note: Using 'HH' instead of 'hh' to get the 24-hour format (21-30) from your example
$timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm"
$newName = "$appId-Signed--$timestamp.apk"
$destination = Join-Path $targetDir $newName

# 2. Run the publish command
# -p:AndroidPackageFormat=apk forces an APK (MAUI defaults to .aab for the Play Store)
Write-Host "Publishing MAUI Android app..." -ForegroundColor Cyan
dotnet publish .\PointlessWaymarks.SiteViewer\PointlessWaymarks.SiteViewerMaui\PointlessWaymarks.SiteViewerMaui.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk

# 3. Locate the generated signed APK in the bin folder
# MAUI appends '-Signed.apk' automatically when a Release build is signed
$publishedApk = Get-ChildItem -Path ".\PointlessWaymarks.SiteViewer\PointlessWaymarks.SiteViewerMaui\bin\Release\*\publish\*-Signed.apk" -Recurse | Select-Object -First 1

# 4. Copy and rename the file to your target directory
if ($publishedApk) {
    if (-not (Test-Path $targetDir)) { 
        New-Item -ItemType Directory -Path $targetDir | Out-Null 
    }
    
    Copy-Item -Path $publishedApk.FullName -Destination $destination -Force
    Write-Host "Success! APK deployed to: $destination" -ForegroundColor Green
} else {
    Write-Host "Error: Could not find the '-Signed.apk' file. Check the build output above for errors." -ForegroundColor Red
}