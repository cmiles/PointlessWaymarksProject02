#ifndef Version
  #define Version = '1902-07-02-00-00-00';
#endif

#ifndef ScmCommit
  #define ScmCommit = '???';
#endif

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Pointless Waymarks Photo Metadata Basics"
#define MyAppDefaultDirName "PointlessWaymarksPhotoMetadataBasicsGui"
#define MyAppExeName "PointlessWaymarks.PhotoMetadataBasicsGui.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-PhotoMetadataBasicsGui-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\PointlessWaymarks.PhotoMetadataBasicsGui\*"

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
WizardSmallImageFile="M:\PointlessWaymarksPublications\PointlessWaymarks.PhotoMetadataBasicsGui\PhotoMetadataBasicsInstallerTopRightImage.bmp"
WizardImageFile="M:\PointlessWaymarksPublications\PointlessWaymarks.PhotoMetadataBasicsGui\PhotoMetadataBasicsInstallerLeftImage.bmp"

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