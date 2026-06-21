@echo off
chcp 65001 > nul
title 蘇打石器 GM 網頁前端
echo ================================
echo  蘇打石器 GM 前端  開發模式
echo  http://localhost:5173
echo ================================
cd /d "%~dp0"
npm run dev
pause
