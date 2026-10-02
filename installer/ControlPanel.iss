; Installer for WinHotCorner Control Panel (Inno Setup 7)
;
; Build with build.ps1, which first builds the hot corner installer and publishes the control panel.
;
; Published as the "Full" installer: it contains the hot corner installer ("HotCornerOnly") and runs it when the
; hot corner is missing or older, so this one installer is enough to get both, and running it after HotCornerOnly
; only adds the control panel. Each keeps its own entry in Installed apps and can be uninstalled alone.

#define AppName "WinHotCorner Control Panel"
#define PublishDir "Output\ControlPanel"
#define AppExe "WinHotCornerControlPanel.exe"
; The numeric version of the built exe; build.ps1 passes the version to show (AppVersion) and to put in the file
; name (FileNameVersion), see "Versions" in docs\technical.md. Both programs always have the same version
#define NumericVersion GetVersionNumbersString(PublishDir + "\" + AppExe)
#ifndef AppVersion
  #define AppVersion NumericVersion
#endif
#ifndef FileNameVersion
  #define FileNameVersion NumericVersion
#endif
#define HotCornerVersion GetVersionNumbersString("..\src\WinHotCorner\bin\Release\net48\WinHotCorner.exe")
#define HotCornerSetup "WinHotCorner-HotCornerOnly-" + FileNameVersion + "-setup.exe"
; AppId of WinHotCorner.iss, to find the installed hot corner
#define HotCornerAppId "{1F9F1765-5178-4A09-9EA9-34E77B75CC21}"

[Setup]
AppId={{AB149040-E4B0-4C7A-ACDD-94165A363C97}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#NumericVersion}
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
OutputBaseFilename=WinHotCorner-Full-{#FileNameVersion}-setup
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

// The number part of a version shown in Installed apps, as a.b.c.d: "1.1.57" -> "1.1.57.0",
// "1.1.56.2+3f2a9c1" -> "1.1.56.2"
function NumericPart(const Version: String): String;
var
  I, Dots: Integer;
begin
  Result := Version;
  I := Pos('+', Result);
  if I > 0 then
    Result := Copy(Result, 1, I - 1);
  Dots := 0;
  for I := 1 to Length(Result) do
    if Result[I] = '.' then
      Dots := Dots + 1;
  if Dots = 2 then
    Result := Result + '.0';
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
  Result := StrToVersion(NumericPart(Installed), InstalledVersion) and StrToVersion('{#HotCornerVersion}', BundledVersion)
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
