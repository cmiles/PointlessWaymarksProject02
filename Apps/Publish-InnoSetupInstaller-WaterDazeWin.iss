#ifndef Version
  #define Version GetDateTimeString('yyyy-mm-dd-hh-nn', '', '')
#endif

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Pointless Waymarks Water Daze"
#define MyAppDefaultDirName "PointlessWaymarksWaterDazeWin"
#define MyAppExeName "WaterDaze.Photino.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-WaterDazeWin-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\WaterDazeWin\*"

[Setup]
AppId={{1D620F38-2A64-4E99-BA7D-B6740536E863}
AppName={#MyAppName}
AppVersion={#Version}
AppPublisher={#MyAppPublisher}
WizardStyle=modern
DefaultDirName={autopf}\{#MyAppDefaultDirName}
DefaultGroupName={#MyAppDefaultGroupName}
Compression=lzma2
SolidCompression=yes
OutputDir={#MyAppOutputDir}
OutputBaseFilename={#MyAppOutputBaseFilename}{#Version}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[InstallDelete]
Type: files; Name: "{app}\PublishVersion--*.txt"

[Files]
Source: {#MyAppFilesSource}; DestDir: "{app}\"; Flags: recursesubdirs ignoreversion;

[Icons]
Name: "{app}\app.icon"; Filename: "{app}\{#MyAppExeName}";

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch application"; Flags: postinstall nowait skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    SaveStringToFile(ExpandConstant('{app}\PublishVersion--{#Version}.txt'), '{#Version}', False);
  end;
end;
