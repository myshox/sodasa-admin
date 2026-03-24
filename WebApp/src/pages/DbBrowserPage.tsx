import { useState, useEffect, useCallback } from 'react'
import api from '../api'

interface BrowseResult {
  columns: string[]
  rows: Record<string, unknown>[]
  total: number
  page: number
  pageSize: number
}

export default function DbBrowserPage() {
  const [tables, setTables]     = useState<string[]>([])
  const [tableSearch, setTableSearch] = useState('')
  const [selected, setSelected] = useState('')
  const [search, setSearch]     = useState('')
  const [inputSearch, setInputSearch] = useState('')
  const [page, setPage]         = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [result, setResult]     = useState<BrowseResult | null>(null)
  const [loading, setLoading]   = useState(false)
  const [tblLoading, setTblLoading] = useState(false)
  const [error, setError]       = useState('')

  // 載入表清單
  useEffect(() => {
    setTblLoading(true)
    api.get('/sql/tables')
      .then(r => setTables(r.data as string[]))
      .catch(() => setTables([]))
      .finally(() => setTblLoading(false))
  }, [])

  // 載入表資料
  const loadData = useCallback(async (tbl: string, kw: string, pg: number, ps: number) => {
    if (!tbl) return
    setLoading(true); setError('')
    try {
      const r = await api.get('/sql/browse', { params: { table: tbl, search: kw || undefined, page: pg, pageSize: ps } })
      setResult(r.data as BrowseResult)
    } catch (e: unknown) {
      const err = e as { response?: { data?: { error?: string } } }
      setError(err.response?.data?.error || '載入失敗')
      setResult(null)
    } finally {
      setLoading(false)
    }
  }, [])

  const selectTable = (tbl: string) => {
    setSelected(tbl); setPage(1); setSearch(''); setInputSearch(''); setResult(null)
    loadData(tbl, '', 1, pageSize)
  }

  const handleSearch = () => {
    setSearch(inputSearch); setPage(1)
    loadData(selected, inputSearch, 1, pageSize)
  }

  const handlePageChange = (newPage: number) => {
    setPage(newPage)
    loadData(selected, search, newPage, pageSize)
  }

  const handlePageSizeChange = (ps: number) => {
    setPageSize(ps); setPage(1)
    loadData(selected, search, 1, ps)
  }

  const filteredTables = tableSearch
    ? tables.filter(t => t.toLowerCase().includes(tableSearch.toLowerCase()))
    : tables

  const totalPages = result ? Math.ceil(result.total / pageSize) : 1

  return (
    <div className="gm-page-fill-row" style={{ overflow: 'hidden' }}>

      {/* ── 左側表列表 ── */}
      <div style={{
        width: 200, minWidth: 160, background: 'var(--bg-sidebar)', borderRight: '1px solid var(--border)',
        display: 'flex', flexDirection: 'column', flexShrink: 0
      }}>
        <div style={{ padding: '10px 10px 6px', fontWeight: 700, fontSize: 13, color: 'var(--text-muted)', borderBottom: '1px solid var(--border)' }}>
          📋 資料表 {tblLoading ? '載入中…' : `(${tables.length})`}
        </div>
        <div style={{ padding: '6px 8px', borderBottom: '1px solid var(--border)' }}>
          <input
            placeholder="篩選表名…"
            value={tableSearch}
            onChange={e => setTableSearch(e.target.value)}
            style={{ width: '100%', padding: '4px 8px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 5, fontSize: 12, color: 'var(--text-primary)', boxSizing: 'border-box' }}
          />
        </div>
        <div style={{ flex: 1, overflowY: 'auto' }}>
          {filteredTables.map(t => (
            <div
              key={t} onClick={() => selectTable(t)}
              style={{
                padding: '7px 12px', cursor: 'pointer', fontSize: 12,
                background: selected === t ? 'var(--accent-blue)' : 'transparent',
                color: selected === t ? '#fff' : 'var(--text-primary)',
                borderBottom: '1px solid rgba(255,255,255,.04)'
              }}
            >
              {t}
            </div>
          ))}
          {!tblLoading && filteredTables.length === 0 && (
            <div style={{ padding: 12, color: 'var(--text-muted)', fontSize: 12 }}>無符合結果</div>
          )}
        </div>
      </div>

      {/* ── 右側資料區 ── */}
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>

        {/* 標題 */}
        <div style={{ padding: '10px 16px', background: 'var(--bg-card)', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          <span style={{ fontWeight: 700, fontSize: 15, color: 'var(--text-primary)' }}>
            {selected ? `📋 ${selected}` : '← 請選擇左側資料表'}
          </span>
          {result && (
            <span style={{ color: 'var(--text-muted)', fontSize: 12 }}>
              共 {result.total.toLocaleString()} 筆 · 欄位：{result.columns.slice(0, 8).join(' | ')}{result.columns.length > 8 ? ' …' : ''}
            </span>
          )}
        </div>

        {/* 搜尋列 */}
        {selected && (
          <div style={{ padding: '8px 16px', background: 'var(--bg-card)', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <input
              placeholder="搜尋任意欄位…"
              value={inputSearch}
              onChange={e => setInputSearch(e.target.value)}
              onKeyDown={e => e.key === 'Enter' && handleSearch()}
              style={{ padding: '5px 10px', width: 220, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 5, color: 'var(--text-primary)', fontSize: 13 }}
            />
            <button onClick={handleSearch} disabled={loading} style={{ padding: '5px 14px', background: 'var(--accent-blue)', color: '#fff', border: 'none', borderRadius: 5, cursor: 'pointer', fontSize: 13 }}>
              {loading ? '…' : '搜尋'}
            </button>
            {search && (
              <button onClick={() => { setInputSearch(''); setSearch(''); setPage(1); loadData(selected, '', 1, pageSize) }}
                style={{ padding: '5px 10px', background: 'var(--bg-sidebar)', color: 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: 5, cursor: 'pointer', fontSize: 13 }}>
                清除
              </button>
            )}
            <span style={{ color: 'var(--text-muted)', fontSize: 12, marginLeft: 8 }}>每頁：</span>
            {[20, 50, 100, 200].map(ps => (
              <button key={ps} onClick={() => handlePageSizeChange(ps)}
                style={{ padding: '4px 10px', background: pageSize === ps ? 'var(--accent-blue)' : 'var(--bg-sidebar)', color: pageSize === ps ? '#fff' : 'var(--text-muted)', border: '1px solid var(--border)', borderRadius: 5, cursor: 'pointer', fontSize: 12 }}>
                {ps}
              </button>
            ))}
          </div>
        )}

        {/* 錯誤訊息 */}
        {error && (
          <div style={{ margin: '12px 16px', padding: 10, background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)', borderRadius: 6, color: 'var(--accent-red)', fontSize: 13 }}>
            {error}
          </div>
        )}

        {/* 資料表格 */}
        {!selected && (
          <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-muted)', fontSize: 15 }}>
            請從左側選擇一張資料表來瀏覽
          </div>
        )}

        {loading && (
          <div style={{ padding: 32, textAlign: 'center', color: 'var(--text-muted)' }}>載入中…</div>
        )}

        {!loading && result && result.columns.length > 0 && (
          <div style={{ flex: 1, overflow: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13, tableLayout: 'fixed' }}>
              <colgroup>
                {result.columns.map(c => <col key={c} style={{ minWidth: 80, maxWidth: 220 }} />)}
              </colgroup>
              <thead>
                <tr style={{ background: 'var(--bg-sidebar)', position: 'sticky', top: 0, zIndex: 1 }}>
                  {result.columns.map(c => (
                    <th key={c} style={{ padding: '7px 10px', textAlign: 'left', color: 'var(--text-muted)', fontWeight: 600, fontSize: 12, borderBottom: '2px solid var(--border)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {c}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {result.rows.map((row, i) => (
                  <tr key={i} style={{ background: i % 2 === 0 ? 'var(--bg-card)' : 'var(--bg-sidebar)', borderBottom: '1px solid var(--border)' }}>
                    {result.columns.map(col => (
                      <td key={col} title={row[col] != null ? String(row[col]) : ''} style={{ padding: '6px 10px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: 220, color: row[col] == null ? 'var(--text-muted)' : 'var(--text-primary)' }}>
                        {row[col] != null ? String(row[col]) : <span style={{ color: 'var(--text-muted)', fontStyle: 'italic' }}>NULL</span>}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* 翻頁列 */}
        {result && result.total > 0 && (
          <div style={{ padding: '8px 16px', background: 'var(--bg-card)', borderTop: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: 10 }}>
            <button disabled={page <= 1} onClick={() => handlePageChange(page - 1)}
              style={{ padding: '5px 12px', background: page <= 1 ? 'var(--bg-sidebar)' : 'var(--accent-blue)', color: '#fff', border: 'none', borderRadius: 5, cursor: page <= 1 ? 'not-allowed' : 'pointer', opacity: page <= 1 ? 0.5 : 1 }}>
              ◀ 上一頁
            </button>
            <span style={{ color: 'var(--text-muted)', fontSize: 13 }}>第 {page} / {Math.max(1, totalPages)} 頁</span>
            <button disabled={page >= totalPages} onClick={() => handlePageChange(page + 1)}
              style={{ padding: '5px 12px', background: page >= totalPages ? 'var(--bg-sidebar)' : 'var(--accent-blue)', color: '#fff', border: 'none', borderRadius: 5, cursor: page >= totalPages ? 'not-allowed' : 'pointer', opacity: page >= totalPages ? 0.5 : 1 }}>
              下一頁 ▶
            </button>
            <span style={{ marginLeft: 16, color: 'var(--text-muted)', fontSize: 12 }}>
              共 {result.total.toLocaleString()} 筆 · 第 {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, result.total)} 筆
            </span>
          </div>
        )}
      </div>
    </div>
  )
}
