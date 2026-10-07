# Stackshot: guía para trabajar en el código

Capturas de pantalla para Windows al estilo CleanShot X: captura propia, pila de miniaturas flotantes, editor rápido,
vídeo/GIF con FFmpeg e instalación por usuario. Un único `.exe` (~390 KB) en C# 5 + WinForms sobre .NET Framework 4.8.
Solo Windows (10/11). Interfaz y comentarios en castellano.

## Compilar y probar

- `.\build.ps1` → `bin\Stackshot.exe` (usa el `csc.exe` de .NET Framework que trae Windows; no hace falta Visual Studio).
  Compila con `/warnaserror+`: cualquier aviso (variable sin usar, miembro que oculta otro de Form/Control…) rompe el build.
- `.\build.ps1 -Run` lo abre sin instalar (`--portable`); `.\build.ps1 -Install` instala/actualiza en el usuario.
- `tools\make-logo.ps1 [-Preview hoja.png]` dibuja logo e icono con `tools\Logo.cs` (GDI+; los navegadores en modo
  headless no funcionan en todos los equipos).
- `tools\make-screenshots.ps1` regenera `docs\*.png` con `tools\Studio.cs`: monta un escritorio sintético en la pantalla
  donde no está el ratón y fotografía cada parte. Las imágenes del README nunca deben mostrar contenido real.
- Parámetros del exe: `--install [--startup] [--folder <ruta>] [--no-start]`, `--uninstall [--quiet]`, `--restart`,
  `--portable`, `--test` (no se oculta de las capturas, no toca portapapeles ni atajos), `--edit <imagen>`.
- Datos: `%LOCALAPPDATA%\Stackshot` (`settings.ini`, `temp\`, `ffmpeg\`, `stackshot.log`).
  Instalación: `%LOCALAPPDATA%\Programs\Stackshot`. Mutex `Local\Stackshot`, evento de cierre `Local\Stackshot.Quit`.

## Reglas del código

- **Solo C# 5**: nada de `$""`, `?.`, `nameof`, `=>` en miembros, `out var`, propiedades autoinicializadas.
- **Literales en ASCII**: los acentos de las cadenas van como escapes Unicode. Algunas herramientas de edición los
  convierten en caracteres reales; después de editar, `perl tools/escape-literals.pl <fichero>` los vuelve a escapar.
  En los comentarios sí se escriben los acentos.
- El proceso es DPI por monitor (manifiesto PerMonitorV2): las medidas van en píxeles reales y se escalan con
  `ShotStack.ScaleFor(screen)` / `P()`.
- Las ventanas flotantes (`FloatWindow`) no roban el foco, son por capas (opacidad animada), siempre encima y quedan fuera
  de las capturas (`WDA_EXCLUDEFROMCAPTURE`). Todo lo que se mueve pasa por `Anim` (un temporizador que solo corre mientras
  hay animaciones; en reposo, 0 % de CPU).
- Estilo: el de los ficheros existentes (comentarios breves en castellano que explican el porqué).

## Mapa

| Fichero | Qué hace |
|---|---|
| `src/Program.cs` | Arranque: bienvenida, instalación, actualización, parámetros, versión (`AssemblyVersion`) |
| `src/Setup.cs` | `Installer` (copia, accesos directos, entrada de desinstalación, Impr Pant) y `SetupWindow` (bienvenida y ajustes) |
| `src/Settings.cs` | `settings.ini` |
| `src/ShotStack.cs` | La pila: atajos, bandeja, capturar/guardar, miniaturas, desplazamiento, limpieza de temporales |
| `src/Card.cs`, `src/Chip.cs` | Miniatura y pastillas "N anteriores / N más recientes" |
| `src/FloatWindow.cs`, `src/Animation.cs` | Base de ventanas flotantes, muelles e interpolaciones |
| `src/Capture/*` | Atajos globales, copia de pantalla y ventanas, selección de región, fijar en pantalla, sonido, grabación (FFmpeg) |
| `src/Editor/*` | Editor: marcas (`Shapes`), lienzo con selección/historial (`Canvas`), barra (`Toolbar`), ventana (`Editor`) |
| `src/Ui.cs` | Controles propios: `DarkForm`, `Pill`, `Toggle`, `HotkeyBox`, `Progress` |
| `src/TrackedData.cs`, `src/FileDrag.cs` | Portapapeles que avisa al pegar; arrastrar con miniatura junto al cursor |

## Publicar una versión

Subir `AssemblyVersion`/`AssemblyFileVersion` en `src/Program.cs`, apuntar los cambios en `CHANGELOG.md`, crear la etiqueta
`vX.Y.Z` y subirla: el workflow `.github/workflows/build.yml` compila y adjunta `Stackshot.exe` y su `.sha256` a la versión.
