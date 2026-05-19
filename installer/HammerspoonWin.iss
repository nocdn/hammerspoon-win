#define MyAppName "HammerSpoon (Windows Edition)"
#define MyAppPublisher "HammerspoonWin"
#define MyAppUrl "https://github.com"
#define MyAppExeName "HammerspoonWin.App.exe"
#define MyInstallFolderName "HammerspoonWin"
#define MyAppVersion GetEnv("HAMMERSPOONWIN_VERSION")
#define MyPublishDir GetEnv("HAMMERSPOONWIN_PUBLISH_DIR")
#define MyOutputDir GetEnv("HAMMERSPOONWIN_OUTPUT_DIR")

#if MyAppVersion == ""
  #define MyAppVersion "0.1.0"
#endif

#if MyPublishDir == ""
  #define MyPublishDir "..\artifacts\publish\HammerspoonWin.App\Release\win-x64"
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
OutputBaseFilename=HammerspoonWinSetup-{#MyAppVersion}-win-x64
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
VersionInfoVersion={#MyAppVersion}

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autoprograms}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch HammerSpoon (Windows Edition)"; Flags: nowait postinstall skipifsilent runasoriginaluser
