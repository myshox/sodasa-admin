import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'

interface TradeLog {
  time: string; fromCdkey: string; fromName: string
  toCdkey: string; toName: string; items: string; pets: string
  gold: number; direction: 'sent' | 'received'
}
interface StreetLog {
  time: string; sellCdkey: string; buyCdkey: string; buyName: string
  itemName: string; num: number; price: number; role: 'seller' | 'buyer'
}
interface SpeedLog  { time: string; speedTime: number; speedCnt: number }
interface CostLog   { time: string; name: string; point: number; check: number }

interface HistoryResult {
  trades: TradeLog[]; street: StreetLog[]
  speed: SpeedLog[];  cost: CostLog[]
  tradeSent: number;  tradeReceived: number
}

type Tab = 'trade' | 'street' | 'speed' | 'cost'

export default function PlayerHistoryPage() {
  const [sp] = useSearchParams()
  const [query, setQuery]   = useState(sp.get('account') || '')
  const [data,  setData]    = useState<HistoryResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [msg,   setMsg]     = useState('')
  const [tab,   setTab]     = useState<Tab>('trade')
  const [limit, setLimit]   = useState(100)

  const search = async () => {
    if (!query.trim()) return
    setLoading(true); setMsg(''); setData(null)
    try {
      const r = await api.get(`/players/${encodeURIComponent(query.trim())}/history`, { params: { limit } })
      setData(r.data)
      if (!r.data.trades.length && !r.data.street.length && !r.data.speed.length && !r.data.cost.length)
        setMsg('查無紀錄')
    } catch {
      setMsg('查詢失敗，請確認帳號是否正確')
    } finally { setLoading(false) }
  }

  // 解析交易物品字串 "物品名[id]*1, 物品名2[id]*2"
  const parseItems = (raw: string) => {
    if (!raw?.trim()) return []
    return raw.split(',').map(s => s.trim()).filter(Boolean).map(s => {
      const m = s.match(/^(.+?)\[.*?\]\*(\d+)$/)
      return m ? `${m[1]} ×${m[2]}` : s
    })
  }

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🔍 玩家活動歷程</h1>

      {/* 搜尋列 */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 20, alignItems: 'center', flexWrap: 'wrap' }}>
        <input value={query} onChange={e => setQuery(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && search()}
          placeholder="帳號（cdkey）或角色名稱" style={{ width: 280, fontSize: 14 }} />
        <select value={limit} onChange={e => setLimit(+e.target.value)}
          style={{ padding: '6px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text-primary)', fontSize: 13 }}>
          {[50, 100, 200, 500].map(n => <option key={n} value={n}>最多 {n} 筆</option>)}
        </select>
        <button onClick={search} disabled={loading}
          style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 20px', fontWeight: 700, fontSize: 14 }}>
          {loading ? '查詢中…' : '🔍 查詢'}
        </button>
        {msg && <span style={{ color: 'var(--accent-red)', fontSize: 13 }}>{msg}</span>}
      </div>

      {data && (
        <>
          {/* 統計摘要 */}
          <div style={{ display: 'flex', gap: 12, marginBottom: 18, flexWrap: 'wrap' }}>
            <StatCard label="交易送出" value={data.tradeSent}     color="var(--accent-orange)" />
            <StatCard label="交易收到" value={data.tradeReceived} color="var(--accent-green)"  />
            <StatCard label="街頭商店" value={data.street.length} color="var(--accent-blue)"   />
            <StatCard label="速度警告" value={data.speed.length}  color={data.speed.length > 0 ? 'var(--accent-red)' : 'var(--text-muted)'} />
            <StatCard label="消費紀錄" value={data.cost.length}   color="var(--text-secondary)" />
          </div>

          {/* Tab 切換 */}
          <div style={{ display: 'flex', gap: 4, marginBottom: 0, borderBottom: '1px solid var(--border)' }}>
            {([
              ['trade',  `💱 玩家交易（${data.trades.length}）`],
              ['street', `🏪 街頭商店（${data.street.length}）`],
              ['speed',  `⚡ 速度異常（${data.speed.length}）`],
              ['cost',   `💸 消費紀錄（${data.cost.length}）`],
            ] as [Tab, string][]).map(([t, label]) => (
              <button key={t} onClick={() => setTab(t)}
                style={{ padding: '8px 16px', fontSize: 13, fontWeight: tab === t ? 700 : 400, cursor: 'pointer',
                  background: tab === t ? 'var(--bg-card)' : 'transparent',
                  border: '1px solid var(--border)', borderBottom: tab === t ? '1px solid var(--bg-card)' : '1px solid var(--border)',
                  borderRadius: '6px 6px 0 0', marginBottom: -1,
                  color: tab === t ? 'var(--accent-blue)' : 'var(--text-muted)' }}>
                {label}
              </button>
            ))}
          </div>

          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: '0 6px 6px 6px', padding: 16 }}>
            {/* 玩家交易 */}
            {tab === 'trade' && (
              data.trades.length === 0
                ? <Empty text="無交易紀錄" />
                : <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                          <TH>時間</TH><TH>方向</TH><TH>對象</TH><TH>物品</TH><TH>寵物</TH><TH style={{ textAlign: 'right' }}>金幣</TH>
                        </tr>
                      </thead>
                      <tbody>
                        {data.trades.map((t, i) => {
                          const isSent = t.direction === 'sent'
                          const items = parseItems(t.items)
                          const pets  = parseItems(t.pets)
                          return (
                            <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                              <td style={TD}>{t.time}</td>
                              <td style={TD}>
                                <span style={{ padding: '2px 8px', borderRadius: 10, fontSize: 11, fontWeight: 700,
                                  background: isSent ? 'rgba(245,101,101,.15)' : 'rgba(86,196,118,.15)',
                                  color: isSent ? 'var(--accent-red)' : 'var(--accent-green)' }}>
                                  {isSent ? '▲ 送出' : '▼ 收到'}
                                </span>
                              </td>
                              <td style={TD}>
                                <div style={{ fontWeight: 600 }}>{isSent ? t.toName : t.fromName}</div>
                                <div style={{ fontSize: 10, color: 'var(--text-muted)' }}>{isSent ? t.toCdkey : t.fromCdkey}</div>
                              </td>
                              <td style={{ ...TD, maxWidth: 280 }}>
                                {items.length > 0
                                  ? <div style={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
                                      {items.slice(0, 5).map((it, j) => (
                                        <span key={j} style={{ background: 'var(--bg-input)', borderRadius: 4, padding: '1px 5px', fontSize: 11 }}>{it}</span>
                                      ))}
                                      {items.length > 5 && <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>+{items.length - 5} 項</span>}
                                    </div>
                                  : <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>—</span>}
                              </td>
                              <td style={TD}>
                                {pets.length > 0
                                  ? <span style={{ background: 'rgba(100,180,255,.1)', borderRadius: 4, padding: '1px 6px', fontSize: 11, color: 'var(--accent-blue)' }}>
                                      🐾 {pets.length} 隻
                                    </span>
                                  : <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>—</span>}
                              </td>
                              <td style={{ ...TD, textAlign: 'right', color: t.gold > 0 ? 'var(--accent-orange)' : 'var(--text-muted)', fontWeight: t.gold > 0 ? 700 : 400 }}>
                                {t.gold > 0 ? t.gold.toLocaleString() : '—'}
                              </td>
                            </tr>
                          )
                        })}
                      </tbody>
                    </table>
                  </div>
            )}

            {/* 街頭商店 */}
            {tab === 'street' && (
              data.street.length === 0
                ? <Empty text="無街頭商店紀錄" />
                : <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                    <thead>
                      <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                        <TH>時間</TH><TH>角色</TH><TH>物品</TH><TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>金額</TH><TH>對象</TH>
                      </tr>
                    </thead>
                    <tbody>
                      {data.street.map((s, i) => (
                        <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                          <td style={TD}>{s.time}</td>
                          <td style={TD}>
                            <span style={{ padding: '2px 8px', borderRadius: 10, fontSize: 11, fontWeight: 700,
                              background: s.role === 'seller' ? 'rgba(245,101,101,.15)' : 'rgba(86,196,118,.15)',
                              color: s.role === 'seller' ? 'var(--accent-red)' : 'var(--accent-green)' }}>
                              {s.role === 'seller' ? '賣出' : '買入'}
                            </span>
                          </td>
                          <td style={{ ...TD, fontWeight: 600 }}>{s.itemName}</td>
                          <td style={{ ...TD, textAlign: 'right' }}>×{s.num}</td>
                          <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-orange)', fontWeight: 700 }}>{s.price.toLocaleString()}</td>
                          <td style={{ ...TD, color: 'var(--text-muted)' }}>
                            {s.role === 'seller' ? s.buyName || '—' : s.sellCdkey}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
            )}

            {/* 速度異常 */}
            {tab === 'speed' && (
              data.speed.length === 0
                ? <Empty text="✅ 無速度異常紀錄" ok />
                : <>
                    <div style={{ background: 'rgba(245,101,101,.08)', border: '1px solid rgba(245,101,101,.3)', borderRadius: 8, padding: '10px 14px', marginBottom: 12, fontSize: 13, color: 'var(--accent-red)' }}>
                      ⚠ 共偵測到 {data.speed.length} 次速度異常，請注意此玩家行為
                    </div>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                          <TH>偵測時間</TH><TH>持續秒數</TH><TH>次數</TH>
                        </tr>
                      </thead>
                      <tbody>
                        {data.speed.map((s, i) => (
                          <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td style={TD}>{s.time}</td>
                            <td style={{ ...TD, color: 'var(--accent-orange)' }}>{s.speedTime} 秒</td>
                            <td style={{ ...TD, color: 'var(--accent-red)', fontWeight: 700 }}>{s.speedCnt} 次</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </>
            )}

            {/* 消費紀錄 */}
            {tab === 'cost' && (
              data.cost.length === 0
                ? <Empty text="無消費紀錄" />
                : <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                    <thead>
                      <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                        <TH>時間</TH><TH>角色名</TH><TH style={{ textAlign: 'right' }}>累計消費點數</TH>
                      </tr>
                    </thead>
                    <tbody>
                      {data.cost.map((c, i) => (
                        <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                          <td style={TD}>{c.time}</td>
                          <td style={{ ...TD, fontWeight: 600 }}>{c.name || '—'}</td>
                          <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-orange)', fontWeight: 700 }}>{c.point.toLocaleString()}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
            )}
          </div>
        </>
      )}
    </div>
  )
}

const TH = ({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) => (
  <th style={{ padding: '8px 12px', color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap', ...style }}>{children}</th>
)
const TD: React.CSSProperties = { padding: '7px 12px', whiteSpace: 'nowrap', verticalAlign: 'middle' }

const StatCard = ({ label, value, color }: { label: string; value: number; color: string }) => (
  <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, padding: '10px 18px', textAlign: 'center', minWidth: 100 }}>
    <div style={{ fontSize: 22, fontWeight: 700, color }}>{value}</div>
    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{label}</div>
  </div>
)

const Empty = ({ text, ok }: { text: string; ok?: boolean }) => (
  <div style={{ textAlign: 'center', padding: '32px 0', color: ok ? 'var(--accent-green)' : 'var(--text-muted)', fontSize: 14 }}>{text}</div>
)
