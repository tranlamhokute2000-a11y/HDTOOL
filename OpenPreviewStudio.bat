@echo off
chcp 65001 >nul
title HDTOOL PREVIEW STUDIO - Trình Thiết Kế & Sắp Xếp Giao Diện Revit
echo =====================================================================
echo    HDTOOL PREVIEW STUDIO - AUTODESK REVIT RIBBON UI DESIGNER
echo =====================================================================
echo Đang mở Studio trên Trình duyệt để Sếp Hồ trực tiếp xem trước và sắp xếp...
start "" "%~dp0PreviewStudio\HDTool_PreviewStudio.html"
exit
