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
| `installer` | Inno Setup scripts for both installers, and `build.ps1`. |

Despite the old name "service worker", the hot corner is not a Windows service: services run in session 0 and cannot see the user's mouse. It is a normal program in the user's session.

## Detecting a push into the corner

The goal is to feel the same as GNOME, so the logic follows GNOME Shell (`js/ui/layout.js`) and mutter (`src/backends/native/meta-barrier-native.c`).

### Mouse hook

A low-level mouse hook (`WH_MOUSE_LL`, `MouseHook.cs`) sees where the pointer is *about to* go, after pointer speed and acceleration but before the edge of the screen stops it. When the pointer is pushed against an edge, that overshoot is how hard it is being pushed, in screen pixels. It is the same quantity GNOME's pointer barriers report.

Raw Input is not used: it gives the mouse's counts before pointer speed and acceleration, which do not match how far the pointer would move on screen, and remote desktops and tablets send absolute positions without any movement to measure.

The hook sits in the path of every mouse event in the system, so its handler does almost nothing: movements far from a corner return without even a system call, and the actual trigger is posted to the message loop. Windows silently removes a hook whose handler is too slow; a watchdog checks every 2 seconds whether the pointer moved without the hook seeing it, and installs the hook again.

### Pressure (GNOME's `PressureBarrier` rules)

On GNOME, invisible pointer barriers along the edges at the corner stop the pointer, and GNOME Shell's `PressureBarrier` measures how hard it is pushed against them. WinHotCorner has no barriers of its own: the edges of the screen stop the pointer, as they always do, and `CornerPressure.cs` applies GNOME's rules to how far the pointer would have gone past them. Two edges count, the left one and the top one, each up to 32 logical pixels from the corner (GNOME's barriers are as long as its top bar is tall):

- A movement that would take the pointer out past an edge, to a point that is on no screen, adds pressure. If the point is on another screen, nothing stops the pointer: it just goes there. (`IsStopped` is the one place that decides whether the pointer is stopped, so something else that holds it, such as a `ClipCursor` barrier, could be added there.)
- A movement more along the edge than out past it does not count, so sliding along the edge does not trigger.
- Each movement adds at most 15. Pressure older than 1 second is forgotten. When the total reaches the threshold (100 by default, as on GNOME), the corner triggers; a single movement of the full threshold triggers at once.
- So a steady push triggers, even a slow one, but nudges with pauses of over a second do not.
- After triggering, the corner waits until the pointer has *left* both edges. As with mutter's barriers, the pointer only leaves an edge when it is more than 2 logical pixels away from it (or past the 32 pixels), so the small recoil of the hand after a push does not re-arm the corner.

GNOME's numbers are in logical pixels. The hot corner is per-monitor DPI aware (`app.manifest`), so the hook and the monitor bounds are in physical pixels, and the numbers are scaled by each monitor's display scale.

### Multiple monitors

`DisplayLayout.cs` lists the monitors and keeps those whose top-left corner is free: neither the pixel just left of the corner nor the pixel just above it is on another monitor. This is GNOME's test. GNOME also always keeps the primary monitor's corner and stops the pointer there with a barrier; Windows has no pointer barriers, so a covered corner cannot be hit, primary or not. The list is rebuilt on display, DPI and setting changes.

### When it does not trigger

- An app is fullscreen on that corner's monitor (if *Disable when fullscreen* is on). Task View itself is exempt, so pushing again closes it.
- A mouse button is held (if *Disable when mouse button is down* is on).
- Shift, Ctrl, Alt or Win is held, so it does not mix into a shortcut the user is pressing.

## Opening Task View

Windows has no public function to open Task View, so the hot corner sends Win+Tab with `SendInput` (`TaskView.cs`), as one uninterrupted sequence. If Windows accepts only part of it, the keys that went down are released, with an unassigned key (`0xE8`) tapped first so that a lone Win release does not open the Start menu.

## Configuration

Settings are DWORD values in `HKEY_CURRENT_USER\Software\WinHotCorner`, written by the control panel:

| Value | Range | Default | |
|---|---|---|---|
| `Enabled` | 0 or 1 | 1 | 0 makes the hot corner exit, at startup or while running |
| `PressureThreshold` | 10 to 1000 | 100 | Pressure needed to trigger, in logical pixels |
| `DisableWhenFullscreen` | 0 or 1 | 1 | |
| `DisableWhenMouseDown` | 0 or 1 | 1 | |

A missing value means the default. The same values in `HKEY_LOCAL_MACHINE\Software\Policies\WinHotCorner` (Group Policy) override the user's; the control panel shows them greyed out.

The hot corner usually runs elevated while anything running as the user can write HKCU, so values are read strictly: a value that is not a DWORD in range is ignored, the default is used and the reason is logged. Changes apply immediately: the hot corner watches both keys with `RegNotifyChangeKeyValue` (`RegistryWatcher.cs`), and opens the user's key again if it is deleted.

The alpha versions read `%LOCALAPPDATA%\WinHotCorner\config.xml`; it is no longer used.

### Log

Errors are appended to `%LOCALAPPDATA%\WinHotCorner\WinHotCorner.log` (moved to `.old` at 1 MB). All messages also go to the debug output, which [DebugView](https://learn.microsoft.com/sysinternals/downloads/debugview) shows, in Release builds too; Debug builds also have a console window.

## Startup and privileges

The installer registers a scheduled task, `WinHotCorner`, that starts the hot corner at every user's sign-in:

- It runs as that user with their highest privileges (`BUILTIN\Users`, `HighestAvailable`). Windows does not let a normal program send input to, or see the mouse over, an elevated window (UIPI), so for administrators the hot corner runs elevated, without a UAC prompt, and also works with Task Manager in front. Standard users get it with their normal rights.
- No time limit, not stopped on battery, normal priority (the task default is below normal), restarted if it fails.
- Users may read and run the task, so the unelevated control panel can start the hot corner the same way.

Because it runs elevated at every sign-in, it must live where only administrators can write: the install folder is fixed to `Program Files\WinHotCorner`.

`uiAccess` (which would allow the same without elevation) is not used: it needs a trusted code signing certificate, and an unsigned `uiAccess` program does not start at all.

Only one hot corner runs per session (a named mutex).

## Control panel

`HotCornerControl` in ConfigManager holds what both sides share:

- `MUTEX_NAME`: held by the running hot corner. `IsRunning()` checks it; *access denied* also means it is running (the hot corner is elevated, the control panel is not).
- `EXIT_EVENT_NAME`: setting this event asks the hot corner to exit (`RequestExit()`). The hot corner creates it with access for the current user, otherwise its unelevated programs could not open it.
- `TASK_NAME`: the scheduled task. Turning the hot corner on in the control panel writes `Enabled = 1` and runs the task, so it starts elevated; if the task cannot be run, the exe is started directly (not elevated). Turning it off just writes `Enabled = 0`.

The control panel writes only the value that changed, so it never copies a Group Policy value into the user's settings.

## Installers

Both are Inno Setup 7 scripts in `installer`, published as two downloads:

- `WinHotCorner.iss` builds **`WinHotCorner-HotCornerOnly-<version>-setup.exe`**: the hot corner. Installs to `Program Files\WinHotCorner`, registers the scheduled task and starts it. Before installing, upgrading or uninstalling it stops the hot corner in every session and waits until it has exited. Uninstalling keeps the user's settings.
- `ControlPanel.iss` builds **`WinHotCorner-Full-<version>-setup.exe`**: the control panel, in `Program Files\WinHotCorner\ControlPanel`, with a Start menu shortcut. It contains the hot corner installer and runs it when the hot corner is missing or older, so it can install both. Each product has its own entry in Installed apps and can be uninstalled alone.

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
- **Releases** are the exception: always the next build number, revision 0, even if the commit was built before. So a release is named with three numbers: tag `v1.1.57`, `WinHotCorner-Full-1.1.57-setup.exe`, and `1.1.57` in Installed apps. A release run cannot be re-run (start a new one, which gets a new number), and a commit cannot be released twice. The release is created as a draft, checked, and only then published, so a failed run never leaves an empty release.
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

GitHub Actions (`.github/workflows/build.yml`) builds both installers on every push and pull request; they can be downloaded from the run. Running the workflow by hand on `main` with *Publish a release* ticked publishes a release.

- **Version**: worked out by `installer\version.ps1`, see [Versions](#versions). A local build is always `<major>.<minor>.0.0`.
- **Visual Studio** is not needed. The control panel needs Windows to build (WinUI's XAML compiler); the hot corner and ConfigManager also build on Linux or macOS with the .NET SDK, e.g. `dotnet build src/WinHotCorner/WinHotCorner.csproj`.
- The control panel is trimmed and references only the WinUI part of the Windows App SDK (the full package adds AI, ML and Search, over 200 MB). `EnableMsixTooling` is needed even without MSIX, or publish leaves out the `.pri` resources and the app crashes at start.
- **Line endings**: `.gitattributes` stores text files with LF; C#, project, XAML, Inno Setup and PowerShell files are checked out with CRLF.

## References

- GNOME Shell, [`js/ui/layout.js`](https://gitlab.gnome.org/GNOME/gnome-shell/-/blob/main/js/ui/layout.js): `LayoutManager._updateHotCorners`, `HotCorner`, `PressureBarrier`.
- mutter, [`src/backends/native/meta-barrier-native.c`](https://gitlab.gnome.org/GNOME/mutter/-/blob/main/src/backends/native/meta-barrier-native.c): when a barrier counts as hit and left (the 2 px hit box).
- [LowLevelMouseProc](https://learn.microsoft.com/windows/win32/winmsg/lowlevelmouseproc), [SendInput](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput), [RegNotifyChangeKeyValue](https://learn.microsoft.com/windows/win32/api/winreg/nf-winreg-regnotifychangekeyvalue).
