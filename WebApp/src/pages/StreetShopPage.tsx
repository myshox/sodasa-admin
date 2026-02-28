import { useState, useEffect } from 'react'
import api from '../api'

type PageTab = 'vendor' | 'streetbuyer' | 'shopbuyer'

interface VendorSummary { cdKey: string; charName: string; itemCount: number }
interface StreetItem   { cdKey: string; itemId: number; itemName: string; num: number; price: number }
interface StreetSale   { time: string; sellCdkey: string; itemName: string; num: number; point: number; buyCdkey: string; buyName: string }
interface VendorResult { cdKey: string; charName: string; currentItems: StreetItem[]; saleHistory: StreetSale[] }
interface StreetBuyer  { time: string; sellCdkey: string; sellerName: string; buyCdkey: string; buyName: string; itemName: string; num: number; point: number }
interface ShopBuyer    { time: string; cdKey: string; charName: string; itemName: string; itemNum: number; oldPoint: number; newPoint: number; shopType: 'fame' | 'vip' }

export default function StreetShopPage() {
  const [pageTab, setPageTab] = useState<PageTab>('vendor')

  // ── 攤主清單 ──
  const [vendors, setVendors]               = useState<VendorSummary[]>([])
  const [vendorListLoad, setVendorListLoad] = useState(true)
  const [listFilter, setListFilter]         = useState('')
  const [selectedCdkey, setSelectedCdkey]   = useState<string | null>(null)

  // ── 攤位查詢 ──
  const [vendorQ, setVendorQ]         = useState('')
  const [vendorData, setVendorData]   = useState<VendorResult | null>(null)
  const [vendorLoad, setVendorLoad]   = useState(false)
  const [vendorMsg, setVendorMsg]     = useState('')
  const [vendorLimit, setVendorLimit] = useState(100)

  // ── 攤位反查 ──
  const [sbQ, setSbQ]         = useState('')
  const [sbData, setSbData]   = useState<StreetBuyer[] | null>(null)
  const [sbLoad, setSbLoad]   = useState(false)
  const [sbMsg, setSbMsg]     = useState('')
  const [sbLimit, setSbLimit] = useState(300)

  // ── 商城反查 ──
  const [shopQ, setShopQ]         = useState('')
  const [shopData, setShopData]   = useState<ShopBuyer[] | null>(null)
  const [shopLoad, setShopLoad]   = useState(false)
  const [shopMsg, setShopMsg]     = useState('')
  const [shopLimit, setShopLimit] = useState(200)

  // 自動載入攤主清單
  useEffect(() => {
    ;(async () => {
      setVendorListLoad(true)
      try { const r = await api.get('/street/vendors'); setVendors(r.data) }
      catch { /* ignore */ }
      finally { setVendorListLoad(false) }
    })()
  }, [])

  const filteredVendors = vendors.filter(v => {
    const q = listFilter.toLowerCase()
    return !q || v.charName.toLowerCase().includes(q) || v.cdKey.toLowerCase().includes(q)
  })

  const searchVendor = async (query: string) => {
    if (!query.trim()) return
    setVendorLoad(true); setVendorMsg(''); setVendorData(null)
    setSelectedCdkey(query.trim()); setPageTab('vendor')
    try {
      const r = await api.get(`/street/vendor/${encodeURIComponent(query.trim())}`, { params: { limit: vendorLimit } })
      setVendorData(r.data)
      if (!r.data.currentItems.length && !r.data.saleHistory.length) setVendorMsg('查無資料')
    } catch { setVendorMsg('查詢失敗') }
    finally { setVendorLoad(false) }
  }

  const clickVendor = (v: VendorSummary) => {
    setVendorQ(v.charName || v.cdKey)
    setSelectedCdkey(v.cdKey)
    searchVendor(v.cdKey)
  }

  const searchStreetBuyers = async () => {
    if (!sbQ.trim()) return
    setSbLoad(true); setSbMsg(''); setSbData(null)
    try {
      const r = await api.get('/street/buyers', { params: { item: sbQ.trim(), limit: sbLimit } })
      setSbData(r.data)
      if (!r.data.length) setSbMsg('查無紀錄')
    } catch { setSbMsg('查詢失敗') }
    finally { setSbLoad(false) }
  }

  const searchShop = async () => {
    if (!shopQ.trim()) return
    setShopLoad(true); setShopMsg(''); setShopData(null)
    try {
      const r = await api.get('/shop/buyers', { params: { item: shopQ.trim(), limit: shopLimit } })
      setShopData(r.data)
      if (!r.data.length) setShopMsg('查無購買紀錄')
    } catch { setShopMsg('查詢失敗') }
    finally { setShopLoad(false) }
  }

  const tabs: { key: PageTab; label: string }[] = [
    { key: 'vendor',      label: '🛖 攤位查詢' },
    { key: 'streetbuyer', label: '🔍 攤位反查（物品→買賣紀錄）' },
    { key: 'shopbuyer',   label: '🏬 商城反查（物品→誰購買）' },
  ]

  return (
    <div style={{ display: 'flex', height: '100%' }}>
      {/* ── 左側攤主清單 ── */}
      <div style={{
        width: 200, minWidth: 180, borderRight: '1px solid var(--border)',
        display: 'flex', flexDirection: 'column', background: 'var(--bg-sidebar)',
        height: '100vh', position: 'sticky', top: 0
      }}>
        <div style={{ padding: '14px 12px 8px', borderBottom: '1px solid var(--border)', flexShrink: 0 }}>
          <div style={{ fontSize: 12, fontWeight: 700, color: 'var(--text-muted)', marginBottom: 6 }}>
            目前攤主（{filteredVendors.length}）
          </div>
          <input value={listFilter} onChange={e => setListFilter(e.target.value)}
            placeholder="搜尋角色名..." style={{ width: '100%', fontSize: 12, padding: '5px 8px', boxSizing: 'border-box' }} />
        </div>
        <div style={{ flex: 1, overflowY: 'auto' }}>
          {vendorListLoad
            ? <div style={{ padding: 16, fontSize: 12, color: 'var(--text-muted)', textAlign: 'center' }}>載入中…</div>
            : filteredVendors.length === 0
              ? <div style={{ padding: 16, fontSize: 12, color: 'var(--text-muted)', textAlign: 'center' }}>無攤位</div>
              : filteredVendors.map(v => {
                  const isSel = selectedCdkey === v.cdKey
                  return (
                    <div key={v.cdKey} onClick={() => clickVendor(v)} style={{
                      padding: '8px 12px', cursor: 'pointer', fontSize: 12,
                      borderBottom: '1px solid var(--border)',
                      background: isSel ? 'rgba(99,179,237,.15)' : 'transparent',
                      borderLeft: isSel ? '3px solid var(--accent-blue)' : '3px solid transparent',
                    }}
                    onMouseEnter={e => { if (!isSel) (e.currentTarget as HTMLElement).style.background = 'rgba(255,255,255,.04)' }}
                    onMouseLeave={e => { if (!isSel) (e.currentTarget as HTMLElement).style.background = 'transparent' }}>
                      <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>{v.charName || '未知角色'}</div>
                      <div style={{ fontSize: 10, color: 'var(--text-muted)', marginTop: 1 }}>
                        {v.itemCount} 件 · {v.cdKey.slice(0, 8)}…
                      </div>
                    </div>
                  )
                })
          }
        </div>
      </div>

      {/* ── 右側內容 ── */}
      <div style={{ flex: 1, padding: 24, overflowY: 'auto' }}>
        <h1 style={{ fontSize: 20, fontWeight: 700, marginBottom: 16 }}>🏪 攤位 &amp; 商城查詢</h1>

        {/* 主 Tab */}
        <div style={{ display: 'flex', gap: 4, marginBottom: 20, borderBottom: '2px solid var(--border)', flexWrap: 'wrap' }}>
          {tabs.map(t => (
            <button key={t.key} onClick={() => setPageTab(t.key)} style={{
              padding: '9px 18px', fontSize: 13, fontWeight: pageTab === t.key ? 700 : 400,
              background: pageTab === t.key ? 'var(--accent-blue)' : 'transparent',
              color: pageTab === t.key ? '#fff' : 'var(--text-muted)',
              border: 'none', borderRadius: '6px 6px 0 0', cursor: 'pointer',
            }}>{t.label}</button>
          ))}
        </div>

        {/* ══ Tab 1：攤位查詢 ══ */}
        {pageTab === 'vendor' && (
          <div>
            <div style={{ display: 'flex', gap: 8, marginBottom: 20, flexWrap: 'wrap', alignItems: 'center' }}>
              <input value={vendorQ} onChange={e => setVendorQ(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && searchVendor(vendorQ)}
                placeholder="角色名 或 帳號（cdkey）" style={{ width: 260, fontSize: 14 }} />
              <select value={vendorLimit} onChange={e => setVendorLimit(+e.target.value)}
                style={{ padding: '6px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text-primary)', fontSize: 13 }}>
                {[50, 100, 200, 500].map(n => <option key={n} value={n}>最多 {n} 筆</option>)}
              </select>
              <button onClick={() => searchVendor(vendorQ)} disabled={vendorLoad}
                style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 20px', fontWeight: 700, fontSize: 14 }}>
                {vendorLoad ? '查詢中…' : '🔍 查詢'}
              </button>
              {vendorMsg && <span style={{ color: 'var(--accent-red)', fontSize: 13 }}>{vendorMsg}</span>}
            </div>

            {!vendorData && !vendorLoad && (
              <div style={{ color: 'var(--text-muted)', fontSize: 14, textAlign: 'center', marginTop: 40 }}>
                ← 從左側點選攤主，或輸入角色名查詢
              </div>
            )}
            {vendorLoad && <div style={{ color: 'var(--text-muted)', fontSize: 14, textAlign: 'center', marginTop: 40 }}>查詢中…</div>}

            {vendorData && (
              <>
                {/* 攤主資訊卡 */}
                <div style={{ marginBottom: 16, padding: '12px 16px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, fontSize: 13, display: 'flex', gap: 24, flexWrap: 'wrap', alignItems: 'center' }}>
                  <div>
                    <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>攤名（角色名）</span>
                    <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--accent-blue)', marginTop: 2 }}>
                      {vendorData.charName || '—'}
                    </div>
                  </div>
                  <div>
                    <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>帳號（cdkey）</span>
                    <div style={{ fontFamily: 'monospace', marginTop: 2, color: 'var(--text-secondary)' }}>{vendorData.cdKey}</div>
                  </div>
                  <div>
                    <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>目前上架</span>
                    <div style={{ fontWeight: 700, marginTop: 2 }}>{vendorData.currentItems.length} 件</div>
                  </div>
                  <div>
                    <span style={{ color: 'var(--text-muted)', fontSize: 11 }}>歷史成交</span>
                    <div style={{ fontWeight: 700, color: 'var(--accent-green)', marginTop: 2 }}>{vendorData.saleHistory.length} 筆</div>
                  </div>
                </div>

                <Section title={`📦 目前上架商品（${vendorData.currentItems.length} 件）`}>
                  {vendorData.currentItems.length === 0
                    ? <Empty text="目前沒有上架商品" />
                    : <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                        <thead>
                          <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                            <TH>物品名稱</TH><TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>單價（金幣）</TH>
                          </tr>
                        </thead>
                        <tbody>
                          {vendorData.currentItems.map((item, i) => (
                            <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                              <td style={TD}><span style={{ fontWeight: 600 }}>{item.itemName || `ID:${item.itemId}`}</span></td>
                              <td style={{ ...TD, textAlign: 'right' }}>×{item.num}</td>
                              <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-orange)', fontWeight: 700 }}>{item.price.toLocaleString()}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                  }
                </Section>

                <Section title={`💰 歷史成交紀錄（${vendorData.saleHistory.length} 筆）`} style={{ marginTop: 20 }}>
                  {vendorData.saleHistory.length === 0
                    ? <Empty text="無成交紀錄" />
                    : <div style={{ overflowX: 'auto' }}>
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                          <thead>
                            <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                              <TH>時間</TH><TH>物品</TH><TH style={{ textAlign: 'right' }}>數量</TH>
                              <TH style={{ textAlign: 'right' }}>成交金幣</TH><TH>買家角色名</TH><TH>買家帳號</TH>
                            </tr>
                          </thead>
                          <tbody>
                            {vendorData.saleHistory.map((s, i) => (
                              <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                                <td style={TD}>{s.time}</td>
                                <td style={{ ...TD, fontWeight: 600 }}>{s.itemName}</td>
                                <td style={{ ...TD, textAlign: 'right' }}>×{s.num}</td>
                                <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-green)', fontWeight: 700 }}>{s.point.toLocaleString()}</td>
                                <td style={TD}><span style={{ color: 'var(--accent-blue)' }}>{s.buyName || '—'}</span></td>
                                <td style={{ ...TD, fontSize: 11, color: 'var(--text-muted)', fontFamily: 'monospace' }}>{s.buyCdkey || '—'}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                  }
                </Section>
              </>
            )}
          </div>
        )}

        {/* ══ Tab 2：攤位反查 ══ */}
        {pageTab === 'streetbuyer' && (
          <div>
            <div style={{ marginBottom: 12, color: 'var(--text-muted)', fontSize: 13 }}>
              輸入物品名稱關鍵字，查詢攤位市場中所有交易紀錄（賣家 &amp; 買家）。
            </div>
            <div style={{ display: 'flex', gap: 8, marginBottom: 20, flexWrap: 'wrap', alignItems: 'center' }}>
              <input value={sbQ} onChange={e => setSbQ(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && searchStreetBuyers()}
                placeholder="物品名稱關鍵字（例：寶石、藥水）" style={{ width: 300, fontSize: 14 }} />
              <select value={sbLimit} onChange={e => setSbLimit(+e.target.value)}
                style={{ padding: '6px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text-primary)', fontSize: 13 }}>
                {[100, 200, 300, 500].map(n => <option key={n} value={n}>最多 {n} 筆</option>)}
              </select>
              <button onClick={searchStreetBuyers} disabled={sbLoad}
                style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 20px', fontWeight: 700, fontSize: 14 }}>
                {sbLoad ? '查詢中…' : '🔍 查詢'}
              </button>
              {sbMsg && <span style={{ color: 'var(--accent-red)', fontSize: 13 }}>{sbMsg}</span>}
            </div>

            {sbData && sbData.length > 0 && (
              <>
                <div style={{ display: 'flex', gap: 12, marginBottom: 16, flexWrap: 'wrap' }}>
                  <StatCard label="交易筆數" value={sbData.length} color="var(--accent-blue)" />
                  <StatCard label="總成交數量" value={sbData.reduce((a, b) => a + b.num, 0)} color="var(--accent-orange)" />
                  <StatCard label="總成交金幣" value={sbData.reduce((a, b) => a + b.point, 0)} color="var(--accent-green)" large />
                </div>
                <Section title={`🛖 攤位交易紀錄（${sbData.length} 筆）`}>
                  <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                          <TH>時間</TH><TH>物品</TH>
                          <TH>賣家（攤名）</TH><TH>賣家帳號</TH>
                          <TH>買家角色名</TH><TH>買家帳號</TH>
                          <TH style={{ textAlign: 'right' }}>數量</TH>
                          <TH style={{ textAlign: 'right' }}>金幣</TH>
                        </tr>
                      </thead>
                      <tbody>
                        {sbData.map((s, i) => (
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
                </Section>
              </>
            )}
          </div>
        )}

        {/* ══ Tab 3：商城反查 ══ */}
        {pageTab === 'shopbuyer' && (
          <div>
            <div style={{ marginBottom: 12, color: 'var(--text-muted)', fontSize: 13 }}>
              輸入物品名稱關鍵字，查詢 VIP 商城 &amp; 聲望商城中有誰購買過。
            </div>
            <div style={{ display: 'flex', gap: 8, marginBottom: 20, flexWrap: 'wrap', alignItems: 'center' }}>
              <input value={shopQ} onChange={e => setShopQ(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && searchShop()}
                placeholder="物品名稱關鍵字（例：寶石、藥水）" style={{ width: 300, fontSize: 14 }} />
              <select value={shopLimit} onChange={e => setShopLimit(+e.target.value)}
                style={{ padding: '6px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text-primary)', fontSize: 13 }}>
                {[50, 100, 200, 500].map(n => <option key={n} value={n}>最多 {n} 筆</option>)}
              </select>
              <button onClick={searchShop} disabled={shopLoad}
                style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 20px', fontWeight: 700, fontSize: 14 }}>
                {shopLoad ? '查詢中…' : '🔍 查詢'}
              </button>
              {shopMsg && <span style={{ color: 'var(--accent-red)', fontSize: 13 }}>{shopMsg}</span>}
            </div>

            {shopData && shopData.length > 0 && (
              <>
                <div style={{ display: 'flex', gap: 12, marginBottom: 16, flexWrap: 'wrap' }}>
                  <StatCard label="總筆數" value={shopData.length} color="var(--accent-blue)" />
                  <StatCard label="💎 VIP 商城" value={shopData.filter(s => s.shopType === 'vip').length} color="var(--accent-blue)" />
                  <StatCard label="⭐ 聲望商城" value={shopData.filter(s => s.shopType === 'fame').length} color="#b97cf3" />
                  <StatCard label="購買總數量" value={shopData.reduce((a, b) => a + b.itemNum, 0)} color="var(--accent-orange)" />
                </div>
                <Section title={`🛒 商城購買紀錄（${shopData.length} 筆）`}>
                  <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                          <TH>時間</TH><TH>商城</TH><TH>角色名</TH><TH>帳號</TH>
                          <TH>物品</TH><TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>花費點數</TH>
                        </tr>
                      </thead>
                      <tbody>
                        {shopData.map((s, i) => (
                          <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td style={TD}>{s.time}</td>
                            <td style={TD}>
                              <span style={{ padding: '2px 8px', borderRadius: 10, fontSize: 11, fontWeight: 700,
                                background: s.shopType === 'vip' ? 'rgba(100,180,255,.15)' : 'rgba(185,124,243,.15)',
                                color: s.shopType === 'vip' ? 'var(--accent-blue)' : '#b97cf3' }}>
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
                </Section>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

// ── 共用元件 ─────────────────────────────────────────────────
const TH = ({ children, style }: { children: React.ReactNode; style?: React.CSSProperties }) => (
  <th style={{ padding: '8px 12px', color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap', ...style }}>{children}</th>
)
const TD: React.CSSProperties = { padding: '7px 12px', whiteSpace: 'nowrap', verticalAlign: 'middle' }
const StatCard = ({ label, value, color, large }: { label: string; value: number; color: string; large?: boolean }) => (
  <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, padding: '10px 18px', textAlign: 'center', minWidth: 100 }}>
    <div style={{ fontSize: large ? 16 : 22, fontWeight: 700, color }}>{large ? value.toLocaleString() : value}</div>
    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{label}</div>
  </div>
)
const Empty = ({ text }: { text: string }) => (
  <div style={{ textAlign: 'center', padding: '24px 0', color: 'var(--text-muted)', fontSize: 13 }}>{text}</div>
)
const Section = ({ title, children, style }: { title: string; children: React.ReactNode; style?: React.CSSProperties }) => (
  <div style={style}>
    <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 10, color: 'var(--text-primary)' }}>{title}</div>
    <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>{children}</div>
  </div>
)
