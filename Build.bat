@echo off
chcp 65001 >nul
title Biên Dịch HDTOOL & Bộ Cài Đặt 1-File (HadoMEE)
color 0B

powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if %ERRORLEVEL% equ 0 (
    echo.
    echo =======================================================================
    echo          BIÊN DỊCH VÀ ĐÓNG GÓI 1-FILE VERSION HOÀN TẤT!
    echo =======================================================================
    echo.
) else (
    color 0C
    echo [LOI] Co loi trong qua trinh bien dich!
)
pause
