import { useState } from 'react'
import PlayersPage from './PlayersPage'
import OnlinePage  from './OnlinePage'
import BanPage     from './BanPage'

type Tab = 'search' | 'online' | 'ban'

const TABS: { key: Tab; icon: string; label: string }[] = [
  { key: 'search', icon: '👥', label: '玩家搜尋' },
  { key: 'online', icon: '🟢', label: '在線玩家' },
  { key: 'ban',    icon: '🔒', label: '封號管理' },
]

export default function PlayerHubPage() {
  const [tab, setTab] = useState<Tab>('search')

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
        {tab === 'search' && <PlayersPage />}
        {tab === 'online' && <OnlinePage />}
        {tab === 'ban'    && <BanPage />}
      </div>
    </div>
  )
}
