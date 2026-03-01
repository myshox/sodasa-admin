import { useState } from 'react'
import api from '../api'
import useIsMobile from '../hooks/useIsMobile'

interface SpeedPlayer {
  account: string
  charName: string
  isOnline: boolean
  totalCnt: number
  records: number
  avgSpeedTime: number
  maxSpeedTime: number
  lastTime: string
  isBanned: boolean
}

export default function SpeedBanPage() {
  const isMobile = useIsMobile()
  const [list, setList]     = useState<SpeedPlayer[]>([])
  const [loading, setLoading] = useState(false)
  const [minCnt, setMinCnt]   = useState(10)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [banDays, setBanDays]   = useState(0)  // 0 = 永久
  const [banning, setBanning]   = useState(false)
  const [msg, setMsg]         = useState('')
  const [msgOk, setMsgOk]     = useState(true)

  const exportCsv = () => {
    if (list.length === 0) return
    const BOM = '\uFEFF'
    const header = '狀態,角色名稱,帳號,異常總次數(speedcnt),紀錄筆數,平均speedtime,最大speedtime,最後偵測時間,風險等級'
    const rows = list.map(p => {
      const status = p.isBanned ? '已封禁' : p.isOnline ? '在線' : '離線'
      const risk   = p.isBanned ? '已處理' : p.totalCnt > 1000 ? '高風險' : p.totalCnt > 100 ? '中風險' : '低風險'
      return [status, p.charName, p.account, p.totalCnt, p.records, p.avgSpeedTime?.toFixed(1) ?? '0', p.maxSpeedTime ?? 0, p.lastTime, risk].join(',')
    })
    const csv = BOM + [header, ...rows].join('\n')
    const a   = document.createElement('a')
    a.href    = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }))
    a.download = `加速外掛報表_${new Date().toISOString().slice(0,10)}.csv`
    a.click()
  }

  const load = async () => {
    setLoading(true); setMsg(''); setSelected(new Set())
    try {
      const r = await api.get('/players/speed-hackers', { params: { min: minCnt, limit: 500 } })
      setList(r.data)
    } catch { setMsg('載入失敗'); setMsgOk(false) }
    finally { setLoading(false) }
  }

  const toggle = (acc: string) => {
    const s = new Set(selected)
    s.has(acc) ? s.delete(acc) : s.add(acc)
    setSelected(s)
  }
  const selectAll  = () => setSelected(new Set(list.filter(p => !p.isBanned).map(p => p.account)))
  const selectNone = () => setSelected(new Set())
  const invertSel  = () => setSelected(new Set(list.filter(p => !p.isBanned && !selected.has(p.account)).map(p => p.account)))

  const doBan = async () => {
    if (selected.size === 0) { setMsg('請先勾選玩家'); setMsgOk(false); return }
    const dur = banDays === 0 ? '永久' : `${banDays} 天`
    if (!window.confirm(`確定封禁以下 ${selected.size} 位玩家（${dur}）？`)) return
    setBanning(true); setMsg('')
    try {
      const r = await api.post('/players/batch-ban', {
        accounts: Array.from(selected),
        days: banDays,
        hours: 0,
      })
      setMsgOk(true)
      setMsg(r.data.message || '封禁完成')
      // 重新整理已封禁狀態
      setList(prev => prev.map(p => selected.has(p.account) ? { ...p, isBanned: true } : p))
      setSelected(new Set())
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } }
      setMsg(err?.response?.data?.message || '封禁失敗')
      setMsgOk(false)
    } finally { setBanning(false) }
  }

  const durBtns = [
    { label: '1 天', days: 1 },
    { label: '3 天', days: 3 },
    { label: '7 天', days: 7 },
    { label: '30 天', days: 30 },
    { label: '永久', days: 0 },
  ]

  return (
    <div style={{ padding: isMobile ? 12 : 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 6 }}>⚡ 加速外掛偵測</h1>
      <div style={{ fontSize: 13, color: 'var(--text-muted)', marginBottom: 20, lineHeight: 1.8 }}>
        <p style={{ margin: 0 }}>根據遊戲引擎寫入的 <code style={{ background: 'var(--bg-input)', padding: '1px 5px', borderRadius: 3 }}>speedlog</code> 表統計各玩家資料，可一鍵批量封禁。</p>
        <p style={{ margin: '4px 0 0', fontSize: 12 }}>
          📌 <b>speedcnt</b>（異常總次數）= 引擎偵測到玩家移動速度超標的累計次數。
          ｜ <b>speedtime</b>（異常持續量）= 每次異常持續的 Tick 數，越大代表加速外掛使用越久。
          ｜ 紀錄筆數 = speedlog 中該玩家的資料列數（每次登入/移動可能產生多筆）。
        </p>
      </div>

      {/* 搜尋列 */}
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginBottom: 16, alignItems: 'center' }}>
        <label style={{ fontSize: 13, display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ color: 'var(--text-muted)' }}>最低次數門檻：</span>
          <input type="number" value={minCnt} min={1} max={99999}
            onChange={e => setMinCnt(+e.target.value || 1)}
            style={{ width: 80, textAlign: 'right' }} />
        </label>
        <button onClick={load} disabled={loading}
          style={{ background: 'var(--accent-blue)', color: '#fff', padding: '8px 20px', fontSize: 13 }}>
          {loading ? '載入中…' : '🔍 查詢'}
        </button>
        {list.length > 0 && (
          <button onClick={exportCsv}
            style={{ background: 'var(--accent-green)', color: '#fff', padding: '8px 16px', fontSize: 13, borderRadius: 6 }}>
            📊 匯出 CSV
          </button>
        )}
        {list.length > 0 && (
          <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>
            共 {list.length} 人，
            <span style={{ color: 'var(--accent-red)' }}>已封 {list.filter(p => p.isBanned).length} 人</span>，
            待處理 {list.filter(p => !p.isBanned).length} 人
          </span>
        )}
      </div>

      {msg && (
        <div style={{
          padding: '10px 16px', borderRadius: 8, fontSize: 13, marginBottom: 14,
          background: msgOk ? 'rgba(86,196,118,.12)' : 'rgba(245,101,101,.1)',
          border: `1px solid ${msgOk ? 'var(--accent-green)' : 'var(--accent-red)'}`,
          color: msgOk ? 'var(--accent-green)' : 'var(--accent-red)',
        }}>{msg}</div>
      )}

      {list.length > 0 && (
        <>
          {/* 批量操作工具列 */}
          <div style={{
            display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center',
            padding: '10px 14px', background: 'var(--bg-card)', border: '1px solid var(--border)',
            borderRadius: 10, marginBottom: 12,
          }}>
            <span style={{ fontSize: 12, color: 'var(--text-muted)', marginRight: 4 }}>選取：</span>
            <button onClick={selectAll}
              style={{ fontSize: 11, padding: '3px 10px', background: 'rgba(74,158,255,.15)', border: '1px solid var(--accent-blue)', borderRadius: 4, color: 'var(--accent-blue)' }}>
              全選未封禁
            </button>
            <button onClick={invertSel}
              style={{ fontSize: 11, padding: '3px 10px', background: 'rgba(246,173,85,.1)', border: '1px solid var(--accent-orange)', borderRadius: 4, color: 'var(--accent-orange)' }}>
              反選
            </button>
            <button onClick={selectNone}
              style={{ fontSize: 11, padding: '3px 10px', background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 4, color: 'var(--text-muted)' }}>
              清除
            </button>

            <div style={{ width: 1, height: 20, background: 'var(--border)', margin: '0 4px' }} />

            <span style={{ fontSize: 12, color: 'var(--text-muted)' }}>封禁時間：</span>
            {durBtns.map(b => (
              <button key={b.days} onClick={() => setBanDays(b.days)}
                style={{
                  fontSize: 11, padding: '3px 10px', borderRadius: 4,
                  background: banDays === b.days ? 'var(--accent-red)' : 'var(--bg-input)',
                  color: banDays === b.days ? '#fff' : 'var(--text-secondary)',
                  border: `1px solid ${banDays === b.days ? 'var(--accent-red)' : 'var(--border)'}`,
                }}>
                {b.label}
              </button>
            ))}

            <button onClick={doBan} disabled={banning || selected.size === 0}
              style={{
                marginLeft: 'auto', background: selected.size > 0 ? 'var(--accent-red)' : 'var(--bg-input)',
                color: selected.size > 0 ? '#fff' : 'var(--text-muted)',
                padding: '7px 18px', fontSize: 13, fontWeight: 700,
                border: 'none', borderRadius: 6,
                opacity: banning ? 0.6 : 1,
              }}>
              {banning ? '封禁中…' : `🚫 封禁選取 (${selected.size})`}
            </button>
          </div>

          {/* 玩家列表 */}
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr style={{ background: 'var(--bg-input)', borderBottom: '1px solid var(--border)' }}>
                  <th style={{ width: 40, padding: '8px 10px' }}>
                    <input type="checkbox"
                      checked={selected.size > 0 && selected.size === list.filter(p => !p.isBanned).length}
                      onChange={e => e.target.checked ? selectAll() : selectNone()} />
                  </th>
                  <Th>狀態</Th>
                  <Th>角色名稱</Th>
                  <Th>帳號</Th>
                  <Th align="right">異常總次數</Th>
                  <Th align="right">紀錄筆數</Th>
                  <Th align="right">平均 speedtime</Th>
                  <Th align="right">最大 speedtime</Th>
                  <Th>最後偵測時間</Th>
                </tr>
              </thead>
              <tbody>
                {list.map(p => (
                  <tr key={p.account}
                    onClick={() => !p.isBanned && toggle(p.account)}
                    style={{
                      borderBottom: '1px solid var(--border)',
                      cursor: p.isBanned ? 'default' : 'pointer',
                      background: p.isBanned
                        ? 'rgba(100,100,100,.05)'
                        : selected.has(p.account)
                        ? 'rgba(245,101,101,.10)'
                        : 'transparent',
                      opacity: p.isBanned ? 0.55 : 1,
                    }}>
                    <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                      {!p.isBanned && (
                        <input type="checkbox" checked={selected.has(p.account)} onChange={() => toggle(p.account)} onClick={e => e.stopPropagation()} />
                      )}
                    </td>
                    <td style={{ padding: '8px 10px' }}>
                      {p.isBanned
                        ? <span style={{ fontSize: 11, color: 'var(--text-muted)', background: 'var(--bg-input)', padding: '2px 8px', borderRadius: 10 }}>🔒 已封禁</span>
                        : p.isOnline
                        ? <span style={{ fontSize: 11, color: 'var(--accent-green)' }}>🟢 在線</span>
                        : <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>⚫ 離線</span>}
                    </td>
                    <td style={{ padding: '8px 10px', fontWeight: 600 }}>{p.charName || '—'}</td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-muted)' }}>{p.account}</td>
                    <td style={{ padding: '8px 10px', textAlign: 'right' }}>
                      <span style={{
                        fontWeight: 700,
                        color: p.totalCnt > 1000 ? 'var(--accent-red)'
                             : p.totalCnt > 100  ? 'var(--accent-orange)'
                                                 : 'var(--text-primary)'
                      }}>{p.totalCnt.toLocaleString()}</span>
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'right', color: 'var(--text-secondary)' }}>
                      {p.records}
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'right', color: 'var(--text-muted)', fontSize: 12 }}>
                      {p.avgSpeedTime?.toFixed(1) ?? '—'}
                    </td>
                    <td style={{ padding: '8px 10px', textAlign: 'right', color: 'var(--text-muted)', fontSize: 12 }}>
                      {p.maxSpeedTime?.toLocaleString() ?? '—'}
                    </td>
                    <td style={{ padding: '8px 10px', color: 'var(--text-muted)', fontSize: 12 }}>{p.lastTime}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}

      {!loading && list.length === 0 && (
        <div style={{ textAlign: 'center', padding: 48, color: 'var(--text-muted)', background: 'var(--bg-card)', borderRadius: 10, border: '1px solid var(--border)' }}>
          <div style={{ fontSize: 36, marginBottom: 12 }}>⚡</div>
          <div>點擊「查詢」載入加速外掛玩家列表</div>
          <div style={{ fontSize: 12, marginTop: 6 }}>建議門檻設 10 次以上，減少誤判</div>
        </div>
      )}
    </div>
  )
}

const Th = ({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'right' }) => (
  <th style={{ padding: '8px 10px', textAlign: align, fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap' }}>
    {children}
  </th>
)
