import { useState, useEffect } from 'react'
import api from '../api'
import { S } from '../strings'

export default function GmPermPage() {
  const [q, setQ] = useState('')
  const [list, setList] = useState<any[]>([])
  const [loading, setLoading] = useState(false)
  const [edit, setEdit] = useState<{ account: string; onlineName: string; neiCe: number; groupId: number } | null>(null)
  const [saving, setSaving] = useState(false)
  const [msg, setMsg] = useState('')

  const load = () => {
    setLoading(true)
    api.get('/gmperm', { params: { q: q || undefined } }).then(r => setList(r.data || [])).catch(() => {}).finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [q])

  const savePerm = async () => {
    if (!edit) return
    setSaving(true)
    try {
      await api.put(`/gmperm/${encodeURIComponent(edit.account)}`, { neiCe: edit.neiCe, groupId: edit.groupId })
      setMsg('✓ 已更新權限')
      setEdit(null)
      load()
    } catch { setMsg('更新失敗') }
    finally { setSaving(false) }
  }

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 8 }}>🛡 {S.navGmPerm}</h1>
      <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 16 }}>NeiCe=1 為 GM 標記 | GroupId 預設 0=一般玩家</p>
      {msg && <p style={{ color: 'var(--accent-green)', marginBottom: 12 }}>{msg}</p>}
      <div className="gm-search-bar">
        <div className="gm-search-bar__grow">
          <input
            className="gm-search-input"
            value={q}
            onChange={e => setQ(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && load()}
            placeholder="搜尋角色名稱或帳號（留空＝顯示所有 GM 標記玩家）"
            enterKeyHint="search"
          />
        </div>
        <div className="gm-search-bar__actions">
          <button type="button" onClick={load} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '10px 22px', borderRadius: 10, fontWeight: 700 }}>查詢</button>
        </div>
      </div>

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 100px 100px 80px 80px', padding: '10px 16px', background: 'var(--bg-sidebar)', fontSize: 12, fontWeight: 600, color: 'var(--text-muted)' }}>
          <span>帳號</span><span>角色</span><span>NeiCe</span><span>GroupId</span><span>在線</span><span></span>
        </div>
        {loading ? <p style={{ padding: 24, color: 'var(--text-muted)' }}>載入中…</p> : list.length === 0 ? <p style={{ padding: 24, color: 'var(--text-muted)', textAlign: 'center' }}>尚無資料</p> : list.map(row => (
          <div key={row.account} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 100px 100px 80px 80px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, alignItems: 'center' }}>
            <span>{row.account}</span>
            <span>{row.onlineName}</span>
            <span>{row.neiCe === 1 ? '✅ GM' : '—'}</span>
            <span>{row.groupId}</span>
            <span>{row.isOnline ? '🟢' : '—'}</span>
            <button onClick={() => setEdit({ account: row.account, onlineName: row.onlineName, neiCe: row.neiCe, groupId: row.groupId })} style={{ padding: '4px 10px', fontSize: 12, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6 }}>編輯</button>
          </div>
        ))}
      </div>

      {edit && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: 24, minWidth: 360 }}>
            <h3 style={{ marginBottom: 16 }}>編輯權限 — {edit.onlineName}</h3>
            <label style={{ display: 'block', marginBottom: 12 }}>
              <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>GM 標記（NeiCe）</span>
              <select value={edit.neiCe} onChange={e => setEdit({ ...edit, neiCe: +e.target.value })} style={{ display: 'block', width: '100%', marginTop: 4 }}>
                <option value={0}>0 = 一般玩家</option>
                <option value={1}>1 = GM</option>
              </select>
            </label>
            <label style={{ display: 'block', marginBottom: 16 }}>
              <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>GroupId</span>
              <input type="number" min={0} value={edit.groupId} onChange={e => setEdit({ ...edit, groupId: +e.target.value || 0 })} style={{ width: '100%', marginTop: 4 }} />
            </label>
            <div style={{ display: 'flex', gap: 8 }}>
              <button onClick={savePerm} disabled={saving} style={{ flex: 1, padding: '8px', background: 'var(--accent-blue)', color: '#fff', borderRadius: 8 }}>{saving ? '儲存中…' : '儲存'}</button>
              <button onClick={() => setEdit(null)} style={{ padding: '8px 16px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 8 }}>取消</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
