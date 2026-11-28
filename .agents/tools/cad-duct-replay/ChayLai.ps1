# =====================================================================
# ChayLai.ps1 - Chay lai nhan dien Dung Ong Gio Tu CAD ngoai Revit va so hoi quy
# - Bien dich HDTOOL tu Source (khong tang Version, khong deploy) + chuong trinh mo phong
# - Moi ban ve trong data\<ten>: co CadDuct_scan.log -> dung dung net Revit da doc; khong co -> dung lines.txt / cache.txt tu DWG
# - So data\<ten>\ket-qua\lienket.txt voi data\<ten>\chuan_lienket.txt (lien ket theo toa do)
# Cach chay:  powershell -ExecutionPolicy Bypass -File .\.agents\tools\cad-duct-replay\ChayLai.ps1
#             -Case tes2            chi chay 1 ban ve
#             -LuuChuan             chap nhan ket qua hien tai lam chuan moi (chi khi da kiem tra ky)
#             -ChiBienDich          chi bien dich (dung cho TaoDuLieu.ps1)
# =====================================================================
param([string[]]$Case, [switch]$LuuChuan, [switch]$ChiBienDich)

$ErrorActionPreference = 'Stop'
$tool = $PSScriptRoot
$root = (Resolve-Path (Join-Path $tool "..\..\..")).Path
$dataRoot = Join-Path $tool "data"
$rv = "C:\Program Files\Autodesk\Revit 2024"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$work = Join-Path $env:TEMP "HDTOOL_Replay"
New-Item -ItemType Directory -Force $work | Out-Null

# ---------- 1. Bien dich HDTOOL (giong KiemTra.ps1) ----------
Write-Host "[1] Bien dich HDTOOL tu Source" -ForegroundColor Cyan
$dll = Join-Path $work "HDTOOL_KiemTra.dll"
$refs = @(
  "/r:$rv\RevitAPI.dll", "/r:$rv\RevitAPIUI.dll",
  "/r:System.Drawing.dll", "/r:System.Windows.Forms.dll", "/r:System.Xaml.dll", "/r:System.Xml.dll",
  "/r:System.IO.Compression.dll", "/r:System.IO.Compression.FileSystem.dll",
  "/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll",
  "/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll")
$src = Get-ChildItem (Join-Path $root "Source\*.cs") | ForEach-Object { $_.FullName }
$res = & $csc /nologo /target:library /codepage:65001 "/out:$dll" @refs @src
$errs = @($res | Where-Object { $_ -match ': error ' })
if ($errs.Count -gt 0) { $errs | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }; exit 1 }

# ---------- 2. Ban sao trich chu DWG chay offline (tao lai tu CadDwgExtractor.cs hien tai) ----------
$ex = [IO.File]::ReadAllText((Join-Path $root "Source\CadDwgExtractor.cs"), [Text.Encoding]::UTF8)
function Replace-Once([string]$text, [string]$pattern, [string]$with, [string]$what) {
  $rx = New-Object Text.RegularExpressions.Regex($pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
  if (-not $rx.IsMatch($text)) { throw "Khong tao duoc ban trich chu offline: CadDwgExtractor.cs da doi o cho '$what' -> sua ChayLai.ps1" }
  return $rx.Replace($text, $with, 1)
}
$ex = Replace-Once $ex 'public static class CadDwgExtractor\b' 'public static class CadDwgExtractorOffline' 'ten lop'
$ex = Replace-Once $ex '(\n\s*)if \(doc == null \|\| cadLink == null\)\s*\{\s*diagnostic = .*?Transform totalXform = GetSymbolToRevitTransform\(cadLink\);' '$1string cacheFile = OfflineCache; string extractStatus = ""; string dwgPath = OfflineCache; Transform totalXform = Transform.Identity;' 'doc/cadLink'
$ex = Replace-Once $ex 'double unitToMm = ResolveCadUnitToMmFactor\(cadLink[^;]*;' 'double unitToMm = 1.0;' 'unitToMm'
$ex = Replace-Once $ex 'XYZ symbolOffsetFeet = ComputeSymbolOffsetFeet\(cadLink[^;]*;' 'XYZ symbolOffsetFeet = XYZ.Zero;' 'symbolOffsetFeet'
$ex = Replace-Once $ex '(public static class CadDwgExtractorOffline\s*\{)' '$1 public static string OfflineCache;' 'OfflineCache'
[IO.File]::WriteAllText((Join-Path $work "TrichChuOffline.cs"), $ex, (New-Object Text.UTF8Encoding $true))

# ---------- 3. Bien dich chuong trinh mo phong ----------
$exe = Join-Path $work "MoPhong.exe"
$res = & $csc /nologo /utf8output /target:exe /platform:x64 "/out:$exe" "/r:$dll" "/r:$rv\RevitAPI.dll" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll /r:System.dll `
  (Join-Path $tool "DocNet.cs") (Join-Path $tool "MoPhong.cs") (Join-Path $work "TrichChuOffline.cs")
$errs = @($res | Where-Object { $_ -match ': error ' })
if ($errs.Count -gt 0) { $errs | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }; exit 1 }
$shot = Join-Path $work "ChupForm.exe"
& $csc /nologo /utf8output /target:exe /platform:x64 "/out:$shot" /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $tool "ChupForm.cs") | Out-Null
Write-Host "  [OK] Bien dich xong: $work"
if ($ChiBienDich) { exit 0 }

# ---------- 4. Chay tung ban ve + so voi chuan ----------
if (-not $Case) { $Case = Get-ChildItem $dataRoot -Directory | ForEach-Object { $_.Name } }
$env:PATH = "$rv;$env:PATH"
$bad = 0
foreach ($c in $Case) {
  $dir = Join-Path $dataRoot $c
  if (-not (Test-Path (Join-Path $dir "lines.txt")) -or -not (Test-Path (Join-Path $dir "cache.txt"))) { Write-Host "[$c] thieu lines.txt / cache.txt trong data\$c (chay TaoDuLieu.ps1)" -ForegroundColor Red; $bad++; continue }
  $out = Join-Path $dir "ket-qua"; New-Item -ItemType Directory -Force $out | Out-Null
  $log = Join-Path $out "run.log"
  Push-Location $work
  cmd /c "`"$exe`" `"$dir`" auto $c > `"$log`" 2>&1"
  Pop-Location
  $lines = Get-Content $log -Encoding UTF8
  $err = $lines | Select-String 'Exception' | Select-Object -First 3
  if ($err) { Write-Host "[$c] LOI khi chay:" -ForegroundColor Red; $err | ForEach-Object { "  " + $_.Line }; $bad++; continue }
  $summary = ($lines | Select-String '^KET QUA:' | Select-Object -Last 1).Line
  $now = Join-Path $out "lienket.txt"; $std = Join-Path $dir "chuan_lienket.txt"
  if ($LuuChuan) { Copy-Item $now $std -Force; Write-Host "[$c] $summary -> DA LUU LAM CHUAN" -ForegroundColor Yellow; continue }
  if (-not (Test-Path $std)) { Write-Host "[$c] $summary (chua co chuan; chay lai voi -LuuChuan sau khi kiem tra)" -ForegroundColor Yellow; continue }
  $diff = Compare-Object (Get-Content $std -Encoding UTF8) (Get-Content $now -Encoding UTF8)
  if (-not $diff) { Write-Host "[$c] $summary | KHOP CHUAN" -ForegroundColor Green }
  else {
    $bad++
    Write-Host "[$c] $summary | KHAC CHUAN $(@($diff).Count) dong (<= chi co o chuan, => chi co bay gio):" -ForegroundColor Red
    $diff | Select-Object -First 40 | ForEach-Object { "  {0} {1}" -f $_.SideIndicator, $_.InputObject }
  }
  Write-Host "      chi tiet: $out (run.log, lienket.txt, mat-bang.png, xem-truoc-3d.png)"
}
if ($bad -gt 0) { exit 1 }
