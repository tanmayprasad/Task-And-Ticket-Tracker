Add-Type -AssemblyName System.Drawing

$img1Path = 'D:\One App\Tasks\TaskTra\Screenshot 2026-06-11 01030_2.png'
$img2Path = 'D:\One App\Tasks\TaskTra\Screenshot 2026-06-11 010339_1.png'
$outPath = 'D:\One App\Tasks\TaskTra\CombinedTheme.png'

$img1 = [System.Drawing.Image]::FromFile($img1Path)
$img2 = [System.Drawing.Image]::FromFile($img2Path)

$padding = 40
$textHeight = 80
$width = $img1.Width + $img2.Width + $padding
$height = [Math]::Max($img1.Height, $img2.Height) + $textHeight

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

$graphics.Clear([System.Drawing.Color]::White)

# Draw Images
$graphics.DrawImage($img1, 0, $textHeight, $img1.Width, $img1.Height)
$graphics.DrawImage($img2, ($img1.Width + $padding), $textHeight, $img2.Width, $img2.Height)

# Draw Text
$font = New-Object System.Drawing.Font('Segoe UI', 32, [System.Drawing.FontStyle]::Bold)
$brush = [System.Drawing.Brushes]::Black

$graphics.DrawString('Light Mode', $font, $brush, 20, 10)
$graphics.DrawString('Dark Mode', $font, $brush, ($img1.Width + $padding + 20), 10)

$bitmap.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)

$graphics.Dispose()
$bitmap.Dispose()
$img1.Dispose()
$img2.Dispose()

Write-Host "Success"
