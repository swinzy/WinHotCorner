# To do

Planned work and open questions. See [docs/technical.md](docs/technical.md) for how things work now.

## Releases

- [ ] **Sign the installers and programs.** Unsigned programs get SmartScreen warnings, and Smart App Control in Windows 11 can block them outright.

## Hot corner

- [ ] **Open Task View without simulating Win+Tab.** Windows has no public function for it. The shell namespace `shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}` ("Switch between windows") may open Task View; to check: whether it does on current Windows 11, how fast it is (it goes through `explorer.exe`), whether it also works with an elevated window in front.

## Translations

- [ ] **Translate the control panel and the installers.** The control panel's text is written straight into `MainWindow.xaml` and `MainWindow.xaml.cs`; WinUI can load it from `.resw` resources instead (`x:Uid` in XAML, `ResourceLoader` in code), one file per language. Inno Setup has its own `[Languages]` and `[CustomMessages]` sections for the installers. The English text currently uses Windows' US spelling ("managed by your organization"), to match Windows' own settings pages; when it moves into resources, the English text switches to the project's Australian spelling ("organisation").

## Multiple monitors experience improvement

- [ ] **Corner covered by another monitor, without sticky corners.** A covered corner works only where Windows' sticky corners hold the pointer (see [docs/technical.md](docs/technical.md#multiple-monitors)). Where they don't (`MouseCornerClipLength` set to 0), holding the pointer with `ClipCursor` near the corner could do what GNOME's barrier does, but `ClipCursor` is shared with games, remote desktop and other apps, so this would be an opt-in setting at most.
- [ ] **Covered corners on Windows 10.** Sticky corners were measured on Windows 11, with monitors side by side and stacked vertically. To check: Windows 10.
- [ ] **Covered corners without stopping the pointer (opt-in).** A compromise for the case above: on a monitor whose top-left corner is covered by another monitor, nothing stops the pointer, so it just moves on to the other monitor. With this setting, the corner would still trigger when the pointer crosses the edge near that corner in the right direction, from this monitor out towards its top left, using the same pressure rules on the crossing movement; movement the other way (coming back from the other monitor) never triggers. As the pointer is not held, only a fast enough movement can build up the pressure. A preference in the control panel, off by default.
