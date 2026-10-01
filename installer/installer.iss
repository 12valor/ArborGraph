; =====================================================================
; DiskScope - Official Inno Setup 6 Installer Script
; Target: Windows 10 / 11 (64-bit x64)
; =====================================================================

#define MyAppName "DiskScope"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AG DIAZ EVANGELISTA"
#define MyAppURL "https://github.com/12valor/C-file-scanner"
#define MyAppExeName "DiskScope.exe"

[Setup]
; Unique AppId generated for DiskScope
AppId={{D37F7E1A-85F4-4BC3-9C1D-72810C24A59E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\DiskScope
DisableProgramGroupPage=yes
; Strict EULA consent enforcement: User must accept license before installation continues
LicenseFile=eula.txt
SetupIconFile=..\Resources\DiskScope.ico
OutputDir=..\dist\setup
OutputBaseFilename=DiskScopeSetup-{#MyAppVersion}-x64
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
Source: "..\DiskScope.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "eula.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up runtime files if any
Type: files; Name: "{app}\DiskScope.exe"
Type: files; Name: "{app}\eula.txt"

[Code]
// Custom code to log acceptance if needed locally
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Local acceptance recorded on disk
    Log('DiskScope EULA accepted at: ' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':'));
  end;
end;
