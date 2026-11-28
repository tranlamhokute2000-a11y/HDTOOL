# =====================================================================
# TaoDuLieu.ps1 - Tao bo du lieu cho 1 ban ve moi de ChayLai.ps1 dung
# - Chep DWG vao thu muc tam (khong dung vao file goc), accoreconsole dump net -> lines.txt
# - Trich chu / block / van bang dung ham cua tool -> cache.txt
# - -KemNhatKyQuet: chep CadDuct_scan.log moi nhat (Sep vua quet ban ve nay trong Revit) -> chay lai dung net Revit doc
# Cach chay:  powershell -ExecutionPolicy Bypass -File .\.agents\tools\cad-duct-replay\TaoDuLieu.ps1 -Dwg "C:\...\ban ve.dwg" -Ten tenngan [-KemNhatKyQuet]
# =====================================================================
param([Parameter(Mandatory = $true)][string]$Dwg, [Parameter(Mandatory = $true)][string]$Ten, [switch]$KemNhatKyQuet)

$ErrorActionPreference = 'Stop'
$tool = $PSScriptRoot
$dir = Join-Path $tool "data\$Ten"
New-Item -ItemType Directory -Force $dir | Out-Null

$acc = @("C:\Program Files\Autodesk\AutoCAD 2027\accoreconsole.exe", "C:\Program Files\Autodesk\AutoCAD 2026\accoreconsole.exe",
         "C:\Program Files\Autodesk\AutoCAD 2025\accoreconsole.exe", "C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe",
         "C:\Program Files\Autodesk\AutoCAD 2021\accoreconsole.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $acc) { throw "Khong tim thay accoreconsole.exe" }

# Bien dich truoc de co HDTOOL_KiemTra.dll
powershell -ExecutionPolicy Bypass -File (Join-Path $tool "ChayLai.ps1") -ChiBienDich | Out-Null
$work = Join-Path $env:TEMP "HDTOOL_Replay"

# Ban sao DWG ten ASCII trong thu muc rieng
$tmp = Join-Path $work ("dwg_" + $Ten); New-Item -ItemType Directory -Force $tmp | Out-Null
$copy = Join-Path $tmp "banve.dwg"
Copy-Item $Dwg $copy -Force

# 1. Net ve -> lines.txt (dump.lsp ghi vao thu muc cua ban ve)
$scr = Join-Path $tmp "dump.scr"
"(setvar `"SECURELOAD`" 0)`r`n(load `"$((Join-Path $tool 'dump.lsp').Replace('\','/'))`")`r`n_.QUIT`r`n_Y`r`n" | Set-Content $scr -Encoding ascii
& $acc /i $copy /s $scr | Out-Null
Copy-Item (Join-Path $tmp "lines.txt") (Join-Path $dir "lines.txt") -Force

# 2. Chu / block / van -> cache.txt (dung ham cua tool)
$env:PATH = "C:\Program Files\Autodesk\Revit 2024;$env:PATH"
[void][Reflection.Assembly]::LoadFrom("C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll")
$asm = [Reflection.Assembly]::LoadFile((Join-Path $work "HDTOOL_KiemTra.dll"))
$m = $asm.GetType("HDTool.CadDwgExtractor").GetMethod("RunAcCoreExtraction", [Reflection.BindingFlags]"NonPublic,Static")
$r = $m.Invoke($null, [object[]]@([string]$acc, [string]$copy, [string](Join-Path $dir "cache.txt")))
Write-Host "Trich chu: $r"

if ($KemNhatKyQuet) {
  $log = Join-Path $env:LOCALAPPDATA "HDTOOL\Logs\CadDuct_scan.log"
  Write-Host ("Nhat ky quet: " + (Get-Content $log -TotalCount 2 -Encoding UTF8 | Select-Object -Last 1))
  Copy-Item $log (Join-Path $dir "CadDuct_scan.log") -Force
}
Write-Host "Xong: $dir  -> chay ChayLai.ps1 -Case $Ten, kiem tra ket-qua\mat-bang.png roi -LuuChuan"
