import { useState } from 'react'
import api from '../api'
import { S } from '../strings'
import ItemBrowser from '../components/ItemBrowser'
import ItemAutocomplete from '../components/ItemAutocomplete'
import type { ItemInfo } from '../components/ItemBrowser'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import type { PlayerRow } from '../api'
import useIsMobile from '../hooks/useIsMobile'

interface CartItem { itemId: number; qty: number; type: number; name?: string; buff3?: string }

export default function BatchPage() {
  const isMobile = useIsMobile()
  const [target,  setTarget]  = useState<'all'|'online'|'online_recent'|'custom'|'search'>('online')
  const [custom,  setCustom]  = useState('')
  const [title,   setTitle]   = useState('')
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [result,  setResult]  = useState('')

  // 搜尋勾選玩家
  const [searchQ, setSearchQ] = useState('')
  const [searchList, setSearchList] = useState<PlayerRow[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [listLoading, setListLoading] = useState(false)

  const loadSearchList = async (mode: 'all' | 'online' | 'online_recent') => {
    setListLoading(true)
    try {
      if (mode === 'all') {
        const r = await api.get('/players/list', { params: { limit: 500 } })
        setSearchList(r.data); setSelected(new Set())
      } else {
        const r = await api.get('/players/online', { params: { recent: mode === 'online_recent' } })
        setSearchList(r.data); setSelected(new Set())
      }
    } finally { setListLoading(false) }
  }

  const toggleSelect = (acc: string) => {
    const s = new Set(selected)
    s.has(acc) ? s.delete(acc) : s.add(acc)
    setSelected(s)
  }

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
    addToCart({ itemId: item.id, qty: 1, type: 1, name: item.name, buff3: item.name })
  }

  const send = async () => {
    if (cart.length === 0) { setResult('請在購物車中加入至少一種道具'); return }

    // 計算目標玩家
    let targetStr = target
    let customListStr = custom
    if (target === 'search') {
      if (selected.size === 0) { setResult('請勾選至少一位玩家'); return }
      targetStr = 'custom'
      customListStr = Array.from(selected).join('\n')
    }

    const targetLabel = target === 'all' ? '全部玩家'
      : target === 'online' ? '在線（Online=1＋同主帳＋同IP 所有角色）'
        : target === 'online_recent' ? '近期登入推測（非即時連線）'
          : target === 'search' ? `${selected.size} 位勾選玩家` : '自訂名單'

    if (!window.confirm(`確認批量發送？\n目標：${targetLabel}\n道具：${cart.length} 種\n標題：${title || '(無)'}`)) return

    setLoading(true); setResult('')
    try {
      const body = { target: targetStr, customList: customListStr, cart: cart.map(c => ({ itemId: c.itemId, qty: c.qty, type: c.type, name: c.name ?? '', buff3: c.buff3 ?? '' })), title, content }
      const r = await api.post('/players/batch-send-cart', body)
      setResult((r.data.message || `已發送至 ${r.data.count || '?'} 人`) + '（在線玩家需重新登入後至信箱領取）')
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
    <div className="gm-page-inner">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>📢 {S.pageBatch}</h1>

      <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start', flexWrap: isMobile ? 'wrap' : 'nowrap' }}>
        {/* 道具清單 */}
        <div style={{ width: isMobile ? '100%' : 380, flexShrink: 0, minWidth: 0 }}>
          <ItemBrowser cart={cart} onAddToCart={addToCart} />
        </div>

        {/* 中間 */}
        <div style={{ flex: 1, minWidth: 0, width: isMobile ? '100%' : undefined }}>
          {/* 目標選擇 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 16 }}>
            <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>{S.batchTarget}</h3>
            <div style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap' }}>
              <button style={btnStyle('all')}    onClick={() => { setTarget('all'); setSearchList([]); setSelected(new Set()) }}>{S.batchAll}</button>
              <button style={btnStyle('online')} onClick={() => { setTarget('online'); setSearchList([]); setSelected(new Set()) }} title="csalogin.Online=1，由遊戲伺服器維護">🟢 在線（Online=1）</button>
              <button style={btnStyle('online_recent')} onClick={() => { setTarget('online_recent'); setSearchList([]); setSelected(new Set()) }} title="依 LoginTime 推測，非真實連線">🟡 近期推測</button>
              <button style={btnStyle('custom')} onClick={() => setTarget('custom')}>自訂帳號</button>
              <button style={btnStyle('search')} onClick={() => setTarget('search')}>🔍 搜尋勾選</button>
            </div>
            {target === 'online' && (
              <p style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10, lineHeight: 1.6 }}>
                「在線」批量含 <code style={{ fontSize: 10 }}>Online=1</code> 及<strong>同主帳號＋同 IP</strong> 之所有角色。
              </p>
            )}
            {target === 'online_recent' && (
              <p style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 10, lineHeight: 1.6 }}>
                「近期推測」不是即時連線，且不套用同主帳＋同 IP 擴充。
              </p>
            )}
            {target === 'custom' && (
              <textarea value={custom} onChange={e => setCustom(e.target.value)}
                placeholder={'一行一個帳號\naccount1\naccount2'}
                style={{ width: '100%', height: 100, background: 'var(--bg-input)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: 6, padding: 8, fontSize: 13, resize: 'vertical' }} />
            )}
            {target === 'search' && (
              <div>
                <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
                  <PlayerAutocomplete
                    value={searchQ}
                    onChange={setSearchQ}
                    onSelect={(p: PlayerRow) => {
                      setSearchList(prev => prev.find(x => x.account === p.account) ? prev : [...prev, p])
                      setSelected(prev => { const s = new Set(prev); s.add(p.account); return s })
                      setSearchQ('')
                    }}
                    placeholder="搜尋玩家加入清單…"
                    style={{ flex: 1 }}
                  />
                  <button type="button" onClick={() => loadSearchList('online')} disabled={listLoading}
                    style={{ ...btnStyle('online'), padding: '6px 12px', fontSize: 12 }}>
                    {listLoading ? '載入…' : '載入在線'}
                  </button>
                  <button type="button" onClick={() => loadSearchList('online_recent')} disabled={listLoading}
                    style={{ ...btnStyle('online_recent'), padding: '6px 12px', fontSize: 12 }}
                    title="依 LoginTime，非真實連線">
                    {listLoading ? '載入…' : '載入近期推測'}
                  </button>
                  <button type="button" onClick={() => loadSearchList('all')} disabled={listLoading}
                    style={{ ...btnStyle('all'), padding: '6px 12px', fontSize: 12 }}>
                    載入全部
                  </button>
                </div>
                {searchList.length > 0 && (
                  <div style={{ border: '1px solid var(--border)', borderRadius: 6, overflow: 'hidden' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 10px', background: 'var(--bg-input)', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                      <span style={{ color: 'var(--text-muted)', flex: 1 }}>共 {searchList.length} 人，已勾選 {selected.size} 人</span>
                      <button onClick={() => setSelected(new Set(searchList.map(p => p.account)))} style={{ fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>全選</button>
                      <button onClick={() => setSelected(new Set())} style={{ fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>清除</button>
                    </div>
                    <div style={{ maxHeight: 200, overflowY: 'auto' }}>
                      {searchList.map(p => (
                        <label
                          key={p.account}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 8,
                            padding: '6px 10px',
                            borderBottom: '1px solid var(--border)',
                            cursor: 'pointer',
                            background: selected.has(p.account) ? 'rgba(74,158,255,.1)' : 'transparent',
                            fontSize: 12,
                            userSelect: 'none',
                            touchAction: 'manipulation',
                            WebkitTapHighlightColor: 'transparent',
                          }}
                        >
                          <input
                            type="checkbox"
                            checked={selected.has(p.account)}
                            onChange={() => toggleSelect(p.account)}
                            style={{ width: 18, height: 18, flexShrink: 0, accentColor: 'var(--accent-blue)' }}
                          />
                          <span style={{ fontSize: 11 }}>{p.isOnline ? '🟢' : '⚫'}</span>
                          <span style={{ fontWeight: 600, flex: 1 }}>{p.onlineName || p.account}</span>
                          <span style={{ color: 'var(--text-muted)' }}>{p.account}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}
              </div>
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
        <div style={{ width: isMobile ? '100%' : 320, flexShrink: 0, minWidth: 0 }}>
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 18 }}>
            <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>🛒 購物車（{cart.length} 種道具）</h3>
            {cart.length === 0
              ? <p style={{ color: 'var(--text-muted)', fontSize: 13, textAlign: 'center', padding: 16 }}>購物車為空</p>
              : (
                <>
                  {cart.map((c, i) => (
                    <div key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: 8, padding: '8px 0', borderBottom: '1px solid var(--border)', fontSize: 13 }}>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ color: 'var(--accent-blue)', fontWeight: 700 }}>#{c.itemId}</div>
                        {c.name && <div style={{ color: 'var(--text-primary)', marginTop: 2, lineHeight: 1.35, wordBreak: 'break-word', overflowWrap: 'anywhere' }}>{c.name}</div>}
                        {c.buff3 && <div style={{ color: 'var(--text-muted)', fontSize: 11, marginTop: 2, lineHeight: 1.35, wordBreak: 'break-word', overflowWrap: 'anywhere' }}>{c.buff3}</div>}
                        <div style={{ color: 'var(--text-muted)', fontSize: 11, marginTop: 2 }}>type {c.type}</div>
                      </div>
                      <input type="number" value={c.qty} min={1} max={99}
                        onChange={e => setCart(cart.map((cc, ii) => ii === i ? { ...cc, qty: +e.target.value || 1 } : cc))}
                        style={{ width: 52, flexShrink: 0, fontSize: 12, textAlign: 'center', boxSizing: 'border-box' }} />
                      <button type="button" onClick={() => setCart(cart.filter((_, ii) => ii !== i))}
                        style={{ color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer', flexShrink: 0 }}>✕</button>
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
