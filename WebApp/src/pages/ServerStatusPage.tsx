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

// ── 小元件 ────────────────────────────────────────────────────
const StatCard = ({
  icon, label, value, color, sub,
}: { icon: string; label: string; value: string | number; color: string; sub?: string }) => (
  <div style={{
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 12, padding: '20px 24px', flex: 1, minWidth: 160,
    borderTop: `3px solid ${color}`,
  }}>
    <div style={{ fontSize: 28, marginBottom: 6 }}>{icon}</div>
    <div style={{ fontSize: 28, fontWeight: 800, color, lineHeight: 1 }}>{value}</div>
    <div style={{ fontSize: 13, color: 'var(--text-muted)', marginTop: 6 }}>{label}</div>
    {sub && <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{sub}</div>}
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
      borderRadius: 10, padding: '14px 16px', minWidth: 130, flex: '1 1 130px',
    }}>
      <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-secondary)', marginBottom: 8 }}>{name}</div>
      {/* 長條 */}
      <div style={{ height: 6, background: 'var(--bg-mid)', borderRadius: 3, marginBottom: 8 }}>
        <div style={{
          height: '100%', borderRadius: 3,
          width: `${pct}%`,
          background: entry.onlineCount > 0 ? '#16b97a' : 'var(--border)',
          transition: 'width .4s',
          minWidth: entry.onlineCount > 0 ? 4 : 0,
        }} />
      </div>
      <div style={{ fontSize: 20, fontWeight: 800, color: '#16b97a' }}>{entry.onlineCount.toLocaleString()}</div>
      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>在線 / 總計 {entry.totalCount.toLocaleString()}</div>
    </div>
  )
}

// ── 主頁面 ────────────────────────────────────────────────────
export default function ServerStatusPage() {
  const [masterStats, setMasterStats]   = useState<MasterStats | null>(null)
  const [channels, setChannels]         = useState<ChannelEntry[]>([])
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

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const [ms, ch, ac] = await Promise.all([
        api.get('/server-status/master-stats'),
        api.get('/server-status/channel-online'),
        api.get(`/server-status/recent-registrations?limit=${limit}`),
      ])
      setMasterStats(ms.data)
      setChannels(ch.data)
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

  return (
    <div style={{ padding: '24px 28px', display: 'flex', flexDirection: 'column', gap: 24, minHeight: 0 }}>
      {/* ── 標題列 ── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 10 }}>
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 800, color: 'var(--text-primary)', margin: 0 }}>🖥 伺服器狀態</h1>
          {lastUpdate && (
            <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4 }}>
              最後更新 {lastUpdate}（每 30 秒自動刷新）
            </div>
          )}
        </div>
        <button
          onClick={refresh} disabled={loading}
          style={{
            padding: '8px 20px', borderRadius: 8, border: 'none', cursor: 'pointer',
            background: loading ? 'var(--bg-mid)' : '#1e4ba0',
            color: '#fff', fontWeight: 700, fontSize: 14,
            opacity: loading ? 0.6 : 1,
          }}
        >
          {loading ? '更新中…' : '🔄 重新整理'}
        </button>
      </div>

      {/* ── 主帳號統計 ── */}
      <section>
        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: 1, marginBottom: 10, textTransform: 'uppercase' }}>
          主帳號統計
        </div>
        <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
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
      <section>
        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: 1, marginBottom: 10, textTransform: 'uppercase' }}>
          各分流在線人數
        </div>
        {channels.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', fontSize: 13 }}>（無分流資料）</div>
        ) : (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {channels.map(ch => (
              <ChannelCard key={ch.serverId} entry={ch} maxOnline={maxOnline} />
            ))}
          </div>
        )}
      </section>

      {/* ── 最新註冊帳號 ── */}
      <section style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10, flexWrap: 'wrap', gap: 8 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: 1, textTransform: 'uppercase' }}>
            最新註冊帳號
          </div>
          <select
            value={limit}
            onChange={e => setLimit(Number(e.target.value))}
            style={{
              background: 'var(--bg-input)', color: 'var(--text-primary)',
              border: '1px solid var(--border)', borderRadius: 6,
              padding: '4px 10px', fontSize: 13,
            }}
          >
            <option value={20}>最新 20 筆</option>
            <option value={30}>最新 30 筆</option>
            <option value={50}>最新 50 筆</option>
            <option value={100}>最新 100 筆</option>
          </select>
        </div>

        <div style={{ flex: 1, overflow: 'auto', border: '1px solid var(--border)', borderRadius: 10 }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
            <thead>
              <tr style={{ background: 'var(--bg-dark)', position: 'sticky', top: 0, zIndex: 1 }}>
                {['狀態', '帳號', '角色名', '主帳號', '分流', '註冊時間', '註冊 IP'].map(h => (
                  <th key={h} style={{
                    padding: '10px 14px', textAlign: 'left',
                    color: 'var(--text-secondary)', fontWeight: 700,
                    borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap',
                  }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {accounts.map((a, i) => (
                <tr key={i} style={{
                  background: i % 2 === 0 ? 'var(--bg-card)' : 'var(--bg-mid)',
                  transition: 'background .1s',
                }}>
                  <td style={{ padding: '9px 14px', whiteSpace: 'nowrap' }}>
                    <span style={{
                      display: 'inline-block', padding: '2px 8px', borderRadius: 4,
                      fontSize: 11, fontWeight: 700,
                      background: a.isOnline ? 'rgba(22,185,122,.15)' : 'rgba(100,110,140,.15)',
                      color: a.isOnline ? '#16b97a' : 'var(--text-muted)',
                    }}>
                      {a.isOnline ? '🟢 在線' : '⚫ 離線'}
                    </span>
                  </td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-primary)', fontWeight: 600 }}>{a.account}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-secondary)' }}>{a.charName || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-secondary)' }}>{a.masterName || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-muted)' }}>{a.serverName || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{a.regTime || '—'}</td>
                  <td style={{ padding: '9px 14px', color: 'var(--text-muted)', fontFamily: 'monospace', fontSize: 12 }}>{a.regIP || '—'}</td>
                </tr>
              ))}
              {accounts.length === 0 && !loading && (
                <tr>
                  <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--text-muted)' }}>
                    無資料
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* ── 重複 IP 偵測 ── */}
      <section>
        <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: 1, marginBottom: 14, textTransform: 'uppercase' }}>
          🔍 重複 IP 偵測
        </div>

        {/* ─ 自動掃描 ─ */}
        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 12, padding: '16px 20px', marginBottom: 16 }}>
          <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12, color: 'var(--text-primary)' }}>
            🤖 全服自動掃描
          </div>
          <div style={{ display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap', marginBottom: 12 }}>
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
                              <code style={{ fontFamily: 'monospace', fontSize: 13, fontWeight: 700, color: hasOnline ? '#f87171' : 'var(--accent-blue)', minWidth: 130 }}>{g.ip}</code>
                              <span style={{ padding: '2px 8px', borderRadius: 4, fontSize: 11, fontWeight: 700, background: 'rgba(248,113,113,.15)', color: '#f87171' }}>
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
                    <code style={{ fontSize: 13, color: 'var(--accent-blue)', fontWeight: 600 }}>{ipResult.loginIp || '—'}</code>
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
                            <td style={{ padding: '7px 12px', fontFamily: 'monospace', fontSize: 12, color: 'var(--accent-blue)' }}>{a.ip || '—'}</td>
                            <td style={{ padding: '7px 12px' }}>
                              {a.matchIps.map(ip => (
                                <span key={ip} style={{ display: 'inline-block', padding: '2px 7px', borderRadius: 4, fontSize: 11, fontWeight: 600, background: 'rgba(239,68,68,.15)', color: '#f87171', marginRight: 4, fontFamily: 'monospace' }}>{ip}</span>
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
      </section>
    </div>
  )
}
