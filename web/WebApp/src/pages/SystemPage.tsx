import { useState } from 'react'
import GmLogPage  from './GmLogPage'
import GmPermPage from './GmPermPage'
import BackupPage from './BackupPage'

type Tab = 'log' | 'perm' | 'backup'

const TABS: { key: Tab; icon: string; label: string }[] = [
  { key: 'log',    icon: '📋', label: 'GM 日誌' },
  { key: 'perm',   icon: '🛡', label: 'GM 權限' },
  { key: 'backup', icon: '💾', label: '備份下載' },
]

export default function SystemPage() {
  const [tab, setTab] = useState<Tab>('log')

  return (
    <div className="hub-layout">
      <div className="tab-bar" role="tablist" aria-label="系統設定分頁">
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
        {tab === 'log'    && <GmLogPage />}
        {tab === 'perm'   && <GmPermPage />}
        {tab === 'backup' && <BackupPage />}
      </div>
    </div>
  )
}
