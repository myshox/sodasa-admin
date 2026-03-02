import { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'

// ── 里程碑定義（與後端/EXE 一致）────────────────────────────
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
  costPoint: number
  costCheck: number       // Bitmask: bit i = 第 i+1 個里程碑已領 (31=11111₂=全部)
  claimedCount: number   // 已領數量 (PopCount of bitmask)
  milestones: MilestoneInfo[]
}

// ── 小元件 ────────────────────────────────────────────────────
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
      boxShadow: canClaim ? '0 0 12px rgba(251,191,36,.2)' : 'none',
      transition: 'all .2s',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-secondary)' }}>
          里程碑 {m.index + 1}
        </div>
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
        <button
          onClick={() => onClaim(m.index)}
          disabled={loading}
          style={{
            marginTop: 10, width: '100%', padding: '7px 0', borderRadius: 6,
            background: 'rgba(251,191,36,.2)', border: '1px solid rgba(251,191,36,.5)',
            color: '#fbbf24', fontWeight: 700, fontSize: 12, cursor: 'pointer',
          }}
        >
          🎁 補發此獎勵
        </button>
      )}
    </div>
  )
}

// ── 主頁面 ────────────────────────────────────────────────────
export default function CostMilestonePage() {
  const [sp] = useSearchParams()
  const [q, setQ]         = useState('')
  const [info, setInfo]   = useState<CostInfo | null>(null)
  const [loading, setLoading] = useState(false)
  const [msg, setMsg]     = useState('')
  const [msgOk, setMsgOk] = useState(true)

  // 調整點數表單
  const [addPt, setAddPt] = useState('')

  useEffect(() => {
    const acc = sp.get('account')
    if (acc) { setQ(acc); loadPlayer(acc) }
  }, [])

  const loadPlayer = async (account: string) => {
    if (!account.trim()) return
    setLoading(true); setInfo(null); setMsg('')
    try {
      const r = await api.get(`/players/${encodeURIComponent(account.trim())}/costdata`)
      setInfo(r.data)
    } catch { setMsg('找不到玩家'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  // 補發模式
  const [claimTarget, setClaimTarget] = useState<number | null>(null)
  const [claimMode, setClaimMode]     = useState<'sync' | 'mail'>('sync')
  const [mailItemId,  setMailItemId]  = useState('100104')
  const [mailQty,     setMailQty]     = useState('1')
  const [mailName,    setMailName]    = useState('綁定79MM')

  const handleClaim = async (milestoneIdx: number) => {
    if (!info) return
    setClaimTarget(milestoneIdx)
  }

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
      setMsg(r.data.message); setMsgOk(true)
      setClaimTarget(null)
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
    if (!window.confirm(`確定將「${info.onlineName}」消費達成進度歸零？\n\n⚠ 此操作無法復原`)) return
    setLoading(true)
    try {
      const r = await api.post(`/players/${info.account}/costdata/reset`)
      setMsg(r.data.message); setMsgOk(true)
      await loadPlayer(info.account)
    } catch (e: any) { setMsg(e.response?.data?.message || '重置失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const claimable = info?.milestones.filter(m => m.reached && !m.claimed) ?? []
  const maxPct = info ? Math.min(100, (info.costPoint / 100_000) * 100) : 0

  return (
    <div style={{ padding: '24px 28px', maxWidth: 900, margin: '0 auto' }}>
      <h1 style={{ fontSize: 22, fontWeight: 800, color: 'var(--text-primary)', marginBottom: 6 }}>
        💸 消費達成獎勵
      </h1>
      <p style={{ color: 'var(--text-muted)', fontSize: 13, marginBottom: 24 }}>
        管理玩家的消費里程碑進度（costdata），里程碑：3,000 / 5,000 / 10,000 / 50,000 / 100,000 金幣
      </p>

      {/* 搜尋 */}
      <div style={{ display: 'flex', gap: 10, marginBottom: 20 }}>
        <input
          value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && loadPlayer(q)}
          placeholder="輸入玩家帳號…"
          style={{
            flex: 1, padding: '10px 14px', borderRadius: 8, fontSize: 14,
            background: 'var(--bg-input)', border: '1px solid var(--border)',
            color: 'var(--text-primary)', outline: 'none',
          }}
        />
        <button
          onClick={() => loadPlayer(q)} disabled={loading}
          style={{
            padding: '10px 22px', borderRadius: 8, border: 'none', cursor: 'pointer',
            background: '#1e4ba0', color: '#fff', fontWeight: 700, fontSize: 14,
            opacity: loading ? 0.6 : 1,
          }}
        >
          {loading ? '查詢中…' : '🔍 查詢'}
        </button>
      </div>

      {/* 訊息 */}
      {msg && (
        <div style={{
          padding: '10px 16px', borderRadius: 8, marginBottom: 16, fontSize: 13,
          background: msgOk ? 'rgba(22,185,122,.1)' : 'rgba(245,101,101,.1)',
          border: `1px solid ${msgOk ? 'rgba(22,185,122,.3)' : 'rgba(245,101,101,.3)'}`,
          color: msgOk ? '#16b97a' : '#f87171'
        }}>{msg}</div>
      )}

      {/* 玩家資訊 */}
      {info && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>

          {/* 摘要卡片 */}
          <div style={{
            background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: 20
          }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14 }}>
              <div>
                <div style={{ fontSize: 16, fontWeight: 800, color: 'var(--text-primary)' }}>{info.onlineName}</div>
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{info.account}</div>
              </div>
              {claimable.length > 0 && (
                <span style={{
                  padding: '4px 12px', borderRadius: 20, fontSize: 12, fontWeight: 700,
                  background: 'rgba(251,191,36,.2)', border: '1px solid rgba(251,191,36,.4)', color: '#fbbf24'
                }}>
                  🎁 {claimable.length} 個獎勵待領取
                </span>
              )}
            </div>

            {/* 總進度 */}
            <div style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, marginBottom: 6 }}>
                <span style={{ color: 'var(--text-muted)' }}>累計消費進度（已領 {info.claimedCount}/5 獎　check={info.costCheck}）</span>
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

              {(info.costCheck < 0 || (info.costCheck === 0 && info.costPoint === 0)) && (
                <div style={{ fontSize: 12, color: 'var(--text-muted)', fontStyle: 'italic' }}>
                  （此玩家尚無 costdata 記錄，從未達成任何消費里程碑）
                </div>
              )}
          </div>

          {/* 里程碑卡片 */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))', gap: 12 }}>
            {info.milestones.map(m => (
              <MilestoneCard key={m.index} m={m} onClaim={handleClaim} loading={loading} />
            ))}
          </div>

          {/* 補發確認面板 */}
          {claimTarget !== null && (
            <div style={{
              background: 'var(--bg-card)', border: '1px solid rgba(251,191,36,.4)',
              borderRadius: 12, padding: '16px 20px',
              boxShadow: '0 0 16px rgba(251,191,36,.15)'
            }}>
              <div style={{ fontSize: 14, fontWeight: 700, color: '#fbbf24', marginBottom: 14 }}>
                🎁 補發第 {claimTarget + 1} 里程碑（{MILESTONES[claimTarget].toLocaleString()} 金幣）
              </div>
              {/* 模式選擇 */}
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
              {/* 郵件設定 */}
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
            <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-secondary)', marginBottom: 14 }}>
              ⚙ 管理操作
            </div>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              {/* 調整點數 */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
                <label style={{ fontSize: 11, color: 'var(--text-muted)' }}>增加消費點數（金幣）</label>
                <div style={{ display: 'flex', gap: 6 }}>
                  <input
                    type="number" value={addPt} onChange={e => setAddPt(e.target.value)}
                    placeholder="例：1000"
                    style={{
                      width: 120, padding: '7px 10px', borderRadius: 6, fontSize: 13,
                      background: 'var(--bg-input)', border: '1px solid var(--border)',
                      color: 'var(--text-primary)', outline: 'none',
                    }}
                  />
                  <button
                    onClick={handleAdjust} disabled={loading || !addPt}
                    style={{
                      padding: '7px 16px', borderRadius: 6, border: 'none', cursor: 'pointer',
                      background: '#1e4ba0', color: '#fff', fontWeight: 700, fontSize: 13,
                      opacity: loading || !addPt ? 0.5 : 1,
                    }}
                  >
                    ➕ 確認
                  </button>
                </div>
              </div>

              {/* 重置已領狀態 */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                <button
                  onClick={handleReset} disabled={loading}
                  style={{
                    padding: '7px 18px', borderRadius: 6, cursor: 'pointer', fontWeight: 700, fontSize: 13,
                    background: 'rgba(248,113,113,.1)', border: '1px solid rgba(248,113,113,.4)',
                    color: '#f87171', opacity: loading ? 0.5 : 1,
                  }}
                >
                  🔄 重置已領狀態
                </button>
                <span style={{ fontSize: 10, color: 'var(--text-muted)' }}>清除 check，讓補發按鈕可再次點擊</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
