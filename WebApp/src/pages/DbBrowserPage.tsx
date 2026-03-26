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

  useEffect(() => {
    setTblLoading(true)
    api.get('/sql/tables')
      .then(r => setTables(r.data as string[]))
      .catch(() => setTables([]))
      .finally(() => setTblLoading(false))
  }, [])

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
    <div className="gm-page-stack">
      <header className="gm-page-header">
        <h1 className="gm-page-title">
          <span className="gm-page-icon" aria-hidden>🗄</span>
          資料庫瀏覽
        </h1>
        <p className="gm-page-subtitle">左側選表、右側檢視資料；支援欄位關鍵字搜尋與分頁（唯讀）。</p>
      </header>

      <div
        className="db-browser-root gm-panel"
        style={{
          height: 'clamp(380px, 72vh, 880px)',
          padding: 0,
          overflow: 'hidden',
          marginBottom: 0,
        }}
      >
        <aside className="db-browser-sidebar">
          <div className="db-browser-sidebar-header">
            資料表 {tblLoading ? '載入中…' : `（${tables.length}）`}
          </div>
          <div className="db-browser-sidebar-search">
            <input
              type="search"
              placeholder="篩選表名…"
              value={tableSearch}
              onChange={e => setTableSearch(e.target.value)}
              autoComplete="off"
            />
          </div>
          <div className="db-browser-table-list" role="listbox" aria-label="資料表清單">
            {filteredTables.map(t => (
              <button
                key={t}
                type="button"
                className="db-browser-tbl-btn"
                data-active={selected === t ? 'true' : undefined}
                onClick={() => selectTable(t)}
              >
                {t}
              </button>
            ))}
            {!tblLoading && filteredTables.length === 0 && (
              <div style={{ padding: 14, color: 'var(--text-muted)', fontSize: 13 }}>無符合結果</div>
            )}
          </div>
        </aside>

        <div className="db-browser-main">
          <div className="db-browser-toolbar">
            <span className="db-browser-toolbar-title">
              {selected ? `📋 ${selected}` : '← 請選擇左側資料表'}
            </span>
            {result && (
              <span style={{ color: 'var(--text-muted)', fontSize: 12, fontWeight: 500 }}>
                共 {result.total.toLocaleString()} 筆 · 欄位：{result.columns.slice(0, 8).join(' | ')}{result.columns.length > 8 ? ' …' : ''}
              </span>
            )}
          </div>

          {selected && (
            <div className="db-browser-search-row">
              <input
                type="text"
                placeholder="搜尋任意欄位…"
                value={inputSearch}
                onChange={e => setInputSearch(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleSearch()}
              />
              <button type="button" className="primary" onClick={handleSearch} disabled={loading}>
                {loading ? '…' : '搜尋'}
              </button>
              {search && (
                <button
                  type="button"
                  onClick={() => { setInputSearch(''); setSearch(''); setPage(1); loadData(selected, '', 1, pageSize) }}
                >
                  清除
                </button>
              )}
              <span style={{ color: 'var(--text-muted)', fontSize: 12, marginLeft: 4 }}>每頁</span>
              <div className="db-browser-page-btns">
                {[20, 50, 100, 200].map(ps => (
                  <button
                    key={ps}
                    type="button"
                    className={pageSize === ps ? 'primary' : undefined}
                    style={pageSize === ps ? undefined : { opacity: 0.9 }}
                    onClick={() => handlePageSizeChange(ps)}
                  >
                    {ps}
                  </button>
                ))}
              </div>
            </div>
          )}

          {error && (
            <div className="ui-error" style={{ margin: '12px 16px' }} role="alert">
              {error}
            </div>
          )}

          {!selected && (
            <div className="db-browser-empty">請從左側選擇一張資料表來瀏覽</div>
          )}

          {loading && (
            <div className="db-browser-loading">載入中…</div>
          )}

          {!loading && result && result.columns.length > 0 && (
            <div className="gm-native-table-wrap" style={{ flex: 1, margin: '0 8px 8px', borderRadius: 'var(--radius-sm)' }}>
              <table className="gm-native-table">
                <colgroup>
                  {result.columns.map(c => <col key={c} style={{ minWidth: 80, maxWidth: 220 }} />)}
                </colgroup>
                <thead>
                  <tr>
                    {result.columns.map(c => (
                      <th key={c} title={c}>{c}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {result.rows.map((row, i) => (
                    <tr key={i}>
                      {result.columns.map(col => (
                        <td
                          key={col}
                          title={row[col] != null ? String(row[col]) : ''}
                          style={{ color: row[col] == null ? 'var(--text-muted)' : undefined }}
                        >
                          {row[col] != null ? String(row[col]) : <span style={{ fontStyle: 'italic' }}>NULL</span>}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {result && result.total > 0 && (
            <div className="db-browser-footer">
              <button type="button" className="primary" disabled={page <= 1} onClick={() => handlePageChange(page - 1)}>
                ◀ 上一頁
              </button>
              <span style={{ color: 'var(--text-muted)', fontSize: 13, fontWeight: 600 }}>
                第 {page} / {Math.max(1, totalPages)} 頁
              </span>
              <button type="button" className="primary" disabled={page >= totalPages} onClick={() => handlePageChange(page + 1)}>
                下一頁 ▶
              </button>
              <span style={{ marginLeft: 8, color: 'var(--text-muted)', fontSize: 12 }}>
                共 {result.total.toLocaleString()} 筆 · 第 {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, result.total)} 筆
              </span>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
