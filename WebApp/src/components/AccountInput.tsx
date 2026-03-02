/**
 * AccountInput — 共用帳號搜尋元件
 *
 * 功能：
 *   1. 輸入任何識別方式（主帳號名/角色名/UID）
 *   2. 若輸入的是主帳號名且底下有多個角色，自動展開角色卡片列表
 *   3. 只有一個角色時直接觸發 onSelect
 *   4. 選定角色後呼叫 onSelect(uid, charName)
 *
 * 用法：
 *   <AccountInput onSelect={(uid, charName) => loadPlayer(uid)} />
 *   <AccountInput initialValue={sp.get('account') ?? ''} onSelect={...} />
 */

import { useState, useEffect, useRef } from 'react'
import api from '../api'

export interface CharOption {
  account:   string   // csalogin.Name (12位UID)
  charName:  string   // OnlineName
  isOnline:  boolean
  gold:      number
  payTotal:  number
}

interface Props {
  onSelect:      (uid: string, charName: string) => void
  initialValue?: string
  placeholder?:  string
  disabled?:     boolean
  autoSearch?:   boolean   // 有 initialValue 時是否自動查詢
}

export default function AccountInput({
  onSelect,
  initialValue = '',
  placeholder  = '輸入主帳號 / 角色名 / UID…',
  disabled     = false,
  autoSearch   = true,
}: Props) {
  const [q, setQ]           = useState(initialValue)
  const [loading, setLoading] = useState(false)
  const [chars, setChars]   = useState<CharOption[]>([])
  const [open, setOpen]     = useState(false)
  const [err, setErr]       = useState('')
  const ref = useRef<HTMLDivElement>(null)

  // 點外面關閉下拉
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  // 有 initialValue 時自動搜尋
  useEffect(() => {
    if (initialValue && autoSearch) doSearch(initialValue)
  }, [])

  const doSearch = async (input: string) => {
    const v = input.trim()
    if (!v) return
    setLoading(true); setErr(''); setChars([]); setOpen(false)
    try {
      // 先試主帳號查詢
      const r = await api.get(`/players/master/${encodeURIComponent(v)}`)
      const list: CharOption[] = (r.data?.chars ?? []).map((c: any) => ({
        account:  c.account,
        charName: c.charName,
        isOnline: c.isOnline,
        gold:     c.gold,
        payTotal: c.payTotal,
      }))
      if (list.length === 1) {
        onSelect(list[0].account, list[0].charName)
        return
      }
      if (list.length > 1) {
        setChars(list)
        setOpen(true)
        return
      }
      // 不是主帳號，直接當 UID 或角色名使用
      onSelect(v, '')
    } catch {
      // 主帳號查不到 → 直接傳給呼叫端
      onSelect(v, '')
    } finally { setLoading(false) }
  }

  const pick = (c: CharOption) => {
    setQ(c.charName || c.account)
    setOpen(false)
    setChars([])
    onSelect(c.account, c.charName)
  }

  return (
    <div ref={ref} style={{ position: 'relative', flex: 1 }}>
      <div style={{ display: 'flex', gap: 8 }}>
        <input
          value={q}
          onChange={e => { setQ(e.target.value); setOpen(false) }}
          onKeyDown={e => e.key === 'Enter' && doSearch(q)}
          placeholder={placeholder}
          disabled={disabled || loading}
          style={{
            flex: 1, padding: '10px 14px', borderRadius: 8, fontSize: 14,
            background: 'var(--bg-input)', border: '1px solid var(--border)',
            color: 'var(--text-primary)', outline: 'none',
            opacity: disabled ? 0.6 : 1,
          }}
        />
        <button
          onClick={() => doSearch(q)}
          disabled={disabled || loading || !q.trim()}
          style={{
            padding: '10px 20px', borderRadius: 8, border: 'none', cursor: 'pointer',
            background: '#1e4ba0', color: '#fff', fontWeight: 700, fontSize: 14,
            opacity: disabled || loading ? 0.6 : 1, whiteSpace: 'nowrap',
          }}
        >
          {loading ? '查詢中…' : '🔍 查詢'}
        </button>
      </div>

      {err && <div style={{ fontSize: 12, color: '#f87171', marginTop: 4 }}>{err}</div>}

      {/* 角色下拉列表 */}
      {open && chars.length > 0 && (
        <div style={{
          position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 1000,
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          borderRadius: 10, boxShadow: '0 8px 32px rgba(0,0,0,.4)',
          marginTop: 6, padding: 10, maxHeight: 360, overflowY: 'auto',
        }}>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 8, paddingLeft: 4 }}>
            主帳號底下有 {chars.length} 個角色，請選擇：
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {chars.map(c => (
              <div
                key={c.account}
                onClick={() => pick(c)}
                style={{
                  display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                  padding: '8px 12px', borderRadius: 8, cursor: 'pointer',
                  background: 'var(--bg-mid)', border: '1px solid transparent',
                  transition: 'all .15s',
                }}
                onMouseEnter={e => {
                  (e.currentTarget as HTMLDivElement).style.borderColor = '#7c3aed'
                  ;(e.currentTarget as HTMLDivElement).style.background = 'var(--bg-input)'
                }}
                onMouseLeave={e => {
                  (e.currentTarget as HTMLDivElement).style.borderColor = 'transparent'
                  ;(e.currentTarget as HTMLDivElement).style.background = 'var(--bg-mid)'
                }}
              >
                <div>
                  <span style={{ fontWeight: 700, fontSize: 13, color: 'var(--text-primary)' }}>
                    {c.charName || '（無角色名）'}
                  </span>
                  {c.isOnline && (
                    <span style={{ marginLeft: 6, fontSize: 10, color: '#16b97a', fontWeight: 700 }}>● 線上</span>
                  )}
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{c.account}</div>
                </div>
                <div style={{ textAlign: 'right', fontSize: 11, color: 'var(--text-muted)' }}>
                  <div>💰 {c.gold.toLocaleString()} 金幣</div>
                  <div>💳 充值 {c.payTotal.toLocaleString()}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
