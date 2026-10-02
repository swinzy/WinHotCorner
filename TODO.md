# To do

Planned work and open questions. See [docs/technical.md](docs/technical.md) for how things work now.

## Releases

- [ ] **Build and publish releases with GitHub Actions**: build both installers on a Windows runner and attach them to a release.
- [ ] **Sign the installers and programs.** Unsigned programs get SmartScreen warnings, and Smart App Control in Windows 11 can block them outright. [SignPath Foundation](https://signpath.org) signs open source projects for free when they are built in CI.

## Hot corner

- [ ] **Open Task View without simulating Win+Tab.** Windows has no public function for it. The shell namespace `shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}` ("Switch between windows") may open Task View; to check: whether it does on current Windows 11, how fast it is (it goes through `explorer.exe`), whether it also works with an elevated window in front.
- [ ] **Run without elevation, using `uiAccess`**, once the programs are signed: the hot corner would no longer need to run elevated to work with administrator windows, and it would also work for standard users. Elevated startup stays as the fallback for unsigned builds.
- [ ] **Right-to-left languages**: GNOME puts the hot corner in the top-right corner when the interface language is written right to left.

## Multiple monitors

- [ ] **Corner covered by another monitor.** GNOME keeps the primary monitor's corner even when another monitor is directly to its left, by stopping the pointer with a barrier there. Windows has no pointer barriers; holding the pointer with `ClipCursor` near the corner could do the same, but `ClipCursor` is shared with games, remote desktop and other apps, so this would be an opt-in setting at most.
- [ ] **Covered corners without stopping the pointer (opt-in).** A compromise for the case above: on a monitor whose top-left corner is covered by another monitor, nothing stops the pointer, so it just moves on to the other monitor. With this setting, the corner would still trigger when the pointer crosses the edge near that corner in the right direction, from this monitor out towards its top left, using the same pressure rules on the crossing movement; movement the other way (coming back from the other monitor) never triggers. As the pointer is not held, only a fast enough movement can build up the pressure. A preference in the control panel, off by default.
- [ ] **Monitors that are not aligned.** When the left monitor sits lower than the right one, both top-left corners are free. To check how well this works, and how Windows 11's *Ease cursor movement between displays* setting affects it.
