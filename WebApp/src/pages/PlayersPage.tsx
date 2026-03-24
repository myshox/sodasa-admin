import { useState, useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '../api'
import type { PlayerRow, PlayerDetail, PetInfo, SharedIpAccount, BanLogEntry, PlayerFamily } from '../api'
import { S } from '../strings'
import useIsMobile from '../hooks/useIsMobile'

const CYCLE = 25_000

const VIP_LABEL = ['', '黃金 VIP', '鑽石 VIP']
const VIP_COLOR = ['', 'var(--accent-orange)', '#4dd0e1']
const VIP_BONUS = [0, 0, 5, 10] // vipLevel → 預設加成%

const RECHARGE_TIERS = [
  { label: 'NT$100',  sub: '1萬金',    gold: 10_000,    twd: 100 },
  { label: 'NT$300',  sub: '3.2萬金',  gold: 32_000,    twd: 300 },
  { label: 'NT$500',  sub: '5.5萬金',  gold: 55_000,    twd: 500 },
  { label: 'NT$1K',   sub: '11.5萬金', gold: 115_000,   twd: 1_000 },
  { label: 'NT$3K',   sub: '36萬金',   gold: 360_000,   twd: 3_000 },
  { label: 'NT$5K',   sub: '62.5萬金', gold: 625_000,   twd: 5_000 },
  { label: 'NT$10K',  sub: '130萬金',  gold: 1_300_000, twd: 10_000 },
]
const BONUS_OPTIONS = [0, 5, 10, 15, 20]

export default function PlayersPage() {
  const isMobile = useIsMobile()
  const navigate = useNavigate()
  const [sp] = useSearchParams()
  const [q, setQ] = useState(sp.get('q') || '')
  const [allPlayers, setAllPlayers] = useState<PlayerRow[]>([])   // 完整列表（自動載入）
  const [players, setPlayers] = useState<PlayerRow[]>([])          // 顯示中（過濾後）
  const [detail, setDetail] = useState<PlayerDetail | null>(null)
  const [loading, setLoading] = useState(false)
  const [totalCount, setTotalCount] = useState(0)
  const [msg, setMsg] = useState('')

  // 頁面開啟自動載入全部玩家（最多 1000 筆，與 EXE 一致）
  useEffect(() => {
    ;(async () => {
      setLoading(true)
      try {
        const r = await api.get('/players/list', { params: { limit: 1000 } })
        setAllPlayers(r.data)
        setTotalCount(r.data.length)
        // 若有 URL 參數 q= 則立即過濾
        const initQ = sp.get('q')
        if (initQ) {
          setQ(initQ)
          const filtered = (r.data as PlayerRow[]).filter(p =>
            p.account.includes(initQ) ||
            (p.onlineName || '').includes(initQ) ||
            (p.masterName || '').includes(initQ)
          )
          setPlayers(filtered.length > 0 ? filtered : r.data)
          if (filtered.length === 1) loadDetail(filtered[0].account)
        } else {
          setPlayers(r.data)
        }
      } catch {
        setAllPlayers([]); setPlayers([])
      } finally { setLoading(false) }
    })()
  }, [])

  // 搜尋框即時過濾（不需按鈕）
  useEffect(() => {
    if (!q.trim()) {
      setPlayers(allPlayers)
      return
    }
    const kw = q.trim().toLowerCase()
    setPlayers(allPlayers.filter(p =>
      p.account.toLowerCase().includes(kw) ||
      (p.onlineName || '').toLowerCase().includes(kw) ||
      (p.masterName || '').toLowerCase().includes(kw)
    ))
  }, [q, allPlayers])

  // 金幣/水晶
  const [goldVal, setGoldVal] = useState('')
  const [crysVal, setCrysVal] = useState('')

  // 清除郵件
  const [clearingMail, setClearingMail] = useState(false)
  const doClearMail = async (unclaimedOnly: boolean) => {
    if (!detail) return
    const label = unclaimedOnly ? '未領取郵件' : '全部郵件'
    if (!window.confirm(`確定清除「${detail.onlineName}」的${label}？\n（此操作不可逆）`)) return
    setClearingMail(true)
    try {
      const r = await api.post(`/players/${detail.account}/clear-mail`, { unclaimedOnly })
      flash(r.data.message || '清除完成')
      loadDetail(detail.account)
    } catch { flash('清除失敗') }
    finally { setClearingMail(false) }
  }

  // 封號
  const [showBan, setShowBan] = useState(false)
  const [banDays, setBanDays] = useState(0)
  const [banHours, setBanHours] = useState(0)  // 0 = 使用天數
  const [banReason, setBanReason] = useState('')

  // 充值
  const [showRecharge, setShowRecharge] = useState(false)
  const [twdAmount, setTwdAmount] = useState(0)
  const [goldAmount, setGoldAmount] = useState(0)
  const [giveGold, setGiveGold] = useState(true)
  const [updatePaydata, setUpdatePaydata] = useState(true)
  const [bonusPct, setBonusPct] = useState(0)
  const [selectedTierIdx, setSelectedTierIdx] = useState(-1)

  // 改名
  const [showRename, setShowRename] = useState(false)
  const [newName, setNewName] = useState('')

  // 寵物清單（玩家詳情內）
  const [playerPets, setPlayerPets] = useState<PetInfo[] | null>(null)
  const [loadingPets, setLoadingPets] = useState(false)
  const loadPets = async (account: string) => {
    setLoadingPets(true); setPlayerPets(null)
    try {
      const r = await api.get(`/players/${encodeURIComponent(account)}/pets`)
      setPlayerPets(Array.isArray(r.data) ? r.data : [])
    } catch { setPlayerPets([]) }
    finally { setLoadingPets(false) }
  }
  const removePet = async (account: string, unicode: string, petName: string) => {
    if (!window.confirm(`確定要移除此筆 capturepet 記錄「${petName}」？\n此操作無法復原。`)) return
    try {
      await api.post(`/players/${encodeURIComponent(account)}/pets/remove`, { unicode })
      flash('已移除該筆 capturepet 記錄')
      loadPets(account)
      loadDetail(account) // 更新詳情中的寵物數
    } catch (e: any) {
      flash(e.response?.data?.message || '移除失敗')
    }
  }

  const flash = (m: string) => { setMsg(m); setTimeout(() => setMsg(''), 3000) }

  // ── 關聯帳號（同IP）──────────────────────────────────────────────
  const [sharedIp, setSharedIp] = useState<SharedIpAccount[] | null>(null)
  const [loadingSharedIp, setLoadingSharedIp] = useState(false)
  const loadSharedIp = async (account: string) => {
    setLoadingSharedIp(true); setSharedIp(null)
    try {
      const r = await api.get(`/players/${encodeURIComponent(account)}/shared-ip`)
      setSharedIp(Array.isArray(r.data) ? r.data : [])
    } catch { setSharedIp([]) }
    finally { setLoadingSharedIp(false) }
  }

  // ── 封禁記錄 ────────────────────────────────────────────────────
  const [banLog, setBanLog] = useState<BanLogEntry[] | null>(null)
  const [loadingBanLog, setLoadingBanLog] = useState(false)
  const loadBanLog = async (account: string) => {
    setLoadingBanLog(true); setBanLog(null)
    try {
      const r = await api.get(`/players/${encodeURIComponent(account)}/ban-log`)
      setBanLog(Array.isArray(r.data) ? r.data : [])
    } catch { setBanLog([]) }
    finally { setLoadingBanLog(false) }
  }

  // ── 家族資訊 ────────────────────────────────────────────────────
  const [family, setFamily] = useState<PlayerFamily | null | undefined>(undefined)
  const [loadingFamily, setLoadingFamily] = useState(false)
  const loadFamily = async (account: string) => {
    setLoadingFamily(true); setFamily(undefined)
    try {
      const r = await api.get(`/players/${encodeURIComponent(account)}/family`)
      setFamily(r.data ?? null)
    } catch { setFamily(null) }
    finally { setLoadingFamily(false) }
  }

  // 如果本地過濾結果為 0，才向伺服器補充搜尋
  const search = async () => {
    if (!q.trim()) { setPlayers(allPlayers); return }
    const kw = q.trim().toLowerCase()
    const local = allPlayers.filter(p =>
      p.account.toLowerCase().includes(kw) ||
      (p.onlineName || '').toLowerCase().includes(kw) ||
      (p.masterName || '').toLowerCase().includes(kw)
    )
    if (local.length > 0) { setPlayers(local); return }
    // 本地無結果 → 向 API 搜尋
    setLoading(true); setDetail(null)
    try {
      const r = await api.get('/players/search', { params: { q: q.trim(), limit: 200 } })
      setPlayers(r.data)
    } finally { setLoading(false) }
  }

  const reload = async () => {
    setLoading(true); setQ('')
    try {
      const r = await api.get('/players/list', { params: { limit: 1000 } })
      setAllPlayers(r.data); setPlayers(r.data); setTotalCount(r.data.length)
    } finally { setLoading(false) }
  }

  const loadDetail = async (account: string) => {
    const r = await api.get(`/players/${account}`)
    const d = r.data as PlayerDetail
    setDetail(d)
    setPlayerPets(null) // 切換玩家時收合寵物清單
    setSharedIp(null); setBanLog(null); setFamily(undefined)
    setGoldVal(String(d.gold))
    setCrysVal(String(d.crystal))
    setShowBan(false); setShowRecharge(false); setShowRename(false)
  }

  const saveGold = async () => {
    if (!detail) return
    const val = parseInt(goldVal) || 0
    await api.put(`/players/${detail.account}/gold`, { value: val })
    setDetail({ ...detail, gold: val }); flash(S.updGold)
  }
  const addGold = async (d: number) => {
    if (!detail) return
    const val = Math.max(0, detail.gold + d)
    await api.put(`/players/${detail.account}/gold`, { value: val })
    setDetail({ ...detail, gold: val }); setGoldVal(String(val)); flash(S.updGold)
  }
  const saveCrystal = async () => {
    if (!detail) return
    const val = parseInt(crysVal) || 0
    await api.put(`/players/${detail.account}/crystal`, { value: val })
    setDetail({ ...detail, crystal: val }); flash(S.updCrystal)
  }

  const doBan = async (ban: boolean) => {
    if (!detail) return
    await api.post(`/players/${detail.account}/ban`, {
      ban,
      days:   ban && banHours === 0 ? banDays : 0,
      hours:  ban ? banHours : 0,
      reason: banReason,
    })
    setDetail({ ...detail, isBanned: ban })
    setShowBan(false); flash(ban ? S.banned : S.unbanned)
  }

  const clearGold = async () => {
    if (!detail || !window.confirm(`確定將「${detail.onlineName}」的金幣清零？`)) return
    await api.put(`/players/${detail.account}/gold`, { value: 0 })
    setDetail({ ...detail, gold: 0 }); setGoldVal('0'); flash('已清零金幣')
  }

  const clearCrystal = async () => {
    if (!detail || !window.confirm(`確定將「${detail.onlineName}」的水晶清零？`)) return
    await api.put(`/players/${detail.account}/crystal`, { value: 0 })
    setDetail({ ...detail, crystal: 0 }); setCrysVal('0'); flash('已清零水晶')
  }

  const doMute = async () => {
    if (!detail) return
    await api.post(`/players/${detail.account}/mute`, { mute: !detail.isMuted })
    setDetail({ ...detail, isMuted: !detail.isMuted })
    flash(detail.isMuted ? '已解除禁言' : '已禁言')
  }

  const doForceOffline = async () => {
    if (!detail) return
    if (!window.confirm(`確認強制下線「${detail.onlineName}」（${detail.account}）？`)) return
    await api.post(`/players/${detail.account}/force-offline`)
    setDetail({ ...detail, isOnline: false }); flash('已強制下線')
  }

  const doRecharge = async () => {
    if (!detail || twdAmount <= 0) return
    const finalGold = giveGold ? goldAmount : 0
    const bonusNote = bonusPct > 0 ? `\n+${bonusPct}% 加成` : ''
    const paydataNote = updatePaydata ? '\n✓ 同步累積儲值' : '\n✗ 不同步累積儲值'
    const ok = window.confirm(
      `確認充值？\n\n玩家：${detail.onlineName}（${detail.account}）\n台幣：NT$ ${twdAmount.toLocaleString()}${bonusNote}\n${giveGold ? `金幣：+${finalGold.toLocaleString()}` : '不發放金幣'}${paydataNote}`
    )
    if (!ok) return
    await api.post(`/players/${detail.account}/recharge`, {
      twdAmount, goldAmount: finalGold, giveGold, updatePaydata, bonusPercent: bonusPct
    })
    flash(S.rechargeDone); setShowRecharge(false)
    setTwdAmount(0); setGoldAmount(0); setBonusPct(0); setSelectedTierIdx(-1)
    loadDetail(detail.account)
  }

  const doRename = async () => {
    if (!detail || !newName.trim()) return
    await api.post(`/players/${detail.account}/rename`, { newName: newName.trim() })
    setDetail({ ...detail, onlineName: newName.trim() })
    setShowRename(false); flash('改名成功')
  }

  const doResetPaydata = async () => {
    if (!detail || !confirm('確定重置累積儲值進度為 0？（不影響歷史永久累計）')) return
    await api.post(`/players/${detail.account}/paydata/reset`)
    flash('已重置進度'); loadDetail(detail.account)
  }

  const cycleProgress = detail ? Math.min(100, ((detail.paydataPoint ?? 0) / CYCLE) * 100) : 0

  return (
    <div className="gm-page-inner">
      <h1>👥 {S.pagePlayerMgr}</h1>

      <div style={{ display: 'flex', gap: 10, marginBottom: 16, alignItems: 'center', flexWrap: 'wrap' }}>
        <input value={q} onChange={e => setQ(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder="即時過濾：輸入帳號 / 角色名 / 主帳號…"
          style={{ flex: 1, maxWidth: 420 }} />
        <button onClick={search} style={{ background: 'var(--accent-blue)', color: '#fff' }}>
          {loading ? S.searching : `🔍 搜尋`}
        </button>
        <button onClick={reload} disabled={loading}
          style={{ background: 'var(--bg-input)', border: '1px solid var(--border)', color: 'var(--text-secondary)', padding: '8px 14px' }}>
          🔄 重新整理
        </button>
        {totalCount > 0 && (
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
            共 {totalCount} 筆
            {q.trim() && players.length !== totalCount && ` → 篩選 ${players.length} 筆`}
          </span>
        )}
      </div>

      <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start', flexWrap: isMobile ? 'wrap' : 'nowrap' }}>
        {/* 玩家列表 - 表格格式 */}
        <div style={{ flex: 1, minWidth: 0, overflow: 'auto', width: isMobile ? '100%' : undefined }}>
          {players.length === 0
            ? <p style={{ padding: 24, color: 'var(--text-muted)', textAlign: 'center', background: 'var(--bg-card)', borderRadius: 10 }}>
                {loading ? '載入中…' : q.trim() ? `找不到「${q}」的玩家` : '尚無玩家資料，請確認資料庫連線'}
              </p>
            : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ background: 'var(--bg-input)' }}>
                    <Th>狀態</Th>
                    <Th>角色名稱</Th>
                    <Th>帳號</Th>
                    <Th>主帳號</Th>
                    <Th>VIP</Th>
                    <Th title={S.petCountHint}>capturepet</Th>
                    <Th>儲值(NT$)</Th>
                    <Th>最後登入</Th>
                    <Th>操作</Th>
                  </tr>
                </thead>
                <tbody>
                  {players.map(p => (
                    <tr key={p.account}
                      onClick={() => loadDetail(p.account)}
                      style={{
                        cursor: 'pointer',
                        background: detail?.account === p.account ? 'rgba(74,158,255,.10)' : 'var(--bg-card)',
                        borderBottom: '1px solid var(--border)',
                      }}>
                      <Td>
                        {p.isOnline
                          ? <span style={{ color: 'var(--accent-green)', fontSize: 11 }}>🟢 在線</span>
                          : <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>⚫ 離線</span>}
                        {p.isBanned && <span style={{ color: 'var(--accent-red)', fontSize: 11, marginLeft: 4 }}>🔒</span>}
                      </Td>
                      <Td><b>{p.onlineName || '—'}</b></Td>
                      <Td style={{ color: 'var(--text-muted)' }}>{p.account}</Td>
                      <Td style={{ color: 'var(--text-muted)' }}>{p.masterName || '—'}</Td>
                      <Td>
                        {p.vipLevel > 0
                          ? <span style={{ color: VIP_COLOR[p.vipLevel], fontSize: 11 }}>{VIP_LABEL[p.vipLevel]}</span>
                          : <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>—</span>}
                      </Td>
                      <Td style={{ color: 'var(--text-secondary)' }}>🐾 {p.petCount}</Td>
                      <Td style={{ color: 'var(--accent-orange)' }}>
                        {p.payTotal > 0 ? `NT$ ${p.payTotal.toLocaleString()}` : '—'}
                      </Td>
                      <Td style={{ color: 'var(--text-muted)', fontSize: 11 }}>{p.loginTime || '—'}</Td>
                      <Td onClick={e => e.stopPropagation()}>
                        <div style={{ display: 'flex', gap: 4, flexWrap: 'nowrap' }}>
                          <ActionBtn label="資料" color="var(--accent-blue)"
                            onClick={() => loadDetail(p.account)} />
                          <ActionBtn label="發送" color="var(--accent-green)"
                            onClick={() => navigate(`/send?account=${p.account}&name=${encodeURIComponent(p.onlineName || p.account)}`)} />
                          <ActionBtn label="充值" color="var(--accent-orange)"
                            onClick={() => { loadDetail(p.account); setShowRecharge(true) }} />
                          <ActionBtn label={p.isBanned ? '解封' : '封禁'} color="var(--accent-red)"
                            onClick={() => { loadDetail(p.account); if (!p.isBanned) setShowBan(true) }} />
                        </div>
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
        </div>

        {/* 詳情面板 */}
        {detail && (
          <div style={{
            width: isMobile ? '100%' : 360, background: 'var(--bg-card)', border: '1px solid var(--border)',
            borderRadius: 10, padding: 18, flexShrink: 0, maxHeight: isMobile ? 'none' : '85vh', overflowY: 'auto'
          }}>
            {msg && <p style={{ color: 'var(--accent-green)', fontSize: 12, marginBottom: 8, textAlign: 'center' }}>{msg}</p>}

            {/* 基本資料 */}
            <div style={{ marginBottom: 12 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                <span style={{ fontWeight: 700, fontSize: 17, color: 'var(--text-primary)' }}>
                  {detail.onlineName}
                </span>
                <button onClick={() => { setShowRename(!showRename); setNewName(detail.onlineName) }}
                  title="改名" style={{ fontSize: 12, padding: '2px 6px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 4 }}>
                  ✏
                </button>
                {detail.vipLevel > 0 && (
                  <span style={{ fontSize: 11, color: VIP_COLOR[detail.vipLevel], background: `${VIP_COLOR[detail.vipLevel]}22`, padding: '2px 8px', borderRadius: 20 }}>
                    {VIP_LABEL[detail.vipLevel]}
                  </span>
                )}
              </div>
              {showRename && (
                <div style={{ display: 'flex', gap: 6, marginBottom: 8 }}>
                  <input value={newName} onChange={e => setNewName(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && doRename()}
                    placeholder="新名稱" style={{ flex: 1, fontSize: 13 }} />
                  <button onClick={doRename} style={{ background: 'var(--accent-blue)', color: '#fff', fontSize: 12, padding: '4px 10px' }}>確定</button>
                  <button onClick={() => setShowRename(false)} style={{ fontSize: 12, padding: '4px 8px' }}>取消</button>
                </div>
              )}
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 6 }}>{detail.account}</div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                {detail.isOnline && <Tag text="🟢 在線" color="var(--accent-green)" />}
                {detail.isBanned && <Tag text={`🔒 封禁${detail.banEndTime ? ` (${detail.banEndTime})` : ''}`} color="var(--accent-red)" />}
                {detail.isMuted && <Tag text="🔇 禁言" color="var(--accent-orange)" />}
              </div>
            </div>

            {/* 快速按鈕 */}
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 12 }}>
              <QuickBtn label="✉ 發送道具" onClick={() => navigate(`/send?account=${detail.account}&name=${encodeURIComponent(detail.onlineName || detail.account)}`)} color="var(--accent-green)" />
              <QuickBtn label="💳 充值" onClick={() => setShowRecharge(!showRecharge)} color="var(--accent-orange)" />
              {detail.isOnline && <QuickBtn label="⚡ 強制下線" onClick={doForceOffline} color="var(--accent-red)" />}
              <QuickBtn label={detail.isMuted ? '🔊 解除禁言' : '🔇 禁言'} onClick={doMute} color="var(--accent-orange)" />
            </div>

            {/* 充值面板 */}
            {showRecharge && (
              <div style={{ marginBottom: 12, padding: 12, background: 'var(--bg-input)', border: '1px solid var(--accent-orange)', borderRadius: 8 }}>
                <div style={{ fontWeight: 600, fontSize: 12, color: 'var(--accent-orange)', marginBottom: 8 }}>💳 {S.rechargeTitle}</div>

                {/* VIP 加成提示 */}
                {detail && detail.vipLevel > 0 && (
                  <div style={{ fontSize: 11, color: VIP_COLOR[detail.vipLevel], background: `${VIP_COLOR[detail.vipLevel]}18`, border: `1px solid ${VIP_COLOR[detail.vipLevel]}44`, borderRadius: 4, padding: '3px 8px', marginBottom: 8 }}>
                    {VIP_LABEL[detail.vipLevel]} 自動套用 +{VIP_BONUS[detail.vipLevel]}% 加成
                  </div>
                )}

                {/* STEP 1：套餐 */}
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 4 }}>STEP 1 — 選擇套餐</div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: 8 }}>
                  {RECHARGE_TIERS.map((t, i) => (
                    <button key={t.twd} onClick={() => {
                      setSelectedTierIdx(i); setTwdAmount(t.twd)
                      const vipB = detail ? VIP_BONUS[detail.vipLevel] ?? 0 : 0
                      const bp = Math.max(bonusPct, vipB)
                      setBonusPct(bp)
                      setGoldAmount(Math.floor(t.gold * (1 + bp / 100)))
                    }}
                      style={{ padding: '5px 8px', fontSize: 11, lineHeight: 1.3, textAlign: 'center',
                        background: selectedTierIdx === i ? 'var(--accent-orange)' : 'var(--bg-card)',
                        color: selectedTierIdx === i ? '#fff' : 'var(--text-secondary)',
                        border: `1px solid ${selectedTierIdx === i ? 'var(--accent-orange)' : 'var(--border)'}`, borderRadius: 4 }}>
                      <div>{t.label}</div>
                      <div style={{ fontSize: 10, opacity: 0.8 }}>{t.sub}</div>
                    </button>
                  ))}
                </div>

                {/* STEP 2：加成 % */}
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 4 }}>STEP 2 — 回饋加成</div>
                <div style={{ display: 'flex', gap: 4, marginBottom: 8, flexWrap: 'wrap' }}>
                  {BONUS_OPTIONS.map(b => (
                    <button key={b} onClick={() => {
                      setBonusPct(b)
                      if (selectedTierIdx >= 0)
                        setGoldAmount(Math.floor(RECHARGE_TIERS[selectedTierIdx].gold * (1 + b / 100)))
                    }}
                      style={{ padding: '4px 10px', fontSize: 11,
                        background: bonusPct === b ? (b === 0 ? 'var(--bg-card)' : 'var(--accent-green)') : 'var(--bg-card)',
                        color: bonusPct === b ? (b === 0 ? 'var(--text-secondary)' : '#fff') : 'var(--text-muted)',
                        border: `1px solid ${bonusPct === b ? (b === 0 ? 'var(--border)' : 'var(--accent-green)') : 'var(--border)'}`, borderRadius: 4 }}>
                      {b === 0 ? '無加成' : `+${b}%`}
                    </button>
                  ))}
                </div>

                {/* 手動輸入 */}
                <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
                  <label style={{ flex: 1, fontSize: 12 }}>NT$ 台幣
                    <input type="number" min={0} value={twdAmount || ''} onChange={e => setTwdAmount(+e.target.value || 0)} style={{ width: '100%', marginTop: 2 }} />
                  </label>
                  <label style={{ flex: 1, fontSize: 12 }}>金幣數量
                    <input type="number" min={0} value={goldAmount || ''} onChange={e => setGoldAmount(+e.target.value || 0)} style={{ width: '100%', marginTop: 2 }} />
                  </label>
                </div>

                {/* 預覽 */}
                {twdAmount > 0 && goldAmount > 0 && (
                  <div style={{ fontSize: 11, color: 'var(--accent-blue)', background: 'rgba(74,158,255,.08)', border: '1px solid var(--accent-blue)33', borderRadius: 4, padding: '4px 8px', marginBottom: 8 }}>
                    📋 確認：NT$ {twdAmount.toLocaleString()}
                    {bonusPct > 0 && ` (+${bonusPct}% 加成)`}
                    {giveGold && ` → +${goldAmount.toLocaleString()} 金幣`}
                    {!giveGold && ` → 不發金幣`}
                    {updatePaydata ? '，同步累積儲值' : '，不同步累積儲值'}
                  </div>
                )}

                {/* 選項 */}
                <div style={{ display: 'flex', gap: 12, marginBottom: 10, fontSize: 12 }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 5, cursor: 'pointer' }}>
                    <input type="checkbox" checked={giveGold} onChange={e => setGiveGold(e.target.checked)} />
                    發放金幣
                  </label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 5, cursor: 'pointer' }}>
                    <input type="checkbox" checked={updatePaydata} onChange={e => setUpdatePaydata(e.target.checked)} />
                    同步累積儲值
                  </label>
                </div>

                <div style={{ display: 'flex', gap: 8 }}>
                  <button onClick={doRecharge} disabled={twdAmount <= 0}
                    style={{ flex: 1, background: 'var(--accent-orange)', color: '#fff', padding: '7px 0', borderRadius: 6, opacity: twdAmount <= 0 ? 0.5 : 1 }}>
                    ✓ {S.rechargeConfirm}
                  </button>
                  <button onClick={() => { setShowRecharge(false); setTwdAmount(0); setGoldAmount(0); setSelectedTierIdx(-1); setBonusPct(0) }}
                    style={{ padding: '7px 14px', border: '1px solid var(--border)', borderRadius: 6 }}>{S.cancel}</button>
                </div>
              </div>
            )}

            {/* 遊戲幣 */}
            <SectionLabel label={`💰 ${S.gold}`} />
            <div style={{ display: 'flex', gap: 6, marginBottom: 4 }}>
              <input value={goldVal} onChange={e => setGoldVal(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && saveGold()} style={{ flex: 1 }} />
              <button onClick={saveGold} style={{ background: 'var(--accent-blue)', color: '#fff', fontSize: 12, padding: '5px 10px' }}>{S.setGold}</button>
              <button onClick={clearGold} title="清零金幣" style={{ background: 'rgba(245,101,101,.15)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)55', fontSize: 12, padding: '5px 8px' }}>清零</button>
            </div>
            <div style={{ display: 'flex', gap: 4, marginBottom: 10, flexWrap: 'wrap' }}>
              <span style={{ fontSize: 11, color: 'var(--text-muted)', alignSelf: 'center', marginRight: 2 }}>加：</span>
              {[10000, 100000, 1000000].map(d => (
                <button key={`g+${d}`} onClick={() => addGold(d)} style={{ background: 'var(--bg-input)', color: 'var(--accent-green)', fontSize: 11, padding: '2px 7px' }}>
                  +{d >= 1000000 ? `${d/10000}萬` : `${d/10000}萬`}
                </button>
              ))}
              <span style={{ fontSize: 11, color: 'var(--text-muted)', alignSelf: 'center', marginLeft: 4, marginRight: 2 }}>扣：</span>
              {[10000, 100000, 1000000].map(d => (
                <button key={`g-${d}`} onClick={() => addGold(-d)} style={{ background: 'var(--bg-input)', color: 'var(--accent-red)', fontSize: 11, padding: '2px 7px' }}>
                  -{d >= 1000000 ? `${d/10000}萬` : `${d/10000}萬`}
                </button>
              ))}
            </div>

            <SectionLabel label={`💎 ${S.crystal}`} />
            <div style={{ display: 'flex', gap: 6, marginBottom: 10 }}>
              <input value={crysVal} onChange={e => setCrysVal(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && saveCrystal()} style={{ flex: 1 }} />
              <button onClick={saveCrystal} style={{ background: 'var(--accent-blue)', color: '#fff', fontSize: 12, padding: '5px 10px' }}>{S.setCrystal}</button>
              <button onClick={clearCrystal} title="清零水晶" style={{ background: 'rgba(245,101,101,.15)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)55', fontSize: 12, padding: '5px 8px' }}>清零</button>
            </div>

            {/* 充值點 & R幣 */}
            <div style={{ display: 'flex', gap: 10, marginBottom: 10 }}>
              <div style={{ flex: 1, background: 'var(--bg-input)', borderRadius: 6, padding: '6px 10px', fontSize: 12 }}>
                <div style={{ color: 'var(--text-muted)' }}>充值點</div>
                <div style={{ fontWeight: 600 }}>{(detail.payPoint ?? 0).toLocaleString()}</div>
              </div>
              <div style={{ flex: 1, background: 'var(--bg-input)', borderRadius: 6, padding: '6px 10px', fontSize: 12 }}>
                <div style={{ color: 'var(--text-muted)' }}>R幣</div>
                <div style={{ fontWeight: 600 }}>{(detail.rmbPoint ?? 0).toLocaleString()}</div>
              </div>
            </div>

            {/* 累積儲值 & 循環進度 */}
            <SectionLabel label="💳 累積儲值進度" />
            <div style={{ marginBottom: 12, background: 'var(--bg-input)', borderRadius: 8, padding: 10 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, marginBottom: 6 }}>
                <span style={{ color: 'var(--text-muted)' }}>本輪進度（每 NT$25,000 一輪）</span>
                <span style={{ color: 'var(--accent-orange)', fontWeight: 600 }}>
                  NT$ {(detail.paydataPoint ?? 0).toLocaleString()} / {CYCLE.toLocaleString()}
                </span>
              </div>
              <div style={{ height: 8, background: 'var(--border)', borderRadius: 4, overflow: 'hidden', marginBottom: 6 }}>
                <div style={{ height: '100%', width: `${cycleProgress}%`, background: 'var(--accent-orange)', borderRadius: 4, transition: 'width .3s' }} />
              </div>
              <Row label="累計儲值(NT$)" value={`NT$ ${(detail.payTotal ?? 0).toLocaleString()}`} />
              <Row label="已完成輪數" value={`${detail.totalCheck ?? 0} 輪`} />
              <Row label="歷史永久累計" value={`NT$ ${(detail.paydataTotal ?? 0).toLocaleString()}`} />
              <button onClick={doResetPaydata} title="將本輪累積進度歸零，不影響歷史紀錄" style={{ marginTop: 6, width: '100%', fontSize: 11, padding: '4px 0', background: 'rgba(245,101,101,.12)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)', borderRadius: 4 }}>
                🔄 清零本輪進度
              </button>
            </div>

            {/* 玩家資訊 */}
            <SectionLabel label="📋 玩家資訊" />
            <div style={{ marginBottom: 12 }}>
              <Row label="主帳號" value={detail.masterName || '—'} />
              <Row label="伺服器" value={detail.serverId > 0 ? `ch${detail.serverId}` : '—'} />
              <Row label="群組 ID" value={String(detail.groupId ?? 0)} />
              <Row label="NeiCe" value={String(detail.neiCe ?? 0)} />
              <Row label={S.petCount} value={`${detail.petCount} 筆`} title={S.petCountHint} />
              {detail.petCount > 0 && (
                <div style={{ marginBottom: 8 }}>
                  <button
                    onClick={() => playerPets === null ? loadPets(detail.account) : setPlayerPets(null)}
                    disabled={loadingPets}
                    style={{ fontSize: 12, padding: '4px 10px', background: 'var(--accent-green)', color: '#fff', border: 'none', borderRadius: 6 }}
                    title={S.petCountHint}>
                    {loadingPets ? '載入中…' : playerPets === null ? '📋 查看 capturepet 清單' : '📋 收合清單'}
                  </button>
                  {playerPets !== null && playerPets.length > 0 && (
                    <div style={{ marginTop: 8, padding: 8, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 8, maxHeight: 220, overflowY: 'auto' }}>
                      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 6 }}>共 {playerPets.length} 筆 · ※ 來自 capturepet 表（如練寵活動），非角色身上寵物 · 點擊「移除」自資料庫刪除（不可復原）</div>
                      {playerPets.map(p => (
                        <div key={p.unicode} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '4px 0', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                          <span><b>{p.name}</b> Lv.{p.lv} 戰力 {Math.round(p.sum)} {p.check === 1 ? '· 出戰中' : ''}</span>
                          <button onClick={() => removePet(detail.account, p.unicode, p.name)} style={{ fontSize: 11, padding: '2px 8px', background: 'rgba(245,101,101,.2)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)', borderRadius: 4 }}>移除</button>
                        </div>
                      ))}
                    </div>
                  )}
                  {playerPets !== null && playerPets.length === 0 && !loadingPets && (
                    <div style={{ marginTop: 6, fontSize: 12, color: 'var(--text-muted)' }}>未查到 capturepet 記錄（可能 cdkey/author 與資料庫不符）</div>
                  )}
                </div>
              )}
              <Row label="郵件" value={`${detail.unreadMails} 未讀 / ${detail.totalMails} 封`} />
              <div style={{ display: 'flex', gap: 6, padding: '4px 0', flexWrap: 'wrap' }}>
                <button onClick={() => doClearMail(true)} disabled={clearingMail}
                  style={{ fontSize: 11, padding: '3px 9px', background: 'rgba(245,159,10,.12)', border: '1px solid var(--accent-orange)', borderRadius: 4, color: 'var(--accent-orange)', cursor: 'pointer', opacity: clearingMail ? 0.5 : 1 }}>
                  🗑 清除未領取郵件
                </button>
                <button onClick={() => doClearMail(false)} disabled={clearingMail}
                  style={{ fontSize: 11, padding: '3px 9px', background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 4, color: 'var(--accent-red)', cursor: 'pointer', opacity: clearingMail ? 0.5 : 1 }}>
                  🗑 清除全部郵件
                </button>
              </div>
              <Row label={S.regTime} value={detail.regTime} />
              <Row label={S.loginTime} value={detail.loginTime} />
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '3px 0', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                <span style={{ color: 'var(--text-muted)' }}>{S.regIP}</span>
                <span
                  style={{ color: detail.regIP ? 'var(--accent-blue)' : 'var(--text-primary)', cursor: detail.regIP ? 'pointer' : 'default', textDecoration: detail.regIP ? 'underline dotted' : 'none' }}
                  title={detail.regIP ? '點擊查詢同IP帳號' : undefined}
                  onClick={() => detail.regIP && loadSharedIp(detail.account)}>
                  {detail.regIP || S.em}
                </span>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', padding: '3px 0', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                <span style={{ color: 'var(--text-muted)' }}>{S.lastIP}</span>
                <span
                  style={{ color: detail.ip ? 'var(--accent-blue)' : 'var(--text-primary)', cursor: detail.ip ? 'pointer' : 'default', textDecoration: detail.ip ? 'underline dotted' : 'none' }}
                  title={detail.ip ? '點擊查詢同IP帳號' : undefined}
                  onClick={() => detail.ip && loadSharedIp(detail.account)}>
                  {detail.ip || S.em}
                </span>
              </div>
              <Row label="UID" value={detail.uid || '—'} />
            </div>

            {/* 封號 */}
            <SectionLabel label="⚠️ 帳號管理" />
            {!showBan
              ? (
                <div style={{ display: 'flex', gap: 8 }}>
                  {detail.isBanned
                    ? <button onClick={() => doBan(false)} style={{ flex: 1, padding: '7px 0', background: 'rgba(86,196,118,.2)', color: 'var(--accent-green)', border: '1px solid var(--accent-green)' }}>{S.unbanBtn}</button>
                    : <button onClick={() => setShowBan(true)} style={{ flex: 1, padding: '7px 0', background: 'rgba(245,101,101,.2)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)' }}>🚫 {S.banBtn}</button>
                  }
                </div>
              )
              : (
                <div style={{ background: 'rgba(245,101,101,.08)', border: '1px solid var(--accent-red)', borderRadius: 8, padding: 12 }}>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 4 }}>小時</div>
                  <div style={{ display: 'flex', gap: 6, marginBottom: 6, flexWrap: 'wrap' }}>
                    {[1, 6, 12, 24].map(h => (
                      <button key={`h${h}`} onClick={() => { setBanHours(h); setBanDays(-1) }}
                        style={{ fontSize: 12, padding: '3px 8px', background: banHours === h ? 'var(--accent-red)' : 'var(--bg-input)', color: banHours === h ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 4 }}>
                        {h}時
                      </button>
                    ))}
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 4 }}>天數 / 永久</div>
                  <div style={{ display: 'flex', gap: 6, marginBottom: 6, flexWrap: 'wrap' }}>
                    {[1, 3, 7, 14, 30].map(d => (
                      <button key={d} onClick={() => { setBanDays(d); setBanHours(0) }}
                        style={{ fontSize: 12, padding: '3px 8px', background: (banDays === d && banHours === 0) ? 'var(--accent-red)' : 'var(--bg-input)', color: (banDays === d && banHours === 0) ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 4 }}>
                        {d}天
                      </button>
                    ))}
                    <button onClick={() => { setBanDays(0); setBanHours(0) }}
                      style={{ fontSize: 12, padding: '3px 8px', background: (banDays === 0 && banHours === 0) ? 'var(--accent-red)' : 'var(--bg-input)', color: (banDays === 0 && banHours === 0) ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 4 }}>
                      永久
                    </button>
                  </div>
                  <input value={banReason} onChange={e => setBanReason(e.target.value)}
                    placeholder="封禁原因（選填）" style={{ width: '100%', marginBottom: 8, fontSize: 13 }} />
                  <div style={{ display: 'flex', gap: 8 }}>
                    <button onClick={() => doBan(true)} style={{ flex: 1, background: 'var(--accent-red)', color: '#fff' }}>🚫 {S.confirm}</button>
                    <button onClick={() => setShowBan(false)} style={{ flex: 1, background: 'var(--bg-input)', border: '1px solid var(--border)' }}>{S.cancel}</button>
                  </div>
                </div>
              )}

            {/* ── 關聯帳號（同IP）區塊 ── */}
            <SectionLabel label="🔗 關聯帳號（同IP小號偵測）" />
            <div style={{ marginBottom: 12 }}>
              {sharedIp === null ? (
                <button onClick={() => loadSharedIp(detail.account)} disabled={loadingSharedIp}
                  style={{ width: '100%', fontSize: 12, padding: '5px 0', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--accent-blue)' }}>
                  {loadingSharedIp ? '查詢中…' : '🔍 查詢同IP帳號'}
                </button>
              ) : sharedIp.length === 0 ? (
                <div style={{ fontSize: 12, color: 'var(--text-muted)', padding: '6px 0' }}>✓ 未查到共用IP的其他帳號</div>
              ) : (
                <div>
                  <div style={{ fontSize: 11, color: 'var(--accent-orange)', marginBottom: 6 }}>⚠️ 發現 {sharedIp.length} 個共用IP帳號</div>
                  <div style={{ maxHeight: 200, overflowY: 'auto', background: 'var(--bg-input)', borderRadius: 6, border: '1px solid var(--border)' }}>
                    {sharedIp.map(a => (
                      <div key={a.account} onClick={() => loadDetail(a.account)}
                        style={{ padding: '5px 8px', borderBottom: '1px solid var(--border)', fontSize: 12, cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <div>
                          <span style={{ fontWeight: 600, color: a.isOnline ? 'var(--accent-green)' : 'var(--text-primary)' }}>
                            {a.isOnline ? '🟢 ' : ''}{a.charName || a.account}
                          </span>
                          <span style={{ color: 'var(--text-muted)', marginLeft: 6 }}>{a.account}</span>
                        </div>
                        <div style={{ textAlign: 'right', fontSize: 11, color: 'var(--text-muted)' }}>
                          {a.payTotal > 0 && <span style={{ color: 'var(--accent-orange)' }}>NT${a.payTotal.toLocaleString()} </span>}
                          {a.loginTime}
                        </div>
                      </div>
                    ))}
                  </div>
                  <button onClick={() => setSharedIp(null)} style={{ marginTop: 4, fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, color: 'var(--text-muted)' }}>收合</button>
                </div>
              )}
            </div>

            {/* ── 封禁記錄區塊 ── */}
            <SectionLabel label="📋 封禁歷史記錄" />
            <div style={{ marginBottom: 12 }}>
              {banLog === null ? (
                <button onClick={() => loadBanLog(detail.account)} disabled={loadingBanLog}
                  style={{ width: '100%', fontSize: 12, padding: '5px 0', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--accent-red)' }}>
                  {loadingBanLog ? '查詢中…' : '📋 查詢封禁記錄'}
                </button>
              ) : banLog.length === 0 ? (
                <div style={{ fontSize: 12, color: 'var(--text-muted)', padding: '6px 0' }}>✓ 無封禁記錄</div>
              ) : (
                <div>
                  <div style={{ fontSize: 11, color: 'var(--accent-red)', marginBottom: 6 }}>共 {banLog.length} 筆封禁記錄</div>
                  <div style={{ maxHeight: 160, overflowY: 'auto', background: 'var(--bg-input)', borderRadius: 6, border: '1px solid var(--border)' }}>
                    {banLog.map((b, i) => (
                      <div key={i} style={{ padding: '5px 8px', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                        <span style={{ color: b.isPermanent ? 'var(--accent-red)' : 'var(--text-secondary)', fontWeight: 600 }}>
                          {b.isPermanent ? '🔒 永久' : `⏱ 至 ${b.banEndTime}`}
                        </span>
                        {b.reason && <span style={{ color: 'var(--text-muted)', marginLeft: 8 }}>— {b.reason}</span>}
                      </div>
                    ))}
                  </div>
                  <button onClick={() => setBanLog(null)} style={{ marginTop: 4, fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, color: 'var(--text-muted)' }}>收合</button>
                </div>
              )}
            </div>

            {/* ── 家族資訊區塊 ── */}
            <SectionLabel label="🏰 家族資訊" />
            <div style={{ marginBottom: 12 }}>
              {family === undefined ? (
                <button onClick={() => loadFamily(detail.account)} disabled={loadingFamily}
                  style={{ width: '100%', fontSize: 12, padding: '5px 0', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--accent-blue)' }}>
                  {loadingFamily ? '查詢中…' : '🏰 查詢家族'}
                </button>
              ) : family === null ? (
                <div style={{ fontSize: 12, color: 'var(--text-muted)', padding: '6px 0' }}>— 無家族（散人）</div>
              ) : (
                <div style={{ background: 'var(--bg-input)', borderRadius: 6, padding: '8px 10px', fontSize: 12 }}>
                  <div style={{ fontWeight: 600, color: 'var(--accent-blue)', marginBottom: 4 }}>🏰 {family.guildName}</div>
                  <Row label="家族 ID" value={String(family.guildId)} />
                  <Row label="家族成員數" value={`${family.memberCount} 人`} />
                  <button onClick={() => setFamily(undefined)} style={{ marginTop: 4, fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, color: 'var(--text-muted)' }}>收合</button>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

const Tag = ({ text, color }: { text: string; color: string }) => (
  <span style={{ fontSize: 11, color, background: `${color}22`, padding: '2px 8px', borderRadius: 20 }}>{text}</span>
)
const SectionLabel = ({ label }: { label: string }) => (
  <div style={{ color: 'var(--accent-blue)', fontSize: 12, fontWeight: 600, marginBottom: 4, marginTop: 2, borderBottom: '1px solid var(--border)', paddingBottom: 3 }}>{label}</div>
)
const Row = ({ label, value, title }: { label: string; value: string; title?: string }) => (
  <div style={{ display: 'flex', justifyContent: 'space-between', padding: '3px 0', borderBottom: '1px solid var(--border)', fontSize: 12 }} title={title}>
    <span style={{ color: 'var(--text-muted)' }}>{label}</span>
    <span style={{ color: 'var(--text-primary)' }}>{value || S.em}</span>
  </div>
)
const Th = ({ children, title }: { children: React.ReactNode; title?: string }) => (
  <th style={{ padding: '8px 10px', textAlign: 'left', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' }} title={title}>
    {children}
  </th>
)
const Td = ({ children, style, onClick }: { children: React.ReactNode; style?: React.CSSProperties; onClick?: (e: React.MouseEvent) => void }) => (
  <td style={{ padding: '8px 10px', ...style }} onClick={onClick}>{children}</td>
)
const ActionBtn = ({ label, color, onClick }: { label: string; color: string; onClick: () => void }) => (
  <button onClick={onClick} style={{ fontSize: 11, padding: '3px 7px', background: `${color}22`, color, border: `1px solid ${color}44`, borderRadius: 4, whiteSpace: 'nowrap' }}>
    {label}
  </button>
)
const QuickBtn = ({ label, onClick, color }: { label: string; onClick: () => void; color: string }) => (
  <button onClick={onClick} style={{ fontSize: 12, padding: '5px 10px', background: `${color}18`, color, border: `1px solid ${color}44`, borderRadius: 6 }}>
    {label}
  </button>
)