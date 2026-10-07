; Installer for WinHotCorner (Inno Setup 7)
;
; Build the Release configuration first, then: ISCC.exe WinHotCorner.iss
;
; The hot corner is started at every logon in one of two ways (see "Startup and privileges" in docs\technical.md):
; - usually by a scheduled task that runs it with the user's highest privileges, so it also works while an elevated
;   window is in the foreground (UIPI), without a UAC prompt;
; - with the uiaccess task: its uiAccess version, signed here with a certificate made on this computer, from the Run
;   key. Only the chosen version is installed.

#define AppName "WinHotCorner"
#define BinDir "..\src\WinHotCorner\bin\Release\net48"
#define UIAccessBinDir "..\src\WinHotCorner\bin\UIAccess\Release\net48"
#define AppExe "WinHotCorner.exe"
; The numeric version of the built exe; build.ps1 passes the version to show (AppVersion) and to put in the file
; name (FileNameVersion), see "Versions" in docs\technical.md
#define NumericVersion GetVersionNumbersString(BinDir + "\" + AppExe)
#ifndef AppVersion
  #define AppVersion NumericVersion
#endif
#ifndef FileNameVersion
  #define FileNameVersion NumericVersion
#endif
#define TaskName "WinHotCorner"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{1F9F1765-5178-4A09-9EA9-34E77B75CC21}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#NumericVersion}
AppPublisher=Stephen Zhang
AppPublisherURL=https://github.com/swinzy/WinHotCorner
AppSupportURL=https://github.com/swinzy/WinHotCorner/issues
; Program Files only: the hot corner runs elevated at every logon, so it must live where only admins can write
DefaultDirName={autopf}\WinHotCorner
DisableDirPage=yes
; No shortcuts: it should feel like part of Windows
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 1903, the first version that comes with .NET Framework 4.8
MinVersion=10.0.18362
SetupIconFile=..\src\WinHotCorner\WHC.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=Output
OutputBaseFilename=WinHotCorner-HotCornerOnly-{#FileNameVersion}-setup
WizardStyle=modern
; Our logo instead of Inno Setup's pictures, in sizes for several display scales (Setup picks the closest);
; the large one, on the first and last pages, is drawn from images\wizard.svg
WizardImageFile=images\wizard-202.png,images\wizard-336.png,images\wizard-430.png,images\wizard-534.png
WizardSmallImageFile=images\wizard-small-58.png,images\wizard-small-77.png,images\wizard-small-97.png,images\wizard-small-116.png,images\wizard-small-124.png,images\wizard-small-143.png,images\wizard-small-159.png
Compression=lzma2
SolidCompression=yes
; Stopping the running hot corner is done in the code below
CloseApplications=no

[Tasks]
Name: "uiaccess"; Description: "Sign WinHotCorner on this computer"; Flags: unchecked

[Files]
Source: "{#BinDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion; Tasks: not uiaccess
; The uiAccess version is signed in the temporary folder before anything is installed (PrepareToInstall), then
; installed from there
Source: "{#UIAccessBinDir}\{#AppExe}"; DestName: "WinHotCorner.uiaccess.exe"; Flags: dontcopy
Source: "{tmp}\WinHotCorner.uiaccess.exe"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: external ignoreversion; Tasks: uiaccess
Source: "{#BinDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDir}\ConfigManager.dll"; DestDir: "{app}"; Flags: ignoreversion
; Also kept installed in either case: switching back or uninstalling removes the certificates with it
Source: "uiaccess.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; Explorer starts Run entries through ShellExecute, which grants uiAccess (Task Scheduler cannot start such a program)
Root: HKLM; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: uiaccess

[Code]
const
  NetFx48Release = 528040;

var
  // The certificate made for this installation, until it is installed
  NewThumbprint: String;
  Installed: Boolean;

#include "UIAccessNote.iss"

procedure InitializeWizard();
begin
  AddUIAccessNote();
end;

function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= NetFx48Release);
  if not Result then
    SuppressibleMsgBox('{#AppName} needs .NET Framework 4.8 or later, which comes with Windows 10 version 1903 and later.',
      mbCriticalError, MB_OK, IDOK);
end;

procedure RunHidden(const FileName, Params: String);
var
  ResultCode: Integer;
begin
  Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PowerShell(): String;
begin
  Result := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
end;

// A PowerShell single-quoted string
function Quoted(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '''', '''''', True);
  Result := '''' + Result + '''';
end;

// Runs uiaccess.ps1 and logs what it prints. Run as a script block, so no execution policy can stop it
function RunUIAccessScript(const Script, Params: String; var Output: TArrayOfString): Boolean;
var
  ExecOutput: TExecOutput;
  ResultCode, I: Integer;
begin
  Result := ExecAndCaptureOutput(PowerShell(),
    '-NoProfile -NonInteractive -Command "& ([scriptblock]::Create([IO.File]::ReadAllText(' + Quoted(Script) + '))) ' + Params + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode, ExecOutput) and (ResultCode = 0);
  for I := 0 to GetArrayLength(ExecOutput.StdOut) - 1 do
    Log('uiaccess.ps1: ' + ExecOutput.StdOut[I]);
  for I := 0 to GetArrayLength(ExecOutput.StdErr) - 1 do
    Log('uiaccess.ps1 error: ' + ExecOutput.StdErr[I]);
  Log(Format('uiaccess.ps1 %s: exit code %d', [Params, ResultCode]));
  Output := ExecOutput.StdOut;
end;

// Signs the uiAccess version in the temporary folder, which only administrators can change
function SignHotCorner(): Boolean;
var
  Output: TArrayOfString;
  I: Integer;
begin
  ExtractTemporaryFile('uiaccess.ps1');
  ExtractTemporaryFile('WinHotCorner.uiaccess.exe');
  Result := RunUIAccessScript(ExpandConstant('{tmp}\uiaccess.ps1'),
    '-Sign ' + Quoted(ExpandConstant('{tmp}\WinHotCorner.uiaccess.exe')), Output);
  if Result then
    for I := 0 to GetArrayLength(Output) - 1 do
      if Pos('Thumbprint=', Output[I]) = 1 then
        NewThumbprint := Copy(Output[I], Length('Thumbprint=') + 1, MaxInt);
  Result := Result and (NewThumbprint <> '');
end;

// Removes the certificates made for earlier installations, all of them if Keep is empty
procedure RemoveCertificates(const Keep: String);
var
  Output: TArrayOfString;
begin
  if not RunUIAccessScript(ExpandConstant('{app}\uiaccess.ps1'), '-Remove -Keep ' + Quoted(Keep), Output) then
    SuppressibleMsgBox('Could not remove the certificate that {#AppName} made on this computer before. ' +
      'It is named "WinHotCorner (made on this computer)" in Trusted Root Certification Authorities.', mbError, MB_OK, IDOK);
end;

// Stops the hot corner in every session and waits until it is gone, so its files can be replaced or deleted
// (taskkill returns before the process has exited). Also stops the portable versions from before the installer
// (WinHotCornerService.exe): they hold the same single-instance mutex and would keep the new one from starting
procedure StopHotCorner();
begin
  RunHidden(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -Command "Get-Process WinHotCorner, WinHotCornerService -ErrorAction SilentlyContinue' +
    ' | Stop-Process -Force -PassThru | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue"');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  // Before anything changes, so a failure leaves the installed hot corner as it was
  if WizardIsTaskSelected('uiaccess') and not SignHotCorner() then
  begin
    Result := 'Could not sign {#AppName} on this computer, so nothing was changed. Run Setup again without signing it.';
    Exit;
  end;
  StopHotCorner();
end;

function XmlEscape(const S: String): String;
begin
  Result := S;
  StringChangeEx(Result, '&', '&amp;', True);
  StringChangeEx(Result, '<', '&lt;', True);
  StringChangeEx(Result, '>', '&gt;', True);
end;

// Task Scheduler definition: at every user's logon, as that user with their highest privileges.
// No encoding in the XML declaration: schtasks refuses to switch to a declared UTF-8 ("unable to switch the
// encoding"), and without one the ASCII text written by SaveStringToFile is read fine
function TaskXml(): String;
begin
  Result :=
    '<?xml version="1.0"?>' + #13#10 +
    '<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' + #13#10 +
    '  <RegistrationInfo>' + #13#10 +
    '    <Author>Stephen Zhang</Author>' + #13#10 +
    '    <Description>{#AppName}: opens Task View when the pointer hits the top-left corner.</Description>' + #13#10 +
    // Admins and SYSTEM manage the task; every user may read and run it, so the control panel, which runs
    // without elevation, can start the hot corner the same way as at logon
    '    <SecurityDescriptor>D:(A;;FA;;;BA)(A;;FA;;;SY)(A;;GRGX;;;BU)</SecurityDescriptor>' + #13#10 +
    '  </RegistrationInfo>' + #13#10 +
    '  <Triggers>' + #13#10 +
    '    <LogonTrigger>' + #13#10 +
    '      <Enabled>true</Enabled>' + #13#10 +
    '    </LogonTrigger>' + #13#10 +
    '  </Triggers>' + #13#10 +
    '  <Principals>' + #13#10 +
    '    <Principal id="Users">' + #13#10 +
    // BUILTIN\Users: everyone who logs on. Admins get their elevated token, standard users their normal one
    '      <GroupId>S-1-5-32-545</GroupId>' + #13#10 +
    '      <RunLevel>HighestAvailable</RunLevel>' + #13#10 +
    '    </Principal>' + #13#10 +
    '  </Principals>' + #13#10 +
    '  <Settings>' + #13#10 +
    '    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>' + #13#10 +
    '    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>' + #13#10 +
    '    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>' + #13#10 +
    '    <AllowHardTerminate>true</AllowHardTerminate>' + #13#10 +
    '    <StartWhenAvailable>false</StartWhenAvailable>' + #13#10 +
    '    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>' + #13#10 +
    '    <IdleSettings>' + #13#10 +
    '      <StopOnIdleEnd>false</StopOnIdleEnd>' + #13#10 +
    '      <RestartOnIdle>false</RestartOnIdle>' + #13#10 +
    '    </IdleSettings>' + #13#10 +
    '    <AllowStartOnDemand>true</AllowStartOnDemand>' + #13#10 +
    '    <Enabled>true</Enabled>' + #13#10 +
    '    <Hidden>false</Hidden>' + #13#10 +
    '    <RunOnlyIfIdle>false</RunOnlyIfIdle>' + #13#10 +
    '    <WakeToRun>false</WakeToRun>' + #13#10 +
    // Runs as long as the user is logged on
    '    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>' + #13#10 +
    // Normal priority: the default (7) is below normal, too low for handling mouse input
    '    <Priority>4</Priority>' + #13#10 +
    '    <RestartOnFailure>' + #13#10 +
    '      <Interval>PT1M</Interval>' + #13#10 +
    '      <Count>3</Count>' + #13#10 +
    '    </RestartOnFailure>' + #13#10 +
    '  </Settings>' + #13#10 +
    '  <Actions Context="Users">' + #13#10 +
    '    <Exec>' + #13#10 +
    '      <Command>' + XmlEscape(ExpandConstant('{app}\{#AppExe}')) + '</Command>' + #13#10 +
    '      <WorkingDirectory>' + XmlEscape(ExpandConstant('{app}')) + '</WorkingDirectory>' + #13#10 +
    '    </Exec>' + #13#10 +
    '  </Actions>' + #13#10 +
    '</Task>' + #13#10;
end;

procedure RegisterTask();
var
  XmlFile: String;
  ResultCode: Integer;
begin
  XmlFile := ExpandConstant('{tmp}\{#TaskName}.xml');
  if not SaveStringToFile(XmlFile, TaskXml(), False) then
  begin
    SuppressibleMsgBox('Could not write the startup task definition.', mbError, MB_OK, IDOK);
    Exit;
  end;

  if not Exec(ExpandConstant('{sys}\schtasks.exe'), '/Create /TN "{#TaskName}" /XML "' + XmlFile + '" /F', '',
      SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    Log(Format('schtasks /Create failed with exit code %d', [ResultCode]));
    SuppressibleMsgBox('Could not register the startup task. {#AppName} will not start at logon.', mbError, MB_OK, IDOK);
    Exit;
  end;

  // Start it now as well, no need to log off first
  RunHidden(ExpandConstant('{sys}\schtasks.exe'), '/Run /TN "{#TaskName}"');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;

  if WizardIsTaskSelected('uiaccess') then
  begin
    // Started from the Run key from now on
    RunHidden(ExpandConstant('{sys}\schtasks.exe'), '/Delete /TN "{#TaskName}" /F');
    RemoveCertificates(NewThumbprint);
    Installed := True;
    // Start it now as well, through ShellExecute for uiAccess
    if not ShellExecAsOriginalUser('', ExpandConstant('{app}\{#AppExe}'), '', ExpandConstant('{app}'), SW_SHOWNORMAL,
        ewNoWait, ResultCode) then
      Log(Format('Could not start the hot corner: %d', [ResultCode]));
  end
  else
  begin
    RegDeleteValue(HKLM, '{#RunKey}', '{#AppName}');
    RemoveCertificates('');
    RegisterTask();
  end;
end;

// A certificate made for an installation that did not finish is not needed
procedure DeinitializeSetup();
var
  Output: TArrayOfString;
begin
  if (NewThumbprint <> '') and not Installed then
    RunUIAccessScript(ExpandConstant('{tmp}\uiaccess.ps1'), '-Remove -Only ' + Quoted(NewThumbprint), Output);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Remove the task first, so nothing starts the hot corner again while it is being stopped
    RunHidden(ExpandConstant('{sys}\schtasks.exe'), '/Delete /TN "{#TaskName}" /F');
    StopHotCorner();
    RemoveCertificates('');
  end;
end;
