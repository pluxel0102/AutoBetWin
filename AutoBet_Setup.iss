; AutoBet Installer Script for Inno Setup
; Download Inno Setup from: https://jrsoftware.org/isdl.php

#define MyAppName "AutoBet"
#define MyAppVersion "1.0.2"
#define MyAppPublisher "AutoBet Team"
#define MyAppExeName "AutoBet.exe"
#define MyAppSourcePath "C:\Users\User\Desktop\AutoBet.NETWinUI3\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
; Application Info
AppId={{F8D3C9A1-5B2E-4D7F-9C3A-1E8B4F6D2A9C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=C:\Users\User\Desktop\AutoBet.NETWinUI3\Installer
OutputBaseFilename=AutoBet_Setup
Compression=lzma2/max
SolidCompression=yes

; System Requirements
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; UI Settings
WizardStyle=modern
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
; SetupIconFile=C:\Users\User\Desktop\AutoBet.NETWinUI3\Assets\StoreLogo.png
PrivilegesRequired=lowest

; Language
ShowLanguageDialog=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные значки:"; Flags: unchecked

[Files]
Source: "{#MyAppSourcePath}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Удалить {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\AutoBet"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  LogFile: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Delete settings folder from Roaming
    DelTree(ExpandConstant('{userappdata}\AutoBet'), True, True, True);
    
    // Delete desktop log
    LogFile := ExpandConstant('{userdesktop}\AutoBet_Log.txt');
    if FileExists(LogFile) then
      DeleteFile(LogFile);
  end;
end;
