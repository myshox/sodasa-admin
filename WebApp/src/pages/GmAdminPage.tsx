import { useEffect, useState } from 'react'
import api from '../api'
import { S } from '../strings'

interface AdminUser {
  id: number
  username: string
  nickname: string
  isEnabled: boolean
  createdAt: string
}

export default function GmAdminPage() {
  const [list, setList] = useState<AdminUser[]>([])
  const [loading, setLoading] = useState(true)
  const [apiErr, setApiErr] = useState(false)
  const [msg, setMsg] = useState('')
  const [showAdd, setShowAdd] = useState(false)
  const [newUser, setNewUser] = useState('')
  const [newPass, setNewPass] = useState('')
  const [newNick, setNewNick] = useState('')

  const load = async () => {
    setLoading(true); setApiErr(false)
    try {
      const r = await api.get('/admin/users')
      setList(r.data)
    } catch {
      setApiErr(true)
    } finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const addUser = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newUser.trim()) { setMsg('請輸入帳號'); return }
    try {
      await api.post('/admin/users', { username: newUser.trim(), password: newPass || '123456', nickname: newNick.trim() })
      setMsg('已新增'); setShowAdd(false); setNewUser(''); setNewPass(''); setNewNick(''); load()
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err.response?.data?.message || '新增失敗')
    }
  }

  const deleteUser = async (id: number, name: string) => {
    if (!confirm(`確定刪除「${name}」？`)) return
    try {
      await api.delete(`/admin/users/${id}`)
      setMsg('已刪除'); load()
    } catch {
      setMsg('刪除失敗（可能不可刪除 admin）')
    }
    setTimeout(() => setMsg(''), 2500)
  }

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🔑 {S.navGmAdmin}</h1>
      {apiErr && (
        <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: '10px 16px', marginBottom: 16, color: 'var(--accent-red)', fontSize: 13 }}>
          ⚠️ {S.apiError}（Port 5050）
        </div>
      )}
      {msg && <div style={{ background: 'rgba(86,196,118,.15)', border: '1px solid var(--accent-green)', borderRadius: 8, padding: '8px 16px', marginBottom: 16, color: 'var(--accent-green)', fontSize: 13 }}>{msg}</div>}

      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>工具後台登入帳號管理</span>
        <button onClick={() => setShowAdd(true)} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 14px', fontSize: 13 }}>+ 新增帳號</button>
      </div>

      {showAdd && (
        <form onSubmit={addUser} style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 16 }}>
          <h3 style={{ fontSize: 13, fontWeight: 700, marginBottom: 12 }}>新增帳號</h3>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
            <input value={newUser} onChange={e => setNewUser(e.target.value)} placeholder="帳號" required style={{ width: 140 }} />
            <input type="password" value={newPass} onChange={e => setNewPass(e.target.value)} placeholder="密碼（留空預設 123456）" style={{ width: 160 }} />
            <input value={newNick} onChange={e => setNewNick(e.target.value)} placeholder="暱稱" style={{ width: 120 }} />
            <button type="submit" style={{ background: 'var(--accent-green)', color: '#fff', padding: '6px 14px' }}>確認新增</button>
            <button type="button" onClick={() => setShowAdd(false)} style={{ background: 'var(--bg-input)', border: '1px solid var(--border)', padding: '6px 14px' }}>{S.cancel}</button>
          </div>
        </form>
      )}

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '80px 1fr 1fr 100px 80px', padding: '8px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>
          <span>ID</span><span>帳號</span><span>暱稱</span><span>狀態</span><span></span>
        </div>
        {loading ? <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</p> : list.length === 0 ? (
          <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.noData}</p>
        ) : (
          list.map(u => (
            <div key={u.id} style={{ display: 'grid', gridTemplateColumns: '80px 1fr 1fr 100px 80px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, alignItems: 'center' }}>
              <span>{u.id}</span>
              <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{u.username}</span>
              <span style={{ color: 'var(--text-secondary)' }}>{u.nickname || S.em}</span>
              <span style={{ color: u.isEnabled ? 'var(--accent-green)' : 'var(--text-muted)' }}>{u.isEnabled ? '啟用' : '停用'}</span>
              <button onClick={() => deleteUser(u.id, u.username)} disabled={u.username === 'admin'} style={{ background: 'rgba(245,101,101,.2)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)', fontSize: 12, padding: '3px 8px' }}>刪除</button>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
