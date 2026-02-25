import { useEffect, useState } from 'react'
import api from '../api'
import { S } from '../strings'

interface GmLogRow { id: number; gmUser: string; action: string; target: string; detail: string; time: string; success: boolean }

export default function GmLogPage() {
  const [rows, setRows] = useState<GmLogRow[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(0)
  const [q, setQ] = useState('')
  const [date, setDate] = useState('')
  const [dates, setDates] = useState<string[]>([])
  const pageSize = 100

  const loadDates = async () => {
    try { const r = await api.get('/gmlog/dates'); setDates(r.data) } catch { }
  }

  const load = async (p = 0, qv = q, dv = date) => {
    setLoading(true)
    try {
      const r = await api.get('/gmlog', { params: { offset: p * pageSize, limit: pageSize, q: qv, date: dv } })
      setRows(r.data.items ?? r.data); setTotal(r.data.total ?? r.data.length); setPage(p)
    } finally { setLoading(false) }
  }

  useEffect(() => { loadDates(); load(0) }, [])

  const doExport = () => {
    const params = date ? `?date=${date}` : ''
    window.open(`${import.meta.env.VITE_API_URL ?? '/api'}/gmlog/export${params}`, '_blank')
  }

  return (
    <div style={{ padding: 28 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h1 style={{ fontSize: 22, fontWeight: 700 }}>📋 {S.navGmLog}</h1>
        <div style={{ display: 'flex', gap: 8 }}>
          <button onClick={doExport} style={{ background: 'rgba(86,196,118,.15)', color: 'var(--accent-green)', border: '1px solid var(--accent-green)', fontSize: 13, padding: '6px 14px' }}>
            💾 匯出
          </button>
          <button onClick={() => load(0)} style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', border: '1px solid var(--border)' }}>
            🔄 {S.refresh}
          </button>
        </div>
      </div>

      {/* 篩選 */}
      <div style={{ display: 'flex', gap: 10, marginBottom: 14, flexWrap: 'wrap', alignItems: 'center' }}>
        <select value={date} onChange={e => { setDate(e.target.value); load(0, q, e.target.value) }}
          style={{ padding: '6px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', color: 'var(--text-primary)', borderRadius: 6 }}>
          <option value="">全部日期</option>
          {dates.map(d => <option key={d} value={d}>{d}（今日: {d}）</option>)}
        </select>
        <input value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && load(0)}
          placeholder="搜尋操作/對象/詳情/GM" style={{ flex: 1, maxWidth: 300 }} />
        <button onClick={() => load(0)} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 14px' }}>🔍 搜尋</button>
        {(q || date) && <button onClick={() => { setQ(''); setDate(''); load(0, '', '') }} style={{ background: 'var(--bg-input)', border: '1px solid var(--border)', fontSize: 12 }}>清除</button>}
        <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>共 {total} 筆</span>
      </div>

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
          <thead>
            <tr style={{ background: 'var(--bg-input)' }}>
              <Th w={28}>結果</Th>
              <Th w={90}>GM</Th>
              <Th w={110}>操作</Th>
              <Th w={120}>對象</Th>
              <Th>詳情</Th>
              <Th w={150}>時間</Th>
            </tr>
          </thead>
          <tbody>
            {loading
              ? <tr><td colSpan={6} style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</td></tr>
              : rows.length === 0
                ? <tr><td colSpan={6} style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.noData}</td></tr>
                : rows.map(r => (
                  <tr key={r.id} style={{ borderBottom: '1px solid var(--border)', background: !r.success ? 'rgba(245,101,101,.04)' : 'transparent' }}>
                    <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                      <span style={{ color: r.success ? 'var(--accent-green)' : 'var(--accent-red)' }}>
                        {r.success ? '✓' : '✗'}
                      </span>
                    </td>
                    <td style={{ padding: '8px 10px', color: 'var(--accent-blue)', fontWeight: 600 }}>{r.gmUser}</td>
                    <td style={{ padding: '8px 10px', color: 'var(--accent-orange)' }}>{r.action}</td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-secondary)' }}>{r.target}</td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-muted)', fontSize: 12 }}>{r.detail}</td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-muted)', fontSize: 12, whiteSpace: 'nowrap' }}>{r.time}</td>
                  </tr>
                ))}
          </tbody>
        </table>
      </div>

      <div style={{ display: 'flex', gap: 8, marginTop: 12, justifyContent: 'center', alignItems: 'center' }}>
        <button disabled={page === 0} onClick={() => load(page - 1)}
          style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', border: '1px solid var(--border)', opacity: page === 0 ? 0.4 : 1 }}>
          &lt; 上一頁
        </button>
        <span style={{ padding: '7px 12px', color: 'var(--text-muted)', fontSize: 13 }}>第 {page + 1} 頁</span>
        <button disabled={rows.length < pageSize} onClick={() => load(page + 1)}
          style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', border: '1px solid var(--border)', opacity: rows.length < pageSize ? 0.4 : 1 }}>
          下一頁 &gt;
        </button>
      </div>
    </div>
  )
}

const Th = ({ children, w }: { children: React.ReactNode; w?: number }) => (
  <th style={{ padding: '8px 10px', textAlign: 'left', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap', width: w }}>
    {children}
  </th>
)
