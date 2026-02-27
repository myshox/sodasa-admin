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
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>👑 {S.navMaster}</h1>

      {/* 搜尋列 */}
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
              <div style={{ ...card, display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap', padding: '12px 16px' }}>
                <span style={{ fontSize: 12, color: 'var(--text-muted)', marginRight: 4 }}>快速套用（已勾選）：</span>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>套餐：</span>
                {TIERS.map(t => (
                  <button key={t.twd} onClick={() => applyToAll({ twdMode: 'tier', selectedTierTwd: t.twd, selectedTierGold: t.gold, customTwd: '' })}
                    style={{ padding: '4px 8px', fontSize: 11, borderRadius: 5, cursor: 'pointer',
                      background: 'var(--bg-input)', border: '1px solid var(--border)', whiteSpace: 'nowrap' }}>
                    {t.label}
                  </button>
                ))}
                <span style={{ fontSize: 12, color: 'var(--text-muted)', marginLeft: 8 }}>優惠：</span>
                {BONUSES.map(b => (
                  <button key={b} onClick={() => applyToAll({ bonusPct: b })}
                    style={{ padding: '4px 8px', fontSize: 11, borderRadius: 5, cursor: 'pointer',
                      background: 'var(--bg-input)', border: '1px solid var(--border)' }}>
                    +{b}%
                  </button>
                ))}
                <button onClick={() => setSplits(prev => prev.map(e => e.enabled ? { ...e, twdMode: 'custom', selectedTierTwd: 0, selectedTierGold: 0, customTwd: '' } : e))}
                  style={{ padding: '4px 8px', fontSize: 11, borderRadius: 5, cursor: 'pointer', marginLeft: 8,
                    background: 'rgba(245,101,101,.15)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)55' }}>
                  清除已勾選
                </button>
              </div>

              {/* 各 CDKEY 輸入列 */}
              {splits.map((entry, idx) => {
                const char = info.chars.find(c => c.account === entry.account)!
                const { twd, baseGold, totalGold } = calcEntry(entry)
                return (
                  <div key={entry.account} style={{
                    ...card, padding: 14,
                    border: `1px solid ${entry.enabled ? 'var(--accent-orange)' : 'var(--border)'}`,
                    opacity: entry.enabled ? 1 : 0.55,
                  }}>
                    <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
                      {/* 勾選 + 帳號資訊 */}
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 200 }}>
                        <input type="checkbox" checked={entry.enabled}
                          onChange={e => updateSplit(idx, { enabled: e.target.checked })}
                          style={{ width: 16, height: 16, cursor: 'pointer', accentColor: 'var(--accent-orange)' }} />
                        <div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                            <span style={{ fontSize: 11 }}>{char.isOnline ? '🟢' : '⚫'}</span>
                            <span style={{ fontWeight: 700, fontSize: 13 }}>{char.charName || char.account}</span>
                          </div>
                          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
                            {char.account} · 累積 NT${char.payTotal.toLocaleString()}
                          </div>
                        </div>
                      </div>

                      {/* 套餐選擇 */}
                      <div style={{ flex: 1, minWidth: 280 }}>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 5 }}>選擇套餐</div>
                        <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginBottom: 8 }}>
                          {TIERS.map(t => (
                            <button key={t.twd}
                              onClick={() => updateSplit(idx, { twdMode: 'tier', selectedTierTwd: t.twd, selectedTierGold: t.gold, customTwd: '', enabled: true })}
                              style={{
                                padding: '4px 7px', fontSize: 11, borderRadius: 5, cursor: 'pointer',
                                background: entry.selectedTierTwd === t.twd && entry.twdMode === 'tier'
                                  ? 'var(--accent-orange)' : 'var(--bg-input)',
                                color: entry.selectedTierTwd === t.twd && entry.twdMode === 'tier'
                                  ? '#fff' : 'var(--text-primary)',
                                border: `1px solid ${entry.selectedTierTwd === t.twd && entry.twdMode === 'tier'
                                  ? 'var(--accent-orange)' : 'var(--border)'}`,
                              }}>
                              {t.label}
                            </button>
                          ))}
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                          <span style={{ fontSize: 11, color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>或自訂 NT$</span>
                          <input type="number" placeholder="例：200" value={entry.customTwd}
                            onChange={e => updateSplit(idx, { customTwd: e.target.value, twdMode: 'custom', selectedTierTwd: 0, selectedTierGold: 0, enabled: true })}
                            style={{ width: 90, fontSize: 12 }} min={0} />
                        </div>
                      </div>

                      {/* 優惠% */}
                      <div style={{ minWidth: 180 }}>
                        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 5 }}>優惠加成</div>
                        <div style={{ display: 'flex', gap: 4 }}>
                          {BONUSES.map(b => (
                            <button key={b}
                              onClick={() => updateSplit(idx, { bonusPct: b })}
                              style={{
                                padding: '4px 7px', fontSize: 11, borderRadius: 5, cursor: 'pointer',
                                background: entry.bonusPct === b ? 'var(--accent-blue)' : 'var(--bg-input)',
                                color: entry.bonusPct === b ? '#fff' : 'var(--text-primary)',
                                border: `1px solid ${entry.bonusPct === b ? 'var(--accent-blue)' : 'var(--border)'}`,
                              }}>
                              +{b}%
                            </button>
                          ))}
                        </div>
                      </div>

                      {/* 預覽 */}
                      <div style={{ minWidth: 160, background: 'var(--bg-input)', borderRadius: 7, padding: '8px 12px', fontSize: 12 }}>
                        {twd > 0 ? (
                          <>
                            <div style={{ color: 'var(--accent-orange)', fontWeight: 700 }}>NT$ {twd.toLocaleString()}</div>
                            <div style={{ color: 'var(--text-muted)', marginTop: 3 }}>
                              基礎金幣：{baseGold.toLocaleString()}
                              {entry.bonusPct > 0 && <span style={{ color: 'var(--accent-blue)' }}> +{entry.bonusPct}%</span>}
                            </div>
                            <div style={{ color: 'var(--accent-green)', fontWeight: 700, marginTop: 2 }}>
                              → {totalGold.toLocaleString()} 金
                            </div>
                            <div style={{ color: 'var(--text-muted)', fontSize: 10, marginTop: 3 }}>
                              累積+NT${twd.toLocaleString()}（贈金不計）
                            </div>
                          </>
                        ) : (
                          <span style={{ color: 'var(--text-muted)' }}>—</span>
                        )}
                      </div>
                    </div>
                  </div>
                )
              })}

              {/* 合計列 + 執行按鈕 */}
              <div style={{ ...card, background: 'rgba(250,170,20,.07)', border: '1px solid var(--accent-orange)55', display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-orange)', marginBottom: 4 }}>
                    合計 NT$ {totalTwd.toLocaleString()}，發出 {totalGold.toLocaleString()} 金（{activeCount} 個帳號）
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>
                    ⚠ 累積儲值進度只計算台幣金額，優惠贈金不納入
                  </div>
                </div>
                <button onClick={doSplitRecharge} disabled={splitLoading || activeCount === 0}
                  style={{ padding: '10px 28px', fontSize: 14, fontWeight: 700, borderRadius: 8, cursor: activeCount > 0 ? 'pointer' : 'not-allowed',
                    background: activeCount > 0 ? 'var(--accent-orange)' : 'var(--bg-input)',
                    color: activeCount > 0 ? '#fff' : 'var(--text-muted)',
                    border: `1px solid ${activeCount > 0 ? 'var(--accent-orange)' : 'var(--border)'}` }}>
                  {splitLoading ? '處理中…' : `💰 確認分配儲值（${activeCount} 帳號）`}
                </button>
              </div>

              {/* 執行結果 */}
              {splitResult && splitResult.length > 0 && (
                <div style={{ ...card, border: '1px solid var(--border)' }}>
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
