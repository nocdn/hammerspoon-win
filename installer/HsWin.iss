#define MyAppName "Hammerspoon (Windows Edition)"
#define MyAppPublisher "Hammerspoon"
#define MyAppUrl "https://github.com/nocdn/hammerspoon-win"
#define MyAppExeName "Hammerspoon (Windows Edition).exe"
#define MyInstallFolderName "Hammerspoon (Windows Edition)"
#define MyAppVersion GetEnv("HSWIN_VERSION")
#define MyVersionInfoVersion GetEnv("HSWIN_VERSION_INFO_VERSION")
#define MyPublishDir GetEnv("HSWIN_PUBLISH_DIR")
#define MyOutputDir GetEnv("HSWIN_OUTPUT_DIR")

#if MyAppVersion == ""
  #define MyAppVersion "0.1.0"
#endif

#if MyVersionInfoVersion == ""
  #define MyVersionInfoVersion "0.1.0.0"
#endif

#if MyPublishDir == ""
  #define MyPublishDir "..\artifacts\publish\HsWin.App\Release\win-x64"
#endif

#if MyOutputDir == ""
  #define MyOutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{5E65F5D3-AC46-43D2-B31C-B98F8757C640}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyInstallFolderName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
OutputDir={#MyOutputDir}
OutputBaseFilename=hswin-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoVersion={#MyVersionInfoVersion}

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autoprograms}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Hammerspoon (Windows Edition)"; Flags: nowait postinstall skipifsilent runasoriginaluser
