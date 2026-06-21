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
    <div className="hub-layout">
      <div className="tab-bar" role="tablist" aria-label="玩家中心分頁">
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
        {tab === 'search' && <PlayersPage />}
        {tab === 'online' && <OnlinePage />}
        {tab === 'ban'    && <BanPage />}
      </div>
    </div>
  )
}
