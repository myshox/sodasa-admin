@echo off
chcp 65001 > nul
title 蘇打石器 GM 工具

echo.
echo  ====================================
echo   蘇打石器 GM 後台管理系統
echo  ====================================
echo.

set "WWWROOT=%~dp0..\web\WebApi\wwwroot"
set "WEBAPP=%~dp0..\web\WebApp"
set "NEED_BUILD=0"

if not exist "%WWWROOT%\index.html" set "NEED_BUILD=1"

if "%NEED_BUILD%"=="0" (
  for /f %%A in ('xcopy /L /D /Y "%WWWROOT%\index.html" "%WEBAPP%\src\main.tsx" 2^>nul ^| find "File"') do set "NEED_BUILD=1"
)

if "%NEED_BUILD%"=="1" (
  echo  [1/2] 前端有更新，正在建置...
  cd /d "%WEBAPP%"
  if not exist "node_modules" (
    echo  安裝依賴中...
    call npm install
    if %errorlevel% neq 0 ( echo npm install 失敗！ & pause & exit /b 1 )
  )
  call npm run build
  if %errorlevel% neq 0 ( echo 前端建置失敗！ & pause & exit /b 1 )
  xcopy /E /Y /I "%WEBAPP%\dist\*" "%WWWROOT%\" > nul
  echo  [1/2] 建置完成
) else (
  echo  [1/2] 前端無變更，略過建置
)

echo.

for /f "tokens=5" %%a in ('netstat -ano 2^>nul ^| findstr ":5050 " ^| findstr "LISTENING"') do (
  echo  釋放 Port 5050...
  taskkill /PID %%a /F > nul 2>&1
  timeout /t 1 /nobreak > nul
)

echo  [2/2] 啟動 API...
cd /d "%~dp0..\web\WebApi"
start "GM API" /min cmd /c "dotnet run --configuration Release"

echo  等待 API 啟動中...
:wait_loop
timeout /t 2 /nobreak > nul
curl -s http://localhost:5050 > nul 2>&1
if %errorlevel% neq 0 goto wait_loop

echo.
echo  啟動完成！開啟瀏覽器...
start "" "http://localhost:5050"
echo.
echo  GM 工具：http://localhost:5050
echo  關閉此視窗即停止伺服器
echo.
pause
