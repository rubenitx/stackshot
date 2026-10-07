# Seguridad

## Qué hace (y qué no) Stackshot con tu equipo

- **Nada sale de tu equipo.** Sin cuentas, sin nube, sin telemetría, sin comprobaciones de versión.
- **La única conexión** es la descarga de [FFmpeg](https://github.com/GyanD/codexffmpeg/releases/tag/9.0.2) la primera vez que grabas vídeo o GIF (y solo si aceptas). Siempre es la misma versión (9.0.2), de una dirección fija, y su **SHA-256 va escrita en el código**: si no coincide, no se instala nada. Del paquete solo se extrae `ffmpeg.exe`.
- **Sin administrador.** Se instala en tu usuario (`%LOCALAPPDATA%\Programs\Stackshot`) y solo escribe en:
  - `%LOCALAPPDATA%\Stackshot` (ajustes, capturas temporales, registro, FFmpeg);
  - la carpeta de capturas guardadas que elijas;
  - los accesos del menú Inicio y de Inicio de Windows (si lo activas);
  - `HKCU\...\Uninstall\Stackshot` (para Configuración > Aplicaciones);
  - `HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled`, solo si le pides que use Impr Pant.
- **La limpieza automática** borra solo capturas temporales de más de una hora, y solo dentro de `%LOCALAPPDATA%\Stackshot\temp`.
- **Las DLL de Windows se cargan desde System32** en cuanto arranca, aunque el `.exe` esté en una carpeta con otros ficheros (Descargas), y la copia instalada vive en su propia carpeta.
- **Escribe con cuidado**: los ajustes se guardan de forma atómica y los atajos no aceptan una tecla normal sola (que dejaría de funcionar en todo Windows).
- **El flujo de compilación** de GitHub tiene permisos de solo lectura salvo al publicar, y sus acciones están fijadas a commits concretos.
- **Sus ventanas no salen en las capturas ni al compartir pantalla** (`WDA_EXCLUDEFROMCAPTURE`).

## Comprobar que el .exe es el bueno

Cada versión la compila GitHub Actions a partir de este código ([build.yml](.github/workflows/build.yml)) y publica:

- `Stackshot.exe.sha256`, la suma del ejecutable:
  ```powershell
  (Get-FileHash .\Stackshot.exe -Algorithm SHA256).Hash
  ```
- una **atestación de procedencia** firmada (Sigstore) que demuestra de qué código y de qué ejecución sale:
  ```powershell
  gh attestation verify .\Stackshot.exe --repo rubenitx/stackshot
  ```

El `.exe` todavía no lleva firma de código, así que Windows SmartScreen avisa la primera vez («Más información > Ejecutar de todas formas»).

## Avisar de un problema

Si encuentras una vulnerabilidad, **no abras un issue público**: usa [Report a vulnerability](https://github.com/rubenitx/stackshot/security/advisories/new) (aviso privado de GitHub). Respuesta en unos días; se publicará la corrección y se te dará crédito si quieres.

Versión con soporte: la última publicada.
