; KEYRA — per-user Windows installer (no admin / UAC).
; Compiled from CI on tag v*  (see .github/workflows/release.yml)
;   ISCC.exe /DMyAppVersion=1.2.3 installer\keyra.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName "KEYRA"
#define MyAppPublisher "KEYRA"
#define MyAppExeName "SshKeyManager.exe"
#define MyAppVersionInfo MyAppVersion + ".0"

[Setup]
AppId={{8F3A1C2E-9B47-4D6A-A1E8-0C5B7F2D4E91}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (c) 2026 KEYRA contributors
VersionInfoVersion={#MyAppVersionInfo}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersionInfo}
DefaultDirName={localappdata}\Programs\KEYRA
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist\installer
OutputBaseFilename=KEYRA-{#MyAppVersion}-win-x64-setup
#if FileExists("..\src\SshKeyManager\Assets\keyra-icon.ico")
SetupIconFile=..\src\SshKeyManager\Assets\keyra-icon.ico
#endif
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
UninstallDisplayName={#MyAppName}
UsePreviousAppDir=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "SSH key vault and SSH client"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "SSH key vault and SSH client"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Leave the vault in %LocalAppData%\SshKeyManager\ — never delete user keys on uninstall.
Type: files; Name: "{app}\*.log"
