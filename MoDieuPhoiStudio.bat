@echo off
chcp 65001 >nul
title HDTOOL - Dieu Phoi Studio (Antigravity + Claude)
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo [LOI] Chua cai Node.js. Tai tai https://nodejs.org roi chay lai.
  pause
  exit /b 1
)
echo [HDTOOL] Dang mo Dieu Phoi Studio tai http://localhost:4870 ...
start "" "http://localhost:4870"
node ".agents\studio\server.mjs"
pause
