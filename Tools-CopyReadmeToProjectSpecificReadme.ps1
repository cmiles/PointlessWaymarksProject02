$ErrorActionPreference = "Stop"

if (-not ("BuildTools\PointlessWaymarks.PublishReadmeHelper.exe" | Test-Path)) {
	
	.\Publish-PublishReadmeHelper.ps1
	
}

.\BuildTools\PointlessWaymarks.PublishReadmeHelper.exe