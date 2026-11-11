# Task View doesn't open, but the ripple plays

## Scope
- Related Issue: #8
- Affected versions: 1.2-1.3
- OS: Windows 10, Windows 11 and Windows Server 2025
  - Reproduced on Windows 10 (LTSC 2021, build 19044), Windows 11 26H2 (build 26300) and Windows Server 2025 (build 26100). The reporter runs Windows 11 24H2.

## Trigger conditions
- The hot corner runs **without `uiAccess`**, elevated or not: the portable version, or an unsigned install. Whether a hot corner with `uiAccess` is affected has not been tested under the trigger below yet.
- **Another program receives keyboard input**, for example the user typing in the app in front.

As users type all the time, the hot corner is in practice almost always in this state.

## Observed behaviour
Once another program has received keyboard input, Task View triggering will fail. Animation will still play.

In detail:
- `WindowSwitcher` returns without an error, so nothing is logged and the Win+Tab fallback is not used. Task View simply does not open; the ripple plays.
- The state is global and lasts: every later push fails, also from a hot corner started afresh, until one of the following happens:
  - Task View is opened while the desktop is in front (all windows minimised), as the reporter found. Afterwards it opens from any window again.
  - The hot corner itself sends input, such as a key tap. Afterwards it opens again without further taps.
- Once it works again, it keeps working until another program receives keyboard input.
- It does not depend on how long the hot corner has run: it fails 11 seconds after start as well.

## Investigation

Each test started the hot corner afresh, then pushed into the corner several times with injected input in a desktop session, and checked whether Task View came to the front. To reproduce the trigger, the test script tapped an unassigned key (`0xE8`) into Notepad, which was in front, before pushing.

### Earlier leads (wrong)
- **Elevated windows:** the reporter mentioned that 1.1 failed only with high-priority windows such as Task Manager. Pushing while an elevated packaged app (the new Notepad, run as administrator) was in front seemed to reproduce the bug, and `AllowSetForegroundWindow(ASFW_ANY)` failed in those calls, so the foreground rights looked like the cause. Without any elevated window the bug occurs as well, and `AllowSetForegroundWindow` fails in calls that work, too.
- **Time since start:** in a series of tests on Windows 11, the first push worked up to 27 seconds after the hot corner started and failed from 36 seconds on, and the hot corner kept failing until it was restarted. This led to the fix in 1.4, which is limited to build 22000 and later. The trigger above, however, makes it fail 11 seconds after start too, and on Windows 10 and Windows Server 2025 as well, where these tests had never failed. What made those tests fail without any keyboard input is not known.

### Matrix: keyboard input from another program, then pushes (1.3.9, ten pushes each)

| OS | Hot corner | 11-12 s after start | 66 s after start |
|---|---|---|---|
| Windows 11 26H2 | unelevated | 0/10 | 0/10 |
| Windows 10 LTSC 2021 | unelevated | 0/10 | 0/10 |
| Windows Server 2025 | elevated (built-in Administrator, so Explorer is elevated too) | 0/10 | 0/10 |

### Matrix: recovering (Windows 11, 1.3.9 unless stated)

| After the trigger | Result |
|---|---|
| Pushes with Notepad in front | 0/3 |
| Then pushes with all windows minimised | 3/3 |
| Then pushes with Notepad in front again | 3/3 |
| A hot corner started afresh afterwards, without the trigger | 10/10 |
| Test build: no key tap in calls 1-2, a tap in call 3, none from call 4 on | calls 1-2 fail, calls 3-10 open |
| Test script taps a key into Notepad, or types into it | 0/3 each |

### Matrix: the 1.4 fix (Windows 11, time-since-start tests)

| Hot corner | Result |
|---|---|
| Unelevated, first push 37 s and 64-67 s after start | 5/5 each |
| Elevated, first push 64 s after start | 5/5 |
| Pushed early, then again 30 s later | 3/3, then 5/5 |

### Ruled out
- **Elevated windows or packaged apps in front:** not needed.
- **The hot corner's foreground rights, as `AllowSetForegroundWindow` reports them:** missing in most calls that work, too.
- **Creating `Shell.Application` late:** creating it at startup does not help.
- **The time since the hot corner started:** see above.

## Possible reasons
Not known for certain. The pattern fits Windows' rules for changing the foreground window: only the process that received the last input event, or the foreground process, may bring another window to the front. Opening Task View from the desktop (Explorer in front) and a key tap by the hot corner (making it the last input's source) would both satisfy them. That `AllowSetForegroundWindow` fails in calls that work does not fit, though.

## Implemented fix
> [!IMPORTANT]
> This is a workaround only, not a proper fix.

In 1.4, the hot corner taps an unassigned key (`0xE8`) with `SendInput` just before each `WindowSwitcher` call. The foreground app receives a key that means nothing.

The key is only tapped on build 22000 or above, when the hot corner has no `uiAccess`; on Windows 10, and with `uiAccess`, nothing is sent. This followed the time-since-start tests, in which only Windows 11 failed.

## Proposed fix
Tap the key on every Windows version whenever the hot corner has no `uiAccess`, as Windows 10 and Windows Server 2025 are affected too. Before that, test whether a hot corner with `uiAccess` is affected by the keyboard trigger; if it is, tap it there as well.

## Further actions
- Find out why the call stops working, and whether the key tap can be replaced by something that doesn't send input.
