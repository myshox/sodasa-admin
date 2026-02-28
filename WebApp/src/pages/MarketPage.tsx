import { useState } from 'react'
import StreetShopPage from './StreetShopPage'
import ItemSearchPage from './ItemSearchPage'

type Tab = 'street' | 'item'

const TABS: { key: Tab; icon: string; label: string }[] = [
  { key: 'street', icon: '🏪', label: '攤位 & 商城查詢' },
  { key: 'item',   icon: '🎁', label: '物品查詢（反查）' },
]

export default function MarketPage() {
  const [tab, setTab] = useState<Tab>('street')

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
      <div style={{ flex: 1, overflow: 'hidden', background: 'var(--bg-page)' }}>
        {tab === 'street' && <StreetShopPage />}
        {tab === 'item'   && <ItemSearchPage />}
      </div>
    </div>
  )
}
