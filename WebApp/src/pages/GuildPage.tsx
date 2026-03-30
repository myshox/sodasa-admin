import { useState, useEffect } from 'react'
import api from '../api'

interface GuildInfo {
  guildId:     number
  guildName:   string
  memberCount: number
  lastActive:  string
  shopContrib: number
}

interface GuildMember {
  cdkey:       string
  charName:    string
  onlineName:  string
  joinTime:    string
  payTotal:    number
  gold:        number
  isOnline:    boolean
  shopContrib: number
}

export default function GuildPage() {
  const [guilds,    setGuilds]    = useState<GuildInfo[]>([])
  const [members,   setMembers]   = useState<GuildMember[]>([])
  const [selected,  setSelected]  = useState<GuildInfo | null>(null)
  const [search,    setSearch]    = useState('')
  const [loading,   setLoading]   = useState(false)
  const [loadingM,  setLoadingM]  = useState(false)
  const [msg,       setMsg]       = useState<{ text: string; ok: boolean } | null>(null)
  const [selMembers, setSelMembers] = useState<Set<string>>(new Set())
  const [showTransfer, setShowTransfer] = useState(false)
  const [transferTarget, setTransferTarget] = useState<GuildInfo | null>(null)

  const flash = (text: string, ok = true) => {
    setMsg({ text, ok })
    setTimeout(() => setMsg(null), 4000)
  }

  const loadGuilds = async () => {
    setLoading(true)
    try {
      const res = await api.get<GuildInfo[]>('/guild')
      setGuilds(res.data)
    } catch { flash('載入失敗', false) }
    finally { setLoading(false) }
  }

  const selectGuild = async (g: GuildInfo) => {
    setSelected(g)
    setSelMembers(new Set())
    setLoadingM(true)
    try {
      const res = await api.get<GuildMember[]>(`/guild/${g.guildId}/members`)
      setMembers(res.data)
    } catch { flash('載入成員失敗', false) }
    finally { setLoadingM(false) }
  }

  const dissolve = async () => {
    if (!selected) return
    if (!confirm(`確定要解散家族「${selected.guildName}」？此操作不可還原。`)) return
    try {
      await api.delete(`/guild/${selected.guildId}`)
      flash('家族已解散')
      setSelected(null); setMembers([])
      await loadGuilds()
    } catch (e: any) { flash(e.response?.data?.message ?? '解散失敗', false) }
  }

  const kick = async () => {
    if (!selected || selMembers.size === 0) return
    const names = members.filter(m => selMembers.has(m.cdkey)).map(m => m.charName || m.cdkey).join(', ')
    if (!confirm(`確定要踢出以下成員？\n${names}`)) return
    let ok = 0, fail = 0
    for (const cdkey of selMembers) {
      try {
        await api.delete(`/guild/${selected.guildId}/members/${cdkey}`)
        ok++
      } catch { fail++ }
    }
    flash(`踢出完成：${ok} 成功 / ${fail} 失敗`, fail === 0)
    setSelMembers(new Set())
    await selectGuild(selected)
  }

  const doTransfer = async () => {
    if (!transferTarget || selMembers.size === 0) return
    let ok = 0, fail = 0
    for (const cdkey of selMembers) {
      try {
        await api.post('/guild/members/transfer', {
          cdkey, targetGuildId: transferTarget.guildId, targetGuildName: transferTarget.guildName
        })
        ok++
      } catch { fail++ }
    }
    flash(`轉移完成：${ok} 成功 / ${fail} 失敗`, fail === 0)
    setSelMembers(new Set()); setShowTransfer(false); setTransferTarget(null)
    if (selected) await selectGuild(selected)
  }

  useEffect(() => { loadGuilds() }, [])

  const filtered = guilds.filter(g =>
    !search || g.guildName.toLowerCase().includes(search.toLowerCase()) ||
    g.guildId.toString().includes(search)
  )
  const otherGuilds = guilds.filter(g => g.guildId !== selected?.guildId)

  const toggleMember = (cdkey: string) => {
    setSelMembers(prev => {
      const n = new Set(prev)
      n.has(cdkey) ? n.delete(cdkey) : n.add(cdkey)
      return n
    })
  }
  const selectAll = () => setSelMembers(new Set(members.map(m => m.cdkey)))
  const clearSel  = () => setSelMembers(new Set())

  return (
    <div className="gm-page-fill guild-page">
      {/* 標題列 */}
      <div className="guild-topbar">
        <span className="guild-topbar-title">家族管理</span>
        <input
          value={search} onChange={e => setSearch(e.target.value)}
          placeholder="搜尋家族名稱…"
          className="gm-search-input gm-search-input--toolbar"
          enterKeyHint="search"
        />
        <button type="button" onClick={loadGuilds} disabled={loading}
          className="primary"
          style={{ padding: '10px 18px', borderRadius: 10, flexShrink: 0, touchAction: 'manipulation' }}>
          {loading ? '載入中...' : '重新載入'}
        </button>
        {msg && (
          <span style={{ marginLeft: 8, color: msg.ok ? 'var(--accent-green)' : 'var(--accent-red)', fontSize: 13 }}>
            {msg.text}
          </span>
        )}
      </div>

      {/* 主體：左（家族列表）+ 右（成員） */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden', gap: 0 }}>
        {/* ── 左側：家族列表 ── */}
        <div className="guild-sidebar">
          <div className="guild-list-hint">
            家族列表（共 {filtered.length} 個）
          </div>
          <div style={{ flex: 1, overflowY: 'auto' }}>
            {loading && <div style={{ padding: 16, color: 'var(--text-muted)' }}>載入中...</div>}
            {!loading && filtered.length === 0 && (
              <div style={{ padding: 16, color: '#666' }}>無家族資料</div>
            )}
            {filtered.map(g => (
              <div key={g.guildId}
                onClick={() => selectGuild(g)}
                style={{
                  padding: '10px 14px', cursor: 'pointer',
                  background: selected?.guildId === g.guildId ? '#1e3558' : 'transparent',
                  borderLeft: selected?.guildId === g.guildId ? '3px solid #4c9cf5' : '3px solid transparent',
                  borderBottom: '1px solid #1e1f27',
                  transition: 'background 0.15s'
                }}
              >
                <div style={{ fontWeight: 600, color: '#eee', marginBottom: 2 }}>{g.guildName}</div>
                <div style={{ fontSize: 12, color: '#888', display: 'flex', gap: 10 }}>
                  <span>ID: {g.guildId}</span>
                  <span>成員: {g.memberCount}</span>
                  {g.shopContrib > 0 && <span>貢獻: {g.shopContrib.toLocaleString()}</span>}
                </div>
                <div style={{ fontSize: 11, color: '#666', marginTop: 2 }}>最近活動: {g.lastActive || '-'}</div>
              </div>
            ))}
          </div>
        </div>

        {/* ── 右側：成員列表 ── */}
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          {!selected ? (
            <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#555', fontSize: 15 }}>
              請從左側選擇一個家族
            </div>
          ) : (
            <>
              {/* 家族標題 + 操作按鈕 */}
              <div style={{ padding: '10px 16px', background: '#1c1e26', borderBottom: '1px solid #2a2d3a', display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
                <span style={{ fontSize: 15, fontWeight: 700, color: '#fff', marginRight: 8 }}>
                  「{selected.guildName}」  ID: {selected.guildId}  人數: {selected.memberCount}
                </span>
                <button onClick={dissolve}
                  style={{ padding: '4px 12px', background: '#c0392b', border: 'none', borderRadius: 5, color: '#fff', cursor: 'pointer', fontSize: 13 }}>
                  解散家族
                </button>
                <button onClick={kick} disabled={selMembers.size === 0}
                  style={{ padding: '4px 12px', background: selMembers.size > 0 ? '#b55e00' : '#555', border: 'none', borderRadius: 5, color: '#fff', cursor: selMembers.size > 0 ? 'pointer' : 'not-allowed', fontSize: 13 }}>
                  踢出選中 ({selMembers.size})
                </button>
                <button onClick={() => setShowTransfer(true)} disabled={selMembers.size === 0 || otherGuilds.length === 0}
                  style={{ padding: '4px 12px', background: selMembers.size > 0 && otherGuilds.length > 0 ? '#2060c0' : '#555', border: 'none', borderRadius: 5, color: '#fff', cursor: selMembers.size > 0 && otherGuilds.length > 0 ? 'pointer' : 'not-allowed', fontSize: 13 }}>
                  轉移至其他家族
                </button>
                <span style={{ marginLeft: 'auto', fontSize: 12, color: '#888' }}>
                  <span onClick={selectAll} style={{ cursor: 'pointer', color: '#4c9cf5', marginRight: 8 }}>全選</span>
                  <span onClick={clearSel}  style={{ cursor: 'pointer', color: '#888' }}>清除</span>
                </span>
              </div>

              {/* 成員表格標題 */}
              <div style={{ display: 'grid', gridTemplateColumns: '36px 1fr 150px 90px 80px 120px', padding: '6px 14px', background: '#1a1c25', fontSize: 12, color: '#666', borderBottom: '1px solid #222' }}>
                <span></span>
                <span>角色名稱</span>
                <span>帳號</span>
                <span>貢獻值</span>
                <span>累積儲值</span>
                <span>加入時間</span>
              </div>

              {/* 成員列表 */}
              <div style={{ flex: 1, overflowY: 'auto' }}>
                {loadingM && <div style={{ padding: 16, color: '#888' }}>載入成員中...</div>}
                {!loadingM && members.length === 0 && (
                  <div style={{ padding: 16, color: '#666' }}>此家族無成員記錄</div>
                )}
                {members.map(m => (
                  <div key={m.cdkey}
                    onClick={() => toggleMember(m.cdkey)}
                    style={{
                      display: 'grid',
                      gridTemplateColumns: '36px 1fr 150px 90px 80px 120px',
                      padding: '7px 14px', cursor: 'pointer',
                      background: selMembers.has(m.cdkey) ? '#1e3558' : 'transparent',
                      borderBottom: '1px solid #1e1f27',
                      alignItems: 'center'
                    }}
                  >
                    <input type="checkbox" readOnly checked={selMembers.has(m.cdkey)}
                      style={{ accentColor: '#4c9cf5', cursor: 'pointer' }} />
                    <span style={{ color: m.isOnline ? '#4ddd80' : '#ccc', fontWeight: m.isOnline ? 600 : 400 }}>
                      {m.isOnline && '● '}{m.charName || m.onlineName || '-'}
                    </span>
                    <span style={{ color: '#aaa', fontSize: 12 }}>{m.cdkey}</span>
                    <span style={{ color: m.shopContrib > 0 ? '#f5c542' : '#666', fontSize: 12 }}>
                      {m.shopContrib > 0 ? m.shopContrib.toLocaleString() : '-'}
                    </span>
                    <span style={{ color: '#aaa', fontSize: 12 }}>
                      {m.payTotal > 0 ? m.payTotal.toLocaleString() : '-'}
                    </span>
                    <span style={{ color: '#666', fontSize: 12 }}>{m.joinTime || '-'}</span>
                  </div>
                ))}
              </div>
              <div style={{ padding: '5px 14px', fontSize: 12, color: '#666', borderTop: '1px solid #222' }}>
                共 {members.length} 名成員
              </div>
            </>
          )}
        </div>
      </div>

      {/* 轉移家族 Modal */}
      {showTransfer && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#1c1e26', borderRadius: 10, padding: 24, width: 360, border: '1px solid #2a2d3a' }}>
            <div style={{ fontSize: 16, fontWeight: 700, color: '#fff', marginBottom: 12 }}>轉移成員至其他家族</div>
            <div style={{ fontSize: 13, color: '#aaa', marginBottom: 12 }}>
              已選 {selMembers.size} 名成員，目標家族：
            </div>
            <select
              value={transferTarget?.guildId ?? ''}
              onChange={e => {
                const id = parseInt(e.target.value)
                setTransferTarget(otherGuilds.find(g => g.guildId === id) ?? null)
              }}
              style={{ width: '100%', padding: '7px 10px', background: '#22242e', border: '1px solid #3a3d4e', borderRadius: 6, color: '#ddd', marginBottom: 16, fontSize: 14 }}
            >
              <option value="">-- 選擇目標家族 --</option>
              {otherGuilds.map(g => (
                <option key={g.guildId} value={g.guildId}>
                  [{g.guildId}] {g.guildName}（{g.memberCount} 人）
                </option>
              ))}
            </select>
            <div style={{ display: 'flex', gap: 10 }}>
              <button onClick={doTransfer} disabled={!transferTarget}
                style={{ flex: 1, padding: '8px 0', background: transferTarget ? '#2060c0' : '#555', border: 'none', borderRadius: 6, color: '#fff', cursor: transferTarget ? 'pointer' : 'not-allowed', fontWeight: 600 }}>
                確定轉移
              </button>
              <button onClick={() => { setShowTransfer(false); setTransferTarget(null) }}
                style={{ flex: 1, padding: '8px 0', background: '#3a3d4e', border: 'none', borderRadius: 6, color: '#ddd', cursor: 'pointer' }}>
                取消
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
