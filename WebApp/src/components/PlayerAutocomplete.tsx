import { useState, useEffect, useRef } from 'react'
import api from '../api'
import type { PlayerRow } from '../api'

interface Props {
  value: string
  onChange: (v: string) => void
  onSelect: (p: PlayerRow) => void
  placeholder?: string
  style?: React.CSSProperties
}

export default function PlayerAutocomplete({ value, onChange, onSelect, placeholder, style }: Props) {
  const [suggestions, setSuggestions] = useState<PlayerRow[]>([])
  const [open, setOpen] = useState(false)
  const [activeIdx, setActiveIdx] = useState(-1)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const wrapRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!value.trim() || value.length < 1) { setSuggestions([]); setOpen(false); return }
    clearTimeout(timer.current)
    timer.current = setTimeout(async () => {
      try {
        const r = await api.get('/players/search', { params: { q: value.trim(), limit: 10 } })
        setSuggestions(r.data)
        setOpen(r.data.length > 0)
        setActiveIdx(-1)
      } catch { setSuggestions([]); setOpen(false) }
    }, 280)
    return () => { clearTimeout(timer.current) }
  }, [value])

  // 點外面關閉
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const select = (p: PlayerRow) => {
    onChange(p.onlineName || p.account)
    onSelect(p)
    setOpen(false)
    setSuggestions([])
  }

  const handleKey = (e: React.KeyboardEvent) => {
    if (!open || suggestions.length === 0) return
    if (e.key === 'ArrowDown') { e.preventDefault(); setActiveIdx(i => Math.min(i + 1, suggestions.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActiveIdx(i => Math.max(i - 1, 0)) }
    else if (e.key === 'Enter' && activeIdx >= 0) { e.preventDefault(); select(suggestions[activeIdx]) }
    else if (e.key === 'Escape') { setOpen(false) }
  }

  return (
    <div ref={wrapRef} style={{ position: 'relative', ...style }}>
      <input
        value={value}
        onChange={e => { onChange(e.target.value); setOpen(true) }}
        onKeyDown={handleKey}
        onFocus={() => suggestions.length > 0 && setOpen(true)}
        placeholder={placeholder || '輸入帳號或角色名稱…'}
        style={{ width: '100%' }}
        autoComplete="off"
      />
      {open && suggestions.length > 0 && (
        <div style={{
          position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 9999,
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          borderRadius: 8, boxShadow: '0 4px 16px rgba(0,0,0,.4)',
          maxHeight: 280, overflowY: 'auto', marginTop: 2
        }}>
          {suggestions.map((p, i) => (
            <div key={p.account}
              onMouseDown={() => select(p)}
              style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '8px 12px', cursor: 'pointer', fontSize: 13,
                background: i === activeIdx ? 'rgba(74,158,255,.18)' : 'transparent',
                borderBottom: i < suggestions.length - 1 ? '1px solid var(--border)' : 'none'
              }}>
              <span style={{ fontSize: 11 }}>{p.isOnline ? '🟢' : '⚫'}</span>
              <span style={{ fontWeight: 600, color: 'var(--text-primary)', flex: 1 }}>
                {p.onlineName || p.account}
              </span>
              <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{p.account}</span>
              {p.vipLevel > 0 && (
                <span style={{ fontSize: 10, color: p.vipLevel === 2 ? '#4dd0e1' : 'var(--accent-orange)', background: p.vipLevel === 2 ? 'rgba(77,208,225,.15)' : 'rgba(255,152,0,.15)', padding: '1px 6px', borderRadius: 10 }}>
                  {p.vipLevel === 2 ? '鑽石' : '黃金'}
                </span>
              )}
              {p.isBanned && <span style={{ fontSize: 10, color: 'var(--accent-red)' }}>🔒封</span>}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
