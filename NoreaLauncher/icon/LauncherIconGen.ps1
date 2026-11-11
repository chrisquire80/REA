<#
  Genera l'icona vettoriale di NOREA Launcher (ispirata al prisma geometrico di ArtCraft Launcher).
#>
param([string]$Out = (Join-Path $PSScriptRoot '..\app.ico'))
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}
function C([string]$hex, [int]$a = 255) { $c = [System.Drawing.ColorTranslator]::FromHtml($hex); [System.Drawing.Color]::FromArgb($a, $c) }

function Draw([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)
    $u = $s / 32.0

    # Sfondo dark carbon con bordo sottile
    $bg = RoundRect (1*$u) (1*$u) (30*$u) (30*$u) (6*$u)
    $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($s, $s)), (C '#161822'), (C '#0B0C10')
    $g.FillPath($lg, $bg)
    $pen = New-Object System.Drawing.Pen (C '#2D3348'), ([math]::Max(1.0, 1.2*$u))
    $g.DrawPath($pen, $bg)

    # Monogramma geometrico "N" / Prisma (blu elettrico + accento rosso corallo NOREA)
    $left = New-Object System.Drawing.Drawing2D.GraphicsPath
    $left.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(7.5*$u, 24.5*$u),
        [System.Drawing.PointF]::new(11.5*$u, 7.5*$u),
        [System.Drawing.PointF]::new(16.0*$u, 7.5*$u),
        [System.Drawing.PointF]::new(12.0*$u, 24.5*$u)
    )); $left.CloseFigure()
    $b1 = New-Object System.Drawing.SolidBrush (C '#2B7CD3')
    $g.FillPath($b1, $left)

    $diag = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diag.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(12.5*$u, 7.5*$u),
        [System.Drawing.PointF]::new(16.5*$u, 7.5*$u),
        [System.Drawing.PointF]::new(22.5*$u, 24.5*$u),
        [System.Drawing.PointF]::new(18.5*$u, 24.5*$u)
    )); $diag.CloseFigure()
    $b2 = New-Object System.Drawing.SolidBrush (C '#4DA3FF')
    $g.FillPath($b2, $diag)

    $right = New-Object System.Drawing.Drawing2D.GraphicsPath
    $right.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(19.5*$u, 24.5*$u),
        [System.Drawing.PointF]::new(22.5*$u, 11.5*$u),
        [System.Drawing.PointF]::new(25.5*$u, 11.5*$u),
        [System.Drawing.PointF]::new(23.0*$u, 24.5*$u)
    )); $right.CloseFigure()
    $b3 = New-Object System.Drawing.SolidBrush (C '#E05252')
    $g.FillPath($b3, $right)

    $g.Dispose(); return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($sz in $sizes) { $b = Draw $sz; $ms = New-Object IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); ,$ms.ToArray() }
$fs = [IO.File]::Create($Out); $w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $d = $pngs[$i]
    $w.Write([byte]($sz % 256)); $w.Write([byte]($sz % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$d.Length); $w.Write([uint32]$offset)
    $offset += $d.Length
}
foreach ($d in $pngs) { $w.Write([byte[]]$d) }
$w.Close()
"Icona generata: $Out"
