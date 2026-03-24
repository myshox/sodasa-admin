import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import type { PlayerRow } from '../api'
import { S } from '../strings'

export default function OnlinePage() {
  const navigate = useNavigate()
  const [players, setPlayers] = useState<PlayerRow[]>([])
  const [loading, setLoading] = useState(true)
  const [apiErr, setApiErr] = useState(false)
  const [autoRefresh, setAutoRefresh] = useState(false)
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const load = async () => {
    setApiErr(false)
    try {
      const r = await api.get('/players/online')
      setPlayers(r.data)
    } catch {
      setApiErr(true)
    } finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  useEffect(() => {
    if (autoRefresh) {
      timerRef.current = setInterval(load, 30_000)
    } else {
      if (timerRef.current) clearInterval(timerRef.current)
    }
    return () => { if (timerRef.current) clearInterval(timerRef.current) }
  }, [autoRefresh])

  return (
    <div className="gm-page-inner">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <h1 style={{ fontSize: 22, fontWeight: 700 }}>🟢 {S.pageOnline}</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>{S.onlineCount(players.length)}</span>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: 'var(--text-secondary)', cursor: 'pointer' }}>
            <input type="checkbox" checked={autoRefresh} onChange={e => setAutoRefresh(e.target.checked)} />
            自動更新(30s)
          </label>
          <button onClick={load} style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', border: '1px solid var(--border)' }}>
            🔔 {S.refresh}
          </button>
        </div>
      </div>

      {apiErr && (
        <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: '10px 16px', marginBottom: 16, color: 'var(--accent-red)', fontSize: 13 }}>
          ⚠️ {S.apiError}（Port 5050）
        </div>
      )}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
          <thead>
            <tr style={{ background: 'var(--bg-input)' }}>
              <Th>角色名稱</Th>
              <Th>帳號</Th>
              <Th>主帳號</Th>
              <Th>頻道</Th>
              <Th>登入時間</Th>
              <Th>累積儲值</Th>
              <Th>IP</Th>
              <Th>操作</Th>
            </tr>
          </thead>
          <tbody>
            {loading
              ? <tr><td colSpan={8} style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</td></tr>
              : players.length === 0
                ? <tr><td colSpan={8} style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.noOnline}</td></tr>
                : players.map(p => (
                  <tr key={p.account} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '10px 12px', fontWeight: 600, color: 'var(--accent-green)' }}>{p.onlineName}</td>
                    <td style={{ padding: '10px 12px', color: 'var(--text-secondary)' }}>{p.account}</td>
                    <td style={{ padding: '10px 12px', color: 'var(--text-muted)', fontSize: 12 }}>{p.masterName || '—'}</td>
                    <td style={{ padding: '10px 12px', color: 'var(--text-muted)' }}>ch{p.serverId}</td>
                    <td style={{ padding: '10px 12px', color: 'var(--text-muted)', fontSize: 12 }}>{p.loginTime || '—'}</td>
                    <td style={{ padding: '10px 12px', color: 'var(--accent-orange)' }}>
                      {p.payTotal > 0 ? `NT$ ${p.payTotal.toLocaleString()}` : '—'}
                    </td>
                    <td style={{ padding: '10px 12px', color: 'var(--text-muted)', fontSize: 12 }}>{p.ip}</td>
                    <td style={{ padding: '8px 12px' }}>
                      <button onClick={() => navigate(`/players?q=${p.account}`)}
                        style={{ fontSize: 11, padding: '3px 8px', background: 'rgba(74,158,255,.15)', color: 'var(--accent-blue)', border: '1px solid var(--accent-blue)44', borderRadius: 4 }}>
                        資料
                      </button>
                    </td>
                  </tr>
                ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

const Th = ({ children }: { children: React.ReactNode }) => (
  <th style={{ padding: '8px 12px', textAlign: 'left', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' }}>
    {children}
  </th>
)
