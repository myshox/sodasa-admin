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
# 編輯 appsettings.json，填入 MySQL 連線資訊與 GM 帳號
```

### 2. 部署網頁版

```bash
# 一鍵 build + 部署
deploy.bat
```

### 3. 開機自動啟動

```powershell
# 以管理員身份執行
powershell -File c:\SodaGM\setup-autostart.ps1
```

### 4. 訪問

- 本機：http://localhost:5050
- 手機/平板（同一 WiFi）：http://你的電腦IP:5050

## 技術棧

- **後端**：ASP.NET Core 6, MySqlConnector, JWT Auth
- **前端**：React 18, TypeScript, Vite, PWA
- **資料庫**：MySQL
