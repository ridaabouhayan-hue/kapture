; Kapsure installer (Inno Setup 6). Installeert voor de huidige gebruiker, zonder beheerdersrechten.
#ifndef AppVersion
  #define AppVersion "0.8.0"
#endif

[Setup]
AppId={{5E2B8C71-3F4A-4D9E-A1C6-7B0D2E8F4A19}
AppName=Kapsure
AppVersion={#AppVersion}
AppVerName=Kapsure {#AppVersion}
AppPublisher=Kapsure
DefaultDirName={localappdata}\Programs\Kapsure
DefaultGroupName=Kapsure
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=..\out
OutputBaseFilename=Kapsure-Setup
SetupIconFile=..\out\Kapsure.ico
UninstallDisplayIcon={app}\Kapsure.exe
UninstallDisplayName=Kapsure
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
SetupMutex=KapsureSetup,KaptureSetup

[Languages]
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktop"; Description: "Snelkoppeling op het bureaublad"; Flags: unchecked
Name: "autostart"; Description: "Starten met Windows"

[Files]
Source: "..\out\Kapsure.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\LICENSE-Lucide.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion
Source: "..\assets\LICENSE-Phosphor.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion
Source: "..\assets\fonts\LICENSE-Inter.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion
Source: "..\assets\fonts\LICENSE-JetBrainsMono.txt"; DestDir: "{app}\licenties"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\Kapsure"; Filename: "{app}\Kapsure.exe"
Name: "{userdesktop}\Kapsure"; Filename: "{app}\Kapsure.exe"; Tasks: desktop

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Kapsure"; ValueData: """{app}\Kapsure.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Kapsure.exe"; Description: "Kapsure starten"; Flags: nowait postinstall skipifsilent
; bijwerken vanuit Kapsure (met /BIJWERKEN): Kapsure daarna vanzelf weer starten
Filename: "{app}\Kapsure.exe"; Flags: nowait; Check: IsUpdateRun

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM Kapsure.exe /F"; Flags: runhidden; RunOnceId: "StopKapsure"
Filename: "{cmd}"; Parameters: "/C taskkill /IM Kapture.exe /F"; Flags: runhidden; RunOnceId: "StopKapture"

[CustomMessages]
nl.AskDeleteData=Wil je ook de instellingen en het foutlog van Kapsure verwijderen? Je screenshots blijven altijd staan.
en.AskDeleteData=Do you also want to delete the Kapsure settings and error log? Your screenshots always stay.
fr.AskDeleteData=Voulez-vous aussi supprimer les paramètres et le journal des erreurs de Kapsure ? Vos captures d'écran restent toujours.
es.AskDeleteData=¿Quieres borrar también los ajustes y el registro de errores de Kapsure? Tus capturas de pantalla se quedan siempre.

[Code]
var
  Installed: Boolean;

// Is deze installer gestart door "bijwerken met een klik" in Kapsure?
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
  if CurStep = ssPostInstall then
  begin
    Installed := True;
    // Van de oude naam (Kapture): pas nu alles goed geïnstalleerd is, het oude programma en de oude snelkoppelingen weg.
    // "Starten met Windows" zet Kapsure zelf om bij de eerste start (dan blijft hij ook aan als hij later aangezet was).
    DeleteFile(ExpandConstant('{app}\Kapture.exe'));
    DeleteFile(ExpandConstant('{userprograms}\Kapture.lnk'));
    DeleteFile(ExpandConstant('{userdesktop}\Kapture.lnk'));
  end;
end;

// Mislukt het bijwerken (virusscanner, volle schijf): Kapsure toch weer starten, de oude versie staat er nog
procedure DeinitializeSetup;
var
  rc: Integer;
  exe: String;
begin
  if IsUpdateRun and (not Installed) then
  begin
    // eerst de nieuwe naam in de map waar hij staat, dan de oude naam (bijwerken vanaf Kapture)
    try
      exe := ExpandConstant('{app}\Kapsure.exe');
      if not FileExists(exe) then exe := ExpandConstant('{app}\Kapture.exe');
    except
      exe := ExpandConstant('{localappdata}\Programs\Kapture\Kapture.exe');   // {app} nog niet bekend
    end;
    if FileExists(exe) then Exec(exe, '', '', SW_SHOWNORMAL, ewNoWait, rc);
  end;
end;

// Na het verwijderen: vragen of %APPDATA%\Kapsure ook weg mag. Screenshots staan daar niet in.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  dataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    dataDir := ExpandConstant('{userappdata}\Kapsure');
    if DirExists(dataDir) and (not UninstallSilent) then
      if MsgBox(CustomMessage('AskDeleteData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(dataDir, True, True, True);
  end;
end;
