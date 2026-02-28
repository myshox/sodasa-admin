import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { S } from '../strings'

type NavGroup = { label: string; items: { to: string; icon: string; label: string }[] }

const navGroups: NavGroup[] = [
  {
    label: '玩家管理',
    items: [
      { to: '/players',  icon: '👥', label: S.navPlayers },
      { to: '/master',   icon: '👑', label: S.navMaster },
      { to: '/vip',      icon: '💎', label: S.navVip },
      { to: '/online',   icon: '🟢', label: S.navOnline },
      { to: '/ban',      icon: '🔒', label: S.navBan },
    ]
  },
  {
    label: '紀錄查詢',
    items: [
      { to: '/history',    icon: '🔍', label: '玩家活動歷程' },
      { to: '/itemsearch', icon: '🎁', label: '物品查詢' },
      { to: '/streetshop', icon: '🏪', label: '攤位 & 商城查詢' },
      { to: '/recharge',   icon: '💰', label: S.navRecharge },
      { to: '/tradelog',   icon: '📊', label: S.navTradeLog },
      { to: '/goldlog',    icon: '💎', label: S.navGoldLog },
      { to: '/mail',       icon: '📧', label: S.navMail },
    ]
  },
  {
    label: 'GM 工具',
    items: [
      { to: '/batchops', icon: '⚙️', label: '批量操作' },
      { to: '/petcmd',   icon: '🐾', label: S.navPetCmd },
      { to: '/recycle',  icon: '🗑', label: S.navRecycle },
      { to: '/sql',      icon: '💻', label: S.navSql },
      { to: '/gmadmin',  icon: '🔑', label: S.navGmAdmin },
    ]
  },
  {
    label: '數據分析',
    items: [
      { to: '/',                   icon: '📈', label: S.navDashboard },
      { to: '/shopstats',          icon: '🏪', label: S.navShop },
      { to: '/analytics/player',   icon: '📊', label: S.navPlayerAna },
      { to: '/analytics/recharge', icon: '💰', label: S.navRechargeAna },
      { to: '/tradeaudit',         icon: '🔍', label: S.navTradeAudit },
    ]
  },
  {
    label: '系統',
    items: [
      { to: '/gmlog',   icon: '📋', label: S.navGmLog },
      { to: '/gmperm',  icon: '🛡', label: S.navGmPerm },
      { to: '/backup',  icon: '💾', label: S.navBackup },
    ]
  },
]

export default function Layout() {
  const nav = useNavigate()
  const logout = () => { localStorage.clear(); nav('/login') }
  const user = localStorage.getItem('gm_user') ?? 'GM'

  return (
    <div style={{ display: 'flex', height: '100vh', overflow: 'hidden' }}>
      <aside style={{
        width: 210, background: 'var(--bg-sidebar)',
        borderRight: '1px solid var(--border)',
        display: 'flex', flexDirection: 'column', flexShrink: 0, overflowY: 'auto'
      }}>
        <div style={{ padding: '16px 14px 12px', borderBottom: '1px solid var(--border)', flexShrink: 0 }}>
          <div style={{ fontSize: 20, marginBottom: 2 }}>🍅</div>
          <div style={{ fontWeight: 700, fontSize: 14, color: 'var(--text-primary)' }}>{S.appName}</div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 1 }}>{S.appSub}</div>
        </div>

        <nav style={{ flex: 1, padding: '8px 6px' }}>
          {navGroups.map(g => (
            <div key={g.label} style={{ marginBottom: 8 }}>
              <div style={{ fontSize: 10, fontWeight: 700, color: 'var(--text-muted)', padding: '6px 8px 2px', letterSpacing: 1, textTransform: 'uppercase' }}>
                {g.label}
              </div>
              {g.items.map(n => (
                <NavLink key={n.to} to={n.to} end={n.to === '/'} style={({ isActive }) => ({
                  display: 'flex', alignItems: 'center', gap: 8,
                  padding: '7px 10px', borderRadius: 6, marginBottom: 1, fontSize: 13,
                  background: isActive ? 'rgba(74,158,255,.18)' : 'transparent',
                  color: isActive ? 'var(--accent-blue)' : 'var(--text-secondary)',
                  fontWeight: isActive ? 600 : 400,
                  textDecoration: 'none', transition: 'background .12s, color .12s'
                })}>
                  <span style={{ width: 18, textAlign: 'center', fontSize: 15 }}>{n.icon}</span>
                  <span>{n.label}</span>
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div style={{
          padding: '10px 14px', borderTop: '1px solid var(--border)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0
        }}>
          <span style={{ color: 'var(--text-secondary)', fontSize: 12 }}>👤 {user}</span>
          <button onClick={logout} style={{
            background: 'transparent', color: 'var(--text-muted)',
            fontSize: 11, padding: '3px 8px',
            border: '1px solid var(--border)', borderRadius: 5
          }}>{S.navLogout}</button>
        </div>
      </aside>

      <main style={{ flex: 1, overflow: 'auto', background: 'var(--bg-page)' }}>
        <Outlet />
      </main>
    </div>
  )
}
