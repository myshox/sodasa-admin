# 蘇打石器 GM 工具

石器時代私服 GM 後台管理系統，包含桌面版（WinForms）與網頁版（React PWA + ASP.NET Core API）。
兩個版本功能對齊，並**共用同一個 MySQL 資料庫**（含統一的 GM 操作歷史紀錄表 `gm_operation_log`）。

## 專案結構（依用途分類）

```
SODAGMTOOL/
│
├─ 🖥️  EXE 桌面版（WinForms / .NET 6）
│   └── Project/              桌面版完整原始碼（GM 工具本體）
│   └── GMTool/               桌面版編譯後執行檔（不進版控，本機/分發用）
│
├─ 🌐 網頁版（前端 + 後端，部署於 gm.sodasa.org）
│   ├── WebApp/               前端 PWA（React 19 + TypeScript + Vite）
│   └── WebApi/               後端 REST API（ASP.NET Core 6，Port 5050）
│       └── wwwroot/          前端 build 後的產物（由 API 一併提供）
│
├─ 🚀 部署 / 工具
│   ├── update-server-ssh/    以 SSH 連線線上伺服器自動部署（git pull + publish + 重啟）
│   ├── update-server.ps1     線上部署輔助腳本（含密鑰，已 gitignore，不上傳）
│   ├── _run_update.ps1       本機：複製最新 EXE 到 GMTool/
│   ├── deploy.bat            本機：build 前端 + 跑 API（本地測試）
│   ├── gmtool.service        線上 systemd 服務定義（參考）
│   └── DbConnTest/           資料庫連線診斷小工具（選用，未進版控）
│
└─ 📄 其他：README.md、appsettings.example.json（範本）
```

> **網頁版 vs EXE 的對應**：兩邊操作（封禁、發物品、儲值、改金幣等）都會寫進同一張
> `gm_operation_log`，因此「歷史操作紀錄」在 EXE（系統 → 📋 GM 操作日誌）與
> 網頁（系統設定 → 📋 GM 日誌 / `/gmlog`）兩端看到的內容完全一致。

> **為什麼不把資料夾搬成「網頁版/」「EXE/」兩大層？**
> 因為線上伺服器部署寫死了 `/opt/gmtool/WebApi` 路徑，且各專案的 `.csproj`、
> 部署腳本都依賴現有目錄結構。搬動會直接讓部署失效，故以本分類表清楚標示用途即可。

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
