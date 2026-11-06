@echo off
chcp 65001 >nul
title VĂN PHÒNG AGENT REVIT 3D - HADOMEE
echo =====================================================================
echo    VĂN PHÒNG AGENT REVIT 3D - MÔ PHỎNG ĐỘI NGŨ THỜI GIAN THỰC
echo =====================================================================
echo Đang khởi động máy chủ SSE và giao diện 3D Three.js...

cd /d "%~dp0office-3d"
start "Server & Client 3D" cmd /c "npm run dev"

timeout /t 3 >nul
set "COCCOC=C:\Program Files\CocCoc\Browser\Application\browser.exe"
if exist "%COCCOC%" (
    start "" "%COCCOC%" "http://localhost:5173"
) else (
    start "" "http://localhost:5173"
)

exit

