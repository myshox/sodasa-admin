import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { S } from '../strings'

const REMEMBER_KEY = 'gm_remember'

export default function LoginPage() {
  const [user,     setUser]     = useState('')
  const [pass,     setPass]     = useState('')
  const [remember, setRemember] = useState(false)
  const [showPass, setShowPass] = useState(false)
  const [err,      setErr]      = useState('')
  const [loading,  setLoading]  = useState(false)
  const nav = useNavigate()

  // 頁面載入時讀取記住的帳密
  useEffect(() => {
    try {
      const saved = localStorage.getItem(REMEMBER_KEY)
      if (saved) {
        const { u, p } = JSON.parse(saved)
        setUser(u || ''); setPass(p || ''); setRemember(true)
      }
    } catch { /* ignore */ }
  }, [])

  const login = async (e: React.FormEvent) => {
    e.preventDefault(); setErr(''); setLoading(true)
    try {
      const r = await api.post('/auth/login', { username: user, password: pass })
      localStorage.setItem('gm_token', r.data.token)
      localStorage.setItem('gm_user',  r.data.username)
      if (remember) {
        localStorage.setItem(REMEMBER_KEY, JSON.stringify({ u: user, p: pass }))
      } else {
        localStorage.removeItem(REMEMBER_KEY)
      }
      nav('/')
    } catch {
      setErr(S.loginErr)
    } finally { setLoading(false) }
  }

  return (
    <div style={{
      minHeight: '100vh', display: 'flex', alignItems: 'center',
      justifyContent: 'center', background: 'var(--bg-page)'
    }}>
      <form onSubmit={login} style={{
        background: 'var(--bg-card)', border: '1px solid var(--border)',
        borderRadius: 12, padding: '40px 48px', width: 360,
        display: 'flex', flexDirection: 'column', gap: 18
      }}>
        <div style={{ textAlign: 'center', marginBottom: 8 }}>
          <div style={{ fontSize: 36 }}>🍅</div>
          <h2 style={{ fontSize: 20, fontWeight: 700, color: 'var(--text-primary)' }}>{S.loginTitle}</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 12, marginTop: 4 }}>{S.loginSub}</p>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{S.loginUser}</label>
          <input value={user} onChange={e => setUser(e.target.value)}
            placeholder={S.loginPlhUser} autoFocus style={{ width: '100%' }} />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label style={{ color: 'var(--text-secondary)', fontSize: 12 }}>{S.loginPass}</label>
          <div style={{ position: 'relative' }}>
            <input type={showPass ? 'text' : 'password'} value={pass}
              onChange={e => setPass(e.target.value)}
              placeholder={S.loginPlhPass} style={{ width: '100%', paddingRight: 38 }} />
            <button type="button" onClick={() => setShowPass(v => !v)}
              style={{
                position: 'absolute', right: 8, top: '50%', transform: 'translateY(-50%)',
                background: 'none', border: 'none', cursor: 'pointer',
                color: 'var(--text-muted)', fontSize: 15, padding: '2px 4px'
              }}>
              {showPass ? '🙈' : '👁'}
            </button>
          </div>
        </div>
        <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', userSelect: 'none' }}>
          <input type="checkbox" checked={remember} onChange={e => setRemember(e.target.checked)}
            style={{ width: 15, height: 15, cursor: 'pointer' }} />
          <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>記住帳號密碼</span>
        </label>
        {err && <p style={{ color: 'var(--accent-red)', fontSize: 12, textAlign: 'center' }}>{err}</p>}
        <button type="submit" disabled={loading} style={{
          background: 'var(--accent-blue)', color: '#fff',
          padding: '10px 0', fontSize: 15, marginTop: 4
        }}>
          {loading ? S.loginLoading : S.loginBtn}
        </button>
      </form>
    </div>
  )
}
