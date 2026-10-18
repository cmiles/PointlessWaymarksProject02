#ifndef Version
  #define Version GetDateTimeString('yyyy-mm-dd-hh-nn', '', '')
#endif

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Rincon Valley Weather"
#define MyAppDefaultDirName "PointlessWaymarksHistoricTempestsWin"
#define MyAppExeName "HistoricTempests.Photino.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-HistoricTempestsWin-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\HistoricTempestsWin\*"

[Setup]
AppId={{0328B3D8-9F63-4CEB-958E-3425A44F9459}
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
