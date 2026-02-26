import { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'
import { S } from '../strings'

// ── 套餐預設（與 EXE PayTotalDialog 一致）──────────────────
const TIERS = [
  { label: 'NT$100',  subLabel: '1萬金',   twd: 100,   gold: 10_000 },
  { label: 'NT$300',  subLabel: '3.2萬',   twd: 300,   gold: 32_000 },
  { label: 'NT$500',  subLabel: '5.5萬',   twd: 500,   gold: 55_000 },
  { label: 'NT$1K',   subLabel: '11.5萬',  twd: 1_000, gold: 115_000 },
  { label: 'NT$3K',   subLabel: '36萬',    twd: 3_000, gold: 360_000 },
  { label: 'NT$5K',   subLabel: '62.5萬',  twd: 5_000, gold: 625_000 },
  { label: 'NT$10K',  subLabel: '130萬',   twd: 10_000,gold: 1_300_000 },
]
const BONUSES = [0, 5, 10, 15, 20]
const CYCLE_MAX = 25_000

interface PaydataInfo {
  account: string; onlineName: string; isOnline: boolean
  gold: number; crystal: number; payTotal: number
  paydataPoint: number; totalCheck: number; lifetimeTotal: number; vipLevel: number
  paydataCheck: number   // 0=待領獎, 1=已領
  claimReady: boolean    // true = 本輪獎勵可發放
}

interface RechargeOrder {
  orderNo: string; account: string; charName: string; productName: string
  yuanbao: number; twd: number; status: string; time: string; source: string
  lifetimeTotal?: number; totalCheck?: number
}

export default function RechargePage() {
  const [sp] = useSearchParams()

  // ── 玩家選擇 ───────────────────────────────
  const [playerQ, setPlayerQ] = useState(sp.get('account') || '')
  const [info, setInfo] = useState<PaydataInfo | null>(null)
  const [infoLoading, setInfoLoading] = useState(false)

  // ── 套餐選擇 ──────────────────────────────
  const [selectedTier, setSelectedTier] = useState<typeof TIERS[0] | null>(null)
  const [bonus, setBonus] = useState(0)
  const [customTwd, setCustomTwd] = useState('')
  const [customGold, setCustomGold] = useState('')
  const [giveGold, setGiveGold] = useState(true)

  // ── 計算結果 ──────────────────────────────
  const finalTwd  = selectedTier ? selectedTier.twd  : parseInt(customTwd, 10) || 0
  const finalGold = selectedTier
    ? Math.floor(selectedTier.gold * (1 + bonus / 100))
    : parseInt(customGold, 10) || 0

  // 預覽循環
  const currentCycle  = info ? info.paydataPoint : 0
  const afterCycle    = finalTwd > 0 ? currentCycle + finalTwd : currentCycle
  const completedExtra = afterCycle > 0 ? Math.floor((afterCycle - 1) / CYCLE_MAX) - Math.floor((currentCycle > 0 ? (currentCycle - 1) / CYCLE_MAX : 0)) : 0
  const afterPoint    = afterCycle - Math.floor((afterCycle > 0 ? (afterCycle - 1) / CYCLE_MAX : 0)) * CYCLE_MAX
  const afterPayTotal = (info?.payTotal ?? 0) + (giveGold ? finalTwd : 0)
  const afterVip      = afterPayTotal >= 15000 ? 2 : afterPayTotal >= 5000 ? 1 : 0
  const cycPct        = Math.min(100, Math.round((currentCycle / CYCLE_MAX) * 100))
  const afterCycPct   = Math.min(100, Math.round(((afterPoint > 0 ? afterPoint : currentCycle) / CYCLE_MAX) * 100))

  // ── 狀態訊息 ─────────────────────────────
  const [loading, setLoading] = useState(false)
  const [msg, setMsg] = useState('')
  const [msgOk, setMsgOk] = useState(true)

  // ── 充值記錄 ─────────────────────────────
  const [histQ, setHistQ] = useState('')
  const [histRows, setHistRows] = useState<RechargeOrder[]>([])
  const [histLoading, setHistLoading] = useState(false)

  useEffect(() => {
    const acc = sp.get('account')
    if (acc) loadPlayer(acc)
  }, [])

  const loadPlayer = async (q: string) => {
    if (!q.trim()) return
    setInfoLoading(true); setInfo(null)
    try {
      const r = await api.get(`/players/${encodeURIComponent(q.trim())}/paydata`)
      setInfo(r.data)
      // VIP 預設加成
      const vip = r.data.vipLevel
      setBonus(vip === 2 ? 10 : vip === 1 ? 5 : 0)
    } catch {
      setMsg('找不到玩家')
      setMsgOk(false)
    } finally { setInfoLoading(false) }
  }

  const handleSelectTier = (tier: typeof TIERS[0]) => {
    setSelectedTier(prev => prev === tier ? null : tier)
    setCustomTwd(''); setCustomGold('')
  }

  const doRecharge = async () => {
    if (!info) { setMsg('請先搜尋並選定玩家'); setMsgOk(false); return }
    if (finalTwd <= 0) { setMsg('請選擇套餐或輸入台幣金額'); setMsgOk(false); return }
    if (giveGold && finalGold <= 0) { setMsg('請輸入金幣數量'); setMsgOk(false); return }
    setLoading(true); setMsg('')
    try {
      const r = await api.post(`/players/${info.account}/recharge`, {
        twdAmount: finalTwd,
        goldAmount: giveGold ? finalGold : 0,
        giveGold,
      })
      setMsg(r.data.message || '✓ 儲值成功')
      setMsgOk(true)
      setSelectedTier(null)
      setCustomTwd(''); setCustomGold('')
      await loadPlayer(info.account)
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err.response?.data?.message || '儲值失敗'); setMsgOk(false)
    } finally { setLoading(false) }
  }

  const doFix = async () => {
    if (!info) return
    setLoading(true)
    try {
      const r = await api.post(`/players/${info.account}/paydata/fix`)
      setMsg(r.data.message); setMsgOk(true)
      await loadPlayer(info.account)
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err.response?.data?.message || '修復失敗'); setMsgOk(false)
    } finally { setLoading(false) }
  }

  const doClaim = async () => {
    if (!info) return
    // 前端防呆：按鈕只有 claimReady 才顯示，這裡再確認一次
    if (!info.claimReady) { setMsg('⚠ 無可發放的獎勵'); setMsgOk(false); return }
    if (!window.confirm(`確定要發放「${info.onlineName}」第 ${info.totalCheck} 輪的累積獎勵？\n\n· check 將設為 1（已領）\n· 下次達成 25,000 才能再領`)) return
    setLoading(true)
    try {
      const r = await api.post(`/players/${info.account}/paydata/claim`)
      setMsg(r.data.message); setMsgOk(true)
      await loadPlayer(info.account)
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err.response?.data?.message || '操作失敗'); setMsgOk(false)
    } finally { setLoading(false) }
  }

  const doReset = async () => {
    if (!info) return
    if (!window.confirm(`確定要將「${info.onlineName}」的累積充值進度歸零？\n\n· paydata.point → 0\n· check / totalcheck → 0\n\n⚠ 此操作無法復原`)) return
    setLoading(true)
    try {
      const r = await api.post(`/players/${info.account}/paydata/reset`)
      setMsg(r.data.message); setMsgOk(true)
      await loadPlayer(info.account)
    } catch { setMsg('重置失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const loadHistory = async () => {
    setHistLoading(true)
    try {
      const r = await api.get('/players/recharge', { params: { q: histQ } })
      setHistRows(Array.isArray(r.data) ? r.data : [])
    } finally { setHistLoading(false) }
  }

  const vipLabel = (v: number) => v === 2 ? '💎 鑽石 VIP' : v === 1 ? '🥇 黃金 VIP' : '一般玩家'
  const vipColor = (v: number) => v === 2 ? 'var(--accent-blue)' : v === 1 ? 'var(--accent-orange)' : 'var(--text-muted)'

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>💰 {S.pageRecharge}</h1>

      {msg && (
        <div style={{ background: msgOk ? 'rgba(86,196,118,.12)' : 'rgba(245,101,101,.1)', border: `1px solid ${msgOk ? 'var(--accent-green)' : 'var(--accent-red)'}`, borderRadius: 8, padding: '10px 16px', marginBottom: 16, color: msgOk ? 'var(--accent-green)' : 'var(--accent-red)', fontSize: 13 }}>
          {msg}
        </div>
      )}

      <div style={{ display: 'flex', gap: 18, alignItems: 'flex-start' }}>
        {/* ── 左：玩家選擇 + 狀態 ── */}
        <div style={{ width: 280, flexShrink: 0 }}>
          <Card title="STEP 1 — 選定玩家">
            <div style={{ display: 'flex', gap: 6, marginBottom: 8 }}>
              <input value={playerQ} onChange={e => setPlayerQ(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && loadPlayer(playerQ)}
                placeholder="帳號或角色名稱" style={{ flex: 1, fontSize: 13 }} />
              <button onClick={() => loadPlayer(playerQ)} disabled={infoLoading}
                style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 10px', fontSize: 12 }}>
                {infoLoading ? '…' : '🔍'}
              </button>
            </div>

            {info && (
              <div style={{ background: 'var(--bg-input)', borderRadius: 6, padding: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 8 }}>
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: info.isOnline ? 'var(--accent-green)' : 'var(--text-muted)', display: 'inline-block', flexShrink: 0 }} />
                  <span style={{ fontWeight: 700, fontSize: 14 }}>{info.onlineName}</span>
                  <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{info.account}</span>
                </div>
                <Row label="金幣（元寶）" value={<span style={{ color: 'var(--accent-orange)', fontWeight: 700 }}>{info.gold.toLocaleString()}</span>} />
                <Row label="水晶" value={<span style={{ color: 'var(--accent-blue)' }}>{info.crystal.toLocaleString()}</span>} />
                <Row label="累積儲值" value={<span style={{ color: 'var(--accent-orange)' }}>NT${info.payTotal.toLocaleString()}</span>} />
                <Row label="VIP 等級" value={<span style={{ color: vipColor(info.vipLevel), fontWeight: 700 }}>{vipLabel(info.vipLevel)}</span>} />

                {/* 循環進度條 */}
                <div style={{ marginTop: 10, marginBottom: 6 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>
                    <span>累積充值獎勵進度（循環 #{(info.totalCheck || 0) + 1}）</span>
                    <span>NT${info.paydataPoint.toLocaleString()} / $25,000</span>
                  </div>
                  <div style={{ background: 'var(--bg-card)', borderRadius: 4, height: 8, overflow: 'hidden' }}>
                    <div style={{ width: `${cycPct}%`, height: '100%', background: 'var(--accent-orange)', transition: 'width .3s' }} />
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
                    歷史總計 NT${(info.lifetimeTotal || info.payTotal).toLocaleString()} | 完成 {info.totalCheck} 輪
                  </div>
                </div>

                {/* 累積獎勵發放按鈕（防呆：claimReady = check==0 && totalCheck>0） */}
                {info.claimReady && (
                  <button onClick={doClaim} disabled={loading}
                    style={{ width: '100%', marginTop: 8, padding: '7px 0', fontSize: 13, fontWeight: 700,
                      background: 'rgba(250,204,21,.15)', border: '1px solid rgba(250,204,21,.6)',
                      borderRadius: 6, color: '#fbbf24', cursor: 'pointer' }}>
                    🎁 發放第 {info.totalCheck} 輪累積獎勵
                  </button>
                )}
                {!info.claimReady && info.totalCheck > 0 && (
                  <div style={{ fontSize: 11, color: 'var(--accent-green)', marginTop: 6, textAlign: 'center' }}>
                    ✓ 第 {info.totalCheck} 輪獎勵已發放
                  </div>
                )}

                {/* 維護按鈕 */}
                <div style={{ display: 'flex', gap: 6, marginTop: 10 }}>
                  <button onClick={doFix} disabled={loading} title="修復 point > 25000 的循環錯誤"
                    style={{ flex: 1, fontSize: 11, padding: '4px 0', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>
                    🔧 修復循環
                  </button>
                  <button onClick={doReset} disabled={loading}
                    style={{ flex: 1, fontSize: 11, padding: '4px 0', background: 'rgba(245,101,101,.15)', border: '1px solid var(--accent-red)', borderRadius: 4, cursor: 'pointer', color: 'var(--accent-red)' }}>
                    🗑 清0進度
                  </button>
                </div>
              </div>
            )}
          </Card>
        </div>

        {/* ── 中：給予儲值 ── */}
        <div style={{ flex: 1, minWidth: 0 }}>
          <Card title="STEP 2 — 選擇套餐">
            <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10 }}>1台幣 = 100金幣（大額有加成）</div>
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 12 }}>
              {TIERS.map(tier => (
                <button key={tier.twd} onClick={() => handleSelectTier(tier)}
                  style={{
                    padding: '8px 10px', minWidth: 72, textAlign: 'center', cursor: 'pointer',
                    background: selectedTier === tier ? 'var(--accent-blue)' : 'var(--bg-input)',
                    border: `1px solid ${selectedTier === tier ? 'var(--accent-blue)' : 'var(--border)'}`,
                    borderRadius: 6, transition: 'all .15s',
                  }}>
                  <div style={{ fontSize: 13, fontWeight: 700, color: selectedTier === tier ? '#fff' : 'var(--text-primary)' }}>{tier.label}</div>
                  <div style={{ fontSize: 10, color: selectedTier === tier ? 'rgba(255,255,255,.8)' : 'var(--text-muted)', marginTop: 2 }}>{tier.subLabel}</div>
                </button>
              ))}
            </div>

            {/* 回饋加成 */}
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontSize: 12, color: 'var(--accent-orange)', fontWeight: 700, marginBottom: 8 }}>
                STEP 3 — 選擇回饋加成
                {info && info.vipLevel > 0 && <span style={{ color: vipColor(info.vipLevel), marginLeft: 6 }}>（{vipLabel(info.vipLevel)} 已自動套用）</span>}
              </div>
              <div style={{ display: 'flex', gap: 6 }}>
                {BONUSES.map(b => (
                  <button key={b} onClick={() => setBonus(b)}
                    style={{ padding: '6px 14px', cursor: 'pointer', borderRadius: 4,
                      background: bonus === b ? (b > 0 ? 'rgba(86,196,118,.25)' : 'var(--bg-card)') : 'var(--bg-input)',
                      border: `1px solid ${bonus === b ? (b > 0 ? 'var(--accent-green)' : 'var(--border)') : 'var(--border)'}`,
                      color: b > 0 ? 'var(--accent-green)' : 'var(--text-secondary)', fontSize: 13, fontWeight: bonus === b ? 700 : 400 }}>
                    {b === 0 ? '無加成' : `+${b}%`}
                  </button>
                ))}
              </div>
            </div>

            {/* 手動輸入 */}
            <div style={{ marginBottom: 12, padding: '10px 12px', background: 'var(--bg-input)', borderRadius: 6 }}>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 8 }}>或手動輸入（不選套餐）</div>
              <div style={{ display: 'flex', gap: 10, alignItems: 'flex-end' }}>
                <label style={{ flex: 1 }}>
                  <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>台幣金額 (NT$)</span>
                  <input type="number" value={customTwd} onChange={e => { setCustomTwd(e.target.value); setSelectedTier(null) }}
                    placeholder="例 500" min={1} style={{ width: '100%', marginTop: 2 }} />
                </label>
                <label style={{ flex: 1 }}>
                  <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>金幣數量</span>
                  <input type="number" value={customGold} onChange={e => { setCustomGold(e.target.value); setSelectedTier(null) }}
                    placeholder={`例 ${(parseInt(customTwd,10)||0) * 100}`} min={0} style={{ width: '100%', marginTop: 2 }} />
                </label>
              </div>
            </div>

            {/* 是否發放金幣 */}
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12, cursor: 'pointer', fontSize: 13 }}>
              <input type="checkbox" checked={giveGold} onChange={e => setGiveGold(e.target.checked)} />
              <span>同時發放金幣（將 +{finalGold.toLocaleString()} 元寶 加入玩家帳戶）</span>
            </label>

            {/* 預覽 */}
            {finalTwd > 0 && (
              <div style={{ background: 'rgba(86,196,118,.08)', border: '1px solid rgba(86,196,118,.3)', borderRadius: 8, padding: '12px 16px', marginBottom: 14 }}>
                <div style={{ fontSize: 12, fontWeight: 700, color: 'var(--accent-green)', marginBottom: 8 }}>📋 確認預覽</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px 16px', fontSize: 13 }}>
                  <PreviewRow label="台幣金額" value={`NT$${finalTwd.toLocaleString()}`} />
                  {giveGold && <PreviewRow label="金幣入帳" value={`+${finalGold.toLocaleString()} 元寶`} color="var(--accent-orange)" />}
                  {bonus > 0 && <PreviewRow label="回饋加成" value={`+${bonus}%`} color="var(--accent-green)" />}
                  <PreviewRow label="循環進度" value={`${currentCycle.toLocaleString()} → ${(afterCycle > CYCLE_MAX ? afterPoint : afterCycle).toLocaleString()}/25,000`} />
                  {completedExtra > 0 && <PreviewRow label="跨越循環" value={`完成 ${completedExtra} 輪！check 歸零`} color="var(--accent-blue)" />}
                  {afterVip !== (info?.vipLevel ?? 0) && <PreviewRow label="VIP 升級" value={vipLabel(afterVip)} color={vipColor(afterVip)} />}
                </div>
                {/* 進度條預覽 */}
                <div style={{ marginTop: 10 }}>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 3 }}>
                    <div style={{ flex: 1, background: 'var(--bg-card)', borderRadius: 4, height: 8, overflow: 'hidden' }}>
                      <div style={{ width: `${cycPct}%`, height: '100%', background: 'var(--text-muted)' }} />
                    </div>
                    <span style={{ fontSize: 10, color: 'var(--text-muted)', width: 30 }}>前</span>
                  </div>
                  <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <div style={{ flex: 1, background: 'var(--bg-card)', borderRadius: 4, height: 8, overflow: 'hidden' }}>
                      <div style={{ width: `${afterCycPct}%`, height: '100%', background: 'var(--accent-orange)' }} />
                    </div>
                    <span style={{ fontSize: 10, color: 'var(--accent-orange)', width: 30 }}>後</span>
                  </div>
                </div>
              </div>
            )}

            <button onClick={doRecharge} disabled={loading || !info || finalTwd <= 0}
              style={{ width: '100%', background: 'var(--accent-green)', color: '#fff', padding: '11px 0', fontSize: 15, fontWeight: 700, borderRadius: 8,
                       opacity: (!info || finalTwd <= 0) ? 0.5 : 1 }}>
              {loading ? '處理中…' : info ? `💰 確認給予 ${info.onlineName || info.account} 儲值` : '請先選擇玩家'}
            </button>
          </Card>
        </div>
      </div>

      {/* ── 充值記錄 ── */}
      <Card title="💳 充值記錄（訂單查詢）">
        <div style={{ display: 'flex', gap: 8, marginBottom: 14 }}>
          <input value={histQ} onChange={e => setHistQ(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && loadHistory()}
            placeholder="角色名稱、帳號或商品（空=全部）" style={{ flex: 1, maxWidth: 400 }} />
          <button onClick={loadHistory} disabled={histLoading}
            style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 16px' }}>
            {histLoading ? '查詢中…' : `🔍 ${S.searchBtn}`}
          </button>
          {info && (
            <button onClick={() => { setHistQ(info.account); setTimeout(loadHistory, 50) }}
              style={{ background: 'var(--bg-input)', border: '1px solid var(--border)', padding: '8px 12px', fontSize: 13 }}>
              查此玩家
            </button>
          )}
        </div>
        {histRows.length > 0 && (
          <RechargeTable rows={histRows} />
        )}
        {histRows.length === 0 && histQ === '' && (
          <p style={{ color: 'var(--text-muted)', fontSize: 13, padding: '12px 0' }}>點「查詢」載入充值記錄</p>
        )}
      </Card>
    </div>
  )
}

// ── 子元件 ────────────────────────────────────────────────

function RechargeTable({ rows }: { rows: RechargeOrder[] }) {
  const isOrders = rows[0]?.source === 'orders'
  const totalTwd = rows.filter(r => r.status === 'completed' || r.source === 'paydata').reduce((s, r) => s + (r.twd || 0), 0)
  const totalGold = rows.filter(r => r.status === 'completed' || r.source === 'paydata').reduce((s, r) => s + (r.yuanbao || 0), 0)

  return (
    <>
      <div style={{ fontSize: 13, color: 'var(--accent-green)', marginBottom: 10 }}>
        共 {rows.length} 筆  {isOrders && `| 合計元寶：${totalGold.toLocaleString()}  | 換算台幣：NT$${totalTwd.toLocaleString()}`}
      </div>
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, overflow: 'auto', maxHeight: 400 }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
          <thead>
            <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
              <th style={TH}>時間</th>
              <th style={TH}>角色</th>
              <th style={TH}>帳號</th>
              {isOrders && <th style={TH}>商品</th>}
              <th style={{ ...TH, textAlign: 'right' }}>元寶</th>
              <th style={{ ...TH, textAlign: 'right' }}>台幣 (NT$)</th>
              {isOrders && <th style={TH}>狀態</th>}
              {isOrders && <th style={TH}>訂單編號</th>}
              {!isOrders && <th style={{ ...TH, textAlign: 'right' }}>歷史總計</th>}
              {!isOrders && <th style={{ ...TH, textAlign: 'right' }}>完成輪數</th>}
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr key={i} style={{ borderBottom: '1px solid var(--border)', background: r.status === 'failed' ? 'rgba(245,101,101,.05)' : undefined }}>
                <td style={TD}>{r.time}</td>
                <td style={TD}>{r.charName || '—'}</td>
                <td style={{ ...TD, color: 'var(--accent-blue)' }}>{r.account}</td>
                {isOrders && <td style={TD}>{r.productName}</td>}
                <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-blue)', fontWeight: 600 }}>{r.yuanbao.toLocaleString()}</td>
                <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-orange)', fontWeight: 600 }}>{r.twd.toLocaleString()}</td>
                {isOrders && (
                  <td style={TD}>
                    <span style={{ color: r.status === 'completed' ? 'var(--accent-green)' : r.status === 'failed' ? 'var(--accent-red)' : 'var(--text-muted)', fontWeight: 600 }}>
                      {r.status === 'completed' ? '✓ 成功' : r.status === 'failed' ? '✗ 失敗' : r.status}
                    </span>
                  </td>
                )}
                {isOrders && <td style={{ ...TD, color: 'var(--text-muted)', fontSize: 11 }}>{r.orderNo}</td>}
                {!isOrders && <td style={{ ...TD, textAlign: 'right', color: 'var(--text-muted)' }}>{(r as RechargeOrder & { lifetimeTotal?: number }).lifetimeTotal?.toLocaleString() ?? '—'}</td>}
                {!isOrders && <td style={{ ...TD, textAlign: 'right', color: 'var(--text-muted)' }}>{(r as RechargeOrder & { totalCheck?: number }).totalCheck?.toLocaleString() ?? '—'}</td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

const TH: React.CSSProperties = { padding: '8px 12px', color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap' }
const TD: React.CSSProperties = { padding: '8px 12px', whiteSpace: 'nowrap' }

const Card = ({ title, children }: { title: string; children: React.ReactNode }) => (
  <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 18, marginBottom: 18 }}>
    <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 14 }}>{title}</h3>
    {children}
  </div>
)
const Row = ({ label, value }: { label: string; value: React.ReactNode }) => (
  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, padding: '2px 0' }}>
    <span style={{ color: 'var(--text-muted)' }}>{label}</span>
    <span>{value}</span>
  </div>
)
const PreviewRow = ({ label, value, color }: { label: string; value: string; color?: string }) => (
  <>
    <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>{label}</span>
    <span style={{ color: color || 'var(--text-primary)', fontWeight: 600, fontSize: 13 }}>{value}</span>
  </>
)
