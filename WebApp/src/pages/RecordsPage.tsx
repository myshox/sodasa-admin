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
    <div className="hub-layout">
      <div className="tab-bar" role="tablist" aria-label="全服記錄分頁">
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
        {tab === 'recharge' && <RechargePage />}
        {tab === 'trade'    && <TradeLogPage />}
        {tab === 'gold'     && <GoldLogPage />}
        {tab === 'mail'     && <MailPage />}
      </div>
    </div>
  )
}
