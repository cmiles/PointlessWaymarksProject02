#ifndef Version
  #define Version = '1902-07-02-00-00-00';
#endif

#ifndef ScmCommit
  #define ScmCommit = '???';
#endif

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Pointless Waymarks Utilitarian Image Combiner"
#define MyAppDefaultDirName "PointlessWaymarksUtilitarianImageCombinerGui"
#define MyAppExeName "PointlessWaymarks.UtilitarianImageCombinerGui.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-UtilitarianImageCombinerGui-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\PointlessWaymarks.UtilitarianImageCombinerGui\*"

[Setup]
AppId={{3752f0c2-dfb6-4926-9bf0-87692e0170e5}
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
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
WizardSmallImageFile="M:\PointlessWaymarksPublications\PointlessWaymarks.UtilitarianImageCombinerGui\UtilitarianImageCombinerInstallerTopRightImage.bmp"
WizardImageFile="M:\PointlessWaymarksPublications\PointlessWaymarks.UtilitarianImageCombinerGui\UtilitarianImageCombinerInstallerLeftImage.bmp"

[Files]
Source: {#MyAppFilesSource}; DestDir: "{app}\"; Flags: recursesubdirs ignoreversion; AfterInstall:PublishVersionAfterInstall;

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}";

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch application"; Flags: postinstall nowait skipifsilent

[Code]
procedure PublishVersionAfterInstall();
begin
  SaveStringToFile(ExpandConstant('{app}\PublishVersion--{#Version}.txt'), ExpandConstant('({#ScmCommit})'), False);
end;