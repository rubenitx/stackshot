# Regenera las imágenes del README (docs\*.png) con tools\Studio.cs: monta un escritorio de mentira en la pantalla
# donde no está el ratón y fotografía la pila, el editor, la selección, la grabación, la bienvenida y los ajustes.
# Tarda unos 15 segundos; durante ese rato esa pantalla queda ocupada.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$work = Join-Path $env:TEMP 'stackshot-studio'
New-Item -ItemType Directory -Force $work | Out-Null
$exe = Join-Path $work 'studio.exe'
$sources = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }) + (Join-Path $PSScriptRoot 'Studio.cs')
$icon = Join-Path $root 'assets\stackshot.ico'
$logo = Join-Path $root 'assets\logo-256.png'
$out = & $csc /nologo /codepage:65001 /target:winexe /optimize+ /main:Stackshot.Studio "/out:$exe" "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
    "/resource:$icon,stackshot.ico" "/resource:$logo,logo.png" /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll $sources 2>&1
if ($LASTEXITCODE -ne 0) { $out | ForEach-Object { Write-Host $_ }; throw 'El estudio no compila.' }
$docs = Join-Path $root 'docs'
$p = Start-Process -FilePath $exe -ArgumentList ('"' + $docs + '"') -PassThru
if (-not $p.WaitForExit(90000)) { $p.Kill(); throw 'El estudio no termin' + [char]0xF3 + '.' }
Get-ChildItem $docs -Filter *.png | Select-Object Name, Length | Format-Table -AutoSize
