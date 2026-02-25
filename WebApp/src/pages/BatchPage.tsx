import { useState } from 'react'
import api from '../api'
import { S } from '../strings'
import ItemBrowser from '../components/ItemBrowser'
import ItemAutocomplete from '../components/ItemAutocomplete'
import type { ItemInfo } from '../components/ItemBrowser'

interface CartItem { itemId: number; qty: number; type: number; name?: string }

export default function BatchPage() {
  const [target,  setTarget]  = useState<'all'|'online'|'custom'>('online')
  const [custom,  setCustom]  = useState('')
  const [title,   setTitle]   = useState('')
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [result,  setResult]  = useState('')

  // 購物車
  const [cart, setCart] = useState<CartItem[]>([])
  const [manualId, setManualId] = useState('')
  const [manualQty, setManualQty] = useState(1)
  const [manualType, setManualType] = useState(1)

  const addToCart = (item: CartItem) => {
    setCart(prev => {
      const existing = prev.find(c => c.itemId === item.itemId && c.type === item.type)
      if (existing) return prev.map(c => c.itemId === item.itemId && c.type === item.type ? { ...c, qty: c.qty + item.qty } : c)
      return [...prev, item]
    })
  }

  const addManualToCart = () => {
    const id = parseInt(manualId, 10)
    if (!id || id <= 0) return
    addToCart({ itemId: id, qty: manualQty, type: manualType })
    setManualId('')
  }

  const addFromAutocomplete = (item: ItemInfo) => {
    addToCart({ itemId: item.id, qty: 1, type: item.isPet ? 2 : 1, name: item.name })
  }

  const send = async () => {
    if (!title.trim() || !content.trim()) { setResult('請填寫郵件標題和內容'); return }
    if (cart.length === 0) { setResult('請在購物車中加入至少一種道具'); return }
    setLoading(true); setResult('')
    try {
      const body = { target, customList: custom, cart: cart.map(c => ({ itemId: c.itemId, qty: c.qty, type: c.type })), title, content }
      const r = await api.post('/players/batch-send-cart', body)
      setResult(r.data.message || `已發送至 ${r.data.count || '?'} 人`)
      setCart([])
    } catch { setResult('發送失敗') }
    finally { setLoading(false) }
  }

  const btnStyle = (v: string) => ({
    padding: '6px 16px', borderRadius: 6, fontSize: 13, fontWeight: 600,
    background: target === v ? 'var(--accent-blue)' : 'var(--bg-input)',
    color: target === v ? '#fff' : 'var(--text-secondary)',
    border: `1px solid ${target === v ? 'var(--accent-blue)' : 'var(--border)'}`,
    cursor: 'pointer'
  })

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>📢 {S.pageBatch}</h1>

      <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
        {/* 道具清單 */}
        <div style={{ width: 340, flexShrink: 0 }}>
          <ItemBrowser cart={cart} onAddToCart={addToCart} />
        </div>

        {/* 中間 */}
        <div style={{ flex: 1 }}>
          {/* 目標選擇 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 16 }}>
            <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>{S.batchTarget}</h3>
            <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
              <button style={btnStyle('all')}    onClick={() => setTarget('all')}>{S.batchAll}</button>
              <button style={btnStyle('online')} onClick={() => setTarget('online')}>{S.batchOnline}</button>
              <button style={btnStyle('custom')} onClick={() => setTarget('custom')}>{S.batchCustom}</button>
            </div>
            {target === 'custom' && (
              <textarea value={custom} onChange={e => setCustom(e.target.value)}
                placeholder={'一行一個帳號\naccount1\naccount2'}
                style={{ width: '100%', height: 100, background: 'var(--bg-input)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: 6, padding: 8, fontSize: 13, resize: 'vertical' }} />
            )}
          </div>

          {/* 加入道具 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, marginBottom: 16 }}>
            <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 10 }}>加入道具 / 寵物</h3>

            {/* 名稱搜尋 */}
            <div style={{ marginBottom: 10 }}>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>名稱搜尋（從已上傳的 xlsx 清單自動偵測）</div>
              <ItemAutocomplete mode="both" onSelect={addFromAutocomplete} />
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: 8, margin: '8px 0' }}>
              <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
              <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>或直接輸入編號</span>
              <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
            </div>

            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'flex-end' }}>
              <label style={{ flex: 1 }}>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>道具編號</span>
                <input type="number" value={manualId} onChange={e => setManualId(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && addManualToCart()}
                  placeholder="例 1001" style={{ width: '100%', marginTop: 2 }} />
              </label>
              <label style={{ width: 65 }}>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>數量/人</span>
                <input type="number" value={manualQty} onChange={e => setManualQty(+e.target.value || 1)} min={1} max={99} style={{ width: '100%', marginTop: 2 }} />
              </label>
              <label style={{ width: 65 }}>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>Type</span>
                <input type="number" value={manualType} onChange={e => setManualType(+e.target.value || 1)} min={1} max={9} style={{ width: '100%', marginTop: 2 }} />
              </label>
              <button onClick={addManualToCart} style={{ background: 'var(--accent-green)', color: '#fff', padding: '8px 14px', alignSelf: 'flex-end' }}>＋ 加入</button>
            </div>
          </div>

          {/* 郵件內容 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20 }}>
            <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>郵件設定</h3>
            <div style={{ marginBottom: 10 }}>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>{S.batchTitle}</div>
              <input value={title} onChange={e => setTitle(e.target.value)} placeholder="郵件標題" style={{ width: '100%' }} />
            </div>
            <div style={{ marginBottom: 12 }}>
              <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>{S.batchContent}</div>
              <textarea value={content} onChange={e => setContent(e.target.value)} placeholder="郵件內容"
                style={{ width: '100%', height: 80, background: 'var(--bg-input)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: 6, padding: 8, fontSize: 13, resize: 'vertical' }} />
            </div>
            {result && (
              <p style={{ color: result.includes('失敗') || result.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)', marginBottom: 10, fontSize: 13 }}>{result}</p>
            )}
            <button onClick={send} disabled={loading} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 24px', fontSize: 14 }}>
              {loading ? S.batchSending : `📤 ${S.batchSend}`}
            </button>
          </div>
        </div>

        {/* 右側 - 購物車 */}
        <div style={{ width: 300, flexShrink: 0 }}>
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 18 }}>
            <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>🛒 購物車（{cart.length} 種道具）</h3>
            {cart.length === 0
              ? <p style={{ color: 'var(--text-muted)', fontSize: 13, textAlign: 'center', padding: 16 }}>購物車為空</p>
              : (
                <>
                  {cart.map((c, i) => (
                    <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 0', borderBottom: '1px solid var(--border)', fontSize: 13 }}>
                      <span style={{ color: 'var(--accent-blue)', fontWeight: 600, flex: 1 }}>#{c.itemId}</span>
                      <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>type:{c.type}</span>
                      <input type="number" value={c.qty} min={1} max={99}
                        onChange={e => setCart(cart.map((cc, ii) => ii === i ? { ...cc, qty: +e.target.value || 1 } : cc))}
                        style={{ width: 50, fontSize: 12, textAlign: 'center' }} />
                      <button onClick={() => setCart(cart.filter((_, ii) => ii !== i))}
                        style={{ color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer' }}>✕</button>
                    </div>
                  ))}
                  <button onClick={() => setCart([])} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginTop: 8, padding: 0 }}>清空</button>
                </>
              )}
          </div>
        </div>
      </div>
    </div>
  )
}
