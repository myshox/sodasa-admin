import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { S } from '../strings'

export default function LoginPage() {
  const [user, setUser] = useState('')
  const [pass, setPass] = useState('')
  const [err,  setErr]  = useState('')
  const [loading, setLoading] = useState(false)
  const nav = useNavigate()

  const login = async (e: React.FormEvent) => {
    e.preventDefault(); setErr(''); setLoading(true)
    try {
      const r = await api.post('/auth/login', { username: user, password: pass })
      localStorage.setItem('gm_token', r.data.token)
      localStorage.setItem('gm_user',  r.data.username)
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
          <input type="password" value={pass} onChange={e => setPass(e.target.value)}
            placeholder={S.loginPlhPass} style={{ width: '100%' }} />
        </div>
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
