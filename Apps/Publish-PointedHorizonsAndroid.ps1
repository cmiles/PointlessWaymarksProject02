param(
    [Parameter(Position = 0, Mandatory = $false, HelpMessage = "Password for the Android keystore and key")]
    [string]$Password,

    [Parameter(Position = 1, Mandatory = $false, HelpMessage = "Key alias in the keystore (optional, auto-detected if omitted)")]
    [string]$KeyAlias,

    [Parameter(Mandatory = $false, HelpMessage = "Password for the keystore store (defaults to Password)")]
    [string]$StorePassword
)

# 1. Prompt for password if not provided on the commandline
if ([string]::IsNullOrWhiteSpace($Password)) {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        $Password = Read-Host "Enter Android signing password" -MaskInput
    } else {
        $secure = Read-Host "Enter Android signing password" -AsSecureString
        $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    }
}

if ([string]::IsNullOrWhiteSpace($Password)) {
    throw "A signing password must be provided."
}

if ([string]::IsNullOrWhiteSpace($StorePassword)) {
    $StorePassword = $Password
}

# 2. Define paths and publication variables
$projectPath = ".\PointedHorizons\PointedHorizons.MauiHybrid\PointedHorizons.MauiHybrid.csproj"
$targetDir = "M:\PointlessWaymarksPublications"
$appId = "com.pointlesswaymarks.pointedhorizons"
$keyStorePath = "M:\GmhDataWell\AndroidKeys\shared.keystore"

# Auto-detect KeyAlias from keystore if not provided
if ([string]::IsNullOrWhiteSpace($KeyAlias)) {
    try {
        $keytoolOutput = & keytool -list -keystore $keyStorePath -storepass $Password 2>$null
        foreach ($line in $keytoolOutput) {
            if ($line -match '^([^,]+),\s+[A-Za-z]+ \d+,') {
                $KeyAlias = $Matches[1].Trim()
                Write-Host "Auto-detected key alias: $KeyAlias" -ForegroundColor DarkGray
                break
            }
        }
    } catch {
        # ignore and continue
    }
}

# Format timestamp: yyyy-MM-dd-HH-mm (e.g., com.pointlesswaymarks.pointedhorizons-Signed--2026-10-15-15-45.apk)
$timestamp = Get-Date -Format "yyyy-MM-dd-HH-mm"
$newName = "$appId-Signed--$timestamp.apk"
$destination = Join-Path $targetDir $newName

# Embed version marker file for runtime discovery
$rawDir = ".\PointedHorizons\PointedHorizons.MauiHybrid\Resources\Raw"
$wwwrootDir = ".\PointedHorizons\PointedHorizons.MauiHybrid\wwwroot"
if (Test-Path $rawDir) {
    Remove-Item -Path "$rawDir\PublishVersion--*.txt" -Force -ErrorAction SilentlyContinue
    Set-Content -Path "$rawDir\PublishVersion--$timestamp.txt" -Value $timestamp
}
if (Test-Path $wwwrootDir) {
    Remove-Item -Path "$wwwrootDir\PublishVersion--*.txt" -Force -ErrorAction SilentlyContinue
    Set-Content -Path "$wwwrootDir\PublishVersion--$timestamp.txt" -Value $timestamp
}

# 3. Build arguments and run dotnet publish
Write-Host "Publishing PointedHorizons MAUI Android app..." -ForegroundColor Cyan

$publishArgs = @(
    "publish",
    $projectPath,
    "-f", "net10.0-android",
    "-c", "Release",
    "-p:AndroidPackageFormat=apk",
    "-p:AndroidKeyStore=true",
    "-p:AndroidSigningKeyStore=$keyStorePath",
    "-p:AndroidSigningKeyPass=$Password",
    "-p:AndroidSigningStorePass=$StorePassword",
    "-p:ApplicationDisplayVersion=$timestamp",
    "-p:InformationalVersion=$timestamp"
)

if (-not [string]::IsNullOrWhiteSpace($KeyAlias)) {
    $publishArgs += "-p:AndroidSigningKeyAlias=$KeyAlias"
}

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) { 
    throw ("Publish failed with exit code: " + $LASTEXITCODE) 
}

# 4. Locate the generated signed APK in the bin folder
$publishedApk = Get-ChildItem -Path ".\PointedHorizons\PointedHorizons.MauiHybrid\bin\Release\*\publish\*-Signed.apk" -Recurse | Select-Object -First 1

if (-not $publishedApk) {
    $publishedApk = Get-ChildItem -Path ".\PointedHorizons\PointedHorizons.MauiHybrid\bin\Release\*-Signed.apk" -Recurse | Select-Object -First 1
}

# 5. Copy and rename the file to your target directory
if ($publishedApk) {
    if (-not (Test-Path $targetDir)) { 
        New-Item -ItemType Directory -Path $targetDir | Out-Null 
    }
    
    Copy-Item -Path $publishedApk.FullName -Destination $destination -Force
    Write-Host "Success! APK deployed to: $destination" -ForegroundColor Green
} else {
    throw "Build succeeded but could not find the output APK file in bin\Release!"
}

