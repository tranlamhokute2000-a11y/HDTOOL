@echo off
chcp 65001 >nul
title Tăng Phiên Bản HDTOOL (Version +1) và Đóng Gói
color 0A

powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -Increment
if %ERRORLEVEL% equ 0 (
    echo.
    echo =======================================================================
    echo          ĐÃ TĂNG PHIÊN BẢN VÀ ĐÓNG GÓI THÀNH CÔNG!
    echo =======================================================================
    echo.
) else (
    color 0C
    echo [LOI] Co loi trong qua trinh dong goi!
)
pause

