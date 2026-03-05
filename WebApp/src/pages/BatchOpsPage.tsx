import { useState, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import api from '../api'
import type { MailHistoryItem } from '../api'
import type { PlayerRow } from '../api'
import ItemBrowser from '../components/ItemBrowser'
import ItemAutocomplete from '../components/ItemAutocomplete'
import PlayerAutocomplete from '../components/PlayerAutocomplete'
import type { ItemInfo } from '../components/ItemBrowser'
import { getApiItems, getApiPets } from '../components/ItemBrowser'
import useIsMobile from '../hooks/useIsMobile'

type MainTab = 'single' | 'batch' | 'gold'
interface CartItem { itemId: number; qty: number; type: number; name?: string; buff3?: string }
interface MailRawEntry { id: number; type: number; buff1: string; buff2: string; rawData: string; buff3: string; sendTime: string; isRead: boolean; deleted: boolean }

// ────────────────────────────────────────────────────────────
// Tab 1 — 道具給予（單人）
// ────────────────────────────────────────────────────────────
function SingleSendTab() {
  const isMobile = useIsMobile()
  const [sp] = useSearchParams()
  const [playerQ, setPlayerQ] = useState(sp.get('account') || '')
  const [selectedAccount, setSelectedAccount] = useState(sp.get('account') || '')
  const [selectedName, setSelectedName] = useState(decodeURIComponent(sp.get('name') || sp.get('account') || ''))
  const [recipients, setRecipients] = useState<{account: string; name: string}[]>([])
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
  const [mailFull, setMailFull] = useState<Record<string, string>[]>([])
  const [showFull, setShowFull] = useState(false)
  const [schema, setSchema] = useState<Record<string, string>[]>([])
  const [showSchema, setShowSchema] = useState(false)
  const [sentSummary, setSentSummary] = useState<{ accounts: {account:string;name:string}[]; items: CartItem[] } | null>(null)

  useEffect(() => {
    const acc = sp.get('account')
    if (acc) {
      const name = sp.get('name') ? decodeURIComponent(sp.get('name')!) : acc
      setSelectedAccount(acc); setPlayerQ(acc); setSelectedName(name)
      setRecipients([{ account: acc, name }])
    }
  }, [sp])

  const addRecipient = (account: string, name: string) => {
    if (!account) return
    setRecipients(prev => prev.find(r => r.account === account) ? prev : [...prev, { account, name }])
  }
  const removeRecipient = (account: string) => setRecipients(prev => prev.filter(r => r.account !== account))

  const addToCart = (item: CartItem) =>
    setCart(prev => { const e = prev.find(c => c.itemId === item.itemId && c.type === item.type); return e ? prev.map(c => c.itemId === item.itemId && c.type === item.type ? { ...c, qty: c.qty + item.qty } : c) : [...prev, item] })
  const addManualToCart = () => { const id = parseInt(manualId, 10); if (!id || id <= 0) return; addToCart({ itemId: id, qty: manualQty, type: manualType }); setManualId('') }
  const addFromAutocomplete = (item: ItemInfo) => addToCart({ itemId: item.id, qty: 1, type: 1, name: item.name, buff3: item.desc })

  const loadHistory = async () => { if (!selectedAccount) return; setHistoryLoading(true); try { const r = await api.get(`/players/${selectedAccount}/mail-history`); setMailHistory(r.data); setShowHistory(true) } finally { setHistoryLoading(false) } }
  const loadRaw = async () => { if (!selectedAccount) return; setRawLoading(true); try { const r = await api.get(`/players/${selectedAccount}/mail-raw`); setMailRaw(r.data); setShowRaw(true) } finally { setRawLoading(false) } }

  const send = async () => {
    if (recipients.length === 0) { setResult('請先加入至少一位玩家'); return }
    if (cart.length === 0) { setResult('購物車為空，請加入道具'); return }
    setLoading(true); setResult('')
    try {
      const sentItems = [...cart]
      const cartPayload = cart.map(c => ({ itemId: c.itemId, qty: c.qty, type: c.type, name: c.name ?? '', buff3: c.buff3 ?? '' }))
      if (recipients.length === 1) {
        const r = await api.post('/players/send-cart', { account: recipients[0].account, cart: cartPayload, title: title.trim(), content: content.trim() })
        setResult(r.data.message || `已發送 ${r.data.success} 筆`)
      } else {
        const r = await api.post('/players/batch-send-cart', { target: 'custom', customList: recipients.map(r => r.account).join('\n'), cart: cartPayload, title: title.trim(), content: content.trim() })
        setResult(r.data.message || `已發送至 ${r.data.accounts?.length ?? 0} 人`)
      }
      setSentSummary({ accounts: [...recipients], items: sentItems })
      setCart([])
    } catch (e: unknown) { const err = e as { response?: { data?: { message?: string } } }; setResult(err.response?.data?.message || '發送失敗') }
    finally { setLoading(false) }
  }

  return (
    <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start', flexWrap: isMobile ? 'wrap' : 'nowrap' }}>
      <div style={{ width: isMobile ? '100%' : 340, flexShrink: 0 }}><ItemBrowser cart={cart} onAddToCart={addToCart} /></div>
      <div style={{ flex: 1, minWidth: 0, width: isMobile ? '100%' : undefined }}>
        {result && <div style={{ background: result.includes('失敗') || result.includes('請') ? 'rgba(245,101,101,.1)' : 'rgba(86,196,118,.15)', border: `1px solid ${result.includes('失敗') || result.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)'}`, borderRadius: 8, padding: '10px 16px', marginBottom: 12, color: result.includes('失敗') || result.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)', fontSize: 13 }}>{result}</div>}
        <Card title={`STEP 1 — 指定玩家（已選 ${recipients.length} 人）`}>
          <div style={{ display: 'flex', gap: 8 }}>
            <div style={{ flex: 1 }}>
              <PlayerAutocomplete value={playerQ} onChange={setPlayerQ}
                onSelect={p => { setSelectedAccount(p.account); setSelectedName(p.onlineName || p.account); setPlayerQ(p.onlineName || p.account) }}
                placeholder="搜尋帳號或角色名稱" />
            </div>
            <button onClick={() => { if (selectedAccount) { addRecipient(selectedAccount, selectedName); setPlayerQ(''); setSelectedAccount(''); setSelectedName('') } }}
              disabled={!selectedAccount}
              style={{ padding: '6px 14px', background: 'var(--accent-blue)', color: '#fff', borderRadius: 6, fontSize: 13, fontWeight: 600, opacity: selectedAccount ? 1 : 0.4 }}>
              ＋ 加入名單
            </button>
          </div>
          {recipients.length > 0 && (
            <div style={{ marginTop: 10, border: '1px solid var(--border)', borderRadius: 6, overflow: 'hidden' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '6px 10px', background: 'var(--bg-input)', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                <span style={{ color: 'var(--text-muted)' }}>已選 {recipients.length} 位玩家</span>
                <button onClick={() => setRecipients([])} style={{ fontSize: 11, color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer' }}>清空</button>
              </div>
              <div style={{ maxHeight: 120, overflowY: 'auto' }}>
                {recipients.map(r => (
                  <div key={r.account} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '5px 10px', borderBottom: '1px solid var(--border)', fontSize: 12 }}>
                    <span style={{ flex: 1, color: 'var(--text-primary)', fontWeight: 500 }}>{r.name}</span>
                    <span style={{ color: 'var(--text-muted)' }}>{r.account}</span>
                    <div style={{ display: 'flex', gap: 4 }}>
                      <button disabled={historyLoading} onClick={() => { setSelectedAccount(r.account); setSelectedName(r.name); loadHistory(); }} style={{ fontSize: 10, padding: '1px 6px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 3, cursor: 'pointer', color: 'var(--text-muted)' }}>📜</button>
                      <button disabled={rawLoading} onClick={() => { setSelectedAccount(r.account); loadRaw(); }} style={{ fontSize: 10, padding: '1px 6px', background: 'rgba(255,159,10,.1)', border: '1px solid var(--accent-orange)', borderRadius: 3, cursor: 'pointer', color: 'var(--accent-orange)' }}>🔬</button>
                      <button onClick={() => removeRecipient(r.account)} style={{ fontSize: 11, color: 'var(--accent-red)', background: 'none', border: 'none', cursor: 'pointer', padding: '0 2px' }}>✕</button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
          {recipients.length === 1 && (
            <div style={{ display: 'flex', gap: 4, marginTop: 6, flexWrap: 'wrap' }}>
              <button onClick={async () => { try { const r = await api.get(`/players/${recipients[0].account}/mail-full`); setMailFull(r.data); setShowFull(true) } catch { setResult('載入失敗') } }} style={{ fontSize: 11, padding: '2px 8px', background: 'rgba(139,92,246,.15)', border: '1px solid #8b5cf6', borderRadius: 4, color: '#8b5cf6' }}>🧬 完整欄位</button>
              <button onClick={async () => { try { const r = await api.get('/players/maildata-schema'); setSchema(r.data); setShowSchema(true) } catch { setResult('載入失敗') } }} style={{ fontSize: 11, padding: '2px 8px', background: 'rgba(139,92,246,.15)', border: '1px solid #8b5cf6', borderRadius: 4, color: '#8b5cf6' }}>📋 表結構</button>
              <button onClick={async () => { if (!window.confirm(`修正 ${recipients[0].name} 的舊版網頁郵件？`)) return; try { const r = await api.post('/players/fix-old-mails', { account: recipients[0].account }); setResult(r.data.message) } catch { setResult('修正失敗') } }} style={{ fontSize: 11, padding: '2px 8px', background: 'rgba(86,196,118,.15)', border: '1px solid var(--accent-green)', borderRadius: 4, color: 'var(--accent-green)' }}>🔧 修正舊郵件</button>
            </div>
          )}
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
          <button onClick={send} disabled={loading || recipients.length === 0 || cart.length === 0} style={{ width: '100%', background: 'var(--accent-blue)', color: '#fff', padding: '10px 0', fontSize: 14, borderRadius: 8, opacity: (recipients.length === 0 || cart.length === 0) ? 0.5 : 1 }}>
            {loading ? '發送中…' : recipients.length > 1 ? `📬 發送至 ${recipients.length} 位玩家` : `📬 發送至 ${recipients[0]?.name || '玩家'}`}
          </button>
        </Card>
        {sentSummary && (
          <Card title={`✅ 發送完成（${sentSummary.accounts.length} 位玩家）`}>
            <div style={{ marginBottom: 8, padding: '8px 12px', background: 'rgba(86,196,118,.12)', border: '1px solid var(--accent-green)', borderRadius: 6 }}>
              <div style={{ fontSize: 12, color: 'var(--accent-green)', fontWeight: 700, marginBottom: 4 }}>收件人：</div>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: 8 }}>
                {sentSummary.accounts.map(a => (
                  <span key={a.account} style={{ padding: '2px 8px', background: 'rgba(86,196,118,.2)', border: '1px solid rgba(86,196,118,.4)', borderRadius: 12, fontSize: 11, color: 'var(--accent-green)' }}>{a.name}</span>
                ))}
              </div>
              <div style={{ fontSize: 12, color: 'var(--text-secondary)', borderTop: '1px solid rgba(255,255,255,.05)', paddingTop: 6 }}>
                {sentSummary.items.map((c, i) => (
                  <div key={i} style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 0' }}>
                    <span>#{c.itemId}{c.name ? ` ${c.name}` : ''}</span>
                    <span style={{ color: 'var(--text-muted)' }}>× {c.qty}</span>
                  </div>
                ))}
              </div>
            </div>
            <button onClick={() => setSentSummary(null)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}>關閉</button>
          </Card>
        )}
        {showSchema && (
          <Card title="📋 maildata 表結構（所有欄位）">
            <button onClick={() => setShowSchema(false)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginBottom: 6, padding: 0 }}>收起</button>
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', fontSize: 11, borderCollapse: 'collapse', fontFamily: 'monospace' }}>
                <thead><tr style={{ background: 'var(--bg-dark)' }}>{['欄位名稱','型別','Null','Key','預設值','Extra'].map(h => <th key={h} style={{ padding: '4px 8px', textAlign: 'left', color: 'var(--text-muted)' }}>{h}</th>)}</tr></thead>
                <tbody>{schema.map((row, i) => <tr key={i} style={{ borderBottom: '1px solid var(--border)' }}>{Object.values(row).map((v, j) => <td key={j} style={{ padding: '3px 8px', color: j === 0 ? '#8b5cf6' : 'var(--text-secondary)' }}>{v}</td>)}</tr>)}</tbody>
              </table>
            </div>
          </Card>
        )}
        {showFull && mailFull.length > 0 && (
          <Card title="🧬 maildata 完整欄位（最新20筆）">
            <button onClick={() => setShowFull(false)} style={{ fontSize: 11, color: 'var(--text-muted)', background: 'none', border: 'none', cursor: 'pointer', marginBottom: 6, padding: 0 }}>收起</button>
            <div style={{ overflowX: 'auto', maxHeight: 400, overflowY: 'auto' }}>
              {mailFull.map((row, i) => (
                <div key={i} style={{ marginBottom: 12, padding: 10, background: 'var(--bg-input)', borderRadius: 6, fontSize: 11, fontFamily: 'monospace' }}>
                  <div style={{ fontWeight: 700, color: '#8b5cf6', marginBottom: 6 }}>記錄 #{row['id']} — check={row['check']} deleamill={row['deleamill']}</div>
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '2px 16px' }}>
                    {Object.entries(row).map(([k, v]) => (
                      <div key={k} style={{ display: 'flex', gap: 6 }}>
                        <span style={{ color: 'var(--text-muted)', minWidth: 80 }}>{k}:</span>
                        <span style={{ color: v === '(null)' || v === '' ? 'var(--text-muted)' : 'var(--text-primary)', fontWeight: ['type','data','buff3','check'].includes(k) ? 700 : 400 }}>{v || '(空)'}</span>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
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
  const isMobile = useIsMobile()
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
  // 排除名單
  const [excludeQ, setExcludeQ] = useState('')
  const [excluded, setExcluded] = useState<{account: string; name: string}[]>([])
  const [showExclude, setShowExclude] = useState(false)

  const addToCart = (item: CartItem) => setCart(prev => { const e = prev.find(c => c.itemId === item.itemId && c.type === item.type); return e ? prev.map(c => c.itemId === item.itemId && c.type === item.type ? { ...c, qty: c.qty + item.qty } : c) : [...prev, item] })
  const addManualToCart = () => { const id = parseInt(manualId, 10); if (!id || id <= 0) return; addToCart({ itemId: id, qty: manualQty, type: manualType }); setManualId('') }
  const addFromAutocomplete = (item: ItemInfo) => addToCart({ itemId: item.id, qty: 1, type: 1, name: item.name, buff3: item.desc })
  const toggleSelect = (acc: string) => { const s = new Set(selected); s.has(acc) ? s.delete(acc) : s.add(acc); setSelected(s) }
  const invertSelect = () => setSelected(new Set(searchList.map(p => p.account).filter(a => !selected.has(a))))
  const addExclude = (p: PlayerRow) => { if (!excluded.find(e => e.account === p.account)) setExcluded(prev => [...prev, { account: p.account, name: p.onlineName || p.account }]); setExcludeQ('') }
  const removeExclude = (acc: string) => setExcluded(prev => prev.filter(e => e.account !== acc))
  const btnStyle = (v: string) => ({ padding: '6px 14px', borderRadius: 6, fontSize: 13, fontWeight: 600, background: target === v ? 'var(--accent-blue)' : 'var(--bg-input)', color: target === v ? '#fff' : 'var(--text-secondary)', border: `1px solid ${target === v ? 'var(--accent-blue)' : 'var(--border)'}`, cursor: 'pointer' })

  const send = async () => {
    if (cart.length === 0) { setResult('請加入至少一種道具'); setResultOk(false); return }
    let targetStr = target, customListStr = custom
    if (target === 'search') {
      if (selected.size === 0) { setResult('請勾選至少一位玩家'); setResultOk(false); return }
      // 搜尋勾選：勾選的人 再扣掉排除名單
      const finalSelected = Array.from(selected).filter(a => !excluded.find(e => e.account === a))
      if (finalSelected.length === 0) { setResult('所有勾選玩家都被排除了'); setResultOk(false); return }
      targetStr = 'custom'; customListStr = finalSelected.join('\n')
    }
    const excludeList = excluded.map(e => e.account)
    const label = target === 'all' ? '全部玩家' : target === 'online' ? '在線玩家' : target === 'search' ? `${selected.size} 位玩家` : '自訂名單'
    const excludeNote = excludeList.length > 0 ? `\n排除：${excludeList.length} 人` : ''
    if (!window.confirm(`確認批量發送？\n目標：${label}${excludeNote}\n道具：${cart.length} 種`)) return
    setLoading(true); setResult(''); setSentAccounts([]); setShowSent(false)
    try {
      const r = await api.post('/players/batch-send-cart', {
        target: targetStr, customList: customListStr,
        cart: cart.map(c => ({ itemId: c.itemId, qty: c.qty, type: c.type, name: c.name ?? '', buff3: c.buff3 ?? '' })),
        title, content,
        excludeList: target !== 'search' ? excludeList : [], // search 模式已在前端過濾
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
    <div style={{ display: 'flex', gap: 16, alignItems: 'flex-start', flexWrap: isMobile ? 'wrap' : 'nowrap' }}>
      <div style={{ width: isMobile ? '100%' : 340, flexShrink: 0 }}><ItemBrowser cart={cart} onAddToCart={addToCart} /></div>
      <div style={{ flex: 1, minWidth: 0, width: isMobile ? '100%' : undefined }}>
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
              <PlayerAutocomplete value={searchQ} onChange={setSearchQ}
                onSelect={(p: PlayerRow) => { setSearchList(prev => prev.find(x => x.account === p.account) ? prev : [...prev, p]); setSelected(prev => { const s = new Set(prev); s.add(p.account); return s }); setSearchQ('') }}
                placeholder="搜尋玩家加入清單…" style={{ flex: 1 }} />
              <button onClick={async () => { setListLoading(true); try { const r = await api.get('/players/online'); setSearchList(r.data); setSelected(new Set(r.data.map((p: PlayerRow) => p.account))) } finally { setListLoading(false) } }} disabled={listLoading} style={{ ...btnStyle('online'), padding: '6px 10px', fontSize: 12 }}>{listLoading ? '載入…' : '載入在線'}</button>
              <button onClick={async () => { setListLoading(true); try { const r = await api.get('/players/list', { params: { limit: 500 } }); setSearchList(r.data); setSelected(new Set(r.data.map((p: PlayerRow) => p.account))) } finally { setListLoading(false) } }} disabled={listLoading} style={{ ...btnStyle('all'), padding: '6px 10px', fontSize: 12 }}>載入全部</button>
            </div>
            {searchList.length > 0 && <div style={{ border: '1px solid var(--border)', borderRadius: 6, overflow: 'hidden' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '6px 10px', background: 'var(--bg-input)', borderBottom: '1px solid var(--border)', fontSize: 12, flexWrap: 'wrap' }}>
                <span style={{ color: 'var(--text-muted)', flex: 1 }}>共 {searchList.length} 人，已勾選 {selected.size} 人</span>
                <button onClick={() => setSelected(new Set(searchList.map(p => p.account)))} style={{ fontSize: 11, padding: '2px 8px', background: 'rgba(74,158,255,.15)', border: '1px solid var(--accent-blue)', borderRadius: 4, cursor: 'pointer', color: 'var(--accent-blue)' }}>全選</button>
                <button onClick={invertSelect} style={{ fontSize: 11, padding: '2px 8px', background: 'rgba(246,173,85,.15)', border: '1px solid var(--accent-orange)', borderRadius: 4, cursor: 'pointer', color: 'var(--accent-orange)' }}>反選</button>
                <button onClick={() => setSelected(new Set())} style={{ fontSize: 11, padding: '2px 8px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>清除</button>
              </div>
              <div style={{ maxHeight: 200, overflowY: 'auto' }}>
                {searchList.map(p => {
                  const isExcluded = excluded.some(e => e.account === p.account)
                  return (
                    <div key={p.account} onClick={() => !isExcluded && toggleSelect(p.account)}
                      style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 10px', borderBottom: '1px solid var(--border)', cursor: isExcluded ? 'not-allowed' : 'pointer', background: isExcluded ? 'rgba(245,101,101,.05)' : selected.has(p.account) ? 'rgba(74,158,255,.1)' : 'transparent', fontSize: 12, opacity: isExcluded ? 0.5 : 1 }}>
                      <input type="checkbox" checked={selected.has(p.account) && !isExcluded} onChange={() => {}} style={{ pointerEvents: 'none' }} />
                      <span style={{ fontSize: 11 }}>{p.isOnline ? '🟢' : '⚫'}</span>
                      <span style={{ fontWeight: 600, flex: 1 }}>{p.onlineName || p.account}</span>
                      {isExcluded && <span style={{ fontSize: 10, color: 'var(--accent-red)', background: 'rgba(245,101,101,.15)', padding: '1px 6px', borderRadius: 10 }}>已排除</span>}
                    </div>
                  )
                })}
              </div>
            </div>}
          </div>}

          {/* ── 排除名單（全部/在線/搜尋勾選皆可用）── */}
          {target !== 'custom' && (
            <div style={{ marginTop: 10 }}>
              <button onClick={() => setShowExclude(v => !v)}
                style={{ fontSize: 11, padding: '3px 10px', background: excluded.length > 0 ? 'rgba(245,101,101,.15)' : 'var(--bg-input)', border: `1px solid ${excluded.length > 0 ? 'var(--accent-red)' : 'var(--border)'}`, borderRadius: 5, cursor: 'pointer', color: excluded.length > 0 ? 'var(--accent-red)' : 'var(--text-muted)' }}>
                🚫 排除名單{excluded.length > 0 ? `（${excluded.length} 人）` : ''}
              </button>
              {showExclude && (
                <div style={{ marginTop: 8, padding: '10px 12px', background: 'rgba(245,101,101,.05)', border: '1px solid rgba(245,101,101,.25)', borderRadius: 8 }}>
                  <div style={{ fontSize: 11, color: 'var(--accent-red)', marginBottom: 8 }}>排除名單中的玩家不會收到道具（即使在全部/在線名單中）</div>
                  <PlayerAutocomplete value={excludeQ} onChange={setExcludeQ}
                    onSelect={(p: PlayerRow) => addExclude(p)}
                    placeholder="搜尋要排除的玩家…" style={{ marginBottom: 8 }} />
                  {excluded.length > 0 && (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                      {excluded.map(e => (
                        <span key={e.account} style={{ display: 'flex', alignItems: 'center', gap: 4, padding: '3px 8px', background: 'rgba(245,101,101,.15)', border: '1px solid rgba(245,101,101,.4)', borderRadius: 12, fontSize: 11 }}>
                          <span style={{ color: 'var(--text-primary)' }}>{e.name}</span>
                          <button onClick={() => removeExclude(e.account)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--accent-red)', padding: 0, fontSize: 13, minHeight: 0 }}>×</button>
                        </span>
                      ))}
                      <button onClick={() => setExcluded([])} style={{ fontSize: 11, padding: '2px 8px', background: 'none', border: '1px solid var(--border)', borderRadius: 10, cursor: 'pointer', color: 'var(--text-muted)' }}>清空</button>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
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
      <div style={{ width: isMobile ? '100%' : 260, flexShrink: 0 }}>
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
  const isMobile = useIsMobile()
  const [tab, setTab] = useState<MainTab>('single')

  const tabs: { key: MainTab; label: string; desc: string }[] = [
    { key: 'single', label: '📬 道具給予',   desc: '搜尋玩家，加入道具後以郵件發送' },
    { key: 'batch',  label: '📢 批量發送',   desc: '一次發送給全服、在線玩家或指定多人' },
    { key: 'gold',   label: '💰 批量金幣',   desc: '對多位玩家同時加減金幣' },
  ]

  return (
    <div style={{ padding: isMobile ? 12 : 24 }}>
      {/* 標題 */}
      <div style={{ marginBottom: 16 }}>
        <h1 style={{ fontSize: isMobile ? 18 : 22, fontWeight: 800, margin: 0,
          background: 'linear-gradient(135deg,#fb923c,#fbbf24)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
          📦 批量工具
        </h1>
        <p style={{ margin: '4px 0 0', fontSize: 12, color: 'var(--text-muted)' }}>
          道具給予單人 · 批量發送全服 · 批量調整金幣 — 請先選擇下方 Tab
        </p>
      </div>

      {/* Tab 列 */}
      <div style={{ display: 'flex', gap: 4, marginBottom: 20, borderBottom: '2px solid var(--border)', overflowX: 'auto', flexShrink: 0 }}>
        {tabs.map(t => (
          <button key={t.key} onClick={() => setTab(t.key)} title={t.desc} style={{
            padding: isMobile ? '9px 14px' : '9px 20px', fontSize: 13, fontWeight: tab === t.key ? 700 : 400,
            background: tab === t.key ? 'var(--accent-blue)' : 'transparent',
            color: tab === t.key ? '#fff' : 'var(--text-muted)',
            border: 'none', borderRadius: '6px 6px 0 0', cursor: 'pointer', whiteSpace: 'nowrap',
          }}>{t.label}</button>
        ))}
      </div>

      {tab === 'single' && <SingleSendTab />}
      {tab === 'batch'  && <BatchSendTab />}
      {tab === 'gold'   && <BatchGoldTab />}

      {/* ── 維護工具列 ── */}
      <GlobalFixBar />
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

function GlobalFixBar() {
  const [msg, setMsg] = useState('')
  const [loading, setLoading] = useState(false)
  const [clearMailLoading, setClearMailLoading] = useState(false)
  const [clearMailMsg, setClearMailMsg] = useState('')

  const doClearAllMail = async (unclaimedOnly: boolean) => {
    const label = unclaimedOnly ? '未領取郵件' : '全部郵件'
    if (!window.confirm(`確定清除全服所有玩家的${label}？\n此操作不可逆，請謹慎操作！`)) return
    setClearMailLoading(true); setClearMailMsg('')
    try {
      const r = await api.post('/players/clear-all-mail', { unclaimedOnly })
      setClearMailMsg(r.data.message || '清除完成')
    } catch { setClearMailMsg('清除失敗') }
    finally { setClearMailLoading(false) }
  }

  const buildItemDescs = () => {
    // 合併道具 + 寵物的 {itemId, desc} 清單（只傳 desc 非空的）
    const all = [...getApiItems(), ...getApiPets()]
    return all.filter(i => i.desc).map(i => ({ itemId: i.id, desc: i.desc }))
  }

  const doFix = async (account = '') => {
    const descs = buildItemDescs()
    const label = account ? `玩家 ${account}` : '全伺服器'
    const descInfo = descs.length > 0 ? `\n已載入 ${descs.length} 種道具描述，將逐一比對回填` : '\n⚠ 尚未載入 items.xlsx，只能用資料庫內既有記錄回填（可能不完整）'
    if (!window.confirm(`確定要修正${label}所有 buff3 為空的舊郵件？${descInfo}`)) return
    setLoading(true); setMsg('')
    try {
      const r = await api.post('/players/fix-old-mails', { account, itemDescriptions: descs })
      setMsg(r.data.message || '完成')
    } catch { setMsg('修正失敗') }
    finally { setLoading(false) }
  }

  const descCount = buildItemDescs().length

  return (
    <div style={{ marginTop: 24, padding: '14px 18px', background: 'rgba(245,101,101,.06)', border: '1px solid rgba(245,101,101,.25)', borderRadius: 10 }}>
      <div style={{ fontSize: 12, color: 'var(--accent-red)', fontWeight: 700, marginBottom: 10 }}>🔧 維護工具</div>

      {/* 舊郵件修正 */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap', marginBottom: 10 }}>
        <button onClick={() => doFix('')} disabled={loading}
          style={{ fontSize: 12, padding: '6px 14px', background: 'rgba(245,101,101,.15)', border: '1px solid rgba(245,101,101,.4)', borderRadius: 6, color: 'var(--accent-red)', cursor: 'pointer' }}>
          {loading ? '處理中…' : `修正全服舊郵件 buff3（讓舊道具可領取）`}
        </button>
        {descCount > 0
          ? <span style={{ fontSize: 11, color: 'var(--accent-green)' }}>✓ 已載入 {descCount} 種道具描述，可完整修復</span>
          : <span style={{ fontSize: 11, color: 'var(--accent-orange)' }}>⚠ 請先在左側載入 items.xlsx 以確保所有道具都能修復</span>}
        {msg && <div style={{ width: '100%', fontSize: 12, color: msg.includes('失敗') ? 'var(--accent-red)' : 'var(--accent-green)', whiteSpace: 'pre-line' }}>{msg}</div>}
      </div>

      {/* 清除全服郵件 */}
      <div style={{ borderTop: '1px solid rgba(245,101,101,.2)', paddingTop: 10 }}>
        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 8 }}>
          🗑 一鍵清除全服遊戲內郵件（軟刪除，玩家信箱會清空）
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <button onClick={() => doClearAllMail(true)} disabled={clearMailLoading}
            style={{ fontSize: 12, padding: '6px 14px', background: 'rgba(245,159,10,.12)', border: '1px solid var(--accent-orange)', borderRadius: 6, color: 'var(--accent-orange)', cursor: 'pointer', opacity: clearMailLoading ? 0.5 : 1 }}>
            {clearMailLoading ? '處理中…' : '清除全服未領取郵件'}
          </button>
          <button onClick={() => doClearAllMail(false)} disabled={clearMailLoading}
            style={{ fontSize: 12, padding: '6px 14px', background: 'rgba(245,101,101,.15)', border: '1px solid rgba(245,101,101,.5)', borderRadius: 6, color: 'var(--accent-red)', cursor: 'pointer', opacity: clearMailLoading ? 0.5 : 1 }}>
            {clearMailLoading ? '處理中…' : '清除全服所有郵件'}
          </button>
        </div>
        {clearMailMsg && (
          <div style={{ marginTop: 6, fontSize: 12, color: clearMailMsg.includes('失敗') ? 'var(--accent-red)' : 'var(--accent-green)' }}>
            {clearMailMsg}
          </div>
        )}
      </div>
    </div>
  )
}
