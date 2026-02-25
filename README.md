# 蘇打石器 GM 工具

石器時代私服 GM 後台管理系統，包含桌面版（WinForms）與網頁版（React PWA + ASP.NET Core API）。

## 專案結構

```
SODAGMTOOL/
├── Project/      WinForms 桌面版（.NET 6）
├── WebApi/       後端 REST API（ASP.NET Core 6，Port 5050）
└── WebApp/       前端 PWA（React + TypeScript + Vite）
```

## 快速開始

### 1. 設定資料庫連線

```bash
cp WebApi/appsettings.example.json WebApi/appsettings.json
# 編輯 appsettings.json，填入 MySQL 連線資訊與 GM 帳號（GmAccounts）
```

### 2. 網頁版使用方式（功能與 EXE 對齊）

**一定要先啟動後端，否則網頁只有外觀、所有功能都無法用。**

1. **啟動後端 API**（Port 5050）  
   - 執行 `WebApi/start-api.bat` 或  
   - `cd WebApi` 後 `dotnet run`
2. **啟動網頁**  
   - 執行 `WebApp/start-web.bat` 或  
   - `cd WebApp` 後 `npm install` 再 `npm run dev`
3. 瀏覽器打開 **http://localhost:5173**，用 appsettings 裡的 GM 帳號登入。
4. **發送金幣**：左側「玩家管理」→ 搜尋並點選玩家 → 右側可設定金幣／水晶；或「批量金幣」對多人加減。  
5. **發送道具**：左側「道具給予」→ 搜尋玩家 → 輸入道具編號與數量 → 發送（玩家從信箱領取）。

### 3. 部署網頁版（例如部署到 Git / 任意主機）

```bash
# 一鍵 build + 部署（會把前端 build 到 WebApi/wwwroot，由 API 一起提供）
deploy.bat
```

若前後端分開部署（例如前端放 GitHub Pages、API 放另一台主機）：

```bash
cd WebApp
# 建置時指定後端網址（請改成你的 API 網址）
set VITE_API_URL=https://你的API網址
npm run build
# 將 dist 內容上傳到靜態空間即可
```

### 4. 開機自動啟動

```powershell
# 以管理員身份執行
powershell -File c:\SodaGM\setup-autostart.ps1
```

### 5. 訪問

- 本機：http://localhost:5050（若用 deploy 則前端與 API 同埠）
- 開發時前端：http://localhost:5173（API 需另開 5050）
- 手機/平板（同一 WiFi）：http://你的電腦IP:5050

## 技術棧

- **後端**：ASP.NET Core 6, MySqlConnector, JWT Auth
- **前端**：React 18, TypeScript, Vite, PWA
- **資料庫**：MySQL
