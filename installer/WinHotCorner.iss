; Installer for WinHotCorner (Inno Setup 7)
;
; Build the Release configuration first, then: ISCC.exe WinHotCorner.iss
;
; The hot corner is started at every logon by a scheduled task that runs it with the user's highest privileges,
; so it also works while an elevated window is in the foreground (UIPI), without a UAC prompt.

#define AppName "WinHotCorner"
#define BinDir "..\src\WinHotCorner\bin\Release\net48"
#define AppExe "WinHotCorner.exe"
#define AppVersion GetVersionNumbersString(BinDir + "\" + AppExe)
#define TaskName "WinHotCorner"

[Setup]
AppId={{1F9F1765-5178-4A09-9EA9-34E77B75CC21}
AppName={#AppName}
AppVersion={#AppVersion}
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
OutputBaseFilename=WinHotCorner-HotCornerOnly-{#AppVersion}-setup
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
; Stopping the running hot corner is done in the code below
CloseApplications=no

[Files]
Source: "{#BinDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BinDir}\ConfigManager.dll"; DestDir: "{app}"; Flags: ignoreversion

[Code]
const
  NetFx48Release = 528040;

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
  StopHotCorner();
  Result := '';
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
begin
  if CurStep = ssPostInstall then
    RegisterTask();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Remove the task first, so nothing starts the hot corner again while it is being stopped
    RunHidden(ExpandConstant('{sys}\schtasks.exe'), '/Delete /TN "{#TaskName}" /F');
    StopHotCorner();
  end;
end;
