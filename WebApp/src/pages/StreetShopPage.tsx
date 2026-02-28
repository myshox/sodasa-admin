import { useState } from 'react'
import api from '../api'

type PageTab = 'vendor' | 'shopbuyer'

interface StreetItem { cdKey: string; itemId: number; itemName: string; num: number; price: number }
interface StreetSale { time: string; sellCdkey: string; itemName: string; num: number; point: number; buyCdkey: string; buyName: string }
interface VendorResult { currentItems: StreetItem[]; saleHistory: StreetSale[] }
interface ShopBuyer { time: string; cdKey: string; charName: string; itemName: string; itemNum: number; oldPoint: number; newPoint: number; shopType: 'fame' | 'vip' }

export default function StreetShopPage() {
  const [pageTab, setPageTab] = useState<PageTab>('vendor')

  // ── 攤位查詢 ──
  const [vendorQ, setVendorQ]       = useState('')
  const [vendorData, setVendorData] = useState<VendorResult | null>(null)
  const [vendorLoad, setVendorLoad] = useState(false)
  const [vendorMsg, setVendorMsg]   = useState('')
  const [vendorLimit, setVendorLimit] = useState(100)

  const searchVendor = async () => {
    if (!vendorQ.trim()) return
    setVendorLoad(true); setVendorMsg(''); setVendorData(null)
    try {
      const r = await api.get(`/street/vendor/${encodeURIComponent(vendorQ.trim())}`, { params: { limit: vendorLimit } })
      setVendorData(r.data)
      if (!r.data.currentItems.length && !r.data.saleHistory.length) setVendorMsg('查無資料')
    } catch { setVendorMsg('查詢失敗') }
    finally { setVendorLoad(false) }
  }

  // ── 商城反查 ──
  const [shopQ, setShopQ]       = useState('')
  const [shopData, setShopData] = useState<ShopBuyer[] | null>(null)
  const [shopLoad, setShopLoad] = useState(false)
  const [shopMsg, setShopMsg]   = useState('')
  const [shopLimit, setShopLimit] = useState(200)

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

  return (
    <div style={{ padding: 24 }}>
      <h1 style={{ fontSize: 20, fontWeight: 700, marginBottom: 20 }}>🏪 攤位 &amp; 商城查詢</h1>

      {/* 主 Tab */}
      <div style={{ display: 'flex', gap: 4, marginBottom: 20, borderBottom: '2px solid var(--border)' }}>
        {([
          ['vendor',    '🛖 攤位查詢'],
          ['shopbuyer', '🔍 商城反查（誰買了某物品）'],
        ] as [PageTab, string][]).map(([t, label]) => (
          <button key={t} onClick={() => setPageTab(t)} style={{
            padding: '9px 20px', fontSize: 13, fontWeight: pageTab === t ? 700 : 400,
            background: pageTab === t ? 'var(--accent-blue)' : 'transparent',
            color: pageTab === t ? '#fff' : 'var(--text-muted)',
            border: 'none', borderRadius: '6px 6px 0 0', cursor: 'pointer',
          }}>{label}</button>
        ))}
      </div>

      {/* ── 攤位查詢 ── */}
      {pageTab === 'vendor' && (
        <div>
          <div style={{ marginBottom: 16, color: 'var(--text-muted)', fontSize: 13 }}>
            輸入攤主的帳號（cdkey），查詢目前上架商品與歷史成交紀錄。
          </div>
          <div style={{ display: 'flex', gap: 8, marginBottom: 20, flexWrap: 'wrap', alignItems: 'center' }}>
            <input value={vendorQ} onChange={e => setVendorQ(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && searchVendor()}
              placeholder="攤主帳號（cdkey）" style={{ width: 260, fontSize: 14 }} />
            <select value={vendorLimit} onChange={e => setVendorLimit(+e.target.value)}
              style={{ padding: '6px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, color: 'var(--text-primary)', fontSize: 13 }}>
              {[50, 100, 200, 500].map(n => <option key={n} value={n}>最多 {n} 筆</option>)}
            </select>
            <button onClick={searchVendor} disabled={vendorLoad}
              style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 20px', fontWeight: 700, fontSize: 14 }}>
              {vendorLoad ? '查詢中…' : '🔍 查詢'}
            </button>
            {vendorMsg && <span style={{ color: 'var(--accent-red)', fontSize: 13 }}>{vendorMsg}</span>}
          </div>

          {vendorData && (
            <>
              {/* 目前上架 */}
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

              {/* 歷史成交 */}
              <Section title={`💰 歷史成交紀錄（${vendorData.saleHistory.length} 筆）`} style={{ marginTop: 20 }}>
                {vendorData.saleHistory.length === 0
                  ? <Empty text="無成交紀錄" />
                  : <div style={{ overflowX: 'auto' }}>
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                        <thead>
                          <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                            <TH>時間</TH><TH>物品</TH><TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>金額</TH><TH>買家</TH><TH>買家帳號</TH>
                          </tr>
                        </thead>
                        <tbody>
                          {vendorData.saleHistory.map((s, i) => (
                            <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                              <td style={TD}>{s.time}</td>
                              <td style={{ ...TD, fontWeight: 600 }}>{s.itemName}</td>
                              <td style={{ ...TD, textAlign: 'right' }}>×{s.num}</td>
                              <td style={{ ...TD, textAlign: 'right', color: 'var(--accent-green)', fontWeight: 700 }}>{s.point.toLocaleString()}</td>
                              <td style={TD}>{s.buyName || '—'}</td>
                              <td style={{ ...TD, color: 'var(--text-muted)', fontSize: 10 }}>{s.buyCdkey}</td>
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

      {/* ── 商城反查 ── */}
      {pageTab === 'shopbuyer' && (
        <div>
          <div style={{ marginBottom: 16, color: 'var(--text-muted)', fontSize: 13 }}>
            輸入物品名稱（支援部分關鍵字），查詢 VIP 商城 &amp; 聲望商城中有誰購買過。
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
              {/* 統計 */}
              <div style={{ display: 'flex', gap: 12, marginBottom: 16, flexWrap: 'wrap' }}>
                <StatCard label="總筆數" value={shopData.length} color="var(--accent-blue)" />
                <StatCard label="VIP 商城" value={shopData.filter(s => s.shopType === 'vip').length} color="var(--accent-blue)" />
                <StatCard label="聲望商城" value={shopData.filter(s => s.shopType === 'fame').length} color="#b97cf3" />
                <StatCard label="購買總數量" value={shopData.reduce((a, b) => a + b.itemNum, 0)} color="var(--accent-orange)" />
              </div>

              <Section title={`🛒 購買紀錄（${shopData.length} 筆）`}>
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                    <thead>
                      <tr style={{ background: 'var(--bg-sidebar)', textAlign: 'left' }}>
                        <TH>時間</TH><TH>商城</TH><TH>帳號</TH><TH>角色名</TH><TH>物品</TH><TH style={{ textAlign: 'right' }}>數量</TH><TH style={{ textAlign: 'right' }}>花費點數</TH>
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
                          <td style={{ ...TD, fontSize: 10, color: 'var(--text-muted)' }}>{s.cdKey}</td>
                          <td style={{ ...TD, fontWeight: 600 }}>{s.charName || '—'}</td>
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
  )
}

// ── 小元件 ─────────────────────────────────────────────────────
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

const Empty = ({ text }: { text: string }) => (
  <div style={{ textAlign: 'center', padding: '24px 0', color: 'var(--text-muted)', fontSize: 13 }}>{text}</div>
)

const Section = ({ title, children, style }: { title: string; children: React.ReactNode; style?: React.CSSProperties }) => (
  <div style={style}>
    <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 10, color: 'var(--text-primary)' }}>{title}</div>
    <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
      {children}
    </div>
  </div>
)
