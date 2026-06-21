import { useState } from 'react'
import Dashboard            from './Dashboard'
import ShopStatsPage        from './ShopStatsPage'
import PlayerAnalyticsPage  from './PlayerAnalyticsPage'
import RechargeAnalyticsPage from './RechargeAnalyticsPage'
import TradeAuditPage       from './TradeAuditPage'

type Tab = 'dashboard' | 'shop' | 'player' | 'recharge' | 'audit'

const TABS: { key: Tab; icon: string; label: string }[] = [
  { key: 'dashboard', icon: '📈', label: '儀表板' },
  { key: 'shop',      icon: '🏪', label: '商城分析' },
  { key: 'player',    icon: '📊', label: '玩家分析' },
  { key: 'recharge',  icon: '💰', label: '儲值分析' },
  { key: 'audit',     icon: '🔍', label: '交易稽核' },
]

export default function AnalyticsPage() {
  const [tab, setTab] = useState<Tab>('dashboard')

  return (
    <div className="hub-layout">
      <div className="tab-bar" role="tablist" aria-label="數據分析分頁">
        {TABS.map(t => (
          <button
            key={t.key}
            type="button"
            className="tab-bar__tab"
            role="tab"
            aria-selected={tab === t.key}
            onClick={() => setTab(t.key)}
          >
            {t.icon} {t.label}
          </button>
        ))}
      </div>
      <div className="hub-layout__body">
        {tab === 'dashboard' && <Dashboard />}
        {tab === 'shop'      && <ShopStatsPage />}
        {tab === 'player'    && <PlayerAnalyticsPage />}
        {tab === 'recharge'  && <RechargeAnalyticsPage />}
        {tab === 'audit'     && <TradeAuditPage />}
      </div>
    </div>
  )
}
