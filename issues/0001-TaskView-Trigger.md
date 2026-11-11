# Unsigned version workaround: Task View doesn't open due to Windows foreground lock

## Scope
- Related Issue: #8
- Affected versions: 1.2-1.3; 1.4.5 on Windows 10 (see [Implemented fix](#implemented-fix))
- OS: Windows 10, Windows 11 and Windows Server 2025
  - Reproduced on Windows 10 (LTSC 2021, build 19044), Windows 11 26H2 (build 26300) and Windows Server 2025 (build 26100). The reporter runs Windows 11 24H2.
- Not a WinHotCorner bug as such: any process without `uiAccess` that calls `WindowSwitcher` behaves the same (see [Minimal reproduction](#minimal-reproduction)).

## Trigger conditions
- The hot corner runs **without `uiAccess`**, elevated or not: the portable version, or an unsigned install. A hot corner signed for `uiAccess` is not affected.
- **The user types in another program** (keyboard input to the app in front), and the hot corner has not sent any input itself since it started.

As users type all the time, an affected hot corner fails almost always in practice.

## Observed behaviour
Once another program has received keyboard input, Task View triggering will fail. Animation will still play.

In detail:
- `WindowSwitcher` returns without an error, so nothing is logged and the Win+Tab fallback is not used. Task View simply does not open; the ripple plays.
- It does not depend on how long the hot corner has run, or on the hot corner being elevated. An elevated window in front is not needed to cause it.
- Not part of this issue: while an elevated window is in front, a hot corner that is neither elevated nor has `uiAccess` does not trigger at all; not even the ripple plays. Windows (UIPI) keeps such windows apart from lower programs, so this is expected; a hot corner signed for `uiAccess`, or elevated, triggers there too.
- The state lasts, also for a hot corner started afresh, until one of these happens:
  - **Task View is opened while the desktop is in front** (all windows minimised, or Win+D), as the reporter found. Afterwards it opens from any window again, until the user types in another program again.
  - **The hot corner sends input itself**, such as a key tap. Afterwards it opens every time, **even after the user types again**.
  - On Windows 10 only: **no input for longer than the foreground lock timeout** (200 seconds by default). Windows 11 still failed after 210 seconds.
- Win+D is keyboard input as well, but goes to the shell and does not cause the state. Mouse movement doesn't either; mouse clicks have not been tested.

## Minimal reproduction
Without WinHotCorner, in PowerShell:

```powershell
Start-Sleep 5; (New-Object -ComObject Shell.Application).WindowSwitcher()
```

Press Enter, switch to Notepad within 5 seconds and type a few letters: Task View does not open. Switch to Notepad without typing, or to the desktop, and it opens. (Typing the command into the console and running it straight away does not reproduce it: the console itself has then received the last keyboard input.)

Tested with a background PowerShell process that has no window and sends no input unless told to, Notepad in front, and real keyboard and mouse input (from a device, not `SendInput`). "Rights" is what `AllowSetForegroundWindow(ASFW_ANY)` returned just before the call. Windows 10 and Windows 11, each with an unelevated and an elevated caller; **all four gave the same result**:

| Step | Task View | Rights |
|---|---|---|
| A: no typing yet | 5/5 | false |
| B: after typing into Notepad | 0/5 | false |
| C: after Win+D (desktop in front) | 3/3 | false |
| D: Notepad in front again, no typing | 3/3 | false |
| E: after typing into Notepad | 0/3 | false |
| F: the caller taps `0xE8` with `SendInput` before the first call, not before the others | 5/5 | false before the tap, then true |
| G: after typing into Notepad | 3/3 | true |

`WindowSwitcher` returned without an error in every call.

## Investigation

### Method
Each test started the hot corner (or caller) afresh with Notepad in front, then pushed into the top-left corner and checked whether Task View came to the front within about 2 seconds; when it did, it was closed with Esc. Two kinds of input were used:
- **Injected:** a test script in the same desktop moved the mouse and typed with `SendInput`. Windows marks this input as injected and attributes it to the test script.
- **Device:** real keyboard and mouse events (a relative mouse for the pushes), as from physical devices. The test script only observed the foreground window and sent nothing.

Both kinds of input gave the same results.

### Earlier leads (wrong)
- **Elevated windows:** the reporter mentioned that 1.1 failed only with high-priority windows such as Task Manager. Pushing while an elevated packaged app (the new Notepad, run as administrator) was in front seemed to reproduce the bug, and `AllowSetForegroundWindow(ASFW_ANY)` failed in those calls, so missing foreground rights looked like the cause. The bug occurs without any elevated window, though, and calls also work without those rights (step A above).
- **Time since start:** in a series of tests on Windows 11 with injected input, the first push worked up to 27 seconds after the hot corner started and failed from 36 seconds on. This led to the fix in 1.4.5, which is limited to build 22000 and later. With the trigger above, it fails 11 seconds after start too, also on Windows 10 and Windows Server 2025, and once recovered, a hot corner started afresh works 66 seconds after start. What made that series fail without any typing was not found; the test scripts started console windows of their own, which may have taken the foreground.

### Hot corner, device input (pushes into the corner)

| Step | Win 11, 1.3.9 | Win 11, 1.4.5 | Win 11, 1.3.9 signed (`uiAccess`) | Win 10, 1.3.9 | Win 10, 1.4.5 (no tap there) |
|---|---|---|---|---|---|
| No typing yet | 3/3 | 3/3 | 3/3 | 3/3 | 3/3 |
| After typing into Notepad | **0/3** | 3/3 | 3/3 | **0/3** | **0/3** |
| After Win+D | 2/2 | 2/2 | 2/2 | 2/2 | 2/2 |
| Notepad in front again, no typing | 3/3 | 3/3 | 3/3 | 3/3 | 3/3 |
| After typing into Notepad | **0/2** | 2/2 | 2/2 | **0/2** | **0/2** |

A test build that taps the key before the first call only, on Windows 11: after typing, 3/3; after typing again, 3/3 and 2/2.

### Hot corner, injected input (1.3.9 unless stated, ten pushes each)

The test script tapped `0xE8` into Notepad, then pushed:

| OS | Hot corner | 11-12 s after start | 66 s after start |
|---|---|---|---|
| Windows 11 26H2 | unelevated | 0/10 | 0/10 |
| Windows 11 26H2 | signed for `uiAccess` | 10/10 | 10/10 |
| Windows 10 LTSC 2021 | unelevated | 0/10 | 0/10 |
| Windows Server 2025 | elevated (built-in Administrator, so Explorer is elevated too) | 0/10 | 0/10 |

Recovery on Windows 11:

| After the trigger | Result |
|---|---|
| Pushes with Notepad in front | 0/3 |
| Then pushes with all windows minimised | 3/3 |
| Then pushes with Notepad in front again | 3/3 |
| A hot corner started afresh afterwards, without the trigger | 10/10 |
| Test build: no tap in calls 1-2, a tap in call 3, none from call 4 on | calls 1-2 fail, calls 3-10 open |

A hot corner signed for `uiAccess` also opened Task View in every other case tried: typing into Notepad between pushes (6/6), typing and moving the mouse during a 60-second wait (5/5), and an elevated window in front (11/11).

### Ruled out
- **How or on which thread the hot corner calls `WindowSwitcher`:** the low-level mouse hook only posts a message, and the call runs in the main thread's message loop. A test build that creates `Shell.Application` and calls `WindowSwitcher` on a new STA thread each time fails the same way (0/10 twice), and so does a separate PowerShell process (see above).
- **Creating `Shell.Application` late:** creating it at startup does not help.
- **Elevated windows or packaged apps in front:** not needed.
- **The time since the hot corner started:** see above.

### Foreground lock
Tested with the background PowerShell caller from [Minimal reproduction](#minimal-reproduction), on Windows 10 and Windows 11, with device input. Each test started after typing into Notepad had made the call fail.

| Test | Win 10 | Win 11 |
|---|---|---|
| Another process with foreground rights (it has just tapped a key) calls `AllowSetForegroundWindow` for the caller | opens | opens |
| The same process only taps the key, without allowing the caller | does not open | does not open |
| ...and then the user types into Notepad again | does not open | does not open |
| `ForegroundLockTimeout` set to 0 (test only, restored afterwards) | opens, also after typing again | opens, also after typing again |
| No input for 210 seconds, longer than the default `ForegroundLockTimeout` (200 seconds) | opens | does not open |
| The caller calls `LockSetForegroundWindow(LSFW_UNLOCK)` first | fails with access denied; does not open | same |

Allowing the caller, or turning the foreground lock off, is enough to make the same call work; nothing else about the call changes.

### Keeping the rights
The same caller tapped `0xE8` once, then did not send input again. After each of the following, the user typed into Notepad and the caller called `WindowSwitcher` twice; "Rights" is what `AllowSetForegroundWindow(ASFW_ANY)` returned before the calls.

| After | Win 10 | Win 11 |
|---|---|---|
| Nothing else (the tap only) | 2/2, rights true | 2/2, rights true |
| The user opens and closes Task View (Win+Tab) | 2/2, true | 2/2, true |
| The user opens File Explorer (Win+E), types there, and switches back with Alt+Tab | 2/2, true | 2/2, true |
| The user opens Start and types there | 2/2, true | 2/2, true |
| The user presses Win+D twice | 2/2, true | 2/2, true |
| The user clicks on the desktop or taskbar | 2/2, true | 2/2, true |
| The user presses keys in an elevated window (Registry Editor) | 2/2, true | 2/2, true |
| 9 minutes, the user typing every 30 seconds (checked after 1, 4 and 9 minutes) | 6/6, true | 6/6, true |
| **Another process taps a key with `SendInput`** | **0/2, false** | **0/2, false** |

The rights from the caller's own input outlast anything the user does, but not input sent by another process. This fits ReactOS's model of one "input provider", the last process that sent input (see [Documentation and prior art](#documentation-and-prior-art)).

### Opening Task View without sending input

| Way | Win 10 | Win 11 |
|---|---|---|
| `ShellExecute` of `shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}` | does not open | does not open |
| UI Automation on the taskbar's Task View button (Invoke on Windows 10, Toggle on Windows 11) | opens | the first call opens (after 3.5 seconds), and the caller has foreground rights afterwards; in later calls the button was not found |

The UI Automation way needs the Task View button on the taskbar, which users can hide, and it imitates a click on the taskbar rather than asking the shell for Task View.

## Root cause
Confirmed by the [foreground lock tests](#foreground-lock): Windows' rules for changing the foreground window. Opening Task View brings it to the front, and Explorer does so only if that is allowed; otherwise `WindowSwitcher` returns without an error and nothing opens.
- **The foreground lock:** when the user types in an app, the foreground is locked for that app, so that programs in the background cannot steal the focus. A foreground change by the shell (Win+D, Task View) ends the lock. On Windows 10 it also ends after `ForegroundLockTimeout` without input; on Windows 11 it did not.
- **Without the lock**, Task View opens, even though the caller has no foreground rights (steps A, C and D).
- **With the lock**, it opens only if the caller has foreground rights, which Explorer then seems to take over. A process gets them when another process with rights allows it (`AllowSetForegroundWindow`), or when it sends input itself (step F). Rights from `AllowSetForegroundWindow` are gone once the user types again; rights from the caller's own input last until another process sends input (see [Keeping the rights](#keeping-the-rights)).
- **A process with `uiAccess`** is not bound by the lock.

This also explains why 1.1 was not affected: it sent Win+Tab, which is input from the hot corner itself, and which Explorer handles as its own hotkey.

The behaviour is the same on Windows 10 and Windows 11, so it is not a change in a Windows update. Windows 11 only makes it more likely, as the lock does not time out there.

## Documentation and prior art
What Microsoft documents:
- [`SetForegroundWindow`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow): a process may set the foreground window only if one of a few conditions holds, such as being the foreground process, having been started by it, having received the last input event, or the foreground lock timeout having expired. Even then, Windows may deny it.
- [`AllowSetForegroundWindow`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow): a process with the right may pass it on to another process; that process loses it the next time the user generates input that is not directed at it. This matches the [foreground lock tests](#foreground-lock).
- [`CoAllowSetForegroundWindow`](https://learn.microsoft.com/en-us/windows/win32/api/objbase/nf-objbase-coallowsetforegroundwindow): the same for COM servers. The caller must hold the right itself, otherwise it fails with `E_ACCESSDENIED`, so it cannot help a caller that has none.
- Raymond Chen, [Foreground activation permission is like love: You can't steal it, it has to be given to you](https://devblogs.microsoft.com/oldnewthing/20090220-00/?p=19083): foreground rights come from input and can only be handed on, not taken.
- [`WindowSwitcher`](https://learn.microsoft.com/en-us/windows/win32/shell/ishelldispatch5-windowswitcher) says nothing about the foreground.

Not documented: that a process gets foreground rights by sending input itself, and how long it keeps them. ReactOS, an independent reimplementation of Windows, records which process sent the last keyboard or mouse input ([`gppiInputProvider`](https://github.com/reactos/reactos/blob/master/win32ss/user/ntuser/keyboard.c)) and lets that process set the foreground window, even while the foreground is locked ([`CanForceFG`](https://github.com/reactos/reactos/blob/master/win32ss/user/ntuser/focus.c) and its callers). This fits what we measured on Windows (see [Keeping the rights](#keeping-the-rights)).

Other programs that need foreground rights from the background send input of their own in the same way:
- **Chromium** ([`foreground_helper.cc`](https://chromium.googlesource.com/chromium/src/+/main/ui/base/win/foreground_helper.cc)) registers F22 as a hotkey, sends it with `SendInput`, and changes the foreground window when the hotkey arrives.
- **Signal** ([`windows-dummy-keystroke`](https://github.com/signalapp/windows-dummy-keystroke)) sends a dummy key press so that `AllowSetForegroundWindow` stops failing with access denied.
- **AutoHotkey** ([`window.cpp`](https://github.com/AutoHotkey/AutoHotkey/blob/alpha/source/window.cpp)) sends an Alt release once per process, noting that this lets all later `SetForegroundWindow` calls succeed.

No documented way to open Task View from a process without foreground rights was found, other than `uiAccess`.

## Implemented fix
A hot corner without `uiAccess` taps an unassigned key (`0xE8`) with `SendInput` just before each `WindowSwitcher` call. Sending input is how a process gets foreground rights (see [Root cause](#root-cause)), and the same approach as in the [programs above](#documentation-and-prior-art). The foreground app receives a key that means nothing. With `uiAccess`, nothing is sent, as it is not needed there.

The key is tapped before every call rather than once, or only when the rights seem to be missing:
- **Rights are only taken when they are needed.** The hot corner takes them when the user pushes into the corner, which is a request to change the foreground window anyway. At other times it leaves them to whichever program has them.
- **The rights can be lost.** They last through the user's own input, but not through input sent by another process (see [Keeping the rights](#keeping-the-rights)), and programs that send input are common (macro tools, remote control, accessibility tools, browsers).
- **Checking is not clean either.** The only way to check, `AllowSetForegroundWindow(ASFW_ANY)`, hands the rights to every process when it succeeds. How long the rights last is not documented either, so tapping only when they seem to be missing would rely on undocumented behaviour.

- **1.4.5 (released):** the key is only tapped on build 22000 and later (Windows 11, Windows Server 2025), following the time-since-start tests, in which only Windows 11 failed. **Windows 10 is still affected** (see the table above).
- **`devel` (not released yet):** the key is tapped on every Windows version, so Windows 10 is covered too.

| Step, device input | Win 10, `devel` | Win 11, `devel` |
|---|---|---|
| No typing yet | 3/3 | 3/3 |
| After typing into Notepad | 3/3 | 3/3 |
| After Win+D | 2/2 | 2/2 |
| Notepad in front again, no typing | 3/3 | 3/3 |
| After typing into Notepad | 2/2 | 2/2 |

## Proposed fix
None beyond the [implemented fix](#implemented-fix), which is to be released for Windows 10 as well. A hot corner signed for `uiAccess` needs no tap.

## Further actions
- Find out what made the time-since-start series fail without any typing.
- Low priority: look further for a way to open Task View that doesn't send input. The documented ways have been tried (see [Documentation and prior art](#documentation-and-prior-art)); what is left, such as UI Automation on the taskbar button (see [Opening Task View without sending input](#opening-task-view-without-sending-input)), imitates the user and may not be appropriate even where it works.
