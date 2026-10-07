# Changelog

## 1.1.1

- New tagline: "Captura, marca y comparte en segundos" in the launch animation, the welcome window and the About section.
- English README as the main one, with the Spanish version in `README.es.md`.

## 1.1.0

- **Main window.** Opens when Stackshot is launched (also if it is already running) and brings together one-click captures, every shortcut (click a field and press the new combination) and every setting, applied immediately. Closing it keeps Stackshot in the tray; it does not open when starting with Windows.
- **Pixel, the mascot.** Follows the mouse with its eyes, blinks, jumps when clicked, gets dizzy if clicked too often, celebrates every capture, shows tips and falls asleep when ignored. Name and colour can be changed, or it can be hidden.
- **Launch animation** when the window opens (can be turned off under General).
- **Tray menu** redesigned with a dark theme, rounded corners and icons.
- **Scrolling capture** (<kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Print Screen</kbd>): stitches a long image while scrolling, manually or automatically.
- **Presentation backdrop** in the editor: 13 backgrounds, padding, rounded corners, shadow and aspect ratio. Also available for videos and GIFs through "Presentar".
- **Faster capture.** The area selector redraws only the regions that change, the thumbnail appears immediately and the PNG files are written in the background.
- **Security.** FFmpeg is downloaded from a fixed version and checked against a SHA-256 hash stored in the source code; system DLLs are loaded from System32; settings are written atomically; single keys without modifiers can no longer be assigned as global shortcuts; releases include a build provenance attestation.

## 1.0.0

- **Built-in capture** with no third-party tools: area (with magnifier, dimensions and window snapping), full screen and active window, with configurable shortcuts.
- **Floating stack** of thumbnails with animations: drag to other applications, "pasted means used", swipe to dismiss, mouse wheel to browse up to 20 captures, follows the mouse across monitors.
- **Quick editor**: curved arrows, rectangles, ellipses, numbers, text, highlighter, pixelation and cropping; marks can be moved, resized and deleted; undo and redo.
- **MP4 video and GIF** of an area, including the cursor (FFmpeg is downloaded and verified the first time).
- **Pin to screen**: the capture floats above every window, with zoom and opacity.
- **Per-user installation** without administrator rights, with a welcome window, optional start with Windows, an entry in Settings > Apps and silent installation for IT departments.
- About 40 MB of RAM and no CPU usage when idle.
