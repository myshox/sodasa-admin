# GM 網頁手機版檢查清單

> 最後更新：DEBUG 更新 — 加強按鈕可點擊（z-index、touch-target、safe-area）。全域樣式（`index.css`）已讓**所有按鈕**在手機上具至少 44×48px 觸控區。

## 一、全域覆蓋（無需逐頁改）

- **所有 `<button>`**：`@media (max-width: 767px)` 強制 `min-height: 48px`、`min-width: 44px`、`padding: 12px 16px`
- **所有 `input/select/textarea`**：`min-height: 48px`、字級 16px（防 iOS 縮放）
- **`[role="button"]`**：同上觸控區
- **側欄／drawer 連結**：`aside a`、`[data-drawer-nav] a` 加大可點區域
- **具 `data-suggestion-item` 的下拉項**：min-height 48px、padding 加大
- **`.table-wrap`、`.tbl-card`**：手機橫向捲動

因此**每一頁的按鈕**都會被這些規則覆蓋，不需逐頁改小 padding。

---

## 二、已個別調整的頁面／元件

| 頁面／元件 | 調整內容 |
|------------|----------|
| **Layout** | 手機 header 漢堡 48×48、充值連結觸控區、drawer 關閉鈕 48×48、`data-drawer-nav` |
| **PetCmdPage** | `useIsMobile`、Nud/GrowthNud 觸控區、CmdBar 直排與複製鈕、頁面 padding |
| **PlayerHubPage** | Tab 列 `tab-bar`、minHeight 48、橫向捲動 |
| **RecordsPage** | 同上 |
| **SystemPage** | 同上 |
| **AnalyticsPage** | 同上 |
| **ShopStatsPage** | `useIsMobile`、表格外包 `table-wrap` 橫向捲動、頁面 padding |
| **LoginPage** | `useIsMobile`、表單寬度 100%、padding、外層 padding |
| **MailPage** | 列表外包 `table-wrap`、列加 `data-suggestion-item`、minWidth 以利捲動 |
| **ItemAutocomplete** | 選項 `data-suggestion-item` |
| **PlayerAutocomplete** | 選項 `data-suggestion-item` |
| **AccountInput** | 選項 `data-suggestion-item` |

---

## 三、其餘頁面（依賴全域 + 既有 RWD）

以下頁面**未在本輪逐行改**，但：

- 所有按鈕已由全域 CSS 強制 48px 高／44px 寬
- 多數已有 `useIsMobile` 或既有 `overflowX: 'auto'` 處理表格

| 頁面 | 備註 |
|------|------|
| Dashboard | 已有 `useIsMobile`、grid 響應 |
| RechargePage | 已有 `useIsMobile`、grid 響應 |
| PlayersPage | 已有 `useIsMobile`；按鈕由全域覆蓋 |
| BanPage | 表格為 grid，可考慮加 `table-wrap` 若小螢幕過窄 |
| OnlinePage | 列表／按鈕由全域覆蓋 |
| MasterPage | 已有 `overflowX: 'auto'` |
| BatchPage / BatchOpsPage | 已有 `useIsMobile` 或 overflow |
| ItemSendPage / ItemSearchPage | 已有 overflow 或側欄布局 |
| SpeedBanPage | 已有 `useIsMobile` |
| ItemQueuePage | 按鈕由全域覆蓋 |
| PetRankPage | 已有 `overflowX: 'auto'` |
| ServerStatusPage | 按鈕由全域覆蓋；表格可橫捲 |
| DbBrowserPage / GuildPage | 固定側欄，小螢幕可能需未來改為堆疊 |
| PlayerHistoryPage | 已有 overflow |
| CostMilestonePage | 多處 grid，按鈕由全域覆蓋 |
| MarketPage / StreetShopPage | 已有 overflow 或布局 |
| VipPage / GmPermPage / GmAdminPage | 按鈕由全域覆蓋；表格多為 grid |
| GmLogPage / SqlQueryPage | 按鈕由全域覆蓋 |
| RechargeAnalyticsPage / TradeLogPage / GoldLogPage | 按鈕由全域覆蓋 |
| PlayerAnalyticsPage / TradeAuditPage | 按鈕由全域覆蓋 |
| RecyclePage / BackupPage | 按鈕由全域覆蓋 |
| BatchGoldPage | 按鈕由全域覆蓋 |

---

## 四、若仍遇到「按不到」的狀況

1. **確認是否為 `<button>`**  
   若為可點擊的 `<div>`，請加上 `role="button"`，即會套用手機觸控區樣式。

2. **表格／列表過寬**  
   外層包一層並加 `className="table-wrap"`，即可在手機橫向捲動。

3. **側欄頁（DbBrowser、Guild、ItemSearch、PlayerHistory、StreetShop）**  
   目前仍為固定寬側欄；若小螢幕上太擠，可再規劃改為「手機版上側欄收合或改為上下堆疊」。
