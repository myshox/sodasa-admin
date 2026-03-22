import { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'
import { S } from '../strings'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import type { PlayerRow } from '../api'
import useIsMobile from '../hooks/useIsMobile'

const TIERS = [
  { label: 'NT$100',  subLabel: '1萬',    twd: 100,    gold: 10_000 },
  { label: 'NT$300',  subLabel: '3.2萬',  twd: 300,    gold: 32_000 },
  { label: 'NT$500',  subLabel: '5.5萬',  twd: 500,    gold: 55_000 },
  { label: 'NT$1K',   subLabel: '11.5萬', twd: 1_000,  gold: 115_000 },
  { label: 'NT$3K',   subLabel: '36萬',   twd: 3_000,  gold: 360_000 },
  { label: 'NT$5K',   subLabel: '62.5萬', twd: 5_000,  gold: 625_000 },
  { label: 'NT$10K',  subLabel: '130萬',  twd: 10_000, gold: 1_300_000 },
]
const BONUSES = [0, 5, 10, 15, 20]
const CYCLE_MAX = 25_000

function twdToGold(twd: number): { baseGold: number; rate: number; tierLabel: string } {
  if (twd <= 0) return { baseGold: 0, rate: 100, tierLabel: '—' }
  let best = TIERS[0]
  for (const t of TIERS) { if (twd >= t.twd) best = t }
  const rate = best.gold / best.twd
  return { baseGold: Math.floor(twd * rate), rate, tierLabel: best.label }
}

/** 金幣反推台幣：找出為取得 targetGold（含 bonusPct 加成後）所需最少台幣 */
function goldToTwd(targetGold: number, bonusPct: number): Array<{ twd: number; actualGold: number; tierLabel: string; rate: number }> {
  if (targetGold <= 0) return []
  const divisor = 1 + bonusPct / 100
  const candidates: Array<{ twd: number; actualGold: number; tierLabel: string; rate: number }> = []

  for (const t of TIERS) {
    const tierRate = t.gold / t.twd
    // 反推：需要多少台幣（基礎金幣）才能讓 base * divisor >= targetGold
    let needed = Math.ceil(targetGold / divisor / tierRate)
    needed = Math.max(needed, t.twd) // 至少達到此套餐門檻

    // 用 twdToGold 確認 needed 台幣時的實際匯率
    const { baseGold, rate, tierLabel } = twdToGold(needed)
    const actualGold = Math.floor(baseGold * divisor)
    if (actualGold >= targetGold) {
      candidates.push({ twd: needed, actualGold, tierLabel, rate })
    }
  }

  if (candidates.length === 0) {
    const needed = Math.ceil(targetGold / divisor / 100)
    candidates.push({ twd: needed, actualGold: Math.floor(needed * 100 * divisor), tierLabel: '基礎', rate: 100 })
  }

  // 去重 + 排序（最少台幣優先）
  const seen = new Set<number>()
  return candidates
    .filter(c => { if (seen.has(c.twd)) return false; seen.add(c.twd); return true })
    .sort((a, b) => a.twd - b.twd)
}

interface PaydataInfo {
  account: string; onlineName: string; isOnline: boolean
  gold: number; crystal: number; payTotal: number
  paydataPoint: number; totalCheck: number; lifetimeTotal: number; vipLevel: number
  paydataCheck: number; claimReady: boolean
}

interface RechargeOrder {
  orderNo: string; account: string; charName: string; productName: string
  yuanbao: number; twd: number; status: string; time: string; source: string
  lifetimeTotal?: number; totalCheck?: number
}

export default function RechargePage() {
  const isMobile = useIsMobile()
  const [sp] = useSearchParams()
  const [playerQ, setPlayerQ] = useState(sp.get('account') || '')
  const [info, setInfo] = useState<PaydataInfo | null>(null)
  const [infoLoading, setInfoLoading] = useState(false)
  const [selectedTier, setSelectedTier] = useState<typeof TIERS[0] | null>(null)
  const [bonus, setBonus] = useState(0)
  const [customTwd, setCustomTwd] = useState('')
  const [opType, setOpType] = useState<null | 'only' | 'gold' | 'onlyGold'>(null)
  const [loading, setLoading] = useState(false)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)
  const [histQ, setHistQ] = useState('')
  const [histRows, setHistRows] = useState<RechargeOrder[]>([])
  const [histLoading, setHistLoading] = useState(false)
  const [calcTwd, setCalcTwd] = useState('')
  const [calcBonus, setCalcBonus] = useState(0)
  const [calcGold, setCalcGold] = useState('')
  const [calcRevBonus, setCalcRevBonus] = useState(0)
  const [calcTab, setCalcTab] = useState<'twd' | 'gold'>('twd')

  const finalTwd  = selectedTier ? selectedTier.twd : parseInt(customTwd, 10) || 0
  const baseGoldAuto = selectedTier ? selectedTier.gold : (parseInt(customTwd, 10) || 0) * 100
  const finalGold = Math.floor(baseGoldAuto * (1 + bonus / 100))
  const giveGold  = opType === 'gold' || opType === 'onlyGold'
  // onlyGold 模式：不影響累積進度，afterCycle 不變
  const effectiveTwd   = opType === 'onlyGold' ? 0 : finalTwd
  const currentCycle   = info ? info.paydataPoint : 0
  const afterCycle     = effectiveTwd > 0 ? currentCycle + effectiveTwd : currentCycle
  const completedExtra = afterCycle > 0 ? Math.floor((afterCycle - 1) / CYCLE_MAX) - Math.floor((currentCycle > 0 ? (currentCycle - 1) / CYCLE_MAX : 0)) : 0
  const afterPoint     = afterCycle - Math.floor((afterCycle > 0 ? (afterCycle - 1) / CYCLE_MAX : 0)) * CYCLE_MAX
  const afterPayTotal  = (info?.payTotal ?? 0) + (opType === 'gold' ? finalTwd : 0)
  const afterVip       = afterPayTotal >= 15000 ? 2 : afterPayTotal >= 5000 ? 1 : 0
  const cycPct         = Math.min(100, Math.round((currentCycle / CYCLE_MAX) * 100))
  const afterCycPct    = Math.min(100, Math.round(((afterPoint > 0 ? afterPoint : currentCycle) / CYCLE_MAX) * 100))

  useEffect(() => { const acc = sp.get('account'); if (acc) loadPlayer(acc) }, [])

  const loadPlayer = async (q: string) => {
    if (!q.trim()) return
    setInfoLoading(true); setInfo(null)
    try {
      const r = await api.get(`/players/${encodeURIComponent(q.trim())}/paydata`)
      setInfo(r.data)
      const vip = r.data.vipLevel
      setBonus(vip === 2 ? 10 : vip === 1 ? 5 : 0)
    } catch { setMsg('找不到玩家'); setMsgOk(false) }
    finally { setInfoLoading(false) }
  }

  const handleSelectTier = (tier: typeof TIERS[0]) => {
    setSelectedTier(prev => prev === tier ? null : tier)
    setCustomTwd('')
  }

  const doRecharge = async () => {
    if (!info) { setMsg('請先搜尋並選定玩家'); setMsgOk(false); return }
    if (opType === null) { setMsg('⚠ 請選擇操作類型（STEP 4）'); setMsgOk(false); return }
    if (opType !== 'onlyGold' && finalTwd <= 0) { setMsg('請選擇套餐或輸入台幣金額'); setMsgOk(false); return }
    if (opType === 'onlyGold' && finalGold <= 0) { setMsg('請先選擇套餐（用來決定發放金幣數量）'); setMsgOk(false); return }

    const effectiveTwd = opType === 'onlyGold' ? 0 : finalTwd

    // 大額（> NT$10,000）額外警告
    if (effectiveTwd > 10_000) {
      if (!window.confirm(`⚠ 充值金額 NT$${effectiveTwd.toLocaleString()} 超過 NT$10,000\n請確認金額無誤。繼續嗎？`)) return
    }

    // 最終確認
    const goldLine = giveGold ? `  金幣入帳：+${finalGold.toLocaleString()} 元寶` : '  本次不發放金幣（僅更新累積進度）'
    const twdLine  = opType === 'onlyGold' ? '  操作類型：只發金幣（不計入累積充值）' : `  台幣金額：NT$${effectiveTwd.toLocaleString()}`
    if (!window.confirm(
      `確認給予儲值？\n\n  玩家：${info.onlineName}（${info.account}）\n${twdLine}\n${goldLine}\n\n此操作無法撤銷！`
    )) return

    setLoading(true); setMsg('')
    try {
      const r = await api.post(`/players/${info.account}/recharge`, {
        twdAmount: opType === 'onlyGold' ? 0 : finalTwd,
        goldAmount: giveGold ? finalGold : 0,
        giveGold
      })
      setMsg(r.data.message || '✓ 儲值成功'); setMsgOk(true)
      setSelectedTier(null); setCustomTwd(''); setOpType(null)
      await loadPlayer(info.account)
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err.response?.data?.message || '儲值失敗'); setMsgOk(false)
    } finally { setLoading(false) }
  }

  const doFix = async () => {
    if (!info) return; setLoading(true)
    try { const r = await api.post(`/players/${info.account}/paydata/fix`); setMsg(r.data.message); setMsgOk(true); await loadPlayer(info.account) }
    catch (e: unknown) { const err = e as { response?: { data?: { message?: string } } }; setMsg(err.response?.data?.message || '修復失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const doClaim = async () => {
    if (!info || !info.claimReady) return
    if (!window.confirm(`確定要發放「${info.onlineName}」第 ${info.totalCheck} 輪的累積獎勵？`)) return
    setLoading(true)
    try { const r = await api.post(`/players/${info.account}/paydata/claim`); setMsg(r.data.message); setMsgOk(true); await loadPlayer(info.account) }
    catch (e: unknown) { const err = e as { response?: { data?: { message?: string } } }; setMsg(err.response?.data?.message || '操作失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const doReset = async () => {
    if (!info) return
    if (!window.confirm(`確定要將「${info.onlineName}」的累積充值進度歸零？\n\n⚠ 此操作無法復原`)) return
    setLoading(true)
    try { const r = await api.post(`/players/${info.account}/paydata/reset`); setMsg(r.data.message); setMsgOk(true); await loadPlayer(info.account) }
    catch { setMsg('重置失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const loadHistory = async () => {
    setHistLoading(true)
    try { const r = await api.get('/players/recharge', { params: { q: histQ } }); setHistRows(Array.isArray(r.data) ? r.data : []) }
    finally { setHistLoading(false) }
  }

  const vipLabel = (v: number) => v === 2 ? '💎 鑽石 VIP' : v === 1 ? '🥇 黃金 VIP' : '一般玩家'
  const vipColor = (v: number) => v === 2 ? '#60a5fa' : v === 1 ? '#fbbf24' : '#6b7280'
  const vipBg    = (v: number) => v === 2 ? 'rgba(96,165,250,.15)' : v === 1 ? 'rgba(251,191,36,.15)' : 'transparent'

  const canSubmit = !!info && opType !== null && (
    opType === 'onlyGold' ? finalGold > 0 : finalTwd > 0
  )

  return (
    <div style={{ padding: isMobile ? '14px 12px' : '28px 32px', maxWidth: 1200, width: '100%', boxSizing: 'border-box' }}>
      {/* ── 標題 ── */}
      <div style={{ marginBottom: isMobile ? 14 : 24 }}>
        <h1 style={{ fontSize: isMobile ? 19 : 24, fontWeight: 800, margin: 0, background: 'linear-gradient(135deg,#4ade80,#22d3ee)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
          💳 {S.pageRecharge}
        </h1>
        <p style={{ margin: '4px 0 0', fontSize: 12, color: 'var(--text-muted)' }}>手動補單 · 充值記錄 · 累儲進度</p>
      </div>

      {/* ── 全域訊息 ── */}
      {msg && (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '12px 16px', marginBottom: 16, borderRadius: 10,
          background: msgOk ? 'rgba(74,222,128,.1)' : 'rgba(248,113,113,.1)',
          border: `1px solid ${msgOk ? 'rgba(74,222,128,.4)' : 'rgba(248,113,113,.4)'}`,
          color: msgOk ? '#4ade80' : '#f87171', fontSize: 14, fontWeight: 600 }}>
          <span style={{ fontSize: 18 }}>{msgOk ? '✅' : '❌'}</span>
          <span style={{ flex: 1 }}>{msg}</span>
          <button onClick={() => setMsg('')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'inherit', opacity: 0.6, fontSize: 18, minHeight: 0, padding: '0 4px' }}>✕</button>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: isMobile ? '1fr' : '300px 1fr', gap: isMobile ? 12 : 20, alignItems: 'flex-start' }}>

        {/* ══════════ 左欄：玩家卡片 ══════════ */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* 搜尋框 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: 18 }}>
            <StepLabel n={1} text="選定玩家" />
            <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
              <PlayerAutocomplete
                value={playerQ}
                onChange={setPlayerQ}
                onSelect={(p: PlayerRow) => { setPlayerQ(p.onlineName || p.account); loadPlayer(p.account) }}
                onSelectMulti={players => { setPlayerQ(players[0].onlineName || players[0].account); loadPlayer(players[0].account) }}
                placeholder="主帳號 / 角色名稱 / UID（主帳號可複選，取第一個操作）"
                style={{ flex: 1, fontSize: 13 }}
              />
              <button onClick={() => loadPlayer(playerQ)} disabled={infoLoading}
                style={{ padding: '8px 14px', background: 'linear-gradient(135deg,#3b82f6,#2563eb)', color: '#fff', borderRadius: 7, fontSize: 13, fontWeight: 700, border: 'none', cursor: 'pointer', minWidth: 44 }}>
                {infoLoading ? '…' : '🔍'}
              </button>
            </div>
          </div>

          {/* 玩家資訊卡 */}
          {info && (
            <div style={{ background: 'linear-gradient(145deg,var(--bg-card),var(--bg-sidebar))', border: '1px solid var(--border)', borderRadius: 12, overflow: 'hidden' }}>
              {/* 頭部 */}
              <div style={{ padding: '16px 18px', background: 'linear-gradient(135deg,rgba(74,222,128,.12),rgba(34,211,238,.08))', borderBottom: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <div style={{ width: 40, height: 40, borderRadius: '50%', background: 'linear-gradient(135deg,#4ade80,#22d3ee)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 18, flexShrink: 0 }}>
                    {info.onlineName.charAt(0) || '?'}
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontWeight: 800, fontSize: 16, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{info.onlineName}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 1 }}>{info.account}</div>
                  </div>
                  <div style={{ marginLeft: 'auto', flexShrink: 0, display: 'flex', alignItems: 'center', gap: 5 }}>
                    <span style={{ width: 8, height: 8, borderRadius: '50%', background: info.isOnline ? '#4ade80' : '#6b7280', display: 'inline-block', boxShadow: info.isOnline ? '0 0 6px #4ade80' : 'none' }} />
                    <span style={{ fontSize: 11, color: info.isOnline ? '#4ade80' : '#6b7280' }}>{info.isOnline ? '在線' : '離線'}</span>
                  </div>
                </div>
                {/* VIP 徽章 */}
                {info.vipLevel > 0 && (
                  <div style={{ marginTop: 10, display: 'inline-flex', alignItems: 'center', gap: 5, padding: '4px 10px', borderRadius: 20, background: vipBg(info.vipLevel), border: `1px solid ${vipColor(info.vipLevel)}40`, fontSize: 12, fontWeight: 700, color: vipColor(info.vipLevel) }}>
                    {vipLabel(info.vipLevel)}
                  </div>
                )}
              </div>

              {/* 數值 */}
              <div style={{ padding: '14px 18px', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                <StatBox label="金幣（元寶）" value={info.gold.toLocaleString()} color="#fb923c" />
                <StatBox label="水晶" value={info.crystal.toLocaleString()} color="#60a5fa" />
                <StatBox label="累積儲值" value={`NT$${info.payTotal.toLocaleString()}`} color="#4ade80" />
                <StatBox label="歷史總計" value={`NT$${(info.lifetimeTotal || info.payTotal).toLocaleString()}`} color="#a78bfa" />
              </div>


              {/* 累儲進度條 */}
              <div style={{ padding: '0 18px 16px' }}>
                <div style={{ background: 'var(--bg-input)', borderRadius: 10, padding: '12px 14px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, marginBottom: 6 }}>
                    <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>累積儲值進度（第 {(info.totalCheck || 0) + 1} 輪）</span>
                    <span style={{ color: '#fb923c', fontWeight: 700 }}>{cycPct}%</span>
                  </div>
                  <div style={{ height: 8, background: 'var(--bg-card)', borderRadius: 4, overflow: 'hidden' }}>
                    <div style={{ width: `${cycPct}%`, height: '100%', background: 'linear-gradient(90deg,#f59e0b,#fb923c)', borderRadius: 4, transition: 'width .4s' }} />
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: 'var(--text-muted)', marginTop: 5 }}>
                    <span>NT${info.paydataPoint.toLocaleString()}</span>
                    <span>目標 NT$25,000</span>
                  </div>
                  <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 4, lineHeight: 1.5 }}>
                    每累積儲值 NT$25,000 完成一輪 → 可領取累積大獎 ｜ 已完成 {info.totalCheck} 輪
                  </div>
                </div>

                {info.claimReady && (
                  <button onClick={doClaim} disabled={loading}
                    style={{ width: '100%', marginTop: 10, padding: '9px 0', fontSize: 13, fontWeight: 700,
                      background: 'linear-gradient(135deg,rgba(251,191,36,.2),rgba(251,191,36,.1))',
                      border: '1px solid rgba(251,191,36,.5)', borderRadius: 8, color: '#fbbf24', cursor: 'pointer' }}>
                    🎁 發放第 {info.totalCheck} 輪累積大獎
                  </button>
                )}
                {!info.claimReady && info.totalCheck > 0 && (
                  <div style={{ fontSize: 11, color: '#4ade80', marginTop: 8, textAlign: 'center', padding: '6px', background: 'rgba(74,222,128,.08)', borderRadius: 6 }}>
                    ✓ 第 {info.totalCheck} 輪獎勵已發放
                  </div>
                )}

                <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                  <button onClick={doFix} disabled={loading} title="進度條顯示異常時點此修復（不會更動儲值金額）"
                    style={{ flex: 1, fontSize: 11, padding: '6px 0', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 6, cursor: 'pointer', color: 'var(--text-muted)' }}>
                    🔧 修復進度顯示
                  </button>
                  <button onClick={doReset} disabled={loading} title="將此玩家的累積儲值進度歸零（無法復原）"
                    style={{ flex: 1, fontSize: 11, padding: '6px 0', background: 'rgba(248,113,113,.1)', border: '1px solid rgba(248,113,113,.4)', borderRadius: 6, cursor: 'pointer', color: '#f87171' }}>
                    🗑 清零進度
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* ══════════ 右欄：充值操作 ══════════ */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* STEP 2：套餐 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: isMobile ? 14 : 20 }}>
            <StepLabel n={2} text="選擇套餐" sub="1台幣 = 100金幣，大額有加成" />
            <div style={{ display: 'grid', gridTemplateColumns: isMobile ? 'repeat(3,1fr)' : 'repeat(7,1fr)', gap: isMobile ? 8 : 8, marginTop: 14 }}>
              {TIERS.map(tier => {
                const sel = selectedTier === tier
                return (
                  <button key={tier.twd} onClick={() => handleSelectTier(tier)}
                    style={{
                      padding: isMobile ? '14px 4px' : '10px 4px',
                      cursor: 'pointer', borderRadius: 10, textAlign: 'center', transition: 'all .15s',
                      background: sel ? 'linear-gradient(135deg,#3b82f6,#2563eb)' : 'var(--bg-input)',
                      border: `2px solid ${sel ? '#60a5fa' : 'transparent'}`,
                      boxShadow: sel ? '0 0 12px rgba(59,130,246,.3)' : 'none',
                      minHeight: isMobile ? 60 : 'auto',
                      WebkitTapHighlightColor: 'transparent',
                    }}>
                    <div style={{ fontSize: isMobile ? 13 : 13, fontWeight: 800, color: sel ? '#fff' : 'var(--text-primary)' }}>{tier.label}</div>
                    <div style={{ fontSize: 10, color: sel ? 'rgba(255,255,255,.75)' : '#fb923c', marginTop: 3, fontWeight: 600 }}>{tier.subLabel}</div>
                  </button>
                )
              })}
            </div>

            {/* 手動輸入 */}
            <div style={{ marginTop: 12, display: 'flex', gap: 10, alignItems: 'center', padding: '10px 12px', background: 'var(--bg-input)', borderRadius: 8, flexWrap: 'wrap' }}>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', flexShrink: 0 }}>或手動輸入：</div>
              <input type="number" inputMode="numeric" value={customTwd}
                onChange={e => { setCustomTwd(e.target.value); setSelectedTier(null) }}
                placeholder="台幣金額 NT$" min={1}
                style={{ width: isMobile ? '100%' : 140, fontSize: 13, padding: '8px 10px', borderRadius: 6, flex: isMobile ? '1 0 100%' : 'none' }} />
              {finalTwd > 0 && (
                <div style={{ fontSize: 13, color: '#4ade80', fontWeight: 700, flex: '1 0 auto' }}>
                  → {finalGold.toLocaleString()} 元寶{bonus > 0 ? `（+${bonus}%）` : ''}
                </div>
              )}
            </div>
          </div>

          {/* STEP 3：加成 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: isMobile ? 14 : 20 }}>
            <StepLabel n={3} text="額外贈金加成" sub="選填・一般補單請選「無」" />
            <div style={{ marginTop: 8, fontSize: 12, color: 'var(--text-muted)', background: 'var(--bg-input)', borderRadius: 6, padding: '7px 10px', lineHeight: 1.6 }}>
              💡 贈金是「活動獎勵」，不計入累積儲值進度。<br />
              如果是一般補單，選「無」即可。VIP 玩家的加成已自動套用。
            </div>
            {info && info.vipLevel > 0 && (
              <div style={{ marginTop: 8, fontSize: 12, color: vipColor(info.vipLevel), display: 'flex', alignItems: 'center', gap: 6 }}>
                <span>✓</span><span>{vipLabel(info.vipLevel)} — 已自動套用加成</span>
              </div>
            )}
            <div style={{ display: 'grid', gridTemplateColumns: `repeat(${BONUSES.length}, 1fr)`, gap: isMobile ? 6 : 8, marginTop: 12 }}>
              {BONUSES.map(b => {
                const sel = bonus === b
                return (
                  <button key={b} onClick={() => setBonus(b)}
                    style={{
                      padding: isMobile ? '12px 4px' : '8px 4px',
                      cursor: 'pointer', borderRadius: 8, fontSize: isMobile ? 14 : 13, fontWeight: sel ? 800 : 500, transition: 'all .15s',
                      background: sel ? (b > 0 ? 'rgba(74,222,128,.2)' : 'var(--bg-input)') : 'var(--bg-input)',
                      border: `2px solid ${sel ? (b > 0 ? '#4ade80' : 'var(--accent-blue)') : 'transparent'}`,
                      color: b > 0 ? '#4ade80' : sel ? 'var(--accent-blue)' : 'var(--text-secondary)',
                      WebkitTapHighlightColor: 'transparent',
                    }}>
                    {b === 0 ? '無' : `+${b}%`}
                  </button>
                )
              })}
            </div>
          </div>

          {/* STEP 4：操作類型 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid rgba(251,191,36,.3)', borderRadius: 12, padding: isMobile ? 14 : 20 }}>
            <StepLabel n={4} text="這次要做什麼？" sub="必填" warn />
            <div style={{ display: 'grid', gridTemplateColumns: isMobile ? '1fr' : '1fr 1fr 1fr', gap: isMobile ? 8 : 10, marginTop: 14 }}>
              <OpCard
                selected={opType === 'gold'} onClick={() => setOpType('gold')}
                icon="💰" color="#4ade80"
                title="記錄＋發金幣"
                desc={finalGold > 0 ? `+${finalGold.toLocaleString()} 元寶入帳` : '儲值紀錄 + 金幣一起到位'}
                badge="一般使用"
              />
              <OpCard
                selected={opType === 'only'} onClick={() => setOpType('only')}
                icon="📝" color="#60a5fa"
                title="只記錄，不給金幣"
                desc="適合金幣已另外處理、只需補儲值紀錄的情況"
              />
              <OpCard
                selected={opType === 'onlyGold'} onClick={() => setOpType('onlyGold')}
                icon="🎁" color="#f59e0b"
                title="只發金幣，不計充值"
                desc="發送金幣但不影響累積儲值進度，適合活動補償"
              />
            </div>
          </div>

          {/* 預覽 */}
          {(opType === 'onlyGold' ? finalGold > 0 : finalTwd > 0) && opType !== null && (
            <div style={{ background: 'linear-gradient(135deg,rgba(74,222,128,.08),rgba(34,211,238,.05))', border: '1px solid rgba(74,222,128,.3)', borderRadius: 12, padding: 20 }}>
              <div style={{ fontSize: 13, fontWeight: 700, color: '#4ade80', marginBottom: 12 }}>📋 確認預覽</div>
              {opType === 'onlyGold' && (
                <div style={{ fontSize: 12, color: '#f59e0b', background: 'rgba(245,158,11,.1)', borderRadius: 8, padding: '6px 10px', marginBottom: 10 }}>
                  ⚠ 此模式只發金幣，累積充值進度不變
                </div>
              )}
              <div style={{ display: 'grid', gridTemplateColumns: isMobile ? '1fr 1fr' : '1fr 1fr 1fr', gap: 10 }}>
                {opType !== 'onlyGold'
                  ? <PreviewCard label="台幣" value={`NT$${finalTwd.toLocaleString()}`} color="#fb923c" />
                  : <PreviewCard label="台幣" value="不計入" color="#6b7280" />}
                {giveGold
                  ? <PreviewCard label="金幣入帳" value={`+${finalGold.toLocaleString()}`} color="#fbbf24" />
                  : <PreviewCard label="類型" value="僅累儲" color="#60a5fa" />}
                {bonus > 0 && <PreviewCard label="加成" value={`+${bonus}%`} color="#4ade80" />}
              </div>
              {/* 進度條對比（onlyGold 模式不顯示，因為進度不變） */}
              {opType !== 'onlyGold' && (
                <>
                  <div style={{ marginTop: 14 }}>
                    <BarCompare label="充值前" pct={cycPct} color="#6b7280" />
                    <BarCompare label="充值後" pct={afterCycPct} color="#fb923c" />
                  </div>
                  <div style={{ marginTop: 8, fontSize: 11, color: 'var(--text-muted)', display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                    <span>循環進度：{currentCycle.toLocaleString()} → {(completedExtra > 0 ? afterPoint : afterCycle).toLocaleString()} / 25,000</span>
                    {completedExtra > 0 && <span style={{ color: '#60a5fa', fontWeight: 700 }}>🎉 完成 {completedExtra} 輪！</span>}
                    {afterVip !== (info?.vipLevel ?? 0) && <span style={{ color: vipColor(afterVip), fontWeight: 700 }}>↑ 升級至 {vipLabel(afterVip)}</span>}
                  </div>
                </>
              )}
            </div>
          )}

          {/* 送出按鈕 */}
          <button onClick={doRecharge} disabled={loading || !canSubmit}
            style={{
              padding: isMobile ? '16px 0' : '14px 0',
              fontSize: isMobile ? 15 : 16, fontWeight: 800, borderRadius: 12, border: 'none',
              cursor: canSubmit ? 'pointer' : 'not-allowed', transition: 'background .2s',
              background: canSubmit ? 'linear-gradient(135deg,#22c55e,#16a34a)' : 'var(--bg-input)',
              color: canSubmit ? '#fff' : 'var(--text-muted)',
              boxShadow: canSubmit ? '0 4px 20px rgba(34,197,94,.3)' : 'none',
              WebkitTapHighlightColor: 'transparent',
              width: '100%',
            }}>
            {loading ? '⏳ 處理中…'
              : !info ? '⬅ 請先選擇玩家'
              : opType === null ? '⬅ 請選擇操作類型（STEP 4）'
              : opType === 'onlyGold'
                ? (finalGold > 0
                    ? `🎁 確認發放 ${info.onlineName} ${finalGold.toLocaleString()} 金幣（不計充值）`
                    : '⬅ 請選擇套餐以決定金幣數量')
              : finalTwd <= 0 ? '⬅ 請選擇套餐或輸入金額'
              : isMobile
                ? `💳 確認儲值 NT$${finalTwd.toLocaleString()}`
                : `💳 確認給予 ${info.onlineName} 儲值 NT$${finalTwd.toLocaleString()}`}
          </button>
        </div>
      </div>

      {/* ── 匯率計算機（合併 Tab）── */}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: isMobile ? 14 : 20, marginTop: isMobile ? 14 : 24 }}>
        <div style={{ fontSize: 14, fontWeight: 700, color: 'var(--text-primary)', marginBottom: 4 }}>💱 匯率試算工具</div>
        <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 14 }}>不影響實際操作，純粹試算用</div>

        {/* Tab 切換 */}
        <div style={{ display: 'flex', gap: 6, marginBottom: 16, background: 'var(--bg-input)', borderRadius: 8, padding: 4 }}>
          {([['twd', '台幣 → 金幣', '我輸入台幣，想知道能拿多少金幣'], ['gold', '金幣 → 台幣（反推）', '玩家要 X 金幣，需要充多少台幣？']] as const).map(([tab, label, hint]) => (
            <button key={tab} onClick={() => setCalcTab(tab as 'twd' | 'gold')}
              title={hint}
              style={{
                flex: 1, padding: isMobile ? '10px 6px' : '8px 12px', borderRadius: 6, cursor: 'pointer', transition: 'all .15s',
                background: calcTab === tab ? 'var(--bg-card)' : 'transparent',
                border: calcTab === tab ? '1px solid var(--border)' : '1px solid transparent',
                color: calcTab === tab ? 'var(--text-primary)' : 'var(--text-muted)',
                fontWeight: calcTab === tab ? 700 : 400, fontSize: isMobile ? 12 : 13,
                WebkitTapHighlightColor: 'transparent',
              }}>
              {label}
            </button>
          ))}
        </div>

        {calcTab === 'twd' ? (
          /* 台幣 → 金幣 */
          <>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10 }}>❓ 想知道充 X 元台幣，玩家能拿多少金幣（元寶）？</div>
            <div style={{ display: 'flex', gap: isMobile ? 10 : 16, alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <label style={{ minWidth: isMobile ? '100%' : 160 }}>
                <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>台幣金額 (NT$)</span>
                <input type="number" inputMode="numeric" value={calcTwd} onChange={e => setCalcTwd(e.target.value)}
                  placeholder="例如 1500" min={1} style={{ width: '100%', marginTop: 4, fontSize: 13 }} />
              </label>
              <div style={{ flex: isMobile ? '1 0 100%' : 'none' }}>
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 6 }}>若有額外贈金加成</div>
                <div style={{ display: 'grid', gridTemplateColumns: `repeat(${BONUSES.length}, 1fr)`, gap: 6 }}>
                  {BONUSES.map(b => (
                    <button key={b} onClick={() => setCalcBonus(b)}
                      style={{
                        padding: isMobile ? '10px 6px' : '6px 12px',
                        cursor: 'pointer', borderRadius: 6, fontSize: 12, fontWeight: calcBonus === b ? 700 : 400,
                        background: calcBonus === b ? (b > 0 ? 'rgba(74,222,128,.2)' : 'var(--bg-card)') : 'var(--bg-input)',
                        border: `1px solid ${calcBonus === b ? (b > 0 ? '#4ade80' : 'var(--border)') : 'var(--border)'}`,
                        color: b > 0 ? '#4ade80' : 'var(--text-secondary)',
                        WebkitTapHighlightColor: 'transparent',
                      }}>
                      {b === 0 ? (isMobile ? '無' : '無加成') : `+${b}%`}
                    </button>
                  ))}
                </div>
              </div>
            </div>
            {(() => {
              const n = parseInt(calcTwd) || 0
              if (n <= 0) return <div style={{ color: 'var(--text-muted)', fontSize: 12, marginTop: 12 }}>請輸入台幣金額…</div>
              const { baseGold, rate, tierLabel } = twdToGold(n)
              const bonusGold = Math.floor(baseGold * calcBonus / 100)
              const totalGold = baseGold + bonusGold
              return (
                <div style={{ marginTop: 14, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                  <CalcBox label="套用匯率" value={`${tierLabel}（${rate.toFixed(1)}金/NT$）`} />
                  <CalcBox label="基礎金幣" value={`${baseGold.toLocaleString()} 元寶`} color="#fb923c" />
                  {calcBonus > 0 && <CalcBox label={`+${calcBonus}% 贈金`} value={`+${bonusGold.toLocaleString()} 元寶`} color="#4ade80" />}
                  {calcBonus > 0 && <CalcBox label="實際合計" value={`${totalGold.toLocaleString()} 元寶`} color="#fbbf24" large />}
                </div>
              )
            })()}
          </>
        ) : (
          /* 金幣 → 台幣（反推）*/
          <>
            <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10 }}>❓ 玩家需要 X 金幣（元寶），最少需要充多少台幣？</div>
            <div style={{ display: 'flex', gap: isMobile ? 10 : 16, alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <label style={{ minWidth: isMobile ? '100%' : 170 }}>
                <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>目標金幣（元寶）</span>
                <input type="number" inputMode="numeric" value={calcGold} onChange={e => setCalcGold(e.target.value)}
                  placeholder="例如 200000" min={1}
                  style={{ width: '100%', marginTop: 4, fontSize: 13, color: '#fb923c' }} />
              </label>
              <div style={{ flex: isMobile ? '1 0 100%' : 'none' }}>
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 6 }}>若有額外贈金加成</div>
                <div style={{ display: 'grid', gridTemplateColumns: `repeat(${BONUSES.length}, 1fr)`, gap: 6 }}>
                  {BONUSES.map(b => (
                    <button key={b} onClick={() => setCalcRevBonus(b)}
                      style={{
                        padding: isMobile ? '10px 6px' : '6px 12px',
                        cursor: 'pointer', borderRadius: 6, fontSize: 12, fontWeight: calcRevBonus === b ? 700 : 400,
                        background: calcRevBonus === b ? (b > 0 ? 'rgba(74,222,128,.2)' : 'var(--bg-card)') : 'var(--bg-input)',
                        border: `1px solid ${calcRevBonus === b ? (b > 0 ? '#4ade80' : 'var(--border)') : 'var(--border)'}`,
                        color: b > 0 ? '#4ade80' : 'var(--text-secondary)',
                        WebkitTapHighlightColor: 'transparent',
                      }}>
                      {b === 0 ? (isMobile ? '無' : '無加成') : `+${b}%`}
                    </button>
                  ))}
                </div>
              </div>
            </div>
            {(() => {
              const g = parseInt(calcGold) || 0
              if (g <= 0) return <div style={{ color: 'var(--text-muted)', fontSize: 12, marginTop: 12 }}>請輸入金幣數量…</div>
              const options = goldToTwd(g, calcRevBonus)
              if (options.length === 0) return <div style={{ color: '#f87171', fontSize: 12, marginTop: 12 }}>無法計算</div>
              const best = options[0]
              const bonusNote = calcRevBonus > 0
                ? `（含 +${calcRevBonus}% 加成，基礎金幣需求 ${Math.ceil(g / (1 + calcRevBonus / 100)).toLocaleString()} 元寶）`
                : ''
              return (
                <div style={{ marginTop: 14 }}>
                  <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                    <CalcBox label="最少需充台幣" value={`NT$ ${best.twd.toLocaleString()}`} color="#fb923c" large />
                    <CalcBox label="套用匯率" value={`${best.tierLabel}（${best.rate.toFixed(1)}金/NT$）`} />
                    <CalcBox label="實際可得金幣" value={`${best.actualGold.toLocaleString()} 元寶`} color="#4ade80" />
                  </div>
                  {bonusNote && <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 8 }}>{bonusNote}</div>}
                  {options.length > 1 && (
                    <div style={{ marginTop: 10, fontSize: 12, color: 'var(--text-secondary)' }}>
                      其他方案：{options.slice(1, 3).map(o =>
                        <span key={o.twd} style={{ marginRight: 16, color: 'var(--text-muted)' }}>
                          NT${o.twd.toLocaleString()}（{o.tierLabel}）→ {o.actualGold.toLocaleString()} 金幣
                        </span>
                      )}
                    </div>
                  )}
                </div>
              )
            })()}
          </>
        )}
      </div>

      {/* ── 充值記錄 ── */}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: isMobile ? 14 : 20, marginTop: 14 }}>
        <div style={{ fontSize: 14, fontWeight: 700, color: 'var(--text-primary)', marginBottom: 14 }}>📋 充值記錄（訂單查詢）</div>
        <div style={{ display: 'flex', gap: 8, marginBottom: 14, flexWrap: isMobile ? 'wrap' : 'nowrap' }}>
          <input value={histQ} onChange={e => setHistQ(e.target.value)} onKeyDown={e => e.key === 'Enter' && loadHistory()}
            placeholder="角色名稱、帳號或商品（空=全部）"
            style={{ flex: 1, minWidth: 0, fontSize: 13, ...(isMobile ? { width: '100%', flexBasis: '100%' } : { maxWidth: 400 }) }} />
          <div style={{ display: 'flex', gap: 8, ...(isMobile ? { width: '100%' } : {}) }}>
            <button onClick={loadHistory} disabled={histLoading}
              style={{ flex: isMobile ? 1 : 'none', padding: '10px 20px', background: 'linear-gradient(135deg,#3b82f6,#2563eb)', color: '#fff', borderRadius: 8, fontSize: 13, fontWeight: 700, border: 'none', cursor: 'pointer', WebkitTapHighlightColor: 'transparent' }}>
              {histLoading ? '查詢中…' : `🔍 ${S.searchBtn}`}
            </button>
            {info && (
              <button onClick={() => { setHistQ(info.account); setTimeout(loadHistory, 50) }}
                style={{ flex: isMobile ? 1 : 'none', padding: '10px 14px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 8, fontSize: 13, cursor: 'pointer', WebkitTapHighlightColor: 'transparent' }}>
                此玩家
              </button>
            )}
          </div>
        </div>
        {histRows.length > 0 && <RechargeTable rows={histRows} />}
        {histRows.length === 0 && (
          <p style={{ color: 'var(--text-muted)', fontSize: 13 }}>點「查詢」載入充值記錄</p>
        )}
      </div>
    </div>
  )
}

// ── 子元件 ──────────────────────────────────────────────────────

function StepLabel({ n, text, sub, warn }: { n: number; text: string; sub?: string; warn?: boolean }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <div style={{ width: 26, height: 26, borderRadius: '50%', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 12, fontWeight: 800,
        background: warn ? 'rgba(251,191,36,.2)' : 'rgba(59,130,246,.2)', color: warn ? '#fbbf24' : '#60a5fa' }}>
        {n}
      </div>
      <div>
        <span style={{ fontWeight: 700, fontSize: 14, color: 'var(--text-primary)' }}>{text}</span>
        {sub && <span style={{ fontSize: 11, color: 'var(--text-muted)', marginLeft: 8 }}>{sub}</span>}
      </div>
    </div>
  )
}

function StatBox({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div style={{ background: 'var(--bg-input)', borderRadius: 8, padding: '10px 12px' }}>
      <div style={{ fontSize: 10, color: 'var(--text-muted)', marginBottom: 3 }}>{label}</div>
      <div style={{ fontSize: 14, fontWeight: 800, color }}>{value}</div>
    </div>
  )
}

function OpCard({ selected, onClick, icon, color, title, desc, badge }: {
  selected: boolean; onClick: () => void; icon: string; color: string; title: string; desc: string; badge?: string
}) {
  return (
    <button onClick={onClick} style={{
      textAlign: 'left', cursor: 'pointer', padding: '14px 16px', borderRadius: 10, transition: 'all .15s',
      background: selected ? `${color}18` : 'var(--bg-input)',
      border: `2px solid ${selected ? color : 'transparent'}`,
      boxShadow: selected ? `0 0 12px ${color}30` : 'none',
      WebkitTapHighlightColor: 'transparent',
      minHeight: 80, position: 'relative',
    }}>
      {badge && (
        <div style={{ position: 'absolute', top: 8, right: 8, fontSize: 9, fontWeight: 800, padding: '2px 6px', borderRadius: 10,
          background: `${color}30`, color, border: `1px solid ${color}60` }}>
          {badge}
        </div>
      )}
      <div style={{ fontSize: 20, marginBottom: 4 }}>{icon}</div>
      <div style={{ fontSize: 13, fontWeight: 700, color: selected ? color : 'var(--text-primary)', lineHeight: 1.3 }}>{title}</div>
      <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 4, lineHeight: 1.5 }}>{desc}</div>
    </button>
  )
}

function PreviewCard({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <div style={{ background: 'var(--bg-input)', borderRadius: 8, padding: '10px 14px' }}>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>{label}</div>
      <div style={{ fontSize: 15, fontWeight: 800, color }}>{value}</div>
    </div>
  )
}

function BarCompare({ label, pct, color }: { label: string; pct: number; color: string }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
      <span style={{ fontSize: 11, color: 'var(--text-muted)', width: 40, textAlign: 'right', flexShrink: 0 }}>{label}</span>
      <div style={{ flex: 1, height: 8, background: 'var(--bg-input)', borderRadius: 4, overflow: 'hidden' }}>
        <div style={{ width: `${pct}%`, height: '100%', background: color, borderRadius: 4, transition: 'width .4s' }} />
      </div>
      <span style={{ fontSize: 11, color, fontWeight: 700, width: 30, textAlign: 'right', flexShrink: 0 }}>{pct}%</span>
    </div>
  )
}

function CalcBox({ label, value, color, large }: { label: string; value: string; color?: string; large?: boolean }) {
  return (
    <div style={{ background: 'var(--bg-input)', borderRadius: 8, padding: '10px 14px', minWidth: 130 }}>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>{label}</div>
      <div style={{ fontSize: large ? 18 : 14, fontWeight: 800, color: color || 'var(--text-primary)' }}>{value}</div>
    </div>
  )
}

function RechargeTable({ rows }: { rows: RechargeOrder[] }) {
  const orderRows   = rows.filter(r => r.source === 'orders')
  const paydataRows = rows.filter(r => r.source === 'paydata')
  const totalTwd    = orderRows.filter(r => r.status === 'completed').reduce((s, r) => s + (r.twd || 0), 0)
  const totalGold   = orderRows.filter(r => r.status === 'completed').reduce((s, r) => s + (r.yuanbao || 0), 0)
  return (
    <>
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 10, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 13, color: '#4ade80', fontWeight: 600 }}>
          訂單 {orderRows.length} 筆 ｜元寶：{totalGold.toLocaleString()} ｜台幣：NT${totalTwd.toLocaleString()}
        </span>
        {paydataRows.length > 0 && (
          <span style={{ fontSize: 12, color: '#fb923c', fontWeight: 600, padding: '2px 10px', background: 'rgba(251,146,60,.1)', borderRadius: 6, border: '1px solid rgba(251,146,60,.3)' }}>
            🔶 付費系統記錄 {paydataRows.length} 筆（無訂單）
          </span>
        )}
      </div>
      <div style={{ border: '1px solid var(--border)', borderRadius: 10, overflow: 'auto', maxHeight: 500 }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
          <thead>
            <tr style={{ background: 'var(--bg-sidebar)', position: 'sticky', top: 0 }}>
              <th style={TH}>時間</th><th style={TH}>角色</th><th style={TH}>帳號</th>
              <th style={TH}>商品 / 說明</th>
              <th style={{ ...TH, textAlign: 'right' }}>元寶</th>
              <th style={{ ...TH, textAlign: 'right' }}>台幣 NT$</th>
              <th style={TH}>狀態</th>
              <th style={TH}>訂單號</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => {
              const isPay = r.source === 'paydata'
              return (
                <tr key={i} style={{
                  borderBottom: '1px solid var(--border)',
                  background: isPay ? 'rgba(251,146,60,.06)' : r.status === 'failed' ? 'rgba(248,113,113,.05)' : i % 2 === 0 ? 'transparent' : 'rgba(255,255,255,.02)'
                }}>
                  <td style={TD}>{r.time}</td>
                  <td style={{ ...TD, fontWeight: 600, color: isPay ? '#fb923c' : undefined }}>{r.charName || '—'}</td>
                  <td style={{ ...TD, color: isPay ? '#fb923c' : '#60a5fa' }}>{r.account}</td>
                  <td style={{ ...TD, color: isPay ? '#fb923c' : undefined }}>{r.productName}</td>
                  <td style={{ ...TD, textAlign: 'right', color: isPay ? '#fb923c' : '#60a5fa', fontWeight: 700 }}>
                    {isPay ? `累計 ${r.yuanbao.toLocaleString()}` : r.yuanbao.toLocaleString()}
                  </td>
                  <td style={{ ...TD, textAlign: 'right', color: '#fb923c', fontWeight: 700 }}>
                    {isPay ? `累計 NT$${r.twd.toLocaleString()}` : r.twd.toLocaleString()}
                  </td>
                  <td style={TD}>
                    <span style={{ padding: '2px 8px', borderRadius: 12, fontSize: 11, fontWeight: 700,
                      background: isPay ? 'rgba(251,146,60,.15)' : r.status === 'completed' ? 'rgba(74,222,128,.15)' : r.status === 'failed' ? 'rgba(248,113,113,.15)' : 'var(--bg-input)',
                      color: isPay ? '#fb923c' : r.status === 'completed' ? '#4ade80' : r.status === 'failed' ? '#f87171' : 'var(--text-muted)' }}>
                      {isPay ? '付費記錄' : r.status === 'completed' ? '✓ 成功' : r.status === 'failed' ? '✗ 失敗' : r.status}
                    </span>
                  </td>
                  <td style={{ ...TD, color: 'var(--text-muted)', fontSize: 11 }}>{r.orderNo || (isPay ? '—（付費系統）' : '—')}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </>
  )
}

const TH: React.CSSProperties = { padding: '10px 14px', color: 'var(--text-muted)', fontWeight: 700, whiteSpace: 'nowrap', fontSize: 11, textTransform: 'uppercase', letterSpacing: .5 }
const TD: React.CSSProperties = { padding: '9px 14px', whiteSpace: 'nowrap' }
