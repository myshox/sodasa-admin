import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import './index.css'
import LoginPage     from './pages/LoginPage'
import Layout        from './pages/Layout'
import MasterPage    from './pages/MasterPage'
import VipPage       from './pages/VipPage'
import PetCmdPage    from './pages/PetCmdPage'
import RecyclePage   from './pages/RecyclePage'
import SqlQueryPage  from './pages/SqlQueryPage'
import GmAdminPage   from './pages/GmAdminPage'
import BatchOpsPage  from './pages/BatchOpsPage'
import PlayerHistoryPage from './pages/PlayerHistoryPage'
// 整合頁面
import PlayerHubPage  from './pages/PlayerHubPage'
import MarketPage     from './pages/MarketPage'
import RecordsPage    from './pages/RecordsPage'
import AnalyticsPage  from './pages/AnalyticsPage'
import SystemPage     from './pages/SystemPage'
// 舊頁面（保留供直接連結使用）
import OnlinePage    from './pages/OnlinePage'
import BanPage       from './pages/BanPage'
import StreetShopPage from './pages/StreetShopPage'
import ItemSearchPage from './pages/ItemSearchPage'
import RechargePage  from './pages/RechargePage'
import TradeLogPage  from './pages/TradeLogPage'
import GoldLogPage   from './pages/GoldLogPage'
import MailPage      from './pages/MailPage'
import Dashboard     from './pages/Dashboard'
import ShopStatsPage from './pages/ShopStatsPage'
import PlayerAnalyticsPage  from './pages/PlayerAnalyticsPage'
import RechargeAnalyticsPage from './pages/RechargeAnalyticsPage'
import TradeAuditPage from './pages/TradeAuditPage'
import GmLogPage     from './pages/GmLogPage'
import GmPermPage    from './pages/GmPermPage'
import BackupPage    from './pages/BackupPage'
import ItemSendPage  from './pages/ItemSendPage'

const Guard = ({ children }: { children: React.ReactNode }) =>
  localStorage.getItem('gm_token') ? <>{children}</> : <Navigate to="/login" replace />

ReactDOM.createRoot(document.getElementById('root')!).render(
  <BrowserRouter>
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<Guard><Layout /></Guard>}>
        {/* 預設首頁 → 數據分析 */}
        <Route index element={<Navigate to="/analytics" replace />} />

        {/* ── 整合頁面 ── */}
        <Route path="players"   element={<PlayerHubPage />} />
        <Route path="market"    element={<MarketPage />} />
        <Route path="records"   element={<RecordsPage />} />
        <Route path="analytics" element={<AnalyticsPage />} />
        <Route path="system"    element={<SystemPage />} />

        {/* ── 未整合頁面 ── */}
        <Route path="master"    element={<MasterPage />} />
        <Route path="vip"       element={<VipPage />} />
        <Route path="history"   element={<PlayerHistoryPage />} />
        <Route path="batchops"  element={<BatchOpsPage />} />
        <Route path="petcmd"    element={<PetCmdPage />} />
        <Route path="recycle"   element={<RecyclePage />} />
        <Route path="sql"       element={<SqlQueryPage />} />
        <Route path="gmadmin"   element={<GmAdminPage />} />

        {/* ── 舊路由（向後相容） ── */}
        <Route path="online"     element={<OnlinePage />} />
        <Route path="ban"        element={<BanPage />} />
        <Route path="streetshop" element={<StreetShopPage />} />
        <Route path="itemsearch" element={<ItemSearchPage />} />
        <Route path="recharge"   element={<RechargePage />} />
        <Route path="tradelog"   element={<TradeLogPage />} />
        <Route path="goldlog"    element={<GoldLogPage />} />
        <Route path="mail"       element={<MailPage />} />
        <Route path="shopstats"  element={<ShopStatsPage />} />
        <Route path="tradeaudit" element={<TradeAuditPage />} />
        <Route path="gmlog"      element={<GmLogPage />} />
        <Route path="gmperm"     element={<GmPermPage />} />
        <Route path="backup"     element={<BackupPage />} />
        <Route path="analytics/player"   element={<PlayerAnalyticsPage />} />
        <Route path="analytics/recharge" element={<RechargeAnalyticsPage />} />
        <Route path="dashboard"  element={<Dashboard />} />
        <Route path="batch"      element={<BatchOpsPage />} />
        <Route path="batchgold"  element={<BatchOpsPage />} />
        <Route path="itemqueue"  element={<ItemSendPage />} />
        <Route path="send"       element={<ItemSendPage />} />
      </Route>
    </Routes>
  </BrowserRouter>
)
