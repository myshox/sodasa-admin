import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import type { DashboardStats } from '../api'

function useIsMobile() {
  const [m, setM] = useState(window.innerWidth < 768)
  useEffect(() => {
    const h = () => setM(window.innerWidth < 768)
    window.addEventListener('resize', h)
    return () => window.removeEventListener('resize', h)
  }, [])
  return m
}

const StatCard = ({ icon, label, value, color, sub }: {
  icon: string; label: string; value: string | number; color: string; sub?: string
}) => (
  <div style={{
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 12, padding: '18px 20px', flex: 1, minWidth: 140
  }}>
    <div style={{ fontSize: 24, marginBottom: 6 }}>{icon}</div>
    <div style={{ fontSize: 24, fontWeight: 800, color, letterSpacing: -0.5 }}>{value}</div>
    <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4 }}>{label}</div>
    {sub && <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2, opacity: 0.7 }}>{sub}</div>}
  </div>
)

interface QuickLink { icon: string; label: string; desc: string; path: string; color: string }

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [err, setErr] = useState(false)
  const isMobile = useIsMobile()
  const nav = useNavigate()

  useEffect(() => {
    api.get('/stats').then(r => setStats(r.data)).catch(() => setErr(true))
  }, [])

  const fmt = (n?: number) => n?.toLocaleString() ?? '—'

  const quickLinks: QuickLink[] = [
    { icon: '💳', label: '充值管理',   desc: '手動補單、累積儲值進度', path: '/recharge',      color: '#4ade80' },
    { icon: '👥', label: '玩家管理',   desc: '搜尋玩家、改金幣水晶',   path: '/players',       color: '#60a5fa' },
    { icon: '📦', label: '批量工具',   desc: '批量發道具、批量加金幣', path: '/batchops',      color: '#fb923c' },
    { icon: '🏆', label: '消費里程碑', desc: '查詢並發放消費獎勵',     path: '/cost-milestone', color: '#fbbf24' },
    { icon: '🖥', label: '伺服器狀態', desc: '各分流在線人數',         path: '/server-status',  color: '#a78bfa' },
    { icon: '📋', label: '全服記錄',   desc: '充值/交易/金幣/郵件',    path: '/records',        color: '#38bdf8' },
    { icon: '⚡', label: '加速外掛偵測', desc: '分析異常並批量封號',   path: '/speedban',       color: '#f87171' },
    { icon: '📈', label: '數據分析',   desc: '儀表板、儲值趨勢',       path: '/analytics',      color: '#34d399' },
  ]

  return (
    <div style={{ padding: isMobile ? '16px 14px' : '28px 32px', maxWidth: 1200 }}>
      {/* 標題 */}
      <div style={{ marginBottom: isMobile ? 16 : 24 }}>
        <h1 style={{ fontSize: isMobile ? 20 : 26, fontWeight: 800, margin: 0,
          background: 'linear-gradient(135deg,#60a5fa,#4ade80)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
          🏠 GM 管理後台
        </h1>
        <p style={{ margin: '4px 0 0', fontSize: 12, color: 'var(--text-muted)' }}>蘇打石器 · 私服管理系統</p>
      </div>

      {/* 後端錯誤提示 */}
      {err && (
        <div style={{ background: 'rgba(245,101,101,.1)', border: '1px solid var(--accent-red)',
          borderRadius: 10, padding: '14px 18px', marginBottom: 20, color: 'var(--accent-red)', fontSize: 13, lineHeight: 1.6 }}>
          <strong>⚠️ 無法連線後端</strong>，所有功能都無法使用。<br />
          請確認 API 伺服器已啟動（Port 5050）並正確設定資料庫連線。
        </div>
      )}

      {/* 統計卡片 */}
      <div style={{ display: 'grid', gridTemplateColumns: isMobile ? 'repeat(2,1fr)' : 'repeat(6,1fr)', gap: isMobile ? 10 : 14, marginBottom: isMobile ? 20 : 28 }}>
        <StatCard icon="👥" label="總玩家數"   value={fmt(stats?.totalPlayers)}  color="var(--accent-blue)" />
        <StatCard icon="🟢" label="目前在線"   value={fmt(stats?.onlinePlayers)} color="var(--accent-green)" />
        <StatCard icon="🚫" label="已封號"     value={fmt(stats?.bannedPlayers)} color="var(--accent-red)" />
        <StatCard icon="🍳" label="今日新增"   value={fmt(stats?.newToday)}      color="var(--accent-orange)" />
        <StatCard icon="💰" label="全服金幣"   value={fmt(stats?.totalGold)}     color="#fbbf24" />
        <StatCard icon="💎" label="全服水晶"   value={fmt(stats?.totalCrystal)}  color="#60a5fa" />
      </div>

      {/* 快捷入口 */}
      <div style={{ marginBottom: isMobile ? 20 : 28 }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--text-muted)', marginBottom: 12, letterSpacing: 0.5, textTransform: 'uppercase' }}>
          常用功能
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: isMobile ? 'repeat(2,1fr)' : 'repeat(4,1fr)', gap: isMobile ? 10 : 14 }}>
          {quickLinks.map(q => (
            <button key={q.path} onClick={() => nav(q.path)}
              style={{
                background: 'var(--bg-card)', border: `1px solid var(--border)`,
                borderRadius: 12, padding: isMobile ? '14px 12px' : '16px 18px',
                textAlign: 'left', cursor: 'pointer', transition: 'all .15s',
                WebkitTapHighlightColor: 'transparent',
              }}
              onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.borderColor = q.color; (e.currentTarget as HTMLButtonElement).style.background = `${q.color}12` }}
              onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.borderColor = 'var(--border)'; (e.currentTarget as HTMLButtonElement).style.background = 'var(--bg-card)' }}
            >
              <div style={{ fontSize: isMobile ? 22 : 26, marginBottom: 8 }}>{q.icon}</div>
              <div style={{ fontSize: isMobile ? 13 : 14, fontWeight: 700, color: 'var(--text-primary)', marginBottom: 4 }}>{q.label}</div>
              <div style={{ fontSize: 11, color: 'var(--text-muted)', lineHeight: 1.4 }}>{q.desc}</div>
            </button>
          ))}
        </div>
      </div>

      {/* 操作提示 */}
      <div style={{ background: 'rgba(74,158,255,.08)', border: '1px solid rgba(74,158,255,.25)', borderRadius: 10, padding: '14px 18px' }}>
        <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--accent-blue)', marginBottom: 8 }}>💡 常見操作說明</div>
        <div style={{ display: 'grid', gridTemplateColumns: isMobile ? '1fr' : '1fr 1fr', gap: '6px 24px', fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.7 }}>
          <div>💰 <strong style={{ color: 'var(--text-secondary)' }}>發金幣給玩家</strong>：玩家管理 → 點選玩家 → 右側設定金幣</div>
          <div>📦 <strong style={{ color: 'var(--text-secondary)' }}>發道具給玩家</strong>：批量工具 → 「道具給予」Tab</div>
          <div>💳 <strong style={{ color: 'var(--text-secondary)' }}>補充值紀錄</strong>：充值管理 → 搜尋玩家 → 選套餐 → 確認</div>
          <div>🏆 <strong style={{ color: 'var(--text-secondary)' }}>發消費獎勵</strong>：消費里程碑 → 搜尋玩家 → 發放獎勵</div>
          <div>🔒 <strong style={{ color: 'var(--text-secondary)' }}>封禁玩家</strong>：玩家管理 → 找到玩家 → 封禁按鈕</div>
          <div>📢 <strong style={{ color: 'var(--text-secondary)' }}>全服發道具</strong>：批量工具 → 「批量發送」Tab</div>
        </div>
      </div>
    </div>
  )
}
