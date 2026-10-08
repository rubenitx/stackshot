# Changelog

## 1.3.1

- **Zoom in the editor.** Ctrl + mouse wheel zooms around the pointer, the wheel scrolls, the middle button drags, and Ctrl+0 / Ctrl+1 / Ctrl++ / Ctrl+- fit, show at 100% and zoom. Tall scrolling captures open fitted to the width and scrolled to the top, so they can be read right away.
- **Save as PDF.** A new PDF button (Ctrl+P) in the editor saves the result with its marks and backdrop; long captures are split into A4-width pages, cutting between lines of text, and stay lossless so they can be zoomed.
- **Print Screen just works.** Assigning Print Screen to a shortcut now takes it back from the Windows 11 Snipping Tool automatically (current user only) and applies the change right away where Windows allows it.
- **No more black band.** The main window always paints its real size, which could differ from the design size after a DPI or monitor change and left a black strip at the bottom and right.
- **New logo in the README banner.**
- **Clearer updates.** A new version without an update signature now offers its download page ("Ver la versión") instead of an install that can only fail.
- **Local release signing.** `tools/sign-release.ps1` signs a published release on the maintainer's computer and uploads only the signature, so the private key never goes to GitHub.

## 1.3.0

- **A mascot you can make your own.** Twelve species (robot, cat, bunny, ghost, slime, dragon, dog, chibi, penguin, panda, fox and frog), 23 colors, 5 eye styles, 43 hats and hairstyles, 36 outfits and 19 face accessories, all previewed live on the mascot. One-click styles (Halloween, Chulo, Cute, Vampiro, Dragoncito) and a random "Sorpréndeme". Seasonal costumes for Halloween and Christmas.
- **Characters: fan tributes.** 38 one-click costumes (Finn, Jake, BMO, Pakkun, Naruto, Sasuke, Kakashi, Edward Elric, Eren Jaeger, Levi, Luffy, Zoro, Goku, Vegeta, Tanjiro, Nezuko, Gojo, Gon, Killua, Hisoka, Saitama, Shin-chan, Pikachu, Doraemon, Totoro, Kratos, Doom Slayer, Spider-Man, Iron Man, Batman, Superman, Flash, Claude, Codex, a knight, an astronaut, a wise wizard and a superhero; hovering one shows where it comes from) built from new pieces: six new characters (dog, chibi, penguin, panda, fox and frog), hair styles, helmets, a bear hood, a ninja headband, a straw hat, capes, vests, armor, a space suit, beards and a blindfold. Drawn from scratch in the mascot's style with no logos or emblems; see the note in the README.
- **The desktop mascot explores.** It remembers where it stood across restarts and sign-ins. It hops onto the window in front of you, walks along its top edge and rides along when you move it; dropped over a window it lands there, and it falls back to the taskbar when the window closes. "Vete a casa" now waves goodbye and walks off the screen. One click opens its menu, quick extra clicks tickle it (four make it dizzy), right-click opens the menu right away, and its bubble and menu are never cut off at the screen edges.
- **README media.** A promotional video, a tour of the real main window, the mascot reel and a character sheet, all rendered offscreen by `toolsmake-reel.ps1` with a new high-quality GIF encoder.
- **Small comforts.** Double-clicking the tray icon opens the main window, restored and in front. The mascot's speech bubble always points at it: above its head (leaving room for hats), below it when the head is scrolled out of view, or as a plain note at the top when the mascot is not visible.
- **Your own backgrounds.** Add any image as a presentation backdrop from "Fondo y editor"; it is copied into Stackshot (up to 4K) and appears next to the gradients, also in the editor.
- **Quick profiles.** Performance, Balanced or Complete set the launch animation, the desktop mascot, greetings and video quality in one click.
- **Friendship levels.** Every capture adds a point; new levels unlock the crown, the halo, the Galaxia color and, at the top level, Oro.
- **Personality.** Cheerful, calm or cheeky: it changes what the mascot says, how much it moves and when it naps. It now fidgets, looks around and yawns before falling asleep. New tricks: a little dance with music notes and a pirouette with sparkles; it breathes softly while idle.
- **Mascot on the desktop (optional).** It wanders above the taskbar, follows the mouse with its eyes, naps when you are away and can be dragged anywhere. It hides during full-screen apps and is never captured or shared on screen.
- **Comic menu on the desktop mascot.** Click it and a comic-style speech bubble offers what it can do: capture, record, open your captures, change clothes, a trick, a nap, open Stackshot or go home.
- **Video editor with trimming and export options.** "Editar" on a recording opens a timeline with a frame strip: drag the yellow handles to trim the start and the end, click the strip to see any moment, then add marks, crop and a backdrop as before. Export at 1×, 1.5× or 2× speed, at the original size, 1080p or 720p, and as MP4 or GIF.
- **Camera bubble.** Optionally show your webcam in a round or square bubble in a corner of the recording, Loom style. Drag it anywhere; double-click switches the shape and right-click the size.
- **Higher video quality.** "Alta" records at 60 fps with much more detail and saves instantly; "Máxima" records at 60 fps losslessly and encodes at very high quality once you stop, so text stays as sharp as on screen.
- **Cleaner, more Apple-like home.** A calm hero with one primary action, quieter tiles and layered cards; the sidebar widget is now a proper capture card with its own button. The editor, thumbnails and recording bars now share the same macOS graphite palette and blue accent, and the welcome window uses grouped cards and key caps.
- **In-app updates.** Stackshot checks GitHub once a day (can be turned off) and updates in one click. Every update is verified with its SHA-256 and a signature before it runs. MSI installs only get a notice.
- **New "Tapar" tool (X)** in the editor: a solid block that really removes what is underneath, safer than pixelation for sensitive data. Pixelation now uses larger blocks.
- **New logo and launch animation.**
- **Smoother.** The mascot page repaints only what changes and renders its previews in the background (about five times faster), the speech bubble no longer gets stuck or covers text when clicked repeatedly, and the desktop mascot no longer hides itself in normal use. Dragging the desktop mascot now follows the mouse instantly, animations run at real 32 and 64 fps instead of being rounded down by the system timer, and the main window, area picker and desktop mascot copy and clear only what changed.
- **Hardening.** Updates refuse a signed build whose version does not match the release (no rollback to an older build), helper programs start from their full system path, camera names cannot break out of the FFmpeg command, and videos opened in the editor only accept real video formats.
- **Security and housekeeping.** The installer no longer removes the download mark explicitly, the log rotates at 1 MB, screenshots are never sent to the cloud clipboard, FFmpeg is only taken from the verified download or an explicit choice, and code comments are now in English.

## 1.2.0

- **MSI package for organizations.** Every release now includes `Stackshot.msi` for Intune, Configuration Manager or `msiexec`. It installs per user without administrator rights, with `STARTWITHWINDOWS` and `LAUNCHAPP` options. Upgrades keep the user's settings and close and reopen Stackshot automatically, without a restart. Uninstalling removes the application and its data, and existing installations made with `Stackshot.exe` are adopted without duplicate entries.
- **Graceful shutdown on request.** Stackshot now closes cleanly when Windows Installer, the Restart Manager or a sign-out asks it to, and keeps the clipboard contents.

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
