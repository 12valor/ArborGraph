; Side-by-side Windows x64 installer for the Electron preview.
; Keep the supported WPF installer and its AppId unchanged.
#define PreviewName "ArborGraph Desktop Preview"
#define PreviewExe "ArborGraph Desktop Preview.exe"
#define PreviewVersion "1.3.2"
#define PreviewSource "..\dist\desktop-preview"

[Setup]
AppId={{58A5B2A8-FA36-4C8E-9737-69D6EA0184B0}
AppName={#PreviewName}
AppVersion={#PreviewVersion}
AppPublisher=AG DIAZ EVANGELISTA
AppPublisherURL=https://github.com/12valor/ArborGraph
DefaultDirName={autopf}\ArborGraph Desktop Preview
DisableProgramGroupPage=yes
LicenseFile=eula.txt
SetupIconFile=..\Resources\ArborGraph.ico
OutputDir=..\dist\desktop-setup
OutputBaseFilename=ArborGraph-Desktop-Preview-Setup-v{#PreviewVersion}-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PreviewSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#PreviewName}"; Filename: "{app}\{#PreviewExe}"
Name: "{autodesktop}\{#PreviewName}"; Filename: "{app}\{#PreviewExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#PreviewExe}"; Description: "{cm:LaunchProgram,{#PreviewName}}"; Flags: nowait postinstall skipifsilent
