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
    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
      <div style={{
        display: 'flex', gap: 2, padding: '12px 20px 0',
        background: 'var(--bg-sidebar)', borderBottom: '2px solid var(--border)',
        flexShrink: 0,
      }}>
        {TABS.map(t => (
          <button key={t.key} onClick={() => setTab(t.key)} style={{
            padding: '8px 18px', fontSize: 13, fontWeight: tab === t.key ? 700 : 400,
            background: tab === t.key ? 'var(--bg-page)' : 'transparent',
            color: tab === t.key ? 'var(--accent-blue)' : 'var(--text-secondary)',
            border: 'none', borderBottom: tab === t.key ? '2px solid var(--accent-blue)' : '2px solid transparent',
            marginBottom: -2, cursor: 'pointer', borderRadius: '6px 6px 0 0', transition: 'all .15s',
          }}>
            {t.icon} {t.label}
          </button>
        ))}
      </div>
      <div style={{ flex: 1, overflow: 'auto', background: 'var(--bg-page)' }}>
        {tab === 'dashboard' && <Dashboard />}
        {tab === 'shop'      && <ShopStatsPage />}
        {tab === 'player'    && <PlayerAnalyticsPage />}
        {tab === 'recharge'  && <RechargeAnalyticsPage />}
        {tab === 'audit'     && <TradeAuditPage />}
      </div>
    </div>
  )
}
