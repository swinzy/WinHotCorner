# Task View doesn't open, but the ripple plays

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

## Possible reasons
Not confirmed, but the results fit Windows' rules for changing the foreground window. Opening Task View brings it to the front, and Explorer seems to do so only if the caller may change the foreground window:
- A caller without foreground rights may do it only while the foreground is not locked. Typing in an app locks it for that app; a foreground change by the shell (Win+D, Task View) unlocks it.
- A process that sends input itself gets foreground rights, and here kept them even after the user typed in another program (step G).
- A process with `uiAccess` is not bound by these rules.

## Implemented fix
> [!IMPORTANT]
> This is a workaround only, not a proper fix.

A hot corner without `uiAccess` taps an unassigned key (`0xE8`) with `SendInput` just before each `WindowSwitcher` call. The foreground app receives a key that means nothing. With `uiAccess`, nothing is sent, as it is not needed there.

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
None at the moment; see [Further actions](#further-actions).

## Further actions
- Find out when a process loses the foreground rights it got from its own input. Tapping once might then be enough; until it is known, the key is tapped before every call.
- Find out what made the time-since-start series fail without any typing.
- Look for a way to open Task View that doesn't send input.
