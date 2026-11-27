# =====================================================================
# KiemTra.ps1 - Kiem tra nhanh HDTOOL truoc khi build
# - Bien dich thu ra thu muc TEMP (KHONG tang Version, KHONG deploy)
# - Quet cac loi cam (IntegerValue, emoji, mojibake, duong dan cung, Form khong ke thua HDToolBaseForm...) tren file .cs thay doi so voi main
# Cach chay:  powershell -ExecutionPolicy Bypass -File .\.agents\tools\KiemTra.ps1
#             them -All de quet toan bo Source
# =====================================================================
param([switch]$All)

$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $root
$fail = 0; $warn = 0
function Fail($m) { Write-Host "  [LOI]   $m" -ForegroundColor Red; $script:fail++ }
function Warn($m) { Write-Host "  [CHU Y] $m" -ForegroundColor Yellow; $script:warn++ }
function Ok($m)   { Write-Host "  [OK]    $m" -ForegroundColor Green }

Write-Host "=== HDTOOL KIEM TRA TU DONG ===" -ForegroundColor Cyan
$branch = (git branch --show-current) 2>$null
Write-Host ("Nhanh hien tai: " + $branch)

# ---------- 1. Bien dich thu (C# 5, .NET 4.8) ----------
Write-Host "`n[1] Bien dich thu (khong tang Version)" -ForegroundColor Cyan
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out = Join-Path $env:TEMP "HDTOOL_KiemTra.dll"
$refs = @(
  "/r:C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll", "/r:C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll",
  "/r:System.Drawing.dll", "/r:System.Windows.Forms.dll", "/r:System.Xaml.dll", "/r:System.Xml.dll",
  "/r:System.IO.Compression.dll", "/r:System.IO.Compression.FileSystem.dll",
  "/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll",
  "/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll")
$src = Get-ChildItem (Join-Path $root "Source\*.cs") | ForEach-Object { $_.FullName }
$res = & $csc /nologo /target:library /codepage:65001 /warn:4 "/out:$out" @refs @src
$errs = @($res | Where-Object { $_ -match ': error ' })
$wrns = @($res | Where-Object { $_ -match ': warning ' })
if ($errs.Count -gt 0) { $errs | ForEach-Object { Fail ($_ -replace '.*\\Source\\', '') } } else { Ok "0 loi bien dich" }
if ($wrns.Count -gt 0) { $wrns | ForEach-Object { Fail ("Canh bao: " + ($_ -replace '.*\\Source\\', '')) } } else { Ok "0 canh bao" }

# ---------- 2. Xac dinh file can quet ----------
Write-Host "`n[2] Quet quy tac cam" -ForegroundColor Cyan
if ($All) {
  $files = Get-ChildItem (Join-Path $root "Source\*.cs") | ForEach-Object { $_.FullName }
} else {
  $base = (git merge-base HEAD main) 2>$null
  $changed = @()
  if ($base) { $changed += (git diff --name-only $base) }
  $changed += (git diff --name-only)
  $changed += (git ls-files --others --exclude-standard)
  $files = $changed | Where-Object { $_ -like "Source/*.cs" } | Sort-Object -Unique | ForEach-Object { Join-Path $root $_ } | Where-Object { Test-Path $_ }
}
Write-Host ("So file .cs duoc quet: " + @($files).Count)

$emoji = '[\uD83C-\uD83E][\uDC00-\uDFFF]|[\u26A1\u23F3\u2705\u274C\u2B50\u2728\u2795\uFE0F]'
$moji  = '\u00E1\u00BA|\u00E1\u00BB|\u00C6\u00B0|\u00E2\u20AC|\u00F0\u0178'

foreach ($f in $files) {
  $name = Split-Path $f -Leaf
  $text = [IO.File]::ReadAllText($f, [Text.Encoding]::UTF8)
  $lines = $text -split "`r?`n"
  for ($i = 0; $i -lt $lines.Length; $i++) {
    $l = $lines[$i]; $at = "${name}:" + ($i + 1)
    if ($l -match '\.IntegerValue\b') { Fail "$at dung ElementId.IntegerValue (da bi bo tu Revit 2026) -> dung .Value" }
    if ($l -match $emoji) { Fail "$at co emoji (WinForms/TextNote hien o vuong)" }
    if ($l -match $moji) { Fail "$at chuoi bi loi ma hoa (mojibake)" }
    if ($l -match '(?i)c:\\\\?users\\\\?nguye') { Fail "$at duong dan cung toi may Sep Ho" }
    if ($l -match 'ActiveUIDocument\.Document\s*:\s*_doc|ActiveUIDocument\s*!=\s*null\s*\?\s*\w+\.ActiveUIDocument\.Document') { Fail "$at ghi vao ActiveUIDocument thay vi du an goc -> dung HDToolModelessHost.ResolveOwnerDocument(_doc)" }
    if ($l -match 'class\s+\w+\s*:\s*(System\.Windows\.Forms\.)?Form\b' -and $l -notmatch 'class\s+HDToolBaseForm\b') { Fail "$at Form khong ke thua HDToolBaseForm" }
    if ($l -match 'action\(null\)') { Fail "$at goi Revit API voi UIApplication null" }
    if ($l -match 'TextRenderer\.DrawText' -and ($l + $lines[[Math]::Min($i+1,$lines.Length-1)]) -notmatch 'NoPrefix') { Warn "$at TextRenderer.DrawText thieu TextFormatFlags.NoPrefix" }
    if ($l -match '\.HeaderText\s*=\s*[^;]*ToUpper') { Fail "$at doi HeaderText (MepQuantityInspector dung HeaderText lam ten Parameter)" }
    if ($l -match 'catch\s*\{\s*\}') { Warn "$at catch rong (nuot loi) - nen ghi log" }
  }
  if ($text -match '\.Show\(\)' -and $text -match 'HDToolBaseForm' -and $name -like '*Command.cs') { Warn "$name mo Form bang Show() khong owner -> dung HDToolModelessHost.ShowModeless" }
}

# ---------- 3. Tai lieu ----------
Write-Host "`n[3] Tai lieu dong bo" -ForegroundColor Cyan
$ver = (Get-Content (Join-Path $root "VERSION.txt") -Raw).Trim()
$skill = [IO.File]::ReadAllText((Join-Path $root "SKILL.md"), [Text.Encoding]::UTF8)
$state = [IO.File]::ReadAllText((Join-Path $root ".agents\SESSION_STATE.md"), [Text.Encoding]::UTF8)
if ($skill -match "Version $ver\b") { Ok "SKILL.md ghi Version $ver" } else { Warn "SKILL.md chua ghi Version $ver" }
if ($state -match "Version $ver\b") { Ok "SESSION_STATE.md ghi Version $ver" } else { Warn "SESSION_STATE.md chua ghi Version $ver" }
$s1 = [IO.File]::ReadAllText((Join-Path $root "SKILL.md"), [Text.Encoding]::UTF8)
$s2 = [IO.File]::ReadAllText((Join-Path $root ".agents\skills\hdtool\SKILL.md"), [Text.Encoding]::UTF8)
if ($s1 -eq $s2) { Ok "2 ban SKILL.md giong nhau" } else { Warn "SKILL.md va .agents/skills/hdtool/SKILL.md khac nhau" }

Write-Host ""
if ($fail -gt 0) {
  Write-Host "KET QUA: KHONG DAT ($fail loi, $warn chu y)" -ForegroundColor Red
  exit 1
} else {
  Write-Host "KET QUA: DAT ($warn chu y)" -ForegroundColor Green
  exit 0
}
