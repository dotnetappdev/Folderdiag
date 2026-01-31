; Inno Setup Script for FoldersDiag
; This script creates a Windows installer for FoldersDiag

#define MyAppName "FoldersDiag"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "NovaDevelop"
#define MyAppURL "https://github.com/dotnetappdev/Folderdiag"
#define MyAppExeName "FoldersDiag.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
AppId={{A8F3D9C1-5B2E-4A7F-9C3D-1E6F8B4A9D2C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\build\installer
OutputBaseFilename=FoldersDiag-Setup-{#MyAppVersion}
SetupIconFile=..\src\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
Source: "..\build\Release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Name: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\NovaDevelop\FoldersDiag"; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\NovaDevelop\FoldersDiag\Settings"; Flags: uninsdeletekey

[Code]
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := True;
  
  if (Version.Major < 10) or ((Version.Major = 10) and (Version.Build < 17763)) then
  begin
    MsgBox('FoldersDiag requires Windows 10 version 1809 (Build 17763) or later.', mbError, MB_OK);
    Result := False;
  end;
end;
