# ==============================================================================
# HDTOOL - SCRIPT TU DONG DONG BO LAYOUT TU PREVIEW STUDIO VAO SOURCE/APP.CS & BUILD
# ==============================================================================
param(
    [string]$LayoutJsonPath = ""
)

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  DONG BO GIAO DIEN HDTOOL RIBBON TU PREVIEW STUDIO VAO REVIT" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$candidateFiles = @()

if ($LayoutJsonPath -and (Test-Path $LayoutJsonPath)) {
    $candidateFiles += (Get-Item $LayoutJsonPath)
}

# Kiem tra thu muc Downloads cua nguoi dung (tim file vua xuat tu Preview Studio)
$downloadsPath = Join-Path $env:USERPROFILE "Downloads"
if (Test-Path $downloadsPath) {
    $dlFiles = Get-ChildItem -Path $downloadsPath -Filter "hdtool_ribbon_layout*.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if ($dlFiles -and $dlFiles.Count -gt 0) {
        $candidateFiles += $dlFiles[0]
    }
}

if (Test-Path "$PSScriptRoot\hdtool_ribbon_layout.json") {
    $candidateFiles += (Get-Item "$PSScriptRoot\hdtool_ribbon_layout.json")
}
if (Test-Path "$PSScriptRoot\PreviewStudio\hdtool_ribbon_layout.json") {
    $candidateFiles += (Get-Item "$PSScriptRoot\PreviewStudio\hdtool_ribbon_layout.json")
}
if (Test-Path "$PSScriptRoot\current_ribbon_layout.json") {
    $candidateFiles += (Get-Item "$PSScriptRoot\current_ribbon_layout.json")
}
if (Test-Path "$PSScriptRoot\PreviewStudio\current_ribbon_layout.json") {
    $candidateFiles += (Get-Item "$PSScriptRoot\PreviewStudio\current_ribbon_layout.json")
}

if ($candidateFiles.Count -eq 0) {
    Write-Host "Khong tim thay file cau hinh layout (hdtool_ribbon_layout.json)!" -ForegroundColor Yellow
    Write-Host "Vui long mo Preview Studio va bam 'CHOT & XUAT CAU HINH' hoac 'AP DUNG NGAY VAO REVIT C# ADDIN'." -ForegroundColor Yellow
    exit 1
}

$chosenFile = $candidateFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host "==> 1. Phat hien cau hinh layout moi nhat: $($chosenFile.FullName) (Cap nhat: $($chosenFile.LastWriteTime))..." -ForegroundColor Green

if ($chosenFile.FullName -ne "$PSScriptRoot\hdtool_ribbon_layout.json") {
    Copy-Item -Path $chosenFile.FullName -Destination "$PSScriptRoot\hdtool_ribbon_layout.json" -Force
}

$rawJson = Get-Content -Path $chosenFile.FullName -Raw -Encoding UTF8
$layout = $rawJson | ConvertFrom-Json

if ($null -eq $layout.panels -or $layout.panels.Count -eq 0) {
    Write-Error "File JSON khong hop le hoac khong co panels nao!"
    exit 1
}

Write-Host "    - Phat hien $($layout.panels.Count) Panels tren thanh Ribbon HDTOOL." -ForegroundColor Green

$appCsPath = "$PSScriptRoot\Source\App.cs"
if (-not (Test-Path $appCsPath)) {
    Write-Error "Khong tim thay file Source\App.cs!"
    exit 1
}

Write-Host "==> 2. Sinh lai ma nguon Source\App.cs theo dung bo cuc Sep Ho da sap xep tren Preview Studio..." -ForegroundColor Green

$codeBlocks = @()
$pIdx = 1

foreach ($p in $layout.panels) {
    $pVar = "panel_$pIdx"
    $pName = $p.name
    $codeBlocks += "                // =================================================================="
    Write-Host "       [+] Panel $pIdx : $pName" -ForegroundColor Cyan
    $codeBlocks += "                // PANEL $pIdx : $($pName.ToUpper())"
    $codeBlocks += "                // =================================================================="
    $codeBlocks += "                RibbonPanel $pVar = GetOrCreateRibbonPanel(application, tabName, `"$pName`");"

    foreach ($btn in $p.buttons) {
        $btnId = $btn.id
        $btnName = $btn.name.Replace("`n", "\n")
        $cmdClass = if ($btn.commandClass) { $btn.commandClass } else { "HDTool.NewCommand" }
        $iconKey = if ($btn.icon) { $btn.icon } else { "HDToolMenu" }
        $tip = if ($btn.tooltip) { $btn.tooltip.Replace('"', '\"') } else { "[HDTOOL] $btnName" }

        if ($btn.type -eq "split") {
            $codeBlocks += ""
            $codeBlocks += "                // SplitButton: $($btn.name.Replace("`n", ' '))"
            $codeBlocks += "                SplitButtonData ${btnId}_Data = new SplitButtonData(`"$btnId`", `"$btnName`");"
            $codeBlocks += "                SplitButton ${btnId}_Btn = ${pVar}.AddItem(${btnId}_Data) as SplitButton;"
            $codeBlocks += "                if (${btnId}_Btn != null)"
            $codeBlocks += "                {"
            $codeBlocks += "                    ${btnId}_Btn.IsSynchronizedWithCurrentItem = true;"
            if ($btn.subButtons) {
                foreach ($sub in $btn.subButtons) {
                    $sId = $sub.id
                    $sName = $sub.name.Replace("`n", "\n")
                    $sCmd = if ($sub.commandClass) { $sub.commandClass } else { $cmdClass }
                    $sIcon = if ($sub.icon) { $sub.icon } else { $iconKey }
                    $codeBlocks += "                    PushButtonData ${sId}_Data = new PushButtonData(`"$sId`", `"$sName`", assemblyPath, `"$sCmd`");"
                    $codeBlocks += "                    ${sId}_Data.LargeImage = ResourceHelper.Create${sIcon}Icon(32);"
                    $codeBlocks += "                    ${sId}_Data.Image = ResourceHelper.Create${sIcon}Icon(16);"
                    $codeBlocks += "                    ${btnId}_Btn.AddPushButton(${sId}_Data);"
                }
            }
            $codeBlocks += "                }"
        }
        else {
            $codeBlocks += ""
            $codeBlocks += "                // PushButton: $($btn.name.Replace("`n", ' '))"
            $codeBlocks += "                PushButtonData ${btnId}_Data = new PushButtonData(`"$btnId`", `"$btnName`", assemblyPath, `"$cmdClass`");"
            $codeBlocks += "                ${btnId}_Data.ToolTip = `"$tip`";"
            $codeBlocks += "                ${btnId}_Data.LargeImage = ResourceHelper.Create${iconKey}Icon(32);"
            $codeBlocks += "                ${btnId}_Data.Image = ResourceHelper.Create${iconKey}Icon(16);"
            $codeBlocks += "                ${pVar}.AddItem(${btnId}_Data);"
        }
    }
    $codeBlocks += ""
    $pIdx++
}

$ribbonCodeText = $codeBlocks -join "`r`n"

$fullAppCs = @"
using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace HDTool
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                string tabName = "HDTOOL";

                // 1. Tao Ribbon Tab HDTOOL (neu chua co)
                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch
                {
                    // Tab co the da ton tai tu truoc
                }

                string assemblyPath = Assembly.GetExecutingAssembly().Location;

$ribbonCodeText

                // Dam bao CLR nhan dien Assembly dong cho Hot Reload
                ToolHotReloadManager.EnsureAssemblyResolve();

                // Dang ky tu dong kiem tra va can khop ty le Bang Khoi Luong khi kich hoat View/Sheet
                try
                {
                    application.ViewActivated += OnViewActivated;
                }
                catch { }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Loi Khoi Dong HDTOOL", ex.Message);
                return Result.Failed;
            }
        }

        private void OnViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            try
            {
                if (e == null || e.Document == null) return;
                Autodesk.Revit.DB.View curView = e.CurrentActiveView;
                if (curView != null && (curView is ViewSheet || curView is ViewDrafting))
                {
                    MepQuantityService.CheckAndRescaleAllTables(e.Document, curView);
                }
            }
            catch { }
        }

        private RibbonPanel GetOrCreateRibbonPanel(UIControlledApplication application, string tabName, string panelName)
        {
            List<RibbonPanel> panels = application.GetRibbonPanels(tabName);
            foreach (RibbonPanel p in panels)
            {
                if (p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return application.CreateRibbonPanel(tabName, panelName);
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
"@

Set-Content -Path $appCsPath -Value $fullAppCs -Encoding UTF8

Write-Host "==> 3. Tien hanh bien dich Build.ps1..." -ForegroundColor Green
& powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\Build.ps1"

if ($LASTEXITCODE -eq 0) {
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "  DONG BO THANH CONG 100%! GIAO DIEN REVIT DA DUOC CAP NHAT!" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    if (Test-Path "$PSScriptRoot\Audio\PlayVoice.ps1") {
        & powershell -ExecutionPolicy Bypass -File "$PSScriptRoot\Audio\PlayVoice.ps1"
    }
} else {
    Write-Host "Bien dich co loi, vui long kiem tra log." -ForegroundColor Red
}
