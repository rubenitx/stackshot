# Security

## What Stackshot does on your computer

- **Nothing leaves your computer.** There are no accounts, cloud services or telemetry, and screenshots are never synced through the Windows cloud clipboard.
- **Network access is limited to two things:**
  - the download of [FFmpeg](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2) the first time you record a video or GIF, and only after you accept it. It is always the same version (9.0.2), from a fixed URL, and its **SHA-256 hash is stored in the source code**; if the download doesn't match, nothing is installed. Only `ffmpeg.exe` is extracted from the package. Stackshot never runs an `ffmpeg.exe` found on `PATH`;
  - a **daily update check** against `api.github.com` (latest release of this repository). It sends nothing but a standard request and can be turned off in General.
- **Updates are verified before they run.** An update installed from the app is only accepted from this repository's release downloads, its SHA-256 must match the digest GitHub reports for the asset, and its **RSA-PSS signature** must verify against the public key in [src/Updater.cs](src/Updater.cs). The private key exists only as a secret of the release workflow. Anything missing or wrong is discarded. Installations managed with the MSI are never updated by the app; they only show a notice.
- **No administrator rights.** Stackshot installs per user (`%LOCALAPPDATA%\Programs\Stackshot`) and only writes to:
  - `%LOCALAPPDATA%\Stackshot` (settings, temporary captures, log, FFmpeg);
  - the folder you choose for saved screenshots;
  - the Start menu shortcut and, if enabled, the Startup shortcut;
  - `HKCU\...\Uninstall\Stackshot` (for Settings > Apps);
  - `HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled`, only if you ask it to use <kbd>Print Screen</kbd>.
- **Automatic cleanup** only deletes temporary captures older than one hour, and only inside `%LOCALAPPDATA%\Stackshot\temp`.
- **System DLLs are loaded from System32** once the program starts, even when the executable sits in a folder with other files (such as Downloads). The installed copy lives in its own folder.
- **Careful writes.** Settings are saved atomically, and a single key without modifiers cannot be assigned as a global shortcut.
- **Stackshot's own windows are excluded** from screenshots and screen sharing (`WDA_EXCLUDEFROMCAPTURE`), including the optional desktop mascot.
- **Redaction.** The editor's "Tapar" tool paints an opaque block, so nothing underneath survives in the output. Pixelation is cosmetic: don't rely on it for passwords or personal data.
- **The build workflow** runs with read-only permissions except when publishing a release, and every action is pinned to a specific commit.

## Verifying a release

Every release is built by GitHub Actions from this repository ([build.yml](.github/workflows/build.yml)) and publishes:

- `Stackshot.exe.sig`, the update signature checked by the app (RSA-PSS over the executable, SHA-256);
- `Stackshot.exe.sha256` and `Stackshot.msi.sha256`, the checksums of the executable and of the MSI package:
  ```powershell
  (Get-FileHash .\Stackshot.exe -Algorithm SHA256).Hash
  ```
- a signed **build provenance attestation** (Sigstore) for both files, linking them to the exact commit and workflow run that produced them:
  ```powershell
  gh attestation verify .\Stackshot.exe --repo rubenitx/stackshot
  gh attestation verify .\Stackshot.msi --repo rubenitx/stackshot
  ```

The MSI is built with WiX Toolset 3.14.1, downloaded during the build and checked against a SHA-256 hash stored in [tools/build-msi.ps1](tools/build-msi.ps1). Like the executable, it installs per user and does not require administrator rights.

Neither file is code-signed yet, so Windows SmartScreen shows a warning the first time the executable runs (**More info > Run anyway**). On managed computers where SmartScreen blocks unsigned applications, the MSI can be deployed by the IT department instead.

## Reporting a vulnerability

Please don't open a public issue. Use [Report a vulnerability](https://github.com/rubenitx/stackshot/security/advisories/new) to send a private report. You can expect an answer within a few days; the fix will be published and credited to you if you wish.

Only the latest release is supported.
