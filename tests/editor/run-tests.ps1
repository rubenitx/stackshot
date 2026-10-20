# Compiles the editor tests (src\ plus tests\editor\EditorTests*.cs) with the same csc and references as build.ps1,
# then runs them headless (no window, no screen capture). Output goes to %TEMP%\stackshot-editor-tests; bin\ is never
# touched. FFmpeg: %LOCALAPPDATA%\Stackshot\ffmpeg\ffmpeg.exe (suites that need it skip without it).
#   .\run-tests.ps1                    all suites (args export strip atomic keep tools player share windows)
#   .\run-tests.ps1 args atomic        only these suites
#   .\run-tests.ps1 sharebench         benchmark of the share export, never part of "all" (STACKSHOT_BENCH_* variables in
#                                      EditorTestsShareBench.cs; results in docs\cine-share-export.md)
#   .\run-tests.ps1 -SrcRoot <dir> args   compile the tests against another src tree (a dir with *.cs, or a dir that
#                                         contains src\). Used to check that a test catches a known bug.
# Results: tests\editor\results\<yyyyMMdd-HHmmss>\results.md and results.json. Exit code = number of FAIL.
$ErrorActionPreference = 'Continue'

# -SrcRoot is taken by hand so that the suite names stay plain arguments.
$SrcRoot = $null
$suites = @()
for ($i = 0; $i -lt $args.Count; $i++) {
    if ($args[$i] -eq '-SrcRoot' -and ($i + 1) -lt $args.Count) { $SrcRoot = $args[$i + 1]; $i++ }
    else { $suites += $args[$i] }
}

$testDir = $PSScriptRoot
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$srcDir = Join-Path $root 'src'
if ($SrcRoot) {
    $given = Resolve-Path $SrcRoot -ErrorAction SilentlyContinue
    if (-not $given) { Write-Host "No existe -SrcRoot $SrcRoot"; exit 1 }
    $given = $given.Path
    if (Test-Path (Join-Path $given 'src') -PathType Container) { $srcDir = Join-Path $given 'src' } else { $srcDir = $given }
    Write-Host "Compilando contra $srcDir"
}
$outDir = Join-Path $env:TEMP 'stackshot-editor-tests'
$exe = Join-Path $outDir 'EditorTests.exe'
New-Item -ItemType Directory -Force $outDir | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { Write-Host 'No encuentro csc.exe de .NET Framework 4.'; exit 1 }

$sources = @()
$sources += Get-ChildItem $srcDir -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$sources += Get-ChildItem $testDir -Filter 'EditorTests*.cs' | ForEach-Object { $_.FullName }
$icon = Join-Path $root 'assets\stackshot.ico'
$logo = Join-Path $root 'assets\logo-256.png'
$manifest = Join-Path $srcDir 'app.manifest'

$cscArgs = @('/nologo', '/codepage:65001', '/target:exe', '/main:Stackshot.Tests.EditorTests', '/optimize+', '/warnaserror+',
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
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon"; $cscArgs += "/resource:$icon,stackshot.ico" }
if (Test-Path $logo) { $cscArgs += "/resource:$logo,logo.png" }

if (Test-Path $exe) { Remove-Item $exe -Force }
$output = & $csc @cscArgs $sources 2>&1 | ForEach-Object { "$_" }
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) {
    $output | ForEach-Object { Write-Host $_ }
    Write-Host 'No compila (tests).'
    exit 1
}
$warnings = @($output | Where-Object { $_ -match 'warning' })
if ($warnings.Count -gt 0) { $warnings | ForEach-Object { Write-Host $_ } }

$env:STACKSHOT_EDITOR_DIR = $testDir
& $exe @suites
exit $LASTEXITCODE
