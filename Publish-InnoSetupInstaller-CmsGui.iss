#ifndef Version
  #define Version = '1902-07-02-00-00-00';
#endif

#ifndef ScmCommit
  #define ScmCommit = '???';
#endif

#define MyAppPublisher "Charles Miles"
#define MyAppOutputDir "M:\PointlessWaymarksPublications"

#define MyAppDefaultGroupName "Pointless Waymarks"

#define MyAppName "Pointless Waymarks CMS"
#define MyAppDefaultDirName "PointlessWaymarksCms"
#define MyAppExeName "PointlessWaymarks.CmsGui.exe"
#define MyAppOutputBaseFilename "PointlessWaymarks-CmsGui-Setup--"
#define MyAppFilesSource "M:\PointlessWaymarksPublications\PointlessWaymarks.CmsGui\*"

[Setup]
AppId={{1780F4D0-0A17-4460-878B-58136D038D51}
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
WizardSmallImageFile="M:\PointlessWaymarksPublications\PointlessWaymarks.CmsGui\CmsInstallerTopRightImage.bmp"
WizardImageFile="M:\PointlessWaymarksPublications\PointlessWaymarks.CmsGui\CmsInstallerLeftImage.bmp"

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