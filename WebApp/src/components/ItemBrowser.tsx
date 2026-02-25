import { useState, useEffect, useRef } from 'react'
import * as XLSX from 'xlsx'

export interface ItemInfo { id: number; name: string; desc: string; isPet: boolean }
interface CartItem { itemId: number; qty: number; type: number; name?: string }

const LS_ITEMS = 'gm_items_cache'
const LS_PETS  = 'gm_pets_cache'
const PAGE_SIZE = 50

function parseXlsx(file: File): Promise<ItemInfo[]> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = (e) => {
      try {
        const data = e.target!.result
        const wb = XLSX.read(data, { type: 'array' })
        const ws = wb.Sheets[wb.SheetNames[0]]
        const rows = XLSX.utils.sheet_to_json<string[]>(ws, { header: 1 }) as string[][]
        const items: ItemInfo[] = []
        // Auto-detect column order like GameDataManager.cs
        // Check first data row: if col1 is numeric → [ID, Name, ...]
        //                        if col3 is numeric → [Name, Desc, ID]
        //                        else               → [Name, ID]
        const detectRow = rows.find((r, i) => i > 0 && r.some(c => c))
        let idCol = 0, nameCol = 1, descCol = -1
        if (detectRow) {
          const isNum = (v: string) => /^\d+$/.test(String(v ?? '').trim())
          if (!isNum(String(detectRow[0] ?? ''))) {
            if (detectRow.length >= 3 && isNum(String(detectRow[2] ?? ''))) {
              // [Name, Desc, ID]
              nameCol = 0; descCol = 1; idCol = 2
            } else {
              // [Name, ID]
              nameCol = 0; idCol = 1
            }
          }
          // else: [ID, Name, Desc?]
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

function loadCache(key: string): ItemInfo[] {
  try { return JSON.parse(localStorage.getItem(key) || '[]') } catch { return [] }
}
function saveCache(key: string, data: ItemInfo[]) {
  try { localStorage.setItem(key, JSON.stringify(data)) } catch { }
}

interface Props {
  cart: CartItem[]
  onAddToCart: (item: CartItem) => void
}

export default function ItemBrowser({ cart, onAddToCart }: Props) {
  const [tab, setTab] = useState<'items' | 'pets'>('items')
  const [items, setItems] = useState<ItemInfo[]>([])
  const [pets, setPets] = useState<ItemInfo[]>([])
  const [q, setQ] = useState('')
  const [page, setPage] = useState(0)
  const [uploadMsg, setUploadMsg] = useState('')
  const fileRef = useRef<HTMLInputElement>(null)
  const petFileRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    setItems(loadCache(LS_ITEMS).map(i => ({ ...i, isPet: false })))
    setPets(loadCache(LS_PETS).map(i => ({ ...i, isPet: true })))
  }, [])

  const handleUpload = async (file: File | undefined, isPet: boolean) => {
    if (!file) return
    setUploadMsg('解析中…')
    try {
      const parsed = await parseXlsx(file)
      const data = parsed.map(i => ({ ...i, isPet }))
      if (isPet) { setPets(data); saveCache(LS_PETS, data) }
      else       { setItems(data); saveCache(LS_ITEMS, data) }
      setUploadMsg(`✓ 已載入 ${data.length} 筆${isPet ? '寵物' : '道具'}`)
      setTimeout(() => setUploadMsg(''), 3000)
      setPage(0)
    } catch (e) {
      setUploadMsg('✗ 解析失敗：' + (e as Error).message)
    }
  }

  const current = tab === 'items' ? items : pets
  const filtered = q.trim()
    ? current.filter(i => i.name.includes(q) || String(i.id).includes(q) || i.desc.includes(q))
    : current
  const paged = filtered.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE)
  const totalPages = Math.ceil(filtered.length / PAGE_SIZE)

  const handleAdd = (item: ItemInfo) => {
    const defaultType = item.isPet ? 2 : 1
    const existing = cart.find(c => c.itemId === item.id && c.type === defaultType)
    if (existing) {
      // signal to parent to increment
      onAddToCart({ itemId: item.id, qty: 1, type: defaultType, name: item.name })
    } else {
      onAddToCart({ itemId: item.id, qty: 1, type: defaultType, name: item.name })
    }
  }

  return (
    <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
      {/* 標籤切換 */}
      <div style={{ display: 'flex', gap: 0, borderBottom: '1px solid var(--border)' }}>
        {(['items', 'pets'] as const).map(t => (
          <button key={t} onClick={() => { setTab(t); setPage(0); setQ('') }}
            style={{ flex: 1, padding: '10px 0', background: tab === t ? 'var(--bg-input)' : 'transparent', color: tab === t ? 'var(--accent-blue)' : 'var(--text-muted)', fontWeight: tab === t ? 700 : 400, border: 'none', cursor: 'pointer', fontSize: 13, borderBottom: tab === t ? '2px solid var(--accent-blue)' : 'none' }}>
            {t === 'items' ? `📦 道具清單 (${items.length})` : `🐾 寵物清單 (${pets.length})`}
          </button>
        ))}
      </div>

      {/* 上傳 Excel */}
      <div style={{ padding: '8px 12px', background: 'var(--bg-input)', borderBottom: '1px solid var(--border)', display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
        <input type="file" accept=".xlsx,.xls" ref={fileRef} style={{ display: 'none' }}
          onChange={e => handleUpload(e.target.files?.[0], false)} />
        <input type="file" accept=".xlsx,.xls" ref={petFileRef} style={{ display: 'none' }}
          onChange={e => handleUpload(e.target.files?.[0], true)} />
        <button onClick={() => (tab === 'items' ? fileRef : petFileRef).current?.click()}
          style={{ fontSize: 12, padding: '4px 12px', background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 4, cursor: 'pointer' }}>
          📂 上傳 {tab === 'items' ? 'items.xlsx' : 'pets.xlsx'}
        </button>
        {uploadMsg && <span style={{ fontSize: 12, color: uploadMsg.startsWith('✓') ? 'var(--accent-green)' : 'var(--accent-red)' }}>{uploadMsg}</span>}
        {current.length === 0 && <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>請上傳 Excel 道具/寵物清單以啟用搜尋</span>}
      </div>

      {/* 搜尋 */}
      <div style={{ padding: '6px 12px', borderBottom: '1px solid var(--border)' }}>
        <input value={q} onChange={e => { setQ(e.target.value); setPage(0) }}
          placeholder={`搜尋${tab === 'items' ? '道具' : '寵物'} 名稱/編號/說明…`}
          style={{ width: '100%', fontSize: 13 }} />
      </div>

      {/* 清單 */}
      <div style={{ height: 320, overflowY: 'auto' }}>
        {current.length === 0
          ? <p style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)', fontSize: 13 }}>請上傳 Excel 檔案載入清單</p>
          : paged.length === 0
            ? <p style={{ padding: 20, textAlign: 'center', color: 'var(--text-muted)', fontSize: 13 }}>無符合結果</p>
            : paged.map(item => (
              <div key={`${item.id}-${item.name}`}
                onClick={() => handleAdd(item)}
                style={{ display: 'flex', alignItems: 'center', padding: '6px 12px', borderBottom: '1px solid var(--border)', cursor: 'pointer', gap: 8 }}>
                <span style={{ color: 'var(--accent-blue)', fontWeight: 600, fontSize: 12, width: 50, flexShrink: 0 }}>#{item.id}</span>
                <span style={{ flex: 1, fontSize: 13, color: 'var(--text-primary)' }}>{item.name}</span>
                {item.desc && <span style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 80, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{item.desc}</span>}
                <span style={{ fontSize: 11, color: 'var(--accent-green)', flexShrink: 0 }}>＋加入</span>
              </div>
            ))}
      </div>

      {/* 翻頁 */}
      {totalPages > 1 && (
        <div style={{ display: 'flex', gap: 6, padding: '6px 12px', alignItems: 'center', borderTop: '1px solid var(--border)' }}>
          <button disabled={page === 0} onClick={() => setPage(p => p - 1)}
            style={{ fontSize: 12, padding: '3px 8px', background: 'var(--bg-input)', border: '1px solid var(--border)', opacity: page === 0 ? 0.4 : 1 }}>‹</button>
          <span style={{ fontSize: 12, color: 'var(--text-muted)', flex: 1, textAlign: 'center' }}>
            {page + 1} / {totalPages}（{filtered.length} 筆）
          </span>
          <button disabled={page >= totalPages - 1} onClick={() => setPage(p => p + 1)}
            style={{ fontSize: 12, padding: '3px 8px', background: 'var(--bg-input)', border: '1px solid var(--border)', opacity: page >= totalPages - 1 ? 0.4 : 1 }}>›</button>
        </div>
      )}
    </div>
  )
}
