# Renders the Stackshot logo and icon with tools\Logo.cs (drawn in code, no design tools).
#   assets\logo-1024.png, logo-512.png   web/README logo (with margin and shadow)
#   assets\logo-256.png                  welcome window logo
#   assets\stackshot.ico                 .exe icon in every Windows size
#   -Preview <png>                       also a sheet with every size on light and dark backgrounds
param([string]$Preview)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $assets | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -Path @((Join-Path $PSScriptRoot 'Logo.cs'), (Join-Path $root 'src\Home\LogoArt.cs')) -ReferencedAssemblies System.Drawing

function Save([int]$size, [int]$style, [string]$name) {
    $b = [StackshotLogo]::Render($size, $style)
    $b.Save((Join-Path $assets $name), [Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
}
Save 1024 0 'logo-1024.png'
Save 512 0 'logo-512.png'
Save 256 1 'logo-256.png'

# ICO: small sizes use the simplified drawing (reads better in the tray) as 32-bit BMP; 256 is PNG, as Windows does.
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$entries = foreach ($s in $sizes) {
    $bmp = [StackshotLogo]::Render($s, $(if ($s -le 32) { 2 } else { 1 }))
    $ms = New-Object IO.MemoryStream
    if ($s -ge 256) { $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png) }
    else {
        $w = New-Object IO.BinaryWriter($ms)
        $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2)); $w.Write([int16]1); $w.Write([int16]32)
        $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
        for ($y = $s - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $s; $x++) { $c = $bmp.GetPixel($x, $y); $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A) } }
        $w.Write((New-Object byte[] ([int]([Math]::Ceiling($s / 32.0) * 4 * $s))))
    }
    $bmp.Dispose()
    ,$ms.ToArray()
}
$fs = [IO.File]::Create((Join-Path $assets 'stackshot.ico'))
$bw = New-Object IO.BinaryWriter($fs)
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $d = [byte]$(if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] })
    $bw.Write($d); $bw.Write($d); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]$entries[$i].Length); $bw.Write([int]$offset)
    $offset += $entries[$i].Length
}
foreach ($e in $entries) { $bw.Write($e) }
$bw.Close()

if ($Preview) {
    $sheet = New-Object Drawing.Bitmap(1180, 640)
    $g = [Drawing.Graphics]::FromImage($sheet)
    $g.InterpolationMode = 'NearestNeighbor'
    $halfW = 590
    $g.FillRectangle((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(242, 242, 247))), 0, 0, $halfW, 640)
    $g.FillRectangle((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(22, 22, 30))), $halfW, 0, $halfW, 640)
    foreach ($side in 0, 1) {
        $ox = $side * $halfW
        $big = [StackshotLogo]::Render(300, 0); $g.DrawImage($big, $ox + 20, 10, 300, 300); $big.Dispose()
        $icon = [StackshotLogo]::Render(220, 1); $g.DrawImage($icon, $ox + 340, 50, 220, 220); $icon.Dispose()
        $x = $ox + 20
        foreach ($s in 16, 20, 24, 32, 48, 64, 128) {
            $b = [StackshotLogo]::Render($s, $(if ($s -le 32) { 2 } else { 1 }))
            $g.DrawImage($b, $x, 340, $s, $s)
            $g.DrawImage($b, $x, 500, $s * 2, $s * 2)   # 2x, to inspect the pixels
            $b.Dispose()
            $x += [Math]::Max($s, 24) + 14
        }
    }
    $g.Dispose()
    $sheet.Save($Preview, [Drawing.Imaging.ImageFormat]::Png)
    $sheet.Dispose()
}
Write-Host "Logo listo en $assets"
