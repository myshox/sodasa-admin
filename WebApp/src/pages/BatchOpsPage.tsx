import { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'
import type { MailHistoryItem } from '../api'
import type { PlayerRow } from '../api'
import ItemBrowser from '../components/ItemBrowser'
import ItemAutocomplete from '../components/ItemAutocomplete'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import type { ItemInfo } from '../components/ItemBrowser'

type MainTab = 'single' | 'batch' | 'gold'
interface CartItem { itemId: number; qty: number; type: number; name?: string; buff3?: string }
interface MailRawEntry { id: number; type: number; buff1: string; buff2: string; rawData: string; buff3: string; sendTime: string; isRead: boolean; deleted: boolean }

// ────────────────────────────────────────────────────────────
// Tab 1 — 道具給予（單人）
// ────────────────────────────────────────────────────────────
function SingleSendTab() {
  const [sp] = useSearchParams()
  const [playerQ, setPlayerQ] = useState(sp.get('account') || '')
  const [selectedAccount, setSelectedAccount] = useState(sp.get('account') || '')
  const [selectedName, setSelectedName] = useState(decodeURIComponent(sp.get('name') || sp.get('account') || ''))
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState('')
  const [manualId, setManualId] = useState('')
  const [manualQty, setManualQty] = useState(1)
  const [manualType, setManualType] = useState(1)
  const [cart, setCart] = useState<CartItem[]>([])
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [mailHistory, setMailHistory] = useState<MailHistoryItem[]>([])
  const [showHistory, setShowHistory] = useState(false)
  const [historyLoading, setHistoryLoading] = useState(false)
  const [mailRaw, setMailRaw] = useState<MailRawEntry[]>([])
  const [showRaw, setShowRaw] = useState(false)
  const [rawLoading, setRawLoading] = useState(false)
  const [sentSummary, setSentSummary] = useState<{ account: string; name: string; items: CartItem[] } | null>(null)

  useEffect(() => {
    const acc = sp.get('account')
    if (acc) { setSelectedAccount(acc); setPlayerQ(acc); setSelectedName(sp.get('name') ? decodeURIComponent(sp.get('name')!) : acc) }
  }, [sp])

  const addToCart = (item: CartItem) =>
    setCart(prev => { const e = prev.find(c => c.itemId === item.itemId && c.type === item.type); return e ? prev.map(c => c.itemId === item.itemId && c.type === item.type ? { ...c, qty: c.qty + item.qty } : c) : [...prev, item] })
  const addManualToCart = () => { const id = parseInt(manualId, 10); if (!id || id <= 0) return; addToCart({ itemId: id, qty: manualQty, type: manualType }); setManualId('') }
  const addFromAutocomplete = (item: ItemInfo) => addToCart({ itemId: item.id, qty: 1, type: 1, name: item.name, buff3: item.desc })

  const loadHistory = async () => { if (!selectedAccount) return; setHistoryLoading(true); try { const r = await api.get(`/players/${selectedAccount}/mail-history`); setMailHistory(r.data); setShowHistory(true) } finally { setHistoryLoading(false) } }
  const loadRaw = async () => { if (!selectedAccount) return; setRawLoading(true); try { const r = await api.get(`/players/${selectedAccount}/mail-raw`); setMailRaw(r.data); setShowRaw(true) } finally { setRawLoading(false) } }

  const send = async () => {
    if (!selectedAccount) { setResult('請先選定玩家'); return }
    if (cart.length === 0) { setResult('購物車為空，請加入道具'); return }
    setLoading(true); setResult('')
    try {
      const sentItems = [...cart]
      const r = await api.post('/players/send-cart', { account: selectedAccount, cart: cart.map(c => ({ itemId: c.itemId, qty: c.qty, type: c.type, name: c.name ?? '', buff3: c.buff3 ?? '' })), title: title.trim(), content: content.trim() })
      setResult(r.data.message || `已發送 ${r.data.success} 筆`)
      setSentSummary({ account: selectedAccount, name: selectedName, items: sentItems })
      setCart([])
    } catch (e: unknown) { const err = e as { response?: { data?: { message?: string } } }; setResult(err.response?.data?.message || '發送失敗') }
    finally { setLoading(false) }
  }

  return (
    <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
      <div style={{ width: 340, flexShrink: 0 }}><ItemBrowser cart={cart} onAddToCart={addToCart} /></div>
      <div style={{ flex: 1, minWidth: 0 }}>
        {result && <div style={{ background: result.includes('失敗') || result.includes('請') ? 'rgba(245,101,101,.1)' : 'rgba(86,196,118,.15)', border: `1px solid ${result.includes('失敗') || result.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)'}`, borderRadius: 8, padding: '10px 16px', marginBottom: 12, color: result.includes('失敗') || result.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)', fontSize: 13 }}>{result}</div>}
        <Card title="STEP 1 — 指定玩家">
          <PlayerAutocomplete value={playerQ} onChange={setPlayerQ} onSelect={p => { setSelectedAccount(p.account); setSelectedName(p.onlineName || p.account); setPlayerQ(p.onlineName || p.account) }} placeholder="輸入帳號或角色名稱" />
          {selectedAccount && <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginTop: 8 }}>
            <p style={{ fontSize: 13, color: 'var(--accent-green)' }}>✓ 已選：{selectedName}（{selectedAccount}）</p>
            <div style={{ display: 'flex', gap: 6 }}>
              <button onClick={loadHistory} disabled={historyLoading} style={{ fontSize: 12, padding: '3px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 4 }}>{historyLoading ? '載入中…' : '📜 郵件歷史'}</button>
              <button onClick={loadRaw} disabled={rawLoading} style={{ fontSize: 12, padding: '3px 10px', background: 'rgba(255,159,10,.15)', border: '1px solid var(--accent-orange)', borderRadius: 4, color: 'var(--accent-orange)' }}>{rawLoading ? '載入中…' : '🔬 診斷格式'}</button>
              <button onClick={async () => { if (!window.confirm(`修正 ${selectedName} 的舊版網頁郵件（使其可領取）？`)) return; try { const r = await api.post('/players/fix-old-mails', { account: selectedAccount }); setResult(r.data.message) } catch { setResult('修正失敗') } }} style={{ fontSize: 12, padding: '3px 10px', background: 'rgba(86,196,118,.15)', border: '1px solid var(--accent-green)', borderRadius: 4, color: 'var(--accent-green)' }}>🔧 修正舊郵件</button>
            </div>
          </div>}
        </Card>
        <Card title="STEP 2 — 加入道具 / 寵物">
          <div style={{ marginBottom: 10 }}><div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>名稱搜尋</div><ItemAutocomplete mode="both" onSelect={addFromAutocomplete} /></div>
          <Divider />
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <label style={{ flex: 1 }}><span style={{ fontSize: 12, color: 'var(--text-muted)' }}>道具編號</span><input type="number" value={manualId} onChange={e => setManualId(e.target.value)} onKeyDown={e => e.key === 'Enter' && addManualToCart()} placeholder="例 1001" style={{ width: '100%', marginTop: 2 }} /></label>
            <label style={{ width: 60 }}><span style={{ fontSize: 12, color: 'var(--text-muted)' }}>數量</span><input type="number" value={manualQty} onChange={e => setManualQty(+e.target.value || 1)} min={1} max={999} style={{ width: '100%', marginTop: 2 }} /></label>
            <label style={{ width: 55 }}><span style={{ fontSize: 12, color: 'var(--text-muted)' }}>Type</span><input type="number" value={manualType} onChange={e => setManualType(+e.target.value || 1)} min={1} max={9} style={{ width: '100%', marginTop: 2 }} /></label>
            <button onClick={addManualToCart} style={{ background: 'var(--accent-green)', color: '#fff', padding: '8px 14px', alignSelf: 'flex-end' }}>＋ 加入</button>
          </div>
          <div style={{ marginTop: 6, fontSize: 11, color: 'var(--text-muted)', background: 'var(--bg-input)', borderRadius: 4, padding: '4px 8px' }}>Type: 1=道具 2=寵物 3=金幣 4=元寶 5=道具(不可轉) 6=公會 7=寵物糖 8=VIP點</div>
        </Card>
        <Card title="STEP 3 — 郵件設定">
          <label style={{ fontSize: 12, color: 'var(--text-muted)' }}>郵件標題（選填）</label>
          <input value={title} onChange={e => setTitle(e.target.value)} placeholder="[GM] 道具發送" style={{ width: '100%', marginBottom: 8, marginTop: 2 }} />
          <label style={{ fontSize: 12, color: 'var(--text-muted)' }}>郵件內容（選填）</label>
          <input value={content} onChange={e => setContent(e.target.value)} placeholder="GM 發放道具" style={{ width: '100%', marginTop: 2 }} />
        </Card>
      </div>
      <div style={{ width: 280, flexShrink: 0 }}>
        <Card title={`🛒 購物車 (${cart.length} 種)`}>
          {cart.length === 0 ? <p style={{ color: 'var(--text-muted)', fontSize: 13, textAlign: 'center', padding: '12px 0' }}>購物車為空<br /><span style={{ fontSize: 11 }}>從左側清單點選道具</span></p>
            : <><table style={{ width: '100%', fontSize: 12, borderCollapse: 'collapse', marginBottom: 10 }}>
                <thead><tr style={{ borderBottom: '1px solid var(--border)' }}><th style={{ padding: '3px 4px', textAlign: 'left', color: 'var(--text-muted)' }}>道具</th><th style={{ padding: '3px 4px', color: 'var(--text-muted)', width: 30 }}>T</th><th style={{ padding: '3px 4px', color: 'var(--text-muted)', width: 55 }}>數量</th><th style={{ width: 20 }}></th></tr></thead>
                <tbody>{cart.map((c, i) => <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '4px 4px' }}><span style={{ color: 'var(--accent-blue)', fontWeight: 600 }}>#{c.itemId}</span>{c.name && <span style={{ color: 'var(--text-muted)', fontSize: 11, marginLeft: 4 }}>{c.name}</span>}</td><td style={{ padding: '4px 4px', color: 'var(--text-muted)' }}>{c.type}</td><td style={{ padding: '2px 4px' }}><input type="number" value={c.qty} onChange={e => setCart(cart.map((cc, ii) => ii === i ? { ...cc, qty: +e.target.value || 1 } : cc))} min={1} max={999} style={{ width: 50, fontSize: 12 }} /></td><td><button onClick={() => setCart(cart.filter((_, ii) => ii !== i))} style={{ color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer', padding: 0, fontSize: 14 }}>✕</button></td></tr>)}</tbody>
              </table>
              <button onClick={() => setCart([])} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginBottom: 8, padding: 0 }}>清空購物車</button></>}
          <button onClick={send} disabled={loading || !selectedAccount || cart.length === 0} style={{ width: '100%', background: 'var(--accent-blue)', color: '#fff', padding: '10px 0', fontSize: 14, borderRadius: 8, opacity: (!selectedAccount || cart.length === 0) ? 0.5 : 1 }}>
            {loading ? '發送中…' : `📬 發送至 ${selectedName || '玩家'}`}
          </button>
        </Card>
        {sentSummary && (
          <Card title="✅ 發送完成">
            <div style={{ marginBottom: 10, padding: '8px 12px', background: 'rgba(86,196,118,.12)', border: '1px solid var(--accent-green)', borderRadius: 6 }}>
              <div style={{ fontSize: 13, color: 'var(--accent-green)', fontWeight: 700, marginBottom: 6 }}>
                已發送至：{sentSummary.name}（{sentSummary.account}）
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>
                {sentSummary.items.map((c, i) => (
                  <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 0', borderBottom: '1px solid rgba(255,255,255,.05)' }}>
                    <span>#{c.itemId}{c.name ? ` ${c.name}` : ''}</span>
                    <span style={{ color: 'var(--text-muted)' }}>× {c.qty}</span>
                  </div>
                ))}
              </div>
            </div>
            <button onClick={() => setSentSummary(null)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}>關閉</button>
          </Card>
        )}
        {showHistory && <Card title="📜 郵件歷史">
          <button onClick={() => setShowHistory(false)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginBottom: 6, padding: 0 }}>收起</button>
          {mailHistory.length === 0 ? <p style={{ color: 'var(--text-muted)', fontSize: 12, textAlign: 'center' }}>無道具郵件記錄</p>
            : <div style={{ maxHeight: 200, overflowY: 'auto' }}>{mailHistory.map(m => <div key={m.mailId} style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', borderBottom: '1px solid var(--border)', fontSize: 12 }}><span><span style={{ color: 'var(--accent-blue)', fontWeight: 600 }}>#{m.itemId}</span><span style={{ marginLeft: 4, color: 'var(--text-secondary)' }}>{m.itemName}</span></span><span style={{ color: m.isRead ? 'var(--accent-green)' : 'var(--accent-orange)' }}>{m.isRead ? '已領' : '未領'}</span></div>)}</div>}
        </Card>}
        {showRaw && <Card title="🔬 maildata 原始格式診斷">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
            <p style={{ fontSize: 11, color: 'var(--text-muted)', margin: 0 }}>比較「已領取✓」與「未領取○」的 type/data/buff3 欄位差異，可找出格式問題</p>
            <button onClick={() => setShowRaw(false)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer' }}>收起</button>
          </div>
          <div style={{ overflowX: 'auto', maxHeight: 300, overflowY: 'auto' }}>
            <table style={{ width: '100%', fontSize: 11, borderCollapse: 'collapse', fontFamily: 'monospace' }}>
              <thead><tr style={{ background: 'var(--bg-dark)', position: 'sticky', top: 0 }}>
                {['狀態','ID','type','data（物品）','buff1（標題）','buff3','時間'].map(h => (
                  <th key={h} style={{ padding: '5px 8px', textAlign: 'left', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>{h}</th>
                ))}
              </tr></thead>
              <tbody>
                {mailRaw.map(m => (
                  <tr key={m.id} style={{ borderBottom: '1px solid var(--border)', background: m.deleted ? 'rgba(245,101,101,.05)' : 'transparent' }}>
                    <td style={{ padding: '4px 8px', color: m.deleted ? 'var(--text-muted)' : m.isRead ? 'var(--accent-green)' : 'var(--accent-orange)', fontWeight: 600 }}>{m.deleted ? '🗑 刪' : m.isRead ? '✓ 已領' : '○ 未領'}</td>
                    <td style={{ padding: '4px 8px', color: 'var(--text-muted)' }}>{m.id}</td>
                    <td style={{ padding: '4px 8px' }}><span style={{ background: 'var(--bg-input)', borderRadius: 3, padding: '1px 5px', color: m.type === 1 ? 'var(--accent-blue)' : m.type === 2 ? 'var(--accent-purple)' : 'var(--accent-orange)' }}>type={m.type}</span></td>
                    <td style={{ padding: '4px 8px', color: 'var(--accent-blue)', fontWeight: 700 }}>{m.rawData}</td>
                    <td style={{ padding: '4px 8px', color: 'var(--text-secondary)', maxWidth: 140, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{m.buff1}</td>
                    <td style={{ padding: '4px 8px', color: m.buff3 ? 'var(--accent-orange)' : 'var(--text-muted)' }}>{m.buff3 || '(空)'}</td>
                    <td style={{ padding: '4px 8px', color: 'var(--text-muted)' }}>{m.sendTime}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div style={{ marginTop: 8, padding: '8px 10px', background: 'rgba(255,159,10,.08)', border: '1px solid rgba(255,159,10,.3)', borderRadius: 6, fontSize: 11, color: 'var(--accent-orange)', lineHeight: 1.8 }}>
            💡 <b>診斷方法：</b>找一筆「✓ 已領取」的信件，記下它的 <b>type 值</b>和 <b>data 格式</b>（可能是純數字、或 "ID,數量" 等格式），與我們發送的信件比較。若格式不同，請告知即可修正。
          </div>
        </Card>}
      </div>
    </div>
  )
}

// ────────────────────────────────────────────────────────────
// Tab 2 — 批量發送（多人）
// ────────────────────────────────────────────────────────────
function BatchSendTab() {
  const [target, setTarget] = useState<'all' | 'online' | 'custom' | 'search'>('online')
  const [custom, setCustom] = useState('')
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState('')
  const [resultOk, setResultOk] = useState(true)
  const [sentAccounts, setSentAccounts] = useState<string[]>([])
  const [showSent, setShowSent] = useState(false)
  const [searchQ, setSearchQ] = useState('')
  const [searchList, setSearchList] = useState<PlayerRow[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [listLoading, setListLoading] = useState(false)
  const [cart, setCart] = useState<CartItem[]>([])
  const [manualId, setManualId] = useState('')
  const [manualQty, setManualQty] = useState(1)
  const [manualType, setManualType] = useState(1)

  const addToCart = (item: CartItem) => setCart(prev => { const e = prev.find(c => c.itemId === item.itemId && c.type === item.type); return e ? prev.map(c => c.itemId === item.itemId && c.type === item.type ? { ...c, qty: c.qty + item.qty } : c) : [...prev, item] })
  const addManualToCart = () => { const id = parseInt(manualId, 10); if (!id || id <= 0) return; addToCart({ itemId: id, qty: manualQty, type: manualType }); setManualId('') }
  const addFromAutocomplete = (item: ItemInfo) => addToCart({ itemId: item.id, qty: 1, type: 1, name: item.name, buff3: item.desc })
  const toggleSelect = (acc: string) => { const s = new Set(selected); s.has(acc) ? s.delete(acc) : s.add(acc); setSelected(s) }
  const btnStyle = (v: string) => ({ padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 600, background: target === v ? 'var(--accent-blue)' : 'var(--bg-input)', color: target === v ? '#fff' : 'var(--text-secondary)', border: `1px solid ${target === v ? 'var(--accent-blue)' : 'var(--border)'}`, cursor: 'pointer' })

  const send = async () => {
    if (cart.length === 0) { setResult('請加入至少一種道具'); setResultOk(false); return }
    let targetStr = target, customListStr = custom
    if (target === 'search') {
      if (selected.size === 0) { setResult('請勾選至少一位玩家'); setResultOk(false); return }
      targetStr = 'custom'; customListStr = Array.from(selected).join('\n')
    }
    const label = target === 'all' ? '全部玩家' : target === 'online' ? '在線玩家' : target === 'search' ? `${selected.size} 位玩家` : '自訂名單'
    if (!window.confirm(`確認批量發送？\n目標：${label}\n道具：${cart.length} 種`)) return
    setLoading(true); setResult(''); setSentAccounts([]); setShowSent(false)
    try {
      const r = await api.post('/players/batch-send-cart', {
        target: targetStr, customList: customListStr,
        cart: cart.map(c => ({ itemId: c.itemId, qty: c.qty, type: c.type, name: c.name ?? '', buff3: c.buff3 ?? '' })),
        title, content,
      })
      const ok = (r.data.accounts?.length ?? 0) > 0
      setResult(r.data.message || `已發送至 ${r.data.accounts?.length ?? 0} 人`)
      setResultOk(ok)
      setSentAccounts(r.data.accounts ?? [])
      setShowSent(ok)
      if (ok) setCart([])
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setResult(err?.response?.data?.message || '發送失敗（請確認伺服器連線）')
      setResultOk(false)
    } finally { setLoading(false) }
  }

  return (
    <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start' }}>
      <div style={{ width: 340, flexShrink: 0 }}><ItemBrowser cart={cart} onAddToCart={addToCart} /></div>
      <div style={{ flex: 1 }}>
        <Card title="STEP 1 — 目標玩家">
          <div style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap' }}>
            <button style={btnStyle('all')} onClick={() => { setTarget('all'); setSearchList([]); setSelected(new Set()) }}>🌐 全部玩家</button>
            <button style={btnStyle('online')} onClick={() => { setTarget('online'); setSearchList([]); setSelected(new Set()) }}>🟢 在線玩家</button>
            <button style={btnStyle('custom')} onClick={() => setTarget('custom')}>📝 自訂帳號</button>
            <button style={btnStyle('search')} onClick={() => setTarget('search')}>🔍 搜尋勾選</button>
          </div>
          {target === 'custom' && <textarea value={custom} onChange={e => setCustom(e.target.value)} placeholder={'一行一個帳號\naccount1\naccount2'} style={{ width: '100%', height: 80, background: 'var(--bg-input)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: 6, padding: 8, fontSize: 13, resize: 'vertical' }} />}
          {target === 'search' && <div>
            <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
              <PlayerAutocomplete value={searchQ} onChange={setSearchQ} onSelect={(p: PlayerRow) => { setSearchList(prev => prev.find(x => x.account === p.account) ? prev : [...prev, p]); setSelected(prev => { const s = new Set(prev); s.add(p.account); return s }); setSearchQ('') }} placeholder="搜尋玩家加入清單…" style={{ flex: 1 }} />
              <button onClick={async () => { setListLoading(true); try { const r = await api.get('/players/online'); setSearchList(r.data); setSelected(new Set()) } finally { setListLoading(false) } }} disabled={listLoading} style={{ ...btnStyle('online'), padding: '6px 10px', fontSize: 12 }}>{listLoading ? '載入…' : '在線'}</button>
              <button onClick={async () => { setListLoading(true); try { const r = await api.get('/players/list', { params: { limit: 500 } }); setSearchList(r.data); setSelected(new Set()) } finally { setListLoading(false) } }} disabled={listLoading} style={{ ...btnStyle('all'), padding: '6px 10px', fontSize: 12 }}>全部</button>
            </div>
            {searchList.length > 0 && <div style={{ border: '1px solid var(--border)', borderRadius: 6, overflow: 'hidden' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 10px', background: 'var(--bg-input)', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                <span style={{ color: 'var(--text-muted)', flex: 1 }}>共 {searchList.length} 人，已勾選 {selected.size} 人</span>
                <button onClick={() => setSelected(new Set(searchList.map(p => p.account)))} style={{ fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>全選</button>
                <button onClick={() => setSelected(new Set())} style={{ fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>清除</button>
              </div>
              <div style={{ maxHeight: 200, overflowY: 'auto' }}>
                {searchList.map(p => <div key={p.account} onClick={() => toggleSelect(p.account)} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 10px', borderBottom: '1px solid var(--border)', cursor: 'pointer', background: selected.has(p.account) ? 'rgba(74,158,255,.1)' : 'transparent', fontSize: 12 }}>
                  <input type="checkbox" checked={selected.has(p.account)} onChange={() => { }} style={{ pointerEvents: 'none' }} />
                  <span style={{ fontSize: 11 }}>{p.isOnline ? '🟢' : '⚫'}</span>
                  <span style={{ fontWeight: 600, flex: 1 }}>{p.onlineName || p.account}</span>
                </div>)}
              </div>
            </div>}
          </div>}
        </Card>
        <Card title="STEP 2 — 加入道具">
          <div style={{ marginBottom: 10 }}><ItemAutocomplete mode="both" onSelect={addFromAutocomplete} /></div>
          <Divider />
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <label style={{ flex: 1 }}><span style={{ fontSize: 12, color: 'var(--text-muted)' }}>道具編號</span><input type="number" value={manualId} onChange={e => setManualId(e.target.value)} onKeyDown={e => e.key === 'Enter' && addManualToCart()} placeholder="例 1001" style={{ width: '100%', marginTop: 2 }} /></label>
            <label style={{ width: 60 }}><span style={{ fontSize: 12, color: 'var(--text-muted)' }}>數量/人</span><input type="number" value={manualQty} onChange={e => setManualQty(+e.target.value || 1)} min={1} max={99} style={{ width: '100%', marginTop: 2 }} /></label>
            <label style={{ width: 55 }}><span style={{ fontSize: 12, color: 'var(--text-muted)' }}>Type</span><input type="number" value={manualType} onChange={e => setManualType(+e.target.value || 1)} min={1} max={9} style={{ width: '100%', marginTop: 2 }} /></label>
            <button onClick={addManualToCart} style={{ background: 'var(--accent-green)', color: '#fff', padding: '8px 14px', alignSelf: 'flex-end' }}>＋</button>
          </div>
        </Card>
        <Card title="STEP 3 — 郵件設定">
          <input value={title} onChange={e => setTitle(e.target.value)} placeholder="郵件標題" style={{ width: '100%', marginBottom: 8 }} />
          <textarea value={content} onChange={e => setContent(e.target.value)} placeholder="郵件內容" style={{ width: '100%', height: 60, background: 'var(--bg-input)', color: 'var(--text-primary)', border: '1px solid var(--border)', borderRadius: 6, padding: 8, fontSize: 13, resize: 'vertical' }} />
          {result && (
            <div style={{ marginTop: 10, padding: '10px 14px', borderRadius: 8, fontSize: 13,
              background: resultOk ? 'rgba(86,196,118,.12)' : 'rgba(245,101,101,.1)',
              border: `1px solid ${resultOk ? 'var(--accent-green)' : 'var(--accent-red)'}`,
              color: resultOk ? 'var(--accent-green)' : 'var(--accent-red)' }}>
              {result}
            </div>
          )}
          <button onClick={send} disabled={loading} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 24px', fontSize: 14, marginTop: 10 }}>{loading ? '發送中…' : `📤 批量發送`}</button>
        </Card>

        {showSent && sentAccounts.length > 0 && (
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '10px 16px', borderBottom: '1px solid var(--border)', background: 'var(--bg-input)' }}>
              <span style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-green)' }}>
                ✓ 已發送至以下 {sentAccounts.length} 位玩家
              </span>
              <button onClick={() => setShowSent(false)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer' }}>收起</button>
            </div>
            <div style={{ maxHeight: 220, overflowY: 'auto', display: 'flex', flexWrap: 'wrap', gap: 4, padding: 12 }}>
              {sentAccounts.map(acc => (
                <span key={acc} style={{ padding: '3px 10px', background: 'rgba(86,196,118,.12)', border: '1px solid rgba(86,196,118,.3)', borderRadius: 20, fontSize: 12, color: 'var(--accent-green)' }}>
                  {acc}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>
      <div style={{ width: 260, flexShrink: 0 }}>
        <Card title={`🛒 購物車（${cart.length} 種）`}>
          {cart.length === 0 ? <p style={{ color: 'var(--text-muted)', fontSize: 13, textAlign: 'center', padding: 16 }}>購物車為空</p>
            : <>{cart.map((c, i) => <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 0', borderBottom: '1px solid var(--border)', fontSize: 13 }}>
                <span style={{ color: 'var(--accent-blue)', fontWeight: 600, flex: 1 }}>#{c.itemId}{c.name ? ` ${c.name}` : ''}</span>
                <input type="number" value={c.qty} min={1} max={99} onChange={e => setCart(cart.map((cc, ii) => ii === i ? { ...cc, qty: +e.target.value || 1 } : cc))} style={{ width: 50, fontSize: 12, textAlign: 'center' }} />
                <button onClick={() => setCart(cart.filter((_, ii) => ii !== i))} style={{ color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer' }}>✕</button>
              </div>)}
              <button onClick={() => setCart([])} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginTop: 8, padding: 0 }}>清空</button></>}
        </Card>
      </div>
    </div>
  )
}

// ────────────────────────────────────────────────────────────
// Tab 3 — 批量金幣
// ────────────────────────────────────────────────────────────
function BatchGoldTab() {
  const [target, setTarget] = useState<'all' | 'online' | 'custom'>('online')
  const [customList, setCustomList] = useState('')
  const [searchKw, setSearchKw] = useState('')
  const [amount, setAmount] = useState(1000)
  const [loading, setLoading] = useState(false)
  const [playerList, setPlayerList] = useState<{ account: string; onlineName: string }[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [result, setResult] = useState('')

  const loadPlayers = async () => {
    if (target === 'custom') { const accounts = customList.split(/[\n,]/).map(s => s.trim()).filter(Boolean); setPlayerList(accounts.map(a => ({ account: a, onlineName: a }))); setSelected(new Set(accounts)); return }
    setLoading(true)
    try {
      const r = await api.get(target === 'online' ? '/players/online' : '/players/list', { params: target === 'all' ? { limit: 500 } : {} })
      setPlayerList((r.data as { account: string; onlineName?: string }[]).map(p => ({ account: p.account, onlineName: p.onlineName || p.account }))); setSelected(new Set())
    } catch { setResult('載入失敗') } finally { setLoading(false) }
  }
  const toggle = (a: string) => { const n = new Set(selected); n.has(a) ? n.delete(a) : n.add(a); setSelected(n) }

  const send = async () => {
    const ids = target === 'custom' ? customList.split(/[\n,]/).map(s => s.trim()).filter(Boolean) : Array.from(selected)
    if (ids.length === 0) { setResult('請先載入並勾選玩家'); return }
    setLoading(true); setResult('')
    try {
      const r = await api.post('/players/batch-gold', { target: target === 'custom' ? 'custom' : target, customList: target === 'custom' ? customList : '', accountIds: ids.join(','), amount })
      setResult(`✓ 成功 ${r.data.done} 人，失敗 ${r.data.fail} 人`)
    } catch { setResult('操作失敗') } finally { setLoading(false) }
  }

  return (
    <div style={{ maxWidth: 860 }}>
      <Card title="STEP 1 — 載入玩家範圍">
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 12 }}>
          {(['all', 'online', 'custom'] as const).map(t => (
            <label key={t} style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
              <input type="radio" checked={target === t} onChange={() => setTarget(t)} />
              <span>{{ all: '🌐 全服所有玩家', online: '🟢 僅在線玩家', custom: '📝 自訂清單' }[t]}</span>
            </label>
          ))}
        </div>
        {target === 'custom'
          ? <div><textarea value={customList} onChange={e => setCustomList(e.target.value)} placeholder="一行一個帳號" style={{ width: '100%', height: 80, marginBottom: 8, padding: 8, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, fontSize: 13 }} /><button onClick={loadPlayers} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 14px' }}>載入清單</button></div>
          : <div style={{ display: 'flex', gap: 8 }}><input value={searchKw} onChange={e => setSearchKw(e.target.value)} placeholder="關鍵字（選填）" style={{ width: 200 }} /><button onClick={loadPlayers} disabled={loading} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 14px' }}>{loading ? '載入中…' : '載入清單'}</button></div>}
      </Card>
      <Card title="STEP 2 — 金幣變動量">
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}><span style={{ color: 'var(--text-muted)', fontSize: 13 }}>數量：</span><input type="number" value={amount} onChange={e => setAmount(Number(e.target.value))} style={{ width: 120, textAlign: 'right' }} /></label>
          <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>（正數=加，負數=扣）</span>
        </div>
      </Card>
      {playerList.length > 0 && <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden', marginBottom: 14 }}>
        <div style={{ padding: '10px 16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>已選 {selected.size} / 共 {playerList.length} 人</span>
          <div style={{ display: 'flex', gap: 8 }}>
            <button onClick={() => setSelected(new Set(playerList.map(p => p.account)))} style={{ fontSize: 12, padding: '4px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)' }}>全選</button>
            <button onClick={() => setSelected(new Set())} style={{ fontSize: 12, padding: '4px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)' }}>全不選</button>
          </div>
        </div>
        <div style={{ maxHeight: 240, overflowY: 'auto' }}>
          {playerList.map(p => <div key={p.account} onClick={() => toggle(p.account)} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 16px', borderBottom: '1px solid var(--border)', cursor: 'pointer', background: selected.has(p.account) ? 'rgba(74,158,255,.1)' : 'transparent' }}>
            <input type="checkbox" checked={selected.has(p.account)} readOnly />
            <span style={{ fontWeight: 500 }}>{p.onlineName}</span>
            <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{p.account}</span>
          </div>)}
        </div>
      </div>}
      {result && <p style={{ color: result.startsWith('✓') ? 'var(--accent-green)' : 'var(--accent-red)', marginBottom: 12, fontSize: 13 }}>{result}</p>}
      <button onClick={send} disabled={loading} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '10px 24px', fontSize: 14 }}>
        {loading ? '執行中…' : `✓ 執行批量金幣（${amount >= 0 ? '+' : ''}${amount}）`}
      </button>
    </div>
  )
}

// ────────────────────────────────────────────────────────────
// 主頁面
// ────────────────────────────────────────────────────────────
export default function BatchOpsPage() {
  const [tab, setTab] = useState<MainTab>('single')

  const tabs: { key: MainTab; label: string }[] = [
    { key: 'single', label: '📬 道具給予（單人）' },
    { key: 'batch',  label: '📢 批量發送（多人）' },
    { key: 'gold',   label: '💰 批量金幣' },
  ]

  return (
    <div style={{ padding: 24 }}>
      <h1 style={{ fontSize: 20, fontWeight: 700, marginBottom: 16 }}>⚙️ 批量操作</h1>

      {/* Tab 列 */}
      <div style={{ display: 'flex', gap: 4, marginBottom: 20, borderBottom: '2px solid var(--border)' }}>
        {tabs.map(t => (
          <button key={t.key} onClick={() => setTab(t.key)} style={{
            padding: '9px 20px', fontSize: 13, fontWeight: tab === t.key ? 700 : 400,
            background: tab === t.key ? 'var(--accent-blue)' : 'transparent',
            color: tab === t.key ? '#fff' : 'var(--text-muted)',
            border: 'none', borderRadius: '6px 6px 0 0', cursor: 'pointer',
          }}>{t.label}</button>
        ))}
      </div>

      {tab === 'single' && <SingleSendTab />}
      {tab === 'batch'  && <BatchSendTab />}
      {tab === 'gold'   && <BatchGoldTab />}
    </div>
  )
}

// ── 共用小元件 ───────────────────────────────────────────────
const Card = ({ title, children }: { title: string; children: React.ReactNode }) => (
  <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 16, marginBottom: 14 }}>
    <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 10 }}>{title}</h3>
    {children}
  </div>
)

const Divider = () => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 8, margin: '8px 0' }}>
    <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
    <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>或直接輸入編號</span>
    <div style={{ flex: 1, height: 1, background: 'var(--border)' }} />
  </div>
)
