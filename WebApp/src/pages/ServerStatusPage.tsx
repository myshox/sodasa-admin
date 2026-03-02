import { useEffect, useState, useCallback } from 'react'
import api from '../api'

// ── 型別 ──────────────────────────────────────────────────────
interface MasterStats {
  totalMasters: number
  onlineMasters: number
  offlineMasters: number
}
interface ChannelEntry {
  serverId: number
  serverName: string
  onlineCount: number
  totalCount: number
}
interface RegAccount {
  account: string
  charName: string
  masterName: string
  regTime: string
  regIP: string
  serverName: string
  isOnline: boolean
}

// ── 小元件 ────────────────────────────────────────────────────
const StatCard = ({
  icon, label, value, color, sub,
}: { icon: string; label: string; value: string | number; color: string; sub?: string }) => (
  <div style={{
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 12, padding: '20px 24px', flex: 1, minWidth: 160,
    borderTop: `3px solid ${color}`,
  }}>
    <div style={{ fontSize: 28, marginBottom: 6 }}>{icon}</div>
    <div style={{ fontSize: 28, fontWeight: 800, color, lineHeight: 1 }}>{value}</div>
    <div style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 6 }}>{label}</div>
    {sub && <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{sub}</div>}
  </div>
)

// ── 分流卡片 ──────────────────────────────────────────────────
const ChannelCard = ({
  entry, maxOnline,
}: { entry: ChannelEntry; maxOnline: number }) => {
  const pct = maxOnline > 0 ? (entry.onlineCount / maxOnline) * 100 : 0
  const name = entry.serverName || `分流 ${entry.serverId}`
  return (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 10, padding: '14px 16px', minWidth: 130, flex: '1 1 130px',
    }}>
      <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-secondary)', marginBottom: 8 }}>{name}</div>
      {/* 長條 */}
      <div style={{ height: 6, background: 'var(--bg-mid)', borderRadius: 3, marginBottom: 8 }}>
        <div style={{
          height: '100%', borderRadius: 3,
          width: `${pct}%`,
          background: entry.onlineCount > 0 ? '#16b97a' : 'var(--border)',
          transition: 'width .4s',
          minWidth: entry.onlineCount > 0 ? 4 : 0,
        }} />
      </div>
      <div style={{ fontSize: 20, fontWeight: 800, color: '#16b97a' }}>{entry.onlineCount.toLocaleString()}</div>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>在線 / 總計 {entry.totalCount.toLocaleString()}</div>
    </div>
  )
}

// ── 主頁面 ────────────────────────────────────────────────────
export default function ServerStatusPage() {
  const [masterStats, setMasterStats]   = useState<MasterStats | null>(null)
  const [channels, setChannels]         = useState<ChannelEntry[]>([])
  const [accounts, setAccounts]         = useState<RegAccount[]>([])
  const [limit, setLimit]               = useState(30)
  const [loading, setLoading]           = useState(false)
  const [lastUpdate, setLastUpdate]     = useState('')

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const [ms, ch, ac] = await Promise.all([
        api.get('/server-status/master-stats'),
        api.get('/server-status/channel-online'),
        api.get(`/server-status/recent-registrations?limit=${limit}`),
      ])
      setMasterStats(ms.data)
      setChannels(ch.data)
      setAccounts(ac.data)
      setLastUpdate(new Date().toLocaleTimeString('zh-TW'))
    } finally {
      setLoading(false)
    }
  }, [limit])

  useEffect(() => { refresh() }, [refresh])

  // 每 30 秒自動刷新
  useEffect(() => {
    const id = setInterval(refresh, 30_000)
    return () => clearInterval(id)
  }, [refresh])

  const maxOnline = Math.max(...channels.map(c => c.onlineCount), 1)

  return (
    <div style={{ padding: '24px 28px', display: 'flex', flexDirection: 'column', gap: 24, minHeight: 0 }}>
      {/* ── 標題列 ── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 10 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 800, color: 'var(--text-primary)', margin: 0 }}>🖥 伺服器狀態</h1>
          {lastUpdate && (
            <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4 }}>
              最後更新 {lastUpdate}（每 30 秒自動刷新）
            </div>
          )}
        </div>
        <button
          onClick={refresh} disabled={loading}
          style={{
            padding: '8px 20px', borderRadius: 8, border: 'none', cursor: 'pointer',
            background: loading ? 'var(--bg-mid)' : '#1e4ba0',
            color: '#fff', fontWeight: 700, fontSize: 14,
            opacity: loading ? 0.6 : 1,
          }}
        >
          {loading ? '更新中…' : '🔄 重新整理'}
        </button>
      </div>

      {/* ── 主帳號統計 ── */}
      <section>
        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: 1, marginBottom: 10, textTransform: 'uppercase' }}>
          主帳號統計
        </div>
        <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
          <StatCard icon="👑" label="主帳號總數"  color="#3b82f6"
            value={masterStats ? masterStats.totalMasters.toLocaleString() : '—'} />
          <StatCard icon="🟢" label="目前在線"    color="#16b97a"
            value={masterStats ? masterStats.onlineMasters.toLocaleString() : '—'}
            sub={masterStats ? `佔 ${masterStats.totalMasters > 0 ? ((masterStats.onlineMasters / masterStats.totalMasters) * 100).toFixed(1) : 0}%` : undefined} />
          <StatCard icon="⚫" label="目前離線"    color="#94a3b8"
            value={masterStats ? masterStats.offlineMasters.toLocaleString() : '—'} />
        </div>
      </section>

      {/* ── 各分流在線 ── */}
      <section>
        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: 1, marginBottom: 10, textTransform: 'uppercase' }}>
          各分流在線人數
        </div>
        {channels.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', fontSize: 13 }}>（無分流資料）</div>
        ) : (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {channels.map(ch => (
              <ChannelCard key={ch.serverId} entry={ch} maxOnline={maxOnline} />
            ))}
          </div>
        )}
      </section>

      {/* ── 最新註冊帳號 ── */}
      <section style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10, flexWrap: 'wrap', gap: 8 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: 1, textTransform: 'uppercase' }}>
            最新註冊帳號
          </div>
          <select
            value={limit}
            onChange={e => setLimit(Number(e.target.value))}
            style={{
              background: 'var(--bg-input)', color: 'var(--text-primary)',
              border: '1px solid var(--border)', borderRadius: 6,
              padding: '4px 10px', fontSize: 13,
            }}
          >
            <option value={20}>最新 20 筆</option>
            <option value={30}>最新 30 筆</option>
            <option value={50}>最新 50 筆</option>
            <option value={100}>最新 100 筆</option>
          </select>
        </div>

        <div style={{ flex: 1, overflow: 'auto', border: '1px solid var(--border)', borderRadius: 10 }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ background: 'var(--bg-dark)', position: 'sticky', top: 0, zIndex: 1 }}>
                {['狀態', '帳號', '角色名', '主帳號', '分流', '註冊時間', '註冊 IP'].map(h => (
                  <th key={h} style={{
                    padding: '10px 14px', textAlign: 'left',
                    color: 'var(--text-secondary)', fontWeight: 700,
                    borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap',
                  }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {accounts.map((a, i) => (
                <tr key={i} style={{
                  background: i % 2 === 0 ? 'var(--bg-card)' : 'var(--bg-mid)',
                  transition: 'background .1s',
                }}>
                  <td style={{ padding: '9px 14px', whiteSpace: 'nowrap' }}>
                    <span style={{
                      display: 'inline-block', padding: '2px 8px', borderRadius: 4,
                      fontSize: 11, fontWeight: 700,
                      background: a.isOnline ? 'rgba(22,185,122,.15)' : 'rgba(100,110,140,.15)',
                      color: a.isOnline ? '#16b97a' : 'var(--text-muted)',
                    }}>
                      {a.isOnline ? '🟢 在線' : '⚫ 離線'}
                    </span>
                  </td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-primary)', fontWeight: 600 }}>{a.account}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-secondary)' }}>{a.charName || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-secondary)' }}>{a.masterName || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-muted)' }}>{a.serverName || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{a.regTime || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-muted)', fontFamily: 'monospace', fontSize: 12 }}>{a.regIP || '—'}</td>
                </tr>
              ))}
              {accounts.length === 0 && !loading && (
                <tr>
                  <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-muted)' }}>
                    無資料
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
