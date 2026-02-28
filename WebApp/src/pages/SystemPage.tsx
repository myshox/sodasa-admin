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
        {tab === 'log'    && <GmLogPage />}
        {tab === 'perm'   && <GmPermPage />}
        {tab === 'backup' && <BackupPage />}
      </div>
    </div>
  )
}
