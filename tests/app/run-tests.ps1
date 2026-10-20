# Compiles the app shell tests (src\ plus tests\app\*.cs) with the same csc and references as build.ps1, then runs
# them headless: no window, no screen, no real clipboard; the registry part uses a throwaway HKCU\Software\StackshotTests
# key that is removed afterwards. Output goes to %TEMP%\stackshot-app-tests; bin\ is never touched.
#   .\run-tests.ps1                  every suite (quit orphans keep explain clipboard uninstall pin posters)
#   .\run-tests.ps1 quit orphans     only these
# Exit code = number of FAIL.
$ErrorActionPreference = 'Continue'

$appDir = $PSScriptRoot
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$srcDir = Join-Path $root 'src'
$outDir = Join-Path $env:TEMP 'stackshot-app-tests'
$exe = Join-Path $outDir 'AppTests.exe'
New-Item -ItemType Directory -Force $outDir | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { Write-Host 'No encuentro csc.exe de .NET Framework 4.'; exit 1 }

$sources = @()
$sources += Get-ChildItem $srcDir -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$sources += Get-ChildItem $appDir -Filter '*.cs' | ForEach-Object { $_.FullName }
$manifest = Join-Path $srcDir 'app.manifest'
$icon = Join-Path $root 'assets\stackshot.ico'

$cscArgs = @('/nologo', '/codepage:65001', '/target:exe', '/main:Stackshot.Tests.AppTests', '/optimize+', '/warnaserror+',
             '/platform:anycpu', "/out:$exe", "/win32manifest:$manifest",
             '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
             '/r:System.Web.Extensions.dll', '/r:System.Security.dll', '/r:System.Xaml.dll')
$wpf = Join-Path (Split-Path $csc) 'WPF'
foreach ($dll in 'PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll', 'WindowsFormsIntegration.dll') { $cscArgs += '/r:' + (Join-Path $wpf $dll) }
# UI Automation (also part of Windows): what screen readers learn about the controls Stackshot draws itself.
foreach ($dll in 'UIAutomationProvider.dll', 'UIAutomationTypes.dll') { $cscArgs += '/r:' + (Join-Path $wpf $dll) }
# Windows' own OCR (Windows.Media.Ocr) and PDF engine (Windows.Data.Pdf) are reached through their WinRT metadata, also part of Windows.
$cscArgs += '/r:' + (Join-Path (Split-Path $csc) 'System.Runtime.dll')
$cscArgs += '/r:' + (Join-Path (Split-Path $csc) 'System.Runtime.WindowsRuntime.dll')
foreach ($md in 'Data', 'Foundation', 'Graphics', 'Media', 'Storage', 'Globalization') { $cscArgs += '/r:' + (Join-Path $env:WINDIR "System32\WinMetadata\Windows.$md.winmd") }
if (Test-Path $icon) { $cscArgs += "/resource:$icon,stackshot.ico" }

if (Test-Path $exe) { Remove-Item $exe -Force }
$output = & $csc @cscArgs $sources 2>&1 | ForEach-Object { "$_" }
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) {
    $output | ForEach-Object { Write-Host $_ }
    Write-Host 'No compila (tests).'
    exit 1
}

& $exe @args
exit $LASTEXITCODE
