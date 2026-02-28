import { useState, useEffect } from 'react'
import api from '../api'
import { getApiItems, getApiPets, subscribeItems, loadItemsFromApi } from '../components/ItemBrowser'
import type { ItemInfo } from '../components/ItemBrowser'

type Tab = 'listing' | 'street' | 'shop'

interface StreetListing { cdKey: string; charName: string; itemName: string; num: number; price: number }
interface StreetBuyer   { time: string; sellCdkey: string; sellerName: string; buyCdkey: string; buyName: string; itemName: string; num: number; point: number }
interface ShopBuyer     { time: string; cdKey: string; charName: string; itemName: string; itemNum: number; oldPoint: number; newPoint: number; shopType: 'fame' | 'vip' }

interface SearchResult {
  listings: StreetListing[]
  street:   StreetBuyer[]
  shop:     ShopBuyer[]
}

export default function ItemSearchPage() {
  const [items, setItems] = useState<ItemInfo[]>([...getApiItems(), ...getApiPets()])
  const [listFilter, setListFilter] = useState('')
  const [selectedItem, setSelectedItem] = useState<ItemInfo | null>(null)
  const [manualQ, setManualQ] = useState('')
  const [tab, setTab] = useState<Tab>('listing')
  const [result, setResult] = useState<SearchResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [msg, setMsg] = useState('')
  const [limit, setLimit] = useState(200)

  // 訂閱 item list 更新
  useEffect(() => {
    if (items.length === 0) loadItemsFromApi()
    return subscribeItems(() => setItems([...getApiItems(), ...getApiPets()]))
  }, [])

  const filteredItems = items.filter(i => {
    const q = listFilter.toLowerCase()
    return !q || i.name.toLowerCase().includes(q) || String(i.id).includes(q)
  })

  const doSearch = async (name: string) => {
    if (!name.trim()) return
    setLoading(true); setMsg(''); setResult(null)
    try {
      const [listRes, streetRes, shopRes] = await Promise.all([
        api.get('/street/listings', { params: { item: name.trim(), limit } }),
        api.get('/street/buyers',   { params: { item: name.trim(), limit } }),
        api.get('/shop/buyers',     { params: { item: name.trim(), limit } }),
      ])
      const r: SearchResult = { listings: listRes.data, street: streetRes.data, shop: shopRes.data }
      setResult(r)
      const total = r.listings.length + r.street.length + r.shop.length
      if (total === 0) setMsg('查無任何紀錄')
    } catch { setMsg('查詢失敗') }
    finally { setLoading(false) }
  }

  const clickItem = (item: ItemInfo) => {
    setSelectedItem(item)
    setManualQ(item.name)
    doSearch(item.name)
  }

  const handleManualSearch = () => {
    setSelectedItem(null)
    doSearch(manualQ)
  }

  const tabs: { key: Tab; label: string; count?: number }[] = result ? [
    { key: 'listing', label: `📦 目前上架（${result.listings.length}）` },
    { key: 'street',  label: `🛖 攤位成交（${result.street.length}）` },
    { key: 'shop',    label: `🏬 商城購買（${result.shop.length}）` },
  ] : [
    { key: 'listing', label: '📦 目前上架' },
    { key: 'street',  label: '🛖 攤位成交' },
    { key: 'shop',    label: '🏬 商城購買' },
  ]

  return (
    <div style={{ display: 'flex', height: '100%' }}>
      {/* ── 左側物品清單 ── */}
      <div style={{ width: 210, minWidth: 190, borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', background: 'var(--bg-sidebar)', height: '100vh', position: 'sticky', top: 0 }}>
        <div style={{ padding: '14px 12px 8px', borderBottom: '1px solid var(--border)', flexShrink: 0 }}>
          <div style={{ fontSize: 11, fontWeight: 700, color: 'var(--text-muted)', marginBottom: 6 }}>
            物品清單（{filteredItems.length}/{items.length}）
          </div>
          <input value={listFilter} onChange={e => setListFilter(e.target.value)}
            placeholder="搜尋名稱或編號…" style={{ width: '100%', fontSize: 12, padding: '5px 8px', boxSizing: 'border-box' }} />
        </div>
        <div style={{ flex: 1, overflowY: 'auto' }}>
          {items.length === 0
            ? <div style={{ padding: 16, fontSize: 12, color: 'var(--text-muted)', textAlign: 'center' }}>
                請先至「批量操作」頁面<br />上傳 items.xlsx 建立清單
              </div>
            : filteredItems.slice(0, 500).map(item => {
                const isSel = selectedItem?.id === item.id && selectedItem?.isPet === item.isPet
                return (
                  <div key={`${item.id}-${item.isPet}`} onClick={() => clickItem(item)}
                    style={{ padding: '7px 12px', cursor: 'pointer', fontSize: 12, borderBottom: '1px solid var(--border)', background: isSel ? 'rgba(99,179,237,.15)' : 'transparent', borderLeft: isSel ? '3px solid var(--accent-blue)' : '3px solid transparent' }}
                    onMouseEnter={e => { if (!isSel) (e.currentTarget as HTMLElement).style.background = 'rgba(255,255,255,.04)' }}
                    onMouseLeave={e => { if (!isSel) (e.currentTarget as HTMLElement).style.background = 'transparent' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span style={{ fontSize: 10 }}>{item.isPet ? '🐾' : '📦'}</span>
                      <span style={{ fontWeight: 600, flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{item.name}</span>
                    </div>
                    <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 1 }}>#{item.id}</div>
                  </div>
                )
              })
          }
        </div>
      </div>

      {/* ── 右側查詢區 ── */}
      <div style={{ flex: 1, padding: 24, overflowY: 'auto' }}>
        <h1 style={{ fontSize: 20, fontWeight: 700, marginBottom: 6 }}>🔎 物品查詢</h1>
        <p style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 20 }}>
          輸入物品名稱關鍵字，或從左側點選，自動查詢：目前誰在賣、誰買過、誰透過商城購買
        </p>

        {/* 搜尋列 */}
        <div style={{ display: 'flex', gap: 8, marginBottom: 20, flexWrap: 'wrap', alignItems: 'center' }}>
          <input value={manualQ} onChange={e => setManualQ(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleManualSearch()}
            placeholder="輸入物品名稱關鍵字（可手動輸入）"
            style={{ flex: 1, minWidth: 220, fontSize: 14 }} />
          <select value={limit} onChange={e => setLimit(+e.target.value)}
            style={{ padding: '6px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text-primary)', fontSize: 13 }}>
            {[100, 200, 300, 500].map(n => <option key={n} value={n}>最多 {n} 筆/類</option>)}
          </select>
          <button onClick={handleManualSearch} disabled={loading}
            style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 22px', fontWeight: 700, fontSize: 14, borderRadius: 6 }}>
            {loading ? '查詢中…' : '🔍 查詢'}
          </button>
          {msg && <span style={{ color: 'var(--accent-red)', fontSize: 13 }}>{msg}</span>}
        </div>

        {/* 統計卡 */}
        {result && (
          <div style={{ display: 'flex', gap: 12, marginBottom: 20, flexWrap: 'wrap' }}>
            <StatCard label="目前上架攤位數" value={new Set(result.listings.map(l => l.cdKey)).size} color="var(--accent-blue)" />
            <StatCard label="目前上架件數" value={result.listings.reduce((a, b) => a + b.num, 0)} color="var(--accent-blue)" />
            <StatCard label="攤位成交筆數" value={result.street.length} color="var(--accent-orange)" />
            <StatCard label="攤位成交金幣" value={result.street.reduce((a, b) => a + b.point, 0)} color="var(--accent-green)" large />
            <StatCard label="商城購買筆數" value={result.shop.length} color="#b97cf3" />
          </div>
        )}

        {/* Tab 列 */}
        {result && (
          <div style={{ display: 'flex', gap: 4, marginBottom: 0, borderBottom: '2px solid var(--border)', flexWrap: 'wrap' }}>
            {tabs.map(t => (
              <button key={t.key} onClick={() => setTab(t.key)} style={{ padding: '9px 18px', fontSize: 13, fontWeight: tab === t.key ? 700 : 400, background: tab === t.key ? 'var(--accent-blue)' : 'transparent', color: tab === t.key ? '#fff' : 'var(--text-muted)', border: 'none', borderRadius: '6px 6px 0 0', cursor: 'pointer' }}>{t.label}</button>
            ))}
          </div>
        )}

        {/* Tab 內容 */}
        {result && (
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderTop: 'none', borderRadius: '0 0 8px 8px', overflow: 'hidden' }}>

            {/* 目前上架 */}
            {tab === 'listing' && (
              result.listings.length === 0
                ? <Empty text="目前無人上架此物品" />
                : <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                          <TH>攤主（角色名）</TH><TH>帳號</TH><TH>物品</TH>
                          <TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>單價（金幣）</TH>
                        </tr>
                      </thead>
                      <tbody>
                        {result.listings.map((r, i) => (
                          <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td style={TD}><span style={{ fontWeight: 600, color: 'var(--accent-blue)' }}>{r.charName || '—'}</span></td>
                            <td style={{ ...TD, fontSize: 11, color: 'var(--text-muted)', fontFamily: 'monospace' }}>{r.cdKey}</td>
                            <td style={TD}>{r.itemName}</td>
                            <td style={{ ...TD, textAlign: 'right' }}>×{r.num}</td>
                            <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-orange)', fontWeight: 700 }}>{r.price.toLocaleString()}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
            )}

            {/* 攤位成交 */}
            {tab === 'street' && (
              result.street.length === 0
                ? <Empty text="無攤位成交紀錄" />
                : <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                          <TH>時間</TH><TH>物品</TH>
                          <TH>賣家（攤名）</TH><TH>賣家帳號</TH>
                          <TH>買家角色名</TH><TH>買家帳號</TH>
                          <TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>金幣</TH>
                        </tr>
                      </thead>
                      <tbody>
                        {result.street.map((s, i) => (
                          <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td style={TD}>{s.time}</td>
                            <td style={{ ...TD, fontWeight: 600 }}>{s.itemName}</td>
                            <td style={TD}><span style={{ color: 'var(--accent-orange)', fontWeight: 600 }}>{s.sellerName || '—'}</span></td>
                            <td style={{ ...TD, fontSize: 11, color: 'var(--text-muted)', fontFamily: 'monospace' }}>{s.sellCdkey}</td>
                            <td style={TD}><span style={{ color: 'var(--accent-blue)' }}>{s.buyName || '—'}</span></td>
                            <td style={{ ...TD, fontSize: 11, color: 'var(--text-muted)', fontFamily: 'monospace' }}>{s.buyCdkey || '—'}</td>
                            <td style={{ ...TD, textAlign: 'right' }}>×{s.num}</td>
                            <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-green)', fontWeight: 700 }}>{s.point.toLocaleString()}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
            )}

            {/* 商城購買 */}
            {tab === 'shop' && (
              result.shop.length === 0
                ? <Empty text="無商城購買紀錄" />
                : <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                          <TH>時間</TH><TH>商城</TH><TH>角色名</TH><TH>帳號</TH>
                          <TH>物品</TH><TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>花費點數</TH>
                        </tr>
                      </thead>
                      <tbody>
                        {result.shop.map((s, i) => (
                          <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td style={TD}>{s.time}</td>
                            <td style={TD}>
                              <span style={{ padding: '2px 8px', borderRadius: 10, fontSize: 11, fontWeight: 700, background: s.shopType === 'vip' ? 'rgba(100,180,255,.15)' : 'rgba(185,124,243,.15)', color: s.shopType === 'vip' ? 'var(--accent-blue)' : '#b97cf3' }}>
                                {s.shopType === 'vip' ? '💎 VIP' : '⭐ 聲望'}
                              </span>
                            </td>
                            <td style={{ ...TD, fontWeight: 600, color: 'var(--accent-blue)' }}>{s.charName || '—'}</td>
                            <td style={{ ...TD, fontSize: 11, color: 'var(--text-muted)', fontFamily: 'monospace' }}>{s.cdKey}</td>
                            <td style={TD}>{s.itemName}</td>
                            <td style={{ ...TD, textAlign: 'right' }}>×{s.itemNum}</td>
                            <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-red)', fontWeight: 700 }}>
                              -{(s.oldPoint - s.newPoint).toLocaleString()}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
            )}
          </div>
        )}

        {!result && !loading && (
          <div style={{ textAlign: 'center', padding: '60px 0', color: 'var(--text-muted)', fontSize: 14 }}>
            ← 從左側點選物品，或上方輸入物品名稱查詢
          </div>
        )}
      </div>
    </div>
  )
}

const TH = ({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) => (
  <th style={{ padding: '8px 12px', color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap', ...style }}>{children}</th>
)
const TD: React.CSSProperties = { padding: '7px 12px', whiteSpace: 'nowrap', verticalAlign: 'middle' }
const StatCard = ({ label, value, color, large }: { label: string; value: number; color: string; large?: boolean }) => (
  <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, padding: '10px 16px', textAlign: 'center', minWidth: 90 }}>
    <div style={{ fontSize: large ? 15 : 22, fontWeight: 700, color }}>{large ? value.toLocaleString() : value}</div>
    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{label}</div>
  </div>
)
const Empty = ({ text }: { text: string }) => (
  <div style={{ textAlign: 'center', padding: '32px 0', color: 'var(--text-muted)', fontSize: 13 }}>{text}</div>
)
