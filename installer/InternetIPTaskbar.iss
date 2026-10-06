; Inno Setup 6 script. Build with scripts\build-installer.ps1 (publishes the app first).

#define MyAppName "InternetIPTaskbar"
#define MyAppPublisher "dakeending"
#define MyAppURL "https://github.com/dakeending/InternetIPTaskbar"
#define MyAppExeName "InternetIPTaskbar.exe"
#define PublishDir AddBackslash(SourcePath) + "..\src\InternetIPTaskbar\bin\Release\net8.0-windows\win-x64\publish"
; the version is read from the published exe (it is set once, in InternetIPTaskbar.csproj)
#define MyAppVersion GetFileProductVersion(PublishDir + "\" + MyAppExeName)

[Setup]
; AppId identifies the application for upgrades and uninstall. Never change it.
AppId={{8E7A6F39-3E1C-4D52-A3A9-7A5A1A7C9B11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\InternetIPTaskbar
DefaultGroupName=InternetIPTaskbar
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=InternetIPTaskbar_Setup_v{#MyAppVersion}
SetupIconFile=..\src\InternetIPTaskbar\assets\app.ico
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.AutoStartTask=Start InternetIPTaskbar when I sign in to Windows
english.AdditionalGroup=Additional options:
english.UninstallDataPrompt=Also delete the settings, the log and the flag cache?
russian.AutoStartTask=Запускать при входе в Windows
russian.AdditionalGroup=Дополнительно:
russian.UninstallDataPrompt=Удалить также настройки, журнал и кэш флагов?

[Tasks]
Name: "autostart"; Description: "{cm:AutoStartTask}"; GroupDescription: "{cm:AdditionalGroup}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autoprograms}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

[Registry]
; autostart lives in the registry: the program's own "Start with Windows" menu item toggles the same value
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart
; a "disabled" mark set in Task Manager would otherwise silence the value we have just written
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueName: "{#MyAppName}"; Flags: deletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// The program lives in the tray and may ignore a close request, so terminate it ourselves.
procedure StopApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/im {#MyAppExeName} /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopApp();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // stop the program only after the user has confirmed the uninstall (if they cancel it keeps running)
  if CurUninstallStep = usUninstall then
    StopApp();
  if CurUninstallStep = usPostUninstall then
  begin
    // settings, log and flag cache live outside the program folder, so ask
    if SuppressibleMsgBox(CustomMessage('UninstallDataPrompt'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
      DelTree(ExpandConstant('{localappdata}\{#MyAppName}'), True, True, True);
  end;
end;
