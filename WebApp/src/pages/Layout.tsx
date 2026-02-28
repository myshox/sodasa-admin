import { useState, useEffect, useRef } from 'react'
import { Outlet, NavLink, useNavigate, useLocation } from 'react-router-dom'
import { S } from '../strings'

type NavItem = { to: string; icon: string; label: string }
type NavGroup = { label: string; items: NavItem[] }

const navGroups: NavGroup[] = [
  {
    label: '玩家管理',
    items: [
      { to: '/players', icon: '👥', label: '玩家管理' },
      { to: '/master',  icon: '👑', label: S.navMaster },
      { to: '/vip',     icon: '💎', label: S.navVip },
    ]
  },
  {
    label: '紀錄查詢',
    items: [
      { to: '/history', icon: '🔍', label: '玩家活動歷程' },
      { to: '/market',  icon: '🏪', label: '市場查詢' },
      { to: '/records', icon: '📋', label: '全服紀錄' },
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
      { to: '/analytics', icon: '📈', label: '數據分析' },
    ]
  },
  {
    label: '系統',
    items: [
      { to: '/system', icon: '⚙️', label: '系統管理' },
    ]
  },
]

const RECHARGE_NAV = { to: '/recharge', icon: '💳', label: '充值管理' }

export default function Layout() {
  const nav = useNavigate()
  const location = useLocation()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [isMobile, setIsMobile] = useState(window.innerWidth < 768)
  const drawerRef = useRef<HTMLDivElement>(null)

  const logout = () => { localStorage.clear(); nav('/login') }
  const user = localStorage.getItem('gm_user') ?? 'GM'

  // 偵測螢幕寬度
  useEffect(() => {
    const onResize = () => setIsMobile(window.innerWidth < 768)
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  // 切換頁面時關閉 drawer
  useEffect(() => { setDrawerOpen(false) }, [location.pathname])

  // 點擊 overlay 關閉
  useEffect(() => {
    if (!drawerOpen) return
    const handler = (e: MouseEvent) => {
      if (drawerRef.current && !drawerRef.current.contains(e.target as Node)) {
        setDrawerOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [drawerOpen])

  const navLinkStyle = (isActive: boolean): React.CSSProperties => ({
    display: 'flex', alignItems: 'center', gap: 10,
    padding: isMobile ? '12px 14px' : '8px 10px',
    borderRadius: 8, marginBottom: 2, fontSize: 14,
    background: isActive ? 'rgba(74,158,255,.18)' : 'transparent',
    color: isActive ? 'var(--accent-blue)' : 'var(--text-secondary)',
    fontWeight: isActive ? 700 : 400,
    textDecoration: 'none', transition: 'background .12s, color .12s',
  })

  const SidebarContent = () => (
    <>
      {/* Logo */}
      <div style={{ padding: '16px 16px 12px', borderBottom: '1px solid var(--border)', flexShrink: 0 }}>
        <div style={{ fontSize: 22, marginBottom: 2 }}>🍅</div>
        <div style={{ fontWeight: 800, fontSize: 15, color: 'var(--text-primary)' }}>{S.appName}</div>
        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 1 }}>{S.appSub}</div>
      </div>

      {/* Nav */}
      <nav style={{ flex: 1, padding: '10px 8px', overflowY: 'auto' }}>
        {/* 充值 — 置頂醒目 */}
        <div style={{ margin: '4px 0 12px' }}>
          <NavLink to={RECHARGE_NAV.to} style={({ isActive }) => ({
            ...navLinkStyle(isActive),
            background: isActive
              ? 'linear-gradient(90deg,rgba(74,222,128,.35),rgba(74,222,128,.15))'
              : 'linear-gradient(90deg,rgba(74,222,128,.18),rgba(74,222,128,.06))',
            color: isActive ? '#4ade80' : '#86efac',
            border: `1px solid ${isActive ? 'rgba(74,222,128,.5)' : 'rgba(74,222,128,.25)'}`,
            fontWeight: 700,
            boxShadow: isActive ? '0 0 10px rgba(74,222,128,.15)' : 'none',
          })}>
            <span style={{ fontSize: 18 }}>{RECHARGE_NAV.icon}</span>
            <span>{RECHARGE_NAV.label}</span>
          </NavLink>
        </div>

        {navGroups.map(g => (
          <div key={g.label} style={{ marginBottom: 10 }}>
            <div style={{ fontSize: 10, fontWeight: 700, color: 'var(--text-muted)', padding: '4px 10px 4px', letterSpacing: 1, textTransform: 'uppercase' }}>
              {g.label}
            </div>
            {g.items.map(n => (
              <NavLink key={n.to} to={n.to} end={n.to === '/'} style={({ isActive }) => navLinkStyle(isActive)}>
                <span style={{ width: 20, textAlign: 'center', fontSize: 16, flexShrink: 0 }}>{n.icon}</span>
                <span>{n.label}</span>
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      {/* 使用者 */}
      <div style={{ padding: '12px 16px', borderTop: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0 }}>
        <span style={{ color: 'var(--text-secondary)', fontSize: 13 }}>👤 {user}</span>
        <button onClick={logout} style={{ background: 'rgba(245,101,101,.15)', color: 'var(--accent-red)', fontSize: 12, padding: '5px 12px', border: '1px solid rgba(245,101,101,.3)', borderRadius: 6 }}>
          {S.navLogout}
        </button>
      </div>
    </>
  )

  if (isMobile) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', height: '100dvh', overflow: 'hidden' }}>
        {/* 頂部 Header */}
        <header style={{ height: 56, background: 'var(--bg-sidebar)', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', padding: '0 16px', gap: 12, flexShrink: 0, zIndex: 100 }}>
          <button onClick={() => setDrawerOpen(true)}
            style={{ width: 40, height: 40, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 8, fontSize: 18, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, padding: 0 }}>
            ☰
          </button>
          <span style={{ fontWeight: 800, fontSize: 16, color: 'var(--text-primary)', flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>🍅 {S.appName}</span>
          {/* 充值快捷按鈕 */}
          <NavLink to="/recharge"
            style={({ isActive }) => ({
              display: 'flex', alignItems: 'center', gap: 5, padding: '6px 12px', borderRadius: 8, textDecoration: 'none', fontSize: 13, fontWeight: 700, flexShrink: 0,
              background: isActive ? 'rgba(74,222,128,.2)' : 'rgba(74,222,128,.12)',
              border: '1px solid rgba(74,222,128,.35)', color: '#4ade80',
            })}>
            💳 充值
          </NavLink>
        </header>

        {/* Drawer 背景遮罩 */}
        {drawerOpen && (
          <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,.55)', zIndex: 200, backdropFilter: 'blur(2px)' }} />
        )}

        {/* 側拉抽屜 */}
        <div ref={drawerRef} style={{
          position: 'fixed', top: 0, left: 0, bottom: 0, width: 280,
          background: 'var(--bg-sidebar)', borderRight: '1px solid var(--border)',
          display: 'flex', flexDirection: 'column',
          zIndex: 300,
          transform: drawerOpen ? 'translateX(0)' : 'translateX(-100%)',
          transition: 'transform .25s cubic-bezier(.4,0,.2,1)',
          boxShadow: drawerOpen ? '4px 0 24px rgba(0,0,0,.5)' : 'none',
        }}>
          {/* 關閉按鈕 */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', borderBottom: '1px solid var(--border)' }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: 'var(--text-primary)' }}>🍅 {S.appName}</div>
            <button onClick={() => setDrawerOpen(false)}
              style={{ width: 36, height: 36, background: 'var(--bg-input)', border: '1px solid var(--border)', borderRadius: 8, fontSize: 18, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 0 }}>
              ✕
            </button>
          </div>

          <nav style={{ flex: 1, padding: '10px 8px', overflowY: 'auto' }}>
            {/* 充值 */}
            <div style={{ margin: '4px 0 12px' }}>
              <NavLink to={RECHARGE_NAV.to} style={({ isActive }) => ({
                display: 'flex', alignItems: 'center', gap: 10,
                padding: '13px 14px', borderRadius: 10, fontSize: 15, fontWeight: 700, textDecoration: 'none',
                background: isActive ? 'linear-gradient(90deg,rgba(74,222,128,.35),rgba(74,222,128,.15))' : 'linear-gradient(90deg,rgba(74,222,128,.18),rgba(74,222,128,.06))',
                color: isActive ? '#4ade80' : '#86efac',
                border: `1px solid ${isActive ? 'rgba(74,222,128,.5)' : 'rgba(74,222,128,.25)'}`,
              })}>
                <span style={{ fontSize: 20 }}>{RECHARGE_NAV.icon}</span>
                {RECHARGE_NAV.label}
              </NavLink>
            </div>

            {navGroups.map(g => (
              <div key={g.label} style={{ marginBottom: 12 }}>
                <div style={{ fontSize: 10, fontWeight: 700, color: 'var(--text-muted)', padding: '4px 12px 6px', letterSpacing: 1, textTransform: 'uppercase' }}>{g.label}</div>
                {g.items.map(n => (
                  <NavLink key={n.to} to={n.to} end={n.to === '/'} style={({ isActive }) => ({
                    display: 'flex', alignItems: 'center', gap: 12,
                    padding: '13px 14px', borderRadius: 8, marginBottom: 2, fontSize: 15,
                    background: isActive ? 'rgba(74,158,255,.18)' : 'transparent',
                    color: isActive ? 'var(--accent-blue)' : 'var(--text-secondary)',
                    fontWeight: isActive ? 700 : 400, textDecoration: 'none',
                  })}>
                    <span style={{ fontSize: 18 }}>{n.icon}</span>
                    {n.label}
                  </NavLink>
                ))}
              </div>
            ))}
          </nav>

          <div style={{ padding: '14px 16px', borderTop: '1px solid var(--border)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ color: 'var(--text-secondary)', fontSize: 14 }}>👤 {user}</span>
            <button onClick={logout} style={{ background: 'rgba(245,101,101,.15)', color: 'var(--accent-red)', fontSize: 13, padding: '8px 14px', border: '1px solid rgba(245,101,101,.3)', borderRadius: 8 }}>
              {S.navLogout}
            </button>
          </div>
        </div>

        {/* 主內容 */}
        <main style={{ flex: 1, overflow: 'auto', background: 'var(--bg-page)' }}>
          <Outlet />
        </main>
      </div>
    )
  }

  // ── 桌機版 ──
  return (
    <div style={{ display: 'flex', height: '100vh', overflow: 'hidden' }}>
      <aside style={{ width: 220, background: 'var(--bg-sidebar)', borderRight: '1px solid var(--border)', display: 'flex', flexDirection: 'column', flexShrink: 0, overflowY: 'auto' }}>
        <SidebarContent />
      </aside>
      <main style={{ flex: 1, overflow: 'auto', background: 'var(--bg-page)' }}>
        <Outlet />
      </main>
    </div>
  )
}
