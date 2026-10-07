# Builds Stackshot with the C# compiler bundled with Windows (.NET Framework 4.8). No Visual Studio needed.
#   .\build.ps1            -> bin\Stackshot.exe
#   .\build.ps1 -Run       build and run without installing (--portable)
#   .\build.ps1 -Install   build and install/update for the current user
param([switch]$Run, [switch]$Install)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$out = Join-Path $root 'bin'
$exe = Join-Path $out 'Stackshot.exe'
New-Item -ItemType Directory -Force $out | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'No encuentro csc.exe de .NET Framework 4 (viene con Windows 10/11).' }

$sources = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$icon = Join-Path $root 'assets\stackshot.ico'
$logo = Join-Path $root 'assets\logo-256.png'
$manifest = Join-Path $root 'src\app.manifest'

$tmp = Join-Path $out ('Stackshot.' + [guid]::NewGuid().ToString('N') + '.tmp.exe')
$cscArgs = @('/nologo', '/codepage:65001', '/target:winexe', '/optimize+', '/warnaserror+', '/platform:anycpu',
             "/out:$tmp", "/win32manifest:$manifest",
             '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
             '/r:System.Web.Extensions.dll')
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon"; $cscArgs += "/resource:$icon,stackshot.ico" }
if (Test-Path $logo) { $cscArgs += "/resource:$logo,logo.png" }

# Build to a temp file and swap it in, so a failed build leaves the previous .exe intact.
$output = & $csc @cscArgs $sources 2>&1
if ($LASTEXITCODE -ne 0) {
    $output | ForEach-Object { Write-Host $_ }
    if (Test-Path $tmp) { Remove-Item $tmp -Force }
    throw 'No compila.'
}
Move-Item -Force $tmp $exe
$size = [math]::Round((Get-Item $exe).Length / 1KB)
Write-Host "Compilado: $exe ($size KB)"

# Wait for the installer only: -Wait would also wait for the installed copy, which keeps running.
if ($Install) { (Start-Process -FilePath $exe -ArgumentList '--install', '--startup' -PassThru).WaitForExit() }
elseif ($Run) { Start-Process -FilePath $exe -ArgumentList '--portable' }
