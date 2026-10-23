Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 128, 128
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Background rounded circle
$brushBg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 41, 59))
$g.FillEllipse($brushBg, 4, 4, 120, 120)

# Monitor frame
$penFrame = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 59, 130, 246)), 6
$brushScreen = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 15, 23, 42))
$g.FillRectangle($brushScreen, 24, 26, 80, 52)
$g.DrawRectangle($penFrame, 24, 26, 80, 52)

# Stand
$brushStand = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 148, 163, 184))
$g.FillRectangle($brushStand, 58, 78, 12, 16)
$g.FillRectangle($brushStand, 42, 94, 44, 8)

# Shield / Lock badge
$brushShield = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 16, 185, 129))
$g.FillEllipse($brushShield, 52, 42, 24, 24)

# Checkmark
$penCheck = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 3
$g.DrawLine($penCheck, 58, 54, 63, 59)
$g.DrawLine($penCheck, 63, 59, 70, 49)

$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$stream = [System.IO.File]::OpenWrite('Assets\app.ico')
$icon.Save($stream)
$stream.Close()
Write-Output "app.ico generated successfully"
