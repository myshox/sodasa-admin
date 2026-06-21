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
    <div className="gm-page-fill">
      {/* 標題列 */}
      <div style={{ padding: '14px 20px 0', background: 'var(--bg-sidebar)', flexShrink: 0 }}>
        <h1 style={{ fontSize: 18, fontWeight: 800, margin: '0 0 2px', color: 'var(--text-primary)' }}>🏪 市場查詢</h1>
        <p style={{ margin: '0 0 10px', fontSize: 11, color: 'var(--text-muted)' }}>
          攤位 & 商城上架物品查詢 · 輸入道具 ID 反查持有者
        </p>
      </div>
      <div style={{
        display: 'flex', gap: 2, padding: '0 20px',
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
