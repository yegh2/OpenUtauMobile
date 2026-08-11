Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

$root = Join-Path $env:USERPROFILE '.openclaw\workspace\OpenUtauMobile'
$src = Join-Path $root 'OpenUtauMobile.Android\Resources\drawable\Icon.png'
$srcImg = [System.Drawing.Image]::FromFile($src)

function New-Icon {
    param([string]$Out, [int]$Size)
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($srcImg, 0, 0, $Size, $Size)
    $g.Dispose()
    $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "  $Out ($Size x $Size)"
}

$iosDir = Join-Path $root 'OpenUtauMobile.iOS\Assets.xcassets\AppIcon.appiconset'
New-Item -ItemType Directory -Force -Path $iosDir | Out-Null

$sizes = @{
    'Icon-20.png'      = 20
    'Icon-20@2x.png'   = 40
    'Icon-20@3x.png'   = 60
    'Icon-29.png'      = 29
    'Icon-29@2x.png'   = 58
    'Icon-29@3x.png'   = 87
    'Icon-40.png'      = 40
    'Icon-40@2x.png'   = 80
    'Icon-40@3x.png'   = 120
    'Icon-60@2x.png'   = 120
    'Icon-60@3x.png'   = 180
    'Icon-76.png'      = 76
    'Icon-76@2x.png'   = 152
    'Icon-83.5@2x.png' = 167
    'Icon-1024.png'    = 1024
}
Write-Host '== iOS icons (from existing Icon.png) =='
foreach ($k in $sizes.Keys) { New-Icon -Out (Join-Path $iosDir $k) -Size $sizes[$k] }

$contentsJson = @'
{
  "images" : [
    { "size" : "20x20", "idiom" : "iphone", "filename" : "Icon-20@2x.png", "scale" : "2x" },
    { "size" : "20x20", "idiom" : "iphone", "filename" : "Icon-20@3x.png", "scale" : "3x" },
    { "size" : "29x29", "idiom" : "iphone", "filename" : "Icon-29@2x.png", "scale" : "2x" },
    { "size" : "29x29", "idiom" : "iphone", "filename" : "Icon-29@3x.png", "scale" : "3x" },
    { "size" : "40x40", "idiom" : "iphone", "filename" : "Icon-40@2x.png", "scale" : "2x" },
    { "size" : "40x40", "idiom" : "iphone", "filename" : "Icon-40@3x.png", "scale" : "3x" },
    { "size" : "60x60", "idiom" : "iphone", "filename" : "Icon-60@2x.png", "scale" : "2x" },
    { "size" : "60x60", "idiom" : "iphone", "filename" : "Icon-60@3x.png", "scale" : "3x" },
    { "size" : "20x20", "idiom" : "ipad", "filename" : "Icon-20.png", "scale" : "1x" },
    { "size" : "20x20", "idiom" : "ipad", "filename" : "Icon-20@2x.png", "scale" : "2x" },
    { "size" : "29x29", "idiom" : "ipad", "filename" : "Icon-29.png", "scale" : "1x" },
    { "size" : "29x29", "idiom" : "ipad", "filename" : "Icon-29@2x.png", "scale" : "2x" },
    { "size" : "40x40", "idiom" : "ipad", "filename" : "Icon-40.png", "scale" : "1x" },
    { "size" : "40x40", "idiom" : "ipad", "filename" : "Icon-40@2x.png", "scale" : "2x" },
    { "size" : "76x76", "idiom" : "ipad", "filename" : "Icon-76.png", "scale" : "1x" },
    { "size" : "76x76", "idiom" : "ipad", "filename" : "Icon-76@2x.png", "scale" : "2x" },
    { "size" : "83.5x83.5", "idiom" : "ipad", "filename" : "Icon-83.5@2x.png", "scale" : "2x" },
    { "size" : "1024x1024", "idiom" : "ios-marketing", "filename" : "Icon-1024.png", "scale" : "1x" }
  ],
  "info" : { "author" : "xcode", "version" : 1 }
}
'@
Set-Content -Path (Join-Path $iosDir 'Contents.json') -Value $contentsJson -Encoding UTF8
Set-Content -Path (Join-Path $root 'OpenUtauMobile.iOS\Assets.xcassets\Contents.json') -Value '{ "info" : { "author" : "xcode", "version" : 1 } }' -Encoding UTF8

$srcImg.Dispose()
Write-Host 'DONE'
