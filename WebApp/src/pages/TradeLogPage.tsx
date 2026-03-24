import { useState } from 'react'
import api from '../api'
import { S } from '../strings'

interface TradeRow {
  fromCdkey: string
  fromName: string
  toCdkey: string
  toName: string
  time: string
  item: string
  pet: string
  gold: number
}

export default function TradeLogPage() {
  const [q, setQ] = useState('')
  const [rows, setRows] = useState<TradeRow[]>([])
  const [loading, setLoading] = useState(false)
  const [apiErr, setApiErr] = useState(false)

  const search = async () => {
    setLoading(true); setApiErr(false)
    try {
      const r = await api.get('/players/tradelog', { params: { q: q.trim() || undefined, limit: 300 } })
      setRows(r.data)
    } catch {
      setApiErr(true)
    } finally { setLoading(false) }
  }

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>📊 {S.navTradeLog}</h1>
      {apiErr && (
        <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: '10px 16px', marginBottom: 16, color: 'var(--accent-red)', fontSize: 13 }}>
          ⚠️ {S.apiError}（Port 5050）
        </div>
      )}
      <div style={{ display: 'flex', gap: 10, marginBottom: 20 }}>
        <input value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder="搜尋雙方帳號或角色名" style={{ flex: 1, maxWidth: 360 }} />
        <button onClick={search} style={{ background: 'var(--accent-blue)', color: '#fff' }}>
          {loading ? S.searching : `🔍 ${S.searchBtn}`}
        </button>
      </div>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr 80px 1fr 160px', padding: '8px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>
          <span>發送方</span><span>接收方</span><span>類型</span><span>內容</span><span>金幣</span><span>時間</span>
        </div>
        {rows.length === 0 && !loading && (
          <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.searchHint}</p>
        )}
        {rows.map((r, i) => (
          <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr 80px 1fr 160px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, alignItems: 'center', gap: 8 }}>
            <span style={{ color: 'var(--text-primary)' }}>{r.fromName || r.fromCdkey}</span>
            <span style={{ color: 'var(--text-secondary)' }}>{r.toName || r.toCdkey}</span>
            <span style={{ fontSize: 12 }}>
              {r.item && r.pet ? '道具+寵物' : r.item ? '📦 道具' : r.pet ? '🐾 寵物' : r.gold > 0 ? '💰 金幣' : '—'}
            </span>
            <span style={{ color: 'var(--text-muted)', fontSize: 12, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {r.item ? (r.item.length > 30 ? r.item.slice(0, 30) + '…' : r.item) : r.pet ? (r.pet.length > 30 ? r.pet.slice(0, 30) + '…' : r.pet) : '—'}
            </span>
            <span style={{ color: r.gold > 0 ? 'var(--accent-green)' : 'var(--text-muted)' }}>{r.gold > 0 ? r.gold.toLocaleString() : '—'}</span>
            <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>{r.time}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
