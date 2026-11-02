$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$crestImage = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'stu-official-crest.png'))
$wallpaperVariants = @(
    @{Source = 'black-red-generated.png'; Output = 'background-black-red-1920x1080.png'; Color = [System.Drawing.Color]::FromArgb(250, 247, 237)},
    @{Source = 'purple-gold-generated.png'; Output = 'background-purple-gold-1920x1080.png'; Color = [System.Drawing.Color]::FromArgb(233, 197, 82)}
)

try {
    foreach ($wallpaperVariant in $wallpaperVariants) {
        $generatedImage = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot $wallpaperVariant.Source))
        $wallpaperCanvas = [System.Drawing.Bitmap]::new(1920, 1080, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $wallpaperGraphics = [System.Drawing.Graphics]::FromImage($wallpaperCanvas)
        $crestAttributes = [System.Drawing.Imaging.ImageAttributes]::new()
        try {
            $wallpaperGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $wallpaperGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $wallpaperGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $wallpaperGraphics.DrawImage($generatedImage, [System.Drawing.Rectangle]::new(0, 0, 1920, 1080))

            # Use the authentic official transparent logo crop, preserving all contours.
            # The university's identity guide permits single-color logo treatments.
            $crestTint = [System.Drawing.Imaging.ColorMatrix]::new()
            $crestTint.Matrix00 = $wallpaperVariant.Color.R / 255.0
            $crestTint.Matrix11 = $wallpaperVariant.Color.G / 255.0
            $crestTint.Matrix22 = $wallpaperVariant.Color.B / 255.0
            $crestAttributes.SetColorMatrix($crestTint)
            $crestHeight = 280
            $crestWidth = [int][Math]::Round($crestHeight * $crestImage.Width / $crestImage.Height)
            $crestDestination = [System.Drawing.Rectangle]::new(1536 - [int]($crestWidth / 2), 285, $crestWidth, $crestHeight)
            $wallpaperGraphics.DrawImage($crestImage, $crestDestination, 0, 0, $crestImage.Width, $crestImage.Height, [System.Drawing.GraphicsUnit]::Pixel, $crestAttributes)

            $wallpaperCanvas.Save((Join-Path $PSScriptRoot $wallpaperVariant.Output), [System.Drawing.Imaging.ImageFormat]::Png)
            [PSCustomObject]@{
                File = $wallpaperVariant.Output
                Width = $wallpaperCanvas.Width
                Height = $wallpaperCanvas.Height
                Alpha = $wallpaperCanvas.GetPixel(960, 540).A
                GenerationWidth = $generatedImage.Width
                GenerationHeight = $generatedImage.Height
            }
        }
        finally {
            $crestAttributes.Dispose()
            $wallpaperGraphics.Dispose()
            $wallpaperCanvas.Dispose()
            $generatedImage.Dispose()
        }
    }
}
finally {
    $crestImage.Dispose()
}
