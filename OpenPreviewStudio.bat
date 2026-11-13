@echo off
setlocal
chcp 65001 >nul
set "HTML_PATH=%~dp0PreviewStudio\HDTool_PreviewStudio.html"
set "COCCOC=C:\Program Files\CocCoc\Browser\Application\browser.exe"

if exist "%COCCOC%" (
    start "" "%COCCOC%" "%HTML_PATH%"
) else (
    start "" "%HTML_PATH%"
)
endlocal
exit /b 0
