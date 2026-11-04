# HDTOOL PREVIEW STUDIO LAUNCHER
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$htmlPath = Join-Path $scriptDir "PreviewStudio\HDTool_PreviewStudio.html"

if (Test-Path $htmlPath) {
    Write-Host "Dang mo HDTOOL Preview Studio: $htmlPath" -ForegroundColor Cyan
    Start-Process $htmlPath
} else {
    Write-Host "Khong tim thay file Preview Studio tai: $htmlPath" -ForegroundColor Red
}
