import { useEffect, useState, useCallback } from 'react'
import api from '../api'

// ── 型別 ──────────────────────────────────────────────────────
interface MasterStats {
  totalMasters: number
  onlineMasters: number
  offlineMasters: number
}
interface ChannelEntry {
  serverId: number
  serverName: string
  onlineCount: number
  totalCount: number
}
interface OnlineIpRow {
  ip: string
  onlineCount: number
  totalCount: number
}
interface OnlineIpSummary {
  totalOnline: number
  distinctIpWithOnline: number
  distinctIpAll: number
  onlineWithoutLoginIp: number
}
interface RegAccount {
  account: string
  charName: string
  masterName: string
  regTime: string
  regIP: string
  serverName: string
  isOnline: boolean
}
interface SharedAccount {
  account: string
  charName: string
  masterName: string
  ip: string
  regIp: string
  isOnline: boolean
  matchIps: string[]
}
interface SharedIpResult {
  found: boolean
  message?: string
  account?: string
  charName?: string
  isOnline?: boolean
  loginIp?: string
  regIp?: string
  ips?: string[]
  sharedAccounts?: SharedAccount[]
}
interface IpGroupAccount {
  account: string
  charName: string
  masterName: string
  isOnline: boolean
}
interface IpGroup {
  ip: string
  onlineCount: number
  totalCount: number
  accounts: IpGroupAccount[]
}
interface IpGroupsResult {
  groups: IpGroup[]
  totalGroups: number
  totalAccounts: number
  error?: string
}
interface IpOwnerResult {
  found: boolean
  message?: string
  ip?: string
  account?: string
  charName?: string
  masterName?: string
  loginIp?: string
  regIp?: string
  isOnline?: boolean
  regTime?: string
  matchType?: string
}

// ── 小元件 ────────────────────────────────────────────────────
const StatCard = ({
  icon, label, value, color, sub,
}: { icon: string; label: string; value: string | number; color: string; sub?: string }) => (
  <div style={{
    display: 'flex', flexDirection: 'row', alignItems: 'stretch', gap: 18,
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 14, padding: '22px 26px', flex: '1 1 200px', minWidth: 200, maxWidth: '100%',
    borderTop: `4px solid ${color}`,
    boxShadow: 'var(--neu-shadow-raised-sm)',
  }}>
    <div style={{
      fontSize: 40, lineHeight: 1, display: 'flex', alignItems: 'center', justifyContent: 'center',
      width: 56, flexShrink: 0,
    }} aria-hidden>{icon}</div>
    <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', justifyContent: 'center', gap: 4 }}>
      <div style={{ fontSize: 12, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '0.02em' }}>{label}</div>
      <div className="tabular-nums" style={{ fontSize: 30, fontWeight: 800, color, lineHeight: 1.12 }}>{value}</div>
      {sub && <div className="tabular-nums" style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>{sub}</div>}
    </div>
  </div>
)

// ── 分流卡片 ──────────────────────────────────────────────────
const ChannelCard = ({
  entry, maxOnline,
}: { entry: ChannelEntry; maxOnline: number }) => {
  const pct = maxOnline > 0 ? (entry.onlineCount / maxOnline) * 100 : 0
  const name = entry.serverName || `分流 ${entry.serverId}`
  return (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 12, padding: '18px 20px', minWidth: 148, flex: '1 1 160px',
      boxShadow: 'var(--neu-shadow-raised-sm)',
      display: 'flex', flexDirection: 'column', gap: 10,
    }}>
      <div style={{ fontSize: 14, fontWeight: 800, color: 'var(--text-secondary)', lineHeight: 1.3 }}>{name}</div>
      <div style={{
        height: 10, background: 'var(--bg-mid)', borderRadius: 5, overflow: 'hidden',
        boxShadow: 'var(--neu-shadow-inset-sm)',
      }}>
        <div style={{
          height: '100%', borderRadius: 5,
          width: `${pct}%`,
          background: entry.onlineCount > 0 ? '#16b97a' : 'var(--border)',
          transition: 'width .4s',
          minWidth: entry.onlineCount > 0 ? 6 : 0,
        }} />
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'baseline', justifyContent: 'space-between', gap: 8 }}>
        <span className="tabular-nums" style={{ fontSize: 22, fontWeight: 800, color: '#16b97a' }}>{entry.onlineCount.toLocaleString()}</span>
        <span style={{ fontSize: 12, color: 'var(--text-muted)', fontWeight: 600 }}>在線</span>
      </div>
      <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 'auto', paddingTop: 2 }}>
        總計 <span className="tabular-nums" style={{ fontWeight: 700, color: 'var(--text-secondary)' }}>{entry.totalCount.toLocaleString()}</span>
      </div>
    </div>
  )
}

// ── 主頁面 ────────────────────────────────────────────────────
export default function ServerStatusPage() {
  const [masterStats, setMasterStats]   = useState<MasterStats | null>(null)
  const [channels, setChannels]         = useState<ChannelEntry[]>([])
  const [onlineByIp, setOnlineByIp]     = useState<OnlineIpRow[]>([])
  const [ipSummary, setIpSummary]         = useState<OnlineIpSummary | null>(null)
  const [accounts, setAccounts]         = useState<RegAccount[]>([])
  const [limit, setLimit]               = useState(30)
  const [loading, setLoading]           = useState(false)
  const [lastUpdate, setLastUpdate]     = useState('')

  // ── 重複 IP 查詢狀態 ────────────────────────────────────────
  const [ipQuery, setIpQuery]           = useState('')
  const [ipLoading, setIpLoading]       = useState(false)
  const [ipResult, setIpResult]         = useState<SharedIpResult | null>(null)

  // ── 自動掃描狀態 ─────────────────────────────────────────────
  const [scanLoading, setScanLoading]   = useState(false)
  const [scanResult, setScanResult]     = useState<IpGroupsResult | null>(null)
  const [minGroup, setMinGroup]         = useState(2)
  const [expandedIps, setExpandedIps]   = useState<Set<string>>(new Set())
  const [filterOnline, setFilterOnline] = useState(false)

  // ── IP 原始主人查詢 ───────────────────────────────────────────
  const [ipOwnerResult, setIpOwnerResult]   = useState<IpOwnerResult | null>(null)
  const [ipOwnerLoading, setIpOwnerLoading] = useState(false)
  const [ipOwnerTarget, setIpOwnerTarget]   = useState('')

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const [ms, ch, ipSum, ip, ac] = await Promise.all([
        api.get('/server-status/master-stats'),
        api.get('/server-status/channel-online'),
        api.get('/server-status/online-ip-summary'),
        api.get('/server-status/online-by-ip', { params: { top: 40 } }),
        api.get(`/server-status/recent-registrations?limit=${limit}`),
      ])
      setMasterStats(ms.data)
      setChannels(ch.data)
      setIpSummary(ipSum.data as OnlineIpSummary)
      setOnlineByIp(Array.isArray(ip.data) ? ip.data : [])
      setAccounts(ac.data)
      setLastUpdate(new Date().toLocaleTimeString('zh-TW'))
    } finally {
      setLoading(false)
    }
  }, [limit])

  useEffect(() => { refresh() }, [refresh])

  // 每 30 秒自動刷新
  useEffect(() => {
    const id = setInterval(refresh, 30_000)
    return () => clearInterval(id)
  }, [refresh])

  const maxOnline = Math.max(...channels.map(c => c.onlineCount), 1)

  const querySharedIp = async () => {
    if (!ipQuery.trim()) return
    setIpLoading(true); setIpResult(null)
    try {
      const r = await api.get('/server-status/shared-ip', { params: { account: ipQuery.trim() } })
      setIpResult(r.data as SharedIpResult)
    } catch {
      setIpResult({ found: false, message: '查詢失敗，請確認帳號是否正確' })
    } finally {
      setIpLoading(false)
    }
  }

  const scanIpGroups = async () => {
    setScanLoading(true); setScanResult(null); setExpandedIps(new Set())
    try {
      const r = await api.get('/server-status/ip-groups', { params: { minGroup } })
      setScanResult(r.data as IpGroupsResult)
    } catch {
      setScanResult({ groups: [], totalGroups: 0, totalAccounts: 0, error: '掃描失敗' })
    } finally {
      setScanLoading(false)
    }
  }

  const toggleExpand = (ip: string) => {
    setExpandedIps(prev => {
      const n = new Set(prev)
      n.has(ip) ? n.delete(ip) : n.add(ip)
      return n
    })
  }

  const queryIpOwner = async (ip: string) => {
    setIpOwnerTarget(ip)
    setIpOwnerLoading(true)
    setIpOwnerResult(null)
    try {
      const r = await api.get('/server-status/ip-owner', { params: { ip } })
      setIpOwnerResult(r.data as IpOwnerResult)
    } catch {
      setIpOwnerResult({ found: false, message: '查詢失敗' })
    } finally {
      setIpOwnerLoading(false)
    }
  }

  return (
    <div className="gm-page-stack server-status-page">
      {/* ── 標題列 ── */}
      <div style={{
        display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap',
        gap: 16, paddingBottom: 4, borderBottom: '1px solid rgba(148, 163, 184, 0.35)', marginBottom: 4,
      }}>
        <div style={{ minWidth: 0 }}>
          <h1 style={{ fontSize: 26, fontWeight: 800, color: 'var(--text-primary)', margin: 0, letterSpacing: '-0.03em' }}>🖥 伺服器狀態</h1>
          {lastUpdate && (
            <div style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 8, lineHeight: 1.5 }}>
              最後更新 {lastUpdate}（每 30 秒自動刷新）
            </div>
          )}
        </div>
        <button
          onClick={refresh} disabled={loading}
          style={{
            padding: '12px 24px', borderRadius: 10, border: 'none', cursor: 'pointer',
            background: loading ? 'var(--bg-mid)' : '#1e4ba0',
            color: '#fff', fontWeight: 700, fontSize: 14,
            opacity: loading ? 0.6 : 1,
            flexShrink: 0,
            boxShadow: loading ? undefined : '4px 4px 12px rgba(30, 75, 160, 0.28)',
          }}
        >
          {loading ? '更新中…' : '🔄 重新整理'}
        </button>
      </div>

      {/* ── 主帳號統計 ── */}
      <section className="server-status-section">
        <div className="server-status-section-title">主帳號統計</div>
        <div style={{ display: 'flex', gap: 18, flexWrap: 'wrap' }}>
          <StatCard icon="👑" label="主帳號總數"  color="#3b82f6"
            value={masterStats ? masterStats.totalMasters.toLocaleString() : '—'} />
          <StatCard icon="🟢" label="目前在線"    color="#16b97a"
            value={masterStats ? masterStats.onlineMasters.toLocaleString() : '—'}
            sub={masterStats ? `佔 ${masterStats.totalMasters > 0 ? ((masterStats.onlineMasters / masterStats.totalMasters) * 100).toFixed(1) : 0}%` : undefined} />
          <StatCard icon="⚫" label="目前離線"    color="#94a3b8"
            value={masterStats ? masterStats.offlineMasters.toLocaleString() : '—'} />
        </div>
      </section>

      {/* ── 各分流在線 ── */}
      <section className="server-status-section">
        <div className="server-status-section-title">各分流在線人數</div>
        {channels.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', fontSize: 14, padding: '8px 4px' }}>（無分流資料）</div>
        ) : (
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            {channels.map(ch => (
              <ChannelCard key={ch.serverId} entry={ch} maxOnline={maxOnline} />
            ))}
          </div>
        )}
      </section>

      {/* ── 登入 IP 在線人數 ── */}
      <section className="server-status-section">
        <div className="server-status-section-title">登入 IP 在線人數</div>
        <p style={{ margin: '0 0 10px', fontSize: 13, color: 'var(--text-muted)', lineHeight: 1.55, maxWidth: '62rem' }}>
          依目前登入 IP（csalogin.IP）彙總；下表為 Top 40。全服總人數與 IP 維度如下（與各分流加總在線應一致）。
        </p>
        {ipSummary && (
          <div style={{
            display: 'flex', flexWrap: 'wrap', gap: 12, rowGap: 10, marginBottom: 12,
            padding: '16px 20px', borderRadius: 12,
            background: 'var(--bg-card)', border: '1px solid var(--border)',
            boxShadow: 'var(--neu-shadow-raised-sm)',
          }}>
            <span style={{ fontSize: 14, fontWeight: 800, color: '#16b97a' }}>
              全服在線人數：{ipSummary.totalOnline.toLocaleString()} 人
            </span>
            <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>
              有在線的登入 IP：{ipSummary.distinctIpWithOnline.toLocaleString()} 個
            </span>
            <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>
              有登入 IP 的相異 IP：{ipSummary.distinctIpAll.toLocaleString()} 個
            </span>
            <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>
              在線但無登入 IP：{ipSummary.onlineWithoutLoginIp.toLocaleString()} 人
            </span>
          </div>
        )}
        <div className="server-status-table-wrap server-status-table-wrap--tall">
          <table className="server-status-table" style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ position: 'sticky', top: 0, zIndex: 1 }}>
                {['登入 IP', '在線', '帳號數'].map(h => (
                  <th key={h} style={{
                    textAlign: h === '登入 IP' ? 'left' : 'right',
                  }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {onlineByIp.map((row, i) => (
                <tr key={row.ip + i} style={{
                  background: i % 2 === 0 ? 'var(--bg-card)' : 'var(--bg-mid)',
                }}>
                  <td style={{ fontFamily: 'ui-monospace, monospace', fontSize: 13 }}>{row.ip}</td>
                  <td style={{ textAlign: 'right', fontWeight: 800, color: '#16b97a' }}>{row.onlineCount.toLocaleString()}</td>
                  <td style={{ textAlign: 'right', color: 'var(--text-secondary)' }}>{row.totalCount.toLocaleString()}</td>
                </tr>
              ))}
              {onlineByIp.length === 0 && !loading && (
                <tr>
                  <td colSpan={3} style={{ padding: 28, textAlign: 'center', color: 'var(--text-muted)' }}>
                    無資料
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* ── 最新註冊帳號 ── */}
      <section
        className="server-status-section"
        style={{ display: 'flex', flexDirection: 'column', gap: 14, flex: '1 1 auto', minHeight: 380 }}
      >
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12 }}>
          <div className="server-status-section-title" style={{ marginBottom: 0 }}>最新註冊帳號</div>
          <select
            value={limit}
            onChange={e => setLimit(Number(e.target.value))}
            style={{
              background: 'var(--bg-input)', color: 'var(--text-primary)',
              border: '1px solid var(--border)', borderRadius: 8,
              padding: '8px 14px', fontSize: 14, minWidth: 140,
            }}
          >
            <option value={20}>最新 20 筆</option>
            <option value={30}>最新 30 筆</option>
            <option value={50}>最新 50 筆</option>
            <option value={100}>最新 100 筆</option>
          </select>
        </div>

        <div className="server-status-table-wrap server-status-table-wrap--fill" style={{ flex: 1, overflow: 'auto', WebkitOverflowScrolling: 'touch' as const }}>
          <table className="server-status-table" style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ position: 'sticky', top: 0, zIndex: 1 }}>
                {['狀態', '帳號', '角色名', '主帳號', '分流', '註冊時間', '註冊 IP'].map(h => (
                  <th key={h} style={{ textAlign: 'left' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {accounts.map((a, i) => (
                <tr key={i} style={{
                  background: i % 2 === 0 ? 'var(--bg-card)' : 'var(--bg-mid)',
                  transition: 'background .1s',
                }}>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <span style={{
                      display: 'inline-block', padding: '4px 10px', borderRadius: 6,
                      fontSize: 12, fontWeight: 700,
                      background: a.isOnline ? 'rgba(22,185,122,.15)' : 'rgba(100,110,140,.15)',
                      color: a.isOnline ? '#16b97a' : 'var(--text-muted)',
                    }}>
                      {a.isOnline ? '🟢 在線' : '⚫ 離線'}
                    </span>
                  </td>
                  <td style={{ color: 'var(--text-primary)', fontWeight: 600 }}>{a.account}</td>
                  <td style={{ color: 'var(--text-secondary)' }}>{a.charName || '—'}</td>
                  <td style={{ color: 'var(--text-secondary)' }}>{a.masterName || '—'}</td>
                  <td style={{ color: 'var(--text-muted)' }}>{a.serverName || '—'}</td>
                  <td style={{ color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{a.regTime || '—'}</td>
                  <td style={{ color: 'var(--text-muted)', fontFamily: 'ui-monospace, monospace', fontSize: 13 }}>{a.regIP || '—'}</td>
                </tr>
              ))}
              {accounts.length === 0 && !loading && (
                <tr>
                  <td colSpan={7} style={{ padding: 36, textAlign: 'center', color: 'var(--text-muted)' }}>
                    無資料
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* ── 重複 IP 偵測 ── */}
      <section className="server-status-section" style={{ paddingTop: 8 }}>
        <div className="server-status-section-title" style={{ letterSpacing: '0.06em' }}>
          🔍 重複 IP 偵測
        </div>

        {/* ─ 自動掃描 ─ */}
        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 14, padding: '22px 24px', marginBottom: 18, boxShadow: 'var(--neu-shadow-raised-sm)' }}>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12, color: 'var(--text-primary)' }}>
            🤖 全服自動掃描
          </div>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap', marginBottom: 14 }}>
            <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>最少帳號數：</span>
            {[2, 3, 5, 10].map(n => (
              <button key={n} onClick={() => setMinGroup(n)}
                style={{ padding: '5px 14px', borderRadius: 6, border: 'none', cursor: 'pointer', fontSize: 13, fontWeight: 600,
                  background: minGroup === n ? 'var(--accent-blue)' : 'var(--bg-sidebar)',
                  color: minGroup === n ? '#fff' : 'var(--text-muted)' }}>
                {n}+
              </button>
            ))}
            <label style={{ display: 'flex', alignItems: 'center', gap: 6, marginLeft: 12, color: 'var(--text-muted)', fontSize: 13, cursor: 'pointer' }}>
              <input type="checkbox" checked={filterOnline} onChange={e => setFilterOnline(e.target.checked)} />
              只顯示有在線的群組
            </label>
            <button onClick={scanIpGroups} disabled={scanLoading}
              style={{ marginLeft: 'auto', padding: '8px 22px', borderRadius: 8, border: 'none', cursor: 'pointer',
                background: scanLoading ? 'var(--bg-mid)' : '#1e4ba0', color: '#fff', fontWeight: 700, fontSize: 14, opacity: scanLoading ? 0.6 : 1 }}>
              {scanLoading ? '⏳ 掃描中…' : '🔍 開始掃描'}
            </button>
          </div>

          {/* 掃描結果 */}
          {scanResult && (
            <div>
              {scanResult.error ? (
                <div style={{ padding: 12, color: 'var(--accent-red)', fontSize: 13 }}>❌ {scanResult.error}</div>
              ) : scanResult.totalGroups === 0 ? (
                <div style={{ padding: 14, color: '#16b97a', fontSize: 13, fontWeight: 600 }}>✅ 未發現任何共用 IP 群組</div>
              ) : (
                <>
                  <div style={{ display: 'flex', gap: 20, marginBottom: 12, flexWrap: 'wrap' }}>
                    <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>
                      共發現 <b style={{ color: '#f87171', fontSize: 16 }}>{scanResult.totalGroups}</b> 個共用IP群組，
                      涉及 <b style={{ color: '#fb923c', fontSize: 16 }}>{scanResult.totalAccounts}</b> 個帳號
                    </span>
                    <button onClick={() => setExpandedIps(new Set(scanResult.groups.map(g => g.ip)))}
                      style={{ padding: '3px 12px', borderRadius: 5, border: '1px solid var(--border)', background: 'transparent', color: 'var(--text-muted)', cursor: 'pointer', fontSize: 12 }}>
                      全部展開
                    </button>
                    <button onClick={() => setExpandedIps(new Set())}
                      style={{ padding: '3px 12px', borderRadius: 5, border: '1px solid var(--border)', background: 'transparent', color: 'var(--text-muted)', cursor: 'pointer', fontSize: 12 }}>
                      全部收合
                    </button>
                  </div>

                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                    {scanResult.groups
                      .filter(g => !filterOnline || g.onlineCount > 0)
                      .map(g => {
                        const expanded = expandedIps.has(g.ip)
                        const hasOnline = g.onlineCount > 0
                        return (
                          <div key={g.ip} style={{ border: `1px solid ${hasOnline ? 'rgba(248,113,113,.4)' : 'var(--border)'}`, borderRadius: 8, overflow: 'hidden' }}>
                            {/* 群組標題列（可點擊展開） */}
                            <div onClick={() => toggleExpand(g.ip)}
                              style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '9px 14px', cursor: 'pointer',
                                background: hasOnline ? 'rgba(248,113,113,.07)' : 'var(--bg-sidebar)',
                                userSelect: 'none' }}>
                              <span style={{ fontSize: 12, color: 'var(--text-muted)', transition: 'transform .2s', display: 'inline-block', transform: expanded ? 'rotate(90deg)' : 'rotate(0)' }}>▶</span>
                              <code
                                onClick={e => { e.stopPropagation(); queryIpOwner(g.ip) }}
                                title="點擊查詢此IP的原始主人"
                                style={{ fontFamily: 'monospace', fontSize: 13, fontWeight: 700, color: hasOnline ? '#f87171' : 'var(--accent-blue)', minWidth: 130, cursor: 'pointer', textDecoration: 'underline dotted' }}
                              >{g.ip}</code>                              <span style={{ padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 700, background: 'rgba(248,113,113,.15)', color: '#f87171' }}>
                                {g.totalCount} 個帳號
                              </span>
                              {g.onlineCount > 0 && (
                                <span style={{ padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 700, background: 'rgba(22,185,122,.15)', color: '#16b97a' }}>
                                  🟢 {g.onlineCount} 在線
                                </span>
                              )}
                              <span style={{ marginLeft: 'auto', fontSize: 12, color: 'var(--text-muted)' }}>
                                {g.accounts.slice(0, 3).map(a => a.charName || a.account).join('、')}{g.accounts.length > 3 ? ` 等${g.accounts.length}人` : ''}
                              </span>
                            </div>

                            {/* 帳號明細（展開時顯示） */}
                            {expanded && (
                              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                                <thead>
                                  <tr style={{ background: 'var(--bg-dark)' }}>
                                    {['狀態', '帳號', '角色名', '主帳號'].map(h => (
                                      <th key={h} style={{ padding: '7px 12px', textAlign: 'left', color: 'var(--text-muted)', fontWeight: 600, borderBottom: '1px solid var(--border)' }}>{h}</th>
                                    ))}
                                  </tr>
                                </thead>
                                <tbody>
                                  {g.accounts.map((a, i) => (
                                    <tr key={i} style={{ background: i % 2 === 0 ? 'var(--bg-card)' : 'var(--bg-mid)', borderBottom: '1px solid var(--border)' }}>
                                      <td style={{ padding: '7px 12px' }}>
                                        <span style={{ padding: '2px 7px', borderRadius: 4, fontSize: 11, fontWeight: 700,
                                          background: a.isOnline ? 'rgba(22,185,122,.15)' : 'rgba(100,110,140,.15)',
                                          color: a.isOnline ? '#16b97a' : 'var(--text-muted)' }}>
                                          {a.isOnline ? '🟢 在線' : '⚫ 離線'}
                                        </span>
                                      </td>
                                      <td style={{ padding: '7px 12px', fontWeight: 700 }}>{a.account}</td>
                                      <td style={{ padding: '7px 12px', color: 'var(--text-secondary)' }}>{a.charName || '—'}</td>
                                      <td style={{ padding: '7px 12px', color: 'var(--text-muted)' }}>{a.masterName || '—'}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            )}
                          </div>
                        )
                      })}
                  </div>
                </>
              )}
            </div>
          )}
        </div>

        {/* ─ 手動查單一帳號 ─ */}
        <details style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: '12px 20px' }}>
          <summary style={{ cursor: 'pointer', fontWeight: 700, fontSize: 13, color: 'var(--text-muted)', userSelect: 'none' }}>
            🔎 手動查單一帳號
          </summary>
          <div style={{ marginTop: 12 }}>
            <div style={{ display: 'flex', gap: 8, marginBottom: 14, flexWrap: 'wrap' }}>
              <input value={ipQuery} onChange={e => setIpQuery(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && querySharedIp()}
                placeholder="輸入帳號名稱（完整）"
                style={{ padding: '8px 14px', width: 260, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 8, color: 'var(--text-primary)', fontSize: 14 }} />
              <button onClick={querySharedIp} disabled={ipLoading}
                style={{ padding: '8px 20px', borderRadius: 8, border: 'none', cursor: 'pointer',
                  background: ipLoading ? 'var(--bg-mid)' : '#1e4ba0', color: '#fff', fontWeight: 700, fontSize: 14, opacity: ipLoading ? 0.6 : 1 }}>
                {ipLoading ? '查詢中…' : '查詢'}
              </button>
            </div>

            {ipResult && !ipResult.found && (
              <div style={{ padding: '10px 14px', background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 8, color: 'var(--accent-red)', fontSize: 13 }}>
                ❌ {ipResult.message}
              </div>
            )}

            {ipResult?.found && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <div style={{ background: 'var(--bg-sidebar)', border: '1px solid var(--border)', borderRadius: 10, padding: '12px 16px', display: 'flex', gap: 28, flexWrap: 'wrap' }}>
                  <div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>查詢帳號</div>
                    <div style={{ fontWeight: 700, fontSize: 14 }}>
                      {ipResult.account}
                      <span style={{ marginLeft: 8, padding: '2px 7px', borderRadius: 4, fontSize: 11, fontWeight: 700,
                        background: ipResult.isOnline ? 'rgba(22,185,122,.15)' : 'rgba(100,110,140,.15)',
                        color: ipResult.isOnline ? '#16b97a' : 'var(--text-muted)' }}>
                        {ipResult.isOnline ? '🟢 在線' : '⚫ 離線'}
                      </span>
                    </div>
                  </div>
                  <div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>登入 IP</div>
                    <code onClick={() => ipResult.loginIp && queryIpOwner(ipResult.loginIp)} title="點擊查詢此IP的原始主人" style={{ fontSize: 13, color: 'var(--accent-blue)', fontWeight: 600, cursor: ipResult.loginIp ? 'pointer' : 'default', textDecoration: ipResult.loginIp ? 'underline dotted' : 'none' }}>{ipResult.loginIp || '—'}</code>
                  </div>
                  <div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>註冊 IP</div>
                    <code style={{ fontSize: 13, color: '#f59e0b', fontWeight: 600 }}>{ipResult.regIp || '—'}</code>
                  </div>
                </div>

                {ipResult.sharedAccounts && ipResult.sharedAccounts.length > 0 ? (
                  <div style={{ border: '1px solid rgba(248,113,113,.4)', borderRadius: 10, overflow: 'auto' }}>
                    <div style={{ padding: '9px 14px', background: 'rgba(239,68,68,.08)', borderBottom: '1px solid var(--border)', fontSize: 13, fontWeight: 700, color: '#f87171' }}>
                      ⚠ 發現 {ipResult.sharedAccounts.length} 個共用 IP 的帳號
                    </div>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-dark)' }}>
                          {['狀態', '帳號', '角色名', '主帳號', '登入 IP', '命中 IP'].map(h => (
                            <th key={h} style={{ padding: '8px 12px', textAlign: 'left', color: 'var(--text-secondary)', fontWeight: 700, borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' }}>{h}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {ipResult.sharedAccounts.map((a, i) => (
                          <tr key={i} style={{ background: i % 2 === 0 ? 'var(--bg-card)' : 'var(--bg-mid)', borderBottom: '1px solid var(--border)' }}>
                            <td style={{ padding: '7px 12px' }}>
                              <span style={{ padding: '2px 7px', borderRadius: 4, fontSize: 11, fontWeight: 700,
                                background: a.isOnline ? 'rgba(22,185,122,.15)' : 'rgba(100,110,140,.15)',
                                color: a.isOnline ? '#16b97a' : 'var(--text-muted)' }}>
                                {a.isOnline ? '🟢 在線' : '⚫ 離線'}
                              </span>
                            </td>
                            <td style={{ padding: '7px 12px', fontWeight: 700 }}>{a.account}</td>
                            <td style={{ padding: '7px 12px', color: 'var(--text-secondary)' }}>{a.charName || '—'}</td>
                            <td style={{ padding: '7px 12px', color: 'var(--text-muted)' }}>{a.masterName || '—'}</td>
                            <td style={{ padding: '7px 12px', fontFamily: 'monospace', fontSize: 12, color: 'var(--accent-blue)' }}>
                            <span onClick={() => a.ip && queryIpOwner(a.ip)} title="點擊查詢原始主人" style={{ cursor: a.ip ? 'pointer' : 'default', textDecoration: a.ip ? 'underline dotted' : 'none' }}>{a.ip || '—'}</span>
                          </td>
                            <td style={{ padding: '7px 12px' }}>
                              {a.matchIps.map(ip => (
                                <span key={ip} onClick={() => queryIpOwner(ip)} title="點擊查詢原始主人" style={{ display: 'inline-block', padding: '2px 7px', borderRadius: 4, fontSize: 11, fontWeight: 600, background: 'rgba(239,68,68,.15)', color: '#f87171', marginRight: 4, fontFamily: 'monospace', cursor: 'pointer', textDecoration: 'underline dotted' }}>{ip}</span>
                              ))}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <div style={{ padding: '12px 16px', background: 'rgba(22,185,122,.08)', border: '1px solid rgba(22,185,122,.3)', borderRadius: 10, color: '#16b97a', fontSize: 13, fontWeight: 600 }}>
                    ✅ 未發現共用相同 IP 的其他帳號
                  </div>
                )}
              </div>
            )}
          </div>
        </details>

        {/* ── IP 原始主人查詢結果 ── */}
        {(ipOwnerLoading || ipOwnerResult) && (
          <div style={{ background: 'var(--bg-card)', border: `1px solid ${ipOwnerResult?.found ? 'var(--accent-blue)' : 'var(--accent-red)'}`, borderRadius: 14, padding: '20px 24px', marginTop: 12, boxShadow: 'var(--neu-shadow-raised-sm)' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
              <div style={{ fontWeight: 700, fontSize: 14 }}>
                🔎 IP 原始主人查詢
                {ipOwnerTarget && <code style={{ marginLeft: 10, fontFamily: 'monospace', fontSize: 13, color: 'var(--accent-blue)' }}>{ipOwnerTarget}</code>}
              </div>
              <button onClick={() => setIpOwnerResult(null)}
                style={{ background: 'transparent', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', fontSize: 18, lineHeight: 1 }}>✕</button>
            </div>
            {ipOwnerLoading && <div style={{ color: 'var(--text-muted)', fontSize: 13 }}>查詢中…</div>}
            {ipOwnerResult && !ipOwnerResult.found && (
              <div style={{ color: 'var(--accent-red)', fontSize: 13 }}>❌ {ipOwnerResult.message}</div>
            )}
            {ipOwnerResult?.found && (
              <div style={{ display: 'flex', gap: 28, flexWrap: 'wrap', alignItems: 'flex-start' }}>
                <div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>最早使用者（原始主人）</div>
                  <div style={{ fontWeight: 800, fontSize: 16 }}>
                    {ipOwnerResult.account}
                    <span style={{ marginLeft: 8, padding: '2px 7px', borderRadius: 4, fontSize: 11, fontWeight: 700,
                      background: ipOwnerResult.isOnline ? 'rgba(22,185,122,.15)' : 'rgba(100,110,140,.15)',
                      color: ipOwnerResult.isOnline ? '#16b97a' : 'var(--text-muted)' }}>
                      {ipOwnerResult.isOnline ? '🟢 在線' : '⚫ 離線'}
                    </span>
                  </div>
                  {ipOwnerResult.charName && <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 2 }}>角色：{ipOwnerResult.charName}</div>}
                  {ipOwnerResult.masterName && <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>主帳號：{ipOwnerResult.masterName}</div>}
                </div>
                <div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>最早時間</div>
                  <div style={{ fontSize: 14, fontWeight: 600 }}>{ipOwnerResult.regTime || '—'}</div>
                </div>
                <div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>命中方式</div>
                  <div style={{ fontSize: 13, color: '#f59e0b', fontWeight: 600 }}>{ipOwnerResult.matchType || '—'}</div>
                </div>
                <div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>登入 IP / 註冊 IP</div>
                  <code style={{ fontSize: 12, color: 'var(--accent-blue)' }}>{ipOwnerResult.loginIp || '—'}</code>
                  <span style={{ margin: '0 6px', color: 'var(--text-muted)' }}>/</span>
                  <code style={{ fontSize: 12, color: '#f59e0b' }}>{ipOwnerResult.regIp || '—'}</code>
                </div>
              </div>
            )}
          </div>
        )}
      </section>
    </div>
  )
}
