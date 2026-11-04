@echo off
chcp 65001 >nul
title DONG BO GIAO DIEN HDTOOL RIBBON TU PREVIEW STUDIO VAO REVIT
echo =====================================================================
echo    HDTOOL - DONG BO GIAO DIEN TU PREVIEW STUDIO VAO REVIT ADD-IN
echo =====================================================================
echo Dang doc cau hinh layout moi nhat va cap nhat ma nguon C# Source\App.cs...
powershell -ExecutionPolicy Bypass -File "%~dp0ApplyLayoutToRevit.ps1"
echo.
pause
