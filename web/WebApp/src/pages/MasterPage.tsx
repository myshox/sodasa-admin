import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import { S } from '../strings'

// ── 套餐預設（與 RechargePage 一致）──────────────────────────
const TIERS = [
  { label: 'NT$100',  subLabel: '1萬金',   twd: 100,    gold: 10_000 },
  { label: 'NT$300',  subLabel: '3.2萬',   twd: 300,    gold: 32_000 },
  { label: 'NT$500',  subLabel: '5.5萬',   twd: 500,    gold: 55_000 },
  { label: 'NT$1K',   subLabel: '11.5萬',  twd: 1_000,  gold: 115_000 },
  { label: 'NT$3K',   subLabel: '36萬',    twd: 3_000,  gold: 360_000 },
  { label: 'NT$5K',   subLabel: '62.5萬',  twd: 5_000,  gold: 625_000 },
  { label: 'NT$10K',  subLabel: '130萬',   twd: 10_000, gold: 1_300_000 },
]
const BONUSES = [0, 5, 10, 15, 20]

function twdToGold(twd: number): number {
  if (twd <= 0) return 0
  let best = TIERS[0]
  for (const t of TIERS) { if (twd >= t.twd) best = t }
  return Math.floor(twd * (best.gold / best.twd))
}

// ── 介面定義 ────────────────────────────────────────────────
interface CharInfo {
  account: string; charName: string; isOnline: boolean
  gold: number; crystal: number; payTotal: number
  loginTime: string; isBanned: boolean; petCount: number
}
interface MasterInfo { masterName: string; chars: CharInfo[] }

// 每個 CDKEY 的分配儲值輸入狀態
interface SplitEntry {
  account: string
  twdMode: 'tier' | 'custom'
  selectedTierTwd: number   // 0 = 未選
  selectedTierGold: number
  customTwd: string
  bonusPct: number
  enabled: boolean          // 勾選此 CDKEY 才計入
}

function makeSplitEntry(account: string): SplitEntry {
  return { account, twdMode: 'tier', selectedTierTwd: 0, selectedTierGold: 0, customTwd: '', bonusPct: 0, enabled: false }
}

// 計算實際 NT$ 和金幣
function calcEntry(e: SplitEntry): { twd: number; baseGold: number; totalGold: number } {
  let twd = 0, baseGold = 0
  if (e.twdMode === 'tier' && e.selectedTierTwd > 0) {
    twd = e.selectedTierTwd; baseGold = e.selectedTierGold
  } else {
    twd = parseInt(e.customTwd, 10) || 0
    baseGold = twdToGold(twd)
  }
  const totalGold = Math.floor(baseGold * (1 + e.bonusPct / 100))
  return { twd, baseGold, totalGold }
}

// ── 樣式輔助 ────────────────────────────────────────────────
const card: React.CSSProperties = {
  background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, marginBottom: 16,
}

export default function MasterPage() {
  const navigate = useNavigate()
  const [q,    setQ]    = useState('')
  const [info, setInfo] = useState<MasterInfo | null>(null)
  const [loading, setLoading] = useState(false)
  const [err, setErr] = useState('')

  // 分配儲值模式
  const [splitMode, setSplitMode] = useState(false)
  const [splits, setSplits] = useState<SplitEntry[]>([])
  const [splitLoading, setSplitLoading] = useState(false)
  const [splitResult, setSplitResult] = useState<{ account: string; ok: boolean; msg: string }[] | null>(null)

  const search = async () => {
    if (!q.trim()) return
    setLoading(true); setErr(''); setInfo(null); setSplitMode(false); setSplitResult(null)
    try {
      const r = await api.get(`/players/master/${encodeURIComponent(q.trim())}`)
      setInfo(r.data)
      setSplits(r.data.chars.map((c: CharInfo) => makeSplitEntry(c.account)))
    } catch { setErr('找不到主帳號') }
    finally { setLoading(false) }
  }

  // 更新某個 CDKEY 的分配條目
  const updateSplit = (idx: number, patch: Partial<SplitEntry>) =>
    setSplits(prev => prev.map((e, i) => i === idx ? { ...e, ...patch } : e))

  // 全部填同一套餐 / 優惠
  const applyToAll = (patch: Partial<SplitEntry>) =>
    setSplits(prev => prev.map(e => e.enabled ? { ...e, ...patch } : e))

  const totalTwd   = splits.filter(e => e.enabled).reduce((s, e) => s + calcEntry(e).twd, 0)
  const totalGold  = splits.filter(e => e.enabled).reduce((s, e) => s + calcEntry(e).totalGold, 0)
  const activeCount = splits.filter(e => e.enabled).length

  const doSplitRecharge = async () => {
    const items = splits
      .filter(e => e.enabled && calcEntry(e).twd > 0)
      .map(e => {
        const { twd, totalGold } = calcEntry(e)
        return { account: e.account, twdAmount: twd, goldAmount: totalGold, giveGold: true, bonusPct: e.bonusPct }
      })
    if (items.length === 0) { setSplitResult([{ account: '', ok: false, msg: '⚠ 未勾選任何有效項目' }]); return }

    const lines = items.map(it => `• ${it.account}：NT$${it.twdAmount.toLocaleString()} → ${it.goldAmount.toLocaleString()} 金`).join('\n')
    if (!window.confirm(`確認分配儲值？\n\n${lines}\n\n合計 NT$${totalTwd.toLocaleString()}，共 ${items.length} 個帳號`)) return

    setSplitLoading(true); setSplitResult(null)
    try {
      const r = await api.post('/players/master-split-recharge', items)
      setSplitResult(r.data.results)
      // 刷新主帳號資料
      const fresh = await api.get(`/players/master/${encodeURIComponent(info!.masterName)}`)
      setInfo(fresh.data)
    } catch { setSplitResult([{ account: '', ok: false, msg: '❌ 伺服器錯誤' }]) }
    finally { setSplitLoading(false) }
  }

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>👑 {S.navMaster}</h1>

      {/* 搜尋列 */}
      <div className="gm-search-bar">
        <div className="gm-search-bar__grow">
          <input
            className="gm-search-input"
            value={q}
            onChange={e => setQ(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && search()}
            placeholder="輸入主帳號名稱…"
            enterKeyHint="search"
          />
        </div>
        <div className="gm-search-bar__actions">
          <button type="button" onClick={search} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '10px 22px', borderRadius: 10, fontWeight: 700 }}>
            {loading ? S.searching : `🔍 ${S.searchBtn}`}
          </button>
        </div>
      </div>

      {err && <p style={{ color: 'var(--accent-red)', marginBottom: 16 }}>{err}</p>}

      {info && (
        <>
          {/* ── 標題列 + 模式切換 ── */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16 }}>
            <span style={{ fontWeight: 700, color: 'var(--accent-blue)', fontSize: 16 }}>👑 {info.masterName}</span>
            <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>{info.chars.length} 個角色</span>
            <span style={{ color: 'var(--accent-green)', fontSize: 13 }}>
              ({info.chars.filter(c => c.isOnline).length} 在線)
            </span>
            <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
              <button onClick={() => { setSplitMode(false); setSplitResult(null) }}
                style={{ padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 600, cursor: 'pointer',
                  background: !splitMode ? 'var(--accent-blue)' : 'var(--bg-input)',
                  color: !splitMode ? '#fff' : 'var(--text-secondary)',
                  border: `1px solid ${!splitMode ? 'var(--accent-blue)' : 'var(--border)'}` }}>
                📋 角色列表
              </button>
              <button onClick={() => { setSplitMode(true); setSplitResult(null) }}
                style={{ padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 600, cursor: 'pointer',
                  background: splitMode ? 'var(--accent-orange)' : 'var(--bg-input)',
                  color: splitMode ? '#fff' : 'var(--text-secondary)',
                  border: `1px solid ${splitMode ? 'var(--accent-orange)' : 'var(--border)'}` }}>
                💰 分配儲值
              </button>
            </div>
          </div>

          {/* ── 角色列表模式 ── */}
          {!splitMode && (
            <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
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
                              style={{ fontSize: 11, padding: '3px 7px', background: 'rgba(74,158,255,.15)', color: 'var(--accent-blue)', border: '1px solid var(--accent-blue)44', borderRadius: 4, cursor: 'pointer' }}>
                              資料
                            </button>
                            <button onClick={() => navigate(`/recharge?account=${c.account}`)}
                              style={{ fontSize: 11, padding: '3px 7px', background: 'rgba(250,170,20,.15)', color: 'var(--accent-orange)', border: '1px solid var(--accent-orange)44', borderRadius: 4, cursor: 'pointer' }}>
                              儲值
                            </button>
                            <button onClick={() => navigate(`/send?account=${c.account}&name=${encodeURIComponent(c.charName || c.account)}`)}
                              style={{ fontSize: 11, padding: '3px 7px', background: 'rgba(86,196,118,.15)', color: 'var(--accent-green)', border: '1px solid var(--accent-green)44', borderRadius: 4, cursor: 'pointer' }}>
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

          {/* ── 分配儲值模式 ── */}
          {splitMode && (
            <div>
              {/* 快速套用工具列 */}
              <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: '10px 14px', marginBottom: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap', marginRight: 2 }}>批次套用已勾選：</span>
                  {TIERS.map(t => (
                    <button key={t.twd} onClick={() => applyToAll({ twdMode: 'tier', selectedTierTwd: t.twd, selectedTierGold: t.gold, customTwd: '' })}
                      style={{ padding: '3px 8px', fontSize: 11, borderRadius: 5, cursor: 'pointer', whiteSpace: 'nowrap',
                        background: 'var(--bg-input)', border: '1px solid var(--border)', color: 'var(--text-primary)' }}>
                      {t.label}
                    </button>
                  ))}
                  <span style={{ fontSize: 11, color: 'var(--text-muted)', margin: '0 2px' }}>優惠：</span>
                  {BONUSES.map(b => (
                    <button key={b} onClick={() => applyToAll({ bonusPct: b })}
                      style={{ padding: '3px 7px', fontSize: 11, borderRadius: 5, cursor: 'pointer',
                        background: 'var(--bg-input)', border: '1px solid var(--border)', color: 'var(--text-primary)' }}>
                      +{b}%
                    </button>
                  ))}
                  <button onClick={() => setSplits(prev => prev.map(e => ({ ...e, twdMode: 'custom' as const, selectedTierTwd: 0, selectedTierGold: 0, customTwd: '', enabled: false })))}
                    style={{ padding: '3px 8px', fontSize: 11, borderRadius: 5, cursor: 'pointer', marginLeft: 4,
                      background: 'rgba(245,101,101,.15)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)55' }}>
                    全部清除
                  </button>
                </div>
              </div>

              {/* 表格主體 */}
              <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden', marginBottom: 12 }}>
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                    <thead>
                      <tr style={{ background: 'var(--bg-input)', borderBottom: '1px solid var(--border)' }}>
                        <th style={{ padding: '9px 10px', textAlign: 'center', width: 36, fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>
                          <input type="checkbox"
                            checked={splits.length > 0 && splits.every(e => e.enabled)}
                            onChange={e => setSplits(prev => prev.map(s => ({ ...s, enabled: e.target.checked })))}
                            style={{ cursor: 'pointer', accentColor: 'var(--accent-orange)' }} />
                        </th>
                        {['狀態', '帳號 / 角色', '套餐選擇', '自訂 NT$', '優惠 %', '金幣預算'].map(h => (
                          <th key={h} style={{ padding: '9px 10px', textAlign: 'left', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap' }}>{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {splits.map((entry, idx) => {
                        const char = info.chars.find(c => c.account === entry.account)!
                        const { twd, baseGold, totalGold } = calcEntry(entry)
                        const isActive = entry.enabled
                        return (
                          <tr key={entry.account} style={{
                            borderBottom: '1px solid var(--border)',
                            background: isActive ? 'rgba(250,170,20,.05)' : 'transparent',
                            opacity: isActive ? 1 : 0.6,
                          }}>
                            {/* 勾選 */}
                            <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                              <input type="checkbox" checked={isActive}
                                onChange={e => updateSplit(idx, { enabled: e.target.checked })}
                                style={{ width: 15, height: 15, cursor: 'pointer', accentColor: 'var(--accent-orange)' }} />
                            </td>
                            {/* 帳號/角色 */}
                            <td style={{ padding: '8px 10px', whiteSpace: 'nowrap' }}>
                              <span style={{ fontSize: 11, marginRight: 4 }}>{char.isOnline ? '🟢' : '⚫'}</span>
                            </td>
                            <td style={{ padding: '8px 10px' }}>
                              <div style={{ fontWeight: 700, fontSize: 13 }}>{char.charName || '—'}</div>
                              <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 1 }}>
                                {char.account}
                              </div>
                              {char.payTotal > 0 && (
                                <div style={{ fontSize: 10, color: 'var(--accent-orange)', marginTop: 1 }}>
                                  累積 NT${char.payTotal.toLocaleString()}
                                </div>
                              )}
                            </td>
                            {/* 套餐 */}
                            <td style={{ padding: '8px 10px' }}>
                              <div style={{ display: 'flex', gap: 3, flexWrap: 'wrap' }}>
                                {TIERS.map(t => {
                                  const sel = entry.selectedTierTwd === t.twd && entry.twdMode === 'tier'
                                  return (
                                    <button key={t.twd}
                                      onClick={() => updateSplit(idx, { twdMode: 'tier', selectedTierTwd: t.twd, selectedTierGold: t.gold, customTwd: '', enabled: true })}
                                      style={{
                                        padding: '3px 6px', fontSize: 11, borderRadius: 4, cursor: 'pointer', whiteSpace: 'nowrap',
                                        background: sel ? 'var(--accent-orange)' : 'var(--bg-input)',
                                        color: sel ? '#fff' : 'var(--text-primary)',
                                        border: `1px solid ${sel ? 'var(--accent-orange)' : 'var(--border)'}`,
                                        fontWeight: sel ? 700 : 400,
                                      }}>
                                      {t.label}
                                    </button>
                                  )
                                })}
                              </div>
                            </td>
                            {/* 自訂 NT$ */}
                            <td style={{ padding: '8px 10px' }}>
                              <input type="number" placeholder="自訂" value={entry.customTwd}
                                onChange={e => updateSplit(idx, { customTwd: e.target.value, twdMode: 'custom', selectedTierTwd: 0, selectedTierGold: 0, enabled: true })}
                                style={{ width: 80, fontSize: 12, padding: '4px 6px' }} min={0} />
                            </td>
                            {/* 優惠% */}
                            <td style={{ padding: '8px 10px' }}>
                              <div style={{ display: 'flex', gap: 3 }}>
                                {BONUSES.map(b => {
                                  const sel = entry.bonusPct === b
                                  return (
                                    <button key={b} onClick={() => updateSplit(idx, { bonusPct: b })}
                                      style={{
                                        padding: '3px 5px', fontSize: 11, borderRadius: 4, cursor: 'pointer',
                                        background: sel ? 'var(--accent-blue)' : 'var(--bg-input)',
                                        color: sel ? '#fff' : 'var(--text-primary)',
                                        border: `1px solid ${sel ? 'var(--accent-blue)' : 'var(--border)'}`,
                                        fontWeight: sel ? 700 : 400,
                                      }}>
                                      +{b}%
                                    </button>
                                  )
                                })}
                              </div>
                            </td>
                            {/* 金幣預算 */}
                            <td style={{ padding: '8px 12px', whiteSpace: 'nowrap', minWidth: 140 }}>
                              {twd > 0 ? (
                                <div>
                                  <span style={{ color: 'var(--accent-orange)', fontWeight: 700 }}>NT$ {twd.toLocaleString()}</span>
                                  <span style={{ color: 'var(--text-muted)', fontSize: 11, margin: '0 4px' }}>→</span>
                                  <span style={{ color: 'var(--accent-green)', fontWeight: 700 }}>{totalGold.toLocaleString()} 金</span>
                                  {entry.bonusPct > 0 && (
                                    <div style={{ fontSize: 10, color: 'var(--accent-blue)', marginTop: 2 }}>
                                      基礎 {baseGold.toLocaleString()} +{entry.bonusPct}%
                                    </div>
                                  )}
                                </div>
                              ) : (
                                <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>未設定</span>
                              )}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* 合計列 + 執行按鈕 */}
              <div style={{ background: 'rgba(250,170,20,.07)', border: '1px solid var(--accent-orange)55', borderRadius: 10, padding: '12px 18px', display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 14, fontWeight: 700, color: 'var(--accent-orange)', marginBottom: 4 }}>
                    合計：NT$ {totalTwd.toLocaleString()}，發出 {totalGold.toLocaleString()} 金，{activeCount} 個帳號
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                    ⚠ 累積儲值紀錄只計算台幣金額，優惠贈金不計入
                  </div>
                </div>
                <button onClick={doSplitRecharge} disabled={splitLoading || activeCount === 0}
                  style={{ padding: '10px 28px', fontSize: 14, fontWeight: 700, borderRadius: 8,
                    cursor: activeCount > 0 ? 'pointer' : 'not-allowed',
                    background: activeCount > 0 ? 'var(--accent-orange)' : 'var(--bg-input)',
                    color: activeCount > 0 ? '#fff' : 'var(--text-muted)',
                    border: `1px solid ${activeCount > 0 ? 'var(--accent-orange)' : 'var(--border)'}` }}>
                  {splitLoading ? '處理中…' : `💰 確認分配儲值（${activeCount} 帳號）`}
                </button>
              </div>

              {/* 執行結果 */}
              {splitResult && splitResult.length > 0 && (
                <div style={{ ...card, marginTop: 12, border: '1px solid var(--border)' }}>
                  <div style={{ fontSize: 13, fontWeight: 700, marginBottom: 10 }}>執行結果</div>
                  {splitResult.map((r, i) => (
                    <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '5px 0', borderBottom: '1px solid var(--border)', fontSize: 13 }}>
                      <span style={{ color: r.ok ? 'var(--accent-green)' : 'var(--accent-red)', fontWeight: 700 }}>
                        {r.ok ? '✓' : '✗'}
                      </span>
                      {r.account && <span style={{ fontWeight: 600, minWidth: 80 }}>{r.account}</span>}
                      <span style={{ color: r.ok ? 'var(--accent-green)' : 'var(--accent-red)' }}>{r.msg}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </>
      )}
    </div>
  )
}
