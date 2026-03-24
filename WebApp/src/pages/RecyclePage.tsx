import { useEffect, useState } from 'react'
import api from '../api'
import { S } from '../strings'

interface RecycleRow {
  recycleId: number
  deletedAt: string
  deletedBy: string
  account: string
  onlineName: string
  masterName: string
}

export default function RecyclePage() {
  const [list, setList] = useState<RecycleRow[]>([])
  const [loading, setLoading] = useState(true)
  const [apiErr, setApiErr] = useState(false)
  const [msg, setMsg] = useState('')

  const load = async () => {
    setLoading(true); setApiErr(false)
    try {
      const r = await api.get('/recycle')
      setList(r.data)
    } catch {
      setApiErr(true)
    } finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const restore = async (id: number) => {
    if (!confirm('確定要還原此角色嗎？')) return
    try {
      await api.post(`/recycle/restore/${id}`)
      setMsg('已還原')
      setTimeout(() => setMsg(''), 2500)
      load()
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err.response?.data?.message || '還原失敗')
      setTimeout(() => setMsg(''), 3000)
    }
  }

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🗑 {S.navRecycle}</h1>
      {apiErr && (
        <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: '10px 16px', marginBottom: 16, color: 'var(--accent-red)', fontSize: 13 }}>
          ⚠️ {S.apiError}（Port 5050）
        </div>
      )}
      {msg && (
        <div style={{ background: msg.includes('失敗') ? 'rgba(245,101,101,.1)' : 'rgba(86,196,118,.15)', border: `1px solid ${msg.includes('失敗') ? 'var(--accent-red)' : 'var(--accent-green)'}`, borderRadius: 8, padding: '8px 16px', marginBottom: 16, color: msg.includes('失敗') ? 'var(--accent-red)' : 'var(--accent-green)', fontSize: 13 }}>{msg}</div>
      )}
      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>共 {list.length} 筆</span>
        <button onClick={load} style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', border: '1px solid var(--border)', fontSize: 12, padding: '6px 12px' }}>🔄 {S.refresh}</button>
      </div>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 140px 100px', padding: '8px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>
          <span>{S.banAccount}</span><span>角色名稱</span><span>主帳號</span><span>刪除時間</span><span></span>
        </div>
        {loading ? (
          <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</p>
        ) : list.length === 0 ? (
          <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>目前回收桶無資料</p>
        ) : (
          list.map(r => (
            <div key={r.recycleId} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 140px 100px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, alignItems: 'center' }}>
              <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{r.account}</span>
              <span style={{ color: 'var(--text-secondary)' }}>{r.onlineName || S.em}</span>
              <span style={{ color: 'var(--text-muted)' }}>{r.masterName || S.em}</span>
              <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>{r.deletedAt}</span>
              <button onClick={() => restore(r.recycleId)} style={{ background: 'rgba(86,196,118,.2)', color: 'var(--accent-green)', border: '1px solid var(--accent-green)', fontSize: 12, padding: '4px 10px' }}>還原</button>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
