# 1. Define your paths
$projectPath = ".\PwTrackTrimmer\PwTrackTrimmer.Photino\PwTrackTrimmer.Photino.csproj"
$targetDir = "M:\PointlessWaymarksPublications\PwTrackTrimmerWin"

# 2. Clear the target directory if it exists, or create it if it doesn't
if (Test-Path $targetDir) {
    Write-Host "Clearing existing files in $targetDir..." -ForegroundColor Yellow
    Remove-Item -Path "$targetDir\*" -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "Creating target directory $targetDir..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

# 3. Run the publish command directly to the output directory
Write-Host "Publishing OpenSilver Photino app..." -ForegroundColor Cyan

dotnet publish $projectPath -c Release -r win-x64 --self-contained true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $targetDir

Write-Host "Success! App published to: $targetDir" -ForegroundColor Green