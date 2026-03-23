import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { S } from '../strings'
const REMEMBER_KEY = 'gm_remember'

export default function LoginPage() {
  const [user, setUser] = useState('')
  const [pass, setPass] = useState('')
  const [remember, setRemember] = useState(false)
  const [showPass, setShowPass] = useState(false)
  const [err, setErr] = useState('')
  const [loading, setLoading] = useState(false)
  const nav = useNavigate()

  useEffect(() => {
    try {
      const saved = localStorage.getItem(REMEMBER_KEY)
      if (saved) {
        const { u, p } = JSON.parse(saved)
        setUser(u || '')
        setPass(p || '')
        setRemember(true)
      }
    } catch {
      /* ignore */
    }
  }, [])

  const login = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr('')
    setLoading(true)
    try {
      const r = await api.post('/auth/login', { username: user, password: pass })
      localStorage.setItem('gm_token', r.data.token)
      localStorage.setItem('gm_user', r.data.username)
      if (remember) {
        localStorage.setItem(REMEMBER_KEY, JSON.stringify({ u: user, p: pass }))
      } else {
        localStorage.removeItem(REMEMBER_KEY)
      }
      nav('/')
    } catch {
      setErr(S.loginErr)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-root">
      <form onSubmit={login} className="login-card">
        <div className="login-brand">
          <div className="emoji">🍅</div>
          <h2>{S.loginTitle}</h2>
          <p>{S.loginSub}</p>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <label style={{ color: 'var(--text-secondary)', fontSize: 12, fontWeight: 600 }}>{S.loginUser}</label>
          <input
            value={user}
            onChange={e => setUser(e.target.value)}
            placeholder={S.loginPlhUser}
            autoFocus
            style={{ width: '100%' }}
            autoComplete="username"
          />
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <label style={{ color: 'var(--text-secondary)', fontSize: 12, fontWeight: 600 }}>{S.loginPass}</label>
          <div style={{ position: 'relative' }}>
            <input
              type={showPass ? 'text' : 'password'}
              value={pass}
              onChange={e => setPass(e.target.value)}
              placeholder={S.loginPlhPass}
              style={{ width: '100%', paddingRight: 44 }}
              autoComplete="current-password"
            />
            <button
              type="button"
              onClick={() => setShowPass(v => !v)}
              aria-label={showPass ? '隱藏密碼' : '顯示密碼'}
              style={{
                position: 'absolute',
                right: 6,
                top: '50%',
                transform: 'translateY(-50%)',
                background: 'var(--neu-bg-light)',
                border: 'none',
                cursor: 'pointer',
                color: 'var(--text-muted)',
                fontSize: 18,
                padding: '6px 8px',
                borderRadius: 8,
                boxShadow: 'var(--neu-shadow-inset-sm)',
              }}
            >
              {showPass ? '🙈' : '👁'}
            </button>
          </div>
        </div>

        <label
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 10,
            cursor: 'pointer',
            userSelect: 'none',
            marginTop: 4,
          }}
        >
          <input
            type="checkbox"
            checked={remember}
            onChange={e => setRemember(e.target.checked)}
            style={{ width: 18, height: 18, cursor: 'pointer' }}
          />
          <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>記住帳號密碼</span>
        </label>

        {err && (
          <p style={{ color: 'var(--accent-red)', fontSize: 13, textAlign: 'center', margin: 0 }}>{err}</p>
        )}

        <button type="submit" disabled={loading} className="login-submit primary">
          {loading ? S.loginLoading : S.loginBtn}
        </button>
      </form>
    </div>
  )
}
