; Inno Setup script for FolderDiag (WPF)
; Built by .github/workflows/build-installers.yml, but can also be run locally:
;   iscc /DAppVersion=1.0.0 /DArch=x64 /DSourceDir=..\artifacts\publish\x64 installer\FolderDiag.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\" + Arch
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installers"
#endif

#define AppName "FolderDiag"
#define AppPublisher "dotnetappdev"
#define AppURL "https://github.com/dotnetappdev/folderdiag"
#define AppExeName "FolderDiag.Wpf.exe"

[Setup]
AppId={{7E4B2C1A-5D3F-4A8E-9B6C-0F1D2E3A4B5C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=FolderDiag-Setup-{#AppVersion}-{#Arch}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Allow per-user install without admin, or per-machine with elevation
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
