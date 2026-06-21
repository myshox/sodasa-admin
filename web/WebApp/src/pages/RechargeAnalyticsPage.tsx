import { useState, useEffect } from 'react'
import api from '../api'
import { S } from '../strings'

export default function RechargeAnalyticsPage() {
  const [kpi, setKpi] = useState<{ todayRevenue: number; monthRevenue: number; totalRevenue: number; payingPlayers: number } | null>(null)
  const [daily, setDaily] = useState<{ dates: string[]; amounts: number[]; counts: number[] } | null>(null)
  const [monthly, setMonthly] = useState<{ months: string[]; amounts: number[]; counts: number[] } | null>(null)
  const [tier, setTier] = useState<Record<string, number>>({})
  const [firstPay, setFirstPay] = useState<Record<string, number>>({})
  const [loading, setLoading] = useState(true)

  const load = () => {
    setLoading(true)
    Promise.all([
      api.get('/analytics/recharge/kpi').then(r => setKpi(r.data)),
      api.get('/analytics/recharge/daily').then(r => setDaily(r.data)),
      api.get('/analytics/recharge/monthly').then(r => setMonthly(r.data)),
      api.get('/analytics/recharge/tier').then(r => setTier(r.data || {})),
      api.get('/analytics/recharge/firstpay').then(r => setFirstPay(r.data || {})),
    ]).catch(() => {}).finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  if (loading) return <div style={{ padding: 28 }}><p style={{ color: 'var(--text-muted)' }}>載入中…</p></div>

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 20 }}>💰 {S.navRechargeAna}</h1>
      <button onClick={load} style={{ marginBottom: 20, padding: '8px 16px', background: 'var(--accent-blue)', color: '#fff', borderRadius: 8 }}>↺ 重新整理</button>

      {kpi && (
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 24 }}>
          <Card icon="📅" title="今日充值（NT$）" value={`NT$ ${Number(kpi.todayRevenue).toLocaleString()}`} />
          <Card icon="📆" title="本月充值（NT$）" value={`NT$ ${Number(kpi.monthRevenue).toLocaleString()}`} />
          <Card icon="🏦" title="累計充值（NT$）" value={`NT$ ${Number(kpi.totalRevenue).toLocaleString()}`} />
          <Card icon="🧑‍💼" title="付費玩家人數" value={`${kpi.payingPlayers?.toLocaleString() ?? 0} 人`} />
        </div>
      )}

      {daily && daily.dates?.length > 0 && (
        <Section title="過去 30 天每日充值">
          <div style={{ display: 'grid', gridTemplateColumns: '100px 120px 80px', gap: 8, padding: '8px 0', fontSize: 12 }}>
            <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>日期</span>
            <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>金額（NT$）</span>
            <span style={{ color: 'var(--text-muted)', fontWeight: 600 }}>筆數</span>
            {daily.dates.map((d, i) => [
              <span key={`${d}-d`}>{d}</span>,
              <span key={`${d}-a`}>{Number(daily.amounts?.[i] ?? 0).toLocaleString()}</span>,
              <span key={`${d}-c`}>{daily.counts?.[i] ?? 0}</span>,
            ])}
          </div>
        </Section>
      )}

      {monthly && monthly.months?.length > 0 && (
        <Section title="近 12 個月">
          <div style={{ display: 'grid', gridTemplateColumns: '100px 120px 80px', gap: 8, padding: '8px 0', fontSize: 12 }}>
            {monthly.months.map((m, i) => [
              <span key={`${m}-m`}>{m}</span>,
              <span key={`${m}-a`}>{Number(monthly.amounts?.[i] ?? 0).toLocaleString()}</span>,
              <span key={`${m}-c`}>{monthly.counts?.[i] ?? 0}</span>,
            ])}
          </div>
        </Section>
      )}

      {Object.keys(tier).length > 0 && (
        <Section title="付費分層">
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {Object.entries(tier).map(([label, count]) => (
              <span key={label} style={{ padding: '6px 12px', background: 'var(--bg-card)', borderRadius: 8, fontSize: 13 }}>{label}: {count}</span>
            ))}
          </div>
        </Section>
      )}

      {Object.keys(firstPay).length > 0 && (
        <Section title="首次付費時機（註冊到首次充值天數）">
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {Object.entries(firstPay).map(([label, count]) => (
              <span key={label} style={{ padding: '6px 12px', background: 'var(--bg-card)', borderRadius: 8, fontSize: 13 }}>{label}: {count}</span>
            ))}
          </div>
        </Section>
      )}

      {!kpi && !daily?.dates?.length && !monthly?.months?.length && Object.keys(tier).length === 0 && (
        <p style={{ color: 'var(--text-muted)' }}>尚無充值資料（需 recharge_orders 表）</p>
      )}
    </div>
  )
}

function Card({ icon, title, value }: { icon: string; title: string; value: string }) {
  return (
    <div style={{ minWidth: 180, padding: 16, background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10 }}>
      <div style={{ fontSize: 20, marginBottom: 4 }}>{icon}</div>
      <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>{title}</div>
      <div style={{ fontSize: 18, fontWeight: 700, color: 'var(--accent-orange)' }}>{value}</div>
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
