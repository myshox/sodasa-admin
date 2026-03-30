import { useState, useRef, useEffect } from 'react'
import { getApiItems, getApiPets, subscribeItems, loadItemsFromApi } from './ItemBrowser'
import type { ItemInfo } from './ItemBrowser'

interface Props {
  mode?: 'item' | 'pet' | 'both'
  onSelect: (item: ItemInfo) => void
  placeholder?: string
  style?: React.CSSProperties
}

export default function ItemAutocomplete({ mode = 'both', onSelect, placeholder, style }: Props) {
  const [q, setQ] = useState('')
  const [suggestions, setSuggestions] = useState<ItemInfo[]>([])
  const [open, setOpen] = useState(false)
  const [activeIdx, setActiveIdx] = useState(-1)
  const [, forceUpdate] = useState(0)
  const wrapRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (getApiItems().length === 0 && getApiPets().length === 0) loadItemsFromApi()
    return subscribeItems(() => forceUpdate(n => n + 1))
  }, [])

  useEffect(() => {
    const handler = (e: Event) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    document.addEventListener('pointerdown', handler)
    return () => {
      document.removeEventListener('mousedown', handler)
      document.removeEventListener('pointerdown', handler)
    }
  }, [])

  const search = (v: string) => {
    setQ(v)
    if (!v.trim()) { setSuggestions([]); setOpen(false); return }
    const items = mode !== 'pet'  ? getApiItems() : []
    const pets  = mode !== 'item' ? getApiPets()  : []
    const all = [...items, ...pets]
    const kw = v.trim().toLowerCase()
    const filtered = all.filter(i => i.name.toLowerCase().includes(kw) || String(i.id).includes(kw) || (i.desc && i.desc.toLowerCase().includes(kw))).slice(0, 15)
    setSuggestions(filtered)
    setOpen(filtered.length > 0)
    setActiveIdx(-1)
  }

  const select = (item: ItemInfo) => { onSelect(item); setOpen(false); setSuggestions([]); setQ('') }

  const handleKey = (e: React.KeyboardEvent) => {
    if (!open || suggestions.length === 0) return
    if (e.key === 'ArrowDown') { e.preventDefault(); setActiveIdx(i => Math.min(i + 1, suggestions.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActiveIdx(i => Math.max(i - 1, 0)) }
    else if (e.key === 'Enter' && activeIdx >= 0) { e.preventDefault(); select(suggestions[activeIdx]) }
    else if (e.key === 'Escape') { setOpen(false) }
  }

  const total = (mode !== 'pet' ? getApiItems().length : 0) + (mode !== 'item' ? getApiPets().length : 0)

  return (
    <div ref={wrapRef} style={{ position: 'relative', ...style }}>
      <input
        className="gm-search-input"
        value={q} onChange={e => search(e.target.value)} onKeyDown={handleKey}
        onFocus={() => suggestions.length > 0 && setOpen(true)}
        placeholder={total > 0 ? (placeholder || `🔍 搜尋道具/寵物名稱或編號… (${total} 筆)`) : `請先上傳 xlsx 清單`}
        style={{ width: '100%' }} autoComplete="off" enterKeyHint="search"
      />
      {open && suggestions.length > 0 && (
        <div style={{ position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 9999, background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 8, boxShadow: '0 4px 20px rgba(0,0,0,.5)', maxHeight: 360, overflowY: 'auto', marginTop: 2, touchAction: 'manipulation' }}>
          {suggestions.map((item, i) => (
            <div
              key={`${item.id}-${item.isPet}`}
              onPointerDown={e => {
                if (e.pointerType === 'mouse' && e.button !== 0) return
                e.preventDefault()
                select(item)
              }}
              data-suggestion-item
              title={`${item.name}${item.desc ? ` — ${item.desc}` : ''}`}
              style={{
                display: 'flex', alignItems: 'flex-start', gap: 8, padding: '12px 14px', cursor: 'pointer', fontSize: 14,
                minHeight: 48,
                background: i === activeIdx ? 'rgba(74,158,255,.18)' : 'transparent',
                borderBottom: i < suggestions.length - 1 ? '1px solid var(--border)' : 'none',
              }}
            >
              <span style={{ paddingTop: 2 }}>{item.isPet ? '🐾' : '📦'}</span>
              <span style={{ color: 'var(--accent-blue)', fontWeight: 700, width: 52, flexShrink: 0, fontSize: 12, lineHeight: 1.35 }}>#{item.id}</span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 600, lineHeight: 1.35, wordBreak: 'break-word', overflowWrap: 'anywhere' }}>{item.name}</div>
                {item.desc
                  ? <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 3, lineHeight: 1.35, wordBreak: 'break-word', overflowWrap: 'anywhere' }}>{item.desc}</div>
                  : null}
              </div>
              <span style={{ fontSize: 10, padding: '2px 6px', borderRadius: 10, flexShrink: 0, color: item.isPet ? 'var(--accent-green)' : 'var(--accent-blue)', background: item.isPet ? 'rgba(86,196,118,.15)' : 'rgba(74,158,255,.15)' }}>
                {item.isPet ? '寵物' : '道具'}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
