import { useState, useEffect, useMemo, useCallback } from 'react'
import api from '../api'

const COL_ZH: Record<string, string> = {
  cdkey: '帳號', cdkeyluo: '帳號(luo)', account: '帳號', userid: '用戶ID', username: '用戶名',
  petid: '寵物ID', pet_id: '寵物ID', name: '寵物名稱', petname: '寵物名稱', pet_name: '寵物名稱',
  type: '種類', pettype: '種類', pet_type: '種類', id: '編號',
  lv: '等級', level: '等級',
  hp: '血量', maxhp: '最大血量', attack: '攻擊', atk: '攻擊', def: '防禦', defense: '防禦',
  quick: '敏捷', spd: '敏捷', speed: '敏捷',
  sum: '戰鬥力', power: '戰鬥力', combat: '戰鬥力', rank: '排名',
  createtime: '建立時間', updatetime: '更新時間', time: '時間',
  author: '捕捉者', owner: '擁有者',
  serverid: '伺服器', server: '伺服器',
  _playername: '玩家名稱', _online: '在線',
  imageid: '外觀ID', image_id: '外觀ID', skinid: '外觀ID',
  score: '評分', point: '積分', exp: '經驗', star: '星級',
  quality: '品質', color: '品質色', remark: '備註', memo: '備註',
  status: '狀態', flag: '旗標',
  basehp: '初始血量', baseatk: '初始攻擊', basedef: '初始防禦', basespd: '初始敏捷',
  growhp: '血量成長', growatk: '攻擊成長', growdef: '防禦成長', growspd: '敏捷成長',
  oldlv: '初始等級', oldhp: '初始血量', oldattack: '初始攻擊', olddef: '初始防禦', oldquick: '初始敏捷',
}

const SORT_PRIORITY = ['sum', 'power', 'combat', 'hp', 'attack', 'atk', 'def', 'quick', 'spd', 'lv', 'level']
const HIDDEN_COLS = new Set(['_online'])
const SHOW_OPTIONS = [
  { label: '全部', value: 0 },
  { label: '前 10', value: 10 },
  { label: '前 25', value: 25 },
  { label: '前 50', value: 50 },
  { label: '前 100', value: 100 },
]

interface BillingData {
  tableName: string | null
  columns: string[]
  rows: Record<string, any>[]
  total: number
  error?: string
}

const card: React.CSSProperties = {
  background: 'var(--neu-bg)', borderRadius: 16,
  padding: '20px 24px', marginBottom: 20,
  boxShadow: '0 2px 12px rgba(0,0,0,.08)',
}
const thStyle: React.CSSProperties = {
  padding: '8px 10px', background: 'var(--primary)', color: '#fff',
  textAlign: 'left', fontWeight: 600, fontSize: 12, whiteSpace: 'nowrap',
  position: 'sticky', top: 0, zIndex: 1,
}
const tdStyle = (i: number): React.CSSProperties => ({
  padding: '6px 10px', borderBottom: '1px solid var(--border)',
  background: i % 2 === 0 ? 'transparent' : 'rgba(0,0,0,.02)',
  fontSize: 13, whiteSpace: 'nowrap',
})

function colHeader(col: string): string {
  const lower = col.toLowerCase()
  if (COL_ZH[lower]) return `${COL_ZH[lower]}（${col}）`
  return col
}

export default function PetBillingPage() {
  const [data, setData] = useState<BillingData | null>(null)
  const [loading, setLoading] = useState(true)
  const [sortCol, setSortCol] = useState('')
  const [classifyCol, setClassifyCol] = useState('')
  const [classifyVal, setClassifyVal] = useState('(全部)')
  const [search, setSearch] = useState('')
  const [showLimit, setShowLimit] = useState(0)
  const [valFilter, setValFilter] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const r = await api.get<BillingData>('/petrank/billing', { params: { limit: 2000 } })
      setData(r.data)
      if (r.data.columns?.length) {
        const defaultSort = SORT_PRIORITY.find(p =>
          r.data.columns.some(c => c.toLowerCase() === p)
        )
        if (defaultSort) {
          const exact = r.data.columns.find(c => c.toLowerCase() === defaultSort) ?? ''
          setSortCol(exact)
        }
      }
    } catch { setData(null) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { load() }, [load])

  const displayCols = useMemo(() => {
    if (!data?.columns) return []
    const priority = ['_online', '_playerName']
    const nameCols = ['name', 'petname', 'pet_name']
    const ordered: string[] = []
    for (const p of priority) {
      const found = data.columns.find(c => c.toLowerCase() === p.toLowerCase())
      if (found) ordered.push(found)
    }
    for (const n of nameCols) {
      const found = data.columns.find(c => c.toLowerCase() === n && !ordered.includes(c))
      if (found) ordered.push(found)
    }
    for (const c of data.columns) {
      if (!ordered.includes(c)) ordered.push(c)
    }
    return ordered
  }, [data?.columns])

  const classifyValues = useMemo(() => {
    if (!data?.rows?.length || !classifyCol) return []
    const set = new Set<string>()
    for (const row of data.rows) {
      const v = row[classifyCol]
      if (v != null) set.add(String(v))
    }
    return Array.from(set).sort()
  }, [data?.rows, classifyCol])

  const filteredValues = useMemo(() => {
    if (!valFilter) return classifyValues
    const q = valFilter.toLowerCase()
    return classifyValues.filter(v => v.toLowerCase().includes(q))
  }, [classifyValues, valFilter])

  const processedRows = useMemo(() => {
    if (!data?.rows) return []
    let rows = [...data.rows]

    if (classifyCol && classifyVal !== '(全部)') {
      rows = rows.filter(r => String(r[classifyCol] ?? '') === classifyVal)
    }

    if (search.trim()) {
      const q = search.trim().toLowerCase()
      rows = rows.filter(r =>
        Object.values(r).some(v => v != null && String(v).toLowerCase().includes(q))
      )
    }

    if (sortCol) {
      rows.sort((a, b) => {
        const va = parseFloat(String(a[sortCol] ?? ''))
        const vb = parseFloat(String(b[sortCol] ?? ''))
        const na = isNaN(va) ? -Infinity : va
        const nb = isNaN(vb) ? -Infinity : vb
        return nb - na
      })
    }

    if (showLimit > 0) rows = rows.slice(0, showLimit)

    return rows
  }, [data?.rows, classifyCol, classifyVal, search, sortCol, showLimit])

  const exportCsv = () => {
    if (!processedRows.length || !displayCols.length) return
    const esc = (v: string) =>
      v.includes(',') || v.includes('"') || v.includes('\n') ? `"${v.replace(/"/g, '""')}"` : v
    const header = ['#', ...displayCols.filter(c => !HIDDEN_COLS.has(c)).map(c => colHeader(c))].map(esc).join(',')
    const lines = processedRows.map((r, i) => {
      const vals = displayCols.filter(c => !HIDDEN_COLS.has(c)).map(c => String(r[c] ?? ''))
      return [String(i + 1), ...vals].map(esc).join(',')
    })
    const csv = '\uFEFF' + [header, ...lines].join('\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `寵物排行_${data?.tableName ?? 'pet'}_${new Date().toISOString().slice(0, 10)}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  if (loading) {
    return (
      <div className="gm-page-stack">
        <h2 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🐾 寵物總排行榜</h2>
        <div style={{ textAlign: 'center', padding: 40, color: '#888' }}>載入中…</div>
      </div>
    )
  }

  if (!data || !data.tableName || data.tableName === 'ERROR') {
    return (
      <div className="gm-page-stack">
        <h2 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🐾 寵物總排行榜</h2>
        <div style={card}>
          <div style={{ textAlign: 'center', padding: 30, color: '#e74c3c' }}>
            {data?.error ? `讀取失敗：${data.error}` : '找不到寵物排行表（petbilling / petrank / rankpet …）'}
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="gm-page-stack">
      <h2 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🐾 寵物總排行榜</h2>

      <div style={{ ...card, display: 'flex', flexWrap: 'wrap', gap: 16, alignItems: 'flex-end', padding: '16px 20px' }}>
        <div style={{ fontSize: 13, color: 'var(--primary)', fontWeight: 700 }}>
          [OK] {data.tableName} | {displayCols.length} 欄 | {data.total} 筆
        </div>

        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, flex: 1, alignItems: 'flex-end' }}>
          <label style={{ fontSize: 12, display: 'flex', flexDirection: 'column', gap: 4 }}>
            分類
            <select value={classifyCol} onChange={e => { setClassifyCol(e.target.value); setClassifyVal('(全部)'); setValFilter('') }}
              style={{ padding: '6px 8px', borderRadius: 8, border: '1px solid var(--border)', fontSize: 12, minWidth: 120 }}>
              <option value="">（不分類）</option>
              {displayCols.filter(c => !HIDDEN_COLS.has(c)).map(c => (
                <option key={c} value={c}>{colHeader(c)}</option>
              ))}
            </select>
          </label>

          <label style={{ fontSize: 12, display: 'flex', flexDirection: 'column', gap: 4 }}>
            排序
            <select value={sortCol} onChange={e => setSortCol(e.target.value)}
              style={{ padding: '6px 8px', borderRadius: 8, border: '1px solid var(--border)', fontSize: 12, minWidth: 120 }}>
              <option value="">（不排序）</option>
              {displayCols.filter(c => !HIDDEN_COLS.has(c)).map(c => (
                <option key={c} value={c}>{colHeader(c)}</option>
              ))}
            </select>
          </label>

          <label style={{ fontSize: 12, display: 'flex', flexDirection: 'column', gap: 4 }}>
            搜尋
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="輸入關鍵字過濾（玩家名、寵物名、帳號…）"
              style={{ padding: '6px 8px', borderRadius: 8, border: '1px solid var(--border)', fontSize: 12, minWidth: 220 }} />
          </label>

          <label style={{ fontSize: 12, display: 'flex', flexDirection: 'column', gap: 4 }}>
            顯示
            <select value={showLimit} onChange={e => setShowLimit(Number(e.target.value))}
              style={{ padding: '6px 8px', borderRadius: 8, border: '1px solid var(--border)', fontSize: 12 }}>
              {SHOW_OPTIONS.map(o => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </label>

          <div style={{ display: 'flex', gap: 6 }}>
            <button onClick={exportCsv} disabled={!processedRows.length}
              style={{ padding: '6px 14px', borderRadius: 8, border: 'none', background: '#27ae60', color: '#fff', cursor: 'pointer', fontSize: 12, fontWeight: 600 }}>
              CSV
            </button>
            <button onClick={load}
              style={{ padding: '6px 14px', borderRadius: 8, border: 'none', background: 'var(--primary)', color: '#fff', cursor: 'pointer', fontSize: 12, fontWeight: 600 }}>
              重新載入
            </button>
          </div>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 16 }}>
        {classifyCol && classifyValues.length > 0 && (
          <div style={{ ...card, width: 200, minWidth: 160, maxHeight: 600, display: 'flex', flexDirection: 'column', flexShrink: 0 }}>
            <div style={{ fontWeight: 700, fontSize: 12, marginBottom: 8, color: 'var(--text-secondary)' }}>
              {colHeader(classifyCol)}
            </div>
            <input value={valFilter} onChange={e => setValFilter(e.target.value)} placeholder="搜尋…"
              style={{ padding: '5px 8px', borderRadius: 6, border: '1px solid var(--border)', fontSize: 11, marginBottom: 8 }} />
            <div style={{ flex: 1, overflowY: 'auto' }}>
              <div
                onClick={() => setClassifyVal('(全部)')}
                style={{
                  padding: '5px 8px', cursor: 'pointer', borderRadius: 6, fontSize: 12, marginBottom: 2,
                  background: classifyVal === '(全部)' ? 'var(--primary)' : 'transparent',
                  color: classifyVal === '(全部)' ? '#fff' : 'inherit', fontWeight: 600,
                }}>
                (全部)
              </div>
              {filteredValues.map(v => (
                <div key={v}
                  onClick={() => setClassifyVal(v)}
                  style={{
                    padding: '5px 8px', cursor: 'pointer', borderRadius: 6, fontSize: 12, marginBottom: 2,
                    background: classifyVal === v ? 'var(--primary)' : 'transparent',
                    color: classifyVal === v ? '#fff' : 'inherit',
                    overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                  }}
                  title={v}>
                  {v}
                </div>
              ))}
            </div>
          </div>
        )}

        <div style={{ ...card, flex: 1, overflow: 'hidden', padding: 0 }}>
          <div style={{ padding: '12px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, color: 'var(--text-secondary)' }}>
            顯示 <strong style={{ color: 'var(--primary)' }}>{processedRows.length}</strong> / {data.total} 筆
            {classifyVal !== '(全部)' && <span style={{ marginLeft: 8, color: '#e67e22' }}>篩選：{classifyVal}</span>}
            {search && <span style={{ marginLeft: 8, color: '#3498db' }}>搜尋：{search}</span>}
          </div>
          <div style={{ overflowX: 'auto', maxHeight: 600 }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  <th style={{ ...thStyle, textAlign: 'center', width: 44 }}>#</th>
                  {displayCols.filter(c => !HIDDEN_COLS.has(c)).map(c => (
                    <th key={c} style={{ ...thStyle, cursor: 'pointer' }}
                      onClick={() => setSortCol(prev => prev === c ? '' : c)}
                      title={`點擊以 ${colHeader(c)} 排序`}>
                      {colHeader(c)}
                      {sortCol === c && ' ▼'}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {processedRows.map((row, i) => (
                  <tr key={i}>
                    <td style={{ ...tdStyle(i), textAlign: 'center', fontWeight: 600, color: '#888' }}>{i + 1}</td>
                    {displayCols.filter(c => !HIDDEN_COLS.has(c)).map(c => {
                      const v = row[c]
                      const lower = c.toLowerCase()
                      const isOnlineCol = lower === '_online'
                      const isNameCol = lower === '_playername'
                      const isStatCol = ['sum', 'power', 'combat'].includes(lower)
                      return (
                        <td key={c} style={{
                          ...tdStyle(i),
                          fontWeight: isNameCol || isStatCol ? 700 : undefined,
                          color: isStatCol ? 'var(--primary)' : isOnlineCol ? undefined : undefined,
                        }}>
                          {isOnlineCol
                            ? (v === 1 || v === '1' || v === true ? '🟢' : '⚫')
                            : v != null ? String(v) : ''
                          }
                        </td>
                      )
                    })}
                  </tr>
                ))}
                {processedRows.length === 0 && (
                  <tr>
                    <td colSpan={displayCols.length + 1} style={{ textAlign: 'center', padding: 30, color: '#aaa' }}>
                      無符合資料
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  )
}
