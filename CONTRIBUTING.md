# Contributing to Stackshot

A screenshot tool for Windows in the spirit of CleanShot X: its own capture (scrolling capture too), a stack of floating
thumbnails, a quick editor with a presentation backdrop, video and GIF through FFmpeg, a main window with a customizable
mascot, and a per-user installation. A single `.exe` (about 0.7 MB) written in C# 5 + WinForms on .NET Framework 4.8.
Windows 10 and 11 only. The user interface is in Spanish; code comments are in English.

## Build and test

- `.\build.ps1` produces `bin\Stackshot.exe` with the `csc.exe` of the .NET Framework that ships with Windows (no Visual
  Studio needed). It compiles with `/warnaserror+`: any warning (an unused variable, a member hiding one of Form or
  Control...) breaks the build.
- `.\build.ps1 -Run` runs it without installing (`--portable`); `.\build.ps1 -Install` installs or updates it for the
  current user.
- `.\tools\build-msi.ps1` produces `bin\Stackshot.msi` (after `build.ps1`) from `tools\Stackshot.wxs` with WiX Toolset
  3.14.1, downloaded once to `bin\wix` and checked against a fixed SHA-256. It installs per user, without administrator
  rights. When the MSI installed it, `Installer.ManagedByMsi` (a flag in `HKCU\Software\Stackshot`) makes the app skip its
  own shortcuts and Apps entry, never update by copying itself, and uninstall through `msiexec /x`. The `UpgradeCode` in
  the `.wxs` never changes. To close cleanly during an upgrade, the app handles WM_CLOSE and WM_ENDSESSION in
  `CloseListener`.
- To try the MSI locally: `msiexec /i bin\Stackshot.msi /qn /l*v log.txt`, and `/x` to remove it. Uninstalling deletes
  `%LOCALAPPDATA%\Stackshot` (settings, FFmpeg, temporary files), so back it up first.
- `tools\make-logo.ps1 [-Preview sheet.png]` draws the logo and icon with `src\Home\LogoArt.cs` (GDI+; headless browsers
  don't work on every machine).
- README media, which must never show real content:
  - `tools\make-screenshots.ps1` regenerates the `docs\*.png` screenshots with `tools\Studio.cs`, on a synthetic desktop
    built on the monitor without the mouse.
  - `tools\make-reel.ps1` renders `docs\promo.gif`, `docs\demo.gif` (a tour of the real main window, photographed
    offscreen with `PrintWindow`), `docs\mascot.gif` and `docs\characters.png` with `tools\Reel.cs`, `tools\Promo.cs`
    and the GIF encoder in `tools\GifWriter.cs`. Everything runs offscreen with read-only settings.
- Executable options: `--install [--startup] [--folder <path>] [--no-start]`, `--uninstall [--quiet]`, `--restart`,
  `--portable`, `--background` (doesn't open the window; used by the Startup shortcut), `--test` (not hidden from captures,
  leaves the clipboard, hotkeys and temporary files alone), `--home` (with `--test`, opens the window),
  `--edit <image|video>`.
- Data: `%LOCALAPPDATA%\Stackshot` (`settings.ini`, `temp\`, `ffmpeg\`, `Fondos\` for custom backgrounds,
  `stackshot.log`). Installation: `%LOCALAPPDATA%\Programs\Stackshot`. Mutex `Local\Stackshot`; events
  `Local\Stackshot.Quit` (close) and `Local\Stackshot.Show` (running the .exe again shows the existing window).
- To test windows without touching the working screen, compile `src\` with a separate `Main` in `--test` mode, set
  `Settings.ReadOnly`, and render offscreen (`Mascot.RenderStill`, `PrintWindow` on a window placed off the desktop).

## Code rules

- **C# 5 only**: no `$""`, `?.`, `nameof`, expression-bodied members, `out var` or auto-property initializers.
- **ASCII literals**: accented characters in strings are Unicode escapes. Some editing tools turn them into real
  characters; after editing, `perl tools/escape-literals.pl <file>` escapes them again.
- The process is per-monitor DPI aware (PerMonitorV2 manifest): sizes are in physical pixels and scaled with
  `ShotStack.ScaleFor(screen)` / `P()`.
- Floating windows (`FloatWindow`) never take focus, are layered (animated opacity), stay on top and are excluded from
  captures (`WDA_EXCLUDEFROMCAPTURE`). Everything that moves goes through `Anim`, a timer that only runs while something
  animates (0% CPU at rest).
- The whole app uses the macOS-style palettes (`Mac` in the main window, `Theme` elsewhere) and hand-drawn line icons
  (`Icons`). The main window (`HomeWindow`) draws into two `Dib` layers (sidebar and content) that are only rebuilt when
  something changes, and composes each repaint in its own back buffer, copying to the screen only the invalidated
  rectangle. With no window visible there is no timer.
- The desktop mascot (`PetWindow`) is a per-pixel-alpha layered window; without a bubble or the menu only the mascot's own
  box is sent to `UpdateLayeredWindow`, and its frame rate drops when idle, asleep or when the user is away.
- Nothing heavy on the UI thread: each capture's PNG is written on another thread (`BeginWrite`; anyone who needs the
  file calls `ShotStack.WaitWritten`), so is the clipboard copy (`TrackedData`), and the wallpaper used by `Backdrop` is
  preloaded at startup. Mascot previews in the main window render on the thread pool.
- The region picker (`RegionPicker`) composes each changed part with `BitBlt` from two `Dib`s prepared once (bright and
  dimmed, with the hints already drawn), and recomposes everything Windows asks for when another window passes over it.
- FFmpeg is downloaded from a fixed version (`FfmpegSetup`: `Version`, `Url`, `Sha256` in `Recorder.cs`); upgrading means
  changing all three together (the SHA-256 can be checked against the `digest` field of the GitHub API). Videos opened in
  the editor are read with a format whitelist (`Recorder.SafeInput`).
- `--test` and the test tools set `Settings.ReadOnly`: they never write the real settings.
- Comments: in English and minimal; only the why of what isn't obvious.
- Mascot catalogs (`MascotParts`) are stored by index in `settings.ini`: new items always go at the end.

## Map

| File | What it does |
|---|---|
| `src/Program.cs` | Startup: welcome window, installation, updates, command-line options, version (`AssemblyVersion`) |
| `src/Setup.cs` | `Installer` (copy, shortcuts, uninstall entry, Print Screen, MSI mode) and `SetupWindow` (welcome) |
| `src/CloseListener.cs` | Hidden window that closes Stackshot cleanly when Windows asks (MSI, sign-out) |
| `tools/Stackshot.wxs`, `tools/build-msi.ps1` | MSI package for organizations |
| `src/Settings.cs` | `settings.ini` |
| `src/ShotStack.cs` | The stack: hotkeys, tray, capture and save, thumbnails, scrolling, temporary file cleanup |
| `src/Look.cs` | macOS-style palette (`Mac`) and hand-drawn line icons (`Icons`) |
| `src/Home/HomeWindow.cs`, `HomePages.cs` | Main window: custom frame with Windows 11 buttons (minimize, close to tray), sections (Home, Shortcuts, General, Backdrop and editor, Recording, Mascot, About), profiles and their controls |
| `src/Home/Mascot.cs` | The mascot: moods, physics, eyes and mouth, particles, tricks; `RenderStill` for previews |
| `src/Home/MascotParts.cs`, `MascotDraw.cs` | Catalog (`MascotLook`: species, color, eyes, hat, outfit, face accessory; friendship levels and unlocks) and the drawing of the original pieces |
| `src/Home/MascotAnime.cs`, `MascotMore.cs`, `MascotExtra.cs`, `MascotCast.cs`, `MascotHeroes.cs` | Character tributes (`AnimeNames`, `AnimeInspiration`), extra species (dog, chibi, penguin, panda, fox, frog), hair styles, helmets, outfits and face paint |
| `src/Home/MascotTalk.cs` | What the mascot says, by personality |
| `src/Home/PetWindow.cs` | The desktop mascot: walks, naps, hops onto windows, drag and drop, comic menu, going home, remembered spot; hides for full-screen apps on its monitor |
| `src/Home/LogoArt.cs`, `Intro.cs` | The logo as a parametric drawing (used by the app and `tools\make-logo.ps1`) and the launch animation |
| `src/Updater.cs` | Updates from GitHub Releases: daily check, download, verification (SHA-256 + RSA-PSS signature, version match) and install |
| `src/Home/TrayMenu.cs` | Tray menu with its own renderer |
| `src/Capture/Dib.cs` | Shared GDI/GDI+ canvas |
| `src/Capture/ScrollCapture.cs` | Scrolling capture: session, stitching by row hashes, bar and frame |
| `src/Capture/Recorder.cs`, `Webcam.cs` | Screen recording through FFmpeg (quality levels, GIF) and the camera bubble |
| `src/Editor/Backdrop.cs`, `BgPanel.cs` | Presentation backdrop (gradients, wallpaper, custom images, video) and its strip in the editor |
| `src/Editor/Timeline.cs` | Video trimming and export options (speed, size, format) |
| `src/Card.cs`, `src/Chip.cs` | Thumbnail and "N older / N newer" pills |
| `src/FloatWindow.cs`, `src/Animation.cs` | Base for floating windows, springs and tweens |
| `src/Capture/*` | Global hotkeys, screen and window capture, region selection, pin to screen, shutter sound |
| `src/Editor/*` | Editor: marks (`Shapes`), canvas with selection and history (`Canvas`), toolbar (`Toolbar`), window (`Editor`) |
| `src/Ui.cs` | Custom controls (`DarkForm`, `Pill`, `Toggle`, `HotkeyBox`, `Progress`) and the font cache (`Fonts`) |
| `src/TrackedData.cs`, `src/FileDrag.cs` | Clipboard that notices pastes; dragging with a thumbnail next to the cursor |

## Releasing a version

Bump `AssemblyVersion` / `AssemblyFileVersion` in `src/Program.cs`, record the changes in `CHANGELOG.md`, then create and
push the tag `vX.Y.Z`. The workflow `.github/workflows/build.yml` builds and attaches `Stackshot.exe`, `Stackshot.msi`,
their `.sha256` files and a build provenance attestation to the release. A published version is never rebuilt: every
change is a new version. Installed copies (1.3.0 and later) check GitHub once a day and show the new version in the tray,
on the home page and under About.

### Update signatures

The app only installs a `Stackshot.exe` signed with the RSA private key whose public half is in `src/Updater.cs`, and
only if its file version matches the release. The private key never goes into the repository. There are two ways to
sign a release:

- **Locally (no secret on GitHub):** after the release is published, run `.\tools\sign-release.ps1 vX.Y.Z`. It downloads
  `Stackshot.exe` from the release, checks it against the published SHA-256, signs it with the key in
  `%USERPROFILE%\.stackshot-keys\update-signing-key.xml`, verifies the signature and uploads only `Stackshot.exe.sig`.
- **In CI:** store the key (.NET XML format) as the repository secret `UPDATE_SIGNING_KEY` and the workflow signs during
  the build.

Without a signature the release is published as usual and the app offers its download page instead of installing it.
If the key is lost, generate a new one, replace the public key in `Updater.cs` and publish that version manually once.
