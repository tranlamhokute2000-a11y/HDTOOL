Add-Type -AssemblyName presentationCore

$audioPath = Join-Path $PSScriptRoot "SepHoXongRoi.mp3"

# Tiếng chuông báo nhẹ
[System.Media.SystemSounds]::Asterisk.Play()
Start-Sleep -Milliseconds 300

$played = $false

# Cách 1: Dùng MediaPlayer của PresentationCore (hỗ trợ mp3 chuẩn nhất của .NET WPF, không phụ thuộc WMP COM)
try {
    $player = New-Object System.Windows.Media.MediaPlayer
    $player.Open([System.Uri](Resolve-Path $audioPath).Path)
    $player.Volume = 1.0
    $player.Play()
    Start-Sleep -Milliseconds 2600
    $player.Close()
    $played = $true
} catch {
}

# Cách 2: Fallback WMPlayer.OCX nếu MediaPlayer lỗi
if (-not $played) {
    try {
        $wmp = New-Object -ComObject WMPlayer.OCX
        $wmp.settings.volume = 100
        $wmp.URL = (Resolve-Path $audioPath).Path
        $wmp.controls.play()
        Start-Sleep -Milliseconds 2500
        $wmp.close()
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($wmp) | Out-Null
        $played = $true
    } catch {
    }
}

# Cách 3: Fallback Beep vui tươi
if (-not $played) {
    [console]::beep(587, 100)
    [console]::beep(659, 100)
    [console]::beep(880, 120)
    [console]::beep(1174, 250)
}
