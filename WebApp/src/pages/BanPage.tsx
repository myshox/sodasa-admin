import { useEffect, useState } from 'react'
import api from '../api'
import { S } from '../strings'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import type { PlayerRow } from '../api'

interface BanRow { account: string; charName: string; endTime: string; isPermanent: boolean }

const BAN_HOUR_PRESETS = [1, 6, 12, 24]
const BAN_DAY_PRESETS = [
  { label: '1天', days: 1 }, { label: '3天', days: 3 }, { label: '7天', days: 7 },
  { label: '14天', days: 14 }, { label: '30天', days: 30 }, { label: '永久', days: 0 },
]

export default function BanPage() {
  const [list,    setList]    = useState<BanRow[]>([])
  const [loading, setLoading] = useState(true)
  const [msg,     setMsg]     = useState('')
  const [listQ,   setListQ]   = useState('')

  // 快速封號
  const [banQ,      setBanQ]      = useState('')
  const [banDays,   setBanDays]   = useState(0)
  const [banHours,  setBanHours]  = useState(0)   // 0 = 用天數模式
  const [banReason, setBanReason] = useState('')
  const [customDays, setCustomDays] = useState<number | ''>('')

  const load = async (q = '') => {
    setLoading(true)
    try {
      const r = await api.get('/players/banned', { params: q ? { q } : {} })
      setList(r.data)
    } finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const flash = (m: string) => { setMsg(m); setTimeout(() => setMsg(''), 2500) }

  const unban = async (account: string) => {
    await api.post(`/players/${encodeURIComponent(account)}/ban`, { ban: false, days: 0 })
    setList(list.filter(x => x.account !== account))
    flash(S.unbanned)
  }

  const doBan = async () => {
    if (!banQ.trim()) return
    const days  = banHours > 0 ? 0 : (customDays !== '' ? Number(customDays) : banDays)
    const hours = banHours > 0 ? banHours : 0
    await api.post(`/players/${encodeURIComponent(banQ.trim())}/ban`, {
      ban: true, days, hours, reason: banReason.trim()
    })
    flash(S.banned); setBanQ(''); setBanReason(''); setBanHours(0); load(listQ)
  }

  const effectiveDays  = customDays !== '' ? Number(customDays) : banDays
  const effectiveLabel = banHours > 0
    ? `封禁 ${banHours} 小時`
    : effectiveDays === 0 ? '永久封禁' : `封禁 ${effectiveDays} 天`

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🔒 {S.pageBan}</h1>

      {msg && <div style={{ background: 'rgba(86,196,118,.15)', border: '1px solid var(--accent-green)', borderRadius: 8, padding: '8px 16px', marginBottom: 16, color: 'var(--accent-green)', fontSize: 13 }}>{msg}</div>}

      {/* 快速封號 */}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 20 }}>
        <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-red)', marginBottom: 12 }}>🚫 快速封號</h3>
        <div style={{ display: 'flex', gap: 10, marginBottom: 10, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <label style={{ flex: 1 }}>
            <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>玩家帳號</span>
            <PlayerAutocomplete
              value={banQ}
              onChange={setBanQ}
              onSelect={(p: PlayerRow) => setBanQ(p.account)}
              placeholder="輸入帳號或角色名稱…"
              style={{ marginTop: 2 }}
            />
          </label>
          <label style={{ flex: 2 }}>
            <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>封禁原因（選填）</span>
            <input value={banReason} onChange={e => setBanReason(e.target.value)}
              placeholder="封禁原因" style={{ width: '100%', marginTop: 2 }} />
          </label>
        </div>
        <div style={{ marginBottom: 10 }}>
          <span style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4, display: 'block' }}>小時</span>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 8 }}>
            {BAN_HOUR_PRESETS.map(h => (
              <button key={`h${h}`} onClick={() => { setBanHours(h); setBanDays(-1); setCustomDays('') }}
                style={{ padding: '4px 10px', fontSize: 12, background: banHours === h ? 'var(--accent-red)' : 'var(--bg-input)', color: banHours === h ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 6 }}>
                {h}時
              </button>
            ))}
          </div>
          <span style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4, display: 'block' }}>天數 / 永久</span>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', alignItems: 'center' }}>
            {BAN_DAY_PRESETS.map(p => (
              <button key={p.days} onClick={() => { setBanDays(p.days); setBanHours(0); setCustomDays('') }}
                style={{ padding: '5px 12px', fontSize: 13, background: (banDays === p.days && customDays === '' && banHours === 0) ? 'var(--accent-red)' : 'var(--bg-input)', color: (banDays === p.days && customDays === '' && banHours === 0) ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 6 }}>
                {p.label}
              </button>
            ))}
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>自訂：</span>
              <input type="number" value={customDays}
                onChange={e => { setCustomDays(e.target.value === '' ? '' : +e.target.value); setBanDays(-1); setBanHours(0) }}
                placeholder="天" min={1} style={{ width: 60, textAlign: 'center' }} />
              <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>天</span>
            </div>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 6 }}>
            選擇：{effectiveLabel}
          </div>
        </div>
        <button onClick={doBan} disabled={!banQ.trim()}
          style={{ background: 'var(--accent-red)', color: '#fff', padding: '8px 24px', opacity: !banQ.trim() ? 0.5 : 1 }}>
          🚫 確定封號
        </button>
      </div>

      {/* 封號清單 */}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 16px', borderBottom: '1px solid var(--border)', gap: 10 }}>
          <span style={{ fontWeight: 600, fontSize: 14 }}>{S.banAll}（{list.length} 人）</span>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
            <input value={listQ} onChange={e => setListQ(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && load(listQ)}
              placeholder="搜尋帳號/角色名" style={{ width: 180 }} />
            <button onClick={() => load(listQ)} style={{ background: 'var(--accent-blue)', color: '#fff', fontSize: 12, padding: '5px 12px' }}>🔍</button>
            <button onClick={() => { setListQ(''); load('') }} style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', border: '1px solid var(--border)', fontSize: 12 }}>全部</button>
          </div>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr auto', padding: '8px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>
          <span>{S.banAccount}</span><span>角色名稱</span><span>{S.banEndTime}</span><span></span>
        </div>
        {loading
          ? <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</p>
          : list.length === 0
            ? <p style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.noBanned}</p>
            : list.map(p => (
              <div key={p.account} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr auto', padding: '10px 16px', borderBottom: '1px solid var(--border)', alignItems: 'center', fontSize: 13 }}>
                <span style={{ fontWeight: 600 }}>{p.account}</span>
                <span style={{ color: 'var(--text-secondary)' }}>{p.charName || S.em}</span>
                <span style={{ color: p.isPermanent ? 'var(--accent-red)' : 'var(--accent-orange)' }}>
                  {p.isPermanent ? S.banPermanent : p.endTime}
                </span>
                <button onClick={() => unban(p.account)} style={{ background: 'rgba(86,196,118,.2)', color: 'var(--accent-green)', border: '1px solid var(--accent-green)', fontSize: 12, padding: '3px 10px' }}>
                  {S.unbanBtn}
                </button>
              </div>
            ))}
      </div>
    </div>
  )
}
