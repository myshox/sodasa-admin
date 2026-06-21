@echo off
chcp 65001 > nul
title 蘇打石器 GM API Server
echo ================================
echo  蘇打石器 GM API Server  Port 5050
echo ================================
cd /d "%~dp0"

REM 若 5050 已被佔用，先關閉該行程（避免「address already in use」）
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5050" ^| findstr "LISTENING"') do (
  echo 正在釋放 Port 5050（PID %%a）...
  taskkill /PID %%a /F 2>nul
  timeout /t 1 /nobreak >nul
)

dotnet run --configuration Release
pause
