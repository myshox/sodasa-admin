@echo off
chcp 65001 > nul
title 蘇打石器 GM API Server
echo ================================
echo  蘇打石器 GM API Server  Port 5050
echo ================================
cd /d "%~dp0"
dotnet run --configuration Release
pause
