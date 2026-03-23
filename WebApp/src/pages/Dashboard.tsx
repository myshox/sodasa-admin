import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import api from '../api'
import type { DashboardStats } from '../api'
function UiStatCard({
  icon,
  label,
  value,
  sub,
  accent,
}: {
  icon: string
  label: string
  value: string | number
  sub?: string
  accent: string
}) {
  return (
    <div
      className="ui-stat-card"
      style={{ ['--stat-accent' as string]: accent } as React.CSSProperties}
    >
      <div className="ui-stat-icon">{icon}</div>
      <div className="ui-stat-value">{value}</div>
      <div className="ui-stat-label">{label}</div>
      {sub && <div className="ui-stat-sub">{sub}</div>}
    </div>
  )
}

interface QuickLink {
  icon: string
  label: string
  desc: string
  path: string
  color: string
}

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [err, setErr] = useState(false)
  const nav = useNavigate()

  useEffect(() => {
    api.get('/stats').then(r => setStats(r.data)).catch(() => setErr(true))
  }, [])

  const fmt = (n?: number) => n?.toLocaleString() ?? '—'

  const quickLinks: QuickLink[] = [
    { icon: '💳', label: '充值管理', desc: '手動補單、累積儲值進度', path: '/recharge', color: '#4ade80' },
    { icon: '👥', label: '玩家管理', desc: '搜尋玩家、改金幣水晶', path: '/players', color: '#60a5fa' },
    { icon: '📦', label: '批量工具', desc: '批量發道具、批量加金幣', path: '/batchops', color: '#fb923c' },
    { icon: '🏆', label: '消費里程碑', desc: '查詢並發放消費獎勵', path: '/cost-milestone', color: '#fbbf24' },
    { icon: '🖥', label: '伺服器狀態', desc: '各分流在線人數', path: '/server-status', color: '#a78bfa' },
    { icon: '📋', label: '全服記錄', desc: '充值/交易/金幣/郵件', path: '/records', color: '#38bdf8' },
    { icon: '⚡', label: '加速外掛偵測', desc: '分析異常並批量封號', path: '/speedban', color: '#f87171' },
    { icon: '📈', label: '數據分析', desc: '儀表板、儲值趨勢', path: '/analytics', color: '#34d399' },
  ]

  return (
    <div className="page-shell">
      <header className="dashboard-hero">
        <div className="dashboard-hero-inner">
          <h1 className="dashboard-title">GM 管理後台</h1>
          <p className="dashboard-sub">蘇打石器 · 私服管理 · 快捷入口與即時概況</p>
          {stats != null && (
            <div className="dashboard-badge">
              <span>📊</span>
              <span>資料已更新</span>
            </div>
          )}
        </div>
      </header>

      {err && (
        <div className="dashboard-alert">
          <strong>無法連線後端</strong>
          ，所有功能都無法使用。請確認 API 已啟動（Port 5050）且資料庫連線正確。
        </div>
      )}

      <section aria-label="即時統計">
        <div className="dashboard-section-label">即時概況</div>
        <div className="dashboard-stats">
          <UiStatCard icon="👥" label="總玩家數" value={fmt(stats?.totalPlayers)} accent="var(--accent-blue)" />
          <UiStatCard icon="🟢" label="目前在線" value={fmt(stats?.onlinePlayers)} accent="var(--accent-green)" />
          <UiStatCard icon="🚫" label="已封號" value={fmt(stats?.bannedPlayers)} accent="var(--accent-red)" />
          <UiStatCard icon="🍳" label="今日新增" value={fmt(stats?.newToday)} accent="var(--accent-orange)" />
          <UiStatCard icon="💰" label="全服金幣" value={fmt(stats?.totalGold)} accent="#d97706" />
          <UiStatCard icon="💎" label="全服水晶" value={fmt(stats?.totalCrystal)} accent="#2563eb" />
        </div>
      </section>

      <section aria-label="常用功能">
        <div className="dashboard-section-label">常用功能</div>
        <div className="dashboard-quick">
          {quickLinks.map(q => (
            <button
              key={q.path}
              type="button"
              className="ui-quick-card"
              onClick={() => nav(q.path)}
            >
              <div
                className="q-ico"
                style={{
                  background: `${q.color}26`,
                  border: `1px solid ${q.color}35`,
                }}
              >
                {q.icon}
              </div>
              <div className="q-title">{q.label}</div>
              <div className="q-desc">{q.desc}</div>
            </button>
          ))}
        </div>
      </section>

      <section className="dashboard-hint" aria-label="操作說明">
        <h3>
          <span>💡</span> 常見操作說明
        </h3>
        <div className="dashboard-hint-grid">
          <div>
            💰 <strong>發金幣給玩家</strong>：玩家管理 → 點選玩家 → 右側設定金幣
          </div>
          <div>
            📦 <strong>發道具給玩家</strong>：批量工具 → 「道具給予」Tab
          </div>
          <div>
            💳 <strong>補充值紀錄</strong>：充值管理 → 搜尋玩家 → 選套餐 → 確認
          </div>
          <div>
            🏆 <strong>發消費獎勵</strong>：消費里程碑 → 搜尋玩家 → 發放獎勵
          </div>
          <div>
            🔒 <strong>封禁玩家</strong>：玩家管理 → 找到玩家 → 封禁按鈕
          </div>
          <div>
            📢 <strong>全服發道具</strong>：批量工具 → 「批量發送」Tab
          </div>
        </div>
      </section>
    </div>
  )
}
