; Inno Setup Script for Auto Typer byGo & Xbox Game Bar Widget
#define MyAppName "Auto Typer byGo"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "by Go"
#define MyAppURL "https://goutham-11-16.github.io/Auto-Typer/"
#define MyAppExeName "AutoTyper-byGo.exe"

[Setup]
AppId={{2B695111-6635-4B92-B7C9-0D8D90518861}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=dist
OutputBaseFilename=AutoTyper-byGo-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
SetupIconFile=logo3.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 1. Auto-Typer Desktop Application
Source: "AutoTyper\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; 2. Xbox Game Bar Widget Package Files
Source: "GameBarWidget\AutoTyperWidget.exe"; DestDir: "{app}\GameBarWidget"; Flags: ignoreversion
Source: "GameBarWidget\AppxManifest.xml"; DestDir: "{app}\GameBarWidget"; Flags: ignoreversion
Source: "GameBarWidget\Microsoft.Gaming.XboxGameBar.dll"; DestDir: "{app}\GameBarWidget"; Flags: ignoreversion
Source: "GameBarWidget\Microsoft.Gaming.XboxGameBar.winmd"; DestDir: "{app}\GameBarWidget"; Flags: ignoreversion
Source: "GameBarWidget\Assets\*"; DestDir: "{app}\GameBarWidget\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "GameBarWidget\lib\*"; DestDir: "{app}\GameBarWidget\lib"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Silently register Xbox Game Bar Widget with Windows
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command ""Add-AppxPackage -Register '{app}\GameBarWidget\AppxManifest.xml' -DisableDevelopmentMode -ErrorAction SilentlyContinue"""; Flags: runhidden
; Prompt to launch desktop app after setup completes
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Silently unregister Xbox Game Bar Widget on uninstallation
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command ""Get-AppxPackage AutoTyper.GameBarWidget -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue"""; Flags: runhidden; RunOnceId: "UnregisterAutoTyperWidget"
