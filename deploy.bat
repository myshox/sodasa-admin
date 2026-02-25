@echo off
chcp 65001 > nul
title 部署網頁版 GM 工具
echo ================================
echo  建置前端...
echo ================================
cd /d "%~dp0WebApp"
call npm run build
if %errorlevel% neq 0 ( echo 前端建置失敗！ & pause & exit /b 1 )

echo.
echo ================================
echo  複製到 API wwwroot...
echo ================================
xcopy /E /Y /I "%~dp0WebApp\dist\*" "%~dp0WebApi\wwwroot\"
if %errorlevel% neq 0 ( echo 複製失敗！ & pause & exit /b 1 )

echo.
echo ================================
echo  啟動 API 伺服器（Port 5050）
echo ================================
cd /d "%~dp0WebApi"
start "GM API" dotnet run

echo.
echo  已啟動！請開啟瀏覽器：
echo  http://localhost:5050
echo.
pause
