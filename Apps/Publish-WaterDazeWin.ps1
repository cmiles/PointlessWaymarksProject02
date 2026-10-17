# 1. Define your paths
$projectPath = ".\WaterDaze\WaterDaze.Photino\WaterDaze.Photino.csproj"
$targetDir = "M:\PointlessWaymarksPublications\WaterDazeWin"
# Define the source path for the icon
$iconPath = ".\WaterDaze\WaterDaze.Photino\app.ico" 

# 2. Clear the target directory if it exists, or create it if it doesn't
if (Test-Path $targetDir) {
    Write-Host "Clearing existing files in $targetDir..." -ForegroundColor Yellow
    Remove-Item -Path "$targetDir\*" -Recurse -Force -ErrorAction SilentlyContinue
}
else {
    Write-Host "Creating target directory $targetDir..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

# 3. Run the publish command directly to the output directory
Write-Host "Publishing OpenSilver Photino app..." -ForegroundColor Cyan

dotnet publish $projectPath -c Release -r win-x64 --self-contained true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $targetDir

# 4. Copy the icon to the publish target directory
if (Test-Path $iconPath) {
    Write-Host "Copying app.ico to publish target..." -ForegroundColor Cyan
    Copy-Item -Path $iconPath -Destination $targetDir -Force
}
else {
    Write-Host "Warning: app.ico not found at $iconPath" -ForegroundColor Red
}

# 5. Compile the Inno Setup Installer
Write-Host "Compiling Inno Setup Installer..." -ForegroundColor Cyan
& "C:\Program Files\Inno Setup 7\ISCC.exe" ".\Publish-InnoSetupInstaller-WaterDazeWin.iss"

if ($lastexitcode -ne 0) { throw ("Exec failed with exit code: " + $lastexitcode) }
