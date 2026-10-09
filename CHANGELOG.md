# Changelog

## 2.0.0

A complete visual redesign in the spirit of CleanShot X and macOS, with the same features. The interface now draws with WPF, which ships with Windows, so building still needs nothing but the C# compiler bundled with .NET Framework 4.8.

- **One design system.** A single palette for light and dark (macOS system colors), one set of line icons in the SF Symbols style, the same radii, shadows and type everywhere. New setting: General > Apariencia (as Windows, light or dark); everything switches live.
- **Main window like macOS System Settings.** Mica glass behind a translucent sidebar with colored icons, rounded groups with hairlines, native-feeling switches, segmented controls and buttons, a toolbar that shows the section name when the page scrolls under it, and smooth page transitions. Home has a calm hero with the mascot and one primary action; the mascot page shows live previews of every style, character and piece of clothing.
- **Home your way.** Three views of the main page, switchable from the page itself or General > Apariencia and remembered: Minimalista (the mascot, one question and a bar with every capture mode, plus the latest captures), Widgets (today at a glance: the last capture with Copiar / Editar / Guardar, captures per day, the mascot, every mode, recording, the folder's size and a tip) and Escena (the mascot on stage beside the modes, recent captures below). Only the chosen view is built.
- **Faster, cleaner area picker.** No magnifier and no full-screen crosshair; each move recomposes the changed area from the frozen screen in one go (1.6 ms on average while dragging at 1080p), so no lines are ever left behind.
- **All-in-one capture (optional).** General > Al capturar > Modo todo en uno: after selecting, the area stays with handles to move and resize it (arrow keys move it, Ctrl+arrows resize it) and a bar to choose Capturar, Vídeo, GIF or Desplazar; Enter or a double click captures.
- **Home comes alive.** Each view enters with a short staggered fade when the window opens, then stays fully idle. In Minimalista a blue pill slides from mode to mode under the pointer; in Escena the blurred backdrop fades out softly at the bottom instead of ending in an edge; in Widgets the cards that do something lift gently on hover. Click the mascot's name in any view to rename it in place (up to 16 characters, empty brings back Pixel) and it answers "¡Ahora me llamo…!". Home keeps itself current: a capture taken while it is open appears once saved, reopening the window shows anything new, and the recording state updates as soon as a recording ends.
- **A new sidebar card.** On the other pages the mascot stands on a pool of light in its own color, with its name, level and a thin friendship meter. On Home and the mascot page, where it is already big, the card is a capture button drawn as a selection marquee, with the status and a record button below.
- **Mascot page as a fitting room.** The mascot stands center stage under a soft glow in its color, with its name, species and personality, Sorpréndeme and Ajustes. Below it, a macOS-style dock: tabs with a sliding indicator (Mis looks, Estilos, Personajes, Especie, Color, Gorro, Ropa, Cara), tiles that magnify with their neighbours under the pointer, smooth wheel scrolling, shimmer placeholders while previews load, and a search field that finds any piece by name across every tab. Hover a tile to try it on, click to wear it: the mascot bounces in a burst of sparkles while the glow crossfades to the new color. Star a tile to keep it first; Mis looks holds saved looks and everything starred. Rename, friendship, personality and the desktop switch are still one click away.
- **Tray menu like macOS.** Clicking the tray icon (left or right) opens a rounded panel instead of the Windows context menu: the logo and the app's status, every capture mode with its shortcut as keycaps, the four latest captures to open with a click, then Abrir Stackshot, Abrir capturas guardadas, Cerrar todas las miniaturas, Ajustes and Salir. The highlight glides between rows, the arrow keys and Enter work, and Esc or a click anywhere else closes it. It follows the theme, never appears in captures, opens in a few milliseconds and drops the thumbnail strip on screens too small for it. A capture chosen from it starts about 0.3 s after the click.
- **Updates find you.** Stackshot looks for a new version 30 seconds after starting, every 3 hours and when the computer wakes up or comes back online (backing off after failures). Each version is announced once, in a small notice at the bottom right: "¿Quieres descargarla?" with Descargar e instalar, still verified (SHA-256 and signature) before it installs. New setting General > Instalar actualizaciones automáticamente (off by default): updates download in the background and install at a quiet moment; a dropped download is retried quietly, and an automatic install that fails is offered by hand once instead of being retried. An install never cuts a recording or scrolling capture short: it waits until it is saved. MSI installs only notify. Acerca de shows every state (searching, up to date, downloading, ready, error).
- **Recording page as widgets.** Quality as four cards with bars for sharpness, smoothness and size; sound tiles for the computer and the microphone, each with its device; the camera's shape, size and device with a live preview of where the bubble goes; GIF speed and the video engine.
- **Choose what is recorded.** The computer sound can come from a specific output (speakers, headphones, a dock...), as the microphone already could; a device that is unplugged falls back to Windows' default and comes back when plugged in again. Device pickers are new menus in the app's style.
- **External cameras work.** The camera can now be chosen; names with accents, duplicated names and cameras that only offer MJPEG or high resolutions open correctly (each camera is probed once for a suitable mode); a camera that fails is skipped for the next one, and one that drops out mid-recording is reconnected.
- **Editor.** Options follow the tool or the selected mark (sizes for text and numbers, only colors for the highlighter), a zoom capsule (−, %, +, Ajustar), macOS-style tooltips with the shortcut as a keycap, animated hover and press, a compact toolbar on narrow screens, handles exactly on the corners, and the canvas repaints six times faster while drawing (4.5 ms instead of 28).
- **A quiet recording frame.** The red border is now a fine translucent white line with white corner marks, and it fades out as soon as a video or GIF recording stops, while the bar goes on showing Guardando….
- **Much lighter.** With the main window open and idle, CPU use drops from about 14% of a core to 1.6%: only the mascot's own layer is redrawn, and only as often as it moves. The window is prepared quietly a few seconds after startup, so it opens in about a quarter of a second.
- **Quick Access thumbnails.** Rounded captures with a real soft shadow; on hover the image blurs and dims behind Copiar and Guardar, with round buttons in the corners (close, pin or open, edit, show in folder) that name themselves on hover.
- **Annotate window like CleanShot.** The toolbar is the title bar, tools are centered with the selected one tinted, colors are circles, the main action is the blue button, the capture floats on a neutral surround, and the video timeline has QuickTime-style yellow trim handles.
- **Capture overlays.** The area picker has a softer dim, a fine white edge, a round magnifier with a pixel grid and the size in a dark pill; recording and scrolling capture use dark floating capsules with real shadows; pinned captures are rounded with a shadow; menus follow the theme.
- **Welcome and FFmpeg download** are new Mica windows.
- **Fix.** Each pinned capture's menu keeps its own fonts, so opening one no longer spoils the text of another.
- **Fix.** Trimming a video so that only removed parts remain now shows a message instead of exporting nothing silently.
- **Fix.** The mascot's efficiency mode now also applies to the UI thread, so the window no longer competes with other apps for CPU when idle.
- **Fix.** The mascot's keep-on-top no longer covers Stackshot's own windows or other apps' menus, tooltips and popups.
- **Instant home pill.** In Minimalista the blue pill now appears under the pointer without lag.
- **View selector moved to General.** The choice of home view lives in General > Apariencia only.
- **Smoother auto scroll.** Scrolling capture advances with steadier pacing and fewer skipped frames.
- **Markdown viewer.** Open .md files in a rendered window (headings, lists, tables, code, quotes), with validation and a Tidy action.
- **XML viewer, selectable Markdown, open with Stackshot.** .xml files open in a colored, collapsible, searchable tree (with Formatear, Compactar, XPath and copy as image); the Markdown preview text can be selected and copied; and .md and .xml files can be opened with Stackshot from Explorer (optional, Windows keeps the final choice), in the running copy when there is one.
- **Mascot settings.** Commands, accessories, styles and behaviour (energy, chatter, what it does alone, dozing off) are configurable from Ajustes de la mascota.
- **Crisper text.** Text is drawn sharper across the app.
- **Recording waits for devices.** The bar shows Preparando… until the audio devices and camera are ready, so a recording never starts with silence or a missing camera.
- **Crisper text.** Editors, floating surfaces and bubbles now render text at the display's pixel grid.
- **Markdown paths from IDEs.** Image paths copied from IDEs resolve against the open document, and the Markdown editor is tinted to match the theme.
- **Mascot plays on its own** and roams across every monitor.
- **Command grid menu** for quick access to every command.
- **Recents and temporary captures.** New settings: show or hide recent captures and how long temporary captures are kept.
- **Capture-options choice** is remembered between runs.
- **Fix.** Several layout fixes across Home, the editors and the settings pages.

## 1.4.0

- **Record sound.** The computer's sound (whatever plays: videos, calls, notifications) and/or the microphone, with a microphone picker under Grabación. While recording, the bar shows a live level for each source and a click mutes the microphone. Sound and image share one clock: measured offset 5–45 ms, with no drift however long the recording. The voice is brought to a steady level (EBU R128, one fixed gain, no pumping) and the mix never clips.
- **"Cine" quality.** Lossless capture at 60 fps in step with the monitor, saved at twice the resolution (up to 4K) so 4:2:0 video keeps every pixel's color: thin and colored text look exactly as on screen and survive re-encoding by YouTube, Teams or LinkedIn (49.6 dB PSNR against the original, versus 29.7 dB at native resolution). 320 kbps sound. Saving takes about twice the recording's length at full screen.
- **Smoother recording.** Frames now come straight from the compositor (DXGI Desktop Duplication) on the GPU instead of a GDI copy, on their own thread: every new screen image is taken and placed in its own frame, cut where the monitor's refreshes leave room. Areas larger than 1920×1200 are converted to NV12 on the GPU and encoded by the graphics card when there is one. Rotated monitors, monitors on two graphics cards or machines without duplication fall back to GDI.
- **Exact colors in every player.** Videos are converted with the BT.709 matrix and tagged as sRGB screen content (they used to be converted as BT.601 and left untagged), and the frame rate is always a standard one (60 or 59.94).
- **Editor exports keep the sound**, also when trimming or changing the speed, composite marks and backdrops at full color resolution and encode at the chosen quality.
- **Lighter, airier text everywhere.** The whole interface now draws text with unhinted, fractional glyph positions, like macOS, instead of GDI's pixel-snapped glyphs that looked heavy and cramped.
- **New character: Chopper** (One Piece), with a new reindeer species, the pink doctor's top hat with its white cross, a blue nose, shorts and a Chocolate color.
- **Mascot settings one click away.** An "Ajustes" button on the mascot's card opens its settings page (personality, greetings, seasonal costume, the desktop mascot) instead of leaving them at the bottom of the page.
- **Fix.** The setting that keeps one reusable back buffer for double-buffered windows had been disabled by mistake.

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
- **README media.** A promotional video, a tour of the real main window, the mascot reel and a character sheet, all rendered offscreen by `tools\make-reel.ps1` with a new high-quality GIF encoder.
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
