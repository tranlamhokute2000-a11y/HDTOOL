
@echo off
chcp 65001 >nul
title HDTOOL PREVIEW STUDIO - Trình Thiết Kế & Sắp Xếp Giao Diện Revit (Windows 11)

echo =====================================================================
echo    HDTOOL PREVIEW STUDIO - AUTODESK REVIT RIBBON UI DESIGNER (WIN 11)
echo =====================================================================
echo Đang mở Studio trên Trình duyệt Cốc Cốc để Sếp Hồ xem trước và sắp xếp...

set "HTML_PATH=%~dp0PreviewStudio\HDTool_PreviewStudio.html"
set "COCCOC=C:\Program Files\CocCoc\Browser\Application\browser.exe"

if exist "%COCCOC%" (
    start "" "%COCCOC%" "%HTML_PATH%"
) else (
    start "" "%HTML_PATH%"
)

exit
