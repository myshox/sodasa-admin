import { useState, useEffect } from 'react'
import api from '../api'
import { S } from '../strings'

export default function TradeAuditPage() {
  const [summary, setSummary] = useState<{ totalTrades: number; uniquePairs: number; suspiciousPairs: number; sameIpPairs: number } | null>(null)
  const [frequent, setFrequent] = useState<any[]>([])
  const [sameIp, setSameIp] = useState<any[]>([])
  const [gold, setGold] = useState<any[]>([])
  const [traders, setTraders] = useState<any[]>([])
  const [tab, setTab] = useState<'frequent' | 'sameip' | 'gold' | 'traders'>('frequent')
  const [loading, setLoading] = useState(true)

  const load = () => {
    setLoading(true)
    Promise.all([
      api.get('/tradeaudit/summary').then(r => setSummary(r.data)),
      api.get('/tradeaudit/frequent').then(r => setFrequent(r.data || [])),
      api.get('/tradeaudit/sameip').then(r => setSameIp(r.data || [])),
      api.get('/tradeaudit/gold').then(r => setGold(r.data || [])),
      api.get('/tradeaudit/traders').then(r => setTraders(r.data || [])),
    ]).finally(() => setLoading(false))
  }

  useEffect(() => { load() }, [])

  if (loading) return <div style={{ padding: 28 }}><p style={{ color: 'var(--text-muted)' }}>載入中…</p></div>

  return (
    <div className="gm-page-stack">
      <h1 style={{ fontSize: 22, fontWeight: 700, marginBottom: 8 }}>🔍 {S.navTradeAudit}</h1>
      <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 20 }}>⚠ 本模組僅供參考，建議結合人工判斷再進行處置</p>
      <button onClick={load} style={{ marginBottom: 20, padding: '8px 16px', background: 'var(--accent-blue)', color: '#fff', borderRadius: 8 }}>↺ 重新整理</button>

      {summary && (
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', marginBottom: 24 }}>
          <Card icon="📊" title="總交易筆數" value={summary.totalTrades?.toLocaleString() ?? '0'} />
          <Card icon="👥" title="不重複交易配對" value={summary.uniquePairs?.toLocaleString() ?? '0'} />
          <Card icon="🚨" title="高頻配對（≥10次）" value={summary.suspiciousPairs?.toLocaleString() ?? '0'} />
          <Card icon="🔗" title="同IP帳號交易配對" value={summary.sameIpPairs?.toLocaleString() ?? '0'} />
        </div>
      )}

      <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
        {(['frequent', 'sameip', 'gold', 'traders'] as const).map(t => (
          <button key={t} onClick={() => setTab(t)}
            style={{
              padding: '8px 16px', borderRadius: 8,
              background: tab === t ? 'var(--accent-red)' : 'var(--bg-card)',
              color: tab === t ? '#fff' : 'var(--text-secondary)',
              border: '1px solid var(--border)',
            }}>
            {t === 'frequent' && '🚨 高頻配對'}
            {t === 'sameip' && '🔗 同IP交易'}
            {t === 'gold' && '💰 金幣異動'}
            {t === 'traders' && '📊 交易量排行'}
          </button>
        ))}
      </div>

      {tab === 'frequent' && (
        <Table headers={['來源帳號', '來源名', '目標帳號', '目標名', '次數', '最後時間']}
          rows={frequent.map((r: any) => [r.fromAccount, r.fromName, r.toAccount, r.toName, r.count, r.lastTime])} />
      )}
      {tab === 'sameip' && (
        <Table headers={['來源帳號', '目標帳號', '次數', '共用IP']}
          rows={sameIp.map((r: any) => [r.fromAccount, r.toAccount, r.count, r.sharedIp])} />
      )}
      {tab === 'gold' && (
        <Table headers={['帳號', '角色', '總收入', '總支出', '筆數']}
          rows={gold.map((r: any) => [r.account, r.name, r.totalGain?.toLocaleString(), r.totalLoss?.toLocaleString(), r.entries])} />
      )}
      {tab === 'traders' && (
        <Table headers={['帳號', '角色', '交易次數', '最後時間']}
          rows={traders.map((r: any) => [r.account, r.name, r.tradeCount, r.lastTime])} />
      )}
    </div>
  )
}

function Card({ icon, title, value }: { icon: string; title: string; value: string }) {
  return (
    <div style={{ minWidth: 180, padding: 16, background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10 }}>
      <div style={{ fontSize: 20, marginBottom: 4 }}>{icon}</div>
      <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 4 }}>{title}</div>
      <div style={{ fontSize: 18, fontWeight: 700 }}>{value}</div>
    </div>
  )
}
function Table({ headers, rows }: { headers: string[]; rows: (string | number)[][] }) {
  return (
    <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, overflow: 'hidden' }}>
      <div style={{ display: 'grid', gridTemplateColumns: `repeat(${headers.length}, 1fr)`, padding: '10px 16px', background: 'var(--bg-sidebar)', fontSize: 12, fontWeight: 600, color: 'var(--text-muted)' }}>
        {headers.map(h => <span key={h}>{h}</span>)}
      </div>
      {rows.length === 0 ? <p style={{ padding: 24, color: 'var(--text-muted)', textAlign: 'center' }}>尚無資料</p> : rows.map((row, i) => (
        <div key={i} style={{ display: 'grid', gridTemplateColumns: `repeat(${headers.length}, 1fr)`, padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13 }}>
          {row.map((cell, j) => <span key={j}>{cell ?? '—'}</span>)}
        </div>
      ))}
    </div>
  )
}
