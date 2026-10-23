; Inno Setup Script for NotionDesk
#define MyAppName "NotionDesk"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Nikolay Lvov"
#define MyAppExeName "NotionDesk.exe"

[Setup]
AppId={{D41A28A8-285A-4B6A-B664-949B24D9C7E1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=D:\projects\DesktopNotesWidget\installer_output
OutputBaseFilename=NotionDesk-Setup-v1.0.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Launch NotionDesk automatically on Windows startup / Запускать при старте Windows"; GroupDescription: "Options:"

[Files]
Source: "D:\projects\DesktopNotesWidget\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\projects\DesktopNotesWidget\publish\config.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "NotionDeskNotes"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
