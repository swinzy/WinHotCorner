# Task View doesn't open, but the ripple plays

## Scope
- Related Issue: #8
- Affected versions: 1.2-1.3
- OS: Windows Build 22000 or above (e.g. Windows 11, Windows Server 2025)
  - Reproduced on Windows 11 26H2 (build 26300). The reporter runs Windows 11 24H2.
  - Not reproduced on Windows 10 (LTSC 2021, build 19044).

## Trigger conditions
- The hot corner runs **unsigned without `uiAccess`**, elevated or not: the portable version, or an unsigned install (started by its scheduled task, elevated).
- Its **first** call to `WindowSwitcher` comes **more than about 30 seconds after it started**.

A hot corner started at logon is almost always first pushed later than that, so in practice it fails from the first push on.

## Observed behaviour
Once app is started, after 30 seconds, Task View triggering will fail. Animation will still play.

In detail:
- `WindowSwitcher` returns without an error, so nothing is logged and the Win+Tab fallback is not used. Task View simply does not open; the ripple plays.
- It stays that way for every later push, until the hot corner is restarted.
- If the first push comes early, every later one works, also after long breaks and with any window in front.
- The threshold lies between 27 and 36 seconds after the hot corner started.
- Whether the user is idle or working (moving the mouse and typing) during the wait does not matter.

## Investigation

Each test started the hot corner afresh, waited, then pushed into the corner five times with injected input in a desktop session, and checked whether Task View (`XamlExplorerHostIslandWindow`) came to the front.

### First lead: elevated windows (wrong)
The reporter mentioned that 1.1 failed only with high-priority windows such as Task Manager. Pushing into the corner while an elevated packaged app (the new Notepad, run as administrator) was in front seemed to reproduce the bug, and `AllowSetForegroundWindow(ASFW_ANY)` failed in those calls, so the first fix restored the foreground rights by tapping a key whenever that call failed. That fix worked, but not for the stated reason: those tests only failed because more than 30 seconds had passed since the hot corner started. Without any elevated window, just waiting reproduces the bug, and the foreground rights turned out to be missing in calls that worked as well.

### Threshold (Windows 11, 1.3.9 portable, unelevated)

| First push after start | Result |
|---|---|
| 9 s, 16 s, 22 s, 27 s | Task View opens |
| 36 s, 64 s, 66-68 s | 0/5 |

### Matrix (Windows 11, five pushes each)

| Hot corner | First push within 30 s | First push after 30-60 s |
|---|---|---|
| 1.3.9 unelevated (portable or installed) | works | **0/5** |
| 1.3.9 elevated (unsigned install, scheduled task) | works | **0/5** |
| 1.3.9 unelevated, user active during the wait | | **0/5** |
| 1.3.9 signed for `uiAccess` | works | 5/5, idle and active |
| Fix, unelevated | 5/5 | 5/5 (after 37 s, 64-67 s) |
| Fix, elevated | | 5/5 (after 64 s) |
| Fix, pushed early, then again 30 s later | 3/3, then 5/5 | |
| Experiment: `Shell.Application` created at startup, no key tap | | 0/5 unelevated, 0/5 elevated |

On Windows 10, 1.3.9 worked in every case: first push after 60 seconds, unelevated and elevated, 5/5 each, and with five different elevated (non-packaged) apps in front. There, Task View also opened in calls where `AllowSetForegroundWindow` failed.

How the hot corner was started (scheduled task, directly, from a script), where it was (Program Files or portable), and which other windows were open (Edge, Explorer) made no difference.

### Ruled out
- **Elevated windows or packaged apps in front:** not needed; waiting alone is enough.
- **Foreground rights:** `AllowSetForegroundWindow(ASFW_ANY)` fails in most calls that work, too.
- **Creating `Shell.Application` late:** creating it at startup, so that only the first `WindowSwitcher` call comes late, still fails.
- **The caller being unelevated or late in general:** a separate unelevated PowerShell process that first created `Shell.Application` and called `WindowSwitcher` 40 seconds after it started opened Task View every time. The failure is specific to the hot corner's process.

## Possible reasons
Not known. What sets the hot corner apart from the PowerShell caller is its WinForms message loop, the low-level mouse hook and its hidden windows; Windows may also treat a process that has been in the background for a while differently (throttling, for example). None of these has been tested.

## Proposed solutions
> [!IMPORTANT]
> This is a workaround only, not a proper fix.

Tap an unassigned key (`0xE8`) with `SendInput` just before each `WindowSwitcher` call. The foreground app receives a key that means nothing. With it, Task View opens in every case above.

The key is only tapped where the bug is known to occur: on build 22000 or above, when the hot corner has no `uiAccess`. On Windows 10, and with `uiAccess`, nothing is sent.

## Further actions
- Find out why the call stops working, and whether the key tap can be replaced by something that doesn't send input.
