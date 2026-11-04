@echo off
chcp 65001 >nul
title Cài Đặt Add-in HDTOOL Cho Autodesk Revit
color 0A
setlocal EnableDelayedExpansion

echo =======================================================================
echo              BỘ CÀI ĐẶT ADD-IN HDTOOL CHO AUTODESK REVIT
echo =======================================================================
echo.

set SCRIPT_DIR=%~dp0
set DLL_SOURCE=%SCRIPT_DIR%Build\HDTOOL.dll
set ADDIN_SOURCE=%SCRIPT_DIR%Build\HDTOOL.addin

if not exist "%DLL_SOURCE%" (
    set DLL_SOURCE=%SCRIPT_DIR%HDTOOL.dll
)
if not exist "%ADDIN_SOURCE%" (
    set ADDIN_SOURCE=%SCRIPT_DIR%HDTOOL.addin
)

if not exist "%DLL_SOURCE%" (
    color 0C
    echo [LOI] Khong tim thay file HDTOOL.dll tai: %DLL_SOURCE%
    echo Vui long chay file Build.bat truoc khi cai dat.
    pause
    exit /b 1
)

set ADDINS_ROOT=%APPDATA%\Autodesk\Revit\Addins
set COUNT=0

echo Dang kiem tra cac phien ban Revit tren may tinh...
echo.

for %%Y in (2020 2021 2022 2023 2024 2025 2026 2027) do (
    set "TARGET_DIR=%ADDINS_ROOT%\%%Y"
    set "FOUND=0"
    if exist "%ADDINS_ROOT%\%%Y" set "FOUND=1"
    if exist "C:\Program Files\Autodesk\Revit %%Y" set "FOUND=1"

    if "!FOUND!"=="1" (
        echo [+] Phat hien Revit %%Y -> Dang cai dat HDTOOL...
        if not exist "!TARGET_DIR!" mkdir "!TARGET_DIR!"
        if not exist "!TARGET_DIR!\HDTOOL" mkdir "!TARGET_DIR!\HDTOOL"
        
        copy /y "%DLL_SOURCE%" "!TARGET_DIR!\HDTOOL\HDTOOL.dll" >nul
        copy /y "%ADDIN_SOURCE%" "!TARGET_DIR!\HDTOOL.addin" >nul

        :: Don dep ban cai cu neu co
        if exist "!TARGET_DIR!\LevelCropOffset.addin" del /f /q "!TARGET_DIR!\LevelCropOffset.addin" >nul 2>&1
        if exist "!TARGET_DIR!\LevelCropOffset" rmdir /s /q "!TARGET_DIR!\LevelCropOffset" >nul 2>&1
        
        echo     - Da copy file HDTOOL thanh cong vao: !TARGET_DIR!
        set /a COUNT+=1
    )
)

:: Neu khong tim thay thu muc Addins san co, cai mac dinh cho 2024
if %COUNT% equ 0 (
    echo [+] Cai dat mac dinh vao thu muc Revit 2024...
    set "TARGET_DIR=%ADDINS_ROOT%\2024"
    if not exist "!TARGET_DIR!" mkdir "!TARGET_DIR!"
    if not exist "!TARGET_DIR!\HDTOOL" mkdir "!TARGET_DIR!\HDTOOL"
    copy /y "%DLL_SOURCE%" "!TARGET_DIR!\HDTOOL\HDTOOL.dll" >nul
    copy /y "%ADDIN_SOURCE%" "!TARGET_DIR!\HDTOOL.addin" >nul
    if exist "!TARGET_DIR!\LevelCropOffset.addin" del /f /q "!TARGET_DIR!\LevelCropOffset.addin" >nul 2>&1
    if exist "!TARGET_DIR!\LevelCropOffset" rmdir /s /q "!TARGET_DIR!\LevelCropOffset" >nul 2>&1
    set /a COUNT+=1
)

echo.
echo =======================================================================
echo              CÀI ĐẶT HDTOOL HOÀN TẤT THÀNH CÔNG!
echo =======================================================================
echo  - Add-in HDTOOL da duoc cai vao: %APPDATA%\Autodesk\Revit\Addins
echo  - Huong dan su dung trong Revit:
echo    1. Mo phan mem Autodesk Revit.
echo    2. Tren thanh Menu Ribbon, click vao Tab moi: 'HDTOOL'.
echo    3. Cac tinh nang san sang:
echo       * Căn Chỉnh Grids: Can luoi truc mat bang & sheet theo Crop
echo       * Căn Chỉnh Level: Can cao do mat cat & dung theo Crop
echo       * Menu HDTOOL: Bang dieu khien trung tam tuy chon khi can
echo       * Tự Động Căn: Tu dong mo theo loai View
echo =======================================================================
echo.
pause
