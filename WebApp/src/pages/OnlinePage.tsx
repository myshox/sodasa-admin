import { useEffect, useState } from 'react'
import api from '../api'
import type { PlayerRow } from '../api'
import { S } from '../strings'

export default function OnlinePage() {
  const [players, setPlayers] = useState<PlayerRow[]>([])
  const [loading, setLoading] = useState(true)

  const load = async () => {
    setLoading(true)
    try { const r = await api.get('/players/online'); setPlayers(r.data) }
    finally { setLoading(false) }
  }

  useEffect(() => { load() }, [])

  return (
    <div style={{ padding: 28 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <h1 style={{ fontSize: 22, fontWeight: 700 }}>&#128994; {S.pageOnline}</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>{S.onlineCount(players.length)}</span>
          <button onClick={load} style={{
            background: 'var(--bg-input)', color: 'var(--text-secondary)',
            border: '1px solid var(--border)'
          }}>
            &#128260; {S.refresh}
          </button>
        </div>
      </div>

      <div style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 10, overflow: 'hidden'
      }}>
        <div style={{
          display: 'grid', gridTemplateColumns: '1fr 1fr 70px 140px 1fr',
          padding: '10px 16px', background: 'var(--bg-sidebar)',
          fontSize: 12, color: 'var(--text-muted)', fontWeight: 600
        }}>
          <span>{S.colChar}</span>
          <span>{S.colAccount}</span>
          <span>{S.colChannel}</span>
          <span>{S.colGold}</span>
          <span>{S.colIP}</span>
        </div>
        {loading ? (
          <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</p>
        ) : players.length === 0 ? (
          <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.noOnline}</p>
        ) : players.map(p => (
          <div key={p.account} style={{
            display: 'grid', gridTemplateColumns: '1fr 1fr 70px 140px 1fr',
            padding: '11px 16px', borderBottom: '1px solid var(--border)',
            fontSize: 13, alignItems: 'center'
          }}>
            <span style={{ fontWeight: 600, color: 'var(--accent-green)' }}>{p.onlineName}</span>
            <span style={{ color: 'var(--text-secondary)' }}>{p.account}</span>
            <span style={{ color: 'var(--text-muted)' }}>ch{p.serverId}</span>
            <span>&#128176; {p.gold.toLocaleString()}</span>
            <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>{p.ip}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
