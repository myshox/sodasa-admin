import { useState } from 'react'
import RechargePage from './RechargePage'
import TradeLogPage from './TradeLogPage'
import GoldLogPage  from './GoldLogPage'
import MailPage     from './MailPage'

type Tab = 'recharge' | 'trade' | 'gold' | 'mail'

const TABS: { key: Tab; icon: string; label: string }[] = [
  { key: 'recharge', icon: '💰', label: '充值記錄' },
  { key: 'trade',    icon: '📊', label: '交易記錄' },
  { key: 'gold',     icon: '💎', label: '金幣日誌' },
  { key: 'mail',     icon: '📧', label: '郵件查詢' },
]

export default function RecordsPage() {
  const [tab, setTab] = useState<Tab>('recharge')

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
        {tab === 'recharge' && <RechargePage />}
        {tab === 'trade'    && <TradeLogPage />}
        {tab === 'gold'     && <GoldLogPage />}
        {tab === 'mail'     && <MailPage />}
      </div>
    </div>
  )
}
