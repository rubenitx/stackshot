<div align="center">

<img src="docs/banner.svg" alt="Stackshot. Captura, marca y comparte en segundos." width="100%">

**Captura, marca y comparte en segundos.**<br>
Herramienta de capturas de pantalla para Windows con miniaturas flotantes, editor rápido, captura con desplazamiento, vídeo y GIF.

[![Última versión](https://img.shields.io/github/v/release/rubenitx/stackshot?label=versi%C3%B3n&color=4F7BFF)](https://github.com/rubenitx/stackshot/releases/latest)
[![Descargas](https://img.shields.io/github/downloads/rubenitx/stackshot/total?label=descargas&color=8B5CF6)](https://github.com/rubenitx/stackshot/releases)
[![Compilación](https://github.com/rubenitx/stackshot/actions/workflows/build.yml/badge.svg)](https://github.com/rubenitx/stackshot/actions/workflows/build.yml)
[![Licencia MIT](https://img.shields.io/badge/licencia-MIT-14B8E6)](LICENSE)

[Descargar](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe) · [Novedades](CHANGELOG.md) · [Seguridad](SECURITY.md) · [English](README.md)

</div>

---

Pulsa <kbd>Impr Pant</kbd>, selecciona un área y la captura se queda en una esquina de la pantalla, lista para arrastrarla a un chat, pegarla con <kbd>Ctrl</kbd>+<kbd>V</kbd> o marcarla. Las capturas que no guardas se borran solas al cabo de una hora.

Stackshot lleva a Windows la forma de trabajar de CleanShot X. Es un único ejecutable de unos 0,7 MB, se instala por usuario sin permisos de administrador y no depende de ningún otro programa.

<div align="center">
<img src="docs/promo.gif" alt="Stackshot en 17 segundos: captura, la ventana principal, la mascota disfrazada y las funciones principales" width="720">
</div>

## Funciones

- **Pila flotante.** Cada captura aparece como miniatura abajo a la izquierda. Se puede arrastrar a cualquier aplicación, pegar, descartar deslizándola o recorrer con la rueda del ratón (hasta 20). Sigue al ratón entre pantallas y no aparece al compartir pantalla.
- **Selección precisa.** La pantalla se congela al instante. Se arrastra un área con lupa y medidas, o se hace clic en una ventana para capturarla entera, con las esquinas redondeadas de Windows 11.
- **Captura con desplazamiento.** Se selecciona un área y se desplaza el contenido, a mano o de forma automática; los fotogramas se unen en una sola imagen sin repetir cabeceras ni pies fijos.
- **Editor.** Flechas que se pueden curvar, recuadros, elipses, números, texto, resaltado, pixelado, bloque para tapar datos y recorte no destructivo. Cualquier marca se puede mover, cambiar de tamaño o borrar después.
- **Fondo de presentación.** La captura se coloca sobre un degradado, sobre el fondo de escritorio desenfocado o sobre una imagen tuya, con margen, esquinas redondeadas, sombra y proporción fija. Las mismas opciones sirven para las grabaciones.
- **Vídeo y GIF.** Graba un área de la pantalla en MP4 o GIF, con el cursor, en calidad estándar, alta o máxima y, si quieres, con tu cámara en una burbuja redonda o cuadrada. Después recorta el principio y el final, cambia la velocidad (1,5× o 2×), el tamaño o el formato y exporta.
- **Fijar en pantalla.** Mantiene una captura por encima de todas las ventanas, con zoom y transparencia.
- **Ventana principal.** Todas las acciones, atajos y ajustes en un sitio, con perfiles rápidos (Rendimiento, Equilibrado, Completo). Al cerrar la ventana, Stackshot sigue en la bandeja; doble clic en su icono la vuelve a abrir.
- **Una mascota a tu gusto.** Doce especies, 23 colores, estilos de ojos, decenas de gorros, ropa y complementos, 38 disfraces de personajes (más abajo), tres personalidades y niveles de amistad que desbloquean extras. Si quieres, vive en el escritorio: pasea sobre la barra de tareas, salta a la ventana que tienes delante y viaja con ella si la mueves, se echa la siesta cuando no estás, se despide y se va andando cuando la mandas a casa, y nunca sale en las capturas.
- **Actualizaciones desde la app.** Con un clic, y verificadas con su suma SHA-256 y una firma antes de ejecutar nada.

<table>
  <tr>
    <td width="50%"><img src="docs/stack-hover.png" alt="Miniaturas flotantes con su barra de botones"></td>
    <td width="50%"><img src="docs/region.png" alt="Selección de un área con lupa y medidas"></td>
  </tr>
  <tr>
    <td><img src="docs/editor.png" alt="Editor con flecha curva, números y texto"></td>
    <td><img src="docs/backdrop.png" alt="Editor con el fondo de presentación"></td>
  </tr>
  <tr>
    <td><img src="docs/app.png" alt="Ventana principal"></td>
    <td><img src="docs/settings.png" alt="Ajustes generales"></td>
  </tr>
</table>

## Míralo en acción

Un recorrido por la ventana principal: ajustes, fondos, opciones de grabación y la página de la mascota, donde un clic en un personaje la disfraza.

<div align="center">
<img src="docs/demo.gif" alt="Recorrido por la ventana principal que termina con la mascota disfrazada" width="760">
</div>

## Personajes

La sección «Personajes» de la página Mascota la disfraza con homenajes fan a personajes de anime, series, cómics y videojuegos, y algunos originales: 38 en total. Un clic y se convierte en Goku, Spider-Man o Kratos; al pasar el ratón por un disfraz se ve de dónde sale.

<div align="center">
<img src="docs/mascot.gif" alt="La mascota probándose estilos y disfraces de personajes" width="640">
<br><br>
<img src="docs/characters.png" alt="Los 38 disfraces de personajes con su nombre y procedencia" width="760">
</div>

## Instalación

1. Descarga **[Stackshot.exe](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe)** y ábrelo.
2. Elige si debe arrancar con Windows y dónde guardar las capturas.
3. Pulsa **Instalar y empezar** y después <kbd>Impr Pant</kbd>.

Se instala en `%LOCALAPPDATA%\Programs\Stackshot`, añade un acceso al menú Inicio y aparece en **Configuración > Aplicaciones** para desinstalarlo. Funciona en Windows 10 y 11 con el .NET Framework 4.8 que ya trae Windows. Para actualizar, basta con abrir un `Stackshot.exe` más reciente.

Como el ejecutable todavía no está firmado digitalmente, SmartScreen muestra un aviso la primera vez (**Más información > Ejecutar de todas formas**). En equipos gestionados por una empresa, SmartScreen puede estar configurado para bloquearlo sin opción de continuar; en ese caso, pide a informática que despliegue el paquete MSI que se describe a continuación. Cada versión se puede verificar como se explica en [SECURITY.md](SECURITY.md).

### Despliegue en empresas

Cada versión incluye también **[Stackshot.msi](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.msi)**, un paquete de Windows Installer para Intune, Configuration Manager o cualquier otra herramienta de despliegue. Se instala por usuario, en el contexto del usuario y sin permisos de administrador.

| | |
|---|---|
| Instalar | `msiexec /i Stackshot.msi /qn` |
| Desinstalar | `msiexec /x Stackshot.msi /qn` (o desde **Configuración > Aplicaciones**) |
| Opciones | `STARTWITHWINDOWS=0` para que no arranque con Windows, `LAUNCHAPP=0` para que no se abra al terminar |
| Detección | Código de producto del paquete, o `%LOCALAPPDATA%\Programs\Stackshot\Stackshot.exe` con versión ≥ la desplegada |
| Intune | Aplicación Win32, comportamiento de instalación **Usuario**, sin reinicio |

Cuando el MSI gestiona la instalación, las actualizaciones y la desinstalación pasan por Windows Installer: un `Stackshot.msi` más reciente sustituye a la versión anterior y conserva los ajustes del usuario. Stackshot se cierra y se vuelve a abrir solo, sin reiniciar. Al desinstalar se quitan la aplicación y sus datos, pero no las capturas que el usuario haya guardado. Si ya estaba instalado con `Stackshot.exe`, el MSI adopta esa instalación sin dejar entradas duplicadas.

<details>
<summary>Instalación silenciosa con el ejecutable</summary>

```powershell
Stackshot.exe --install --startup --folder "D:\Capturas"   # --no-start para no abrirlo al terminar
Stackshot.exe --uninstall --quiet
```
</details>

## Atajos

| Atajo | Acción |
|---|---|
| <kbd>Impr Pant</kbd> | Capturar un área, una ventana o la pantalla entera |
| <kbd>Ctrl</kbd>+<kbd>Impr Pant</kbd> | Capturar la pantalla en la que está el ratón |
| <kbd>Alt</kbd>+<kbd>Impr Pant</kbd> | Capturar la ventana activa |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Impr Pant</kbd> | Captura con desplazamiento (otra vez para terminar) |
| <kbd>Mayús</kbd>+<kbd>Impr Pant</kbd> | Grabar vídeo (otra vez para parar) |
| <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Impr Pant</kbd> | Grabar GIF |

Todos se pueden cambiar en la sección **Atajos** de la ventana principal. Si Windows 11 reserva <kbd>Impr Pant</kbd> para Recortes, Stackshot se ofrece a liberarla para el usuario actual.

<details>
<summary>Atajos del editor</summary>

| Tecla | Acción |
|---|---|
| <kbd>F</kbd> <kbd>R</kbd> <kbd>E</kbd> <kbd>T</kbd> <kbd>N</kbd> <kbd>H</kbd> <kbd>P</kbd> <kbd>X</kbd> <kbd>C</kbd> | Flecha, recuadro, elipse, texto, números, resaltar, pixelar, tapar, recortar |
| <kbd>B</kbd> | Fondo de presentación |
| <kbd>1</kbd>–<kbd>5</kbd>, <kbd>−</kbd> <kbd>+</kbd> | Color, grosor |
| <kbd>Ctrl</kbd>+<kbd>Z</kbd>, <kbd>Ctrl</kbd>+<kbd>Y</kbd> | Deshacer, rehacer |
| <kbd>Ctrl</kbd>+<kbd>C</kbd>, <kbd>Ctrl</kbd>+<kbd>S</kbd> | Copiar, guardar |
| <kbd>Enter</kbd> | Aplicar, copiar y cerrar |
| <kbd>Supr</kbd>, flechas | Borrar o mover la marca seleccionada |
</details>

## Privacidad y seguridad

- Sin cuentas, sin servicios en la nube y sin telemetría. Las capturas no salen del equipo.
- Solo hay dos conexiones: la descarga, una sola vez, de FFmpeg 9.0.2 al grabar por primera vez (comprobada con una suma SHA-256 guardada en el código) y una consulta diaria a GitHub para saber si hay versión nueva, que no envía nada tuyo y se puede desactivar en General.
- Las actualizaciones instaladas desde la app tienen que coincidir con la SHA-256 que publica GitHub y llevar una firma válida del flujo de publicación; si no, se descartan.
- Las capturas que copia Stackshot nunca se sincronizan con otros equipos a través del portapapeles en la nube.
- Las capturas temporales se guardan en `%LOCALAPPDATA%\Stackshot\temp` y se borran al cabo de una hora, nunca mientras sigan en pantalla.
- Cada versión la compila GitHub Actions a partir de este repositorio y se publica con su suma SHA-256 y una atestación de procedencia firmada.

Los detalles y cómo informar de una vulnerabilidad están en [SECURITY.md](SECURITY.md).

## Rendimiento

| | |
|---|---|
| En reposo | Unos 40 MB de RAM y sin uso de CPU |
| Selección de área | Menos de 1 ms por movimiento del ratón; solo se redibuja lo que cambia |
| Tras capturar | La miniatura aparece al momento; el PNG se escribe en segundo plano |
| Ejecutable | Unos 0,7 MB, sin nada más que instalar |

## Compilar desde el código

No hace falta Visual Studio: basta el compilador de C# que incluye Windows.

```powershell
git clone https://github.com/rubenitx/stackshot
cd stackshot
.\build.ps1          # genera bin\Stackshot.exe
.\build.ps1 -Run     # compila y lo abre sin instalar
```

| Carpeta | Contenido |
|---|---|
| `src/` | La aplicación (C# 5, WinForms): captura, miniaturas, editor, grabación, ventana principal e instalación |
| `assets/` | Logo e icono, dibujados en código por `tools\make-logo.ps1` |
| `docs/` | Imágenes del README: capturas regeneradas sobre un escritorio sintético por `tools\make-screenshots.ps1`, y la animación de la mascota y la lámina de personajes por `tools\make-reel.ps1` |

## Contribuir

Los fallos y las sugerencias son bienvenidos en [Issues](https://github.com/rubenitx/stackshot/issues). Los comentarios del código están en inglés; cómo compilar, las reglas del código y el mapa de ficheros están en [CONTRIBUTING.md](CONTRIBUTING.md). Los problemas de seguridad se comunican de forma privada, como se indica en [SECURITY.md](SECURITY.md).

## Licencia

[MIT](LICENSE) © 2026 Rubén Martínez.

FFmpeg no se incluye. Se descarga aparte, con su propia licencia (GPL), solo al grabar vídeo o GIF.

Los disfraces de la sección «Personajes» son homenajes fan sin ánimo de lucro, dibujados desde cero con el estilo de la mascota. Los personajes y sus nombres (Finn, Jake, BMO, Pakkun, Naruto, Sasuke, Kakashi, Edward Elric, Eren Jaeger, Levi, Luffy, Zoro, Goku, Vegeta, Tanjiro, Nezuko, Gojo, Gon, Killua, Hisoka, Saitama, Shin-chan, Pikachu, Doraemon, Totoro, Kratos, Doom Slayer, Spider-Man, Iron Man, Batman, Superman, Flash, Claude, Codex) pertenecen a sus respectivos dueños, y Stackshot no tiene relación con ninguno de ellos ni cuenta con su respaldo. Si tienes los derechos de alguno y quieres que se retire, abre una incidencia y se quitará.
