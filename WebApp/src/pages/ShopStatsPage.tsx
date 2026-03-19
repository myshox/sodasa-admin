import { useState, useEffect } from 'react'
import api from '../api'
import { S } from '../strings'
import useIsMobile from '../hooks/useIsMobile'

const SHOPS = [
  { id: 'vipshop', label: '金幣商店', icon: '💰', unit: '金幣' },
  { id: 'fameshop', label: '聲望商店', icon: '🏆', unit: '聲望' },
  { id: 'csshopnum', label: '石壁商店', icon: '🪨', unit: '石壁' },
  { id: 'csxsshopnum', label: '戰點商店', icon: '⚔', unit: '戰點' },
]

export default function ShopStatsPage() {
  const isMobile = useIsMobile()
  const [tab, setTab] = useState('vipshop')
  const [data, setData] = useState<{ items: any[]; spenders: any[] } | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    api.get(`/shop/${tab}`).then(r => { setData(r.data); setLoading(false) }).catch(() => setLoading(false))
  }, [tab])

  return (
    <div style={{ padding: isMobile ? 16 : 28 }}>
      <h1 style={{ fontSize: isMobile ? 18 : 22, fontWeight: 700, marginBottom: 20 }}>🏪 {S.navShop}</h1>
      <div style={{ display: 'flex', gap: 8, marginBottom: 20, flexWrap: 'wrap' }}>
        {SHOPS.map(s => (
          <button key={s.id} onClick={() => setTab(s.id)}
            style={{
              padding: '8px 16px', borderRadius: 8,
              background: tab === s.id ? 'var(--accent-orange)' : 'var(--bg-card)',
              color: tab === s.id ? '#fff' : 'var(--text-secondary)',
              border: '1px solid var(--border)',
            }}>{s.icon} {s.label}</button>
        ))}
      </div>
      {loading ? <p style={{ color: 'var(--text-muted)' }}>載入中…</p> : data && (
        <>
          <h2 style={{ fontSize: 16, marginBottom: 12 }}>熱賣道具 Top 20</h2>
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10, marginBottom: 24 }}>
            <div className="table-wrap" style={{ overflowX: 'auto', WebkitOverflowScrolling: 'touch' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '60px 80px 1fr 100px 100px 120px 140px', padding: '10px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, minWidth: 640 }}>
              <span>排名</span><span>道具ID</span><span>名稱</span><span>數量</span><span>筆數</span><span>消耗</span><span>最後購買</span>
            </div>
            {data.items.length === 0 ? <p style={{ padding: 24, color: 'var(--text-muted)', textAlign: 'center' }}>尚無購買記錄</p> : data.items.map((row: any) => (
              <div key={row.rank} style={{ display: 'grid', gridTemplateColumns: '60px 80px 1fr 100px 100px 120px 140px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, minWidth: 640 }}>
                <span>{row.rank}</span><span>{row.itemId}</span><span>{row.itemName}</span><span>{row.totalQty?.toLocaleString()}</span><span>{row.orderCount}</span><span>{row.totalCost != null ? row.totalCost.toLocaleString() : '—'}</span><span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{row.lastTime || '—'}</span>
              </div>
            ))}
            </div>
          </div>
          <h2 style={{ fontSize: 16, marginBottom: 12 }}>消費排行 Top 20</h2>
          <div style={{ background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 10 }}>
            <div className="table-wrap" style={{ overflowX: 'auto', WebkitOverflowScrolling: 'touch' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '60px 1fr 1fr 100px 120px', padding: '10px 16px', background: 'var(--bg-sidebar)', fontSize: 12, color: 'var(--text-muted)', fontWeight: 600, minWidth: 420 }}>
              <span>排名</span><span>帳號</span><span>角色</span><span>數量</span><span>消耗</span>
            </div>
            {data.spenders.length === 0 ? <p style={{ padding: 24, color: 'var(--text-muted)', textAlign: 'center' }}>尚無記錄</p> : data.spenders.map((row: any) => (
              <div key={row.rank} style={{ display: 'grid', gridTemplateColumns: '60px 1fr 1fr 100px 120px', padding: '10px 16px', borderBottom: '1px solid var(--border)', fontSize: 13, minWidth: 420 }}>
                <span>{row.rank}</span><span>{row.cdkey}</span><span>{row.name}</span><span>{row.totalQty?.toLocaleString()}</span><span>{row.totalCost?.toLocaleString()}</span>
              </div>
            ))}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
