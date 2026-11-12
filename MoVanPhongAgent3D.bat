
@echo off
title Van phong Agent 3D - HadoMEE
cd /d "%~dp0office-3d"
echo [HadoMEE] Dang khoi dong may chu Van phong Agent 3D (Cong 5173 & 5174)...
start "" "http://localhost:5173"
npm run dev
