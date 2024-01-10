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
</div>

## Introduction
The aim of this repository is to replicate the classic GNOME hot corner bahaviour on Windows as close as possible.

The GNOME hot corner has one function, that is when user's mouse cursor hits the top left corner of the display, it will show the "Activity" page, which allows user to quickly navigate through opened apps without needing to touch your keyboard.

Hence this app will allow user to have the same experience as on GNOME.

## Philosophy
The principles of design of this app are:
<div align="center">
  
### Simple
The app should just work out of the box, without **needing** to be configured in any way (although it should **allow** configurations).
### Lightweight
The app should be small in size, consumes little resources.
### Elegant
The app should have elegant design, and blend into your system, as if it was a Windows feature.
</div>

## Functionalities
- [X] **Trigger hot corner**
- [X] **Require force**: hot corner will not trigger simply when cursor is **at** the top-left corner, but when user **hits** the top-left corner, similar to GNOME default behaviour.
- [X] **Singleton**: service worker will not create multiple instances even when user accidentally runs mutiple times.
- [X] **Disable when fullscreen**: hot corner will not trigger if foreground application is in fullscreen mode.
- [ ] **Multi-display awareness**: user can choose whether hotcorner triggers on primary display only or all displays.
- [X] **Disable when mouse down**: prevents accidental trigger while dragging/using gestures.
- [X] **Configurable**: service worker reads configurations from config file.
- [ ] **Automatically configure run on startup**
- [ ] **Control panel configurable**: service worker can be configured via the WinHotCorner control panel.
- [ ] **Control panel controllable**: service worker can be killed/started via the WinHotCorner control panel.

## Configuration
See Wiki for configuration methods.

## Components
### Service Worker
The WinHotCorner Service Worker is the main component of this app. It does not have a graphical user interface (GUI), and will run (and stay forever) in the background as soon as user executes it.

It simply provides the functionality of this app.

It is configurable either via Windows Registry or the WinHotCorner Control Panel.
### Control Panel
The WinHotCorner Control Panel is a totally optional and separate application. Users are not required to install this component to enjoy the full function of the app.

It simply provides an elegant entry of tweaking the behaviour of the service worker.

Removing this component will not affect the functionality of the service worker.
## Mechanism
The application utilises a global mouse hook[^1] to detect cursor postion. And when it detects that the mouse is hitting the top-left corner, the app will then simulate[^2] <kbd>Win</kbd> + <kbd>Tab</kbd> hotkey to trigger the "Task Switch" interface.

## Troubleshooting
#### Hot corner does not trigger when some applications are running (i.e. Task Manager, Task Scheduler, Registry Editor)
**Solution**: Run the service worker as administrator.

**Reason**: Because of a security constraint of Windows, any application running as user will not be able to hook global mouse/keyboard onto any application running as administrator, hence the app cannot detect hot corner when admin app is running.

## References
[^1]: [Global Mouse Hook](https://github.com/gmamaladze/globalmousekeyhook)
[^2]: [Input Simulator](https://github.com/michaelnoonan/inputsimulator)
