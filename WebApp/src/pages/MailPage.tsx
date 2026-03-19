import { useState } from 'react'
import api from '../api'
import { S } from '../strings'
import useIsMobile from '../hooks/useIsMobile'

interface MailRow { id: number; sender: string; title: string; content: string; isRead: boolean; time: string }

export default function MailPage() {
  const isMobile = useIsMobile()
  const [q,    setQ]    = useState('')
  const [rows, setRows] = useState<MailRow[]>([])
  const [loading, setLoading] = useState(false)
  const [sel, setSel] = useState<MailRow | null>(null)

  const [apiErr, setApiErr] = useState(false)
  const search = async () => {
    if (!q.trim()) return
    setLoading(true); setSel(null); setApiErr(false)
    try {
      const r = await api.get('/players/mail', { params: { q } })
      setRows(r.data)
    } catch {
      setApiErr(true)
    } finally { setLoading(false) }
  }

  return (
    <div style={{ padding: isMobile ? 12 : 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>
        {'\u{1F4E7}'} {S.navMail}
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
      <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start', flexWrap: isMobile ? 'wrap' : 'nowrap' }}>
        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, flex: 1, minWidth: 0, width: isMobile ? '100%' : undefined }}>
          <div className="table-wrap" style={{ overflowX: 'auto', WebkitOverflowScrolling: 'touch' }}>
          <div style={{ display: 'grid', gridTemplateColumns: '80px 1fr 80px 140px', padding: '8px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, minWidth: isMobile ? 380 : undefined }}>
            <span>發件人</span><span>標題</span><span>狀態</span><span>時間</span>
          </div>
          {rows.length === 0
            ? <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.searchHint}</p>
            : rows.map(r => (
              <div key={r.id} onClick={() => setSel(r)} data-suggestion-item style={{
                display: 'grid', gridTemplateColumns: '80px 1fr 80px 140px',
                padding: '10px 16px', borderBottom: '1px solid var(--border)',
                fontSize: 13, alignItems: 'center', cursor: 'pointer',
                background: sel?.id === r.id ? 'rgba(74,158,255,.08)' : 'transparent',
                minWidth: isMobile ? 380 : undefined
              }}>
                <span style={{ color: 'var(--text-muted)' }}>{r.sender}</span>
                <span style={{ fontWeight: r.isRead ? 400 : 700, color: r.isRead ? 'var(--text-secondary)' : 'var(--text-primary)' }}>{r.title}</span>
                <span style={{ fontSize: 11 }}>{r.isRead ? <span style={{ color: 'var(--text-muted)' }}>已讀</span> : <span style={{ color: 'var(--accent-blue)', background: 'rgba(74,158,255,.15)', padding: '1px 6px', borderRadius: 10 }}>未讀</span>}</span>
                <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>{r.time}</span>
              </div>
            ))}
          </div>
        </div>
        {sel && (
          <div style={{ width: isMobile ? '100%' : 300, background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, flexShrink: 0 }}>
            <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 8 }}>{sel.title}</div>
            <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 12 }}>
              發件：{sel.sender} | {sel.time}
            </div>
            <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6, whiteSpace: 'pre-wrap' }}>
              {sel.content}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
