<div align="center">

<img src="docs/banner.svg" alt="Stackshot. Capture, annotate and share in seconds." width="100%">

**Capture, annotate and share in seconds.**<br>
A screenshot tool for Windows with floating thumbnails, a quick editor, scrolling capture, video and GIF.

[![Latest release](https://img.shields.io/github/v/release/rubenitx/stackshot?color=4F7BFF)](https://github.com/rubenitx/stackshot/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/rubenitx/stackshot/total?color=8B5CF6)](https://github.com/rubenitx/stackshot/releases)
[![Build](https://github.com/rubenitx/stackshot/actions/workflows/build.yml/badge.svg)](https://github.com/rubenitx/stackshot/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-14B8E6)](LICENSE)

[Download](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe) · [Changelog](CHANGELOG.md) · [Security](SECURITY.md) · [Español](README.es.md)

</div>

---

Press <kbd>Print Screen</kbd>, select an area and the screenshot stays in the corner of the screen, ready to be dragged into a chat, pasted with <kbd>Ctrl</kbd>+<kbd>V</kbd> or annotated. Screenshots you don't keep are deleted automatically after an hour.

Stackshot brings the CleanShot X workflow to Windows. It ships as a single executable of about 0.5 MB, installs per user without administrator rights and doesn't depend on any other program.

<div align="center">
<img src="docs/demo.gif" alt="Stackshot main window, launch animation and settings" width="760">
</div>

> [!NOTE]
> The user interface is currently in Spanish. An English translation is planned and contributions are welcome.

## Features

- **Floating stack.** Each screenshot appears as a thumbnail in the bottom-left corner. Drag it into any application, paste it, swipe it away, or scroll through up to 20 of them. It follows the mouse across monitors and is excluded from screen sharing.
- **Precise selection.** The screen freezes instantly. Drag an area with a magnifier and live dimensions, or click a window to capture it, including the rounded corners of Windows 11.
- **Scrolling capture.** Select an area and scroll, or let Stackshot scroll for you; the frames are stitched into a single image without repeating sticky headers or footers.
- **Editor.** Arrows that can be curved, rectangles, ellipses, numbered steps, text, highlighter, pixelation and non-destructive cropping. Every mark can be moved, resized or deleted afterwards.
- **Presentation backdrop.** Place a screenshot on a gradient or on your blurred wallpaper, with padding, rounded corners, shadow and a fixed aspect ratio. The same options apply to recordings.
- **Video and GIF.** Record an area of the screen as MP4 or GIF, including the cursor.
- **Pin to screen.** Keep a screenshot floating above every window, with zoom and opacity.
- **Main window.** Every action, shortcut and setting in one place, with Pixel, a small animated mascot that can be renamed, recoloured or hidden. Closing the window keeps Stackshot running in the tray.

<table>
  <tr>
    <td width="50%"><img src="docs/stack-hover.png" alt="Floating thumbnails with their toolbar"></td>
    <td width="50%"><img src="docs/region.png" alt="Area selection with magnifier and dimensions"></td>
  </tr>
  <tr>
    <td><img src="docs/editor.png" alt="Editor with a curved arrow, numbered steps and text"></td>
    <td><img src="docs/backdrop.png" alt="Editor with the presentation backdrop enabled"></td>
  </tr>
  <tr>
    <td><img src="docs/app.png" alt="Main window"></td>
    <td><img src="docs/settings.png" alt="General settings"></td>
  </tr>
</table>

## Installation

1. Download **[Stackshot.exe](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe)** and run it.
2. Choose whether it starts with Windows and where saved screenshots go.
3. Click **Instalar y empezar**, then press <kbd>Print Screen</kbd>.

Stackshot installs to `%LOCALAPPDATA%\Programs\Stackshot`, adds a Start menu entry and appears in **Settings > Apps** for uninstalling. It runs on Windows 10 and 11 with the .NET Framework 4.8 that ships with Windows. To update, run a newer `Stackshot.exe`.

Because the executable is not code-signed yet, SmartScreen shows a warning the first time it runs (**More info > Run anyway**). Each release can be verified as described in [SECURITY.md](SECURITY.md).

<details>
<summary>Silent installation</summary>

```powershell
Stackshot.exe --install --startup --folder "D:\Screenshots"   # add --no-start to skip launching it
Stackshot.exe --uninstall --quiet
```
</details>

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| <kbd>Print Screen</kbd> | Capture an area, a window or the whole screen |
| <kbd>Ctrl</kbd>+<kbd>Print Screen</kbd> | Capture the screen under the mouse |
| <kbd>Alt</kbd>+<kbd>Print Screen</kbd> | Capture the active window |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Print Screen</kbd> | Scrolling capture (press again to finish) |
| <kbd>Shift</kbd>+<kbd>Print Screen</kbd> | Record video (press again to stop) |
| <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Print Screen</kbd> | Record a GIF |

All shortcuts can be changed in the **Atajos** section of the main window. If Windows 11 reserves <kbd>Print Screen</kbd> for the Snipping Tool, Stackshot offers to release it for the current user.

<details>
<summary>Editor shortcuts</summary>

| Key | Action |
|---|---|
| <kbd>F</kbd> <kbd>R</kbd> <kbd>E</kbd> <kbd>T</kbd> <kbd>N</kbd> <kbd>H</kbd> <kbd>P</kbd> <kbd>C</kbd> | Arrow, rectangle, ellipse, text, numbers, highlight, pixelate, crop |
| <kbd>B</kbd> | Presentation backdrop |
| <kbd>1</kbd>–<kbd>5</kbd>, <kbd>−</kbd> <kbd>+</kbd> | Colour, stroke width |
| <kbd>Ctrl</kbd>+<kbd>Z</kbd>, <kbd>Ctrl</kbd>+<kbd>Y</kbd> | Undo, redo |
| <kbd>Ctrl</kbd>+<kbd>C</kbd>, <kbd>Ctrl</kbd>+<kbd>S</kbd> | Copy, save |
| <kbd>Enter</kbd> | Apply, copy and close |
| <kbd>Delete</kbd>, arrow keys | Delete or move the selected mark |
</details>

## Privacy and security

- No accounts, cloud services or telemetry. Screenshots never leave the computer.
- The only network access is a one-time download of FFmpeg 9.0.2 when recording for the first time. The package is checked against a SHA-256 hash stored in the source code and discarded if it doesn't match.
- Temporary screenshots live in `%LOCALAPPDATA%\Stackshot\temp` and are removed after an hour, never while they are still on screen.
- Releases are built by GitHub Actions from this repository and published with a SHA-256 checksum and a signed build provenance attestation.

Details and the vulnerability reporting process are in [SECURITY.md](SECURITY.md).

## Performance

| | |
|---|---|
| Idle | About 40 MB of RAM and no CPU usage |
| Area selection | Under 1 ms per mouse movement; only the changed regions are redrawn |
| After a capture | The thumbnail appears immediately; the PNG is written in the background |
| Executable | About 0.5 MB, no runtime to install |

## Building from source

Visual Studio is not required; the C# compiler included with Windows is enough.

```powershell
git clone https://github.com/rubenitx/stackshot
cd stackshot
.\build.ps1          # produces bin\Stackshot.exe
.\build.ps1 -Run     # builds and runs it without installing
```

| Folder | Contents |
|---|---|
| `src/` | The application (C# 5, WinForms): capture, thumbnails, editor, recording, main window and installer |
| `assets/` | Logo and icon, drawn in code by `tools\make-logo.ps1` |
| `docs/` | README images, regenerated on a synthetic desktop by `tools\make-screenshots.ps1` |

## Contributing

Bug reports and suggestions are welcome in [Issues](https://github.com/rubenitx/stackshot/issues). The code is commented in Spanish; build instructions, conventions and an overview of the source are in [CONTRIBUTING.md](CONTRIBUTING.md). Please report security issues privately as described in [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © 2026 Rubén Martínez.

FFmpeg is not bundled. It is downloaded separately, under its own license (GPL), only when recording video or GIF.
