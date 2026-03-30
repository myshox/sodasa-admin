import { useState } from 'react'
import api from '../api'
import { S } from '../strings'

export default function ItemQueuePage() {
  const [playerQ, setPlayerQ] = useState('')
  const [selectedAccount, setSelectedAccount] = useState('')
  const [selectedName, setSelectedName] = useState('')
  const [itemId, setItemId] = useState('')
  const [itemName, setItemName] = useState('')
  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [quantity, setQuantity] = useState(1)
  const [loading, setLoading] = useState(false)
  const [msg, setMsg] = useState('')
  const [searchResults, setSearchResults] = useState<{ account: string; onlineName: string }[]>([])

  const searchPlayer = async () => {
    if (!playerQ.trim()) return
    setLoading(true); setMsg('')
    try {
      const r = await api.get('/players/search', { params: { q: playerQ.trim(), limit: 20 } })
      setSearchResults(r.data)
      if (r.data.length === 0) setMsg('找不到玩家')
    } catch {
      setMsg('搜尋失敗，請確認後端 API 已啟動')
    } finally { setLoading(false) }
  }

  const sendItem = async () => {
    if (!selectedAccount) { setMsg('請先搜尋並選擇要發送的玩家'); return }
    const id = parseInt(itemId, 10)
    if (isNaN(id) || id < 0) { setMsg('請輸入有效的道具編號（數字）'); return }
    setLoading(true); setMsg('')
    try {
      const r = await api.post('/players/send-item', {
        account: selectedAccount,
        itemId: id,
        itemName: itemName.trim() || `道具#${id}`,
        title: title.trim() || undefined,
        content: content.trim() || undefined,
        quantity: Math.max(1, Math.min(999, quantity || 1)),
      })
      setMsg(r.data.message || '發送成功')
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err.response?.data?.message || '發送失敗')
    } finally { setLoading(false) }
  }

  return (
    <div className="gm-page-stack gm-max-md">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 8 }}>📬 {S.navItemQueue}</h1>
      <p style={{ color: 'var(--text-muted)', fontSize: 13, marginBottom: 20 }}>
        發送道具至玩家信箱（maildata），玩家重新登入後在信件欄領取。
      </p>

      {msg && (
        <div style={{
          marginBottom: 16, padding: '10px 14px', borderRadius: 8,
          background: msg.includes('失敗') || msg.includes('請') ? 'rgba(245,101,101,.1)' : 'rgba(86,196,118,.15)',
          border: `1px solid ${msg.includes('失敗') || msg.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)'}`,
          color: msg.includes('失敗') || msg.includes('請') ? 'var(--accent-red)' : 'var(--accent-green)',
          fontSize: 13
        }}>{msg}</div>
      )}

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 20 }}>
        <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>STEP 1 — 選擇玩家</h3>
        <div className="gm-search-bar gm-search-bar--tight">
          <div className="gm-search-bar__grow">
            <input
              className="gm-search-input"
              value={playerQ}
              onChange={e => setPlayerQ(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && searchPlayer()}
              placeholder="輸入帳號或角色名稱搜尋"
              enterKeyHint="search"
            />
          </div>
          <div className="gm-search-bar__actions">
            <button type="button" onClick={searchPlayer} disabled={loading} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '10px 20px', borderRadius: 10, fontWeight: 700 }}>
              {loading ? S.searching : `🔍 ${S.searchBtn}`}
            </button>
          </div>
        </div>
        {searchResults.length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
            {searchResults.map(p => (
              <button key={p.account} onClick={() => { setSelectedAccount(p.account); setSelectedName(p.onlineName || p.account) }}
                style={{
                  padding: '8px 14px', borderRadius: 8, fontSize: 13,
                  background: selectedAccount === p.account ? 'rgba(74,158,255,.25)' : 'var(--bg-input)',
                  border: `1px solid ${selectedAccount === p.account ? 'var(--accent-blue)' : 'var(--border)'}`,
                  color: 'var(--text-primary)'
                }}>
                {p.onlineName || p.account}（{p.account}）
              </button>
            ))}
          </div>
        )}
        {selectedAccount && (
          <p style={{ marginTop: 12, fontSize: 13, color: 'var(--accent-green)' }}>✓ 已選：{selectedName}（{selectedAccount}）</p>
        )}
      </div>

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 20 }}>
        <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>STEP 2 — 道具與數量</h3>
        <div style={{ display: 'grid', gap: 12 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <label style={{ minWidth: 80, fontSize: 13 }}>道具編號</label>
            <input type="number" value={itemId} onChange={e => setItemId(e.target.value)} placeholder="必填，數字" style={{ width: 120 }} />
            <label style={{ minWidth: 60, fontSize: 13 }}>數量</label>
            <input type="number" min={1} max={999} value={quantity} onChange={e => setQuantity(Number(e.target.value) || 1)} style={{ width: 80 }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <label style={{ minWidth: 80, fontSize: 13 }}>道具名稱</label>
            <input value={itemName} onChange={e => setItemName(e.target.value)} placeholder="選填，顯示用" style={{ flex: 1, maxWidth: 200 }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <label style={{ minWidth: 80, fontSize: 13 }}>信件標題</label>
            <input value={title} onChange={e => setTitle(e.target.value)} placeholder="選填，預設為道具名稱" style={{ flex: 1, maxWidth: 280 }} />
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <label style={{ minWidth: 80, fontSize: 13 }}>信件內容</label>
            <input value={content} onChange={e => setContent(e.target.value)} placeholder="選填" style={{ flex: 1, maxWidth: 280 }} />
          </div>
        </div>
      </div>

      <button onClick={sendItem} disabled={loading || !selectedAccount || !itemId.trim()}
        style={{ background: 'var(--accent-blue)', color: '#fff', padding: '10px 24px', fontSize: 14 }}>
        {loading ? '發送中…' : '📬 發送道具至玩家信箱'}
      </button>
    </div>
  )
}
