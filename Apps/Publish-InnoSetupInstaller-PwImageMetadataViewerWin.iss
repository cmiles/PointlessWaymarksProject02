#ifndef Version
  #define Version GetDateTimeString('yyyy-mm-dd-hh-nn', '', '')
#endif

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Pointless Waymarks Image Metadata Viewer"
#define MyAppDefaultDirName "PointlessWaymarksImageMetadataViewerWin"
#define MyAppExeName "PwImageMetadataViewer.Photino.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-PwImageMetadataViewerWin-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\PwImageMetadataViewerWin\*"

[Setup]
AppId={{F8296CCB-4CB5-4064-B489-6C7CE6565E66}
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
