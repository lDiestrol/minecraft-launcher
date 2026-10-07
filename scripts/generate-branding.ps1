[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot 'src\Launcher.App\Assets\Launcher.ico'
}

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $scale = $size / 256.0
        $background = [System.Drawing.RectangleF]::new(16 * $scale, 16 * $scale, 224 * $scale, 224 * $scale)
        $radius = 52 * $scale
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $diameter = $radius * 2
            $path.AddArc($background.Left, $background.Top, $diameter, $diameter, 180, 90)
            $path.AddArc($background.Right - $diameter, $background.Top, $diameter, $diameter, 270, 90)
            $path.AddArc($background.Right - $diameter, $background.Bottom - $diameter, $diameter, $diameter, 0, 90)
            $path.AddArc($background.Left, $background.Bottom - $diameter, $diameter, $diameter, 90, 90)
            $path.CloseFigure()

            $backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#151a20'))
            $borderPen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#3d4854'), [Math]::Max(1, 8 * $scale))
            try {
                $graphics.FillPath($backgroundBrush, $path)
                $graphics.DrawPath($borderPen, $path)
            }
            finally {
                $backgroundBrush.Dispose()
                $borderPen.Dispose()
            }
        }
        finally {
            $path.Dispose()
        }

        $top = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(128 * $scale, 48 * $scale),
            [System.Drawing.PointF]::new(192 * $scale, 83 * $scale),
            [System.Drawing.PointF]::new(128 * $scale, 122 * $scale),
            [System.Drawing.PointF]::new(64 * $scale, 83 * $scale))
        $left = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(64 * $scale, 83 * $scale),
            [System.Drawing.PointF]::new(128 * $scale, 122 * $scale),
            [System.Drawing.PointF]::new(128 * $scale, 208 * $scale),
            [System.Drawing.PointF]::new(64 * $scale, 160 * $scale))
        $right = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(128 * $scale, 122 * $scale),
            [System.Drawing.PointF]::new(192 * $scale, 83 * $scale),
            [System.Drawing.PointF]::new(192 * $scale, 160 * $scale),
            [System.Drawing.PointF]::new(128 * $scale, 208 * $scale))
        $play = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(112 * $scale, 139 * $scale),
            [System.Drawing.PointF]::new(154 * $scale, 165 * $scale),
            [System.Drawing.PointF]::new(112 * $scale, 192 * $scale))

        $topBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#85e697'))
        $leftBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#4eb566'))
        $rightBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#60c875'))
        $playBrush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#101318'))
        try {
            $graphics.FillPolygon($topBrush, $top)
            $graphics.FillPolygon($leftBrush, $left)
            $graphics.FillPolygon($rightBrush, $right)
            if ($size -ge 24) {
                $graphics.FillPolygon($playBrush, $play)
            }
        }
        finally {
            $topBrush.Dispose()
            $leftBrush.Dispose()
            $rightBrush.Dispose()
            $playBrush.Dispose()
        }

        $stream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $images.Add($stream.ToArray())
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$file = [System.IO.FileStream]::new($OutputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $image = $images[$index]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Length)
        $writer.Write([uint32]$offset)
        $offset += $image.Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

$pngPath = [System.IO.Path]::ChangeExtension($OutputPath, '.png')
[System.IO.File]::WriteAllBytes($pngPath, $images[$images.Count - 1])

Write-Host "Generated multi-size Launcher icon: $OutputPath"
Write-Host "Generated Launcher UI image: $pngPath"
