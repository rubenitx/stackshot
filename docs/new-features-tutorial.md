# Stackshot: New Features Guide & Tutorial

This guide provides a walkthrough of the new features added in the `feature/multi-language-support` release, explaining what each feature does, how to use it, and how it protects your sensitive information and speeds up your workflow.

---

## 1. Multiple Language Architecture & German (Deutsch) Support

### What it is
Stackshot adds **German (Deutsch)** alongside English and Spanish. Some strings still appear in English.

- **German Localization Engine** ([`src/I18n.German.cs`](../src/I18n.German.cs)): Holds translations for navigation, settings, editor controls, dialogs, and notifications.
- **Dynamic Language Detection**: In **Automático / Auto** mode, Stackshot automatically matches your Windows display language:
  - German Windows (`de-DE`, `de-AT`, `de-CH`) -> German (`de`)
  - Spanish Windows (`es-ES`, `es-MX`, `ca-ES`, `gl-ES`, `eu-ES`) -> Spanish (`es`)
  - All other display languages -> English (`en`)
- **Settings Selector**: Under **Settings › General › Appearance › Language**, open the picker and search by name or language code. Choose a language and select **Apply language**.
- **Preview languages**: French, Portuguese, Simplified Chinese, Hindi, Arabic and Bengali translate the picker and common controls. Other text appears in English. Select these languages manually. Automatic mode continues to choose Spanish, German or English based on Windows.

---

## 2. First-Run Language Selector (Welcome Screen)

### What it is
On first installation, the **Welcome** window offers a language selector under the welcome banner.

### How to use
1. Launch `Stackshot.exe` on a fresh system.
2. Open the language button beneath the title.
3. Search for a language, select it and apply your choice.
4. The entire setup wizard re-renders immediately in the selected language, while preserving all your selected toggles (startup options, screenshot hotkeys, sound, and save paths).

---

## 3. One-Click Language Switcher in System Tray

### What it is
Switching languages no longer requires navigating through multiple settings tabs (*Ajustes › General › Apariencia › Idioma*). The system tray popup menu includes a dedicated **Language switcher item** with a globe icon.

### How to use
1. Click the Stackshot icon in your Windows notification tray (next to the clock).
2. Click **Choose language…** to open the searchable picker.
3. Settings, the tray, and the editor toolbar refresh automatically.

---

## 4. Developer Secrets & International PII Redaction

### What it is
The **Sensitive Data Shield** ([`src/Capture/Sensitive.cs`](../src/Capture/Sensitive.cs)) uses Windows OCR to look for these patterns:

- **Cloud & API Secrets**:
  - **GitHub Personal Access Tokens**: Classic (`ghp_...`, `gho_...`, `ghu_...`, `ghs_...`, `ghr_...`) and Fine-Grained tokens (`github_pat_...`).
  - **OpenAI & Anthropic API Keys**: `sk-proj-...`, `sk-ant-...`, `sk-admin-...`, `sk-...`.
  - **Amazon Web Services (AWS)**: Access Key IDs (`AKIA...` and `ASIA...`).
  - **Private Cryptographic Keys**: PEM blocks whose opening marker `-----BEGIN ... PRIVATE KEY-----` is recognized by OCR. The cover spans the space between the recognized lines, so it can also cover nearby content in those rows.
  - **Slack Workspace Tokens**: `xoxb-...`, `xoxp-...`, `xapp-...`.
- **International Personal Data (PII)**:
  - **US Social Security Numbers (SSN)**: Standard 3-2-4 layout (`XXX-XX-XXXX`) validated against official Social Security Administration rules.
  - **German & International IBANs**: German IBAN format (`DE22`) and international banking numbers verified with mod 97-10 check digits.
  - **International Phone Numbers**: Formatted domestic numbers like `(555) 234-5678` or `555-234-5678` with phone labels, and international dialing codes (`+1`, `+44`, `+34`).
  - **Bank Cards**: Formatted card numbers (Luhn algorithm validated).
  - **Spanish Identity Documents**: DNI, NIE, and Social Security numbers.

### How to use
1. Take a screenshot containing code, terminals, or documents: press <kbd>Print Screen</kbd>.
2. In the floating thumbnail, click the **Edit** icon (or press <kbd>Ctrl</kbd> + <kbd>E</kbd>).
3. In the Editor toolbar, click **Sensitive data** (the shield icon) or press the shortcut <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>X</kbd>.
4. Review the detected areas and cover any missed text manually before sharing. OCR can miss text, including tokens broken across words or lines.
5. If you want to reveal a specific item, simply click its redaction box and press <kbd>Delete</kbd> or <kbd>Backspace</kbd>.

> [!TIP]
> **Automatic Background Redaction**: Enable it under **Settings › General › Sensitive data**. If OCR reports an error, Stackshot discards the capture without saving or copying it. When it detects credentials, it saves only the covered capture. For other detected personal data, Stackshot can keep an unredacted copy in its local data folder so that covers can be edited.

---

## 5. Localized Hotkeys & Dynamic Mnemonics in the Editor

### What it is
Editor shortcuts adapt to your active language ([`src/Editor/Editor.cs`](../src/Editor/Editor.cs)):

| Tool | English Key | Spanish Key | German Key | Description |
|---|:---:|:---:|:---:|---|
| **Arrow** | <kbd>A</kbd> | <kbd>F</kbd> (*Flecha*) | <kbd>A</kbd> / <kbd>F</kbd> (*Pfeil*) | Draw directional indicator arrows |
| **Line** | <kbd>L</kbd> | <kbd>L</kbd> (*Línea*) | <kbd>L</kbd> (*Linie*) | Draw straight lines |
| **Rectangle** | <kbd>R</kbd> | <kbd>R</kbd> (*Recuadro*) | <kbd>R</kbd> (*Rechteck*) | Draw bounding boxes |
| **Ellipse** | <kbd>E</kbd> | <kbd>E</kbd> (*Elipse*) | <kbd>E</kbd> (*Ellipse*) | Draw circles and ovals |
| **Text** | <kbd>T</kbd> | <kbd>T</kbd> (*Texto*) | <kbd>T</kbd> (*Text*) | Add text callouts |
| **Numbers / Counter** | <kbd>N</kbd> | <kbd>N</kbd> (*Números*) | <kbd>N</kbd> (*Zahlen*) | Numbered sequence badges (1, 2, 3...) |
| **Highlight** | <kbd>H</kbd> | <kbd>H</kbd> (*Resaltar*) | <kbd>H</kbd> (*Hervorheben*) | Fluorescent text highlighter |
| **Spotlight** | <kbd>S</kbd> | <kbd>O</kbd> (*Foco*) | <kbd>S</kbd> (*Scheinwerfer*) | Dims the rest of the canvas |
| **Pixelate** | <kbd>P</kbd> | <kbd>P</kbd> (*Pixelar*) | <kbd>P</kbd> (*Verpixeln*) | Mosaic blurring |
| **Redact** | <kbd>X</kbd> | <kbd>X</kbd> (*Tapar*) | <kbd>X</kbd> (*Schwärzen*) | Solid black privacy cover |
| **Crop** | <kbd>C</kbd> | <kbd>C</kbd> (*Recortar*) | <kbd>C</kbd> (*Zuschneiden*) | Resize and trim canvas |

> [!NOTE]
> All hotkeys (<kbd>A</kbd>/<kbd>F</kbd> for Arrow, <kbd>S</kbd>/<kbd>O</kbd> for Spotlight) work seamlessly across all language modes.

![Localized Editor Tool Hotkeys](screens/editor-hotkeys.png)

---

## 6. Dynamic UI-Aware OCR Engine

### What it is
Stackshot integrates directly with Windows 10 and 11's built-in OCR subsystem ([`src/Capture/Ocr.cs`](../src/Capture/Ocr.cs)). The recognition engine uses your current UI setting:

- In **German mode**, it prioritizes German OCR recognition (`de-DE`, `de-AT`, `de-CH`), accurately detecting umlauts (`ä`, `ö`, `ü`) and Eszett (`ß`).
- In **English mode**, it prioritizes English OCR recognition (`en-US`, `en-GB`), improving accuracy for technical terms, camelCase code identifiers, and English prose.
- In **Spanish mode**, it prioritizes Spanish recognizers (`es-ES`, `es-MX`), accurately detecting characters like `ñ`, accented vowels (`á`, `é`, `í`, `ó`, `ú`), and inverted punctuation (`¿`, `¡`).
- When a language pack is not installed on Windows, it gracefully falls back to the user's installed profile languages.
