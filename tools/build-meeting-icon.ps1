param([string]$Source = "$PSScriptRoot/../meeting/Assets/company-logo.jpg", [string]$Output = "$PSScriptRoot/../meeting/Assets/company.ico")
Add-Type -AssemblyName PresentationCore, WindowsBase
$image = [System.Windows.Media.Imaging.BitmapImage]::new([Uri]::new((Resolve-Path -LiteralPath $Source).Path))
# company-logo.jpg is the unmodified image embedded in company-logo-source.pdf.
$mark = [System.Windows.Media.Imaging.CroppedBitmap]::new($image, [System.Windows.Int32Rect]::new(0,0,104,97))
$markEncoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
$markEncoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($mark))
$markStream = [System.IO.File]::Create([System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Output)), 'company-mark.png'))
try { $markEncoder.Save($markStream) } finally { $markStream.Dispose() }
# Remove the white matte only for the Windows icon, including pale edge pixels.
$pixels = [byte[]]::new(104 * 97 * 4)
$converted = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new($mark, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
$converted.CopyPixels($pixels, 104 * 4, 0)
for ($pixel = 0; $pixel -lt $pixels.Length; $pixel += 4) {
    $minimum = [Math]::Min($pixels[$pixel], [Math]::Min($pixels[$pixel+1], $pixels[$pixel+2]))
    $maximum = [Math]::Max($pixels[$pixel], [Math]::Max($pixels[$pixel+1], $pixels[$pixel+2]))
    $alpha = if ($maximum - $minimum -lt 18) { 0 } else { 255 - $minimum }
    for ($channel = 0; $channel -lt 3; $channel++) {
        $pixels[$pixel+$channel] = if ($alpha -eq 0) { 0 } else { [byte][Math]::Round(255.0 * ($pixels[$pixel+$channel] - $minimum) / $alpha) }
    }
    $pixels[$pixel+3] = [byte]$alpha
}
$mark = [System.Windows.Media.Imaging.BitmapSource]::Create(104,97,96,96,[System.Windows.Media.PixelFormats]::Bgra32,$null,$pixels,104*4)
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @()
foreach ($size in $sizes) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $draw = $visual.RenderOpen()
    $width = $size * 0.78
    $height = $width * 97 / 104
    $draw.DrawImage($mark, [System.Windows.Rect]::new(0,0,$width,$height))
    # A report-page badge distinguishes this tool from other company apps.
    $draw.PushTransform([System.Windows.Media.ScaleTransform]::new($size/100.0,$size/100.0))
    $ink=[System.Windows.Media.BrushConverter]::new().ConvertFromString('#28323E')
    $orange=[System.Windows.Media.BrushConverter]::new().ConvertFromString('#EF7710')
    $page=[System.Windows.Media.Geometry]::Parse('M 56,40 L 84,40 L 98,54 L 98,98 L 56,98 Z')
    $draw.DrawGeometry([System.Windows.Media.Brushes]::White,[System.Windows.Media.Pen]::new($ink,3),$page)
    $draw.DrawGeometry($null,[System.Windows.Media.Pen]::new($ink,3),[System.Windows.Media.Geometry]::Parse('M 84,41 L 84,54 L 97,54'))
    $draw.DrawRectangle($orange,$null,[System.Windows.Rect]::new(63,61,27,7))
    foreach($line in @(76,85)){$draw.DrawRectangle($ink,$null,[System.Windows.Rect]::new(63,$line,24,3))}
    $draw.Pop()
    $draw.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new(); $encoder.Save($stream)
    if ($size -eq 256) {
        [System.IO.File]::WriteAllBytes([System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Output)), 'company-app-icon.png'), $stream.ToArray())
    }
    $frames += ,$stream.ToArray(); $stream.Dispose()
}
$file = [System.IO.File]::Create([System.IO.Path]::GetFullPath($Output))
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index=0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32)
        $writer.Write([UInt32]$frames[$index].Length); $writer.Write([UInt32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $file.Dispose() }
