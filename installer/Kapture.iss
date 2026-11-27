; Kapture installer (Inno Setup 6). Installeert voor de huidige gebruiker, zonder beheerdersrechten.
#ifndef AppVersion
  #define AppVersion "0.7.0"
#endif

[Setup]
AppId={{5E2B8C71-3F4A-4D9E-A1C6-7B0D2E8F4A19}
AppName=Kapture
AppVersion={#AppVersion}
AppVerName=Kapture {#AppVersion}
AppPublisher=Kapture
DefaultDirName={localappdata}\Programs\Kapture
DefaultGroupName=Kapture
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=..\out
OutputBaseFilename=Kapture-Setup
SetupIconFile=..\out\Kapture.ico
UninstallDisplayIcon={app}\Kapture.exe
UninstallDisplayName=Kapture
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
SetupMutex=KaptureSetup

[Languages]
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktop"; Description: "Snelkoppeling op het bureaublad"; Flags: unchecked
Name: "autostart"; Description: "Starten met Windows"

[Files]
Source: "..\out\Kapture.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\LICENSE-Lucide.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion
Source: "..\assets\LICENSE-Phosphor.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion
Source: "..\assets\fonts\LICENSE-Inter.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion
Source: "..\assets\fonts\LICENSE-JetBrainsMono.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Kapture"; Filename: "{app}\Kapture.exe"
Name: "{userdesktop}\Kapture"; Filename: "{app}\Kapture.exe"; Tasks: desktop

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Kapture"; ValueData: """{app}\Kapture.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Kapture.exe"; Description: "Kapture starten"; Flags: nowait postinstall skipifsilent
; bijwerken vanuit Kapture (met /BIJWERKEN): Kapture daarna vanzelf weer starten
Filename: "{app}\Kapture.exe"; Flags: nowait; Check: IsUpdateRun

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM Kapture.exe /F"; Flags: runhidden; RunOnceId: "StopKapture"

[CustomMessages]
nl.AskDeleteData=Wil je ook de instellingen en het foutlog van Kapture verwijderen? Je screenshots blijven altijd staan.
en.AskDeleteData=Do you also want to delete the Kapture settings and error log? Your screenshots always stay.
fr.AskDeleteData=Voulez-vous aussi supprimer les paramètres et le journal des erreurs de Kapture ? Vos captures d'écran restent toujours.
es.AskDeleteData=¿Quieres borrar también los ajustes y el registro de errores de Kapture? Tus capturas de pantalla se quedan siempre.

[Code]
var
  Installed: Boolean;

// Is deze installer gestart door "bijwerken met een klik" in Kapture?
function IsUpdateRun: Boolean;
var
  i: Integer;
begin
  Result := False;
  for i := 1 to ParamCount do
    if CompareText(ParamStr(i), '/BIJWERKEN') = 0 then Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then Installed := True;
end;

// Mislukt het bijwerken (virusscanner, volle schijf): Kapture toch weer starten, de oude versie staat er nog
procedure DeinitializeSetup;
var
  rc: Integer;
  exe: String;
begin
  if IsUpdateRun and (not Installed) then
  begin
    exe := ExpandConstant('{localappdata}\Programs\Kapture\Kapture.exe');
    if FileExists(exe) then Exec(exe, '', '', SW_SHOWNORMAL, ewNoWait, rc);
  end;
end;

// Na het verwijderen: vragen of %APPDATA%\Kapture ook weg mag. Screenshots staan daar niet in.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  dataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    dataDir := ExpandConstant('{userappdata}\Kapture');
    if DirExists(dataDir) and (not UninstallSilent) then
      if MsgBox(CustomMessage('AskDeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(dataDir, True, True, True);
  end;
end;
