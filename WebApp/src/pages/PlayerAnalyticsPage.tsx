import { useState, useEffect } from 'react'
import api from '../api'
import { S } from '../strings'

export default function PlayerAnalyticsPage() {
  const [stats, setStats] = useState<{ totalPlayers: number; onlinePlayers: number; newToday: number; todayActive: number } | null>(null)
  const [hour, setHour] = useState<number[]>([])
  const [weekday, setWeekday] = useState<number[]>([])
  const [growth, setGrowth] = useState<{ dates: string[]; counts: number[] } | null>(null)
  const [retention, setRetention] = useState<Record<string, { cohort: number; retained: number; rate: number }>>({})
  const [inactive, setInactive] = useState<any[]>([])
  const [loading, setLoading] = useState(true)

  const load = () => {
    setLoading(true)
    Promise.all([
      api.get('/analytics/player/stats').then(r => setStats(r.data)),
      api.get('/analytics/player/hour').then(r => setHour(r.data || [])),
      api.get('/analytics/player/weekday').then(r => setWeekday(r.data || [])),
      api.get('/analytics/player/growth').then(r => setGrowth(r.data)),
      api.get('/analytics/player/retention').then(r => setRetention(r.data || {})),
      api.get('/analytics/player/inactive?days=30').then(r => setInactive(r.data || [])),
    ]).finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  if (loading) return <div style={{ padding: 28 }}><p style={{ color: 'var(--text-muted)' }}>載入中…</p></div>

  const weekLabels = ['周日', '周一', '周二', '周三', '周四', '周五', '周六']

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>📊 {S.navPlayerAna}</h1>
      <button onClick={load} style={{ marginBottom: 20, padding: '8px 16px', background: 'var(--accent-blue)', color: '#fff', borderRadius: 8 }}>↺ 重新整理</button>

      {stats && (
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 24 }}>
          <Card icon="👥" title="總玩家數" value={stats.totalPlayers?.toLocaleString() ?? '0'} />
          <Card icon="🟢" title="目前在線" value={stats.onlinePlayers?.toLocaleString() ?? '0'} />
          <Card icon="🆕" title="今日新增" value={stats.newToday?.toLocaleString() ?? '0'} />
          <Card icon="🕹" title="今日活躍" value={stats.todayActive?.toLocaleString() ?? '0'} />
        </div>
      )}

      {hour.length > 0 && (
        <Section title="登入時段（24 小時）">
          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', alignItems: 'flex-end', height: 120 }}>
            {hour.map((v, i) => (
              <div key={i} title={`${i}時: ${v}`} style={{ width: 24, height: Math.max(4, (v / Math.max(...hour)) * 80), background: 'var(--accent-blue)', borderRadius: 4 }} />
            ))}
          </div>
        </Section>
      )}

      {weekday.length > 0 && (
        <Section title="登入星期分佈">
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {weekLabels.map((label, i) => (
              <span key={i} style={{ padding: '6px 12px', background: 'var(--bg-card)', borderRadius: 8, fontSize: 13 }}>{label}: {weekday[i] ?? 0}</span>
            ))}
          </div>
        </Section>
      )}

      {growth && growth.dates?.length > 0 && (
        <Section title="帳號成長（每日新增）">
          <div style={{ display: 'grid', gridTemplateColumns: '100px 80px', gap: 8, fontSize: 12 }}>
            {growth.dates.map((d, i) => [
              <span key={`${d}-d`}>{d}</span>,
              <span key={`${d}-c`}>{growth.counts?.[i] ?? 0}</span>,
            ])}
          </div>
        </Section>
      )}

      {Object.keys(retention).length > 0 && (
        <Section title="留存分析">
          <div style={{ display: 'grid', gridTemplateColumns: '80px 100px 100px 80px', gap: 12, fontSize: 13 }}>
            <span style={{ fontWeight: 600 }}>區間</span><span style={{ fontWeight: 600 }}>同期群</span><span style={{ fontWeight: 600 }}>留存數</span><span style={{ fontWeight: 600 }}>留存率</span>
            {Object.entries(retention).flatMap(([label, v]) => [
              <span key={`${label}-l`}>{label}</span>,
              <span key={`${label}-c`}>{v.cohort}</span>,
              <span key={`${label}-r`}>{v.retained}</span>,
              <span key={`${label}-p`}>{typeof v.rate === 'number' ? v.rate.toFixed(1) + '%' : String(v.rate)}</span>,
            ])}
          </div>
        </Section>
      )}

      {inactive.length > 0 && (
        <Section title="沉睡玩家（超過 30 天未登入）">
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 140px 80px', padding: '10px 16px', background: 'var(--bg-sidebar)', fontSize: 12, fontWeight: 600, color: 'var(--text-muted)' }}>
              <span>角色</span><span>帳號</span><span>最後登入</span><span>天數</span>
            </div>
            {inactive.slice(0, 100).map((row: any, i) => (
              <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 140px 80px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13 }}>
                <span>{row.onlineName}</span><span>{row.account}</span><span>{row.lastLogin}</span><span>{row.daysSince}</span>
              </div>
            ))}
            {inactive.length > 100 && <p style={{ padding: 12, color: 'var(--text-muted)', fontSize: 12 }}>僅顯示前 100 筆，共 {inactive.length} 筆</p>}
          </div>
        </Section>
      )}
    </div>
  )
}

function Card({ icon, title, value }: { icon: string; title: string; value: string }) {
  return (
    <div style={{ minWidth: 160, padding: 16, background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10 }}>
      <div style={{ fontSize: 20, marginBottom: 4 }}>{icon}</div>
      <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>{title}</div>
      <div style={{ fontSize: 18, fontWeight: 700 }}>{value}</div>
    </div>
  )
}
function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 24 }}>
      <h2 style={{ fontSize: 16, marginBottom: 12, color: 'var(--text-secondary)' }}>{title}</h2>
      {children}
    </div>
  )
}
