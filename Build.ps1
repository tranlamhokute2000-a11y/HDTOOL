# Build script for HDTOOL & Setup.exe with Versioning
param(
    [switch]$NoIncrement,
    [switch]$Increment, # Tương thích ngược
    [int]$SetVersion = 0
)

$ErrorActionPreference = 'Stop'

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$revitApi = "C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll"
$revitApiUI = "C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll"
$srcDir = "c:\Users\Nguye\Desktop\ADDIN REVIT"
$buildDir = Join-Path $srcDir "Build"
$installerDir = Join-Path $srcDir "Installer"
$desktop = "C:\Users\Nguye\Desktop"
$versionFile = Join-Path $srcDir "VERSION.txt"

# 1. Quản lý số phiên bản (Version 1, 2, 3...)
$versionNum = 1
if (Test-Path $versionFile) {
    $rawVer = (Get-Content $versionFile -Raw).Trim()
    if ($rawVer -match '(\d+)') {
        $versionNum = [int]$matches[1]
    }
}

if ($SetVersion -gt 0) {
    $versionNum = $SetVersion
    Write-Host "==> Thiet lap phien ban co dinh: Version $versionNum" -ForegroundColor Cyan
} elseif ($NoIncrement) {
    Write-Host "==> Giu nguyen phien ban: Version $versionNum" -ForegroundColor Yellow
} else {
    # Mac dinh theo yeu cau cua Sep Ho: Tu dong tang phien ban (+1) sau moi lan sua doi bo sung
    $oldVer = $versionNum
    $versionNum += 1
    Write-Host "==> TU DONG TANG PHIEN BAN: Version $oldVer -> Version $versionNum!" -ForegroundColor Cyan
}

Set-Content -Path $versionFile -Value "$versionNum" -Force

# Cap nhat so version vao hdtool_ribbon_layout.json
$jsonLayoutFile = Join-Path $srcDir "hdtool_ribbon_layout.json"
if (Test-Path $jsonLayoutFile) {
    try {
        $jsonContent = Get-Content $jsonLayoutFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $jsonContent.version = $versionNum
        $jsonContent | ConvertTo-Json -Depth 10 | Set-Content -Path $jsonLayoutFile -Encoding UTF8
    } catch { }
}

$versionTag = "Version $versionNum"
$versionFileName = "HDTOOL_Setup_Version_$versionNum.exe"
$versionZipName = "HDTOOL_Version_$versionNum.zip"

Write-Host "============================================================" -ForegroundColor Green
Write-Host "  DONG GOI BO CAI DAT 1-FILE: HDTOOL $versionTag" -ForegroundColor Green
Write-Host "  File cai dat se la: $versionFileName" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green

if (-not (Test-Path $buildDir)) { New-Item -ItemType Directory -Path $buildDir | Out-Null }

Write-Host "==> 1. Compiling HDTOOL.dll ($versionTag)..."
$sources = (Get-ChildItem -Path (Join-Path $srcDir "Source\*.cs") | ForEach-Object { '"{0}"' -f $_.FullName })
$dllOut = Join-Path $buildDir "HDTOOL.dll"
$logoRes = Join-Path $srcDir "Resources\hadomee_logo.png"
$ductCustomIcon = Join-Path $srcDir "Resources\cmdHDToolDuctQuantity_custom.png"

$args1 = @(
    "/target:library",
    "/codepage:65001",
    "/utf8output",
    "`"/out:$dllOut`"",
    "`"/r:$revitApi`"",
    "`"/r:$revitApiUI`"",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Xaml.dll",
    "/r:System.Xml.dll",
    "/r:System.IO.Compression.dll",
    "/r:System.IO.Compression.FileSystem.dll",
    "`"/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll`"",
    "`"/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll`"",
    "`"/resource:$logoRes,hadomee_logo.png`"",
    "`"/resource:$versionFile,version.txt`""
)
if (Test-Path $ductCustomIcon) {
    $args1 += "`"/resource:$ductCustomIcon,cmdHDToolDuctQuantity_custom.png`""
}
$args1 += $sources

$p1 = Start-Process -FilePath $csc -ArgumentList $args1 -NoNewWindow -Wait -PassThru
if ($p1.ExitCode -ne 0) {
    Write-Error "HDTOOL.dll compilation failed with exit code $($p1.ExitCode)!"
    exit 1
}
Write-Host "==> HDTOOL.dll ($versionTag) compiled successfully!" -ForegroundColor Green

Write-Host "==> 2. Compiling Standalone 1-File Installer: $versionFileName..."
$setupOut = Join-Path $srcDir "Setup.exe"
$setupCs = Join-Path $installerDir "Setup.cs"
$icon = Join-Path $installerDir "app.ico"
$addinFile = Join-Path $srcDir "HDTOOL.addin"
$installerLogo = Join-Path $installerDir "hadomee_logo.png"

$args2 = @(
    "/target:winexe",
    "/codepage:65001",
    "/utf8output",
    "`"/out:$setupOut`"",
    "`"/win32icon:$icon`"",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "`"/resource:$dllOut,HDTOOL.dll`"",
    "`"/resource:$addinFile,HDTOOL.addin`"",
    "`"/resource:$installerLogo,hadomee_logo.png`"",
    "`"/resource:$versionFile,version.txt`"",
    "`"$setupCs`""
)

$p2 = Start-Process -FilePath $csc -ArgumentList $args2 -NoNewWindow -Wait -PassThru
if ($p2.ExitCode -ne 0) {
    Write-Error "Setup.exe compilation failed with exit code $($p2.ExitCode)!"
    exit 1
}
Write-Host "==> Setup.exe ($versionTag) compiled successfully!" -ForegroundColor Green

# 3. Sao chép và đóng gói các phiên bản file cài đặt trong thư mục dự án (KHÔNG sinh ra Desktop)
Copy-Item $setupOut (Join-Path $srcDir $versionFileName) -Force
Copy-Item $setupOut (Join-Path $installerDir "Setup.exe") -Force
Copy-Item $setupOut (Join-Path $installerDir $versionFileName) -Force
Copy-Item $addinFile (Join-Path $buildDir "HDTOOL.addin") -Force

# Đảm bảo không để tệp nào rác sinh ra trên Desktop theo lệnh Sếp Hồ
$desktop = "C:\Users\Nguye\Desktop"
if (Test-Path $desktop) {
    Get-ChildItem -Path $desktop -File -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -like "HDTOOL_Setup*.exe" -or
        $_.Name -like "HDTOOL_Version*.zip" -or
        $_.Name -eq "HDTOOL.zip" -or
        $_.Name -eq "HDTOOL_Setup.exe" -or
        $_.Name -eq "Setup_HDTOOL_HadoMEE.exe" -or
        $_.Name -eq "DONG_BO_REVIT_TU_PREVIEW.bat"
    } | Remove-Item -Force -ErrorAction SilentlyContinue
}

# 4. Trien khai truc tiep vao Revit 2024 & Revit 2025 de Hot Reload ngay
$targetYears = @("2024", "2025", "2026", "2027")
$appDataAddins = Join-Path $env:APPDATA "Autodesk\Revit\Addins"

foreach ($yr in $targetYears) {
    $yearDir = Join-Path $appDataAddins $yr
    $revitProgDir = "C:\Program Files\Autodesk\Revit $yr"
    if ((Test-Path $yearDir) -or (Test-Path $revitProgDir)) {
        if (-not (Test-Path $yearDir)) { New-Item -ItemType Directory -Path $yearDir -Force | Out-Null }
        $hdtoolDir = Join-Path $yearDir "HDTOOL"
        if (-not (Test-Path $hdtoolDir)) { New-Item -ItemType Directory -Path $hdtoolDir -Force | Out-Null }
        $targetDll = Join-Path $hdtoolDir "HDTOOL.dll"
        try {
            Copy-Item $dllOut $targetDll -Force
        } catch {
            $backupDll = Join-Path $hdtoolDir ("HDTOOL_old_" + [System.Guid]::NewGuid().ToString().Substring(0, 8) + ".dll")
            Move-Item $targetDll $backupDll -Force -ErrorAction SilentlyContinue
            Copy-Item $dllOut $targetDll -Force
            Write-Host "==> File was locked by Revit $yr; rotated old file and updated HDTOOL.dll!"
        }
        Copy-Item $addinFile (Join-Path $yearDir "HDTOOL.addin") -Force
        Write-Host "==> Deployed to Revit $yr Addins successfully!" -ForegroundColor Green
    }
}

# 5. Đóng gói file nén Zip (chỉ lưu trong thư mục làm việc, KHÔNG lưu ra Desktop)
$zipVersionPath = Join-Path $srcDir $versionZipName
$zipPathRoot = Join-Path $srcDir "HDTOOL.zip"

$tempZipFolder = Join-Path $env:TEMP "HDTOOL_Package"
if (Test-Path $tempZipFolder) { Remove-Item $tempZipFolder -Recurse -Force }
New-Item -ItemType Directory -Path $tempZipFolder | Out-Null
Copy-Item $setupOut (Join-Path $tempZipFolder $versionFileName) -Force
Copy-Item $setupOut (Join-Path $tempZipFolder "Setup.exe") -Force
if (Test-Path (Join-Path $srcDir "HUONG_DAN_SU_DUNG.md")) {
    Copy-Item (Join-Path $srcDir "HUONG_DAN_SU_DUNG.md") (Join-Path $tempZipFolder "HUONG_DAN_SU_DUNG.md") -Force
}
if (Test-Path $zipVersionPath) { Remove-Item $zipVersionPath -Force }
if (Test-Path $zipPathRoot) { Remove-Item $zipPathRoot -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($tempZipFolder, $zipVersionPath)
Copy-Item $zipVersionPath $zipPathRoot -Force
Remove-Item $tempZipFolder -Recurse -Force
Write-Host "==> Repackaged $versionZipName in project folder!" -ForegroundColor Green

# 6. DỌN DẸP LƯU TRỮ: CHỈ GIỮ LẠI ĐÚNG 2 VERSION GẦN NHẤT, CÒN LẠI XÓA HẾT!
# THEO LỆNH SẾP HỒ: XÓA SẠCH MỌI FILE VIẾT TẮT 'v...' (HDTOOL_Setup_v*), CHỈ DÙNG ĐỊNH DẠNG 'Version'!
$keepVersions = @($versionNum, ($versionNum - 1))
Write-Host "==> Retention Policy: Chi giu lai 2 version gan nhat (Version $($versionNum - 1) va Version $versionNum)..." -ForegroundColor Cyan

$foldersToCheck = @($srcDir, $installerDir)
foreach ($dir in $foldersToCheck) {
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -File | ForEach-Object {
            $fname = $_.Name

            # Xóa triệt để các file dạng HDTOOL_Setup_v* hoặc HDTOOL_Setup.exe trùng lặp
            if ($fname -match '^HDTOOL_Setup_v\d+\.exe$' -or $fname -eq 'HDTOOL_Setup.exe') {
                Write-Host "    [-] Da xoa file trung lap khong dung dinh dang: $fname" -ForegroundColor DarkGray
                Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
                return
            }

            $fileVer = $null
            if ($fname -match '^HDTOOL_Setup_Version_(\d+)\.exe$') {
                $fileVer = [int]$matches[1]
            } elseif ($fname -match '^HDTOOL_Version_(\d+)\.zip$') {
                $fileVer = [int]$matches[1]
            }

            if ($null -ne $fileVer -and ($fileVer -notin $keepVersions)) {
                Write-Host "    [-] Da xoa file version cu: $fname" -ForegroundColor DarkGray
                Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

# 7. Kiểm tra danh sách file đã tạo trong thư mục dự án
Write-Host "`nDANH SACH FILE DA TAO TRONG THU MUC DU AN (0 file tren Desktop):" -ForegroundColor Green
Get-ChildItem -Path $srcDir -File | Where-Object { 
    $_.Name -like "HDTOOL_Setup_Version_*" -or 
    $_.Name -like "HDTOOL_Version_*" -or 
    $_.Name -eq "Setup.exe" -or 
    $_.Name -eq "HDTOOL.zip" 
} | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
