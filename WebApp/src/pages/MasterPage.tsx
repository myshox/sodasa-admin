import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { S } from '../strings'

interface CharInfo {
  account: string; charName: string; isOnline: boolean
  gold: number; crystal: number; payTotal: number
  loginTime: string; isBanned: boolean; petCount: number
}
interface MasterInfo { masterName: string; chars: CharInfo[] }

export default function MasterPage() {
  const navigate = useNavigate()
  const [q,    setQ]    = useState('')
  const [info, setInfo] = useState<MasterInfo | null>(null)
  const [loading, setLoading] = useState(false)
  const [err, setErr] = useState('')

  const search = async () => {
    if (!q.trim()) return
    setLoading(true); setErr(''); setInfo(null)
    try {
      const r = await api.get(`/players/master/${encodeURIComponent(q.trim())}`)
      setInfo(r.data)
    } catch { setErr('找不到主帳號') }
    finally { setLoading(false) }
  }

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>👑 {S.navMaster}</h1>

      <div style={{ display: 'flex', gap: 10, marginBottom: 24, maxWidth: 480 }}>
        <input value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder="輸入主帳號名稱…" style={{ flex: 1 }} />
        <button onClick={search} style={{ background: 'var(--accent-blue)', color: '#fff' }}>
          {loading ? S.searching : `🔍 ${S.searchBtn}`}
        </button>
      </div>

      {err && <p style={{ color: 'var(--accent-red)', marginBottom: 16 }}>{err}</p>}

      {info && (
        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
          <div style={{ padding: '12px 16px', borderBottom: '1px solid var(--border)', background: 'var(--bg-sidebar)', display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ fontWeight: 700, color: 'var(--accent-blue)', fontSize: 15 }}>👑 {info.masterName}</span>
            <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>{info.chars.length} 個角色</span>
            <span style={{ color: 'var(--accent-green)', fontSize: 13, marginLeft: 4 }}>
              ({info.chars.filter(c => c.isOnline).length} 在線)
            </span>
          </div>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr style={{ background: 'var(--bg-input)' }}>
                  {['狀態', '帳號', '角色名', '寵物', '累積儲值', '最後登入', '操作'].map(h => (
                    <th key={h} style={{ padding: '8px 12px', textAlign: 'left', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' }}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {info.chars.map(c => (
                  <tr key={c.account} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '9px 12px' }}>
                      {c.isOnline
                        ? <span style={{ color: 'var(--accent-green)', fontSize: 11 }}>🟢 在線</span>
                        : c.isBanned
                          ? <span style={{ color: 'var(--accent-red)', fontSize: 11 }}>🔒 封禁</span>
                          : <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>⚫ 離線</span>}
                    </td>
                    <td style={{ padding: '9px 12px', fontWeight: 600 }}>{c.account}</td>
                    <td style={{ padding: '9px 12px', color: 'var(--text-secondary)' }}>{c.charName || S.em}</td>
                    <td style={{ padding: '9px 12px', color: 'var(--text-muted)' }}>🐾 {c.petCount}</td>
                    <td style={{ padding: '9px 12px', color: 'var(--accent-orange)' }}>
                      {c.payTotal > 0 ? `NT$ ${c.payTotal.toLocaleString()}` : '—'}
                    </td>
                    <td style={{ padding: '9px 12px', color: 'var(--text-muted)', fontSize: 12 }}>{c.loginTime || '—'}</td>
                    <td style={{ padding: '8px 12px' }}>
                      <div style={{ display: 'flex', gap: 4 }}>
                        <button onClick={() => navigate(`/players?q=${c.account}`)}
                          style={{ fontSize: 11, padding: '3px 7px', background: 'rgba(74,158,255,.15)', color: 'var(--accent-blue)', border: '1px solid var(--accent-blue)44', borderRadius: 4 }}>
                          資料
                        </button>
                        <button onClick={() => navigate(`/send?account=${c.account}&name=${encodeURIComponent(c.charName || c.account)}`)}
                          style={{ fontSize: 11, padding: '3px 7px', background: 'rgba(86,196,118,.15)', color: 'var(--accent-green)', border: '1px solid var(--accent-green)44', borderRadius: 4 }}>
                          發送
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}
