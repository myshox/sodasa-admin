import { useState } from 'react'
import api from '../api'
import { S } from '../strings'

interface GoldLogRow { account: string; before: number; after: number; diff: number; op: string; time: string }

export default function GoldLogPage() {
  const [q,    setQ]    = useState('')
  const [rows, setRows] = useState<GoldLogRow[]>([])
  const [loading, setLoading] = useState(false)

  const [apiErr, setApiErr] = useState(false)
  const search = async () => {
    if (!q.trim()) return
    setLoading(true); setApiErr(false)
    try {
      const r = await api.get('/players/goldlog', { params: { q } })
      setRows(r.data)
    } catch {
      setApiErr(true)
    } finally { setLoading(false) }
  }

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>
        {'\u{1F48E}'} {S.pageGoldLog}
      </h1>
      {apiErr && (
        <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: '10px 16px', marginBottom: 16, color: 'var(--accent-red)', fontSize: 13 }}>
          ⚠️ {S.apiError}（Port 5050）
        </div>
      )}
      <div style={{ display: 'flex', gap: 10, marginBottom: 20 }}>
        <input value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder={S.searchPlh} style={{ flex: 1, maxWidth: 360 }} />
        <button onClick={search} style={{ background: 'var(--accent-blue)', color: '#fff' }}>
          {loading ? S.searching : `${'\u{1F50D}'} ${S.searchBtn}`}
        </button>
      </div>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr 1fr 160px', padding: '8px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>
          <span>{S.colAccount}</span><span>{S.colBefore}</span><span>{S.colAfter}</span><span>{S.colDiff}</span><span>{S.colOp}</span><span>{S.colTime}</span>
        </div>
        {rows.length === 0
          ? <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.searchHint}</p>
          : rows.map((r, i) => (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr 1fr 160px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, alignItems: 'center' }}>
              <span style={{ fontWeight: 600 }}>{r.account}</span>
              <span>{r.before.toLocaleString()}</span>
              <span>{r.after.toLocaleString()}</span>
              <span style={{ color: r.diff >= 0 ? 'var(--accent-green)' : 'var(--accent-red)', fontWeight: 600 }}>
                {r.diff >= 0 ? '+' : ''}{r.diff.toLocaleString()}
              </span>
              <span style={{ color: 'var(--text-secondary)' }}>{r.op}</span>
              <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>{r.time}</span>
            </div>
          ))}
      </div>
    </div>
  )
}
