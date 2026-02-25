import { useEffect, useState } from 'react'
import api from '../api'
import type { DashboardStats } from '../api'
import { S } from '../strings'

const StatCard = ({ icon, label, value, color }: {
  icon: string; label: string; value: string | number; color: string
}) => (
  <div style={{
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 10, padding: '20px 24px', flex: 1, minWidth: 160
  }}>
    <div style={{ fontSize: 28, marginBottom: 8 }} dangerouslySetInnerHTML={{ __html: icon }} />
    <div style={{ fontSize: 26, fontWeight: 700, color }}>{value}</div>
    <div style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 4 }}>{label}</div>
  </div>
)

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [err, setErr] = useState(false)

  useEffect(() => {
    api.get('/stats').then(r => setStats(r.data)).catch(() => setErr(true))
  }, [])

  const fmt = (n?: number) => n?.toLocaleString() ?? '...'

  const shortcuts = [
    { icon: '&#128101;', label: S.navPlayers, path: '/players' },
    { icon: '&#128994;', label: S.navOnline,  path: '/online' },
    { icon: '&#128062;', label: S.navPetCmd,  path: '/petcmd' },
  ]

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 24, color: 'var(--text-primary)' }}>
        &#128202; {S.navDashboard}
      </h1>

      {err && (
        <div style={{
          background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)',
          borderRadius: 8, padding: '10px 16px', marginBottom: 20,
          color: 'var(--accent-red)', fontSize: 13
        }}>
          &#9888; API {'\u9023\u7DDA\u5931\u6557\uff0C\u8ACB\u78BA\u8A8D API \u5DF2\u555F\u52D5\uff08Port 5050\uff09'}
        </div>
      )}

      <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 32 }}>
        <StatCard icon="&#128101;" label={S.statTotal}   value={fmt(stats?.totalPlayers)}  color="var(--accent-blue)" />
        <StatCard icon="&#128994;" label={S.statOnline}  value={fmt(stats?.onlinePlayers)} color="var(--accent-green)" />
        <StatCard icon="&#128683;" label={S.statBanned}  value={fmt(stats?.bannedPlayers)} color="var(--accent-red)" />
        <StatCard icon="&#127379;" label={S.statNewToday} value={fmt(stats?.newToday)}     color="var(--accent-orange)" />
        <StatCard icon="&#128176;" label={S.statGold}    value={fmt(stats?.totalGold)}     color="var(--accent-blue)" />
        <StatCard icon="&#128142;" label={S.statCrystal} value={fmt(stats?.totalCrystal)}  color="var(--accent-blue)" />
      </div>

      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 10, padding: '20px 24px'
      }}>
        <p style={{ color: 'var(--text-secondary)', fontSize: 14, marginBottom: 14 }}>
          &#128295; {S.shortcuts}
        </p>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          {shortcuts.map(s => (
            <a key={s.path} href={s.path} style={{
              background: 'var(--bg-input)', border: '1px solid var(--border)',
              borderRadius: 8, padding: '10px 18px', display: 'flex',
              alignItems: 'center', gap: 8, color: 'var(--text-primary)',
              fontSize: 14, fontWeight: 500, textDecoration: 'none'
            }}>
              <span dangerouslySetInnerHTML={{ __html: s.icon }} />
              <span>{s.label}</span>
            </a>
          ))}
        </div>
      </div>
    </div>
  )
}
