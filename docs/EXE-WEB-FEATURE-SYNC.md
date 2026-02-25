# EXE 與網頁版功能對照與同步說明

本文件用於保持 **EXE 桌面版** 與 **Web 網頁版** 功能一致，資料皆來自同一資料庫，操作結果雙向同步。

---

## 功能對照表

| EXE 表單 / 功能 | 網頁路由 | 狀態 | 備註 |
|-----------------|----------|------|------|
| 玩家管理（MainForm 主列表 + 詳情） | `/players` | ✅ 已對齊 | 搜尋、詳情、金幣/水晶/封號/禁言、**充值** |
| 主帳號查詢 | `/master` | ✅ 已對齊 | |
| 充值記錄 | `/recharge` | ✅ 已對齊 | 查詢 + 玩家詳情內「給予儲值」 |
| 交易記錄 | `/tradelog` | ✅ 已對齊 | |
| 金幣日誌 | `/goldlog` | ✅ 已對齊 | |
| 郵件記錄 | `/mail` | ✅ 已對齊 | |
| VIP 玩家管理 | `/vip` | ✅ 已對齊 | |
| 線上玩家 | `/online` | ✅ 已對齊 | |
| 封號管理 | `/ban` | ✅ 已對齊 | |
| GM 寵物指令 | `/petcmd` | ✅ 已對齊 | |
| 批量發送 | `/batch` | ✅ 已對齊 | |
| 角色回收桶 | `/recycle` | ✅ 已對齊 | |
| 道具給予 | `/itemqueue` | ✅ 已對齊 | |
| 批量金幣 | `/batchgold` | ✅ 已對齊 | |
| SQL 查詢 | `/sql` | ✅ 已對齊 | |
| 統計面板 | `/` (Dashboard) | ✅ 已對齊 | |
| **商城分析** | `/shopstats` | ✅ 已對齊 | 金幣/聲望/石壁/戰點商店熱賣與消費排行 |
| **儲值趨勢分析** | `/analytics/recharge` | ✅ 已對齊 | 今日/本月/累計、每日/月度、付費分層、首次付費 |
| **玩家活躍分析** | `/analytics/player` | ✅ 已對齊 | 登入時段、帳號成長、留存、沉睡玩家 |
| **交易稽核** | `/tradeaudit` | ✅ 已對齊 | 高頻配對、同IP、金幣異動、交易量排行 |
| **GM 權限管理** | `/gmperm` | ✅ 已對齊 | NeiCe / GroupId 查詢與設定 |
| **備份還原** | `/backup` | ⚠ 部分 | 網頁提供「下載備份」；還原建議用 EXE 或伺服器腳本 |
| GM 操作日誌 | `/gmlog` | ✅ 已對齊 | |
| 工具帳號 | `/gmadmin` | ✅ 已對齊 | |

---

## 資料同步說明

- **同一資料庫**：EXE 與 Web API 皆連線至同一 MySQL，故任一端的新增/修改/刪除都會即時反映在另一端。
- **業務邏輯一致**：儲值、金幣、封號、批量、道具等寫入邏輯已與 EXE 的 `DatabaseManager` 對齊（Web 使用 `DbService` 實作相同 SQL / 流程）。
- **新增功能時**：若在 EXE 新增功能，請同步在 Web 補上對應 API 與頁面，並更新本表。

---

## 開發注意

- 後端：`WebApi/Services/DbService.cs`、`WebApi/Controllers/`
- 前端：`WebApp/src/pages/`、`WebApp/src/strings.ts`
- EXE 參考：`Project/DatabaseManager.cs`、`Project/*Form.cs`
