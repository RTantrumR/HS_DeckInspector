; HS Deck Inspector setup. Built by installer/build.ps1, which stages the files into installer/stage first:
;   stage\bepinex\  BepInEx 5.4.23.5 + doorstop 4.5.0 x64, laid out as in the Hearthstone folder
;   stage\plugin\   the plugin DLL
; BepInEx is shared with other mods: its files go in only where missing and are reference-counted
; (sharedfile), so uninstalling removes them only when nothing else counted on them. A BepInEx that was
; there before, with no count, keeps one count for whoever put it there and is never removed by this.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{DE57D0F6-FF39-406F-A7F2-43645B28D073}
AppName=HS Deck Inspector
AppVersion={#AppVersion}
AppVerName=HS Deck Inspector {#AppVersion}
AppPublisher=RTantrumR
DefaultDirName={code:HsDir}
AppendDefaultDirName=no
DirExistsWarning=no
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
DefaultGroupName=HS Deck Inspector
; The shared-file counts live under HKLM.
PrivilegesRequired=admin
UninstallFilesDir={app}\BepInEx\HSDeckInspector\uninstall
UninstallDisplayName=HS Deck Inspector
OutputDir=out
OutputBaseFilename=HSDeckInspector-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
; 64-bit mode, as HS Replay Watcher's setup: both must count BepInEx in the same SharedDLLs key.
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no

[Messages]
SelectDirDesc=Where is Hearthstone installed?
SelectDirLabel3=Deck Inspector goes into your Hearthstone folder (the one with Hearthstone.exe).
SelectDirBrowseLabel=Setup found this folder. If it is wrong, click Browse and pick the Hearthstone folder.
FinishedLabel=HS Deck Inspector is installed. Start Hearthstone from Battle.net as usual: in a game, D shows your deck and F your opponent's. Change the keys under Options, Deck Keys.

[Files]
Source: "stage\bepinex\*"; DestDir: "{app}"; Flags: onlyifdoesntexist sharedfile uninsnosharedfileprompt recursesubdirs createallsubdirs
Source: "stage\plugin\com.tantrumua.deckinspector.dll"; DestDir: "{app}\BepInEx\plugins"; Flags: ignoreversion
Source: "licenses\*"; DestDir: "{app}\BepInEx\HSDeckInspector\licenses"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}\BepInEx\HSDeckInspector\licenses"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\NOTICE"; DestDir: "{app}\BepInEx\HSDeckInspector\licenses"; DestName: "NOTICE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\Uninstall HS Deck Inspector"; Filename: "{uninstallexe}"

[Code]
function HsDir(Param: String): String;
begin
  if not RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hearthstone',
                             'InstallLocation', Result) or (Result = '') then
    Result := ExpandConstant('{commonpf32}\Hearthstone');
end;

function HearthstoneRunning: Boolean;
var
  Locator, Wmi, Found: Variant;
begin
  Result := False;
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Wmi := Locator.ConnectServer('.', 'root\CIMV2');
    Found := Wmi.ExecQuery('SELECT ProcessId FROM Win32_Process WHERE Name = ''Hearthstone.exe''');
    Result := Found.Count > 0;
  except
  end;
end;

function WaitForHearthstoneClosed: Boolean;
begin
  Result := True;
  while HearthstoneRunning do
    if MsgBox('Hearthstone is running. Close it, then click Retry.', mbError, MB_RETRYCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
end;

function InitializeSetup: Boolean;
begin
  Result := WaitForHearthstoneClosed;
end;

function InitializeUninstall: Boolean;
begin
  Result := WaitForHearthstoneClosed;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and not FileExists(AddBackslash(WizardDirValue) + 'Hearthstone.exe') then
  begin
    MsgBox('There is no Hearthstone.exe in this folder. Pick the Hearthstone folder.', mbError, MB_OK);
    Result := False;
  end;
end;
