import { useState, useEffect } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import api from '../api'
import type { PlayerRow, PlayerDetail } from '../api'
import { S } from '../strings'

const CYCLE = 25_000

const VIP_LABEL = ['', '黃金 VIP', '鑽石 VIP']
const VIP_COLOR = ['', 'var(--accent-orange)', '#4dd0e1']

const RECHARGE_TIERS = [
  { label: 'NT$100 / 1萬', gold: 10_000, twd: 100 },
  { label: 'NT$300 / 3.2萬', gold: 32_000, twd: 300 },
  { label: 'NT$500 / 5.5萬', gold: 55_000, twd: 500 },
  { label: 'NT$1K / 11.5萬', gold: 115_000, twd: 1_000 },
  { label: 'NT$3K / 36萬', gold: 360_000, twd: 3_000 },
  { label: 'NT$5K / 62.5萬', gold: 625_000, twd: 5_000 },
  { label: 'NT$10K / 130萬', gold: 1_300_000, twd: 10_000 },
]

export default function PlayersPage() {
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

  // 封號
  const [showBan, setShowBan] = useState(false)
  const [banDays, setBanDays] = useState(0)
  const [banReason, setBanReason] = useState('')

  // 充值
  const [showRecharge, setShowRecharge] = useState(false)
  const [twdAmount, setTwdAmount] = useState(0)
  const [goldAmount, setGoldAmount] = useState(0)
  const [giveGold, setGiveGold] = useState(true)

  // 改名
  const [showRename, setShowRename] = useState(false)
  const [newName, setNewName] = useState('')

  const flash = (m: string) => { setMsg(m); setTimeout(() => setMsg(''), 3000) }

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
    await api.post(`/players/${detail.account}/ban`, { ban, days: ban ? banDays : 0, reason: banReason })
    setDetail({ ...detail, isBanned: ban })
    setShowBan(false); flash(ban ? S.banned : S.unbanned)
  }

  const doMute = async () => {
    if (!detail) return
    await api.post(`/players/${detail.account}/mute`, { mute: !detail.isMuted })
    setDetail({ ...detail, isMuted: !detail.isMuted })
    flash(detail.isMuted ? '已解除禁言' : '已禁言')
  }

  const doForceOffline = async () => {
    if (!detail) return
    await api.post(`/players/${detail.account}/force-offline`)
    setDetail({ ...detail, isOnline: false }); flash('已強制下線')
  }

  const doRecharge = async () => {
    if (!detail || twdAmount <= 0) return
    await api.post(`/players/${detail.account}/recharge`, { twdAmount, goldAmount: giveGold ? goldAmount : 0, giveGold })
    flash(S.rechargeDone); setShowRecharge(false); setTwdAmount(0); setGoldAmount(0)
    loadDetail(detail.account)
  }

  const doRename = async () => {
    if (!detail || !newName.trim()) return
    await api.post(`/players/${detail.account}/rename`, { newName: newName.trim() })
    setDetail({ ...detail, onlineName: newName.trim() })
    setShowRename(false); flash('改名成功')
  }

  const doResetPaydata = async () => {
    if (!detail || !confirm('確定重置儲值循環進度為 0？')) return
    await api.post(`/players/${detail.account}/paydata/reset`)
    flash('已重置進度'); loadDetail(detail.account)
  }

  const cycleProgress = detail ? Math.min(100, ((detail.paydataPoint ?? 0) / CYCLE) * 100) : 0

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>👥 {S.pagePlayerMgr}</h1>

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

      <div style={{ display: 'flex', gap: 20, alignItems: 'flex-start' }}>
        {/* 玩家列表 - 表格格式 */}
        <div style={{ flex: 1, minWidth: 0, overflow: 'auto' }}>
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
                    <Th>寵物</Th>
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
            width: 360, background: 'var(--bg-card)', border: '1px solid var(--border)',
            borderRadius: 10, padding: 18, flexShrink: 0, maxHeight: '85vh', overflowY: 'auto'
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
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: 8 }}>
                  {RECHARGE_TIERS.map(t => (
                    <button key={t.twd} onClick={() => { setTwdAmount(t.twd); setGoldAmount(t.gold); setGiveGold(true) }}
                      style={{ padding: '4px 8px', fontSize: 11, background: twdAmount === t.twd ? 'var(--accent-orange)' : 'var(--bg-card)', color: twdAmount === t.twd ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 4 }}>
                      {t.label}
                    </button>
                  ))}
                </div>
                <div style={{ display: 'flex', gap: 8, marginBottom: 6 }}>
                  <label style={{ flex: 1, fontSize: 12 }}>NT$ 台幣
                    <input type="number" min={0} value={twdAmount || ''} onChange={e => setTwdAmount(+e.target.value || 0)} style={{ width: '100%', marginTop: 2 }} />
                  </label>
                  <label style={{ flex: 1, fontSize: 12 }}>金幣數量
                    <input type="number" min={0} value={goldAmount || ''} onChange={e => setGoldAmount(+e.target.value || 0)} style={{ width: '100%', marginTop: 2 }} />
                  </label>
                </div>
                <label style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, marginBottom: 8 }}>
                  <input type="checkbox" checked={giveGold} onChange={e => setGiveGold(e.target.checked)} />
                  同時發放金幣
                </label>
                <div style={{ display: 'flex', gap: 8 }}>
                  <button onClick={doRecharge} disabled={twdAmount <= 0}
                    style={{ flex: 1, background: 'var(--accent-orange)', color: '#fff', padding: '6px 0', borderRadius: 6 }}>{S.rechargeConfirm}</button>
                  <button onClick={() => { setShowRecharge(false); setTwdAmount(0); setGoldAmount(0) }}
                    style={{ padding: '6px 12px', border: '1px solid var(--border)', borderRadius: 6 }}>{S.cancel}</button>
                </div>
              </div>
            )}

            {/* 遊戲幣 */}
            <SectionLabel label={`💰 ${S.gold}`} />
            <div style={{ display: 'flex', gap: 6, marginBottom: 4 }}>
              <input value={goldVal} onChange={e => setGoldVal(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && saveGold()} style={{ flex: 1 }} />
              <button onClick={saveGold} style={{ background: 'var(--accent-blue)', color: '#fff', fontSize: 12, padding: '5px 10px' }}>{S.setGold}</button>
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
                <span style={{ color: 'var(--text-muted)' }}>本循環進度</span>
                <span style={{ color: 'var(--accent-orange)', fontWeight: 600 }}>
                  NT$ {(detail.paydataPoint ?? 0).toLocaleString()} / {CYCLE.toLocaleString()}
                </span>
              </div>
              <div style={{ height: 8, background: 'var(--border)', borderRadius: 4, overflow: 'hidden', marginBottom: 6 }}>
                <div style={{ height: '100%', width: `${cycleProgress}%`, background: 'var(--accent-orange)', borderRadius: 4, transition: 'width .3s' }} />
              </div>
              <Row label="累計儲值(NT$)" value={`NT$ ${(detail.payTotal ?? 0).toLocaleString()}`} />
              <Row label="完成循環次數" value={`${detail.totalCheck ?? 0} 次`} />
              <Row label="歷史永久累計" value={`NT$ ${(detail.paydataTotal ?? 0).toLocaleString()}`} />
              <button onClick={doResetPaydata} style={{ marginTop: 6, width: '100%', fontSize: 11, padding: '4px 0', background: 'rgba(245,101,101,.12)', color: 'var(--accent-red)', border: '1px solid var(--accent-red)', borderRadius: 4 }}>
                🔄 重置循環進度
              </button>
            </div>

            {/* 玩家資訊 */}
            <SectionLabel label="📋 玩家資訊" />
            <div style={{ marginBottom: 12 }}>
              <Row label="主帳號" value={detail.masterName || '—'} />
              <Row label="伺服器" value={detail.serverId > 0 ? `ch${detail.serverId}` : '—'} />
              <Row label="群組 ID" value={String(detail.groupId ?? 0)} />
              <Row label="NeiCe" value={String(detail.neiCe ?? 0)} />
              <Row label="寵物數" value={`${detail.petCount} 隻`} />
              <Row label="郵件" value={`${detail.unreadMails} 未讀 / ${detail.totalMails} 封`} />
              <Row label={S.regTime} value={detail.regTime} />
              <Row label={S.loginTime} value={detail.loginTime} />
              <Row label={S.regIP} value={detail.regIP} />
              <Row label={S.lastIP} value={detail.ip} />
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
                  <div style={{ display: 'flex', gap: 8, marginBottom: 6 }}>
                    {[1, 3, 7, 14, 30].map(d => (
                      <button key={d} onClick={() => setBanDays(d)}
                        style={{ fontSize: 12, padding: '3px 8px', background: banDays === d ? 'var(--accent-red)' : 'var(--bg-input)', color: banDays === d ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 4 }}>
                        {d}天
                      </button>
                    ))}
                    <button onClick={() => setBanDays(0)}
                      style={{ fontSize: 12, padding: '3px 8px', background: banDays === 0 ? 'var(--accent-red)' : 'var(--bg-input)', color: banDays === 0 ? '#fff' : 'var(--text-secondary)', border: '1px solid var(--border)', borderRadius: 4 }}>
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
const Row = ({ label, value }: { label: string; value: string }) => (
  <div style={{ display: 'flex', justifyContent: 'space-between', padding: '3px 0', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
    <span style={{ color: 'var(--text-muted)' }}>{label}</span>
    <span style={{ color: 'var(--text-primary)' }}>{value || S.em}</span>
  </div>
)
const Th = ({ children }: { children: React.ReactNode }) => (
  <th style={{ padding: '8px 10px', textAlign: 'left', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' }}>
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
