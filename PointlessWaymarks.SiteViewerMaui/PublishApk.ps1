#Publish for sideloading - apk will end up in \bin\Release\net10.0-android\publish
#Currently using the signed version personally

# Run the publish command
dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk

# Generate the datestamp and set destination
$dateStamp = Get-Date -Format "yyyy-MM-dd-HH-mm"
$outputFolder = "M:\PointlessWaymarksPublications"

# Create destination directory if it doesn't exist
if (-not (Test-Path $outputFolder)) { New-Item -ItemType Directory -Path $outputFolder }

# Find and copy the signed APK
Get-ChildItem -Path "bin\Release\net10.0-android" -Filter "*-Signed.apk" | ForEach-Object {
    $newName = "$($_.BaseName)--$dateStamp$($_.Extension)"
    Copy-Item $_.FullName -Destination (Join-Path $outputFolder $newName) -Force
    Write-Host "Copied: $newName -> $outputFolder" -ForegroundColor Green
}