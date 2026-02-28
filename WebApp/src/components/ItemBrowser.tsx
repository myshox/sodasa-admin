import { useState, useEffect, useRef } from 'react'
import * as XLSX from 'xlsx'
import api from '../api'

export interface ItemInfo { id: number; name: string; desc: string; isPet: boolean }
interface CartItem { itemId: number; qty: number; type: number; name?: string }

const LS_ITEMS = 'gm_items_cache'
const LS_PETS  = 'gm_pets_cache'
const PAGE_SIZE = 50

// 從 API 載入（全域快取，避免每個元件重複請求）
let _apiLoaded = false
let _apiItems: ItemInfo[] = []
let _apiPets: ItemInfo[] = []
let _apiListeners: (() => void)[] = []

export async function loadItemsFromApi(): Promise<void> {
  try {
    const r = await api.get('/items')
    const d = r.data as { items: any[]; pets: any[] }
    const normalize = (i: any, isPet: boolean): ItemInfo => ({
      id:    i.id    ?? i.Id    ?? 0,
      name:  i.name  ?? i.Name  ?? '',
      desc:  i.desc  ?? i.Desc  ?? '',
      isPet,
    })
    _apiItems = (d.items || []).map(i => normalize(i, false))
    _apiPets  = (d.pets  || []).map(i => normalize(i, true))
    _apiLoaded = true
    // 同步到 localStorage
    try { localStorage.setItem(LS_ITEMS, JSON.stringify(_apiItems)) } catch { }
    try { localStorage.setItem(LS_PETS,  JSON.stringify(_apiPets)) }  catch { }
    _apiListeners.forEach(fn => fn())
  } catch {
    // API 失敗時用 localStorage 快取
    try { _apiItems = JSON.parse(localStorage.getItem(LS_ITEMS) || '[]') } catch { }
    try { _apiPets  = JSON.parse(localStorage.getItem(LS_PETS)  || '[]') } catch { }
    _apiLoaded = true
    _apiListeners.forEach(fn => fn())
  }
}

export function getApiItems(): ItemInfo[] { return _apiItems }
export function getApiPets():  ItemInfo[] { return _apiPets  }
export function subscribeItems(fn: () => void) { _apiListeners.push(fn); return () => { _apiListeners = _apiListeners.filter(f => f !== fn) } }

function parseXlsx(file: File): Promise<ItemInfo[]> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = (e) => {
      try {
        const wb = XLSX.read(e.target!.result, { type: 'array' })
        const ws = wb.Sheets[wb.SheetNames[0]]
        const rows = XLSX.utils.sheet_to_json<string[]>(ws, { header: 1 }) as string[][]
        const items: ItemInfo[] = []
        const detectRow = rows.find((r, i) => i > 0 && r.some(c => c))
        let idCol = 0, nameCol = 1, descCol = -1
        if (detectRow) {
          const isNum = (v: string) => /^\d+$/.test(String(v ?? '').trim())
          if (!isNum(String(detectRow[0] ?? ''))) {
            if (detectRow.length >= 3 && isNum(String(detectRow[2] ?? ''))) { nameCol = 0; descCol = 1; idCol = 2 }
            else { nameCol = 0; idCol = 1 }
          }
          if (idCol === 0) { descCol = detectRow.length >= 3 ? 2 : -1 }
        }
        for (let i = 1; i < rows.length; i++) {
          const row = rows[i]
          if (!row || row.length === 0) continue
          const name = String(row[nameCol] ?? '').trim()
          const id   = parseInt(String(row[idCol] ?? ''), 10)
          const desc = descCol >= 0 ? String(row[descCol] ?? '').trim() : ''
          if (!name || isNaN(id)) continue
          items.push({ id, name, desc, isPet: false })
        }
        resolve(items)
      } catch (err) { reject(err) }
    }
    reader.onerror = reject
    reader.readAsArrayBuffer(file)
  })
}

interface Props {
  cart: CartItem[]
  onAddToCart: (item: CartItem) => void
}

export default function ItemBrowser({ onAddToCart }: Props) {
  const [tab, setTab] = useState<'items' | 'pets'>('items')
  const [items, setItems] = useState<ItemInfo[]>(_apiItems)
  const [pets,  setPets]  = useState<ItemInfo[]>(_apiPets)
  const [q, setQ] = useState('')
  const [page, setPage] = useState(0)
  const [msg, setMsg] = useState('')
  const fileRef    = useRef<HTMLInputElement>(null)
  const petFileRef = useRef<HTMLInputElement>(null)

  // 訂閱 API 載入完成事件
  useEffect(() => {
    if (!_apiLoaded) { loadItemsFromApi() }
    return subscribeItems(() => { setItems([..._apiItems]); setPets([..._apiPets]) })
  }, [])

  const [gitSyncing, setGitSyncing] = useState(false)

  const handleUpload = async (file: File | undefined, isPet: boolean) => {
    if (!file) return
    setMsg('解析中…')
    try {
      const parsed = await parseXlsx(file)
      const data = parsed.map(i => ({ ...i, isPet }))

      const payload = isPet ? { pets: data } : { items: data }
      await api.post('/items/save', payload)

      if (isPet) { _apiPets = data; setPets([...data]) }
      else       { _apiItems = data; setItems([...data]) }

      setMsg(`✓ 已儲存 ${data.length} 筆${isPet ? '寵物' : '道具'}（請按「同步到 Git」永久保存）`)
      setPage(0)
    } catch (e) { setMsg('✗ 失敗：' + (e as Error).message) }
  }

  const handleGitSync = async () => {
    setGitSyncing(true); setMsg('同步中…')
    try {
      const r = await api.post('/items/git-sync')
      setMsg(`✓ ${r.data.message}`)
      setTimeout(() => setMsg(''), 5000)
    } catch { setMsg('✗ 同步失敗') }
    finally { setGitSyncing(false) }
  }

  const current = tab === 'items' ? items : pets
  const filtered = q.trim() ? current.filter(i => i.name.includes(q) || String(i.id).includes(q) || i.desc.includes(q)) : current
  const paged = filtered.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE)
  const totalPages = Math.ceil(filtered.length / PAGE_SIZE)

  return (
    <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
      <div style={{ display: 'flex', gap: 0, borderBottom: '1px solid var(--border)' }}>
        {(['items', 'pets'] as const).map(t => (
          <button key={t} onClick={() => { setTab(t); setPage(0); setQ('') }}
            style={{ flex: 1, padding: '10px 0', background: tab === t ? 'var(--bg-input)' : 'transparent', color: tab === t ? 'var(--accent-blue)' : 'var(--text-muted)', fontWeight: tab === t ? 700 : 400, border: 'none', cursor: 'pointer', fontSize: 13, borderBottom: tab === t ? '2px solid var(--accent-blue)' : 'none' }}>
            {t === 'items' ? `📦 道具 (${items.length})` : `🐾 寵物 (${pets.length})`}
          </button>
        ))}
      </div>

      <div style={{ padding: '8px 12px', background: 'var(--bg-input)', borderBottom: '1px solid var(--border)', display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="file" accept=".xlsx,.xls" ref={fileRef}    style={{ display: 'none' }} onChange={e => handleUpload(e.target.files?.[0], false)} />
        <input type="file" accept=".xlsx,.xls" ref={petFileRef} style={{ display: 'none' }} onChange={e => handleUpload(e.target.files?.[0], true)} />
        <button onClick={() => (tab === 'items' ? fileRef : petFileRef).current?.click()}
          style={{ fontSize: 12, padding: '4px 12px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>
          📂 上傳 {tab === 'items' ? 'items.xlsx' : 'pets.xlsx'}
        </button>
        <button onClick={handleGitSync} disabled={gitSyncing}
          style={{ fontSize: 12, padding: '4px 12px', background: gitSyncing ? 'var(--bg-input)' : 'var(--accent-green)', color: gitSyncing ? 'var(--text-muted)' : '#fff', border: '1px solid var(--border)', borderRadius: 4, cursor: gitSyncing ? 'default' : 'pointer' }}>
          {gitSyncing ? '同步中…' : '☁️ 同步到 Git'}
        </button>
        {msg
          ? <span style={{ fontSize: 12, color: msg.startsWith('✓') ? 'var(--accent-green)' : msg.startsWith('✗') ? 'var(--accent-red)' : 'var(--text-muted)' }}>{msg}</span>
          : current.length === 0
            ? <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>上傳後按「同步到 Git」永久保存</span>
            : <span style={{ fontSize: 11, color: 'var(--accent-green)' }}>✓ 已從伺服器載入（{current.length} 筆）</span>
        }
      </div>

      <div style={{ padding: '6px 12px', borderBottom: '1px solid var(--border)' }}>
        <input value={q} onChange={e => { setQ(e.target.value); setPage(0) }}
          placeholder={`搜尋${tab === 'items' ? '道具' : '寵物'} 名稱/編號…`}
          style={{ width: '100%', fontSize: 13 }} />
      </div>

      <div style={{ height: 320, overflowY: 'auto' }}>
        {current.length === 0
          ? <p style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)', fontSize: 13 }}>請上傳 Excel 檔案（上傳一次後自動儲存）</p>
          : paged.length === 0
            ? <p style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)', fontSize: 13 }}>無符合結果</p>
            : paged.map(item => (
              <div key={`${item.id}-${item.name}`} onClick={() => onAddToCart({ itemId: item.id, qty: 1, type: item.isPet ? 2 : 1, name: item.name })}
                style={{ display: 'flex', alignItems: 'center', padding: '6px 12px', borderBottom: '1px solid var(--border)', cursor: 'pointer', gap: 8 }}>
                <span style={{ color: 'var(--accent-blue)', fontWeight: 600, fontSize: 12, width: 50, flexShrink: 0 }}>#{item.id}</span>
                <span style={{ flex: 1, fontSize: 13 }}>{item.name}</span>
                {item.desc && <span style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 80, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{item.desc}</span>}
                <span style={{ fontSize: 11, color: 'var(--accent-green)', flexShrink: 0 }}>＋</span>
              </div>
            ))}
      </div>

      {totalPages > 1 && (
        <div style={{ display: 'flex', gap: 6, padding: '6px 12px', alignItems: 'center', borderTop: '1px solid var(--border)' }}>
          <button disabled={page === 0} onClick={() => setPage(p => p - 1)} style={{ fontSize: 12, padding: '3px 8px', background: 'var(--bg-input)', border: '1px solid var(--border)', opacity: page === 0 ? 0.4 : 1 }}>‹</button>
          <span style={{ fontSize: 12, color: 'var(--text-muted)', flex: 1, textAlign: 'center' }}>{page + 1} / {totalPages}（{filtered.length} 筆）</span>
          <button disabled={page >= totalPages - 1} onClick={() => setPage(p => p + 1)} style={{ fontSize: 12, padding: '3px 8px', background: 'var(--bg-input)', border: '1px solid var(--border)', opacity: page >= totalPages - 1 ? 0.4 : 1 }}>›</button>
        </div>
      )}
    </div>
  )
}
