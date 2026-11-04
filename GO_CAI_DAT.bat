@echo off
chcp 65001 >nul
title Gỡ Cài Đặt Add-in HDTOOL (Revit)
color 0C
setlocal EnableDelayedExpansion

echo =======================================================================
echo          GỠ CÀI ĐẶT ADD-IN HDTOOL CHO AUTODESK REVIT
echo =======================================================================
echo.

set ADDINS_ROOT=%APPDATA%\Autodesk\Revit\Addins

echo Dang go bo Add-in khoi cac phien ban Revit...
echo.

for %%Y in (2020 2021 2022 2023 2024 2025 2026 2027) do (
    set "TARGET_DIR=%ADDINS_ROOT%\%%Y"
    if exist "!TARGET_DIR!\HDTOOL.addin" (
        del /f /q "!TARGET_DIR!\HDTOOL.addin" 2>nul
        echo [-] Da xoa: !TARGET_DIR!\HDTOOL.addin
    )
    if exist "!TARGET_DIR!\HDTOOL" (
        rmdir /s /q "!TARGET_DIR!\HDTOOL" 2>nul
        echo [-] Da xoa thu muc: !TARGET_DIR!\HDTOOL
    )
    if exist "!TARGET_DIR!\LevelCropOffset.addin" (
        del /f /q "!TARGET_DIR!\LevelCropOffset.addin" 2>nul
    )
    if exist "!TARGET_DIR!\LevelCropOffset" (
        rmdir /s /q "!TARGET_DIR!\LevelCropOffset" 2>nul
    )
)

echo.
echo =======================================================================
echo       ĐÃ GỠ BỎ ADD-IN HDTOOL THÀNH CÔNG KHỎI CÁC PHIÊN BẢN REVIT!
echo =======================================================================
echo.
pause
