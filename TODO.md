# To do

Planned work and open questions. See [docs/technical.md](docs/technical.md) for how things work now.

## Releases

- [ ] **Sign the installers and programs.** Unsigned programs get SmartScreen warnings, and Smart App Control in Windows 11 can block them outright. [SignPath Foundation](https://signpath.org) signs open source projects for free when they are built in CI.

## Hot corner

- [ ] **Open Task View without simulating Win+Tab.** Windows has no public function for it. The shell namespace `shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}` ("Switch between windows") may open Task View; to check: whether it does on current Windows 11, how fast it is (it goes through `explorer.exe`), whether it also works with an elevated window in front.
- [ ] **Run without elevation, using `uiAccess`**, once the programs are signed: the hot corner would no longer need to run elevated to work with administrator windows, and it would also work for standard users. Elevated startup stays as the fallback for unsigned builds. A program that asks for `uiAccess` in its manifest doesn't start at all unless it is signed and installed in a protected folder such as Program Files, so only signed builds would ask for it (a build property); local builds stay as they are. Signed builds would start at logon from the `Run` registry key instead of the startup task (Task Scheduler can't start a `uiAccess` program: error 740, while Explorer starts `Run` entries through `ShellExecute`, which grants it). The hot corner then shows up in Task Manager's startup apps, and the control panel gets a per-user *Start at logon* switch that uses the same setting as Task Manager. Unlike the task, nothing restarts the hot corner if it crashes; the control panel shows that it is not running.
- [ ] **Ripple effect**, like GNOME's: a short animation of three quarter circles at the corner when it triggers. It is only visible with `uiAccess` (so the programs must be signed, see above): Task View covers ordinary topmost windows, while a window of a `uiAccess` program stays above it, so the ripple can play while Task View opens. Unsigned builds play it too, Task View just covers it, so the program doesn't need to know whether it is signed. It should follow the Windows animation setting.
- [ ] **Right-to-left languages**: GNOME puts the hot corner in the top-right corner when the interface language is written right to left.

## Multiple monitors

- [ ] **Corner covered by another monitor.** GNOME keeps the primary monitor's corner even when another monitor is directly to its left, by stopping the pointer with a barrier there. Windows has no pointer barriers; holding the pointer with `ClipCursor` near the corner could do the same, but `ClipCursor` is shared with games, remote desktop and other apps, so this would be an opt-in setting at most.
- [ ] **Covered corners without stopping the pointer (opt-in).** A compromise for the case above: on a monitor whose top-left corner is covered by another monitor, nothing stops the pointer, so it just moves on to the other monitor. With this setting, the corner would still trigger when the pointer crosses the edge near that corner in the right direction, from this monitor out towards its top left, using the same pressure rules on the crossing movement; movement the other way (coming back from the other monitor) never triggers. As the pointer is not held, only a fast enough movement can build up the pressure. A preference in the control panel, off by default.
- [ ] **Monitors that are not aligned.** When the left monitor sits lower than the right one, both top-left corners are free. To check how well this works, and how Windows 11's *Ease cursor movement between displays* setting affects it.
