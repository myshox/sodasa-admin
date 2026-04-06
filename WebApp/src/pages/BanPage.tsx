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
  const [banQ,        setBanQ]        = useState('')
  const [banTargets,  setBanTargets]  = useState<PlayerRow[]>([])
  const [banDays,     setBanDays]     = useState(0)
  const [banHours,    setBanHours]    = useState(0)   // 0 = 用天數模式
  const [banReason,   setBanReason]   = useState('')
  const [customDays,  setCustomDays]  = useState<number | ''>('')

  const load = async (q = '') => {
    setLoading(true)
    try {
      const r = await api.get('/players/banned', { params: q ? { q } : {} })
      setList(r.data)
    } catch { } finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const flash = (m: string) => { setMsg(m); setTimeout(() => setMsg(''), 2500) }

  const unban = async (account: string) => {
    if (!window.confirm(`確定解除封禁帳號「${account}」？`)) return
    try {
      await api.post(`/players/${encodeURIComponent(account)}/ban`, { ban: false, days: 0 })
      setList(prev => prev.filter(x => x.account !== account))
      flash(S.unbanned)
    } catch { flash('解封失敗') }
  }

  const addBanTargets = (players: PlayerRow[]) => {
    setBanTargets(prev => {
      const existing = new Set(prev.map(p => p.account))
      return [...prev, ...players.filter(p => !existing.has(p.account))]
    })
    setBanQ('')
  }

  const doBan = async () => {
    const targets = banTargets.length > 0 ? banTargets : (banQ.trim() ? [{ account: banQ.trim(), onlineName: '', isOnline: false, vipLevel: 0, isBanned: false }] : [])
    if (targets.length === 0) return
    const days  = banHours > 0 ? 0 : (customDays !== '' ? Number(customDays) : banDays)
    const hours = banHours > 0 ? banHours : 0
    const isPerm = hours === 0 && days === 0

    // 列出前 5 個目標
    const nameList = targets.slice(0, 5).map(t => `• ${t.onlineName || t.account}（${t.account}）`).join('\n')
    const moreNote = targets.length > 5 ? `\n  …（共 ${targets.length} 位）` : ''

    if (isPerm) {
      // 永久封禁：需輸入「永久封禁」才能執行
      const confirmInput = window.prompt(
        `⚠ 永久封禁警告！此操作需手動解封。\n\n` +
        `封禁目標（${targets.length} 位）：\n${nameList}${moreNote}\n\n` +
        `請輸入「永久封禁」確認執行：`
      )
      if (confirmInput !== '永久封禁') {
        if (confirmInput !== null) alert('輸入不符，操作已取消')
        return
      }
    } else {
      const durText = hours > 0 ? `${hours} 小時` : `${days} 天`
      if (!window.confirm(
        `確定封禁以下玩家（${durText}）？\n\n${nameList}${moreNote}\n\n` +
        `原因：${banReason.trim() || 'GM 封禁'}`
      )) return
    }

    try {
      for (const t of targets) {
        await api.post(`/players/${encodeURIComponent(t.account)}/ban`, {
          ban: true, days, hours, reason: banReason.trim()
        })
      }
      flash(targets.length > 1 ? `已封禁 ${targets.length} 位玩家` : S.banned)
    } catch { flash('封禁操作失敗') }
    setBanQ(''); setBanTargets([]); setBanReason(''); setBanHours(0); load(listQ)
  }

  const effectiveDays  = customDays !== '' ? Number(customDays) : banDays
  const effectiveLabel = banHours > 0
    ? `封禁 ${banHours} 小時`
    : effectiveDays === 0 ? '永久封禁' : `封禁 ${effectiveDays} 天`

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🔒 {S.pageBan}</h1>

      {msg && <div style={{ background: 'rgba(86,196,118,.15)', border: '1px solid var(--accent-green)', borderRadius: 8, padding: '8px 16px', marginBottom: 16, color: 'var(--accent-green)', fontSize: 13 }}>{msg}</div>}

      {/* 快速封號 */}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 20 }}>
        <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-red)', marginBottom: 12 }}>🚫 快速封號</h3>
        <div style={{ display: 'flex', gap: 10, marginBottom: 10, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <div style={{ flex: 1 }}>
            <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>玩家帳號（主帳號可複選）</span>
            <PlayerAutocomplete
              value={banQ}
              onChange={setBanQ}
              onSelect={(p: PlayerRow) => addBanTargets([p])}
              onSelectMulti={addBanTargets}
              placeholder="主帳號 / 角色名 / UID…"
              style={{ marginTop: 2 }}
            />
            {banTargets.length > 0 && (
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 5, marginTop: 6 }}>
                {banTargets.map(p => (
                  <span key={p.account} style={{
                    display: 'inline-flex', alignItems: 'center', gap: 4,
                    background: 'rgba(245,101,101,.15)', border: '1px solid rgba(245,101,101,.4)',
                    borderRadius: 20, padding: '2px 8px', fontSize: 12, color: 'var(--accent-red)'
                  }}>
                    {p.onlineName || p.account}
                    <button onClick={() => setBanTargets(prev => prev.filter(x => x.account !== p.account))}
                      style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'inherit', fontSize: 13, lineHeight: 1, padding: 0 }}>✕</button>
                  </span>
                ))}
                <button onClick={() => setBanTargets([])}
                  style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer' }}>清除全部</button>
              </div>
            )}
          </div>
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
        <button onClick={doBan} disabled={banTargets.length === 0 && !banQ.trim()}
          style={{ background: 'var(--accent-red)', color: '#fff', padding: '8px 24px', opacity: (banTargets.length === 0 && !banQ.trim()) ? 0.5 : 1 }}>
          {banTargets.length > 1 ? `🚫 封禁 ${banTargets.length} 位玩家` : '🚫 確定封號'}
        </button>
      </div>

      {/* 封號清單 */}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 16px', borderBottom: '1px solid var(--border)', gap: 10, flexWrap: 'wrap' }}>
          <span style={{ fontWeight: 600, fontSize: 14, flex: '1 1 auto', minWidth: 0 }}>{S.banAll}（{list.length} 人）</span>
          <div className="gm-search-bar__actions" style={{ flex: '1 1 260px', justifyContent: 'flex-end', minWidth: 0, maxWidth: '100%' }}>
            <input
              className="gm-search-input"
              value={listQ}
              onChange={e => setListQ(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && load(listQ)}
              placeholder="搜尋帳號／角色名"
              style={{ flex: '1 1 160px', minWidth: 120 }}
              enterKeyHint="search"
            />
            <button type="button" onClick={() => load(listQ)} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '10px 16px', borderRadius: 10, fontWeight: 700 }}>🔍</button>
            <button type="button" onClick={() => { setListQ(''); load('') }} style={{ background: 'var(--bg-input)', color: 'var(--text-secondary)', border: '1px solid var(--border)', padding: '10px 14px', borderRadius: 10 }}>全部</button>
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
