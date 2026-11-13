#ifndef Version
  #define Version GetDateTimeString('yyyy-mm-dd-hh-nn', '', '')
#endif

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Pointed Horizons"
#define MyAppDefaultDirName "PointlessWaymarksPointedHorizonsWin"
#define MyAppExeName "PointedHorizons.Photino.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-PointedHorizonsWin-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\PointedHorizonsWin\*"

[Setup]
AppId={{FA22B940-7034-475B-9A31-D8685964EDE0}
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
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico";

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

