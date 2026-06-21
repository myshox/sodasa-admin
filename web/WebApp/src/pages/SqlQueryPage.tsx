import { useState } from 'react'
import api from '../api'
import { S } from '../strings'

export default function SqlQueryPage() {
  const [sql, setSql] = useState('SELECT `Name` AS 帳號, OnlineName AS 角色, VipPoint AS 金幣 FROM csalogin LIMIT 50')
  const [loading, setLoading] = useState(false)
  const [rows, setRows] = useState<Record<string, unknown>[]>([])
  const [error, setError] = useState('')
  const [columns, setColumns] = useState<string[]>([])

  const run = async () => {
    if (!sql.trim()) return
    setLoading(true); setError(''); setRows([]); setColumns([])
    try {
      const r = await api.post('/sql/query', { sql: sql.trim() })
      if (r.data.error) {
        setError(r.data.error)
        return
      }
      const list = r.data.rows as Record<string, unknown>[]
      setRows(list)
      if (list.length > 0) setColumns(Object.keys(list[0]))
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } }
      setError(err.response?.data?.error || '請求失敗')
    } finally { setLoading(false) }
  }

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>💻 {S.navSql}</h1>
      <p style={{ color: 'var(--text-muted)', fontSize: 12, marginBottom: 12 }}>⚠ 僅允許 SELECT / SHOW / DESCRIBE 查詢（資料只讀）</p>
      <textarea value={sql} onChange={e => setSql(e.target.value)} style={{
        width: '100%', minHeight: 100, padding: 12, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 8, fontFamily: 'Consolas, monospace', fontSize: 13, marginBottom: 12
      }} />
      <button onClick={run} disabled={loading} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 20px', marginBottom: 16 }}>{loading ? '查詢中…' : '執行查詢'}</button>

      {error && <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: 12, marginBottom: 16, color: 'var(--accent-red)', fontSize: 13 }}>{error}</div>}

      {columns.length > 0 && (
        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'auto' }}>
          <div style={{ display: 'grid', gridTemplateColumns: columns.map(() => '1fr').join(' '), padding: '8px 12px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, gap: 1 }}>
            {columns.map(c => <span key={c} style={{ padding: '4px 8px' }}>{c}</span>)}
          </div>
          {rows.map((row, i) => (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: columns.map(() => '1fr').join(' '), padding: '8px 12px', borderBottom: '1px solid var(--border)', fontSize: 13, gap: 1 }}>
              {columns.map(col => (
                <span key={col} style={{ padding: '4px 8px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                  {row[col] != null ? String(row[col]) : '—'}
                </span>
              ))}
            </div>
          ))}
          <p style={{ padding: 12, color: 'var(--text-muted)', fontSize: 13 }}>共 {rows.length} 筆</p>
        </div>
      )}
    </div>
  )
}
