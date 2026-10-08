# Original QuietMSI Q / airflow mark. Produces small-size PNG frames in a Windows ICO.
Add-Type -AssemblyName System.Drawing
$assetDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/QuietMSI/Assets'))
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size / 256.0, $size / 256.0)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(8,8,108,108,180,90)
    $path.AddArc(140,8,108,108,270,90)
    $path.AddArc(140,140,108,108,0,90)
    $path.AddArc(8,140,108,108,90,90)
    $path.CloseFigure()
    $background = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#171c24'))
    $g.FillPath($background,$path)
    $mint = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#62ddd0'),20)
    $mint.StartCap = $mint.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $g.DrawEllipse($mint,50,50,138,138)
    $mint.Width = 22
    $g.DrawLine($mint,166,166,207,207)
    if ($size -ge 24) {
        $air = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#f2fbfa'),10)
        $air.StartCap = $air.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($air,88,101,144,101)
        $g.DrawLine($air,88,121,134,121)
        $g.DrawLine($air,88,141,122,141)
        $air.Dispose()
    }
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@{ Size=$size; Bytes=$stream.ToArray() }
    if ($size -eq 256) { $bitmap.Save((Join-Path $assetDir 'QuietMSI.png'),[Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $path.Dispose(); $background.Dispose(); $mint.Dispose()
}
$stream = [IO.File]::Create((Join-Path $assetDir 'QuietMSI.ico'))
$writer = [IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
$writer.Dispose()