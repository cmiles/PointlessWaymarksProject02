param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$program,
    [switch]$IncludePhotoPreviewGui,
    [switch]$IncludePwTrackTrimmer
)

$baseName = "PointlessWaymarks.$program"

$ErrorActionPreference = "Stop"

$fossilCheckout = fossil info | Select-String -Pattern "checkout:" | ForEach-Object { $_.Line.Substring(9, $_.Line.Length - 9).Trim().Split(' ')[0] } | Select-Object -First 1

$fossilStatusBrief = fossil status -b
if ($fossilStatusBrief -match "dirty") {
    $fossilStatusBrief = "Uncommitted_Changes"
} else {
    $fossilStatusBrief = ""
}

Write-Host "Fossil Version: $fossilCheckout"
Write-Host "Fossil Status Brief: $fossilStatusBrief"

$fossilId = "$fossilCheckout`_$fossilStatusBrief"
Write-Host "Fossil Id: $fossilId"

dotnet clean .\PointlessWaymarks.slnx -property:Configuration=Release -property:Platform=x64 -verbosity:minimal

dotnet restore .\PointlessWaymarks.slnx -r win-x64 -verbosity:minimal

$vsWhere = "{0}\Microsoft Visual Studio\Installer\vswhere.exe" -f ${env:ProgramFiles(x86)}

$msBuild = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe

& $msBuild .\PointlessWaymarks.slnx -property:Configuration=Release -property:Platform=x64 -verbosity:minimal

if ($lastexitcode -ne 0) { throw ("Exec: " + $errorMessage) }

$publishPath = "M:\PointlessWaymarksPublications\$baseName"
if (!(Test-Path -PathType Container $publishPath)) { New-Item -ItemType Directory -Path $publishPath }

Remove-Item -Path $publishPath\* -Recurse

& $msBuild .\$baseName\$baseName.csproj -t:publish -p:PublishProfile=.\$baseName\Properties\PublishProfile\FolderProfile.pubxml -verbosity:minimal

if ($lastexitcode -ne 0) { throw ("Exec: " + $errorMessage) }

if ($IncludePhotoPreviewGui) {
    $photoPreviewPublishPath = "M:\PointlessWaymarksPublications\PointlessWaymarks.PhotoPreviewGui"
    if (!(Test-Path -PathType Container $photoPreviewPublishPath)) {
        New-Item -ItemType Directory -Path $photoPreviewPublishPath
    }

    Remove-Item -Path "$photoPreviewPublishPath\*" -Recurse -Force

    & $msBuild .\PointlessWaymarksTools\PointlessWaymarks.PhotoPreviewGui\PointlessWaymarks.PhotoPreviewGui.csproj -t:publish -p:PublishProfile=.\PointlessWaymarksTools\PointlessWaymarks.PhotoPreviewGui\Properties\PublishProfiles\FolderProfile.pubxml -verbosity:minimal

    if ($lastexitcode -ne 0) { throw ("Exec: " + $errorMessage) }

    $photoPreviewDestination = "$publishPath\PointlessWaymarks.PhotoPreviewGui"
    if (!(Test-Path -PathType Container $photoPreviewDestination)) {
        New-Item -ItemType Directory -Path $photoPreviewDestination
    }
    Copy-Item -Path "$photoPreviewPublishPath\*" -Destination $photoPreviewDestination -Recurse -Force
}

if ($IncludePwTrackTrimmer) {
	cd .\Apps
    & .\Publish-PwTrackTrimmerWin.ps1
    if ($lastexitcode -ne 0) { throw ("Exec: Publishing PwTrackTrimmerWin failed") }

    $pwTrackSource = "M:\PointlessWaymarksPublications\PwTrackTrimmerWin"
    $pwTrackDestination = "$publishPath\PwTrackTrimmerWin"

    if (!(Test-Path -PathType Container $pwTrackDestination)) {
        New-Item -ItemType Directory -Path $pwTrackDestination -Force
    }

    Copy-Item -Path "$pwTrackSource\*" -Destination $pwTrackDestination -Recurse -Force
	
	cd ..
}

$exePath = "M:\PointlessWaymarksPublications\$baseName\$baseName.exe"
$fileVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)

# Calculate hour and minute from FilePrivatePart
$versionHour = [math]::Floor($fileVersionInfo.FilePrivatePart / 100)
$versionMinute = $fileVersionInfo.FilePrivatePart - ($versionHour * 100)
$versionDate = New-Object DateTime($fileVersionInfo.FileMajorPart, $fileVersionInfo.FileMinorPart, $fileVersionInfo.FileBuildPart, $hour, $minute, 0)
$publishVersion = "{0}-{1}-{2}-{3}-{4}" -f $versionDate.ToString("yyyy"), $versionDate.ToString("MM"), $versionDate.ToString("dd"), $versionHour.ToString("00"), $versionMinute.ToString("00")

Write-Host "Publish Version: $publishVersion"

& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ".\Publish-InnoSetupInstaller-$program.iss" /DVersion=$publishVersion /DScmCommit=$fossilId

if ($lastexitcode -ne 0) { throw ("Exec: " + $errorMessage) }