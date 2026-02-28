import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import './index.css'
import LoginPage     from './pages/LoginPage'
import Layout       from './pages/Layout'
import Dashboard    from './pages/Dashboard'
import PlayersPage  from './pages/PlayersPage'
import MasterPage   from './pages/MasterPage'
import OnlinePage   from './pages/OnlinePage'
import BanPage      from './pages/BanPage'
import RechargePage from './pages/RechargePage'
import GoldLogPage  from './pages/GoldLogPage'
import MailPage     from './pages/MailPage'
import BatchPage    from './pages/BatchPage'
import PetCmdPage   from './pages/PetCmdPage'
import GmLogPage    from './pages/GmLogPage'
import TradeLogPage      from './pages/TradeLogPage'
import PlayerHistoryPage from './pages/PlayerHistoryPage'
import StreetShopPage    from './pages/StreetShopPage'
import VipPage      from './pages/VipPage'
import RecyclePage  from './pages/RecyclePage'
import BatchGoldPage from './pages/BatchGoldPage'
import SqlQueryPage from './pages/SqlQueryPage'
import GmAdminPage   from './pages/GmAdminPage'
import ItemSendPage  from './pages/ItemSendPage'
import ShopStatsPage from './pages/ShopStatsPage'
import RechargeAnalyticsPage from './pages/RechargeAnalyticsPage'
import PlayerAnalyticsPage from './pages/PlayerAnalyticsPage'
import TradeAuditPage from './pages/TradeAuditPage'
import GmPermPage from './pages/GmPermPage'
import BackupPage from './pages/BackupPage'

const Guard = ({ children }: { children: React.ReactNode }) =>
  localStorage.getItem('gm_token') ? <>{children}</> : <Navigate to="/login" replace />

ReactDOM.createRoot(document.getElementById('root')!).render(
  <BrowserRouter>
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/" element={<Guard><Layout /></Guard>}>
        <Route index            element={<Dashboard />} />
        <Route path="players"   element={<PlayersPage />} />
        <Route path="master"    element={<MasterPage />} />
        <Route path="online"    element={<OnlinePage />} />
        <Route path="ban"       element={<BanPage />} />
        <Route path="recharge"  element={<RechargePage />} />
        <Route path="goldlog"   element={<GoldLogPage />} />
        <Route path="mail"      element={<MailPage />} />
        <Route path="tradelog"   element={<TradeLogPage />} />
        <Route path="history"    element={<PlayerHistoryPage />} />
        <Route path="streetshop" element={<StreetShopPage />} />
        <Route path="vip"       element={<VipPage />} />
        <Route path="batch"     element={<BatchPage />} />
        <Route path="recycle"   element={<RecyclePage />} />
        <Route path="batchgold" element={<BatchGoldPage />} />
        <Route path="petcmd"    element={<PetCmdPage />} />
        <Route path="sql"       element={<SqlQueryPage />} />
        <Route path="gmlog"     element={<GmLogPage />} />
        <Route path="gmadmin"   element={<GmAdminPage />} />
        <Route path="itemqueue" element={<ItemSendPage />} />
        <Route path="send"      element={<ItemSendPage />} />
        <Route path="shopstats" element={<ShopStatsPage />} />
        <Route path="tradeaudit" element={<TradeAuditPage />} />
        <Route path="gmperm"    element={<GmPermPage />} />
        <Route path="backup"    element={<BackupPage />} />
        <Route path="analytics/player"   element={<PlayerAnalyticsPage />} />
        <Route path="analytics/recharge" element={<RechargeAnalyticsPage />} />
      </Route>
    </Routes>
  </BrowserRouter>
)
