#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\app\Xianpu.Desktop\bin\Release\net10.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{7E8B5C42-9A3D-4F18-B812-6C5A9E2F104B}
AppName=弦笺 (StringNote)
AppVersion={#AppVersion}
AppVerName=弦笺 (StringNote) {#AppVersion}
AppPublisher=StringNote
DefaultDirName={localappdata}\Programs\StringNote
DefaultGroupName=弦笺 (StringNote)
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=yes
ChangesAssociations=yes
OutputDir=..\dist
OutputBaseFilename=StringNote-Setup-{#AppVersion}
SetupIconFile=..\app\Xianpu.Desktop\Assets\StringNote.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\弦笺 (StringNote)"; Filename: "{app}\Xianpu.Desktop.exe"
Name: "{autodesktop}\弦笺 (StringNote)"; Filename: "{app}\Xianpu.Desktop.exe"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Classes\.xianpu.json"; ValueType: string; ValueName: ""; ValueData: "StringNote.Score"; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\StringNote.Score"; ValueType: string; ValueName: ""; ValueData: "弦笺曲谱文件 (StringNote Score)"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\StringNote.Score\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Xianpu.Desktop.exe,0"
Root: HKA; Subkey: "Software\Classes\StringNote.Score\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Xianpu.Desktop.exe"" ""%1"""

[Run]
Filename: "{app}\Xianpu.Desktop.exe"; Description: "{cm:LaunchProgram,弦笺 (StringNote)}"; Flags: nowait postinstall skipifsilent
