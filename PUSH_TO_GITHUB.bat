@echo off
chcp 65001 >nul
title HDTOOL - PUSH LEN GITHUB

echo ============================================================
echo   HDTOOL: DAY TOAN BO TRI THUC & MA NGUON LEN GITHUB
echo   User: tranlamhokute2000-a11y
echo ============================================================
echo.

set /p REPO_URL="Nhap URL repository tren GitHub (mac dinh: https://github.com/tranlamhokute2000-a11y/HDTOOL.git): "
if "%REPO_URL%"=="" set REPO_URL=https://github.com/tranlamhokute2000-a11y/HDTOOL.git

echo.
echo Dang ket noi toi: %REPO_URL% ...
git remote remove origin 2>nul
git remote add origin %REPO_URL%
git branch -M main

echo.
echo Dang day du lieu len GitHub (git push -u origin main)...
git push -u origin main

if %ERRORLEVEL% equ 0 (
    echo.
    echo ============================================================
    echo   DA DAY THANH CONG TOAN BO TRI THUC LEN GITHUB!
    echo ============================================================
) else (
    echo.
    echo [!] Neu GitHub yeu cau dang nhap, Sep co the mo ung dung GITHUB DESKTOP
    echo     bam 'Add Local Repository' va bam 'Publish Repository' la xong ngay!
)

pause

