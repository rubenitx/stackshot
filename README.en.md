<div align="center">

<img src="docs/banner.svg" alt="Stackshot: beautiful screenshots for Windows" width="100%">

<br>

<a href="https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe"><img src="https://img.shields.io/badge/%E2%AC%87%EF%B8%8F%20Download%20Stackshot-Windows%2010%20%7C%2011-4F7BFF?style=for-the-badge&labelColor=8B5CF6" alt="Download Stackshot for Windows"></a>

[![Latest release](https://img.shields.io/github/v/release/rubenitx/stackshot?color=8B5CF6)](https://github.com/rubenitx/stackshot/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/rubenitx/stackshot/total?color=4F7BFF)](https://github.com/rubenitx/stackshot/releases)
[![Build](https://github.com/rubenitx/stackshot/actions/workflows/build.yml/badge.svg)](https://github.com/rubenitx/stackshot/actions/workflows/build.yml)
[![MIT License](https://img.shields.io/badge/license-MIT-14B8E6)](LICENSE)
![Under 1 MB](https://img.shields.io/badge/size-%3C%201%20MB-30D158)
![No admin](https://img.shields.io/badge/install-no%20admin-FF9F0A)

**[Español](README.md)** · [Download](https://github.com/rubenitx/stackshot/releases/latest) · [Changelog](CHANGELOG.md) · [Security](SECURITY.md)

</div>

---

Press <kbd>Print Screen</kbd>, pick an area and your screenshot **floats in a corner**, ready to drag into a chat, paste anywhere or annotate in a second. Whatever you don't save cleans itself up — no more desktop full of `Screenshot (37).png`.

The **CleanShot X experience, finally on Windows**: one `.exe` under 1 MB, free, open source, nothing else to install.

> [!NOTE]
> The app's interface is in Spanish for now. An English UI is on the roadmap — contributions welcome!

<div align="center">
<img src="docs/demo.gif" alt="Stackshot in action: launch animation, Pixel the mascot and settings" width="760">
</div>

## ✨ Features

| | |
|---|---|
| 🪟 **Floating stack** of thumbnails with smooth animations | 🖱️ **Drag & drop** into Teams, Slack, Outlook, your AI chat… |
| 🎯 **Pixel-perfect selection** with magnifier and live size | 📜 **Scrolling capture** of whole pages |
| ✏️ **Quick editor**: curved arrows, steps, text, pixelate… | 🎨 **Presentation backdrop**, CleanShot-style |
| 🎬 **MP4 video and GIF** with the cursor | 📌 **Pin to screen** on top of everything |
| 🤖 **Pixel**, a tiny robot mascot | 🔒 **Nothing leaves your PC**: no cloud, no telemetry |

- **Floating stack**: drag to any app (the thumbnail follows your cursor), paste with <kbd>Ctrl</kbd>+<kbd>V</kbd> and it retires itself, swipe left to dismiss, mouse wheel when there are many (up to 20). Follows your mouse across monitors and stays out of screen sharing.
- **Selection**: the screen freezes instantly; drag an area or click a window (with transparent Windows 11 rounded corners). Smooth even on several 4K monitors — only what changes is redrawn.
- **Scrolling capture** (<kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Print Screen</kbd>): scroll (or press **Auto**) and Stackshot stitches one long image without repeating sticky headers or footers.
- **Editor**: tapered arrows that bend, rectangles, ellipses, numbered steps, text, highlighter, pixelate, non-destructive crop; move, resize or delete any mark afterwards.
- **Backdrop** (<kbd>B</kbd>): 13 gradients (including your blurred wallpaper), padding, rounded corners, shadow and aspect ratio — also for videos and GIFs.
- **The app window**: one-click captures, every shortcut and setting in one place, a launch animation, and **Pixel**, who follows your mouse, jumps when poked, celebrates every capture and naps when ignored. Closing the window keeps Stackshot in the tray.

<img src="docs/pixel.gif" alt="Pixel, the Stackshot mascot" width="180" align="right">

## 📥 Install

1. **[Download Stackshot.exe](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe)** and open it.
2. Choose whether it starts with Windows and where to keep saved screenshots.
3. Click **Instalar y empezar**. Done — press <kbd>Print Screen</kbd>.

Per-user install, **no admin rights**, listed in **Settings > Apps** for uninstalling. Windows 10 and 11, nothing else needed (.NET Framework 4.8 ships with Windows).

> [!NOTE]
> **"Windows protected your PC"**: Stackshot isn't code-signed yet, so SmartScreen warns the first time. Click **More info > Run anyway**. You can verify that the `.exe` was built from this source — see [SECURITY.md](SECURITY.md).

<br clear="right">

## ⌨️ Shortcuts

| Shortcut | Action |
|---|---|
| <kbd>Print Screen</kbd> | Capture an area (or click a window / the desktop) |
| <kbd>Ctrl</kbd>+<kbd>Print Screen</kbd> | Capture the screen under the mouse |
| <kbd>Alt</kbd>+<kbd>Print Screen</kbd> | Capture the active window |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Print Screen</kbd> | Scrolling capture (again to finish) |
| <kbd>Shift</kbd>+<kbd>Print Screen</kbd> | Record video (again to stop) |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Print Screen</kbd> | Record a GIF |

## 🔒 Private, secure, light

- No accounts, no cloud, no telemetry. The only download is FFmpeg 9.0.2 the first time you record, checked against a SHA-256 hard-coded in the source.
- Every release is built by GitHub Actions from this source, with a SHA-256 sum and a signed **build provenance attestation**: `gh attestation verify Stackshot.exe --repo rubenitx/stackshot`.
- ~40 MB of RAM and 0% CPU when idle; the thumbnail appears as soon as you release the mouse.

## 🛠️ Build from source

No Visual Studio needed — Windows ships the C# compiler:

```powershell
git clone https://github.com/rubenitx/stackshot
cd stackshot
.\build.ps1        # bin\Stackshot.exe
.\build.ps1 -Run   # build and run without installing
```

## 📄 License

[MIT](LICENSE) © 2026 Rubén Martínez. FFmpeg is not bundled; it is downloaded separately under its own license (GPL) only if you record video or GIF.

<div align="center">
<br>
<sub>Saves you time? Leave a ⭐ — Pixel will celebrate. 🤖</sub>
</div>
