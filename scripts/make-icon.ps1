# Generates src/InternetIPTaskbar/assets/app.ico (a globe with meridians on a dark plate): PNG frames 16/32/48/256 inside an ICO.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $r = [single]($size * 0.18)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2; $s = [single]($size - 1)
    $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($s - $d, 0, $d, $d, 270, 90)
    $path.AddArc($s - $d, $s - $d, $d, $d, 0, 90); $path.AddArc(0, $s - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 41, 59))), $path)

    $m = [single]($size * 0.17); $w = [single]($size - 2 * $m)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 56, 189, 248)), ([single][Math]::Max(1, $size / 20))
    $g.DrawEllipse($pen, $m, $m, $w, $w)
    $g.DrawEllipse($pen, [single]($m + $w * 0.25), $m, [single]($w * 0.5), $w)   # meridian
    $g.DrawLine($pen, $m, [single]($size / 2), [single]($size - $m), [single]($size / 2))  # equator
    $g.Dispose()
    return $bmp
}

$sizes = 16, 32, 48, 256
$pngs = foreach ($sz in $sizes) {
    $bmp = New-Frame $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()

$target = Join-Path $PSScriptRoot '..\src\InternetIPTaskbar\assets\app.ico'
New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
[System.IO.File]::WriteAllBytes($target, $out.ToArray())
Write-Host "OK: $target ($($out.Length) bytes)"
