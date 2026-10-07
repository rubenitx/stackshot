# Genera bin\Stackshot.msi a partir de bin\Stackshot.exe (compílalo antes con build.ps1) y tools\Stackshot.wxs.
# Usa WiX Toolset 3.14.1 (libre, MS-RL), que funciona con el .NET Framework de Windows: se descarga una vez a
# bin\wix, se comprueba su SHA-256 y no se instala nada.
#   .\tools\build-msi.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'bin'
$exe = Join-Path $bin 'Stackshot.exe'
if (-not (Test-Path $exe)) { throw 'Falta bin\Stackshot.exe: ejecuta antes .\build.ps1' }

$wixUrl = 'https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip'
$wixSha = '6ac824e1642d6f7277d0ed7ea09411a508f6116ba6fae0aa5f2c7daa2ff43d31'
$wix = Join-Path $bin 'wix'
if (-not (Test-Path (Join-Path $wix 'candle.exe'))) {
    New-Item -ItemType Directory -Force $wix | Out-Null
    $zip = Join-Path $bin 'wix314-binaries.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Write-Host 'Descargando WiX Toolset 3.14.1...'
    Invoke-WebRequest -UseBasicParsing $wixUrl -OutFile $zip
    $sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    if ($sha -ne $wixSha) { Remove-Item $zip -Force; throw "WiX descargado no coincide con la SHA-256 esperada ($sha)." }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $wix)
    Remove-Item $zip -Force
}

# Versión de 3 cifras (Windows Installer ignora la cuarta).
$v = [Version](Get-Item $exe).VersionInfo.FileVersion
$version = "$($v.Major).$($v.Minor).$($v.Build)"
$obj = Join-Path $bin 'Stackshot.wixobj'
$msi = Join-Path $bin 'Stackshot.msi'
$icon = Join-Path $root 'assets\stackshot.ico'

& (Join-Path $wix 'candle.exe') -nologo -ext WixUtilExtension "-dVersion=$version" "-dExe=$exe" "-dIcon=$icon" `
    -out $obj (Join-Path $PSScriptRoot 'Stackshot.wxs')
if ($LASTEXITCODE -ne 0) { throw 'candle: el paquete no compila.' }
# Avisos de validación que no son problemas en este paquete:
#   ICE91: los ficheros van al perfil del usuario; es justo lo que queremos (instalación por usuario).
#   ICE61: se permite reinstalar la misma versión (AllowSameVersionUpgrades).
#   ICE69: los accesos directos apuntan al .exe, que está en otro componente del mismo paquete (normal en accesos no anunciados).
& (Join-Path $wix 'light.exe') -nologo -ext WixUtilExtension -spdb -sice:ICE91 -sice:ICE61 -sice:ICE69 -out $msi $obj
if ($LASTEXITCODE -ne 0) { throw 'light: no se pudo generar el MSI.' }
Remove-Item $obj -Force
$size = [math]::Round((Get-Item $msi).Length / 1KB)
Write-Host "MSI: $msi ($size KB, versión $version)"
