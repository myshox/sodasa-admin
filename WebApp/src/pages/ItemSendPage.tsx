import { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'
import type { MailHistoryItem, PlayerRow } from '../api'
import { S } from '../strings'
import ItemBrowser from '../components/ItemBrowser'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import ItemAutocomplete from '../components/ItemAutocomplete'
import type { ItemInfo } from '../components/ItemBrowser'
import useIsMobile from '../hooks/useIsMobile'

interface CartItem { itemId: number; qty: number; type: number; name?: string; buff3?: string }

export default function ItemSendPage() {
  const isMobile = useIsMobile()
  const [sp] = useSearchParams()
  const [playerQ, setPlayerQ] = useState(sp.get('account') || '')
  // 只需要 account + onlineName + isOnline，其餘欄位填預設
  const makePlayer = (acc: string, name: string): PlayerRow => ({
    account: acc, onlineName: name, isOnline: false,
    serverId: 0, regTime: '', loginTime: '', ip: '',
    isBanned: false, gold: 0, crystal: 0, petCount: 0,
    payTotal: 0, masterName: '', vipLevel: 0
  })

  const [selectedAccounts, setSelectedAccounts] = useState<PlayerRow[]>(
    sp.get('account') ? [makePlayer(sp.get('account')!, decodeURIComponent(sp.get('name') || sp.get('account') || ''))] : []
  )
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState('')

  // 手動輸入道具
  const [manualId, setManualId] = useState('')
  const [manualQty, setManualQty] = useState(1)
  const [manualType, setManualType] = useState(1)

  // 購物車
  const [cart, setCart] = useState<CartItem[]>([])

  // 郵件設定
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')

  // 郵件歷史
  const [mailHistory, setMailHistory] = useState<MailHistoryItem[]>([])
  const [showHistory, setShowHistory] = useState(false)
  const [historyLoading, setHistoryLoading] = useState(false)

  useEffect(() => {
    const acc = sp.get('account')
    if (acc) {
      const name = decodeURIComponent(sp.get('name') || acc)
      setSelectedAccounts([makePlayer(acc, name)])
      setPlayerQ(name)
    }
  }, [sp])

  const addPlayers = (players: PlayerRow[]) => {
    setSelectedAccounts(prev => {
      const existing = new Set(prev.map(p => p.account))
      return [...prev, ...players.filter(p => !existing.has(p.account))]
    })
    setPlayerQ('')
  }

  const removePlayer = (account: string) =>
    setSelectedAccounts(prev => prev.filter(p => p.account !== account))

  const loadHistory = async () => {
    const acc = selectedAccounts[0]?.account
    if (!acc) return
    setHistoryLoading(true)
    try {
      const r = await api.get(`/players/${acc}/mail-history`)
      setMailHistory(r.data); setShowHistory(true)
    } finally { setHistoryLoading(false) }
  }

  // 從 ItemBrowser 或手動加入購物車
  const addToCart = (item: CartItem) => {
    setCart(prev => {
      const existing = prev.find(c => c.itemId === item.itemId && c.type === item.type)
      if (existing) return prev.map(c => c.itemId === item.itemId && c.type === item.type ? { ...c, qty: c.qty + item.qty } : c)
      return [...prev, item]
    })
  }

  const addManualToCart = () => {
    const id = parseInt(manualId, 10)
    if (!id || id <= 0) { setResult('請輸入有效道具編號'); return }
    addToCart({ itemId: id, qty: manualQty, type: manualType })
    setManualId('')
  }

  const addFromAutocomplete = (item: ItemInfo) => {
    addToCart({ itemId: item.id, qty: 1, type: 1, name: item.name, buff3: item.name })
  }

  const removeFromCart = (idx: number) => setCart(cart.filter((_, i) => i !== idx))
  const updateCartQty = (idx: number, qty: number) =>
    setCart(cart.map((c, i) => i === idx ? { ...c, qty: Math.max(1, qty) } : c))

  const send = async () => {
    if (selectedAccounts.length === 0) { setResult('請先選定玩家'); return }
    if (cart.length === 0) { setResult('購物車為空，請加入道具'); return }
    setLoading(true); setResult('')
    try {
      const cartPayload = cart.map(c => ({ itemId: c.itemId, qty: c.qty, type: c.type, buff3: c.buff3 ?? '' }))
      let successCount = 0
      for (const p of selectedAccounts) {
        await api.post('/players/send-cart', {
          account: p.account,
          cart: cartPayload,
          title: title.trim(), content: content.trim(),
        })
        successCount++
      }
      setResult(`✓ 已發送給 ${successCount} 位玩家`)
      setCart([])
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setResult(err.response?.data?.message || '發送失敗')
    } finally { setLoading(false) }
  }

  return (
    <div className="gm-page-inner">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 16 }}>📬 {S.navItemQueue}</h1>

      {result && (
        <div style={{
          background: result.includes('失敗') || result.includes('請') ? 'rgba(245,101,101,.1)' : 'rgba(86,196,118,.15)',
          border: `1px solid ${result.includes('失敗') || result.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)'}`,
          borderRadius: 8, padding: '10px 16px', marginBottom: 16,
          color: result.includes('失敗') || result.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)', fontSize: 13
        }}>{result}</div>
      )}

      <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start', flexWrap: isMobile ? 'wrap' : 'nowrap' }}>
        {/* 左側：道具清單瀏覽器 */}
        <div style={{ width: isMobile ? '100%' : 380, flexShrink: 0, minWidth: 0 }}>
          <ItemBrowser cart={cart} onAddToCart={addToCart} />
        </div>

        {/* 中間：玩家 + 手動輸入 + 郵件設定 */}
        <div style={{ flex: 1, minWidth: 0, width: isMobile ? '100%' : undefined }}>
          {/* 指定玩家 */}
          <Card title="STEP 1 — 指定收件人">
            <PlayerAutocomplete
              value={playerQ}
              onChange={setPlayerQ}
              onSelect={p => { addPlayers([p]); setPlayerQ('') }}
              onSelectMulti={addPlayers}
              placeholder="主帳號 / 角色名 / UID（主帳號可複選全部子帳號）"
            />
            {selectedAccounts.length > 0 && (
              <div style={{ marginTop: 8 }}>
                <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>
                  收件人 ({selectedAccounts.length})：
                  <button onClick={() => setSelectedAccounts([])}
                    style={{ fontSize: 11, color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer', marginLeft: 8 }}>
                    全部清除
                  </button>
                </div>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {selectedAccounts.map(p => (
                    <span key={p.account} style={{
                      display: 'inline-flex', alignItems: 'center', gap: 4,
                      background: 'rgba(74,158,255,.15)', border: '1px solid rgba(74,158,255,.35)',
                      borderRadius: 20, padding: '3px 10px', fontSize: 12, color: 'var(--accent-blue)'
                    }}>
                      {p.isOnline ? '🟢' : '⚫'} {p.onlineName || p.account}
                      <button onClick={() => removePlayer(p.account)}
                        style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--text-muted)', fontSize: 13, lineHeight: 1, padding: 0 }}>
                        ✕
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            )}
            {selectedAccounts.length === 1 && (
              <div style={{ marginTop: 6 }}>
                <button onClick={loadHistory} disabled={historyLoading}
                  style={{ fontSize: 12, padding: '3px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 4 }}>
                  {historyLoading ? '載入中…' : '📜 郵件歷史'}
                </button>
              </div>
            )}
          </Card>

          {/* 道具/寵物搜尋 + 手動輸入 */}
          <Card title="STEP 2 — 加入道具 / 寵物">
            {/* 名稱搜尋（需先上傳 xlsx）*/}
            <div style={{ marginBottom: 10 }}>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>
                📂 名稱搜尋（從已上傳的 xlsx 清單自動偵測）
              </div>
              <ItemAutocomplete
                mode="both"
                onSelect={addFromAutocomplete}
                placeholder="輸入道具或寵物名稱關鍵字…"
              />
            </div>

            {/* 分隔 */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, margin: '8px 0' }}>
              <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
              <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>或直接輸入編號</span>
              <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
            </div>

            {/* 手動輸入 */}
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <label style={{ flex: 1, minWidth: 90 }}>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>道具編號</span>
                <input type="number" value={manualId} onChange={e => setManualId(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && addManualToCart()}
                  placeholder="例 1001" min={0} style={{ width: '100%', marginTop: 2 }} />
              </label>
              <label style={{ width: 60 }}>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>數量</span>
                <input type="number" value={manualQty} onChange={e => setManualQty(+e.target.value || 1)}
                  min={1} max={999} style={{ width: '100%', marginTop: 2 }} />
              </label>
              <label style={{ width: 55 }}>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>Type</span>
                <input type="number" value={manualType} onChange={e => setManualType(+e.target.value || 1)}
                  min={1} max={9} style={{ width: '100%', marginTop: 2 }} />
              </label>
              <button onClick={addManualToCart}
                style={{ background: 'var(--accent-green)', color: '#fff', padding: '8px 14px', alignSelf: 'flex-end' }}>＋ 加入</button>
            </div>
            <div style={{ marginTop: 6, fontSize: 11, color: 'var(--text-muted)', background: 'var(--bg-input)', borderRadius: 4, padding: '4px 8px' }}>
              Type: 1=道具 2=寵物 3=金幣 4=元寶 5=道具(不可轉) 6=公會資金 7=寵物糖果 8=VIP點
            </div>
          </Card>

          {/* 郵件設定 */}
          <Card title="STEP 3 — 郵件設定">
            <label style={{ fontSize: 12, color: 'var(--text-muted)' }}>郵件標題（選填）</label>
            <input value={title} onChange={e => setTitle(e.target.value)}
              placeholder="[GM] 道具發送" style={{ width: '100%', marginBottom: 8, marginTop: 2 }} />
            <label style={{ fontSize: 12, color: 'var(--text-muted)' }}>郵件內容（選填）</label>
            <input value={content} onChange={e => setContent(e.target.value)}
              placeholder="GM 發放道具" style={{ width: '100%', marginTop: 2 }} />
          </Card>
        </div>

        {/* 右側：購物車 + 歷史 */}
        <div style={{ width: 300, flexShrink: 0 }}>
          <Card title={`🛒 購物車 (${cart.length} 種)`}>
            {cart.length === 0
              ? <p style={{ color: 'var(--text-muted)', fontSize: 13, textAlign: 'center', padding: '12px 0' }}>購物車為空<br/><span style={{fontSize:11}}>從左側清單點選道具</span></p>
              : (
                <>
                  <table style={{ width: '100%', fontSize: 12, borderCollapse: 'collapse', marginBottom: 10 }}>
                    <thead>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <th style={{ padding: '3px 4px', textAlign: 'left', color: 'var(--text-muted)' }}>道具</th>
                        <th style={{ padding: '3px 4px', color: 'var(--text-muted)', width: 30 }}>T</th>
                        <th style={{ padding: '3px 4px', color: 'var(--text-muted)', width: 55 }}>數量</th>
                        <th style={{ width: 20 }}></th>
                      </tr>
                    </thead>
                    <tbody>
                      {cart.map((c, i) => (
                        <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>
                          <td style={{ padding: '4px 4px' }}>
                            <span style={{ color: 'var(--accent-blue)', fontWeight: 600 }}>#{c.itemId}</span>
                            {c.name && <span style={{ color: 'var(--text-muted)', fontSize: 11, marginLeft: 4 }}>{c.name}</span>}
                          </td>
                          <td style={{ padding: '4px 4px', color: 'var(--text-muted)' }}>{c.type}</td>
                          <td style={{ padding: '2px 4px' }}>
                            <input type="number" value={c.qty} onChange={e => updateCartQty(i, +e.target.value || 1)}
                              min={1} max={999} style={{ width: 50, fontSize: 12 }} />
                          </td>
                          <td>
                            <button onClick={() => removeFromCart(i)}
                              style={{ color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer', padding: 0, fontSize: 14 }}>✕</button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <button onClick={() => setCart([])} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginBottom: 8, padding: 0 }}>清空購物車</button>
                </>
              )}
            <button onClick={send} disabled={loading || selectedAccounts.length === 0 || cart.length === 0}
              style={{ width: '100%', background: 'var(--accent-blue)', color: '#fff', padding: '10px 0', fontSize: 14, borderRadius: 8, opacity: (selectedAccounts.length === 0 || cart.length === 0) ? 0.5 : 1 }}>
              {loading ? '發送中…' : selectedAccounts.length > 1
                ? `📬 發送至 ${selectedAccounts.length} 位玩家`
                : `📬 發送至 ${selectedAccounts[0]?.onlineName || '玩家'}`}
            </button>
          </Card>

          {/* 郵件歷史 */}
          {showHistory && (
            <Card title={`📜 郵件歷史`}>
              <button onClick={() => setShowHistory(false)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginBottom: 6, padding: 0 }}>收起</button>
              {mailHistory.length === 0
                ? <p style={{ color: 'var(--text-muted)', fontSize: 12, textAlign: 'center' }}>無道具郵件記錄</p>
                : (
                  <div style={{ maxHeight: 240, overflowY: 'auto' }}>
                    {mailHistory.map(m => (
                      <div key={m.mailId} style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                        <span>
                          <span style={{ color: 'var(--accent-blue)', fontWeight: 600 }}>#{m.itemId}</span>
                          {m.itemName !== `[GM] 道具 #${m.itemId}` && <span style={{ marginLeft: 4, color: 'var(--text-secondary)' }}>{m.itemName}</span>}
                        </span>
                        <span style={{ display: 'flex', gap: 6, color: 'var(--text-muted)' }}>
                          <span>{m.sendTime}</span>
                          <span style={{ color: m.isRead ? 'var(--accent-green)' : 'var(--accent-orange)' }}>{m.isRead ? '已領' : '未領'}</span>
                        </span>
                      </div>
                    ))}
                  </div>
                )}
            </Card>
          )}
        </div>
      </div>
    </div>
  )
}

const Card = ({ title, children }: { title: string; children: React.ReactNode }) => (
  <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, marginBottom: 14 }}>
    <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 10 }}>{title}</h3>
    {children}
  </div>
)
