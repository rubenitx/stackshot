# Builds bin\Stackshot.msi from bin\Stackshot.exe (run build.ps1 first) and tools\Stackshot.wxs.
# Uses WiX Toolset 3.14.1 (MS-RL), downloaded once to bin\wix and verified against a pinned SHA-256; nothing is installed.
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

# Three-part version (Windows Installer ignores the fourth).
$v = [Version](Get-Item $exe).VersionInfo.FileVersion
$version = "$($v.Major).$($v.Minor).$($v.Build)"
$obj = Join-Path $bin 'Stackshot.wixobj'
$msi = Join-Path $bin 'Stackshot.msi'
$icon = Join-Path $root 'assets\stackshot.ico'

& (Join-Path $wix 'candle.exe') -nologo -ext WixUtilExtension "-dVersion=$version" "-dExe=$exe" "-dIcon=$icon" `
    -out $obj (Join-Path $PSScriptRoot 'Stackshot.wxs')
if ($LASTEXITCODE -ne 0) { throw 'candle: el paquete no compila.' }
# Suppressed ICE warnings that are expected for this package:
#   ICE91: files go to the user profile (per-user install by design).
#   ICE61: same-version reinstall is allowed (AllowSameVersionUpgrades).
#   ICE69: non-advertised shortcuts point to the .exe in another component.
& (Join-Path $wix 'light.exe') -nologo -ext WixUtilExtension -spdb -sice:ICE91 -sice:ICE61 -sice:ICE69 -out $msi $obj
if ($LASTEXITCODE -ne 0) { throw 'light: no se pudo generar el MSI.' }
Remove-Item $obj -Force
$size = [math]::Round((Get-Item $msi).Length / 1KB)
Write-Host "MSI: $msi ($size KB, versión $version)"
