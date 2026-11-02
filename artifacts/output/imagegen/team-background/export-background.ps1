param(
    [string]$SourcePath = (Join-Path $PSScriptRoot 'team-background-source.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$imageSource = [System.Drawing.Image]::FromFile($SourcePath)
try {
    $targetSize = [System.Drawing.Size]::new(1920, 1080)
    $opaqueCanvas = [System.Drawing.Bitmap]::new($targetSize.Width, $targetSize.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $opaqueGraphics = [System.Drawing.Graphics]::FromImage($opaqueCanvas)
    try {
        $opaqueGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $opaqueGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $opaqueGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $opaqueGraphics.Clear([System.Drawing.Color]::FromArgb(247, 250, 252))
        $opaqueGraphics.DrawImage($imageSource, [System.Drawing.Rectangle]::new(0, 0, $targetSize.Width, $targetSize.Height))
        $opaqueCanvas.Save((Join-Path $PSScriptRoot 'background-1920x1080.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $opaqueGraphics.Dispose()
    }

    $opacityMatrix = [System.Drawing.Imaging.ColorMatrix]::new()
    $opacityMatrix.Matrix33 = 0.20
    $opacityAttributes = [System.Drawing.Imaging.ImageAttributes]::new()
    $opacityAttributes.SetColorMatrix($opacityMatrix)
    try {
        $transparentCanvas = [System.Drawing.Bitmap]::new($targetSize.Width, $targetSize.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $transparentGraphics = [System.Drawing.Graphics]::FromImage($transparentCanvas)
        try {
            $transparentGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $transparentGraphics.DrawImage($opaqueCanvas, [System.Drawing.Rectangle]::new(0, 0, $targetSize.Width, $targetSize.Height), 0, 0, $targetSize.Width, $targetSize.Height, [System.Drawing.GraphicsUnit]::Pixel, $opacityAttributes)
            $transparentCanvas.Save((Join-Path $PSScriptRoot 'background-opacity20.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $transparentGraphics.Dispose()
            $transparentCanvas.Dispose()
        }

        $previewCanvas = [System.Drawing.Bitmap]::new($targetSize.Width, $targetSize.Height)
        $previewGraphics = [System.Drawing.Graphics]::FromImage($previewCanvas)
        try {
            $previewGraphics.Clear([System.Drawing.Color]::FromArgb(240, 240, 240))
            $previewGraphics.DrawImage($opaqueCanvas, [System.Drawing.Rectangle]::new(0, 0, $targetSize.Width, $targetSize.Height), 0, 0, $targetSize.Width, $targetSize.Height, [System.Drawing.GraphicsUnit]::Pixel, $opacityAttributes)
            $previewCanvas.Save((Join-Path $PSScriptRoot 'preview-opacity20.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $previewGraphics.Dispose()
            $previewCanvas.Dispose()
        }
    }
    finally {
        $opacityAttributes.Dispose()
        $opaqueCanvas.Dispose()
    }
}
finally {
    $imageSource.Dispose()
}

$exportFiles = @('background-1920x1080.png', 'background-opacity20.png', 'preview-opacity20.png')
foreach ($exportName in $exportFiles) {
    $exportPath = Join-Path $PSScriptRoot $exportName
    $exportedImage = [System.Drawing.Bitmap]::new($exportPath)
    try {
        [PSCustomObject]@{
            File = $exportName
            Width = $exportedImage.Width
            Height = $exportedImage.Height
            CenterAlpha = $exportedImage.GetPixel(960, 540).A
            Bytes = (Get-Item -LiteralPath $exportPath).Length
        }
    }
    finally {
        $exportedImage.Dispose()
    }
}
