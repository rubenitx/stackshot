<div align="center">

<img src="docs/banner-es.svg" alt="Stackshot, herramienta gratuita de capturas de pantalla y grabador de pantalla para Windows: captura, marca y comparte en segundos" width="100%">

<h1>Captura, marca y comparte en segundos</h1>

Pulsa <kbd>Impr Pant</kbd>, selecciona y listo. Cada captura queda en una miniatura flotante para copiarla, anotarla o arrastrarla donde quieras.

[![Descargar para Windows](https://img.shields.io/badge/Descargar_para_Windows-2%2C2_MB-4F7BFF?style=for-the-badge)](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe) [![Ver el vídeo](https://img.shields.io/badge/Ver_el_v%C3%ADdeo-1%3A38-1C1C20?style=for-the-badge)](https://stackshot.rubenitx.me/#video)

Gratis y de código abierto · 2,2 MB · Sin permisos de administrador<br>
Para Windows 10 y 11 · Versión 2.1.0 · También como [paquete MSI](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.msi)

[![Última versión](https://img.shields.io/github/v/release/rubenitx/stackshot?label=versi%C3%B3n&color=4F7BFF)](https://github.com/rubenitx/stackshot/releases/latest) [![Descargas](https://img.shields.io/github/downloads/rubenitx/stackshot/total?label=descargas&color=8B5CF6)](https://github.com/rubenitx/stackshot/releases) [![Compilación](https://github.com/rubenitx/stackshot/actions/workflows/build.yml/badge.svg)](https://github.com/rubenitx/stackshot/actions/workflows/build.yml) [![Licencia MIT](https://img.shields.io/badge/licencia-MIT-14B8E6)](LICENSE)

[Web](https://stackshot.rubenitx.me) · [English](README.md) · [Registro de cambios](CHANGELOG.md) · [Seguridad](SECURITY.md) · [Directivas de empresa](docs/it-policies.md)

<a href="https://stackshot.rubenitx.me/#video"><img src="docs/video/poster-es.png" alt="Stackshot, el vídeo. Haz clic para verlo." width="100%"></a>

</div>

**Contenido:** [Tres pasos](#tres-pasos-ningún-menú) · [Novedades](#novedades-de-la-21) · [Funciones](#funciones-de-un-vistazo) · [Modos](#siete-formas-de-capturar) · [Capturar](#selecciona-captura-listo) · [Marcar](#márcala-en-segundos) · [Datos sensibles](#tápalo-antes-de-compartir) · [Desplazamiento](#captura-la-página-entera) · [Grabar](#graba-la-pantalla-con-sonido) · [Historial](#encuéntrala-por-lo-que-decía) · [XML y Markdown](#xml-y-markdown-a-otro-nivel) · [Ventana principal](#una-ventana-a-tu-manera) · [Pixel](#conoce-a-pixel-39-disfraces) · [Más](#y-mucho-más) · [Rendimiento](#ligera-rápida-privada) · [Instalación](#instalación) · [Línea de órdenes](#línea-de-órdenes-y-directivas-de-empresa) · [Atajos](#atajos-de-teclado) · [Privacidad](#privacidad-y-seguridad) · [Preguntas frecuentes](#preguntas-frecuentes) · [Compilar](#compilar-desde-el-código) · [Licencia](#licencia)

> [!NOTE]
> La interfaz de Stackshot está en español y en inglés: sigue el idioma de Windows y se cambia en **Ajustes › General › Apariencia › Idioma**. Las capturas de esta página muestran la interfaz en español.

## Tres pasos, ningún menú

<table>
<tr>
<th align="center" width="33%">1 · <kbd>Impr Pant</kbd></th>
<th align="center" width="33%">2 · Editor</th>
<th align="center" width="33%">3 · <kbd>Ctrl</kbd> + <kbd>V</kbd></th>
</tr>
<tr>
<td align="center" valign="top"><b>Captura</b><br>Un área, una ventana, la pantalla entera o una página con desplazamiento. Siete modos, cada uno con su atajo.</td>
<td align="center" valign="top"><b>Márcala</b><br>Flechas, formas, pasos numerados, texto, resaltado y tapado. Y un fondo con margen y sombra si la vas a presentar.</td>
<td align="center" valign="top"><b>Compártela</b><br>Ya está copiada. Pégala donde quieras o arrastra la miniatura a un chat, un correo o un documento.</td>
</tr>
</table>

## Novedades de la 2.1

La versión 2.1 ya está disponible. Esto es lo que añade; todos los cambios están en el [registro de cambios](CHANGELOG.md).

- **Encuentra una captura por lo que decía.** El [Historial](#encuéntrala-por-lo-que-decía) muestra todas las capturas, vídeos y GIF por días, y su búsqueda también lee el texto de dentro de las imágenes, en tu equipo.
- **Tapa los datos sensibles.** Con un clic en el editor, o automáticamente en cada captura, [tapa DNI, IBAN, tarjetas, correos y teléfonos](#tápalo-antes-de-compartir).
- **Más control sobre las capturas.** El [puntero del ratón](#selecciona-captura-listo) puede salir en la captura, los nombres de archivo se arman con una plantilla, las capturas guardadas pueden ir a carpetas por mes y **Repetir área** vuelve a capturar la última área.
- **Mejoras al grabar.** [Pausar y reanudar](#graba-como-un-profesional), mostrar los clics y los atajos de teclado, probar el micrófono con un medidor en directo, elegir opciones y una cuenta atrás antes de grabar y convertir en GIF cualquier tramo marcado de un vídeo.
- **Un máster «Cine» sin pérdidas y verificado, y un reproductor integrado.** Cine decodifica el archivo al terminar y lo compara fotograma a fotograma; los vídeos se abren en el [reproductor de Stackshot](#recórtalo-acéleralo-compártelo) y las exportaciones a MP4 pueden ajustarse a 16 MB o 25 MB.
- **Copiar texto de la pantalla.** [Selecciona un área y su texto pasa al portapapeles](#y-mucho-más), con el OCR que trae Windows.
- **XML para servicios web.** Los [mensajes SOAP](#xml-y-markdown-a-otro-nivel) tienen una barra de resumen, comparación lado a lado, copia como JSON y adjuntos en base64; los XML que aparecen en una carpeta pueden abrirse solos.
- **Markdown para documentar.** [Imágenes y tablas de hojas de cálculo pegadas, panel de índice y exportación a PDF con texto de verdad](#xml-y-markdown-a-otro-nivel).
- **Encuentra cualquier ajuste.** Un [buscador en la ventana principal](#una-ventana-a-tu-manera) y páginas de ajustes que empiezan con una tira de chips.
- **Accesibilidad.** Los controles dicen su nombre, su función y su estado a los lectores de pantalla, todo funciona con el teclado y se respetan los temas de contraste de Windows.
- **Avisos propios de Stackshot** en lugar de los globos de la bandeja de Windows.
- **Línea de órdenes y directivas de empresa.** [`--capture <modo>`, `--open-home` y `--last-xml`](#línea-de-órdenes-y-directivas-de-empresa), y valores del registro para fijar la carpeta de capturas o desactivar funciones.

<details>
<summary>Versión 2.0</summary>
<br>

- **Rediseño completo.** Un solo sistema de diseño en claro y oscuro, una ventana principal como la Configuración del Sistema de macOS, miniaturas flotantes redondeadas y un panel en la bandeja en lugar del menú contextual.
- **Inicio a tu manera.** Tres vistas de inicio (Minimalista, Widgets y Escena) con tus últimas capturas a un clic.
- **Visores de Markdown y XML.** Abre archivos `.md` y `.xml` directamente desde el Explorador.
- **Mejor grabación.** Elige los altavoces y el micrófono exactos, las cámaras externas funcionan a la primera y la barra espera a que todo esté listo.
- **Más rápida en todo.** El selector de área responde en 1,6 ms, y la ventana principal se abre en un cuarto de segundo y apenas usa CPU en reposo.
- **Actualizaciones que te encuentran.** Verificadas con SHA-256 y firma y, si quieres, instaladas solas en un momento tranquilo.
</details>

## Funciones de un vistazo

**Stackshot es una herramienta gratuita y de código abierto para hacer capturas de pantalla y grabar la pantalla en Windows 10 y 11.** Lleva a Windows la forma de trabajar de CleanShot X. Si buscas una alternativa a ShareX, Snagit, Greenshot o Lightshot, es una opción gratuita y con licencia MIT, con miniaturas flotantes, un editor rápido, sin cuentas y sin telemetría.

| Función | Notas |
|---|---|
| Capturas de un área, una ventana o la pantalla entera | <kbd>Impr Pant</kbd>, <kbd>Alt</kbd>+<kbd>Impr Pant</kbd>, <kbd>Ctrl</kbd>+<kbd>Impr Pant</kbd> |
| Captura con desplazamiento, también como PDF paginado | Con desplazamiento manual o automático |
| Miniaturas flotantes: copiar, guardar, arrastrar a cualquier aplicación, fijar encima de las ventanas | Hasta 20 a la vez |
| Editor de anotaciones: flechas, recuadros, pasos, texto, resaltado, pixelado, tapar, recorte | Cada marca se puede editar después |
| Fondo de presentación (degradado, escritorio o tu imagen) | También en las grabaciones |
| Copiar texto de la pantalla (OCR) | Usa el OCR que trae Windows; nada sale del equipo |
| Tapar datos sensibles (DNI, IBAN, tarjetas, correos, teléfonos) | Botón del editor; el tapado automático viene apagado |
| Historial con búsqueda dentro de las imágenes | El índice de texto se puede apagar o borrar |
| Grabación de vídeo (MP4) y GIF | FFmpeg se descarga la primera vez que grabas, si lo aceptas |
| Sonido del equipo, micrófono y burbuja de cámara | Con el dispositivo que elijas para cada uno |
| Máster «Cine» sin pérdidas, verificado al terminar | Una calidad de la página Grabación |
| Pausa, clics y teclas en el vídeo, medidor del micrófono | Los clics y las teclas vienen apagados |
| Reproductor integrado, recorte, unir vídeos, exportar MP4 y GIF | Reproduce MP4, MOV, MKV, WebM, AVI, WMV, TS y más |
| Visor y editor de Markdown, con exportación a PDF | |
| Visor de XML con herramientas para SOAP y comparación | |
| Buscador de ajustes | <kbd>Ctrl</kbd>+<kbd>F</kbd> en la ventana principal |
| Temas claro y oscuro, lectores de pantalla, alto contraste | |
| Mascota (Pixel) con 39 disfraces de personajes | En el escritorio solo si tú quieres |
| Línea de órdenes, paquete MSI y directivas del registro | Para informática y botones de teclados auxiliares |
| Actualizaciones verificadas con SHA-256 y firma | La instalación automática viene apagada |
| Sin cuentas, sin telemetría, licencia MIT | |

## Siete formas de capturar.

**Siete formas de capturar, cada una con su atajo.**

<div align="center">
<img src="docs/clips/es/modes.webp" alt="Los siete modos de captura uno tras otro, cada uno con su atajo: área, pantalla, ventana, desplazamiento, vídeo, GIF y Markdown" width="100%">
</div>

<details>
<summary>Cómo funciona</summary>
<br>

Un área, la pantalla entera, una ventana, una página larga, un vídeo, un GIF o un documento Markdown: cada uno tiene su atajo y todos parten de <kbd>Impr Pant</kbd>. La lista completa está en [Atajos de teclado](#atajos-de-teclado).
</details>

## Selecciona. Captura. Listo.

**Pulsa Impr Pant, arrastra sobre lo que quieras y te espera en la esquina.**

<div align="center">
<img src="docs/clips/es/capture.webp" alt="Selección de un área con medidas en directo, la captura volando a la esquina como miniatura y arrastrada a un chat" width="100%">
</div>

<details>
<summary>Cómo funciona</summary>
<br>

- **Selección precisa.** La pantalla se congela al instante. Arrastra un área con las medidas en directo, o pasa el ratón por una ventana para resaltarla y haz clic para capturarla entera, con las esquinas redondeadas de Windows 11.
- **Repetir la última área.** **Repetir área** vuelve a capturar la misma área sin elegirla otra vez, desde la bandeja o desde **Atajos** (sin atajo por defecto; puedes asignarle uno). Si ha cambiado una pantalla que tocaba, se abre el selector de siempre.
- **Nombres y carpetas a tu gusto.** En General > Capturas guardadas, arma el nombre de los archivos con el tipo, la fecha, la hora, el programa, el título de la ventana, el año, el mes, el día o un contador que no se repite, con un ejemplo en directo. **Subcarpetas** guarda las capturas en una carpeta por mes (`2026-10`) o por año y mes (`2026\10`). Con los valores por defecto, los nombres son los de siempre.
- **El puntero del ratón, si quieres.** General > Al capturar > **Incluir el puntero** dibuja el puntero, tal como estaba al empezar, en las capturas de área, pantalla y ventana. Viene apagado.
</details>

### Arrástrala donde quieras.

**A un chat, un correo o un documento. O pégala con Ctrl+V.**

<details>
<summary>Cómo funciona</summary>
<br>

- **Pila flotante.** Cada captura aparece como miniatura en la esquina. Al pasar el ratón muestra **Copiar** y **Guardar**; se puede arrastrar a cualquier aplicación, descartar deslizándola o recorrer con la rueda (hasta 20). Sigue al ratón entre pantallas y nunca aparece al compartir pantalla.
- **Directa al portapapeles.** Cada captura se copia nada más hacerla, lista para <kbd>Ctrl</kbd>+<kbd>V</kbd> (los vídeos y GIF se copian como archivo). Nunca se sincroniza por el portapapeles en la nube de Windows.
</details>

## Márcala en segundos.

**Flechas, formas, pasos numerados, texto, resaltado y tapado.**

<div align="center">
<img src="docs/clips/es/editor.webp" alt="Se dibujan un recuadro, una flecha curva, pasos numerados, una etiqueta y un resaltado, y después la captura pasa a un fondo degradado" width="100%">
</div>

<details>
<summary>Cómo funciona</summary>
<br>

- **Editor.** Flechas curvas, líneas, recuadros, elipses, pasos numerados, texto, resaltado, un foco que oscurece todo lo que queda fuera de un área, pixelado, bloque para tapar que elimina de verdad lo que hay debajo y recorte no destructivo. Cualquier marca se puede mover, cambiar de tamaño o borrar después, y el lienzo hace zoom alrededor del puntero.
- **Exportar.** Copia, guarda en PNG o PDF, o arrastra el resultado directamente desde el editor.
</details>

### Y déjala perfecta.

**Fondos, margen, esquinas redondeadas y sombra. También en vídeos.**

<details>
<summary>Cómo funciona</summary>
<br>

- **Fondo de presentación.** Un panel lateral como el de CleanShot X coloca la captura sobre un degradado, tu fondo de escritorio desenfocado, una imagen tuya, la propia captura desenfocada o un color liso, con margen, inserción del color del borde de la captura (con auto-equilibrado si quieres), sombra, esquinas redondeadas, alineación y proporción fija. Las grabaciones tienen los degradados, las imágenes y los colores.
</details>

## Tápalo antes de compartir.

**Un clic encuentra DNI, IBAN, tarjetas, correos y teléfonos, y los tapa.**

<div align="center">
<img src="docs/clips/es/sensitive.webp" alt="El botón Datos sensibles del editor encontrando y tapando un DNI, un IBAN, un correo y un teléfono en una captura, cada uno con una marca que se puede quitar" width="100%">
</div>

<details>
<summary>Cómo funciona</summary>
<br>

Comparte una captura sin compartir lo que no debe salir en ella. El botón **Datos sensibles** del editor (<kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>X</kbd>) busca DNI y NIE, números de la Seguridad Social, IBAN, tarjetas, correos y teléfonos, y tapa cada uno con una marca que puedes quitar. Comprueba los dígitos de control, así que los números de factura, los importes y las fechas se dejan en paz.

La detección puede dejarse algo sin tapar, así que revisa el resultado antes de compartirlo.
</details>

### O que pase solo.

**Actívalo una vez y cada captura se revisa antes. Todo se queda en tu equipo.**

<details>
<summary>Cómo funciona</summary>
<br>

Para hacerlo en cada captura, activa General > Datos sensibles > **Tapar los datos sensibles al capturar**. La miniatura dice «Revisando datos…» un instante (unos 0,4 s a 1080p), y el portapapeles, el archivo y la miniatura solo reciben la imagen tapada. **Viene apagado**, todo se lee en tu equipo y el original sin tapar se guarda en `%LOCALAPPDATA%\Stackshot\originales` solo mientras la captura exista, para poder quitar un tapado desde el editor.

La detección puede dejarse algo sin tapar, así que revisa el resultado antes de compartirlo.
</details>

## Captura la página entera.

**Desplázate tú o deja que Stackshot lo haga. Una sola imagen, sin cortes.**

<div align="center">
<img src="docs/clips/es/scroll.webp" alt="Una página larga desplazándose dentro del marco de captura mientras la imagen unida crece al lado" width="100%">
</div>

<details>
<summary>Cómo funciona</summary>
<br>

Selecciona un área y desplázate, o deja que Stackshot lo haga por ti. Los fotogramas se unen en una sola imagen, sin cortes y sin repetir cabeceras ni pies fijos. Las capturas largas se abren ajustadas al ancho, listas para leer, y se pueden guardar como PDF paginado.
</details>

## Graba la pantalla. Con sonido.

**MP4 o GIF hasta 60 fps, con sonido y con tu cámara si quieres.**

<div align="center">
<img src="docs/clips/es/record.webp" alt="Grabación de un área con la burbuja de la cámara y los medidores de sonido, y después el recorte del clip en una línea de tiempo con tiradores amarillos" width="100%">
</div>

<details>
<summary>Cómo funciona</summary>
<br>

Graba un área en **MP4 o GIF**, con el cursor, hasta a 60 fps. Añade el sonido del equipo y tu micrófono, cada uno desde el dispositivo que elijas y sincronizados con la imagen, y tu cámara en una burbuja redonda o cuadrada. La primera vez que grabas, Stackshot te ofrece descargar FFmpeg (mira [Privacidad y seguridad](#privacidad-y-seguridad)).

Antes de grabar, una ventana pequeña te deja ajustar las opciones (calidad, fotogramas por segundo, sonido, micrófono, cámara) y empieza una cuenta atrás, de 3 segundos por defecto (0, 3, 5 o 10; nunca sale en el vídeo). **Grabación > Antes de grabar** apaga la ventana o cambia la cuenta atrás.

La calidad va de ligera y rápida a **Cine**: un máster de verdad sin pérdidas (H.264 RGB + PCM en MKV) que Stackshot decodifica al terminar y compara fotograma a fotograma con lo capturado. Las áreas de más de 1920 × 1200 a 60 fps preguntan antes de empezar. Si el equipo no da abasto o el disco se está llenando, la grabación se detiene limpiamente y conserva lo que lleva, y todo lo que falle después se mueve a `%LOCALAPPDATA%\Stackshot\recuperado`.
</details>

### Recórtalo. Acéleralo. Compártelo.

**Recorta los extremos y exporta a 1,5× o 2×, en 1080p o 720p.**

<details>
<summary>Cómo funciona</summary>
<br>

Al parar, recorta el principio y el final con los tiradores amarillos, añade más vídeos, cambia la velocidad (1,5× o 2×), el tamaño (1080p o 720p), el formato o la calidad, añade marcas y un fondo, y exporta. La calidad del MP4 puede ser **Máxima** (por defecto), **Alta**, **Ligera**, o ajustarse a un tamaño: **16 MB (WhatsApp)** o **25 MB (correo)**, codificados en dos pasadas (un vídeo demasiado largo para caber con calidad decente se rechaza, diciendo cuánto cabe). Cada MP4 se comprueba antes de dártelo.

**Reproductor de vídeo integrado.** Los vídeos se abren en el reproductor de Stackshot, sin VLC y sin códecs extra: los másters Cine siempre, y los demás por defecto (Grabación > Después de grabar). Reproduce MP4, MOV, MKV, WebM, AVI, WMV, TS y más, con velocidad de 0,25× a 2× conservando el tono, avance fotograma a fotograma, elección de pista de sonido y un panel de información. Los vídeos se abren a su tamaño real cuando la ventana cabe en la pantalla, y uno de más de dos minutos se abre donde lo dejaste. <kbd>C</kbd> guarda como imagen el fotograma que se ve. En General > Archivos se puede ofrecer Stackshot para los archivos `.mkv`, que el reproductor de Windows no sabe decodificar.
</details>

## Graba como un profesional.

**Pausa y reanuda, muestra clics y teclas, y prueba el micrófono antes de empezar.**

<div align="center">
<img src="docs/clips/es/record-plus.webp" alt="Una barra de grabación con pausa, círculos dibujados donde hace clic el ratón y atajos de teclado mostrados abajo del vídeo, junto a un medidor del nivel del micrófono" width="100%">
</div>

<details>
<summary>Cómo funciona</summary>
<br>

- **Pausar y reanudar** desde la barra de grabación, la bandeja o la línea de órdenes, en todas las calidades. Lo que pasa durante la pausa no está en el archivo, y el sonido y la imagen siguen sincronizados.
- **Mostrar clics y teclas (opcional).** Grabación > Clics y teclas dibuja un aro donde haces clic (azul con el izquierdo, naranja con el derecho) y muestra atajos como «Ctrl + C» abajo del vídeo. Lo que escribes nunca se muestra, así que las contraseñas no acaban en el vídeo. Las dos opciones vienen apagadas, y el teclado y el ratón solo se vigilan mientras dura una grabación con alguna de ellas activada.
- **Probar el micrófono.** **Probar micrófono** y el medidor **Nivel de entrada** muestran el nivel en directo, avisan si satura (**Saturando**) y dicen si no hay micrófono. Una grabación también avisa de cuándo saturó el sonido.
- **Un GIF de un tramo.** En el reproductor, marca un tramo con <kbd>E</kbd> y <kbd>S</kbd> y pulsa <kbd>G</kbd> para hacer un GIF solo de esa parte.
</details>

## Encuéntrala por lo que decía.

**Busca el texto dentro de tus capturas. Las lee el OCR de Windows, en tu equipo.**

<table>
<tr>
<td width="50%" valign="top"><img src="docs/clips/es/history.webp" alt="La página Historial filtrando capturas, vídeos y GIF por días mientras una palabra de búsqueda encuentra una captura por el texto escrito dentro de ella" width="100%"></td>
<td width="50%" valign="top"><img src="docs/screens/history-search.png" alt="El Historial buscando la palabra factura: capturas de facturas encontradas por el texto que contienen, con la coincidencia resaltada, agrupadas en Hoy y Ayer" width="100%"></td>
</tr>
</table>

<details>
<summary>Cómo funciona</summary>
<br>

La sección **Historial** muestra todas las capturas, vídeos y GIF por días, con miniaturas, filtros (Todo, Capturas, Vídeos, GIF) y una búsqueda que también mira dentro de las imágenes. Stackshot lee el texto de cada captura en segundo plano con el OCR de Windows, en tu equipo (en pausa mientras capturas o grabas), para que encuentres el mensaje de error de la semana pasada escribiendo una palabra que salía en él. Las coincidencias se resaltan en un fragmento de lo que decía la captura.

El historial cubre las 5.000 capturas más recientes, con las guardadas y las subcarpetas por fecha incluidas. El índice de texto está en `%LOCALAPPDATA%\Stackshot\historial`; General > Recientes apaga la lectura y lo borra (**Buscar por el texto de las capturas**), y **Borrar el índice de texto** lo vacía.
</details>

## XML y Markdown, a otro nivel.

**Compara dos XML, lee SOAP de un vistazo y cópialos como JSON. Exporta Markdown a PDF.**

<table>
<tr>
<td width="50%" valign="top"><img src="docs/clips/es/viewers.webp" alt="El visor de Markdown con un documento renderizado junto al visor de XML con un árbol de colores plegable" width="100%"></td>
<td width="50%" valign="top"><img src="docs/clips/es/docs-pro.webp" alt="Herramientas para documentar: una comparación de XML con las diferencias resaltadas, y un documento Markdown con panel de índice, una imagen pegada y una tabla pegada desde una hoja de cálculo" width="100%"></td>
</tr>
</table>

<details>
<summary>Cómo funciona</summary>
<br>

Abre archivos `.md` y `.xml` desde el Explorador (opcional; Windows siempre tiene la última palabra), pégalos o suéltalos en la ventana. El Markdown se muestra con títulos, listas, tablas, código y citas, con un editor al lado, buscar y reemplazar, validación y **Ordenar**. El XML se convierte en un árbol de colores, plegable y con búsqueda, con formatear, compactar, XPath, validación y copiar como imagen. Los dos visores conservan la codificación y los saltos de línea de cada archivo, y preguntan antes de descartar cambios sin guardar.

**XML y SOAP**

- **Barra de resumen SOAP.** Un mensaje SOAP muestra su operación, su hora y su tamaño y una etiqueta OK o **Fault** con el código y el mensaje del fallo en rojo, con botones para el XML anterior y el siguiente de la carpeta.
- **Comparar dos XML.** **Comparar con el XML anterior de la carpeta** o **Comparar con otro archivo…** muestra en el árbol los nodos cambiados, añadidos y quitados, sin tener en cuenta los espacios, el orden de los atributos ni los prefijos de espacio de nombres. <kbd>F7</kbd> y <kbd>Mayús</kbd>+<kbd>F7</kbd> recorren las diferencias, y «Solo diferencias» oculta el resto.
- **Copiar como valor o JSON.** Con el botón derecho en un nodo se copia su valor, el valor de un atributo o toda la rama como JSON.
- **XML escapado y adjuntos en base64.** El XML escapado dentro de un campo se abre como una rama más del árbol, y un archivo que viaja en base64 se abre o se guarda con un clic. Los programas, scripts, accesos directos, ZIP y archivos de Office con macros solo se guardan, nunca se abren.

<div align="center">
<img src="docs/screens/xml-compare.png" alt="El visor de XML comparando dos respuestas SOAP: la barra de resumen muestra la operación y OK, y los nodos cambiados, añadidos y quitados aparecen resaltados con el recuento de diferencias" width="720">
</div>

En General > Avanzado se puede vigilar una carpeta: cada XML nuevo que aparece en ella se abre solo en el visor, delante de todo, y <kbd>[</kbd> <kbd>]</kbd> pasan al anterior o al siguiente. Está pensado para programas que dejan cada mensaje SOAP como un archivo XML en una carpeta: solo aparece si informática fija esa carpeta (la directiva `XmlWatchFolder`, ver [docs/it-policies.md](docs/it-policies.md)) o donde ya estaba configurado, y viene apagado. Desde la línea de órdenes, `--last-xml` abre el más reciente.

**Markdown para documentar**

- **Imágenes.** Pega o suelta una imagen en un documento guardado y se guarda en una carpeta `imagenes` junto a él, con el enlace ya insertado. Las imágenes de otros equipos o de la web nunca se cargan solo por abrir un documento.
- **Tablas de hojas de cálculo.** Pega celdas de Excel, LibreOffice o Google Sheets y se convierten en una tabla Markdown (<kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>V</kbd> pega texto sin formato). Al pulsar <kbd>Enter</kbd> en una lista numerada, se renumeran los elementos de debajo.
- **Índice.** El botón **Índice** abre un panel lateral con los títulos de H1 a H3 que salta a cada uno y sigue por dónde vas.
- **PDF con texto de verdad.** **Exportar PDF…** escribe páginas A4 con texto seleccionable, fuentes incrustadas, números de página y marcadores.
</details>

## Una ventana. A tu manera.

**Tres vistas de inicio, todos los modos y todos los ajustes en un solo sitio.**

<div align="center">
<img src="docs/clips/es/home.webp" alt="La ventana principal en sus vistas Escena, Widgets y Minimalista, y la página de ajustes de grabación" width="100%">
</div>

<table>
<tr>
<td width="50%" valign="top"><img src="docs/clips/es/theme.webp" alt="La ventana principal cambiando entre el aspecto oscuro y el claro" width="100%"><br><b>Claro u oscuro.</b> Sigue a Windows o eliges uno.</td>
<td width="50%" valign="top"><img src="docs/clips/es/tray.webp" alt="Un clic en el icono de la bandeja abre un panel redondeado con todos los modos y su atajo, las últimas capturas y las acciones principales" width="100%"><br><b>Siempre a un clic.</b> Todos los modos y tus últimas capturas, desde la bandeja.</td>
</tr>
</table>

<details>
<summary>Cómo funciona</summary>
<br>

Todos los modos de captura, atajos y ajustes en un solo sitio: Inicio, Historial, Atajos, General, Fondo y editor, Grabación, Mascota y Acerca de. Elige cómo se ve el inicio: **Minimalista** (la mascota, una pregunta y todos los modos), **Widgets** (el día de un vistazo: la última captura, capturas por día, la grabación y la carpeta) o **Escena**. Los perfiles rápidos (**Rendimiento**, **Equilibrado**, **Completo**) lo ajustan todo de un clic.

**Encuentra cualquier ajuste.** Un buscador arriba de la barra lateral (<kbd>Ctrl</kbd>+<kbd>F</kbd>) encuentra cualquier ajuste mientras escribes, sin distinguir acentos ni mayúsculas, con todas las palabras en cualquier orden y los sinónimos habituales (al escribir «micro» aparece el micrófono). Los resultados se agrupan por sección; <kbd>Enter</kbd> abre uno y la fila brilla un instante. Las páginas largas de General y Grabación empiezan con unos chips que saltan a cada grupo.

<div align="center">
<img src="docs/screens/settings-search.png" alt="El buscador de ajustes de la barra lateral: al escribir micro aparece el ajuste Micrófono dentro de Grabación con las palabras coincidentes resaltadas" width="720">
</div>

**Accesible y legible.** Los lectores de pantalla (Narrador, NVDA) pueden leer y usar todos los controles, todo funciona con el teclado con un anillo de foco visible y se respetan los temas de contraste de Windows. Los avisos propios de Stackshot, con botones de acción donde ayudan, sustituyen a los globos de la bandeja de Windows; nunca roban el foco y no salen en las capturas ni al compartir pantalla.

**Claro u oscuro.** Stackshot sigue a Windows, o eliges claro u oscuro en General > Apariencia. Todo cambia al momento, sin reiniciar.

**Siempre a un clic.** Al cerrar la ventana, Stackshot sigue en la bandeja. Un clic en su icono abre un panel con todos los modos y su atajo en teclas, tus cuatro últimas capturas y acceso rápido a la ventana, a las capturas guardadas y a los ajustes. También funciona con las flechas y <kbd>Enter</kbd>.
</details>

## Conoce a Pixel. 39 disfraces.

**Tu compañero de capturas, con 39 disfraces. Hasta puede vivir en tu escritorio.**

<table>
<tr>
<td width="50%" valign="top"><img src="docs/clips/es/mascot.webp" alt="Pixel, la mascota, cambiando de disfraz, y después la cuadrícula con los 39 disfraces" width="100%"></td>
<td width="50%" valign="top"><img src="docs/clips/es/pet.webp" alt="Pixel paseando sobre la barra de tareas, saltando a una ventana y echándose la siesta; una captura de esa zona sale sin Pixel" width="100%"></td>
</tr>
</table>

<details>
<summary>Cómo funciona</summary>
<br>

Te presentamos a **Pixel**: doce especies, 23 colores, estilos de ojos, decenas de gorros, ropa y complementos, tres personalidades y niveles de amistad que desbloquean extras. Celebra tus capturas y da consejos. Se puede desactivar en los ajustes, y una organización puede desactivarla para todos.

Si quieres, vive en tu escritorio: pasea sobre la barra de tareas, salta a la ventana que tienes delante, viaja con ella si la mueves y se echa la siesta cuando no estás. Y es invisible para las capturas, las grabaciones y al compartir pantalla, así que nunca sale en tus capturas.

La sección **Personajes** la disfraza con homenajes fan a personajes de anime, series, cómics y videojuegos, y algunos originales: **51 en total**. Un clic y se convierte en Goku, Chopper o Spider-Man; al pasar el ratón por un disfraz se ve de dónde sale.

<details>
<summary>Los 39 disfraces de personajes</summary>
<br>
<div align="center">
<img src="docs/characters.png" alt="Los 39 disfraces de personajes con su nombre y procedencia" width="760">
</div>
</details>
</details>

## Y mucho más.

**Los pequeños detalles, todos en los mismos 2,2 MB.**

<table>
<tr>
<td width="50%" valign="top"><img src="docs/clips/es/aio.webp" alt="Modo todo en uno: el área seleccionada con tiradores y una barra con Capturar, Vídeo, GIF y Desplazar; al elegir Vídeo empieza a grabar esa misma área" width="100%"><br><b>Primero seleccionas, luego decides.</b> Ajusta el área y elige capturar, grabar o desplazar.</td>
<td width="50%" valign="top"><img src="docs/clips/es/pin.webp" alt="Una captura fijada desde su miniatura flota sobre las ventanas, hace zoom con la rueda y se vuelve transparente con Ctrl y la rueda" width="100%"><br><b>Fíjala encima de todo.</b> Rueda para el zoom, Ctrl + rueda para la transparencia.</td>
</tr>
</table>

<details>
<summary>Cómo funciona</summary>
<br>

**Primero seleccionas, luego decides.** Activa el **modo todo en uno** (General > Al capturar > Después de seleccionar un área > **Mostrar opciones**) y el área se queda en pantalla después de seleccionarla, con tiradores para moverla y cambiarla de tamaño (también con las flechas) y una barra para elegir **Capturar**, **Vídeo**, **GIF** o **Desplazar**. <kbd>Enter</kbd> o un doble clic capturan.

**Fíjala encima de todo.** Fija cualquier captura desde su miniatura y flotará encima de todas las ventanas: la rueda hace zoom, <kbd>Ctrl</kbd>+rueda cambia la transparencia, arrastrándola la mueves y con doble clic o <kbd>Esc</kbd> se cierra. Ideal para copiar datos de una ventana a otra.

**Copiar texto con OCR.** Selecciona un área con <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd> y su texto pasa al portapapeles, listo para pegar. También desde el botón **Texto** de cada miniatura, del editor (<kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>C</kbd>, **Copiar texto**) o de una imagen fijada. Usa el reconocimiento de texto que trae Windows 10 y 11, así que no se descarga nada y nada sale del equipo. Prefiere el español cuando está instalado y, si no, usa los idiomas de tu Windows. Lee la letra pequeña de la pantalla y las capturas con desplazamiento enteras, línea a línea. Si tu Windows no tiene instalado el reconocimiento de texto, Stackshot te lo dice.

**Línea de órdenes.** Un botón de un teclado auxiliar, un acceso directo o un script pueden lanzar cualquier modo: mira [Línea de órdenes y directivas de empresa](#línea-de-órdenes-y-directivas-de-empresa).
</details>

## Ligera. Rápida. Privada.

**2,2 MB, sin permisos de administrador, sin cuentas ni telemetría.**

<table align="center">
<tr>
<td align="center" width="25%"><b>2,2 MB</b><br>un solo ejecutable</td>
<td align="center" width="25%"><b>0</b><br>permisos de administrador</td>
<td align="center" width="25%"><b>0</b><br>cuentas, nube ni telemetría</td>
<td align="center" width="25%"><b>1,6 ms</b><br>por movimiento del ratón</td>
</tr>
</table>

| Tarea | Medido |
|---|---|
| Selección de área | 1,6 ms de media por movimiento del ratón a 1080p; solo se redibuja lo que cambia |
| Tras capturar | La miniatura aparece al momento; el PNG se escribe en segundo plano |
| Ventana principal | Se abre en un cuarto de segundo; un 1,6 % de un núcleo abierta y en reposo |
| Editor | Se repinta en 4,5 ms mientras dibujas |
| Tapar datos sensibles | Unos 0,4 s por captura a 1080p |
| Texto de una captura larga con desplazamiento | Una página de 9000 px se lee en un segundo aproximadamente |
| Ejecutable | 2,2 MB, sin nada más que instalar |

## Instalación

<div align="center">
<img src="docs/clips/es/install.webp" alt="El Stackshot.exe descargado abre la ventana de bienvenida; Instalar y empezar lo instala e Impr Pant queda lista" width="100%">
</div>

1. Descarga **[Stackshot.exe](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.exe)** y ábrelo.
2. Elige si debe arrancar con Windows y dónde guardar las capturas.
3. Pulsa **Instalar y empezar** y después <kbd>Impr Pant</kbd>.

Se instala en `%LOCALAPPDATA%\Programs\Stackshot`, añade un acceso al menú Inicio y aparece en **Configuración > Aplicaciones** para desinstalarlo. Funciona en Windows 10 y 11 con el .NET Framework 4.8 que ya trae Windows. Las versiones nuevas se ofrecen desde la propia app; también puedes abrir un `Stackshot.exe` más reciente. Para probarlo sin instalar, ejecuta `Stackshot.exe --portable`: se ejecuta donde está, aunque sus ajustes y archivos temporales siguen en `%LOCALAPPDATA%\Stackshot`.

Como el ejecutable todavía no está firmado digitalmente, SmartScreen muestra un aviso la primera vez (**Más información > Ejecutar de todas formas**). En equipos gestionados por una empresa, SmartScreen puede bloquearlo sin opción de continuar; en ese caso, pide a informática que despliegue el paquete MSI que se describe a continuación. Cada versión se puede verificar como se explica en [SECURITY.md](SECURITY.md).

<details>
<summary><b>Despliegue en empresas</b> (MSI para Intune, Configuration Manager y herramientas similares)</summary>
<br>

Cada versión incluye también **[Stackshot.msi](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.msi)**, un paquete de Windows Installer que se instala por usuario, en el contexto del usuario y sin permisos de administrador.

| Acción | Orden o valor |
|---|---|
| Instalar | `msiexec /i Stackshot.msi /qn` |
| Desinstalar | `msiexec /x Stackshot.msi /qn` (o desde **Configuración > Aplicaciones**) |
| Opciones | `STARTWITHWINDOWS=0` para que no arranque con Windows, `LAUNCHAPP=0` para que no se abra al terminar |
| Detección | Código de producto del paquete, o `%LOCALAPPDATA%\Programs\Stackshot\Stackshot.exe` con versión ≥ la desplegada |
| Intune | Aplicación Win32, comportamiento de instalación **Usuario**, sin reinicio |

Cuando el MSI gestiona la instalación, las actualizaciones y la desinstalación pasan por Windows Installer: un `Stackshot.msi` más reciente sustituye a la versión anterior y conserva los ajustes del usuario, y la app solo avisa de las versiones nuevas. Stackshot se cierra y se vuelve a abrir solo, sin reiniciar. Al desinstalar se quitan la aplicación y sus datos, pero no las capturas que el usuario haya guardado. Si ya estaba instalado con `Stackshot.exe`, el MSI adopta esa instalación sin dejar entradas duplicadas.
</details>

<details>
<summary><b>Instalación silenciosa con el ejecutable</b></summary>

```powershell
Stackshot.exe --install --startup --folder "D:\Capturas"   # --no-start para no abrirlo al terminar
Stackshot.exe --uninstall --quiet
```
</details>

## Línea de órdenes y directivas de empresa

`Stackshot.exe --capture <modo>`, `--open-home` y `--last-xml` se hacen en la copia que ya está abierta (o arrancan Stackshot si no lo está), así que un botón de un teclado auxiliar, un acceso directo o un script pueden lanzar una captura:

```powershell
Stackshot.exe --capture region      # modos: region, repeat, screen, window, scroll, text, video, gif, markdown, pause
Stackshot.exe --open-home           # abre la ventana principal
Stackshot.exe --last-xml            # abre el XML más reciente de la carpeta vigilada
```

Con valores en `HKLM\SOFTWARE\Policies\Stackshot` (o en `HKCU`), informática puede dejar Stackshot configurado para todos. Los ajustes propios del usuario se conservan por si se quita la directiva, y las filas forzadas muestran **Lo decide tu organización** en los ajustes.

| Valor | Efecto |
|---|---|
| `SaveFolder` | Fija la carpeta donde se guardan las capturas (ruta completa; admite variables como `%USERNAME%`) |
| `CheckUpdates` = 0 | No busca actualizaciones nunca |
| `AutoUpdate` = 0 | No instala actualizaciones por su cuenta |
| `Mascot` = 0 | Sin mascota |
| `SensitiveAuto` = 1 o 0 | Tapa siempre, o nunca, los datos sensibles en las capturas nuevas |
| `HistorySearch` = 0 | El Historial no lee el texto de las capturas |

Todas las opciones, los códigos de salida y un ejemplo `.reg` listo para importar están en [docs/it-policies.md](docs/it-policies.md).

## Atajos de teclado

| Atajo | Acción |
|---|---|
| <kbd>Impr Pant</kbd> | Capturar un área, una ventana o la pantalla entera |
| <kbd>Ctrl</kbd>+<kbd>Impr Pant</kbd> | Capturar la pantalla en la que está el ratón |
| <kbd>Alt</kbd>+<kbd>Impr Pant</kbd> | Capturar la ventana activa |
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>Impr Pant</kbd> | Captura con desplazamiento (otra vez para terminar) |
| <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Alt</kbd>+<kbd>T</kbd> | Copiar el texto de un área de la pantalla |
| <kbd>Mayús</kbd>+<kbd>Impr Pant</kbd> | Grabar vídeo (otra vez para parar) |
| <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Impr Pant</kbd> | Grabar GIF |
| <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>Alt</kbd>+<kbd>M</kbd> | Abrir el visor de Markdown y XML |
| (ninguno por defecto) | Repetir la última área; asígnale una tecla en **Atajos** |

Todos se pueden cambiar en la sección **Atajos** de la ventana principal. Si Windows 11 reserva <kbd>Impr Pant</kbd> para Recortes, Stackshot la recupera para el usuario actual.

<details>
<summary>Atajos del editor</summary>

| Tecla | Acción |
|---|---|
| <kbd>F</kbd> <kbd>L</kbd> <kbd>R</kbd> <kbd>E</kbd> <kbd>T</kbd> <kbd>N</kbd> <kbd>H</kbd> <kbd>O</kbd> <kbd>P</kbd> <kbd>X</kbd> <kbd>C</kbd> | Flecha, línea, recuadro, elipse, texto, números, resaltar, foco, pixelar, tapar, recortar |
| <kbd>B</kbd> | Fondo de presentación |
| <kbd>1</kbd>–<kbd>5</kbd>, <kbd>−</kbd> <kbd>+</kbd> | Color, grosor |
| <kbd>Ctrl</kbd>+<kbd>Z</kbd>, <kbd>Ctrl</kbd>+<kbd>Y</kbd> | Deshacer, rehacer |
| <kbd>Ctrl</kbd>+<kbd>C</kbd>, <kbd>Ctrl</kbd>+<kbd>S</kbd>, <kbd>Ctrl</kbd>+<kbd>P</kbd> | Copiar, guardar, guardar como PDF |
| <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>C</kbd> | Copiar el texto de la imagen |
| <kbd>Ctrl</kbd>+<kbd>Mayús</kbd>+<kbd>X</kbd> | Tapar datos sensibles |
| <kbd>Ctrl</kbd>+rueda, <kbd>Ctrl</kbd>+<kbd>0</kbd>, <kbd>Ctrl</kbd>+<kbd>1</kbd> | Zoom, ajustar, 100 % |
| <kbd>Enter</kbd> | Aplicar, copiar y cerrar |
| <kbd>Supr</kbd>, flechas | Borrar o mover la marca seleccionada |
</details>

<details>
<summary>Atajos del reproductor, los visores y la ventana principal</summary>

| Tecla | Acción |
|---|---|
| <kbd>Espacio</kbd> o <kbd>K</kbd>, <kbd>←</kbd> <kbd>→</kbd>, <kbd>J</kbd> <kbd>L</kbd> | Reproductor: reproducir o pausar, saltar 5 s, saltar 10 s |
| <kbd>,</kbd> <kbd>.</kbd> | Reproductor: fotograma anterior o siguiente |
| <kbd>E</kbd> <kbd>S</kbd>, <kbd>G</kbd>, <kbd>C</kbd> | Reproductor: marcar el inicio y el final de un tramo, GIF del tramo, guardar el fotograma actual |
| <kbd>F7</kbd>, <kbd>Mayús</kbd>+<kbd>F7</kbd> | Comparación de XML: diferencia siguiente o anterior |
| <kbd>[</kbd> <kbd>]</kbd> | Visor de XML: XML anterior o siguiente de la carpeta |
| <kbd>Ctrl</kbd>+<kbd>F</kbd> | Ventana principal: buscar ajustes (en Historial, buscar en el historial) |
</details>

## Privacidad y seguridad

<div align="center">
<img src="docs/clips/es/updates.webp" alt="Un aviso de actualización descargando, comprobando la suma SHA-256 y la firma, e instalando" width="100%">
</div>

- Sin cuentas, sin servicios en la nube y sin telemetría. Las capturas no salen del equipo.
- Solo hay dos conexiones: la descarga, una sola vez, de FFmpeg 9.0.2 al grabar por primera vez, si la aceptas (comprobada con una suma SHA-256 guardada en el código) y la consulta de versiones nuevas a GitHub (30 segundos después de arrancar, cada 3 horas y al reanudar el equipo), que no envía nada tuyo y se puede desactivar en General.
- Las actualizaciones que se instalan desde la app se verifican: tienen que coincidir con la SHA-256 que publica GitHub y llevar una firma RSA-PSS válida hecha con la clave del mantenedor, que nunca se guarda en el repositorio; si no, se descartan. La instalación automática viene desactivada y nunca corta una grabación.
- SmartScreen avisa la primera vez que se ejecuta el archivo porque todavía no está firmado digitalmente, no por nada de lo que hace. Cada versión la compila GitHub Actions y se publica con su suma SHA-256 y una atestación de procedencia firmada, así que puedes comprobar lo que has descargado (mira las [preguntas frecuentes](#preguntas-frecuentes)).
- Las capturas que copia Stackshot nunca se sincronizan con otros equipos a través del portapapeles en la nube.
- El texto de tus capturas (para la búsqueda del Historial y para tapar datos sensibles) se lee con el OCR de Windows y se queda en `%LOCALAPPDATA%\Stackshot`; el índice se puede apagar o borrar cuando quieras.
- Mostrar los clics y los atajos de teclado en una grabación solo vigila el ratón y el teclado mientras dura esa grabación, y nunca muestra ni guarda lo que escribes.
- Los archivos que viajan dentro de un XML solo se decodifican cuando lo pides, y los programas, scripts y archivos con macros nunca se abren. Las imágenes de los documentos Markdown solo se cargan del disco local o del mismo servidor que el documento.
- Las capturas temporales se guardan en `%LOCALAPPDATA%\Stackshot\temp` y se borran al cabo de una hora por defecto (configurable), nunca mientras sigan en pantalla.
- Las ventanas de Stackshot, y la mascota del escritorio si la activas, quedan fuera de las capturas y de compartir pantalla.

Los detalles y cómo informar de una vulnerabilidad están en [SECURITY.md](SECURITY.md).

## Preguntas frecuentes

<details>
<summary><b>¿Es gratis?</b></summary>
<br>

Sí. Stackshot es gratis y de código abierto, con la [licencia MIT](LICENSE), sin cuentas y sin telemetría. El código está en [GitHub](https://github.com/rubenitx/stackshot).
</details>

<details>
<summary><b>¿En qué versiones de Windows funciona?</b></summary>
<br>

En Windows 10 y Windows 11, con el .NET Framework 4.8 que ya trae Windows y sin permisos de administrador. Copiar texto y la búsqueda por texto del Historial usan el reconocimiento de texto que trae Windows. Grabar vídeo y GIF necesita FFmpeg, que Stackshot descarga una sola vez.
</details>

<details>
<summary><b>¿Necesito permisos de administrador para instalarla?</b></summary>
<br>

No. Es un único `Stackshot.exe` de 2,2 MB que se instala solo para tu usuario desde su ventana de bienvenida.
</details>

<details>
<summary><b>¿Por qué Windows me avisa al abrirla la primera vez?</b></summary>
<br>

El ejecutable todavía no está firmado con un certificado de código, así que SmartScreen avisa la primera vez. Pulsa **Más información** y después **Ejecutar de todas formas**. Las actualizaciones que descarga la propia app sí se comprueban con SHA-256 y firma. Cada versión la compila GitHub Actions a partir de este repositorio y se publica con su suma SHA-256 y una atestación de procedencia firmada, así que puedes comprobar que el archivo que has descargado es el que produjo el flujo de trabajo:

```powershell
gh attestation verify .\Stackshot.exe --repo rubenitx/stackshot
```

Stackshot no necesita permisos de administrador y su código fuente está abierto para que cualquiera lo lea. En equipos donde SmartScreen bloquea las aplicaciones sin firmar, pide a informática que despliegue el [paquete MSI](#instalación). En [SECURITY.md](SECURITY.md) está todo lo que hace en tu equipo.
</details>

<details>
<summary><b>¿Envía mis capturas a algún sitio?</b></summary>
<br>

No. No hay cuentas, ni nube, ni telemetría. Tus capturas se quedan en tu equipo, y el texto que lee para buscarlas en el Historial también.
</details>

<details>
<summary><b>¿Puede grabar vídeo?</b></summary>
<br>

Sí. Graba la pantalla en MP4 o GIF hasta 60 fps, con sonido del equipo, micrófono y tu cámara si quieres. Con la calidad Cine guarda un MKV sin pérdidas. Después puedes recortar los extremos y cambiar la velocidad. La primera vez que grabas, Stackshot te ofrece descargar FFmpeg.
</details>

<details>
<summary><b>¿Hay versión para empresas?</b></summary>
<br>

Sí: un [paquete MSI](https://github.com/rubenitx/stackshot/releases/latest/download/Stackshot.msi) que hace la misma instalación con Windows Installer, para equipos gestionados. Las [directivas](docs/it-policies.md) del registro permiten a informática dejarlo configurado para todos.
</details>

<details>
<summary><b>¿Cómo se desinstala?</b></summary>
<br>

Desde **Configuración > Aplicaciones**, como cualquier otra, con `Stackshot.exe --uninstall`, o con `msiexec /x Stackshot.msi` si se instaló con el MSI. Al desinstalar se quitan la aplicación y sus datos, pero no las capturas que hayas guardado ni las grabaciones recuperadas tras un fallo. Si Stackshot le había quitado <kbd>Impr Pant</kbd> a Recortes, se la devuelve.
</details>

<details>
<summary><b>¿Funciona sin conexión?</b></summary>
<br>

Sí. Capturar, editar, copiar texto, el historial, los visores y el reproductor no necesitan conexión. Internet solo se usa para la descarga única de FFmpeg que necesitan la grabación de vídeo y GIF, y para buscar actualizaciones, que se pueden desactivar.
</details>

<details>
<summary><b>¿Dónde se guardan mis capturas?</b></summary>
<br>

Las capturas que guardas (el botón **Guardar** de una miniatura, o desde el editor) van por defecto a `Imágenes\Stackshot`; se cambia en General > Capturas guardadas, o una organización puede fijarla con una directiva. Las que no guardas son temporales: están en `%LOCALAPPDATA%\Stackshot\temp` y se borran al cabo de una hora por defecto (General > Recientes). Los ajustes están en `%LOCALAPPDATA%\Stackshot\settings.ini`.
</details>

<details>
<summary><b>¿Instalador o portátil?</b></summary>
<br>

Al abrir `Stackshot.exe` te ofrece instalarlo para tu usuario (sin permisos de administrador y con actualizaciones desde la propia app). `Stackshot.exe --portable` lo ejecuta donde está, sin instalar. Las organizaciones pueden desplegar el MSI.
</details>

<details>
<summary><b>¿Tiene la interfaz en español?</b></summary>
<br>

Sí, y desde la 2.3.0 también en inglés. Una instalación nueva sigue el idioma de Windows (español en un Windows en español, catalán, gallego o euskera; inglés en los demás), y **Ajustes › General › Apariencia › Idioma** lo cambia al momento (Automático, Español, English). Quien ya usaba Stackshot sigue en español mientras no elija otro. La traducción al inglés la aportó [@altmashnaveed](https://github.com/altmashnaveed).
</details>

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
| `src/` | La aplicación (C# 5, WinForms y WPF): captura, miniaturas, editor, grabación, visores, ventana principal e instalación |
| `assets/` | Logo e icono, dibujados en código por `tools\make-logo.ps1` |
| `docs/` | Imágenes y vídeos del README, generados con la app real sobre contenido sintético: capturas por `tools\make-screenshots.ps1`, la animación de la mascota y la lámina de personajes por `tools\make-reel.ps1`, y el vídeo y los clips de funciones por `tools\make-film.ps1` |

## Contribuir

Los fallos y las sugerencias son bienvenidos en [Issues](https://github.com/rubenitx/stackshot/issues). Los comentarios del código están en inglés; cómo compilar, las reglas del código y el mapa de ficheros están en [CONTRIBUTING.md](CONTRIBUTING.md). Los problemas de seguridad se comunican de forma privada, como se indica en [SECURITY.md](SECURITY.md).

## Licencia

[MIT](LICENSE) © 2026 Rubén Martínez.

FFmpeg no se incluye. Se descarga aparte, con su propia licencia (GPL), solo al grabar vídeo o GIF.

CleanShot X, ShareX, Snagit, Greenshot y Lightshot se mencionan solo para explicar qué tipo de herramienta es Stackshot. Sus nombres pertenecen a sus respectivos dueños, y Stackshot no tiene relación con ninguno de ellos ni cuenta con su respaldo.

Los disfraces de la sección «Personajes» son homenajes fan sin ánimo de lucro, dibujados desde cero con el estilo de la mascota. Los personajes y sus nombres (Finn, Jake, BMO, Pakkun, Naruto, Sasuke, Kakashi, Edward Elric, Eren Jaeger, Levi, Luffy, Zoro, Chopper, Goku, Vegeta, Tanjiro, Nezuko, Gojo, Gon, Killua, Hisoka, Saitama, Shin-chan, Pikachu, Doraemon, Totoro, Kratos, Doom Slayer, Spider-Man, Iron Man, Batman, Superman, Flash, Claude, Codex, Kero, Kamina, Desdentao, Furia Luminosa, Tormenta, Colmillo, Albóndiga, Stitch, Kirby, Grogu, Eevee, Charmander) pertenecen a sus respectivos dueños, y Stackshot no tiene relación con ninguno de ellos ni cuenta con su respaldo. Si tienes los derechos de alguno y quieres que se retire, abre una incidencia y se quitará.
