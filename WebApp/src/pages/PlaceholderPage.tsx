import { useLocation } from 'react-router-dom'

const titles: Record<string, string> = {
  '/itemqueue': '道具給予',
  '/shopstats': '商城分析',
  '/analytics/player': '玩家活躍分析',
  '/analytics/recharge': '儲值趨勢分析',
  '/tradeaudit': '交易稽核',
  '/gmperm': 'GM 權限管理',
  '/backup': '備份還原',
}

export default function PlaceholderPage() {
  const path = useLocation().pathname
  const title = titles[path] || '此功能'

  return (
    <div style={{ padding: 28, textAlign: 'center', maxWidth: 480, margin: '60px auto 0' }}>
      <div style={{ fontSize: 48, marginBottom: 16 }}>🔧</div>
      <h1 style={{ fontSize: 20, fontWeight: 700, marginBottom: 12 }}>{title}</h1>
      <p style={{ color: 'var(--text-muted)', fontSize: 14, lineHeight: 1.6 }}>
        網頁版此功能與 EXE 版同步開發中，請先使用桌面版執行相關操作。
      </p>
    </div>
  )
}
