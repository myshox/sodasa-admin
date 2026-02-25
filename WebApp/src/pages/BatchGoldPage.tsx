import { useState } from 'react'
import api from '../api'
import { S } from '../strings'

type Target = 'all' | 'online' | 'custom'

export default function BatchGoldPage() {
  const [target, setTarget] = useState<Target>('online')
  const [customList, setCustomList] = useState('')
  const [searchKw, setSearchKw] = useState('')
  const [amount, setAmount] = useState(1000)
  const [loading, setLoading] = useState(false)
  const [playerList, setPlayerList] = useState<{ account: string; onlineName: string }[]>([])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [result, setResult] = useState('')

  const loadPlayers = async () => {
    if (target === 'custom' && !customList.trim()) return
    if (target === 'all' || target === 'online') {
      setLoading(true)
      try {
        const r = await api.get(target === 'online' ? '/players/online' : '/players/list', { params: target === 'all' ? { limit: 500 } : {} })
        const rows = r.data as { account: string; onlineName?: string }[]
        setPlayerList(rows.map(p => ({ account: p.account, onlineName: p.onlineName || p.account })))
        setSelected(new Set())
      } catch {
        setResult('載入失敗')
      } finally { setLoading(false) }
      return
    }
    if (target === 'custom') {
      const accounts = customList.split(/[\n,]/).map(s => s.trim()).filter(Boolean)
      setPlayerList(accounts.map(account => ({ account, onlineName: account })))
      setSelected(new Set(accounts))
      return
    }
    if (searchKw.trim()) {
      setLoading(true)
      try {
        const r = await api.get('/players/search', { params: { q: searchKw, limit: 200 } })
        const rows = r.data as { account: string; onlineName?: string }[]
        setPlayerList(rows.map(p => ({ account: p.account, onlineName: p.onlineName || p.account })))
        setSelected(new Set())
      } catch {
        setResult('搜尋失敗')
      } finally { setLoading(false) }
    }
  }

  const toggle = (account: string) => {
    const next = new Set(selected)
    if (next.has(account)) next.delete(account); else next.add(account)
    setSelected(next)
  }
  const selectAll = () => setSelected(new Set(playerList.map(p => p.account)))
  const selectNone = () => setSelected(new Set())

  const send = async () => {
    const ids = target === 'custom' ? customList.split(/[\n,]/).map(s => s.trim()).filter(Boolean) : Array.from(selected)
    if (ids.length === 0) { setResult('請先載入並勾選要操作的玩家'); return }
    setLoading(true); setResult('')
    try {
      const r = await api.post('/players/batch-gold', {
        target: target === 'custom' ? 'custom' : target,
        customList: target === 'custom' ? customList : '',
        accountIds: ids.join(','),
        amount,
      })
      setResult(`✓ 成功 ${r.data.done} 人，失敗 ${r.data.fail} 人`)
    } catch {
      setResult('操作失敗')
    } finally { setLoading(false) }
  }

  return (
    <div style={{ padding: 28, maxWidth: 900 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>💰 {S.navBatchGold}</h1>

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 16 }}>
        <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>STEP 1 — 載入玩家範圍</h3>
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 12 }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
            <input type="radio" checked={target === 'all'} onChange={() => setTarget('all')} />
            <span>🌐 全服所有玩家</span>
          </label>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
            <input type="radio" checked={target === 'online'} onChange={() => setTarget('online')} />
            <span>🟢 僅在線玩家</span>
          </label>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
            <input type="radio" checked={target === 'custom'} onChange={() => setTarget('custom')} />
            <span>📝 自訂清單</span>
          </label>
        </div>
        {target === 'custom' ? (
          <div>
            <textarea value={customList} onChange={e => setCustomList(e.target.value)} placeholder="一行一個帳號" style={{ width: '100%', height: 80, marginBottom: 8, padding: 8, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 6, fontSize: 13 }} />
            <button onClick={loadPlayers} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 14px' }}>載入清單</button>
          </div>
        ) : (
          <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
            <input value={searchKw} onChange={e => setSearchKw(e.target.value)} placeholder="關鍵字搜尋（選填）" style={{ width: 200 }} />
            <button onClick={loadPlayers} disabled={loading} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '6px 14px' }}>{loading ? '載入中…' : '載入清單'}</button>
          </div>
        )}
      </div>

      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, padding: 20, marginBottom: 16 }}>
        <h3 style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 12 }}>STEP 2 — 金幣變動量</h3>
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>數量：</span>
            <input type="number" value={amount} onChange={e => setAmount(Number(e.target.value))} style={{ width: 120, textAlign: 'right' }} />
          </label>
          <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>（正數=加，負數=扣）</span>
        </div>
      </div>

      {playerList.length > 0 && (
        <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden', marginBottom: 16 }}>
          <div style={{ padding: '10px 16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>已選 {selected.size} / 共 {playerList.length} 人</span>
            <div style={{ display: 'flex', gap: 8 }}>
              <button onClick={selectAll} style={{ fontSize: 12, padding: '4px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)' }}>全選</button>
              <button onClick={selectNone} style={{ fontSize: 12, padding: '4px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)' }}>全不選</button>
            </div>
          </div>
          <div style={{ maxHeight: 280, overflowY: 'auto' }}>
            {playerList.map(p => (
              <div key={p.account} onClick={() => toggle(p.account)} style={{
                display: 'flex', alignItems: 'center', gap: 10, padding: '8px 16px', borderBottom: '1px solid var(--border)',
                cursor: 'pointer', background: selected.has(p.account) ? 'rgba(74,158,255,.1)' : 'transparent'
              }}>
                <input type="checkbox" checked={selected.has(p.account)} readOnly />
                <span style={{ fontWeight: 500 }}>{p.onlineName || p.account}</span>
                <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>{p.account}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {result && <p style={{ color: result.startsWith('✓') ? 'var(--accent-green)' : 'var(--accent-red)', marginBottom: 12, fontSize: 13 }}>{result}</p>}
      <button onClick={send} disabled={loading || (playerList.length > 0 && selected.size === 0 && target !== 'custom')} style={{ background: 'var(--accent-blue)', color: '#fff', padding: '10px 24px', fontSize: 14 }}>
        {loading ? '執行中…' : `✓ 執行批量金幣（${amount >= 0 ? '+' : ''}${amount}）`}
      </button>
    </div>
  )
}
