#define Version GetDateTimeString('yyyy.mm.dd.hhnn', '', '')

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

[Files]
Source: {#MyAppFilesSource}; DestDir: "{app}\"; Flags: recursesubdirs ignoreversion;

[Icons]
Name: "{app}\app.icon"; Filename: "{app}\{#MyAppExeName}";

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch application"; Flags: postinstall nowait skipifsilent
