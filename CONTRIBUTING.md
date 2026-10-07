# Stackshot: guía para trabajar en el código

Capturas de pantalla para Windows al estilo CleanShot X: captura propia (también con desplazamiento), pila de miniaturas
flotantes, editor rápido con fondo de presentación, vídeo/GIF con FFmpeg, ventana principal con mascota e instalación por
usuario. Un único `.exe` (~520 KB) en C# 5 + WinForms sobre .NET Framework 4.8.
Solo Windows (10/11). Interfaz en castellano; comentarios del código en inglés.

## Compilar y probar

- `.\build.ps1` → `bin\Stackshot.exe` (usa el `csc.exe` de .NET Framework que trae Windows; no hace falta Visual Studio).
  Compila con `/warnaserror+`: cualquier aviso (variable sin usar, miembro que oculta otro de Form/Control…) rompe el build.
- `.\build.ps1 -Run` lo abre sin instalar (`--portable`); `.\build.ps1 -Install` instala/actualiza en el usuario.
- `.\tools\build-msi.ps1` → `bin\Stackshot.msi` (después de `build.ps1`), con `tools\Stackshot.wxs` y WiX Toolset 3.14.1,
  que descarga una vez a `bin\wix` y comprueba con una SHA-256 fija. Instalación por usuario, sin administrador.
  Si la instaló el MSI, `Installer.ManagedByMsi` (marca en `HKCU\Software\Stackshot`) hace que la app no cree sus accesos
  ni su entrada en Aplicaciones, no se actualice copiándose a sí misma y desinstale con `msiexec /x`.
  El `UpgradeCode` del `.wxs` no se cambia nunca. Para cerrarse al actualizar, la app atiende WM_CLOSE y WM_ENDSESSION
  en `CloseListener`.
- Probar el MSI en este equipo: `msiexec /i bin\Stackshot.msi /qn /l*v log.txt`, y para desinstalar `/x`. La
  desinstalación borra `%LOCALAPPDATA%\Stackshot` (ajustes, FFmpeg, temporales): haz copia antes.
- `tools\make-logo.ps1 [-Preview hoja.png]` dibuja logo e icono con `tools\Logo.cs` (GDI+; los navegadores en modo
  headless no funcionan en todos los equipos).
- `tools\make-screenshots.ps1` regenera `docs\*.png` con `tools\Studio.cs`: monta un escritorio sintético en la pantalla
  donde no está el ratón y fotografía cada parte. Las imágenes del README nunca deben mostrar contenido real.
- Parámetros del exe: `--install [--startup] [--folder <ruta>] [--no-start]`, `--uninstall [--quiet]`, `--restart`,
  `--portable`, `--background` (no abre la ventana; lo usa el acceso de Inicio de Windows), `--test` (no se oculta de las
  capturas, no toca portapapeles, atajos ni temporales), `--home` (con `--test`, abre la ventana), `--edit <imagen|vídeo>`.
- Datos: `%LOCALAPPDATA%\Stackshot` (`settings.ini`, `temp\`, `ffmpeg\`, `stackshot.log`).
  Instalación: `%LOCALAPPDATA%\Programs\Stackshot`. Mutex `Local\Stackshot`, eventos `Local\Stackshot.Quit` (cerrarse) y
  `Local\Stackshot.Show` (abrir otra vez el .exe con uno en marcha: enseña su ventana).
- Probar la ventana principal sin tocar la pantalla de trabajo: un programa aparte que compile `src\` con su propio
  `Main`, en modo `--test`, con un fondo neutro `TopMost` en la pantalla sin ratón y la ventana también `TopMost` (si no,
  la tapan las ventanas del usuario y salen en las fotos). Cambiar `ShotStack.LogPath` a una carpeta propia.

## Reglas del código

- **Solo C# 5**: nada de `$""`, `?.`, `nameof`, `=>` en miembros, `out var`, propiedades autoinicializadas.
- **Literales en ASCII**: los acentos de las cadenas van como escapes Unicode. Algunas herramientas de edición los
  convierten en caracteres reales; después de editar, `perl tools/escape-literals.pl <fichero>` los vuelve a escapar.
- El proceso es DPI por monitor (manifiesto PerMonitorV2): las medidas van en píxeles reales y se escalan con
  `ShotStack.ScaleFor(screen)` / `P()`.
- Las ventanas flotantes (`FloatWindow`) no roban el foco, son por capas (opacidad animada), siempre encima y quedan fuera
  de las capturas (`WDA_EXCLUDEFROMCAPTURE`). Todo lo que se mueve pasa por `Anim` (un temporizador que solo corre mientras
  hay animaciones; en reposo, 0 % de CPU).
- La ventana principal (`HomeWindow`) usa la paleta de macOS (`Mac`) e iconos de línea propios (`Icons`); el resto (pila,
  editor) sigue con `Theme` (Tokyo Night). Se dibuja en dos lienzos `Dib` (barra lateral y contenido) que solo se rehacen
  al cambiar algo; la mascota y lo animado van encima en cada fotograma. Sin ventana visible, no hay temporizador.
- Nada pesado en el hilo de la interfaz: el PNG de cada captura se escribe en otro hilo (`BeginWrite`; quien necesite el
  fichero llama a `ShotStack.WaitWritten`), el del portapapeles también (`TrackedData`) y el fondo de escritorio de
  `Backdrop` se precarga al arrancar.
- El selector de región (`RegionPicker`) compone cada trozo que cambia con `BitBlt` desde dos `Dib` preparados una vez
  (claro y oscurecido, con las ayudas ya dibujadas). Repintar la pantalla entera en cada movimiento era lo que lo hacía lento.
- FFmpeg se descarga de una versión fija (`FfmpegSetup`: `Version`, `Url`, `Sha256` en `Recorder.cs`); para subir de versión
  hay que cambiar las tres a la vez (la SHA-256 se puede contrastar con el campo `digest` del API de GitHub).
- `--test` y las herramientas de pruebas ponen `Settings.ReadOnly`: nunca escriben los ajustes de verdad.
- Comentarios: en inglés y los mínimos; solo el porqué de lo que no es evidente (nada de repetir lo que dice el código).

## Mapa

| Fichero | Qué hace |
|---|---|
| `src/Program.cs` | Arranque: bienvenida, instalación, actualización, parámetros, versión (`AssemblyVersion`) |
| `src/Setup.cs` | `Installer` (copia, accesos directos, entrada de desinstalación, Impr Pant, modo MSI) y `SetupWindow` (bienvenida) |
| `src/CloseListener.cs` | Ventana invisible que cierra Stackshot ordenadamente cuando Windows lo pide (MSI, cierre de sesión) |
| `tools/Stackshot.wxs`, `tools/build-msi.ps1` | Paquete MSI para despliegues en empresas |
| `src/Settings.cs` | `settings.ini` |
| `src/ShotStack.cs` | La pila: atajos, bandeja, capturar/guardar, miniaturas, desplazamiento, limpieza de temporales |
| `src/Look.cs` | Paleta al estilo macOS (`Mac`) e iconos de línea dibujados a mano (`Icons`) |
| `src/Home/HomeWindow.cs`, `HomePages.cs` | Ventana principal: marco propio con los botones de Windows 11 (minimizar y cerrar a la bandeja), secciones (Inicio, Atajos, General, Fondo y editor, Grabación, Mascota, Acerca de) y sus controles |
| `src/Home/Mascot.cs` | La mascota: estados de ánimo, física, ojos y boca, partículas; `RenderStill` para las vistas previas |
| `src/Home/MascotParts.cs`, `MascotDraw.cs` | Catálogo (`MascotLook`: personaje, color, ojos, gorro, ropa, complementos; niveles de amistad y desbloqueos) y el dibujo de cada pieza. Los índices se guardan en `settings.ini`: lo nuevo, siempre al final |
| `src/Home/MascotTalk.cs` | Lo que dice la mascota según su personalidad |
| `src/Home/PetWindow.cs` | La mascota en el escritorio: ventana por capas con alfa por píxel (`UpdateLayeredWindow`), paseos, siesta, arrastrar y soltar; se esconde con apps a pantalla completa |
| `src/Home/LogoArt.cs`, `Intro.cs` | El logo como dibujo paramétrico (lo usan la app y `tools\make-logo.ps1`) y la animación de inicio |
| `src/Updater.cs` | Actualizaciones desde GitHub Releases: comprobación diaria, descarga, verificación (SHA-256 + firma RSA-PSS) e instalación |
| `src/Home/TrayMenu.cs` | Menú de la bandeja con su propio renderizador |
| `src/Capture/Dib.cs` | Lienzo compartido GDI/GDI+ (lo usan el selector y la ventana principal) |
| `src/Capture/ScrollCapture.cs` | Captura con desplazamiento: sesión, cosido por hashes de filas, barrita y marco |
| `src/Editor/Backdrop.cs`, `BgPanel.cs` | Fondo de presentación (fondos, composición, vídeo) y su franja en el editor |
| `src/Card.cs`, `src/Chip.cs` | Miniatura y pastillas "N anteriores / N más recientes" |
| `src/FloatWindow.cs`, `src/Animation.cs` | Base de ventanas flotantes, muelles e interpolaciones |
| `src/Capture/*` | Atajos globales, copia de pantalla y ventanas, selección de región, fijar en pantalla, sonido, grabación (FFmpeg) |
| `src/Editor/*` | Editor: marcas (`Shapes`), lienzo con selección/historial (`Canvas`), barra (`Toolbar`), ventana (`Editor`) |
| `src/Ui.cs` | Controles propios: `DarkForm`, `Pill`, `Toggle`, `HotkeyBox`, `Progress` |
| `src/TrackedData.cs`, `src/FileDrag.cs` | Portapapeles que avisa al pegar; arrastrar con miniatura junto al cursor |

## Publicar una versión

Subir `AssemblyVersion`/`AssemblyFileVersion` en `src/Program.cs`, apuntar los cambios en `CHANGELOG.md`, crear la etiqueta
`vX.Y.Z` y subirla: el workflow `.github/workflows/build.yml` compila y adjunta `Stackshot.exe`, `.msi`, sus `.sha256` y la
firma `Stackshot.exe.sig` a la versión. Nunca se rehace una versión ya publicada: cada cambio es una versión nueva.

Firma de actualizaciones: la app solo instala un `Stackshot.exe` firmado con la clave privada RSA cuyo par público está en
`src/Updater.cs`. La privada vive fuera del repositorio (en el equipo del mantenedor y como secreto `UPDATE_SIGNING_KEY` del
repositorio, en formato XML de .NET). Sin el secreto, la versión se publica igual pero la app no la instala (ofrece el enlace).
Si se pierde la clave, hay que generar otra, cambiar la pública en `Updater.cs` y publicar esa versión a mano una vez.
