import { useState, useEffect, useCallback } from 'react'
import api from '../api'
import type { PetRankType, PetRankEntry, PetPlayerEntry } from '../api'

const card: React.CSSProperties = {
  background: 'var(--neu-bg)', borderRadius: 16,
  padding: '20px 24px', marginBottom: 20,
  boxShadow: '0 2px 12px rgba(0,0,0,.08)'
}
const tbl: React.CSSProperties = { width: '100%', borderCollapse: 'collapse', fontSize: 13 }
const th: React.CSSProperties = {
  padding: '8px 10px', background: 'var(--primary)', color: '#fff',
  textAlign: 'left', fontWeight: 600
}
const td = (i: number): React.CSSProperties => ({
  padding: '7px 10px', borderBottom: '1px solid var(--border)',
  background: i % 2 === 0 ? 'transparent' : 'rgba(0,0,0,.02)'
})
const btn = (variant: 'primary' | 'danger' | 'ghost' | 'success' = 'primary'): React.CSSProperties => ({
  padding: '5px 12px', borderRadius: 8, border: 'none', cursor: 'pointer', fontSize: 12,
  background: variant === 'primary' ? 'var(--primary)'
    : variant === 'danger'  ? '#e74c3c'
    : variant === 'success' ? '#27ae60'
    : 'rgba(0,0,0,.06)',
  color: variant === 'ghost' ? 'inherit' : '#fff',
  marginLeft: 4
})

const rankBadge = (rank: number) => {
  if (rank === 1) return <span style={{ fontSize: 18 }}>🥇</span>
  if (rank === 2) return <span style={{ fontSize: 18 }}>🥈</span>
  if (rank === 3) return <span style={{ fontSize: 18 }}>🥉</span>
  return <span style={{ color: '#888', fontWeight: 600 }}>#{rank}</span>
}

type LeaderboardMode = 'best' | 'raw'

export default function PetRankPage() {
  const [petTypes,    setPetTypes]    = useState<PetRankType[]>([])
  const [selectedPet, setSelectedPet] = useState<PetRankType | null>(null)
  const [leaderboard, setLeaderboard] = useState<PetRankEntry[]>([])
  const [loading,     setLoading]     = useState(false)
  const [msg,         setMsg]         = useState('')
  const [lbMode,      setLbMode]      = useState<LeaderboardMode>('best')

  // 查單一玩家
  const [playerQ,      setPlayerQ]     = useState('')
  const [playerEntries,setPlayerEntries] = useState<PetPlayerEntry[] | null>(null)
  const [playerLoading,setPlayerLoading] = useState(false)

  // 載入寵物種類
  useEffect(() => {
    api.get<PetRankType[]>('/petrank/pets').then(r => {
      setPetTypes(r.data)
      if (r.data.length > 0) {
        setSelectedPet(r.data[0])
      }
    }).catch(() => {})
  }, [])

  // 切換寵物或顯示模式時載入排行
  useEffect(() => {
    if (!selectedPet) return
    setLoading(true)
    setLeaderboard([])
    const limit = lbMode === 'raw' ? 500 : 50
    api.get<PetRankEntry[]>('/petrank/leaderboard', {
      params: { petId: selectedPet.id, limit, mode: lbMode },
    })
      .then(r => setLeaderboard(r.data))
      .finally(() => setLoading(false))
  }, [selectedPet, lbMode])

  const flashMsg = (m: string) => { setMsg(m); setTimeout(() => setMsg(''), 3000) }

  const exportCsv = () => {
    if (leaderboard.length === 0) return
    const petName = selectedPet?.name ?? 'pet'
    const header = '名次,角色名,帳號,寵物名,戰鬥力,HP,攻擊,防禦,速度,提交次數,提交時間,審核'
    const esc = (v: string | number) => {
      const s = String(v)
      return s.includes(',') || s.includes('"') || s.includes('\n') ? `"${s.replace(/"/g,'""')}"` : s
    }
    const rows = leaderboard.map(e =>
      [e.rank, e.author, e.cdkey, e.petName, e.sum, e.hp, e.attack, e.def, e.quick,
       e.entryCount, e.inserttime, e.check ? '已審核' : '待審'].map(esc).join(',')
    )
    const csv = '\uFEFF' + [header, ...rows].join('\n')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = `練寵_${petName}_${new Date().toISOString().slice(0,10)}.csv`
    a.click(); URL.revokeObjectURL(url)
    flashMsg(`已匯出 ${leaderboard.length} 筆`)
  }

  const toggleCheck = async (entry: PetRankEntry) => {
    try {
      await api.put(`/petrank/${encodeURIComponent(entry.unicode)}/check`, !entry.check)
      setLeaderboard(prev => prev.map(e =>
        e.unicode === entry.unicode ? { ...e, check: !entry.check } : e
      ))
      flashMsg(`已${!entry.check ? '通過審核' : '取消審核'}：${entry.author}`)
    } catch { flashMsg('操作失敗') }
  }

  const deleteEntry = async (entry: PetRankEntry) => {
    if (!confirm(`確定刪除 ${entry.author} 的記錄（分數 ${entry.sum}）？此操作不可還原！`)) return
    try {
      await api.delete(`/petrank/${encodeURIComponent(entry.unicode)}`)
      setLeaderboard(prev => prev.filter(e => e.unicode !== entry.unicode).map((e, i) => ({ ...e, rank: i + 1 })))
      flashMsg(`已刪除 ${entry.author} 的記錄`)
    } catch { flashMsg('刪除失敗') }
  }

  const queryPlayer = useCallback(async () => {
    if (!playerQ.trim()) return
    setPlayerLoading(true)
    setPlayerEntries(null)
    try {
      const r = await api.get<PetPlayerEntry[]>(`/petrank/player/${encodeURIComponent(playerQ.trim())}`)
      setPlayerEntries(r.data)
    } catch { setPlayerEntries([]) } finally { setPlayerLoading(false) }
  }, [playerQ])

  const deletePlayerEntry = async (entry: PetPlayerEntry) => {
    if (!confirm(`確定刪除此記錄？`)) return
    try {
      await api.delete(`/petrank/${encodeURIComponent(entry.unicode)}`)
      setPlayerEntries(prev => prev ? prev.filter(e => e.unicode !== entry.unicode) : prev)
      flashMsg('已刪除')
    } catch { flashMsg('刪除失敗') }
  }

  return (
    <div className="gm-page-stack">
      <h2 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>🐾 練寵排行榜管理</h2>

      {msg && (
        <div style={{ padding: '10px 16px', borderRadius: 10, background: '#d4edda', color: '#155724', marginBottom: 16 }}>
          ✅ {msg}
        </div>
      )}

      {/* ── 寵物種類選擇 ── */}
      <div style={card}>
        <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14 }}>本期寵物選擇</h3>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          {petTypes.map(p => (
            <button
              key={p.id}
              onClick={() => setSelectedPet(p)}
              style={{
                padding: '8px 16px', borderRadius: 20, border: '2px solid',
                borderColor: selectedPet?.id === p.id ? 'var(--primary)' : 'var(--border)',
                background: selectedPet?.id === p.id ? 'var(--primary)' : 'transparent',
                color: selectedPet?.id === p.id ? '#fff' : 'inherit',
                cursor: 'pointer', fontSize: 13, fontWeight: 600
              }}
            >
              {p.name}
              <span style={{ marginLeft: 6, opacity: 0.75, fontSize: 11 }}>
                ({p.entryCount}筆 / 最高{p.topScore})
              </span>
            </button>
          ))}
        </div>

        {selectedPet && (
          <div style={{ marginTop: 14, padding: '10px 14px', background: 'rgba(0,0,0,.03)', borderRadius: 10, fontSize: 13, color: '#555' }}>
            <strong>本期：{selectedPet.name}</strong>
            　 最高分：<strong style={{ color: 'var(--primary)' }}>{selectedPet.topScore}</strong>
            　 總參賽：<strong>{selectedPet.entryCount}</strong> 筆
            　 最後提交：{selectedPet.lastEntry}
          </div>
        )}
      </div>

      {/* ── 排行榜 ── */}
      <div style={card}>
        <div style={{ marginBottom: 12, display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 10 }}>
          <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>顯示：</span>
          <select
            value={lbMode}
            onChange={e => setLbMode(e.target.value as LeaderboardMode)}
            style={{ padding: '6px 12px', borderRadius: 8, border: '1px solid var(--border)', fontSize: 13, minWidth: 280 }}
          >
            <option value="best">每人最高戰力一筆（名次＝人數）</option>
            <option value="raw">全部提交列・僅依戰力 sum（WHERE id=本期 + ORDER BY sum）</option>
          </select>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14 }}>
          <h3 style={{ fontSize: 15, fontWeight: 700, margin: 0 }}>
            戰鬥力排行榜 {selectedPet ? `— ${selectedPet.name}` : ''}
          </h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 12, color: '#888' }}>
              {lbMode === 'best'
                ? '每位玩家一列：最高戰力；同分取較晚提交。若要對齊技術全表排序請選「全部提交列」。'
                : '每一筆提交一列，僅依戰力 sum 排序（與技術 WHERE id 後按戰力一致）；同分時順序由資料庫決定。'}
            </span>
            {leaderboard.length > 0 && (
              <button onClick={exportCsv} style={{ ...btn('success'), padding: '5px 14px' }}>
                📥 匯出 CSV
              </button>
            )}
          </div>
        </div>

        {loading && <div style={{ color: '#888', padding: 20, textAlign: 'center' }}>載入中…</div>}

        {!loading && leaderboard.length === 0 && selectedPet && (
          <div style={{ color: '#aaa', padding: 20, textAlign: 'center' }}>無資料</div>
        )}

        {leaderboard.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <table style={tbl}>
              <thead>
                <tr>
                  {[(lbMode === 'raw' ? '列#' : '名次'),'玩家名','帳號','戰鬥力','HP','攻擊','防禦','速度','提交次數','提交時間','審核','操作'].map(h => (
                    <th key={h} style={th}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {leaderboard.map((e, i) => (
                  <tr key={e.unicode}>
                    <td style={td(i)}>{rankBadge(e.rank)}</td>
                    <td style={{ ...td(i), fontWeight: 600 }}>{e.author}</td>
                    <td style={{ ...td(i), color: '#888', fontSize: 12 }}>{e.cdkey}</td>
                    <td style={{ ...td(i), fontWeight: 700, color: 'var(--primary)' }}>{e.sum}</td>
                    <td style={td(i)}>{e.hp}</td>
                    <td style={td(i)}>{e.attack}</td>
                    <td style={td(i)}>{e.def}</td>
                    <td style={td(i)}>{e.quick}</td>
                    <td style={{ ...td(i), textAlign: 'center' }}>
                      {e.entryCount > 1
                        ? <span style={{ background: '#fff3cd', color: '#856404', padding: '2px 8px', borderRadius: 10, fontSize: 11 }}>
                            ⚠️ {e.entryCount}次
                          </span>
                        : <span style={{ color: '#888' }}>{e.entryCount}</span>
                      }
                    </td>
                    <td style={{ ...td(i), fontSize: 11, color: '#888' }}>{e.inserttime}</td>
                    <td style={td(i)}>
                      <span style={{
                        padding: '2px 8px', borderRadius: 10, fontSize: 11,
                        background: e.check ? '#d4edda' : '#f8d7da',
                        color: e.check ? '#155724' : '#721c24'
                      }}>
                        {e.check ? '✅ 已審核' : '⏳ 待審'}
                      </span>
                    </td>
                    <td style={td(i)}>
                      <button style={btn(e.check ? 'ghost' : 'success')} onClick={() => toggleCheck(e)}>
                        {e.check ? '取消' : '通過'}
                      </button>
                      <button style={btn('danger')} onClick={() => deleteEntry(e)}>刪除</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* ── 玩家查詢（多號刷榜偵測）── */}
      <div style={card}>
        <h3 style={{ fontSize: 15, fontWeight: 700, marginBottom: 14 }}>🔍 查玩家所有參賽記錄（多號偵測）</h3>
        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          <input
            value={playerQ}
            onChange={e => setPlayerQ(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && queryPlayer()}
            placeholder="輸入帳號或角色名…"
            style={{
              flex: 1, padding: '8px 12px', borderRadius: 8,
              border: '1px solid var(--border)', fontSize: 13
            }}
          />
          <button onClick={queryPlayer} style={{ ...btn('primary'), padding: '8px 20px', fontSize: 13 }}>
            查詢
          </button>
        </div>

        {playerLoading && <div style={{ color: '#888', textAlign: 'center', padding: 16 }}>載入中…</div>}

        {playerEntries !== null && playerEntries.length === 0 && (
          <div style={{ color: '#aaa', textAlign: 'center', padding: 16 }}>此玩家無練寵記錄</div>
        )}

        {playerEntries && playerEntries.length > 0 && (
          <>
            <div style={{ marginBottom: 10, color: '#555', fontSize: 13 }}>
              共 <strong>{playerEntries.length}</strong> 筆記錄
              {playerEntries.length > 1 && (
                <span style={{ marginLeft: 8, background: '#fff3cd', color: '#856404', padding: '2px 8px', borderRadius: 10, fontSize: 11 }}>
                  ⚠️ 多次提交，請確認是否正常
                </span>
              )}
            </div>
            <div style={{ overflowX: 'auto' }}>
              <table style={tbl}>
                <thead>
                  <tr>
                    {['寵物','帳號','角色名','分數','HP','攻擊','防禦','速度','提交時間','審核','操作'].map(h => (
                      <th key={h} style={th}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {playerEntries.map((e, i) => (
                    <tr key={e.unicode}>
                      <td style={{ ...td(i), fontWeight: 600 }}>{e.petName}</td>
                      <td style={{ ...td(i), fontSize: 12, color: '#888' }}>{e.cdkey}</td>
                      <td style={td(i)}>{e.author}</td>
                      <td style={{ ...td(i), fontWeight: 700, color: 'var(--primary)' }}>{e.sum}</td>
                      <td style={td(i)}>{e.hp}</td>
                      <td style={td(i)}>{e.attack}</td>
                      <td style={td(i)}>{e.def}</td>
                      <td style={td(i)}>{e.quick}</td>
                      <td style={{ ...td(i), fontSize: 11, color: '#888' }}>{e.inserttime}</td>
                      <td style={td(i)}>
                        <span style={{
                          padding: '2px 8px', borderRadius: 10, fontSize: 11,
                          background: e.check ? '#d4edda' : '#f8d7da',
                          color: e.check ? '#155724' : '#721c24'
                        }}>
                          {e.check ? '✅ 已審核' : '⏳ 待審'}
                        </span>
                      </td>
                      <td style={td(i)}>
                        <button style={btn('danger')} onClick={() => deletePlayerEntry(e)}>刪除</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
