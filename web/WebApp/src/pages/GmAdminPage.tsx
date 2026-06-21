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

  // 新增帳號
  const [showAdd, setShowAdd] = useState(false)
  const [newUser, setNewUser] = useState('')
  const [newPass, setNewPass] = useState('')
  const [newNick, setNewNick] = useState('')

  // 重設密碼 modal
  const [resetId, setResetId] = useState<number | null>(null)
  const [resetName, setResetName] = useState('')
  const [resetPass, setResetPass] = useState('')

  const flash = (m: string) => { setMsg(m); setTimeout(() => setMsg(''), 2500) }

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
    if (!newUser.trim()) { flash('請輸入帳號'); return }
    try {
      await api.post('/admin/users', { username: newUser.trim(), password: newPass || '123456', nickname: newNick.trim() })
      flash('已新增'); setShowAdd(false); setNewUser(''); setNewPass(''); setNewNick(''); load()
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      flash(err.response?.data?.message || '新增失敗')
    }
  }

  const deleteUser = async (id: number, name: string) => {
    if (!confirm(`確定刪除「${name}」？此操作無法還原！`)) return
    try {
      await api.delete(`/admin/users/${id}`)
      flash('已刪除'); load()
    } catch {
      flash('刪除失敗（不可刪除 admin）')
    }
  }

  const toggleStatus = async (u: AdminUser) => {
    const action = u.isEnabled ? '停用' : '啟用'
    if (!confirm(`確定${action}帳號「${u.username}」？`)) return
    try {
      await api.put(`/admin/users/${u.id}/status`, { enabled: !u.isEnabled })
      flash(`已${action}`); load()
    } catch {
      flash(`${action}失敗`)
    }
  }

  const doResetPassword = async () => {
    if (!resetPass.trim()) { flash('請輸入新密碼'); return }
    try {
      await api.put(`/admin/users/${resetId}/password`, { newPassword: resetPass.trim() })
      flash(`已重設「${resetName}」的密碼`)
      setResetId(null); setResetPass('')
    } catch {
      flash('重設失敗')
    }
  }

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🔑 {S.navGmAdmin}</h1>

      {apiErr && (
        <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: '10px 16px', marginBottom: 16, color: 'var(--accent-red)', fontSize: 13 }}>
          ⚠️ {S.apiError}（Port 5050）
        </div>
      )}
      {msg && (
        <div style={{ background: 'rgba(86,196,118,.15)', border: '1px solid var(--accent-green)', borderRadius: 8, padding: '8px 16px', marginBottom: 16, color: 'var(--accent-green)', fontSize: 13 }}>
          {msg}
        </div>
      )}

      {/* 重設密碼 Modal */}
      {resetId !== null && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.55)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: 28, minWidth: 320 }}>
            <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 16 }}>🔐 重設密碼 — {resetName}</h3>
            <label style={{ fontSize: 13, color: 'var(--text-muted)', display: 'block', marginBottom: 4 }}>新密碼</label>
            <input
              type="password"
              value={resetPass}
              onChange={e => setResetPass(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && doResetPassword()}
              placeholder="輸入新密碼"
              style={{ width: '100%', marginBottom: 16 }}
              autoFocus
            />
            <div style={{ display: 'flex', gap: 10 }}>
              <button onClick={doResetPassword} style={{ flex: 1, background: 'var(--accent-blue)', color: '#fff', padding: '8px 0' }}>確認重設</button>
              <button onClick={() => { setResetId(null); setResetPass('') }} style={{ flex: 1, background: 'var(--bg-input)', border: '1px solid var(--border)', padding: '8px 0' }}>{S.cancel}</button>
            </div>
          </div>
        </div>
      )}

      <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>工具後台登入帳號管理</span>
        <button onClick={() => setShowAdd(true)} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 14px', fontSize: 13 }}>+ 新增帳號</button>
      </div>

      {showAdd && (
        <form onSubmit={addUser} style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 16 }}>
          <h3 style={{ fontSize: 13, fontWeight: 700, marginBottom: 12 }}>新增帳號</h3>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
            <input value={newUser} onChange={e => setNewUser(e.target.value)} placeholder="帳號" required style={{ width: 140 }} />
            <input type="password" value={newPass} onChange={e => setNewPass(e.target.value)} placeholder="密碼（留空預設 123456）" style={{ width: 200 }} />
            <input value={newNick} onChange={e => setNewNick(e.target.value)} placeholder="暱稱" style={{ width: 120 }} />
            <button type="submit" style={{ background: 'var(--accent-green)', color: '#fff', padding: '6px 14px' }}>確認新增</button>
            <button type="button" onClick={() => setShowAdd(false)} style={{ background: 'var(--bg-input)', border: '1px solid var(--border)', padding: '6px 14px' }}>{S.cancel}</button>
          </div>
        </form>
      )}

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '60px 1fr 1fr 100px 1fr', padding: '8px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>
          <span>ID</span><span>帳號</span><span>暱稱</span><span>狀態</span><span style={{ textAlign: 'right' }}>操作</span>
        </div>
        {loading
          ? <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</p>
          : list.length === 0
            ? <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.noData}</p>
            : list.map(u => (
              <div key={u.id} style={{ display: 'grid', gridTemplateColumns: '60px 1fr 1fr 100px 1fr', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, alignItems: 'center' }}>
                <span style={{ color: 'var(--text-muted)' }}>{u.id}</span>
                <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>
                  {u.username}
                  {u.username === 'admin' && (
                    <span style={{ fontSize: 10, background: 'rgba(74,158,255,.2)', color: 'var(--accent-blue)', padding: '1px 5px', borderRadius: 4, marginLeft: 6 }}>超管</span>
                  )}
                </span>
                <span style={{ color: 'var(--text-secondary)' }}>{u.nickname || S.em}</span>
                <span style={{ color: u.isEnabled ? 'var(--accent-green)' : 'var(--text-muted)' }}>
                  {u.isEnabled ? '✅ 啟用' : '🔴 停用'}
                </span>
                <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                  {u.username !== 'admin' && (
                    <button
                      onClick={() => toggleStatus(u)}
                      style={{ fontSize: 11, padding: '3px 8px', background: u.isEnabled ? 'rgba(245,101,101,.15)' : 'rgba(86,196,118,.15)', color: u.isEnabled ? 'var(--accent-red)' : 'var(--accent-green)', border: `1px solid ${u.isEnabled ? 'var(--accent-red)' : 'var(--accent-green)'}55`, borderRadius: 4 }}>
                      {u.isEnabled ? '停用' : '啟用'}
                    </button>
                  )}
                  <button
                    onClick={() => { setResetId(u.id); setResetName(u.username); setResetPass('') }}
                    style={{ fontSize: 11, padding: '3px 8px', background: 'rgba(74,158,255,.15)', color: 'var(--accent-blue)', border: '1px solid var(--accent-blue)44', borderRadius: 4 }}>
                    重設密碼
                  </button>
                  {u.username !== 'admin' && (
                    <button
                      onClick={() => deleteUser(u.id, u.username)}
                      style={{ fontSize: 11, padding: '3px 8px', background: 'rgba(245,101,101,.2)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)55', borderRadius: 4 }}>
                      刪除
                    </button>
                  )}
                </div>
              </div>
            ))
        }
      </div>
    </div>
  )
}
