# To do

Planned work and open questions. See [docs/technical.md](docs/technical.md) for how things work now.

## Releases

- [ ] **Sign the installers and programs.** Unsigned programs get SmartScreen warnings, and Smart App Control in Windows 11 can block them outright. [SignPath Foundation](https://signpath.org) signs open source projects for free when they are built in CI.

## Hot corner

- [ ] **Open Task View without simulating Win+Tab.** Windows has no public function for it. The shell namespace `shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}` ("Switch between windows") may open Task View; to check: whether it does on current Windows 11, how fast it is (it goes through `explorer.exe`), whether it also works with an elevated window in front.
- [ ] **Run without elevation, using `uiAccess`**, once the programs are signed: the hot corner would no longer need to run elevated to work with administrator windows, it would also work for standard users, and its ripple would show above Task View instead of under it. Elevated startup stays as the fallback for unsigned builds. A program that asks for `uiAccess` in its manifest doesn't start at all unless it is signed and installed in a protected folder such as Program Files, so only signed builds would ask for it (a build property); local builds stay as they are. Signed builds would start at logon from the `Run` registry key instead of the startup task (Task Scheduler can't start a `uiAccess` program: error 740, while Explorer starts `Run` entries through `ShellExecute`, which grants it). The hot corner then shows up in Task Manager's startup apps, and the control panel gets a per-user *Start at logon* switch that uses the same setting as Task Manager. Unlike the task, nothing restarts the hot corner if it crashes; the control panel shows that it is not running.
- [ ] **Right-to-left languages**: GNOME puts the hot corner in the top-right corner when the interface language is written right to left.

## Translations

- [ ] **Translate the control panel and the installers.** The control panel's text is written straight into `MainWindow.xaml` and `MainWindow.xaml.cs`; WinUI can load it from `.resw` resources instead (`x:Uid` in XAML, `ResourceLoader` in code), one file per language. Inno Setup has its own `[Languages]` and `[CustomMessages]` sections for the installers. The English text currently uses Windows' US spelling ("managed by your organization"), to match Windows' own settings pages; when it moves into resources, the English text switches to the project's Australian spelling ("organisation").

## Multiple monitors

- [ ] **Windows' sticky corners.** Windows stops the pointer for a few pixels (6 by default, `MouseCornerClipLength`) at the top and bottom of an edge shared by two monitors. So with two monitors of the same height side by side, the right monitor's top-left corner does hold the pointer, but the hot corner doesn't count it, because another monitor is to its left. To check: what the mouse hook reports while the pointer is held there, whether a fast movement gets through, and whether the clip length scales with DPI. If it holds reliably, that corner could work like a free one, without `ClipCursor` (unless the clip length is set to 0).
- [ ] **Corner covered by another monitor.** GNOME keeps the primary monitor's corner even when another monitor is directly to its left, by stopping the pointer with a barrier there. Windows has no pointer barriers; holding the pointer with `ClipCursor` near the corner could do the same, but `ClipCursor` is shared with games, remote desktop and other apps, so this would be an opt-in setting at most.
- [ ] **Covered corners without stopping the pointer (opt-in).** A compromise for the case above: on a monitor whose top-left corner is covered by another monitor, nothing stops the pointer, so it just moves on to the other monitor. With this setting, the corner would still trigger when the pointer crosses the edge near that corner in the right direction, from this monitor out towards its top left, using the same pressure rules on the crossing movement; movement the other way (coming back from the other monitor) never triggers. As the pointer is not held, only a fast enough movement can build up the pressure. A preference in the control panel, off by default.
- [ ] **Monitors that are not aligned.** When the left monitor sits lower than the right one, both top-left corners are free. To check how well this works, and how Windows 11's *Ease cursor movement between displays* setting affects it.
