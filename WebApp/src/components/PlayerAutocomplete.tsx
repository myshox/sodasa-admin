import { useState, useEffect, useRef } from 'react'
import api from '../api'
import type { PlayerRow } from '../api'

interface Props {
  value: string
  onChange: (v: string) => void
  /** 單選模式必傳；若已傳 onSelectMulti 可省略 */
  onSelect?: (p: PlayerRow) => void
  /** 若提供此 callback，則啟用複選模式，確認後回傳所有選定的玩家 */
  onSelectMulti?: (players: PlayerRow[]) => void
  placeholder?: string
  style?: React.CSSProperties
}

export default function PlayerAutocomplete({
  value, onChange, onSelect, onSelectMulti, placeholder, style
}: Props) {
  const [suggestions, setSuggestions] = useState<PlayerRow[]>([])
  const [open,        setOpen]        = useState(false)
  const [activeIdx,   setActiveIdx]   = useState(-1)
  const [checked,     setChecked]     = useState<Set<string>>(new Set())
  const timer  = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const wrapRef = useRef<HTMLDivElement>(null)

  const multiMode = !!onSelectMulti

  useEffect(() => {
    if (!value.trim() || value.length < 1) { setSuggestions([]); setOpen(false); return }
    clearTimeout(timer.current)
    timer.current = setTimeout(async () => {
      try {
        const r = await api.get('/players/search', { params: { q: value.trim(), limit: 20 } })
        setSuggestions(r.data)
        setOpen(r.data.length > 0)
        setActiveIdx(-1)
        // 多選模式下：有新結果時重置勾選
        if (multiMode) setChecked(new Set())
      } catch { setSuggestions([]); setOpen(false) }
    }, 280)
    return () => { clearTimeout(timer.current) }
  }, [value, multiMode])

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

  const selectOne = (p: PlayerRow) => {
    onChange(p.onlineName || p.account)
    onSelect?.(p)
    setOpen(false)
    setSuggestions([])
    setChecked(new Set())
  }

  const toggleCheck = (acc: string) => {
    setChecked(prev => {
      const next = new Set(prev)
      next.has(acc) ? next.delete(acc) : next.add(acc)
      return next
    })
  }

  const selectAll = () => setChecked(new Set(suggestions.map(p => p.account)))
  const clearAll  = () => setChecked(new Set())

  const confirmMulti = () => {
    const selected = suggestions.filter(p => checked.has(p.account))
    if (selected.length === 0) return
    onSelectMulti!(selected)
    onChange('')
    setOpen(false)
    setSuggestions([])
    setChecked(new Set())
  }

  /** 搜尋僅 1 筆時：一鍵加入，不需再勾選＋確認 */
  const pickSingleInMulti = (p: PlayerRow) => {
    onSelectMulti!([p])
    onChange('')
    setOpen(false)
    setSuggestions([])
    setChecked(new Set())
  }

  const handleKey = (e: React.KeyboardEvent) => {
    if (!open || suggestions.length === 0) return
    if (e.key === 'ArrowDown') { e.preventDefault(); setActiveIdx(i => Math.min(i + 1, suggestions.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActiveIdx(i => Math.max(i - 1, 0)) }
    else if (e.key === 'Enter' && activeIdx >= 0) {
      e.preventDefault()
      if (multiMode) {
        if (suggestions.length === 1) pickSingleInMulti(suggestions[0])
        else toggleCheck(suggestions[activeIdx].account)
      } else selectOne(suggestions[activeIdx])
    }
    else if (e.key === 'Escape') setOpen(false)
  }

  const allChecked = suggestions.length > 0 && suggestions.every(p => checked.has(p.account))
  const checkedCount = checked.size

  return (
    <div ref={wrapRef} style={{ position: 'relative', ...style }}>
      <input
        className="gm-search-input"
        value={value}
        onChange={e => { onChange(e.target.value); setOpen(true) }}
        onKeyDown={handleKey}
        onFocus={() => suggestions.length > 0 && setOpen(true)}
        placeholder={placeholder || '主帳號 / 角色名 / UID…'}
        style={{ width: '100%' }}
        autoComplete="off"
        enterKeyHint="search"
      />
      {open && suggestions.length > 0 && (
        <div style={{
          position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 9999,
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          borderRadius: 8, boxShadow: '0 4px 16px rgba(0,0,0,.4)',
          maxHeight: 340, overflowY: 'auto', marginTop: 2, touchAction: 'manipulation', WebkitOverflowScrolling: 'touch',
        }}>
          {/* 多選工具列（≥1 筆即顯示；全選僅在 2 筆以上有意義） */}
          {multiMode && suggestions.length > 0 && (
            <div style={{
              display: 'flex', alignItems: 'center', justifyContent: 'space-between',
              padding: '6px 10px', borderBottom: '1px solid var(--border)',
              background: 'rgba(74,158,255,.07)', fontSize: 12, gap: 8
            }}>
              <span style={{ color: 'var(--text-muted)' }}>
                共 {suggestions.length} 個角色
                {checkedCount > 0 && <span style={{ color: 'var(--accent-blue)', marginLeft: 6 }}>（已勾 {checkedCount}）</span>}
              </span>
              <div style={{ display: 'flex', gap: 6 }}>
                {suggestions.length > 1 && (
                  <button type="button" onPointerDown={e => { e.preventDefault(); allChecked ? clearAll() : selectAll() }}
                    style={{
                      fontSize: 11, padding: '2px 8px', borderRadius: 4, cursor: 'pointer',
                      background: allChecked ? 'rgba(74,158,255,.25)' : 'rgba(74,158,255,.12)',
                      color: 'var(--accent-blue)', border: '1px solid rgba(74,158,255,.3)', touchAction: 'manipulation',
                    }}>
                    {allChecked ? '取消全選' : '全選'}
                  </button>
                )}
              </div>
            </div>
          )}

          {/* 玩家列表 */}
          {suggestions.map((p, i) => {
            const isChecked = checked.has(p.account)
            return (
              <div key={p.account}
                onPointerDown={e => {
                  if (e.pointerType === 'mouse' && e.button !== 0) return
                  e.preventDefault()
                  if (!multiMode) { selectOne(p); return }
                  // 僅 1 筆結果：點列即加入（與 ItemSend / 道具給予一致）
                  if (suggestions.length === 1) { pickSingleInMulti(p); return }
                  toggleCheck(p.account)
                }}
                data-suggestion-item
                style={{
                  display: 'flex', alignItems: 'center', gap: 8,
                  padding: '12px 14px', cursor: 'pointer', fontSize: 14,
                  minHeight: 48,
                  background: isChecked
                    ? 'rgba(74,158,255,.18)'
                    : i === activeIdx ? 'rgba(74,158,255,.10)' : 'transparent',
                  borderBottom: i < suggestions.length - 1 ? '1px solid var(--border)' : 'none',
                  transition: 'background .1s', touchAction: 'manipulation',
                }}>
                {/* 多選模式：勾選框（點勾選框時永遠 toggle） */}
                {multiMode && suggestions.length > 1 && (
                  <div
                    onPointerDown={e => { e.stopPropagation(); e.preventDefault(); toggleCheck(p.account) }}
                    style={{
                      width: 18, height: 18, border: `2px solid ${isChecked ? 'var(--accent-blue)' : 'var(--border)'}`,
                      borderRadius: 4, background: isChecked ? 'var(--accent-blue)' : 'transparent',
                      flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center',
                      fontSize: 10, color: 'white', transition: 'all .1s', cursor: 'pointer', touchAction: 'manipulation',
                    }}>
                    {isChecked && '✓'}
                  </div>
                )}
                <span style={{ fontSize: 11 }}>{p.isOnline ? '🟢' : '⚫'}</span>
                <span style={{ fontWeight: 600, color: 'var(--text-primary)', flex: 1 }}>
                  {p.onlineName || p.account}
                </span>
                <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{p.account}</span>
                {p.vipLevel > 0 && (
                  <span style={{
                    fontSize: 10, color: p.vipLevel === 2 ? '#4dd0e1' : 'var(--accent-orange)',
                    background: p.vipLevel === 2 ? 'rgba(77,208,225,.15)' : 'rgba(255,152,0,.15)',
                    padding: '1px 6px', borderRadius: 10
                  }}>
                    {p.vipLevel === 2 ? '鑽石' : '黃金'}
                  </span>
                )}
                {p.isBanned && <span style={{ fontSize: 10, color: 'var(--accent-red)' }}>🔒封</span>}
              </div>
            )
          })}

          {/* 多選確認（僅 ≥2 筆時需要；1 筆時點列即加入） */}
          {multiMode && suggestions.length > 1 && (
            <div style={{
              padding: '8px 10px', borderTop: '1px solid var(--border)',
              background: 'rgba(0,0,0,.2)', display: 'flex', gap: 8, justifyContent: 'flex-end'
            }}>
              <button type="button" onPointerDown={e => { e.preventDefault(); setOpen(false) }}
                style={{
                  fontSize: 12, padding: '5px 14px', borderRadius: 6, cursor: 'pointer',
                  background: 'transparent', color: 'var(--text-muted)',
                  border: '1px solid var(--border)', touchAction: 'manipulation',
                }}>取消</button>
              <button type="button" onPointerDown={e => { e.preventDefault(); confirmMulti() }}
                disabled={checkedCount === 0}
                style={{
                  fontSize: 12, padding: '5px 16px', borderRadius: 6, cursor: checkedCount > 0 ? 'pointer' : 'not-allowed',
                  background: checkedCount > 0 ? 'var(--accent-blue)' : 'rgba(74,158,255,.2)',
                  color: checkedCount > 0 ? 'white' : 'var(--text-muted)',
                  border: 'none', fontWeight: 600, touchAction: 'manipulation',
                }}>
                確認選取{checkedCount > 0 ? ` (${checkedCount})` : ''}
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
