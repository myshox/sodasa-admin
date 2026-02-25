@echo off
chcp 65001 > nul
title 部署 GM 工具

echo.
echo  ====================================
echo   強制重新建置並部署
echo  ====================================
echo.

echo  [1/3] 建置前端...
cd /d "%~dp0WebApp"
if not exist "node_modules" (
  echo  安裝依賴中...
  call npm install
  if %errorlevel% neq 0 ( echo 失敗！ & pause & exit /b 1 )
)
call npm run build
if %errorlevel% neq 0 ( echo 前端建置失敗！ & pause & exit /b 1 )

echo.
echo  [2/3] 複製到 API wwwroot...
xcopy /E /Y /I "%~dp0WebApp\dist\*" "%~dp0WebApi\wwwroot\" > nul

echo.
echo  [3/3] 釋放 Port 5050 並啟動 API...
for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5050 " ^| findstr "LISTENING"') do (
  taskkill /PID %%a /F > nul 2>&1
  timeout /t 1 /nobreak > nul
)

cd /d "%~dp0WebApi"
start "GM API" /min cmd /c "dotnet run --configuration Release"

echo  等待 API 啟動中...
:wait_loop
timeout /t 2 /nobreak > nul
curl -s http://localhost:5050 > nul 2>&1
if %errorlevel% neq 0 goto wait_loop

echo.
echo  完成！開啟瀏覽器...
start "" "http://localhost:5050"
echo  http://localhost:5050
echo.
pause
