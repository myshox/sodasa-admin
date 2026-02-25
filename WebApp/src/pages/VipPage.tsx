import { useEffect, useState } from 'react'
import api from '../api'
import { S } from '../strings'

const GOLD_THRESHOLD    = 5_000
const DIAMOND_THRESHOLD = 15_000

interface VipRow {
  account: string; onlineName: string; masterName: string
  payTotal: number; gold: number; crystal: number
  isOnline: boolean; loginTime: string; vipLevel: number
}

function getVipTier(payTotal: number) {
  if (payTotal >= DIAMOND_THRESHOLD) return { label: '鑽石 VIP', color: '#4dd0e1' }
  if (payTotal >= GOLD_THRESHOLD)    return { label: '黃金 VIP', color: '#ffc83d' }
  return { label: '一般', color: 'var(--text-muted)' }
}

export default function VipPage() {
  const [rows, setRows] = useState<VipRow[]>([])
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState<0 | 1 | 2>(0)

  const load = async () => {
    setLoading(true)
    try { const r = await api.get('/players/vip'); setRows(r.data) }
    finally { setLoading(false) }
  }
  useEffect(() => { load() }, [])

  const filtered = filter === 0 ? rows : rows.filter(r => r.vipLevel === filter)
  const goldCount    = rows.filter(r => r.vipLevel === 1).length
  const diamondCount = rows.filter(r => r.vipLevel === 2).length

  return (
    <div style={{ padding: 28 }}>
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 16 }}>💎 {S.navVip}</h1>

      {/* VIP 說明卡片 */}
      <div style={{ display: 'flex', gap: 12, marginBottom: 16 }}>
        <div style={{ flex: 1, background: 'rgba(255,200,61,.1)', border: '1px solid #ffc83d44', borderRadius: 10, padding: '12px 16px' }}>
          <div style={{ color: '#ffc83d', fontWeight: 700, marginBottom: 4 }}>🔸 黃金 VIP</div>
          <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>累計儲值 ≥ NT$ {GOLD_THRESHOLD.toLocaleString()}</div>
          <div style={{ fontSize: 20, fontWeight: 700, marginTop: 4 }}>{goldCount} 人</div>
        </div>
        <div style={{ flex: 1, background: 'rgba(77,208,225,.1)', border: '1px solid #4dd0e144', borderRadius: 10, padding: '12px 16px' }}>
          <div style={{ color: '#4dd0e1', fontWeight: 700, marginBottom: 4 }}>🔹 鑽石 VIP</div>
          <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>累計儲值 ≥ NT$ {DIAMOND_THRESHOLD.toLocaleString()}</div>
          <div style={{ fontSize: 20, fontWeight: 700, marginTop: 4 }}>{diamondCount} 人</div>
        </div>
      </div>

      {/* 篩選 */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 12, alignItems: 'center', flexWrap: 'wrap' }}>
        {([['全部', 0], ['🔸 黃金', 1], ['🔹 鑽石', 2]] as const).map(([label, val]) => (
          <button key={val} onClick={() => setFilter(val)}
            style={{ padding: '6px 14px', fontSize: 13, borderRadius: 6,
              background: filter === val ? 'var(--accent-blue)' : 'var(--bg-input)',
              color: filter === val ? '#fff' : 'var(--text-secondary)',
              border: `1px solid ${filter === val ? 'var(--accent-blue)' : 'var(--border)'}` }}>
            {label}
          </button>
        ))}
        <span style={{ marginLeft: 'auto', color: 'var(--text-muted)', fontSize: 13 }}>{filtered.length} 人</span>
        <button onClick={load} style={{ background: 'var(--bg-input)', border: '1px solid var(--border)', fontSize: 12, padding: '6px 12px' }}>🔄 {S.refresh}</button>
      </div>

      {/* 表格 */}
      <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
          <thead>
            <tr style={{ background: 'var(--bg-input)' }}>
              {['VIP等級', '帳號', '角色名', '主帳號', '累計儲值', '最後登入', '在線'].map(h => (
                <th key={h} style={{ padding: '8px 12px', textAlign: 'left', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' }}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {loading
              ? <tr><td colSpan={7} style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.loading}</td></tr>
              : filtered.length === 0
                ? <tr><td colSpan={7} style={{ padding: 24, textAlign: 'center', color: 'var(--text-muted)' }}>{S.noData}</td></tr>
                : filtered.map((r, i) => {
                  const tier = getVipTier(r.payTotal)
                  return (
                    <tr key={r.account + i} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '9px 12px', color: tier.color, fontWeight: 600 }}>{tier.label}</td>
                      <td style={{ padding: '9px 12px' }}>{r.account}</td>
                      <td style={{ padding: '9px 12px', color: 'var(--text-secondary)' }}>{r.onlineName || S.em}</td>
                      <td style={{ padding: '9px 12px', color: 'var(--text-muted)', fontSize: 12 }}>{r.masterName || '—'}</td>
                      <td style={{ padding: '9px 12px', color: 'var(--accent-orange)', fontWeight: 600 }}>NT$ {r.payTotal.toLocaleString()}</td>
                      <td style={{ padding: '9px 12px', color: 'var(--text-muted)', fontSize: 12 }}>{r.loginTime || '—'}</td>
                      <td style={{ padding: '9px 12px' }}>
                        {r.isOnline && <span style={{ color: 'var(--accent-green)', fontSize: 11 }}>🟢 在線</span>}
                      </td>
                    </tr>
                  )
                })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
