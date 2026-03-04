import { useState, useEffect, useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import type { PlayerRow } from '../api'

const MILESTONES = [3_000, 5_000, 10_000, 50_000, 100_000]

interface MilestoneInfo {
  index: number
  required: number
  reached: boolean
  claimed: boolean
}
interface CostInfo {
  account: string
  onlineName: string
  masterAccount?: string
  isOnline?: boolean
  costPoint: number
  costCheck: number
  claimedCount: number
  milestones: MilestoneInfo[]
  lastTime?: string
}

// ── 里程碑卡片 ─────────────────────────────────────────────────
const MilestoneCard = ({
  m, onClaim, loading,
}: { m: MilestoneInfo; onClaim: (idx: number) => void; loading: boolean }) => {
  const stateColor = m.claimed ? '#16b97a' : m.reached ? '#fbbf24' : '#475569'
  const stateLabel = m.claimed ? '✅ 已領取' : m.reached ? '🎁 可領取' : '🔒 未達成'
  const canClaim   = m.reached && !m.claimed
  return (
    <div style={{
      background: 'var(--bg-card)', border: `1px solid ${canClaim ? '#fbbf24' : 'var(--border)'}`,
      borderRadius: 10, padding: '14px 16px',
      boxShadow: canClaim ? '0 0 12px rgba(251,191,36,.2)' : 'none', transition: 'all .2s',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-secondary)' }}>里程碑 {m.index + 1}</div>
        <span style={{
          fontSize: 11, fontWeight: 700, padding: '2px 8px', borderRadius: 4,
          background: m.claimed ? 'rgba(22,185,122,.15)' : m.reached ? 'rgba(251,191,36,.15)' : 'rgba(71,85,105,.2)',
          color: stateColor
        }}>{stateLabel}</span>
      </div>
      <div style={{ fontSize: 22, fontWeight: 800, color: stateColor, lineHeight: 1 }}>
        {m.required.toLocaleString()}
      </div>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 4 }}>金幣消費達成</div>
      {canClaim && (
        <button onClick={() => onClaim(m.index)} disabled={loading} style={{
          marginTop: 10, width: '100%', padding: '7px 0', borderRadius: 6,
          background: 'rgba(251,191,36,.2)', border: '1px solid rgba(251,191,36,.5)',
          color: '#fbbf24', fontWeight: 700, fontSize: 12, cursor: 'pointer',
        }}>🎁 補發此獎勵</button>
      )}
    </div>
  )
}

// ── 角色列表卡（主帳號有多角色時顯示）────────────────────────────
const CharCard = ({ c, onSelect }: { c: CostInfo; onSelect: () => void }) => {
  const maxPct = Math.min(100, (c.costPoint / 100_000) * 100)
  const claimable = c.milestones.filter(m => m.reached && !m.claimed).length
  return (
    <div onClick={onSelect} style={{
      background: 'var(--bg-card)', border: `1px solid ${claimable > 0 ? 'rgba(251,191,36,.4)' : 'var(--border)'}`,
      borderRadius: 10, padding: '14px 16px', cursor: 'pointer', transition: 'all .2s',
    }}
    onMouseEnter={e => (e.currentTarget.style.borderColor = '#7c3aed')}
    onMouseLeave={e => (e.currentTarget.style.borderColor = claimable > 0 ? 'rgba(251,191,36,.4)' : 'var(--border)')}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 8 }}>
        <div>
          <span style={{ fontWeight: 700, fontSize: 14, color: 'var(--text-primary)' }}>
            {c.onlineName || '（無角色名）'}
          </span>
          {c.isOnline && <span style={{ marginLeft: 6, fontSize: 10, color: '#16b97a', fontWeight: 700 }}>● 線上</span>}
          <div style={{ fontSize: 11, color: '#60a5fa', marginTop: 2 }}>CDKEY：{c.account}</div>
          {c.masterAccount && (
            <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 1 }}>主帳號：{c.masterAccount}</div>
          )}
        </div>
        {claimable > 0 && (
          <span style={{
            fontSize: 11, fontWeight: 700, padding: '2px 8px', borderRadius: 10,
            background: 'rgba(251,191,36,.15)', color: '#fbbf24',
          }}>🎁 {claimable} 待補發</span>
        )}
      </div>
      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>
        <span>已領 {c.claimedCount}/5</span>
        <span style={{ fontWeight: 700, color: '#b87fff' }}>{c.costPoint.toLocaleString()} 金幣</span>
      </div>
      <div style={{ height: 6, background: 'var(--bg-mid)', borderRadius: 3, overflow: 'hidden' }}>
        <div style={{
          width: `${maxPct}%`, height: '100%', borderRadius: 3,
          background: 'linear-gradient(90deg,#b87fff,#7c3aed)', minWidth: c.costPoint > 0 ? 4 : 0
        }} />
      </div>
    </div>
  )
}

// ── 全服覽覽 Tab ────────────────────────────────────────────────
function BatchView() {
  const [rows, setRows]           = useState<CostInfo[]>([])
  const [loading, setLoading]     = useState(false)
  const [onlineOnly, setOnlineOnly] = useState(false)
  const [selected, setSelected]   = useState<Set<string>>(new Set())
  const [filter, setFilter]       = useState('')
  const [msg, setMsg]             = useState('')
  const [msgOk, setMsgOk]         = useState(true)
  const [sortBy, setSortBy]       = useState<'point' | 'name' | 'claimed'>('point')

  const loadBatch = useCallback(async (online: boolean) => {
    setLoading(true); setRows([]); setSelected(new Set()); setMsg('')
    try {
      const r = await api.get(`/players/costdata/list?online=${online}`)
      setRows(r.data)
    } catch { setMsg('載入失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { loadBatch(false) }, [])

  const filtered = rows.filter(r =>
    !filter || r.onlineName.includes(filter) || r.account.includes(filter) ||
    (r.masterAccount || '').includes(filter)
  )
  const sorted = [...filtered].sort((a, b) => {
    if (sortBy === 'point') return b.costPoint - a.costPoint
    if (sortBy === 'name') return (a.onlineName || a.account).localeCompare(b.onlineName || b.account)
    if (sortBy === 'claimed') return b.claimedCount - a.claimedCount
    return 0
  })

  const toggleAll = () => {
    if (selected.size === sorted.length) setSelected(new Set())
    else setSelected(new Set(sorted.map(r => r.account)))
  }
  const toggleOne = (acc: string) => {
    const s = new Set(selected)
    s.has(acc) ? s.delete(acc) : s.add(acc)
    setSelected(s)
  }

  const handleBatchReset = async (fullReset: boolean) => {
    if (selected.size === 0) { setMsg('請先選取玩家'); setMsgOk(false); return }
    const accounts = Array.from(selected)
    const kind = fullReset ? '🗑 完全重置（point+check 歸零，須重新消費）' : '🔄 重置已領狀態（點數保留，可立即重領）'
    if (!window.confirm(`${kind}\n\n已選 ${accounts.length} 個玩家，確定執行？\n\n⚠ 此操作無法復原！`)) return
    setLoading(true)
    try {
      const r = await api.post('/players/costdata/batch-reset', { accounts, fullReset })
      setMsg(r.data.message); setMsgOk(true)
      await loadBatch(onlineOnly)
    } catch (e: any) { setMsg(e.response?.data?.message || '批量操作失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const canClaim = (r: CostInfo) => r.milestones.some(m => m.reached && !m.claimed)

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      {/* 工具列 */}
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
        <div style={{ display: 'flex', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
          <button onClick={() => { setOnlineOnly(false); loadBatch(false) }} style={{
            padding: '8px 18px', border: 'none', cursor: 'pointer', fontWeight: 700, fontSize: 13,
            background: !onlineOnly ? '#1e4ba0' : 'var(--bg-input)', color: !onlineOnly ? '#fff' : 'var(--text-muted)',
          }}>🌐 全服</button>
          <button onClick={() => { setOnlineOnly(true); loadBatch(true) }} style={{
            padding: '8px 18px', border: 'none', cursor: 'pointer', fontWeight: 700, fontSize: 13,
            background: onlineOnly ? '#0d7c3a' : 'var(--bg-input)', color: onlineOnly ? '#fff' : 'var(--text-muted)',
          }}>🟢 線上玩家</button>
        </div>
        <input value={filter} onChange={e => setFilter(e.target.value)}
          placeholder="篩選角色名 / CDKEY / 主帳號…"
          style={{
            flex: 1, minWidth: 160, padding: '8px 12px', borderRadius: 8, fontSize: 13,
            background: 'var(--bg-input)', border: '1px solid var(--border)',
            color: 'var(--text-primary)', outline: 'none',
          }} />
        <select value={sortBy} onChange={e => setSortBy(e.target.value as any)} style={{
          padding: '8px 12px', borderRadius: 8, fontSize: 13,
          background: 'var(--bg-input)', border: '1px solid var(--border)', color: 'var(--text-primary)',
        }}>
          <option value="point">排序：消費點數高→低</option>
          <option value="claimed">排序：已領里程碑多→少</option>
          <option value="name">排序：角色名 A→Z</option>
        </select>
        <button onClick={() => loadBatch(onlineOnly)} disabled={loading} style={{
          padding: '8px 14px', borderRadius: 8, border: '1px solid var(--border)', cursor: 'pointer',
          background: 'var(--bg-input)', color: 'var(--text-muted)', fontSize: 13,
        }}>🔄 重新載入</button>
      </div>

      {/* 狀態訊息 */}
      {msg && (
        <div style={{
          padding: '10px 16px', borderRadius: 8, fontSize: 13,
          background: msgOk ? 'rgba(22,185,122,.1)' : 'rgba(245,101,101,.1)',
          border: `1px solid ${msgOk ? 'rgba(22,185,122,.3)' : 'rgba(245,101,101,.3)'}`,
          color: msgOk ? '#16b97a' : '#f87171',
        }}>{msg}</div>
      )}

      {/* 批量操作列 */}
      <div style={{
        display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap',
        padding: '12px 16px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10,
      }}>
        <span style={{ fontSize: 13, color: 'var(--text-secondary)', fontWeight: 600 }}>
          已選 <span style={{ color: selected.size > 0 ? '#fbbf24' : 'var(--text-muted)', fontWeight: 800 }}>{selected.size}</span> / {sorted.length} 筆
        </span>
        <button onClick={toggleAll} style={{
          padding: '6px 14px', borderRadius: 6, border: '1px solid var(--border)', cursor: 'pointer',
          background: 'var(--bg-input)', color: 'var(--text-muted)', fontSize: 12, fontWeight: 600,
        }}>{selected.size === sorted.length && sorted.length > 0 ? '☑ 取消全選' : '☐ 全選'}</button>
        <button onClick={() => setSelected(new Set(sorted.filter(canClaim).map(r => r.account)))} style={{
          padding: '6px 14px', borderRadius: 6, border: '1px solid rgba(251,191,36,.4)', cursor: 'pointer',
          background: 'rgba(251,191,36,.08)', color: '#fbbf24', fontSize: 12, fontWeight: 600,
        }}>🎁 選取待補發</button>
        <div style={{ flex: 1 }} />
        <button onClick={() => handleBatchReset(false)} disabled={loading || selected.size === 0} style={{
          padding: '7px 18px', borderRadius: 6, border: '1px solid rgba(251,191,36,.4)', cursor: 'pointer',
          background: 'rgba(251,191,36,.1)', color: '#fbbf24', fontWeight: 700, fontSize: 13,
          opacity: loading || selected.size === 0 ? 0.4 : 1,
        }}>🔄 批量重置已領</button>
        <button onClick={() => handleBatchReset(true)} disabled={loading || selected.size === 0} style={{
          padding: '7px 18px', borderRadius: 6, border: '1px solid rgba(248,113,113,.4)', cursor: 'pointer',
          background: 'rgba(248,113,113,.1)', color: '#f87171', fontWeight: 700, fontSize: 13,
          opacity: loading || selected.size === 0 ? 0.4 : 1,
        }}>🗑 批量完全重置</button>
      </div>

      {/* 資料表 */}
      {loading ? (
        <div style={{ textAlign: 'center', padding: 40, color: 'var(--text-muted)' }}>載入中…</div>
      ) : sorted.length === 0 ? (
        <div style={{ textAlign: 'center', padding: 40, color: 'var(--text-muted)' }}>
          {rows.length === 0 ? '尚未載入資料' : '無符合篩選條件的玩家'}
        </div>
      ) : (
        <div style={{ border: '1px solid var(--border)', borderRadius: 10, overflow: 'auto', maxHeight: 520 }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
            <thead>
              <tr style={{ background: 'var(--bg-sidebar)', position: 'sticky', top: 0, zIndex: 1 }}>
                <th style={TH}><input type="checkbox" checked={selected.size === sorted.length && sorted.length > 0}
                  onChange={toggleAll} /></th>
                <th style={TH}>角色名</th>
                <th style={TH}>CDKEY</th>
                <th style={TH}>主帳號</th>
                <th style={{ ...TH, textAlign: 'right' }}>累計金幣</th>
                <th style={{ ...TH, textAlign: 'center' }}>里程碑</th>
                <th style={TH}>狀態</th>
                <th style={TH}>最後更新</th>
              </tr>
            </thead>
            <tbody>
              {sorted.map((r, i) => {
                const isSel = selected.has(r.account)
                const hasPending = canClaim(r)
                return (
                  <tr key={r.account} onClick={() => toggleOne(r.account)}
                    style={{
                      borderBottom: '1px solid var(--border)', cursor: 'pointer',
                      background: isSel ? 'rgba(124,58,237,.12)' : hasPending ? 'rgba(251,191,36,.04)' : i % 2 === 0 ? 'transparent' : 'rgba(255,255,255,.02)',
                      transition: 'background .12s',
                    }}
                    onMouseEnter={e => { if (!isSel) e.currentTarget.style.background = 'rgba(255,255,255,.05)' }}
                    onMouseLeave={e => { e.currentTarget.style.background = isSel ? 'rgba(124,58,237,.12)' : hasPending ? 'rgba(251,191,36,.04)' : i % 2 === 0 ? 'transparent' : 'rgba(255,255,255,.02)' }}
                  >
                    <td style={{ ...TD, textAlign: 'center' }} onClick={e => e.stopPropagation()}>
                      <input type="checkbox" checked={isSel} onChange={() => toggleOne(r.account)} />
                    </td>
                    <td style={TD}>
                      <span style={{ fontWeight: 700, color: isSel ? '#b87fff' : 'var(--text-primary)' }}>{r.onlineName || '—'}</span>
                      {r.isOnline && <span style={{ marginLeft: 4, fontSize: 10, color: '#16b97a', fontWeight: 700 }}>●</span>}
                    </td>
                    <td style={{ ...TD, color: '#60a5fa', fontFamily: 'monospace', fontSize: 11 }}>{r.account}</td>
                    <td style={{ ...TD, color: 'var(--text-muted)', fontSize: 11 }}>{r.masterAccount || '—'}</td>
                    <td style={{ ...TD, textAlign: 'right', fontWeight: 700, color: '#b87fff' }}>
                      {r.costPoint.toLocaleString()}
                    </td>
                    <td style={{ ...TD, textAlign: 'center' }}>
                      <div style={{ display: 'flex', gap: 3, justifyContent: 'center' }}>
                        {r.milestones.map(m => (
                          <div key={m.index} title={`里程碑${m.index+1}：${m.required.toLocaleString()} 金幣`} style={{
                            width: 12, height: 12, borderRadius: 3,
                            background: m.claimed ? '#16b97a' : m.reached ? '#fbbf24' : 'var(--bg-mid)',
                            border: `1px solid ${m.claimed ? '#16b97a' : m.reached ? '#fbbf24' : 'var(--border)'}`,
                          }} />
                        ))}
                      </div>
                    </td>
                    <td style={TD}>
                      {hasPending && (
                        <span style={{ padding: '2px 7px', borderRadius: 10, fontSize: 10, fontWeight: 700,
                          background: 'rgba(251,191,36,.15)', color: '#fbbf24' }}>
                          🎁 待補發
                        </span>
                      )}
                    </td>
                    <td style={{ ...TD, color: 'var(--text-muted)', fontSize: 11 }}>{r.lastTime || '—'}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

// ── 主頁面 ────────────────────────────────────────────────────
export default function CostMilestonePage() {
  const [sp] = useSearchParams()
  const [view, setView]         = useState<'single' | 'batch'>('single')
  const [q, setQ]               = useState('')
  const [chars, setChars]       = useState<CostInfo[]>([])
  const [info, setInfo]         = useState<CostInfo | null>(null)
  const [loading, setLoading]   = useState(false)
  const [msg, setMsg]           = useState('')
  const [msgOk, setMsgOk]       = useState(true)
  const [addPt, setAddPt]       = useState('')

  useEffect(() => {
    const acc = sp.get('account')
    if (acc) { setQ(acc); doSearch(acc) }
  }, [])

  const doSearch = async (input: string) => {
    if (!input.trim()) return
    setLoading(true); setInfo(null); setChars([]); setMsg('')
    try {
      const r1 = await api.get(`/players/${encodeURIComponent(input.trim())}/costdata/all-chars`)
      if (r1.data && r1.data.length > 0) {
        if (r1.data.length === 1) setInfo(r1.data[0])
        else setChars(r1.data)
        return
      }
      const r2 = await api.get(`/players/${encodeURIComponent(input.trim())}/costdata`)
      setInfo(r2.data)
    } catch { setMsg('找不到玩家'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const loadPlayer = async (account: string) => {
    setLoading(true); setMsg('')
    try {
      const r = await api.get(`/players/${encodeURIComponent(account)}/costdata`)
      setInfo(r.data)
    } catch { setMsg('查詢失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  // ── 補發 ──────────────────────────────────────────────────────
  const [claimTarget, setClaimTarget] = useState<number | null>(null)
  const [claimMode, setClaimMode]     = useState<'sync' | 'mail'>('sync')
  const [mailItemId,  setMailItemId]  = useState('100104')
  const [mailQty,     setMailQty]     = useState('1')
  const [mailName,    setMailName]    = useState('綁定79MM')

  const doConfirmClaim = async () => {
    if (!info || claimTarget === null) return
    setLoading(true)
    try {
      let r
      if (claimMode === 'sync') {
        r = await api.post(`/players/${info.account}/costdata/claim/${claimTarget}`)
      } else {
        r = await api.post(`/players/${info.account}/costdata/claim-mail/${claimTarget}`, {
          addPoint: parseInt(mailItemId, 10),
          charName: mailName,
          quantity: parseInt(mailQty, 10)
        })
      }
      setMsg(r.data.message); setMsgOk(true); setClaimTarget(null)
      await loadPlayer(info.account)
    } catch (e: any) { setMsg(e.response?.data?.message || '操作失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const handleAdjust = async () => {
    if (!info) return
    const pt = parseInt(addPt, 10)
    if (!pt || pt <= 0) { setMsg('請輸入有效的點數'); setMsgOk(false); return }
    if (!window.confirm(`確定增加「${info.onlineName}」${pt.toLocaleString()} 消費點數？`)) return
    setLoading(true)
    try {
      const r = await api.post(`/players/${info.account}/costdata/adjust`, { addPoint: pt, charName: info.onlineName })
      setMsg(r.data.message); setMsgOk(true); setAddPt('')
      await loadPlayer(info.account)
    } catch (e: any) { setMsg(e.response?.data?.message || '調整失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const handleReset = async () => {
    if (!info) return
    if (!window.confirm(
      `🔄 重置「${info.onlineName}」已領取狀態？\n\n消費點數保留 → 玩家可立即重新領取所有里程碑！\n如要讓玩家必須重新消費，請用「完全重置」。`
    )) return
    setLoading(true)
    try {
      const r = await api.post(`/players/${info.account}/costdata/reset`)
      setMsg(r.data.message); setMsgOk(true)
      await loadPlayer(info.account)
    } catch (e: any) { setMsg(e.response?.data?.message || '重置失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const handleFullReset = async () => {
    if (!info) return
    if (!window.confirm(
      `🗑 完全重置「${info.onlineName}」消費達成進度？\n\n消費點數（point）和已領狀態（check）全部歸零！\n玩家必須重新消費達到里程碑才能再領取獎勵。\n\n⚠ 此操作無法復原！`
    )) return
    setLoading(true)
    try {
      const r = await api.post(`/players/${info.account}/costdata/full-reset`)
      setMsg(r.data.message); setMsgOk(true)
      await loadPlayer(info.account)
    } catch (e: any) { setMsg(e.response?.data?.message || '完全重置失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const claimable = info?.milestones.filter(m => m.reached && !m.claimed) ?? []
  const maxPct    = info ? Math.min(100, (info.costPoint / 100_000) * 100) : 0

  return (
    <div style={{ padding: '24px 28px', maxWidth: 1100, margin: '0 auto' }}>
      <h1 style={{ fontSize: 22, fontWeight: 800, color: 'var(--text-primary)', marginBottom: 6 }}>
        💸 消費達成獎勵
      </h1>
      <p style={{ color: 'var(--text-muted)', fontSize: 13, marginBottom: 20 }}>
        管理玩家的消費里程碑進度（costdata），里程碑：3,000 / 5,000 / 10,000 / 50,000 / 100,000 金幣
      </p>

      {/* Tab 切換 */}
      <div style={{ display: 'flex', gap: 4, marginBottom: 22, borderBottom: '1px solid var(--border)', paddingBottom: 0 }}>
        {([['single', '🔍 單人查詢'], ['batch', '👥 全服覽覽']] as const).map(([v, label]) => (
          <button key={v} onClick={() => setView(v)} style={{
            padding: '9px 22px', border: 'none', borderRadius: '8px 8px 0 0', cursor: 'pointer',
            fontWeight: 700, fontSize: 14, transition: 'all .15s',
            background: view === v ? 'var(--bg-card)' : 'transparent',
            color: view === v ? '#b87fff' : 'var(--text-muted)',
            borderBottom: view === v ? '2px solid #7c3aed' : '2px solid transparent',
            marginBottom: -1,
          }}>{label}</button>
        ))}
      </div>

      {/* ── 全服覽覽 ── */}
      {view === 'batch' && <BatchView />}

      {/* ── 單人查詢 ── */}
      {view === 'single' && (
        <div>
          <div style={{ display: 'flex', gap: 10, marginBottom: 20 }}>
            <PlayerAutocomplete
              value={q}
              onChange={setQ}
              onSelect={(p: PlayerRow) => { setQ(p.onlineName || p.account); doSearch(p.account) }}
              onSelectMulti={players => { setQ(players[0].onlineName || players[0].account); doSearch(players[0].account) }}
              placeholder="主帳號 / 角色名稱 / UID"
              style={{ flex: 1 }}
            />
            <button onClick={() => doSearch(q)} disabled={loading} style={{
              padding: '10px 22px', borderRadius: 8, border: 'none', cursor: 'pointer',
              background: '#1e4ba0', color: '#fff', fontWeight: 700, fontSize: 14,
              opacity: loading ? 0.6 : 1,
            }}>{loading ? '查詢中…' : '🔍 查詢'}</button>
          </div>

          {msg && (
            <div style={{
              padding: '10px 16px', borderRadius: 8, marginBottom: 16, fontSize: 13,
              background: msgOk ? 'rgba(22,185,122,.1)' : 'rgba(245,101,101,.1)',
              border: `1px solid ${msgOk ? 'rgba(22,185,122,.3)' : 'rgba(245,101,101,.3)'}`,
              color: msgOk ? '#16b97a' : '#f87171'
            }}>{msg}</div>
          )}

          {/* 多角色選擇列表 */}
          {chars.length > 0 && !info && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              <div style={{ fontSize: 14, fontWeight: 700, color: 'var(--text-secondary)', marginBottom: 4 }}>
                找到 {chars.length} 個角色，請選擇：
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))', gap: 12 }}>
                {chars.map(c => (
                  <CharCard key={c.account} c={c} onSelect={() => setInfo(c)} />
                ))}
              </div>
            </div>
          )}

          {/* 單角色詳細 */}
          {info && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>

              {chars.length > 1 && (
                <button onClick={() => setInfo(null)} style={{
                  alignSelf: 'flex-start', padding: '6px 14px', borderRadius: 6, fontSize: 13,
                  background: 'transparent', border: '1px solid var(--border)',
                  color: 'var(--text-muted)', cursor: 'pointer', fontWeight: 600,
                }}>← 返回角色列表</button>
              )}

              {/* 摘要卡片 */}
              <div style={{
                background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: 20
              }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 14 }}>
                  <div>
                    <div style={{ fontSize: 16, fontWeight: 800, color: 'var(--text-primary)' }}>
                      {info.onlineName || '（無角色名）'}
                      {info.isOnline && <span style={{ marginLeft: 8, fontSize: 12, color: '#16b97a' }}>● 線上</span>}
                    </div>
                    <div style={{ display: 'flex', gap: 18, marginTop: 4, flexWrap: 'wrap' }}>
                      <span style={{ fontSize: 12, color: '#60a5fa' }}>
                        CDKEY：<span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{info.account}</span>
                      </span>
                      {info.masterAccount && (
                        <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
                          主帳號：<span style={{ fontWeight: 700, color: 'var(--text-secondary)' }}>{info.masterAccount}</span>
                        </span>
                      )}
                    </div>
                  </div>
                  {claimable.length > 0 && (
                    <span style={{
                      padding: '4px 12px', borderRadius: 20, fontSize: 12, fontWeight: 700,
                      background: 'rgba(251,191,36,.2)', border: '1px solid rgba(251,191,36,.4)', color: '#fbbf24'
                    }}>🎁 {claimable.length} 個獎勵待補發</span>
                  )}
                </div>
                <div style={{ marginBottom: 12 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 6 }}>
                    <span style={{ color: 'var(--text-muted)' }}>累計消費進度（已領 {info.claimedCount}/5　check={info.costCheck}）</span>
                    <span style={{ fontWeight: 700, color: '#b87fff' }}>{info.costPoint.toLocaleString()} 金幣</span>
                  </div>
                  <div style={{ height: 10, background: 'var(--bg-mid)', borderRadius: 5, overflow: 'hidden' }}>
                    <div style={{
                      width: `${maxPct}%`, height: '100%', borderRadius: 5,
                      background: 'linear-gradient(90deg,#b87fff,#7c3aed)',
                      transition: 'width .4s', minWidth: info.costPoint > 0 ? 6 : 0
                    }} />
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 10, color: 'var(--text-muted)', marginTop: 4 }}>
                    {MILESTONES.map(m => <span key={m}>{m.toLocaleString()}</span>)}
                  </div>
                </div>
              </div>

              {/* 里程碑卡片 */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))', gap: 12 }}>
                {info.milestones.map(m => (
                  <MilestoneCard key={m.index} m={m} onClaim={idx => setClaimTarget(idx)} loading={loading} />
                ))}
              </div>

              {/* 補發確認面板 */}
              {claimTarget !== null && (
                <div style={{
                  background: 'var(--bg-card)', border: '1px solid rgba(251,191,36,.4)',
                  borderRadius: 12, padding: '16px 20px', boxShadow: '0 0 16px rgba(251,191,36,.15)'
                }}>
                  <div style={{ fontSize: 14, fontWeight: 700, color: '#fbbf24', marginBottom: 14 }}>
                    🎁 補發第 {claimTarget + 1} 里程碑（{MILESTONES[claimTarget].toLocaleString()} 金幣）
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 16 }}>
                    <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, cursor: 'pointer' }}>
                      <input type="radio" checked={claimMode === 'sync'} onChange={() => setClaimMode('sync')} style={{ marginTop: 2 }} />
                      <div>
                        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-primary)' }}>🔄 同步遊戲（推薦）</div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>退 check 讓遊戲伺服器偵測，自動發道具到玩家背包</div>
                      </div>
                    </label>
                    <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, cursor: 'pointer' }}>
                      <input type="radio" checked={claimMode === 'mail'} onChange={() => setClaimMode('mail')} style={{ marginTop: 2 }} />
                      <div>
                        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-primary)' }}>📬 郵件發道具</div>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>直接寄出道具，玩家從信箱領取</div>
                      </div>
                    </label>
                  </div>
                  {claimMode === 'mail' && (
                    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 14, padding: '12px', background: 'var(--bg-input)', borderRadius: 8 }}>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        <label style={{ fontSize: 11, color: 'var(--text-muted)' }}>道具 ID <span style={{ color: '#475569' }}>（79MM=100103 / 綁定=100104）</span></label>
                        <input type="number" value={mailItemId} onChange={e => setMailItemId(e.target.value)}
                          style={{ width: 100, padding: '6px 8px', borderRadius: 6, fontSize: 13, background: 'var(--bg-card)', border: '1px solid var(--border)', color: 'var(--text-primary)', outline: 'none' }} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        <label style={{ fontSize: 11, color: 'var(--text-muted)' }}>道具名稱</label>
                        <input type="text" value={mailName} onChange={e => setMailName(e.target.value)}
                          style={{ width: 160, padding: '6px 8px', borderRadius: 6, fontSize: 13, background: 'var(--bg-card)', border: '1px solid var(--border)', color: 'var(--text-primary)', outline: 'none' }} />
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        <label style={{ fontSize: 11, color: 'var(--text-muted)' }}>數量</label>
                        <input type="number" value={mailQty} onChange={e => setMailQty(e.target.value)}
                          style={{ width: 100, padding: '6px 8px', borderRadius: 6, fontSize: 13, background: 'var(--bg-card)', border: '1px solid var(--border)', color: 'var(--text-primary)', outline: 'none' }} />
                      </div>
                    </div>
                  )}
                  <div style={{ display: 'flex', gap: 10 }}>
                    <button onClick={doConfirmClaim} disabled={loading} style={{
                      padding: '8px 22px', borderRadius: 6, border: 'none', cursor: 'pointer',
                      background: '#b87fff', color: '#fff', fontWeight: 700, fontSize: 13, opacity: loading ? 0.6 : 1
                    }}>✅ 確認補發</button>
                    <button onClick={() => setClaimTarget(null)} style={{
                      padding: '8px 16px', borderRadius: 6, cursor: 'pointer', fontWeight: 600, fontSize: 13,
                      background: 'transparent', border: '1px solid var(--border)', color: 'var(--text-muted)'
                    }}>取消</button>
                  </div>
                </div>
              )}

              {/* 操作區 */}
              <div style={{
                background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: '16px 20px'
              }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-secondary)', marginBottom: 14 }}>⚙ 管理操作</div>
                <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                    <label style={{ fontSize: 11, color: 'var(--text-muted)' }}>增加消費點數（金幣）</label>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <input type="number" value={addPt} onChange={e => setAddPt(e.target.value)}
                        placeholder="例：1000"
                        style={{
                          width: 120, padding: '7px 10px', borderRadius: 6, fontSize: 13,
                          background: 'var(--bg-input)', border: '1px solid var(--border)',
                          color: 'var(--text-primary)', outline: 'none',
                        }} />
                      <button onClick={handleAdjust} disabled={loading || !addPt} style={{
                        padding: '7px 16px', borderRadius: 6, border: 'none', cursor: 'pointer',
                        background: '#1e4ba0', color: '#fff', fontWeight: 700, fontSize: 13,
                        opacity: loading || !addPt ? 0.5 : 1,
                      }}>➕ 確認</button>
                    </div>
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                      <button onClick={handleReset} disabled={loading} style={{
                        padding: '7px 18px', borderRadius: 6, cursor: 'pointer', fontWeight: 700, fontSize: 13,
                        background: 'rgba(251,191,36,.1)', border: '1px solid rgba(251,191,36,.4)',
                        color: '#fbbf24', opacity: loading ? 0.5 : 1,
                      }}>🔄 重置已領狀態</button>
                      <span style={{ fontSize: 10, color: 'var(--text-muted)', textAlign: 'center' }}>點數保留，玩家可立即重領</span>
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                      <button onClick={handleFullReset} disabled={loading} style={{
                        padding: '7px 18px', borderRadius: 6, cursor: 'pointer', fontWeight: 700, fontSize: 13,
                        background: 'rgba(248,113,113,.1)', border: '1px solid rgba(248,113,113,.4)',
                        color: '#f87171', opacity: loading ? 0.5 : 1,
                      }}>🗑 完全重置</button>
                      <span style={{ fontSize: 10, color: 'var(--text-muted)', textAlign: 'center' }}>point+check 歸零，須重新消費</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

const TH: React.CSSProperties = { padding: '10px 12px', color: 'var(--text-muted)', fontWeight: 700, whiteSpace: 'nowrap', fontSize: 11, textTransform: 'uppercase', letterSpacing: .5 }
const TD: React.CSSProperties = { padding: '9px 12px', whiteSpace: 'nowrap' }
