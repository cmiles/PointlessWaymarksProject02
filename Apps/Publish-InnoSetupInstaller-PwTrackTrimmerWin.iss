#define Version GetDateTimeString('yyyy.mm.dd.hhnn', '', '')

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Pointless Waymarks Track Trimmer"
#define MyAppDefaultDirName "PointlessWaymarksTrackTrimmerWin"
#define MyAppExeName "PwTrackTrimmer.Photino.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-PwTrackTrimmerWin-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\PwTrackTrimmerWin\*"

[Setup]
AppId={{A508DA69-DFA8-4EA2-A5C9-1BAB28AF5D0A}
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