# Renders the film and its parts with tools\Film.cs: real windows photographed offscreen as plates, then every frame
# composed with WPF and encoded by the FFmpeg that Stackshot downloads for recording.
#   docs\video\stackshot-<lang>.mp4      the film at 1080p60, downscaled from the 4K master (for the README)
#   docs\video\poster-<lang>.png         its cover
#   docs\clips\<lang>\<part>.webp        one looping clip per README section
#   <Kit>\<lang>\stackshot-<lang>-4k.mp4 the 4K master, and <Kit>\<lang>\parts\*.mp4 (4K) and gif\*.gif for every part
# Usage: .\tools\make-film.ps1 [-Lang en,es] [-Fps 60] [-Scale 2] [-Jobs 4] [-Kit path] [-PlatesOnly] [-Stills]
#   (about 45 minutes for both languages with 4 jobs; progress in %TEMP%\stackshot-film\progress-*.log)
#   -Stills writes a few frames of every scene and part to %TEMP%\stackshot-film\stills instead, to review in seconds.
param([string[]]$Lang = @('en', 'es'), [int]$Fps = 60, [int]$Scale = 2, [string]$Kit = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'stackshot-media'),
      [string]$Ffmpeg = (Join-Path $env:LOCALAPPDATA 'Stackshot\ffmpeg\ffmpeg.exe'), [int]$Jobs = 4, [switch]$PlatesOnly, [switch]$Stills)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path $Ffmpeg)) { throw "FFmpeg not found at $Ffmpeg (record once with Stackshot or pass -Ffmpeg)." }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf = Join-Path (Split-Path $csc) 'WPF'
$work = Join-Path $env:TEMP 'stackshot-film'
New-Item -ItemType Directory -Force $work | Out-Null
$exe = Join-Path $work 'film.exe'
$src = @(Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }) +
       @('GifWriter.cs', 'Promo.cs', 'Studio.cs', 'Plates.cs', 'PlatesPro.cs', 'Film.cs', 'FilmScenes.cs', 'FilmParts.cs', 'FilmNew.cs' | ForEach-Object { Join-Path $PSScriptRoot $_ })
$fw = Split-Path $csc
$refs = @('/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll', '/r:System.Web.Extensions.dll', '/r:System.Security.dll', '/r:System.Xaml.dll')
foreach ($dll in 'PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll', 'WindowsFormsIntegration.dll', 'UIAutomationProvider.dll', 'UIAutomationTypes.dll') { $refs += '/r:' + (Join-Path $wpf $dll) }
$refs += '/r:' + (Join-Path $fw 'System.Runtime.dll')
$refs += '/r:' + (Join-Path $fw 'System.Runtime.WindowsRuntime.dll')
foreach ($md in 'Data', 'Foundation', 'Graphics', 'Media', 'Storage', 'Globalization') { $refs += '/r:' + (Join-Path $env:WINDIR "System32\WinMetadata\Windows.$md.winmd") }
$out = & $csc /nologo /optimize+ /codepage:65001 /target:winexe /main:Stackshot.Film "/out:$exe" "/win32manifest:$root\src\app.manifest" `
    "/resource:$root\assets\logo-256.png,logo.png" "/resource:$root\assets\stackshot.ico,stackshot.ico" @refs $src 2>&1
if ($LASTEXITCODE -ne 0) { $out | ForEach-Object { Write-Host $_ }; throw 'Film does not compile.' }
$plates = Join-Path $work 'plates'
New-Item -ItemType Directory -Force $plates | Out-Null
Remove-Item (Join-Path $plates 'errors.txt') -ErrorAction SilentlyContinue
# Nothing of the real installation is read or written: the app's data folder is a private one.
$env:STACKSHOT_TEST_DATA = Join-Path $work 'data'
New-Item -ItemType Directory -Force $env:STACKSHOT_TEST_DATA | Out-Null
(Start-Process $exe -ArgumentList "plates `"$plates`"" -PassThru -Wait) | Out-Null
if (Test-Path (Join-Path $plates 'errors.txt')) { Get-Content (Join-Path $plates 'errors.txt') | Write-Host; throw 'Some plates failed.' }

# Pictures of the real windows from the media kit's gallery (dark theme), for the scenes of the newest features. They are
# made by <Kit>\_tools\gallery\make-gallery.ps1 from the app's own code over synthetic content.
$galleryPlates = @{
    'screens'    = '04-historial', '06-historial-busqueda', '11-atajos', '13-general-al-capturar', '14-general-capturas-guardadas', '19b-buscar-micro',
                   '22-markdown-indice', '24-markdown-tabla-pegada', '25-markdown-mas-opciones', '26-markdown-pdf',
                   '27-xml-soap-ok', '29-xml-comparar', '31-xml-menu-nodo', '42-grabar-opciones'
    'components' = 'bar-recording', 'bar-paused'
}
foreach ($kind in $galleryPlates.Keys) {
    foreach ($n in $galleryPlates[$kind]) {
        $from = Join-Path $Kit "app\$kind\oscuro\$n.png"
        if (-not (Test-Path $from)) { throw "Gallery picture not found: $from (run $Kit\_tools\gallery\make-gallery.ps1 or pass -Kit)." }
        Copy-Item $from (Join-Path $plates "gal-$n.png") -Force
    }
}
if ($PlatesOnly) { Get-ChildItem $plates | Select-Object Name, Length | Format-Table -AutoSize; return }

$env:FILM_DOCS = Join-Path $root 'docs'
$inv = [Globalization.CultureInfo]::InvariantCulture
# Every part renders on its own (at its own pace, without fades) in this order; the film has its own, faster cut of them.
$partNames = 'modes', 'capture', 'editor', 'scroll', 'record', 'viewers', 'home', 'mascot', 'install', 'tray', 'updates', 'theme', 'pet', 'aio', 'pin',
             'history', 'sensitive', 'record-plus', 'docs-pro'

function Run([string]$arguments) {
    $p = Start-Process $exe -ArgumentList $arguments -PassThru -Wait
    if ($p.ExitCode -ne 0) { throw "film.exe failed: $arguments" }
}
function Ff([string[]]$a) {
    & $Ffmpeg -hide_banner -loglevel error -y @a
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed: $a" }
}

if ($Stills) {
    $dir = Join-Path $work 'stills'
    Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $dir | Out-Null
    $env:FILM_STILLS = '1'
    foreach ($l in $Lang) {
        Run "render `"$plates`" $l `"$dir\film.mp4`" `"$Ffmpeg`" $Fps 1"
        foreach ($x in $partNames) { Run "part `"$plates`" $l $x `"$dir\$x.mp4`" `"$Ffmpeg`" $Fps 1" }
    }
    Remove-Item Env:FILM_STILLS
    Write-Host "Stills in $dir"
    return
}

# Rendering is single-threaded per process, so the work is a queue of processes run $Jobs at a time: the film split
# into segments (joined afterwards without re-encoding) and each part; then the encodes that derive from them.
$log = Join-Path $work ('progress-' + (Get-Date -Format yyyyMMdd-HHmmss) + '.log')
function Log([string]$s) {
    $line = (Get-Date -Format HH:mm:ss) + ' ' + $s + [Environment]::NewLine
    for ($i = 0; $i -lt 20; $i++) { try { [IO.File]::AppendAllText($log, $line); return } catch { Start-Sleep -Milliseconds 150 } }
}
function Q([string]$s) { return '"' + $s + '"' }
$ffArgs = '-hide_banner -loglevel error -y '
$x264 = '-pix_fmt yuv420p -colorspace bt709 -color_primaries bt709 -color_trc bt709 -movflags +faststart'

# Runs every task (Exe, Args, Out, optional Range for FILM_RANGE) with at most $max at once; stops at the first failure.
function Pool($tasks, [int]$max, [string]$what) {
    $running = @(); $next = 0; $done = 0
    try {
        while ($next -lt $tasks.Count -or $running.Count -gt 0) {
            while ($running.Count -lt $max -and $next -lt $tasks.Count) {
                $t = $tasks[$next]
                if ($t.Range) { $env:FILM_RANGE = $t.Range }
                $p = Start-Process $t.Exe -ArgumentList $t.Args -PassThru -WindowStyle Hidden
                Remove-Item Env:FILM_RANGE -ErrorAction SilentlyContinue
                $running += @{ P = $p; T = $t }
                $next++
            }
            Start-Sleep -Milliseconds 500
            $still = @()
            foreach ($r in $running) {
                if (-not $r.P.HasExited) { $still += $r; continue }
                $r.P.WaitForExit()
                if ($r.P.ExitCode -ne 0 -or -not (Test-Path $r.T.Out)) { throw "Failed: $($r.T.Exe) $($r.T.Args) [$($r.T.Range)]" }
                $done++
                Log ("{0} {1}/{2} {3}" -f $what, $done, $tasks.Count, (Split-Path $r.T.Out -Leaf))
            }
            $running = $still
        }
    }
    catch {
        foreach ($r in $running) { if (-not $r.P.HasExited) { try { $r.P.Kill() } catch { } } }
        Log ("FAILED " + $_)
        throw
    }
}

Log 'start'
$render = @()
$films = @{}
foreach ($l in $Lang) {
    $kitLang = Join-Path $Kit $l
    New-Item -ItemType Directory -Force (Join-Path $kitLang 'parts'), (Join-Path $kitLang 'gif'), (Join-Path $kitLang 'segments'), (Join-Path $root "docs\clips\$l"), (Join-Path $root 'docs\video') | Out-Null
    $master = Join-Path $kitLang "stackshot-$l-4k.mp4"
    $env:FILM_INFO = '1'
    Run "render $(Q $plates) $l $(Q $master) $(Q $Ffmpeg) $Fps $Scale"
    Remove-Item Env:FILM_INFO
    $frames = [int](Get-Content "$master.frames.txt")
    $per = [int][Math]::Ceiling($frames / 8.0)
    $segs = @()
    for ($s = 0; $s * $per -lt $frames; $s++) {
        $seg = Join-Path $kitLang ("segments\film-{0:00}.mp4" -f $s)
        $segs += $seg
        $render += @{ Exe = $exe; Args = "render $(Q $plates) $l $(Q $seg) $(Q $Ffmpeg) $Fps $Scale"; Range = "$($s * $per):$([Math]::Min($frames, ($s + 1) * $per))"; Out = $seg }
    }
    $films[$l] = $segs
    $n = 1
    foreach ($x in $partNames) {
        $mp4 = Join-Path $kitLang ("parts\{0:00}-{1}.mp4" -f $n, $x)
        $render += @{ Exe = $exe; Args = "part $(Q $plates) $l $x $(Q $mp4) $(Q $Ffmpeg) $Fps $Scale"; Out = $mp4 }
        $n++
    }
}
Pool $render $Jobs 'render'

# Join the film, then the 1080p film.
$encode = @()
$parts = @()
foreach ($l in $Lang) {
    $kitLang = Join-Path $Kit $l
    $master = Join-Path $kitLang "stackshot-$l-4k.mp4"
    $list = Join-Path $kitLang 'segments\list.txt'
    Set-Content $list ($films[$l] | ForEach-Object { "file '" + $_.Replace('\', '/') + "'" }) -Encoding ASCII
    Ff @('-f', 'concat', '-safe', '0', '-i', $list, '-c', 'copy', '-movflags', '+faststart', $master)
    Remove-Item -Recurse -Force (Join-Path $kitLang 'segments'), "$master.frames.txt"
    Remove-Item "$master.scenes.txt"
    Log "joined $master"
    $hd = Join-Path $root "docs\video\stackshot-$l.mp4"
    $encode += @{ Exe = $Ffmpeg; Out = $hd; Args = $ffArgs + "-i $(Q $master) -vf scale=1920:1080:flags=lanczos -c:v libx264 -preset slow -crf 18 -threads 4 $x264 $(Q $hd)" }
    $n = 1
    foreach ($name in $partNames) {
        $parts += @{ Lang = $l; Name = $name; N = $n; Mp4 = (Join-Path $kitLang ("parts\{0:00}-{1}.mp4" -f $n, $name)) }
        $n++
    }
}
Pool $encode 3 'encode'
foreach ($l in $Lang) { Copy-Item (Join-Path $root "docs\video\stackshot-$l.mp4") (Join-Path (Join-Path $Kit $l) "stackshot-$l-1080p.mp4") -Force }

# README clips (animated WebP, 1280 px) and kit GIFs (1280 px) from every 4K part.
$clipsTasks = @()
foreach ($p in $parts) {
    $webp = Join-Path $root "docs\clips\$($p.Lang)\$($p.Name).webp"
    $gif = Join-Path $Kit ("{0}\gif\{1:00}-{2}.gif" -f $p.Lang, $p.N, $p.Name)
    $clipsTasks += @{ Exe = $Ffmpeg; Out = $webp; Args = $ffArgs + "-i $(Q $p.Mp4) -vf fps=24,scale=1280:-1:flags=lanczos -c:v libwebp_anim -quality 82 -compression_level 6 -loop 0 $(Q $webp)" }
    $clipsTasks += @{ Exe = $Ffmpeg; Out = $gif; Args = $ffArgs + "-i $(Q $p.Mp4) -vf $(Q 'fps=20,scale=1280:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=256:stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle') $(Q $gif)" }
}
Pool $clipsTasks 4 'clips'
Log 'finished'
Remove-Item Env:FILM_DOCS
