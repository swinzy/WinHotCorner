<div align="center">

<table>
<tbody>
  <tr>
    <td><img src="res/WHC_Logo.svg" alt="Logo" width="128px"/> </td>
    <td>
    
  # WinHotCorner
  </td>
  </tr>
</tbody>
</table>

Classic GNOME hot corner function for Windows!

![CSharp](https://img.shields.io/badge/C%23-5C2D91?style=flat-square&logo=.net&logoColor=white)
![Windows 11](https://img.shields.io/badge/Designed%20for%20Windows%2011-%230079d5.svg?style=flat-square&logo=Windows%2011&logoColor=white)
![Inkscape](https://img.shields.io/badge/Inkscape-e0e0e0?style=flat-square&logo=inkscape&logoColor=080A13)

<img src="res/controlpanel_scrshot.png" alt="WinHotCorner Control Panel" width="640px"/>

</div>

## What it does

Push the mouse pointer into the top-left corner of a screen (top-right when Windows is in a right-to-left language, as on GNOME; the control panel can change that back) and Task View opens, just like the Activities overview on GNOME. No keyboard needed.

It feels like GNOME too: the corner reacts to a deliberate push, not to the pointer just passing by, and it waits until you move away before it can trigger again.

## Philosophy

- **Simple**: works out of the box, nothing to set up (but you can).
- **Lightweight**: small, with no window, no tray icon and no shortcuts.
- **Elegant**: blends into Windows, as if it were a Windows feature.

## Install

Download **one** installer from [Releases](https://github.com/swinzy/WinHotCorner/releases):

| Installer | Installs | |
|---|---|---|
| **`WinHotCorner-Full-<version>-setup.exe`** | the hot corner **and** the control panel | Recommended |
| **`WinHotCorner-HotCornerOnly-<version>-setup.exe`** | the hot corner only (about 2 MB) | If you don't want the control panel |

You never need both: *Full* already contains the hot corner. If you installed *HotCornerOnly* and want the control panel later, just run *Full*: it adds the control panel and leaves the hot corner as it is (it only updates it if *Full* is newer).

The hot corner starts right away and from then on whenever you sign in. In *Installed apps* the two parts appear as *WinHotCorner* and *WinHotCorner Control Panel*.

Setup offers to **sign WinHotCorner on this computer** (on by default). Signed, it runs without administrator rights, also works with administrator windows in front on standard accounts (not officially tested), and its ripple shows above Task View. Setup makes a certificate on your computer that can sign nothing else, because its private key is deleted right after signing; see [Signing WinHotCorner on your computer](docs/digital-signature.md). If signing is not possible (some company policies do not allow it), Setup installs it the usual way and says so. To change your mind, run Setup again; uninstalling removes the certificate.

The installers are not signed yet, so Windows may show a SmartScreen warning: choose *More info* → *Run anyway*.

**Requirements**: Windows 10 version 1903 or later, or Windows 11; 64-bit.

## Settings

Open **WinHotCorner Control Panel** from the Start menu to:

- turn the hot corner off or on,
- choose which screens have a hot corner,
- keep it from triggering while an app is fullscreen on that screen (games, videos), or while a mouse button is held (dragging),
- change how hard you have to push into the corner.

Changes apply immediately. Without the control panel, the hot corner works with its defaults; settings can also be set in the registry or by Group Policy, see the [technical notes](docs/technical.md#configuration).

## Turning it off or removing it

- **Turn it off**: switch *Hot corner* off in the control panel. It stays off, also after signing in again.
- **Remove it**: *Settings* → *Apps* → *Installed apps* → *WinHotCorner* (and *WinHotCorner Control Panel*) → *Uninstall*.

## Multiple screens

As on GNOME, the primary screen has a hot corner, and so does every screen with no neighbour at its top left (no other screen directly to the left of its top-left corner or directly above it; top right and to the right in a right-to-left language). The control panel's *Hot corner screens* can change this to the primary screen only, screens with no top-left neighbour only, or all screens.

A corner with another screen next to it works because Windows holds the pointer there for a few pixels (its "sticky corners"): push into the very top of the edge. If you have turned sticky corners off (`MouseCornerClipLength` set to 0), or the screens are not aligned at the top, such a corner does not trigger.

## Troubleshooting

**It does not trigger while Task Manager or another app running as administrator is in front.**
WinHotCorner runs with your highest privileges so that this works. On a standard (non-administrator) account it cannot, because Windows does not let normal programs control administrator windows, unless you let Setup sign it on your computer.

**Something else is wrong.**
Errors are written to `%LOCALAPPDATA%\WinHotCorner\WinHotCorner.log`. Please attach it to an [issue](https://github.com/swinzy/WinHotCorner/issues).

## How it works

See the [technical notes](docs/technical.md): how a push is detected, configuration, startup, the installers and how to build.

Planned work is in [TODO.md](https://github.com/swinzy/WinHotCorner/blob/devel/TODO.md) (on the `devel` branch, where development happens).
