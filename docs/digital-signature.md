# Signing WinHotCorner on your computer

The standard install **signs WinHotCorner on your computer**. This page explains what that means, why it is needed and why it is safe, and what [installing without signing](#installing-without-signing) leaves out.

## What you get

Signed, WinHotCorner:

- runs **without administrator rights**, and still works while an app running as administrator (such as Task Manager) is in front;
- also works in front of such apps on a **standard (non-administrator) account** (not officially tested), which is not possible otherwise;
- opens **Task View directly**, without pressing a key first;
- shows its **ripple above Task View**, as on GNOME, instead of under it.

## Installing without signing

Setup also offers *Install without signing (limited)*, and the [portable version](../README.md#portable) is never signed. Windows then treats WinHotCorner as an ordinary app, which leaves it with known limitations that can't be cleanly worked around:

- **It presses a key before opening Task View.** Windows lets an ordinary app bring a window to the front only if it has just sent input itself, so WinHotCorner taps a key that means nothing (`0xE8`) each time. The app in front receives it, and so does any program that watches every key press. See [Task View doesn't open due to Windows foreground lock](../issues/0001-TaskView-Trigger.md) for the details.
- **Administrator rights:** on an administrator account, the installed version runs with administrator rights, so that it works in front of apps running as administrator (such as Task Manager). On a standard account, and in the portable version, it doesn't trigger at all while such an app is in front.
- **Its ripple is mostly hidden** by Task View.

Because of these limitations, an unsigned WinHotCorner gets **best-effort support only**.

## Why it needs a signature

Windows lets a program work with every window on the screen, including Task View and apps running as administrator, without running as administrator itself, if the program asks for it (this is called `uiAccess`) and is **signed by a certificate that the computer trusts**. An unsigned program that asks for it does not start at all.

Certificates that every computer trusts are issued by certificate authorities, and WinHotCorner does not have one. So Setup makes a certificate for this one purpose on your computer instead.

## What Setup does

1. It makes a new certificate named `WinHotCorner (made on this computer)`. It can only be used for signing programs.
2. It adds the certificate to your computer's trusted root certificates, so that Windows trusts the signature.
3. It signs this copy of WinHotCorner with it.
4. It **deletes the certificate's private key** straight away.

The private key is what signing needs. Without it, nobody, including me, can sign anything else with this certificate, ever. So the only thing your computer trusts because of it is this one copy of WinHotCorner. The key never leaves your computer and is gone before Setup even installs anything.

> [!TIP]
> Each certificate is used once. When you update WinHotCorner, Setup signs the new version with a new certificate and removes the old one.

## Why not my (WinHotCorner's official) certificate

If WinHotCorner shipped a certificate for everyone to trust, whoever held its private key could sign any program, and every computer that trusted the certificate would trust that program too. We don't do this so that you don't have to trust me, either to not release any malware (I promise I won't anyway), or to protect the private key from being stolen. A certificate like that cannot be revoked if the key is ever stolen. A certificate made on your computer, whose key is deleted, has none of these problems.

## Why WinHotCorner itself is not signed

The installers and the programs are deliberately not signed with a certificate of mine:

1. **Every certificate that Windows trusts costs money**, year after year. I don't want to support that: it makes it harder to publish open source software for Windows. I know that some companies offer free certificates to individual developers or open source projects. But the more developers use them (or buy their own), the less resistance there will be when Windows one day requires a certificate, and in the end that hurts open source developers. Google is already heading that way: since 30 September 2026, certified Android devices in Brazil, Indonesia, Singapore and Thailand install apps from other app stores than Google Play only if their developers have verified their identity (power users get a harder way around it), and Google plans to do the same everywhere in 2027 ([Android developer verification](https://developer.android.com/developer-verification)).
2. **A certificate would not get rid of the SmartScreen warning anyway**: SmartScreen goes by reputation, which a new certificate has to build up first. What a certificate would give, Setup already gives by signing WinHotCorner on your computer.
3. **WinHotCorner is open source software under the GPLv3, which comes without any warranty.** Whether to trust it, and whether to download and run it, is for you to decide, not for a certificate or the authority that issued it. This is not shirking responsibility: deciding for yourself what to run is a good habit that protects you.

## Checking it

- **The certificate**: open *Manage computer certificates* (`certlm.msc`) → *Trusted Root Certification Authorities* → *Certificates*, and look for `WinHotCorner (made on this computer)`. There is at most one. It says *Code Signing* under *Intended Purposes*, and it has no private key.
- **The signature**: right-click `C:\Program Files\WinHotCorner\WinHotCorner.exe` → *Properties* → *Digital Signatures*.
- **How it runs**: in Task Manager → *Details*, *WinHotCorner.exe* shows *No* under *Elevated* (add the column with right-click on the column headers → *Select columns*). Signed, it also shows up in Task Manager → *Startup apps*.

## Changing your mind

- **To stop signing it**: run Setup again and choose *Install without signing (limited)*. Setup removes the certificate and installs the unsigned version.
- **To sign it later**: run Setup again and choose the standard install.
- **Uninstalling** WinHotCorner removes the certificate too.

> [!TIP]
> Setup remembers your choice, so updates keep it.

## If signing is not possible

Some computers do not allow new trusted certificates, for example because of a company policy, and some security software blocks it. Setup then says that it could not sign WinHotCorner and installs it [without signing](#installing-without-signing) instead. It does not try again on updates; run Setup again and choose the standard install if you want to retry.

> [!TIP]
> For a silent installation, install without signing with `/MERGETASKS="unsigned"` on the command line.

## Starting at sign-in

Signed, WinHotCorner starts at sign-in from Windows' list of startup apps instead of from a scheduled task. If you turn it off in Task Manager → *Startup apps*, it no longer starts when you sign in; the WinHotCorner Control Panel then says that it is not running and offers to run it.

> [!TIP]
> To turn the hot corner off, it is recommended to use the control panel instead.

More technical details are in the [technical notes](technical.md#startup-and-privileges).
