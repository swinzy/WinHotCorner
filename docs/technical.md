# Technical notes

How WinHotCorner works, how it is configured and started, and how to build it.

- [Components](#components)
- [Detecting a push into the corner](#detecting-a-push-into-the-corner)
- [Opening Task View](#opening-task-view)
- [Configuration](#configuration)
- [Startup and privileges](#startup-and-privileges)
- [Control panel](#control-panel)
- [Installers](#installers)
- [Versions](#versions)
- [Building](#building)
- [References](#references)

## Components

| Project | What it is |
|---|---|
| `src/WinHotCorner` | The hot corner, `WinHotCorner.exe`. .NET Framework 4.8 (comes with Windows 10 1903 and later), no third-party dependencies. No visible window: a hidden window receives display changes, and the program runs as long as the user is signed in. |
| `src/ConfigManager` | Shared library: reads and writes the settings, and the names both programs use to talk to each other (`HotCornerControl`). Built for `net48` and `net10.0-windows`. |
| `src/ControlPanel` | WinHotCorner Control Panel, `WinHotCornerControlPanel.exe`. WinUI 3, unpackaged and self-contained. Optional: the hot corner works without it. |
| `installer` | Inno Setup scripts for both installers, `build.ps1`, and `uiaccess.ps1`, which signs the hot corner on the user's computer (see [Startup and privileges](#startup-and-privileges)). |

Despite the old name "service worker", the hot corner is not a Windows service: services run in session 0 and cannot see the user's mouse. It is a normal program in the user's session.

## Detecting a push into the corner

The goal is to feel the same as GNOME, so the logic follows GNOME Shell (`js/ui/layout.js`) and mutter (`src/backends/native/meta-barrier-native.c`).

### Mouse hook

A low-level mouse hook (`WH_MOUSE_LL`, `MouseHook.cs`) sees where the pointer is *about to* go, after pointer speed and acceleration but before the edge of the screen stops it. When the pointer is pushed against an edge, that overshoot is how hard it is being pushed, in screen pixels. It is the same quantity GNOME's pointer barriers report.

Raw Input is not used: it gives the mouse's counts before pointer speed and acceleration, which do not match how far the pointer would move on screen, and remote desktops and tablets send absolute positions without any movement to measure.

The hook sits in the path of every mouse event in the system, so its handler does almost nothing: movements far from a corner return without even a system call, and the actual trigger is posted to the message loop. Windows silently removes a hook whose handler is too slow; a watchdog checks every 2 seconds whether the pointer moved without the hook seeing it, and installs the hook again.

### Pressure (GNOME's `PressureBarrier` rules)

On GNOME, invisible pointer barriers along the edges at the corner stop the pointer, and GNOME Shell's `PressureBarrier` measures how hard it is pushed against them. WinHotCorner has no barriers of its own: the edges of the screen stop the pointer, as they always do, and `CornerPressure.cs` applies GNOME's rules to how far the pointer would have gone past them. Two edges count, the left one and the top one, each up to 32 logical pixels from the corner (GNOME's barriers are as long as its top bar is tall):

- A movement that would take the pointer out past an edge adds pressure if the pointer stays: when the point is on no screen, the edge of the screen stops it. When the point is on another screen, the pointer usually just goes there, but near the corner Windows may hold it (see [Multiple monitors](#multiple-monitors)). The hook cannot tell which: such a movement is kept until the next event, and counts only if the pointer has not moved past the edge by then.
- A movement more along the edge than out past it does not count, so sliding along the edge does not trigger.
- Each movement adds at most 15. Pressure older than 1 second is forgotten. When the total reaches the threshold (100 by default, as on GNOME), the corner triggers; a single movement of the full threshold triggers at once.
- So a steady push triggers, even a slow one, but nudges with pauses of over a second do not.
- After triggering, the corner waits until the pointer has *left* both edges. As with mutter's barriers, the pointer only leaves an edge when it is more than 2 logical pixels away from it (or past the 32 pixels), so the small recoil of the hand after a push does not re-arm the corner.

GNOME's numbers are in logical pixels. The hot corner is per-monitor DPI aware (`app.manifest`), so the hook and the monitor bounds are in physical pixels, and the numbers are scaled by each monitor's display scale.

### Multiple monitors

`DisplayLayout.cs` lists the monitors and decides which corners count, by the *Hot corner screens* setting (`Screens`). A corner is covered when another monitor is directly to its left or above it: the pixel just left of the corner or the pixel just above it is on another monitor. This is GNOME's test. The default is GNOME's choice: the primary monitor's corner, and every corner that is not covered. The list is rebuilt on display, DPI and setting changes.

When Windows' display language is written right to left (Arabic, Hebrew and others; `LOCALE_IREADINGLAYOUT` of the user's UI language, `InterfaceDirection` in ConfigManager), every corner is the top-right one instead, as on GNOME, which goes by its interface language. GNOME moves its Activities button to the top right then, but Windows has no top bar to follow, so `MirrorForRightToLeft` (on by default; the control panel shows it only in a right-to-left language) can keep the top-left corner. Covered then means another monitor directly to the right or above. GNOME's points for that test look wrong right to left (`monitor.x + 1` beside the corner, and one pixel too far right above it); WinHotCorner uses the pixels next to the corner. `CornerPressure` mirrors positions around a top-right corner, so the rules above apply with the right edge in place of the left one, and the ripple is mirrored too (GNOME's `.ripple-box:rtl`).

GNOME stops the pointer at a covered primary corner with a pointer barrier. Windows has no pointer barriers, but it has *sticky corners*: at the ends of an edge shared by two monitors, it holds the pointer for a few pixels (`MouseCornerClipLength` in `HKEY_CURRENT_USER\Control Panel\Desktop`, 6 when not set, 0 turns it off). So with two monitors of the same height side by side, pushing left within the top few pixels of the right monitor's corner does not cross over. Measured on Windows 11 with the right monitor at 200%: the pointer was held at the top 6 rows every time, crossed at the 7th, and diagonal flicks into the corner were held too; the 6 are physical pixels. Meanwhile the hook reports points on the left monitor, so the hot corner only learns that the pointer was held at the next event (see above). Monitors stacked vertically work the same way: the lower monitor's corner is held when pushing up within the clip length of its left edge, and a push further right goes on to the upper monitor. When the left monitor sits a little lower than the right one, the right monitor's corner is not covered at all (nothing is just left of it), and works like any free corner; the sticky corner is there too. Where Windows does not hold the pointer (the clip length is 0), a covered corner simply never triggers. The registry value is only logged: it may not be what is in effect, as Explorer reads it when it starts.

### When it does not trigger

- An app is fullscreen on that corner's monitor (if *Disable when fullscreen* is on). Task View itself is exempt, so pushing again closes it.
- A mouse button is held (if *Disable when mouse button is down* is on).

## Opening Task View

`TaskView.cs` asks the shell to open Task View: `Shell.Application`'s `WindowSwitcher` (`IShellDispatch5`), a documented function that opens Task View on Windows 10 and 11, or closes it when it is open, as pushing into the corner again should. It is as fast as Win+Tab (about 40 ms until Task View is in front, measured on Windows Server 2025), starts no process, and sends no keystroke, so a key the user holds does not matter and UIPI does not stand in the way. The object is created once and kept; if a call fails (Explorer restarted, for example), it is created again once.

If the shell cannot do it, the hot corner sends Win+Tab with `SendInput` instead, as one uninterrupted sequence, unless Shift, Ctrl, Alt or Win is held (it would mix into a shortcut the user is pressing). If Windows accepts only part of it, the keys that went down are released, with an unassigned key (`0xE8`) tapped first so that a lone Win release does not open the Start menu.

## Ripple

When Task View opens, the corner plays GNOME's ripple (`Ripple.cs`, a port of GNOME Shell's `js/ui/ripples.js`): three quarter circles grow out of the corner and fade away within about 1.4 s, drawn in the `.ripple-box` style of GNOME's theme (white at 20 %, 52 logical pixels, with a soft edge). It is skipped when Windows animations are turned off (*Animation effects* in Settings).

The ripple is a click-through layered window that never takes the focus or shows up in Task View. Task View covers ordinary topmost windows, though; only a window of a program with `uiAccess` stays above it. So the ripple shows above Task View only when the hot corner was installed signed for `uiAccess` (see [Startup and privileges](#startup-and-privileges)); otherwise it plays under Task View and is mostly hidden by it. The hot corner does not need to know which case it is in.

## Configuration

Settings are DWORD values in `HKEY_CURRENT_USER\Software\WinHotCorner`, written by the control panel:

| Value | Range | Default | |
|---|---|---|---|
| `Enabled` | 0 or 1 | 1 | 0 makes the hot corner exit, at startup or while running |
| `PressureThreshold` | 10 to 1000 | 100 | Pressure needed to trigger, in logical pixels |
| `MirrorForRightToLeft` | 0 or 1 | 1 | In a right-to-left display language, use the top-right corner (as GNOME) instead of the top-left one |
| `Screens` | 0 to 3 | 0 | Which screens have a hot corner: 0 the primary screen and every screen whose corner is not covered (GNOME), 1 the primary screen only, 2 only screens whose corner is not covered, 3 all screens |
| `DisableWhenFullscreen` | 0 or 1 | 1 | |
| `DisableWhenMouseDown` | 0 or 1 | 1 | |

A missing value means the default. The same values in `HKEY_LOCAL_MACHINE\Software\Policies\WinHotCorner` (Group Policy) override the user's; the control panel shows them greyed out.

The hot corner usually runs elevated while anything running as the user can write HKCU, so values are read strictly: a value that is not a DWORD in range is ignored, the default is used and the reason is logged. Changes apply immediately: the hot corner watches both keys with `RegNotifyChangeKeyValue` (`RegistryWatcher.cs`), and opens the user's key again if it is deleted.

The alpha versions read `%LOCALAPPDATA%\WinHotCorner\config.xml`; it is no longer used.

### Log

Errors are appended to `%LOCALAPPDATA%\WinHotCorner\WinHotCorner.log` (moved to `.old` at 1 MB). All messages also go to the debug output, which [DebugView](https://learn.microsoft.com/sysinternals/downloads/debugview) shows, in Release builds too; Debug builds also have a console window.

## Startup and privileges

Windows does not let a normal program send input to, or see the mouse over, an elevated window (UIPI). There are two ways around that, and the installer offers both: it asks before installing whether to sign WinHotCorner on this computer (the `uiaccess` task, on by default). Only the chosen version is installed. Signed is the better choice where it works: the hot corner then runs without elevation; tested on Windows 11 with an administrator account, it still triggers with an elevated Task Manager in front.

**Usually: elevated, from a scheduled task.** The installer registers a scheduled task, `WinHotCorner`, that starts the hot corner at every user's sign-in:

- It runs as that user with their highest privileges (`BUILTIN\Users`, `HighestAvailable`), so for administrators the hot corner runs elevated, without a UAC prompt, and also works with Task Manager in front. Standard users get it with their normal rights, so for them it does not trigger in front of elevated windows.
- No time limit, not stopped on battery, normal priority (the task default is below normal), restarted if it fails.
- Users may read and run the task, so the unelevated control panel can start the hot corner the same way.

**Signed: `uiAccess`, from the Run key.** A program whose manifest asks for `uiAccess` may send input to and stay above any window, elevated ones and Task View included, without being elevated itself, so it also works for standard users and its ripple shows above Task View. Windows starts such a program only if it is signed by a trusted certificate and installed in a protected folder such as Program Files; otherwise it does not start at all. The manifest is part of the program, so this is a second build of the hot corner (`-p:UIAccess=true`, built into `bin\UIAccess`).

The project has no certificate from a certificate authority, so the installer makes one on the user's computer (`installer\uiaccess.ps1`, run elevated):

1. It makes a self-signed certificate for code signing only, adds it to the computer's trusted root certificates, signs the hot corner with it and deletes the private key straight away. Without the key nothing else can ever be signed with that certificate, so trusting it trusts only this one file. A certificate shared by all users would not do: a trusted root covers everything its key signs, and a self-signed certificate cannot be revoked if the key leaks.
2. The signing happens in Setup's temporary folder, which only administrators can change, before anything is installed. If it fails (a policy may not allow new root certificates, for example), Setup says so and installs the usual version instead, and records the task as not chosen, so updates do not try again.
3. The signature has no timestamp, so it is valid only while the certificate is; the certificate lasts 100 years.
4. Every update replaces the program, so it is signed again with a new certificate, and the old one is removed. Switching back (running Setup again without the task) and uninstalling remove the certificate too, so none is left behind.

Task Scheduler cannot start a `uiAccess` program (error 740), but Explorer starts the entries of the `Run` key through `ShellExecute`, which grants `uiAccess`: the installer adds `WinHotCorner` to `HKEY_LOCAL_MACHINE\...\CurrentVersion\Run` instead of the task. Unlike the task, nothing restarts the hot corner if it crashes; the control panel then shows that it is not running and offers to run it. Users can also turn it off in Task Manager's startup apps.

Either way it starts at every sign-in, and the hot corner turns itself off at once when `Enabled` is 0: turning the hot corner off in the control panel also keeps it from running at sign-in.

The hot corner logs at startup whether it is elevated and whether it has `uiAccess`. Because it can run elevated at every sign-in, it must live where only administrators can write: the install folder is fixed to `Program Files\WinHotCorner`.

Only one hot corner runs per session (a named mutex).

## Control panel

`HotCornerControl` in ConfigManager holds what both sides share:

- `MUTEX_NAME`: held by the running hot corner. `IsRunning()` checks it; *access denied* also means it is running (the hot corner is elevated, the control panel is not).
- `EXIT_EVENT_NAME`: setting this event asks the hot corner to exit (`RequestExit()`). The hot corner creates it with access for the current user, otherwise its unelevated programs could not open it.
- `TASK_NAME`: the scheduled task. Turning the hot corner on in the control panel writes `Enabled = 1` and runs the task, so it starts elevated. Without a task (signed for `uiAccess`, or the task is gone) it starts the exe through `ShellExecute`, which grants `uiAccess` to the signed version; the usual version then runs without elevation until the next sign-in. Turning it off just writes `Enabled = 0`. *Run now* next to *Not running* starts it the same way.

The control panel writes only the value that changed, so it never copies a Group Policy value into the user's settings.

In a right-to-left display language the control panel is laid out right to left, as Windows' own apps are: WinUI does not do that by itself, so it sets `WS_EX_LAYOUTRTL` on the window (the title bar) and `FlowDirection` on the content; the logo keeps its direction.

## Installers

Both are Inno Setup 7 scripts in `installer`, published as two downloads:

- `WinHotCorner.iss` builds **`WinHotCorner-HotCornerOnly-<version>-setup.exe`**: the hot corner. Installs to `Program Files\WinHotCorner`, registers the scheduled task, or signs it and adds it to the Run key with the `uiaccess` task (see [Startup and privileges](#startup-and-privileges)), and starts it. Inno Setup remembers the task, so an update keeps the choice. Before installing, upgrading or uninstalling it stops the hot corner in every session and waits until it has exited. Uninstalling keeps the user's settings.
- `ControlPanel.iss` builds **`WinHotCorner-Full-<version>-setup.exe`**: the control panel, in `Program Files\WinHotCorner\ControlPanel`, with a Start menu shortcut. It contains the hot corner installer and runs it silently when the hot corner is missing or older, so it can install both. It offers the same `uiaccess` task, starting from what the hot corner installer recorded (Inno Setup keeps both the chosen and the declined tasks; without either, from a version before the task, the default applies, as in the hot corner installer), passes the choice on (`/MERGETASKS`), and also runs the hot corner installer when the choice changes. A silent run without tasks on the command line keeps the hot corner's choice. If signing failed, it says so once the hot corner installer has finished. Each product has its own entry in Installed apps and can be uninstalled alone.

## Translations

The control panel and the installers are in English, Simplified Chinese, Traditional Chinese and Spanish (Latin American and Spain's), in the language of Windows' display language; anything else gets English.

- **Control panel**: `src/ControlPanel/Strings/<language>/Resources.resw`. `en` is in the project's Australian spelling and is the default language (`DefaultLanguage`), so it also covers every other English and the languages without a translation; `en-US` is the same in US spelling. `zh-Hans` and `zh-Hant`: the script tags also cover Singapore, Hong Kong and Macao. `es` is Latin American Spanish, for every Spanish but Spain's; `es-ES` is Spain's (ratón, pulsado, vídeo, and the present perfect). XAML takes its text through `x:Uid` (`<Uid>.Text`, `<Uid>.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name` and so on), code through MRT Core's `ResourceLoader`: the window title, On and Off, the status line and the *Hot corner screens* choices (with separate top-right wording for right-to-left languages).
- **Installers**: `installer/Languages.iss`, included by both scripts: the `[Languages]`, with Inno Setup's own translations of its text, and `[CustomMessages]` for WinHotCorner's text (one English, in Australian spelling). Inno Setup has one Spanish; `SpanishLatinAmerica.isl` gives it the language ID of Spanish (Mexico) for the Latin American entry. Without an exact match Setup takes the first entry with the same primary language, so the order matters: Traditional Chinese before Simplified (Hong Kong and Macao get Traditional, Singapore too), Latin American Spanish before Spain's. Setup picks the language without asking (`ShowLanguageDialog=no`); `/LANG=` chooses one.

A new language needs a `Resources.resw` folder and a block of messages in `Languages.iss`. The `en-US` file is the `en` one with US spelling, and `es-ES` the `es` one in Spain's Spanish: change both. The hot corner itself shows no text.

## Versions

All three projects share one version, `major.minor.build.revision`. It says which version is being developed: every `1.1.x.x` is a development build of 1.1, and the release of 1.1 is simply the last build of that line.

| Part | Meaning |
|---|---|
| Major | `VersionMajor` in `src/Directory.Build.props`, changed by hand. After changing it to 2, the next build is `2.0.1.0`. |
| Minor | The development line of that major: 0 until its first release, then one more after each release. |
| Build | Builds since the line started, counted by GitHub Actions; a failed build counts too. A new line starts at 1. |
| Revision | How many times the same commit was built before (the code is the same, the binaries may not be). |

For example: after 1.0 is released, 1.1 is developed as `1.1.1.0`, `1.1.2.0`, …; releasing it gives `1.1.57`; then 1.2 starts at `1.2.1.0`.

`installer\version.ps1` works it out:

- **Build number**: the workflow's run number minus the run number where the line started. The run number counts across the whole repository, so branches, deleted branches, squash merges and rewritten history cannot give two builds the same number. Numbers can be skipped, never reused.
- **Where a line starts** is kept in annotated tags `metadata/line-<major>.<minor>`, with `baseline-run: <run number>` in their message. They are not code versions; do not delete or move them. The release run tags the next line; the first build after changing the major version tags `<major>.0`.
- **Revision**: GitHub's API tells whether the same commit was built before in this line; if so, the build keeps that build number and the revision counts the earlier builds, re-runs included. (Two runs of one commit at the very same time could get the same revision.) Pull request runs build a temporary merge commit, which is always new.
- **Releases** are the exception: always the next build number, even if the commit was built before, and named with three numbers: tag `v1.1.57`, `WinHotCorner-Full-1.1.57-setup.exe`, and `1.1.57` in Installed apps. If a release run fails for reasons other than the code (a download, GitHub itself) it is re-run: the build number stays, the revision counts the attempts (the files say `1.1.57.1`), and the release notes say why the earlier attempts failed. A commit cannot be released twice.
- **Local builds** are always `<major>.<minor>.0.0`: anyone can build locally, so a local build cannot have a number that only ever grows, and 0 never appears in an official build. A local build is older than any CI build of its line, so installing the *Full* installer of a local build over a CI build leaves the hot corner as it is (uninstall it first, or use *HotCornerOnly*, which always installs).

The four numbers are the file and assembly version (Windows allows at most 65535 for each; the build stops if one is larger). The product version string and Installed apps add where a build comes from:

| | Installed apps | Product version |
|---|---|---|
| Release | `1.1.57` | `1.1.57.0+3f2a9c1` |
| GitHub Actions | `1.1.56.2+3f2a9c1` | same |
| Local | `1.1.0.0+3f2a9c1.local.a91c03be` | same |

After `+` come the commit, then for a local build `local` and a random id that tells local builds apart, and `devel` when the working tree has uncommitted changes.

## Building

On Windows, with the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Inno Setup 7](https://jrsoftware.org/isinfo.php):

```powershell
installer\build.ps1
```

It builds the hot corner, publishes the control panel and writes both installers to `installer\Output`. It needs git, for the version.

GitHub Actions (`.github/workflows/build.yml`) builds both installers on every push and pull request; they can be downloaded from the run. A push of a commit that is already released, such as `devel` fast-forwarded to `main` after a release, builds nothing.

### Releases

`main` only takes pull requests whose build passed, and **every push to `main`, i.e. every merged pull request, is a release**, so `main` is always a released version. The release run:

1. builds both installers as a release (see [Versions](#versions)),
2. creates a draft release with both installers (nobody sees a draft, and GitHub creates the `v` tag only when it is published), checks that both are there, and publishes it, so a failed run never leaves an empty release,
3. tags the start of the next development line.

The release notes are Markdown: the pull request's title (as a `##` heading) and description, then, after a line, why earlier attempts failed if the run was re-run (`Rev. 1:` …), a tip on which installer to download, and a link to the changes since the previous release. Without a pull request, GitHub's generated notes take the place of the title and description.

If a release run fails, re-run it. When an earlier attempt had already published the release, a re-run only finishes the remaining steps. When a run can no longer be re-run (after 30 days), running the workflow by hand on `main` releases the commit with a new build number.

- **Version**: worked out by `installer\version.ps1`, see [Versions](#versions). A local build is always `<major>.<minor>.0.0`.
- **Visual Studio** is not needed. The control panel needs Windows to build (WinUI's XAML compiler); the hot corner and ConfigManager also build on Linux or macOS with the .NET SDK, e.g. `dotnet build src/WinHotCorner/WinHotCorner.csproj`.
- The control panel is trimmed and references only the WinUI part of the Windows App SDK (the full package adds AI, ML and Search, over 200 MB). `EnableMsixTooling` is needed even without MSIX, or publish leaves out the `.pri` resources and the app crashes at start.
- **Line endings**: `.gitattributes` stores text files with LF; C#, project, XAML, Inno Setup and PowerShell files are checked out with CRLF.

## References

- GNOME Shell, [`js/ui/layout.js`](https://gitlab.gnome.org/GNOME/gnome-shell/-/blob/main/js/ui/layout.js): `LayoutManager._updateHotCorners`, `HotCorner`, `PressureBarrier`; [`js/ui/ripples.js`](https://gitlab.gnome.org/GNOME/gnome-shell/-/blob/main/js/ui/ripples.js) and the `.ripple-box` style: the ripple.
- mutter, [`src/backends/native/meta-barrier-native.c`](https://gitlab.gnome.org/GNOME/mutter/-/blob/main/src/backends/native/meta-barrier-native.c): when a barrier counts as hit and left (the 2 px hit box).
- [LowLevelMouseProc](https://learn.microsoft.com/windows/win32/winmsg/lowlevelmouseproc), [SendInput](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput), [RegNotifyChangeKeyValue](https://learn.microsoft.com/windows/win32/api/winreg/nf-winreg-regnotifychangekeyvalue).
