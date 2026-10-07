# Builds tools\Reel.cs and tools\Promo.cs with the app sources and writes the README media, all rendered offscreen with
# read-only settings: docs\mascot.gif, docs\characters.png, docs\demo.gif (tour of the main window) and docs\promo.gif.
# Usage: .\tools\make-reel.ps1   (about a minute)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$out = Join-Path ([IO.Path]::GetTempPath()) 'stackshot-reel.exe'
$src = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }) +
       (Join-Path $PSScriptRoot 'GifWriter.cs') + (Join-Path $PSScriptRoot 'Reel.cs') + (Join-Path $PSScriptRoot 'Promo.cs')
& $csc /nologo /optimize+ /codepage:65001 /target:winexe /main:Stackshot.Reel "/out:$out" `
    "/resource:$root\assets\logo-256.png,logo.png" "/resource:$root\assets\stackshot.ico,stackshot.ico" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Web.Extensions.dll $src
if ($LASTEXITCODE -ne 0) { throw 'No compila.' }
$docs = Join-Path $root 'docs'
(Start-Process -FilePath $out -ArgumentList "`"$docs\mascot.gif`"" -PassThru).WaitForExit()
(Start-Process -FilePath $out -ArgumentList "`"$docs\characters.png`" grid" -PassThru).WaitForExit()
(Start-Process -FilePath $out -ArgumentList "`"$docs\demo.gif`" videos `"$docs\promo.gif`"" -PassThru).WaitForExit()
Remove-Item $out -ErrorAction SilentlyContinue
foreach ($f in 'mascot.gif', 'characters.png', 'demo.gif', 'promo.gif') { Write-Host ("{0}: {1:N1} MB" -f $f, ((Get-Item (Join-Path $docs $f)).Length / 1MB)) }
