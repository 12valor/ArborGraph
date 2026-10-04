; =====================================================================
; ArborGraph - Official Inno Setup 6 Installer Script
; Target: Windows 10 / 11 (64-bit x64)
; =====================================================================

#define MyAppName "ArborGraph"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AG DIAZ EVANGELISTA"
#define MyAppURL "https://github.com/12valor/ArborGraph"
#define MyAppExeName "ArborGraph.exe"

[Setup]
; AppId preserved for seamless in-place upgrade from legacy installations
AppId={{D37F7E1A-85F4-4BC3-9C1D-72810C24A59E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\ArborGraph
DisableProgramGroupPage=yes
; Strict EULA consent enforcement: User must accept license before installation continues
LicenseFile=eula.txt
SetupIconFile=..\Resources\ArborGraph.ico
OutputDir=..\dist\setup
OutputBaseFilename=ArborGraph-Setup-{#MyAppVersion}-x64
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
Source: "..\ArborGraph.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "eula.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up runtime files
Type: files; Name: "{app}\ArborGraph.exe"
Type: files; Name: "{app}\eula.txt"

[Code]
// Detect and migrate previous DiskScope installation settings if present
procedure CurStepChanged(CurStep: TSetupStep);
var
  LegacyDir: String;
begin
  if CurStep = ssPostInstall then
  begin
    Log('ArborGraph EULA accepted and installation completed at: ' + GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':'));
    LegacyDir := ExpandConstant('{autopf}\DiskScope');
    if DirExists(LegacyDir) then
    begin
      Log('Detected previous DiskScope installation directory at: ' + LegacyDir);
    end;
  end;
end;
