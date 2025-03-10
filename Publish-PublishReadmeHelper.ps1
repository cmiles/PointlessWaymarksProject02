$ErrorActionPreference = "Stop"

$vsWhere = "{0}\Microsoft Visual Studio\Installer\vswhere.exe" -f ${env:ProgramFiles(x86)}

$msBuild = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe

dotnet clean .\PointlessWaymarks.PublishReadmeHelper\PointlessWaymarks.PublishReadmeHelper.csproj -property:Configuration=Release -property:Platform=x64 -verbosity:minimal

dotnet restore .\PointlessWaymarks.PublishReadmeHelper\PointlessWaymarks.PublishReadmeHelper.csproj -r win-x64 -verbosity:minimal

& $msBuild .\PointlessWaymarks.PublishReadmeHelper\PointlessWaymarks.PublishReadmeHelper.csproj -t:publish -p:PublishProfile=.\PointlessWaymarks.PublishReadmeHelper\Properties\PublishProfile\FolderProfile.pubxml -verbosity:minimal

if ($lastexitcode -ne 0) { throw ("Exec: " + $errorMessage) }