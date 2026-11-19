<#
  Genera l'icona originale di WinSCPREA: cartella e frecce bidirezionali.
  Disegno vettoriale con System.Drawing, senza immagini di terzi.
  Uso: .\IconGen.ps1 [-Out file.ico] [-Preview file.png]
#>
param([string]$Out, [string]$Preview)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
function Brush($color) { New-Object System.Drawing.SolidBrush $color }

function Draw([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $u = $size / 32.0

    $background = New-Object System.Drawing.Drawing2D.GraphicsPath
    $background.AddArc((1*$u), (1*$u), (14*$u), (14*$u), 180, 90)
    $background.AddArc((17*$u), (1*$u), (14*$u), (14*$u), 270, 90)
    $background.AddArc((17*$u), (17*$u), (14*$u), (14*$u), 0, 90)
    $background.AddArc((1*$u), (17*$u), (14*$u), (14*$u), 90, 90)
    $background.CloseFigure()
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($size, $size)), (C '#2B88D8'), (C '#0F4C9E')
    $g.FillPath($gradient, $background)

    $folder = New-Object System.Drawing.Drawing2D.GraphicsPath
    $folder.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(6.5*$u, 10*$u),
        [System.Drawing.PointF]::new(13.5*$u, 10*$u),
        [System.Drawing.PointF]::new(16*$u, 12.5*$u),
        [System.Drawing.PointF]::new(25*$u, 12.5*$u),
        [System.Drawing.PointF]::new(25*$u, 25*$u),
        [System.Drawing.PointF]::new(6.5*$u, 25*$u)))
    $folder.CloseFigure()
    $g.FillPath((Brush (C '#FFFFFF')), $folder)

    $rightArrow = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(9.5*$u, 13.5*$u),
        [System.Drawing.PointF]::new(18.5*$u, 13.5*$u),
        [System.Drawing.PointF]::new(18.5*$u, 11.5*$u),
        [System.Drawing.PointF]::new(23*$u, 15.5*$u),
        [System.Drawing.PointF]::new(18.5*$u, 19*$u),
        [System.Drawing.PointF]::new(18.5*$u, 17.5*$u),
        [System.Drawing.PointF]::new(9.5*$u, 17.5*$u))
    $g.FillPolygon((Brush (C '#0F4C9E')), $rightArrow)

    $leftArrow = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(22.5*$u, 19.5*$u),
        [System.Drawing.PointF]::new(13.5*$u, 19.5*$u),
        [System.Drawing.PointF]::new(13.5*$u, 18*$u),
        [System.Drawing.PointF]::new(9*$u, 22*$u),
        [System.Drawing.PointF]::new(13.5*$u, 25.5*$u),
        [System.Drawing.PointF]::new(13.5*$u, 23.5*$u),
        [System.Drawing.PointF]::new(22.5*$u, 23.5*$u))
    $g.FillPolygon((Brush (C '#E8912D')), $leftArrow)

    $g.Dispose()
    $gradient.Dispose()
    $background.Dispose()
    $folder.Dispose()
    return $bmp
}

if (-not $Out) { $Out = Join-Path $root 'app.ico' }
$outPath = [IO.Path]::GetFullPath($Out)

if ($Preview) {
    $previewPath = [IO.Path]::GetFullPath($Preview)
    $previewBitmap = Draw 256
    $previewBitmap.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $previewBitmap.Dispose()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($size in $sizes) {
    $bitmap = Draw $size
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    ,$stream.ToArray()
}

$file = [IO.File]::Create($outPath)
$writer = New-Object IO.BinaryWriter $file
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $data = $pngs[$i]
    $writer.Write([byte]($size % 256))
    $writer.Write([byte]($size % 256))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($data in $pngs) { $writer.Write([byte[]]$data) }
$writer.Close()
"Icona scritta: $outPath"
