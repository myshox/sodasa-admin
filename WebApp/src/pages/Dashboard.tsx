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
    <div style={{ fontSize: 28, marginBottom: 8 }}>{icon}</div>
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
    { icon: '👥', label: S.navPlayers, path: '/players', key: 'players' },
    { icon: '💰', label: '設定／發送金幣', path: '/players', key: 'gold' },
    { icon: '📊', label: S.navBatchGold, path: '/batchgold', key: 'batchgold' },
    { icon: '📬', label: S.navItemQueue, path: '/itemqueue', key: 'itemqueue' },
    { icon: '📢', label: S.navBatch, path: '/batch', key: 'batch' },
    { icon: '🟢', label: S.navOnline, path: '/online', key: 'online' },
    { icon: '🔒', label: S.navBan, path: '/ban', key: 'ban' },
    { icon: '🐾', label: S.navPetCmd, path: '/petcmd', key: 'petcmd' },
  ]

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 24, color: 'var(--text-primary)' }}>
        📊 {S.navDashboard}
      </h1>

      {err && (
        <div style={{
          background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)',
          borderRadius: 8, padding: '14px 18px', marginBottom: 20,
          color: 'var(--accent-red)', fontSize: 13, lineHeight: 1.5
        }}>
          <strong>⚠️ 無法連線後端，所有功能都無法使用。</strong><br />
          請先：① 執行 <strong>start-api.bat</strong>（或 cd WebApi 後 dotnet run）啟動 API（Port 5050）<br />
          ② 將 <strong>appsettings.example.json</strong> 複製為 <strong>appsettings.json</strong> 並填寫資料庫連線與 GM 帳號。
        </div>
      )}

      {!err && (
        <div style={{
          background: 'rgba(74,158,255,.08)', border: '1px solid var(--accent-blue)',
          borderRadius: 8, padding: '10px 16px', marginBottom: 20,
          color: 'var(--text-secondary)', fontSize: 12
        }}>
          💡 發送金幣：左側「玩家管理」點選玩家後右側可設定金幣／水晶；或使用「批量金幣」一次對多人加減。發送道具：左側「道具給予」。
        </div>
      )}

      <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 32 }}>
        <StatCard icon="👥" label={S.statTotal}   value={fmt(stats?.totalPlayers)}  color="var(--accent-blue)" />
        <StatCard icon="🟢" label={S.statOnline}  value={fmt(stats?.onlinePlayers)} color="var(--accent-green)" />
        <StatCard icon="🚫" label={S.statBanned}  value={fmt(stats?.bannedPlayers)} color="var(--accent-red)" />
        <StatCard icon="🍳" label={S.statNewToday} value={fmt(stats?.newToday)}     color="var(--accent-orange)" />
        <StatCard icon="💰" label={S.statGold}    value={fmt(stats?.totalGold)}     color="var(--accent-blue)" />
        <StatCard icon="💎" label={S.statCrystal} value={fmt(stats?.totalCrystal)}  color="var(--accent-blue)" />
      </div>

      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 10, padding: '20px 24px'
      }}>
        <p style={{ color: 'var(--text-secondary)', fontSize: 14, marginBottom: 14 }}>
          🔧 {S.shortcuts}
        </p>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          {shortcuts.map(s => (
            <a key={s.key} href={s.path} style={{
              background: 'var(--bg-input)', border: '1px solid var(--border)',
              borderRadius: 8, padding: '10px 18px', display: 'flex',
              alignItems: 'center', gap: 8, color: 'var(--text-primary)',
              fontSize: 14, fontWeight: 500, textDecoration: 'none'
            }}>
              <span>{s.icon}</span>
              <span>{s.label}</span>
            </a>
          ))}
        </div>
      </div>
    </div>
  )
}
