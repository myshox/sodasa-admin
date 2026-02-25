import { useState } from 'react'
import api from '../api'
import type { PlayerRow, PlayerDetail } from '../api'
import { S } from '../strings'

export default function PlayersPage() {
  const [q,       setQ]       = useState('')
  const [players, setPlayers] = useState<PlayerRow[]>([])
  const [detail,  setDetail]  = useState<PlayerDetail | null>(null)
  const [loading, setLoading] = useState(false)
  const [msg,     setMsg]     = useState('')
  const [goldVal, setGoldVal] = useState('')
  const [crysVal, setCrysVal] = useState('')
  const [banDays, setBanDays] = useState(0)
  const [showBan, setShowBan] = useState(false)

  const flash = (m: string) => { setMsg(m); setTimeout(() => setMsg(''), 2500) }

  const search = async () => {
    if (!q.trim()) return
    setLoading(true); setDetail(null)
    try {
      const r = await api.get('/players/search', { params: { q } })
      setPlayers(r.data)
    } finally { setLoading(false) }
  }

  const loadDetail = async (account: string) => {
    const r = await api.get(`/players/${account}`)
    const d = r.data as PlayerDetail
    setDetail(d)
    setGoldVal(String(d.gold))
    setCrysVal(String(d.crystal))
    setShowBan(false)
  }

  const addGold = async (delta: number) => {
    if (!detail) return
    const val = Math.max(0, detail.gold + delta)
    await api.put(`/players/${detail.account}/gold`, { value: val })
    setDetail({ ...detail, gold: val }); setGoldVal(String(val))
    flash(S.updGold)
  }

  const saveGold = async () => {
    if (!detail) return
    const val = parseInt(goldVal) || 0
    await api.put(`/players/${detail.account}/gold`, { value: val })
    setDetail({ ...detail, gold: val })
    flash(S.updGold)
  }

  const addCrystal = async (delta: number) => {
    if (!detail) return
    const val = Math.max(0, detail.crystal + delta)
    await api.put(`/players/${detail.account}/crystal`, { value: val })
    setDetail({ ...detail, crystal: val }); setCrysVal(String(val))
    flash(S.updCrystal)
  }

  const saveCrystal = async () => {
    if (!detail) return
    const val = parseInt(crysVal) || 0
    await api.put(`/players/${detail.account}/crystal`, { value: val })
    setDetail({ ...detail, crystal: val })
    flash(S.updCrystal)
  }

  const doBan = async (ban: boolean) => {
    if (!detail) return
    await api.post(`/players/${detail.account}/ban`, { ban, days: ban ? banDays : 0 })
    setDetail({ ...detail, isBanned: ban })
    setShowBan(false)
    flash(ban ? S.banned : S.unbanned)
  }

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>
        &#128101; {S.pagePlayerMgr}
      </h1>

      <div style={{ display: 'flex', gap: 10, marginBottom: 20 }}>
        <input value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder={S.searchPlh}
          style={{ flex: 1, maxWidth: 400 }} />
        <button onClick={search}
          style={{ background: 'var(--accent-blue)', color: '#fff' }}>
          {loading ? S.searching : `&#128269; ${S.searchBtn}`}
        </button>
      </div>

      <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start' }}>
        {/* ???? */}
        <div style={{
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          borderRadius: 10, flex: 1, overflow: 'hidden', minWidth: 0
        }}>
          {players.length === 0
            ? <p style={{ padding: 24, color: 'var(--text-muted)', textAlign: 'center' }}>
                {S.searchHint}
              </p>
            : players.map(p => (
              <div key={p.account} onClick={() => loadDetail(p.account)}
                style={{
                  padding: '12px 16px', borderBottom: '1px solid var(--border)',
                  cursor: 'pointer', display: 'flex',
                  justifyContent: 'space-between', alignItems: 'center',
                  background: detail?.account === p.account ? 'rgba(74,158,255,.08)' : 'transparent',
                }}>
                <div>
                  <span style={{ fontWeight: 600, color: 'var(--text-primary)', marginRight: 10 }}>
                    {p.onlineName || p.account}
                  </span>
                  <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{p.account}</span>
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                  {p.isOnline && <Tag text={S.tagOnline} color="var(--accent-green)" />}
                  {p.isBanned && <Tag text={S.tagBanned} color="var(--accent-red)" />}
                  <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
                    &#128176; {p.gold.toLocaleString()}
                  </span>
                </div>
              </div>
            ))}
        </div>

        {/* ???? */}
        {detail && (
          <div style={{
            width: 340, background: 'var(--bg-card)',
            border: '1px solid var(--border)',
            borderRadius: 10, padding: 20, flexShrink: 0
          }}>
            {msg && (
              <p style={{ color: 'var(--accent-green)', fontSize: 13, marginBottom: 10, textAlign: 'center' }}>
                {msg}
              </p>
            )}

            <div style={{ marginBottom: 14 }}>
              <div style={{ fontWeight: 700, fontSize: 18, color: 'var(--text-primary)' }}>
                {detail.onlineName}
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>{detail.account}</div>
              <div style={{ marginTop: 6, display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {detail.isOnline && <Tag text={S.tagOnline} color="var(--accent-green)" />}
                {detail.isBanned && <Tag text={S.tagBanned} color="var(--accent-red)" />}
                {detail.isMuted  && <Tag text={S.tagMuted}  color="var(--accent-orange)" />}
              </div>
            </div>

            {/* ?? */}
            <SectionLabel label={`&#128176; ${S.gold}`} />
            <div style={{ display: 'flex', gap: 6, marginBottom: 4 }}>
              <input value={goldVal} onChange={e => setGoldVal(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && saveGold()}
                style={{ flex: 1, minWidth: 0 }} />
              <button onClick={saveGold}
                style={{ background: 'var(--accent-blue)', color: '#fff', padding: '5px 10px', fontSize: 12 }}>
                {S.setGold}
              </button>
            </div>
            <div style={{ display: 'flex', gap: 5, marginBottom: 14, flexWrap: 'wrap' }}>
              {[100, 1000, 10000, 100000].map(d => (
                <button key={d} onClick={() => addGold(d)}
                  style={{ background: 'var(--bg-input)', color: 'var(--accent-green)', fontSize: 12, padding: '3px 8px' }}>
                  +{d >= 10000 ? `${d / 10000}${S.tenK}` : d >= 1000 ? `${d / 1000}${S.oneK}` : d}
                </button>
              ))}
              {[100, 1000, 10000].map(d => (
                <button key={-d} onClick={() => addGold(-d)}
                  style={{ background: 'var(--bg-input)', color: 'var(--accent-red)', fontSize: 12, padding: '3px 8px' }}>
                  -{d >= 10000 ? `${d / 10000}${S.tenK}` : d >= 1000 ? `${d / 1000}${S.oneK}` : d}
                </button>
              ))}
            </div>

            {/* ?? */}
            <SectionLabel label={`&#128142; ${S.crystal}`} />
            <div style={{ display: 'flex', gap: 6, marginBottom: 4 }}>
              <input value={crysVal} onChange={e => setCrysVal(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && saveCrystal()}
                style={{ flex: 1, minWidth: 0 }} />
              <button onClick={saveCrystal}
                style={{ background: 'var(--accent-blue)', color: '#fff', padding: '5px 10px', fontSize: 12 }}>
                {S.setCrystal}
              </button>
            </div>
            <div style={{ display: 'flex', gap: 5, marginBottom: 14, flexWrap: 'wrap' }}>
              {[100, 1000, 10000, 100000].map(d => (
                <button key={d} onClick={() => addCrystal(d)}
                  style={{ background: 'var(--bg-input)', color: 'var(--accent-green)', fontSize: 12, padding: '3px 8px' }}>
                  +{d >= 10000 ? `${d / 10000}${S.tenK}` : d >= 1000 ? `${d / 1000}${S.oneK}` : d}
                </button>
              ))}
            </div>

            {/* ???? */}
            <div style={{ marginBottom: 12 }}>
              <Row label={S.regIP}    value={detail.regIP} />
              <Row label={S.lastIP}   value={detail.ip} />
              <Row label={S.regTime}  value={detail.regTime} />
              <Row label={S.loginTime}value={detail.loginTime} />
              <Row label={S.unreadMail} value={`${detail.unreadMails} / ${detail.totalMails}`} />
              <Row label={S.petCount} value={`${detail.petCount} \u96BB`} />
            </div>

            {/* ?? */}
            {!showBan
              ? (
                <div style={{ display: 'flex', gap: 8 }}>
                  {detail.isBanned
                    ? <button onClick={() => doBan(false)} style={{
                        flex: 1, padding: '8px 0',
                        background: 'rgba(86,196,118,.2)', color: 'var(--accent-green)',
                        border: '1px solid var(--accent-green)'
                      }}>{S.unbanBtn}</button>
                    : <button onClick={() => setShowBan(true)} style={{
                        flex: 1, padding: '8px 0',
                        background: 'rgba(245,101,101,.2)', color: 'var(--accent-red)',
                        border: '1px solid var(--accent-red)'
                      }}>&#128683; {S.banBtn}</button>
                  }
                </div>
              )
              : (
                <div style={{
                  background: 'rgba(245,101,101,.08)', border: '1px solid var(--accent-red)',
                  borderRadius: 8, padding: 12
                }}>
                  <p style={{ fontSize: 12, color: 'var(--accent-red)', marginBottom: 8 }}>
                    {S.banDays}
                  </p>
                  <input type="number" value={banDays} onChange={e => setBanDays(+e.target.value)}
                    min={0} style={{ width: '100%', marginBottom: 8 }} />
                  <div style={{ display: 'flex', gap: 8 }}>
                    <button onClick={() => doBan(true)} style={{
                      flex: 1, background: 'var(--accent-red)', color: '#fff'
                    }}>&#128683; {S.confirm}</button>
                    <button onClick={() => setShowBan(false)} style={{
                      flex: 1, background: 'var(--bg-input)',
                      color: 'var(--text-secondary)', border: '1px solid var(--border)'
                    }}>{S.cancel}</button>
                  </div>
                </div>
              )
            }
          </div>
        )}
      </div>
    </div>
  )
}

const Tag = ({ text, color }: { text: string; color: string }) => (
  <span style={{ fontSize: 11, color, background: `${color}22`, padding: '2px 8px', borderRadius: 20 }}>
    {text}
  </span>
)
const SectionLabel = ({ label }: { label: string }) => (
  <div style={{ color: 'var(--text-muted)', fontSize: 12, marginBottom: 4 }}
    dangerouslySetInnerHTML={{ __html: label }} />
)
const Row = ({ label, value }: { label: string; value: string }) => (
  <div style={{
    display: 'flex', justifyContent: 'space-between',
    padding: '4px 0', borderBottom: '1px solid var(--border)', fontSize: 13
  }}>
    <span style={{ color: 'var(--text-muted)' }}>{label}</span>
    <span style={{ color: 'var(--text-primary)' }}>{value || S.em}</span>
  </div>
)
