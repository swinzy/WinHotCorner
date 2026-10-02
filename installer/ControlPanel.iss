; Installer for WinHotCorner Control Panel (Inno Setup 7)
;
; Build with build.ps1, which first builds the hot corner installer and publishes the control panel.
;
; It contains the hot corner installer too, and runs it when the hot corner is missing or older, so this one
; installer is enough to get both. Each keeps its own entry in Installed apps and can be uninstalled alone.

#define AppName "WinHotCorner Control Panel"
#define PublishDir "Output\ControlPanel"
#define AppExe "WinHotCornerControlPanel.exe"
#define AppVersion GetVersionNumbersString(PublishDir + "\" + AppExe)
#define HotCornerVersion GetVersionNumbersString("..\src\WinHotCorner\bin\Release\net48\WinHotCorner.exe")
#define HotCornerSetup "WinHotCorner-" + HotCornerVersion + "-setup.exe"
; AppId of WinHotCorner.iss, to find the installed hot corner
#define HotCornerAppId "{1F9F1765-5178-4A09-9EA9-34E77B75CC21}"

[Setup]
AppId={{AB149040-E4B0-4C7A-ACDD-94165A363C97}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Stephen Zhang
AppPublisherURL=https://github.com/swinzy/WinHotCorner
AppSupportURL=https://github.com/swinzy/WinHotCorner/issues
; Next to the hot corner, which the control panel looks for in its parent folder
DefaultDirName={autopf}\WinHotCorner\ControlPanel
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.18362
SetupIconFile=..\src\WinHotCorner\WHC.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=Output
OutputBaseFilename=WinHotCornerControlPanel-{#AppVersion}-setup
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Output\{#HotCornerSetup}"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Run]
Filename: "{app}\{#AppExe}"; Description: "Open {#AppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure RunHidden(const FileName, Params: String);
var
  ResultCode: Integer;
begin
  Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

// Stops the control panel in every session and waits until it is gone, so its files can be replaced or deleted
procedure StopControlPanel();
begin
  RunHidden(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -Command "Get-Process WinHotCornerControlPanel -ErrorAction SilentlyContinue' +
    ' | Stop-Process -Force -PassThru | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue"');
end;

// True if the hot corner is not installed, or older than the one inside this installer
function HotCornerNeedsInstall(): Boolean;
var
  Installed: String;
  InstalledVersion, BundledVersion: Int64;
begin
  if not RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#HotCornerAppId}_is1',
      'DisplayVersion', Installed) then
  begin
    Result := True;
    Exit;
  end;
  Result := StrToVersion(Installed, InstalledVersion) and StrToVersion('{#HotCornerVersion}', BundledVersion)
    and (ComparePackedVersion(InstalledVersion, BundledVersion) < 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  StopControlPanel();

  if HotCornerNeedsInstall() then
  begin
    ExtractTemporaryFile('{#HotCornerSetup}');
    if not Exec(ExpandConstant('{tmp}\{#HotCornerSetup}'), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '',
        SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
      Result := Format('Could not install WinHotCorner (exit code %d).', [ResultCode]);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopControlPanel();
end;
