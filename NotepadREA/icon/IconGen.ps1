<#
  Genera l'icona di NotepadREA (disegno vettoriale con System.Drawing, nessuna immagine di terzi).
  -Stile A|B|C   -Out file.ico (multi-risoluzione 16..256)   -Preview file.png
#>
param([ValidateSet('A','B','C')][string]$Stile = 'A', [string]$Out, [string]$Preview)
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
function Brush($c) { New-Object System.Drawing.SolidBrush $c }
function Bar($g, $c, [float]$x, [float]$y, [float]$w, [float]$h) { $p = RoundRect $x $y $w $h ($h / 2); $g.FillPath((Brush $c), $p) }

function Draw([int]$s, [string]$stile) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([System.Drawing.Color]::Transparent)
    $u = $s / 32.0   # unità: disegno su griglia 32x32
    $small = $s -le 24

    switch ($stile) {
        'A' {   # Pagina bianca con angolo piegato su fondo blu, righe di codice colorate
            $bg = RoundRect (1*$u) (1*$u) (30*$u) (30*$u) (7*$u)
            $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($s, $s)), (C '#2B88D8'), (C '#0F4C9E')
            $g.FillPath($lg, $bg)
            $page = New-Object System.Drawing.Drawing2D.GraphicsPath
            $page.AddLines([System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(8*$u, 5*$u), [System.Drawing.PointF]::new(19*$u, 5*$u), [System.Drawing.PointF]::new(25*$u, 11*$u),
                [System.Drawing.PointF]::new(25*$u, 27*$u), [System.Drawing.PointF]::new(8*$u, 27*$u))); $page.CloseFigure()
            $g.FillPath((Brush (C '#FFFFFF')), $page)
            $fold = New-Object System.Drawing.Drawing2D.GraphicsPath
            $fold.AddLines([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(19*$u, 5*$u), [System.Drawing.PointF]::new(19*$u, 11*$u), [System.Drawing.PointF]::new(25*$u, 11*$u))); $fold.CloseFigure()
            $g.FillPath((Brush (C '#BFD9F2')), $fold)
            $h = if ($small) { 2.2*$u } else { 1.8*$u }
            Bar $g (C '#0F4C9E') (10.5*$u) (13*$u) (7*$u) $h
            Bar $g (C '#E8912D') (12.5*$u) (16.5*$u) (8*$u) $h
            Bar $g (C '#3FA34D') (12.5*$u) (20*$u) (5.5*$u) $h
            if (-not $small) { Bar $g (C '#9AA7B5') (10.5*$u) (23.5*$u) (9*$u) $h }
        }
        'B' {   # Fondo scuro stile editor, numeri di riga, righe colorate e cursore
            $bg = RoundRect (1*$u) (1*$u) (30*$u) (30*$u) (7*$u)
            $g.FillPath((Brush (C '#272822')), $bg)
            $g.SetClip($bg); $g.FillRectangle((Brush (C '#3E3D32')), [float](1*$u), [float](1*$u), [float](6.5*$u), [float](30*$u)); $g.ResetClip()
            $h = if ($small) { 2.6*$u } else { 2*$u }
            $rows = @(@(7, '#F92672', 9), @(12, '#A6E22E', 13), @(17, '#66D9EF', 8), @(22, '#E6DB74', 11))
            foreach ($r in $rows) {
                if (-not $small) { Bar $g (C '#75715E') (3*$u) ($r[0]*$u) (2.5*$u) $h }
                Bar $g (C $r[1]) (10*$u) ($r[0]*$u) ($r[2]*$u) $h
            }
            $g.FillRectangle((Brush (C '#F8F8F0')), [float](20.5*$u), [float](16*$u), [float]([math]::Max(1, 1.4*$u)), [float](4*$u))
        }
        'C' {   # Foglio con matita: richiamo al blocco note classico, colori NotepadREA
            $bg = RoundRect (1*$u) (1*$u) (30*$u) (30*$u) (7*$u)
            $lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new(0, $s)), (C '#5B5FC7'), (C '#3A3D99')
            $g.FillPath($lg, $bg)
            $g.FillPath((Brush (C '#FFFFFF')), (RoundRect (7*$u) (5*$u) (16*$u) (22*$u) (2*$u)))
            $h = if ($small) { 2.2*$u } else { 1.6*$u }
            foreach ($y in 10, 14, 18) { Bar $g (C '#C9CBF0') (10*$u) ($y*$u) (10*$u) $h }
            if (-not $small) { Bar $g (C '#C9CBF0') (10*$u) (22*$u) (6*$u) $h }
            $st = $g.Save()
            $g.TranslateTransform([float](23*$u), [float](19*$u)); $g.RotateTransform(45)
            $pw = 3.6*$u; $pl = 13*$u
            $g.FillRectangle((Brush (C '#FFB900')), [float](-$pw/2), [float](-$pl/2), [float]$pw, [float]$pl)
            $g.FillRectangle((Brush (C '#E81123')), [float](-$pw/2), [float](-$pl/2), [float]$pw, [float](2.2*$u))
            $tip = New-Object System.Drawing.Drawing2D.GraphicsPath
            $tip.AddLines([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(-$pw/2, $pl/2), [System.Drawing.PointF]::new($pw/2, $pl/2), [System.Drawing.PointF]::new(0, $pl/2 + 3*$u))); $tip.CloseFigure()
            $g.FillPath((Brush (C '#F3D9A4')), $tip)
            $g.Restore($st)
        }
    }
    $g.Dispose(); return $bmp
}

if ($Preview) {
    $sizes = 256, 64, 48, 32, 16
    $pv = New-Object System.Drawing.Bitmap 530, 270
    $g = [System.Drawing.Graphics]::FromImage($pv); $g.Clear((C '#F3F3F3'))
    $g.FillRectangle((Brush (C '#1E1E1E')), 270, 135, 260, 135)
    $x = 280
    foreach ($sz in $sizes) {
        $b = Draw $sz $Stile
        if ($sz -eq 256) { $g.DrawImage($b, 7, 7, 256, 256) }
        else { $g.DrawImage($b, $x, 30, $sz, $sz); $g.DrawImage($b, $x, 160, $sz, $sz); $x += $sz + 12 }
        $b.Dispose()
    }
    $pv.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $pv.Dispose()
}

if ($Out) {
    # ICO con immagini PNG incorporate (supportato da Windows Vista in poi)
    $sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
    $pngs = foreach ($sz in $sizes) { $b = Draw $sz $Stile; $ms = New-Object IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); ,$ms.ToArray() }
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
    "Icona scritta: $Out"
}
