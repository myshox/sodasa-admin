import { useState, useEffect, useRef } from 'react'
import { Outlet, NavLink, useNavigate, useLocation } from 'react-router-dom'
import { S } from '../strings'
import { MOBILE_BREAKPOINT, getRouteTitle } from '../constants/layout'

type NavItem = { to: string; icon: string; label: string; title?: string }
type NavGroup = { label: string; items: NavItem[] }

const navGroups: NavGroup[] = [
  {
    label: '玩家管理',
    items: [
      { to: '/players', icon: '👥', label: '玩家管理',       title: '搜尋玩家・查看詳情・封禁・改名・發道具・調金幣' },
      { to: '/master',  icon: '👑', label: S.navMaster,      title: '以主帳號查詢旗下所有子角色，並可分帳充值' },
      { to: '/vip',     icon: '💎', label: S.navVip,         title: '查看黃金 VIP / 鑽石 VIP 玩家名單' },
      { to: '/cost-milestone', icon: '🏆', label: '消費里程碑', title: '查詢玩家累積消費進度，手動發放里程碑獎勵' },
    ]
  },
  {
    label: '紀錄查詢',
    items: [
      { to: '/history', icon: '🔍', label: '玩家活動歷程', title: '查詢單一玩家的交易、攤位、商店、消費等歷史紀錄' },
      { to: '/market',  icon: '🏪', label: '市場查詢',     title: '查詢攤位/商城上架商品，或根據道具 ID 反查持有者' },
      { to: '/records', icon: '📋', label: '全服記錄',     title: '全伺服器充值、交易、金幣異動、郵件紀錄查詢' },
    ]
  },
  {
    label: 'GM 工具',
    items: [
      { to: '/batchops', icon: '📦', label: '批量工具',     title: '批量發送道具、金幣、或全服廣播郵件' },
      { to: '/speedban', icon: '⚡', label: '加速外掛封禁', title: '分析加速行為異常玩家，批量封號' },
      { to: '/guild',    icon: '♖', label: '家族管理',     title: '家族列表、成員管理、解散家族、轉移成員' },
      { to: '/petcmd',   icon: '🐾', label: S.navPetCmd,    title: '產生 GM 寵物製作指令（petmake / petmakeabi）' },
      { to: '/petrank',  icon: '🏆', label: '練寵排行榜',   title: '練寵活動排行榜管理・審核・查玩家提交記錄・多號偵測' },
      { to: '/recycle',  icon: '🗑', label: S.navRecycle,   title: '查看並還原被刪除的角色' },
      { to: '/sql',         icon: '💻', label: S.navSql,       title: '執行唯讀 SQL 查詢（SELECT / SHOW / DESCRIBE）' },
      { to: '/db-browser',  icon: '🗄', label: '資料庫瀏覽',  title: '點選任意資料表即可查看內容，支援搜尋/翻頁' },
    ]
  },
  {
    label: '監控 / 分析',
    items: [
      { to: '/server-status', icon: '🖥', label: '伺服器狀態', title: '各分流在線人數、主帳號統計、最新註冊名單' },
      { to: '/analytics',     icon: '📈', label: '數據分析',   title: '儀表板・商城分析・玩家活躍度・儲值趨勢・交易稽核' },
    ]
  },
  {
    label: '系統管理',
    items: [
      { to: '/gmadmin', icon: '🔑', label: S.navGmAdmin, title: '新增或停用 GM 工具帳號、重設密碼' },
      { to: '/system',  icon: '⚙️', label: '系統設定',   title: 'GM 操作日誌・GM 權限管理・資料庫備份還原' },
    ]
  },
]

const HOME_NAV     = { to: '/',         icon: '🏠', label: '首頁',   title: '統計面板・伺服器概覽・常用快捷入口' }
const RECHARGE_NAV = { to: '/recharge', icon: '💳', label: '充值管理', title: '手動補單・累積儲值進度・匯率試算・充值記錄' }

export default function Layout() {
  const nav = useNavigate()
  const location = useLocation()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [isMobile, setIsMobile] = useState(() => typeof window !== 'undefined' && window.innerWidth < MOBILE_BREAKPOINT)
  const drawerRef = useRef<HTMLDivElement>(null)

  const logout = () => { localStorage.clear(); nav('/login') }
  const user = localStorage.getItem('gm_user') ?? 'GM'

  // 偵測螢幕寬度（與 useIsMobile / 手機切版一致）
  useEffect(() => {
    const onResize = () => setIsMobile(window.innerWidth < MOBILE_BREAKPOINT)
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  // 切換頁面時關閉 drawer
  useEffect(() => { setDrawerOpen(false) }, [location.pathname])

  // 點擊 / 觸控 overlay 關閉（同時支援 mousedown 與 touchstart）
  useEffect(() => {
    if (!drawerOpen) return
    const handler = (e: Event) => {
      const target = e instanceof TouchEvent
        ? e.targetTouches[0]?.target
        : (e as MouseEvent).target
      if (drawerRef.current && !drawerRef.current.contains(target as Node)) {
        setDrawerOpen(false)
      }
    }
    document.addEventListener('mousedown', handler)
    document.addEventListener('touchstart', handler, { passive: true })
    return () => {
      document.removeEventListener('mousedown', handler)
      document.removeEventListener('touchstart', handler)
    }
  }, [drawerOpen])

  const pageTitle = getRouteTitle(location.pathname)

  const navLinkStyle = (isActive: boolean): React.CSSProperties => ({
    display: 'flex', alignItems: 'center', gap: 10,
    padding: isMobile ? '12px 14px' : '8px 12px',
    borderRadius: 12, marginBottom: 4, fontSize: 14,
    background: isActive ? 'var(--neu-bg)' : 'transparent',
    color: isActive ? 'var(--accent-blue)' : 'var(--text-secondary)',
    fontWeight: isActive ? 700 : 500,
    textDecoration: 'none',
    boxShadow: isActive ? 'inset 3px 3px 6px #bebebe, inset -3px -3px 6px #ffffff' : 'none',
    border: isActive ? '1px solid rgba(59, 130, 246, 0.2)' : '1px solid transparent',
    transition: 'box-shadow .15s, color .15s, border-color .15s',
  })

  const SidebarContent = () => (
    <>
      {/* Logo */}
      <div style={{ padding: '18px 16px 14px', flexShrink: 0, boxShadow: 'inset 0 -2px 4px rgba(0,0,0,.04)' }}>
        <div style={{ fontSize: 22, marginBottom: 2 }}>🍅</div>
        <div style={{ fontWeight: 800, fontSize: 15, color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>{S.appName}</div>
        <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 1 }}>{S.appSub}</div>
        <div style={{ fontSize: 10, fontWeight: 700, color: 'var(--tech-cyan)', marginTop: 8, letterSpacing: '0.14em', textTransform: 'uppercase', opacity: 0.85 }}>
          GM Console
        </div>
      </div>

      {/* Nav */}
      <nav style={{ flex: 1, padding: '10px 8px', overflowY: 'auto' }}>
        {/* 首頁 + 充值 — 置頂醒目 */}
        <div style={{ margin: '4px 0 12px', display: 'flex', flexDirection: 'column', gap: 4 }}>
          <NavLink to={HOME_NAV.to} end title={HOME_NAV.title} style={({ isActive }) => ({
            ...navLinkStyle(isActive),
          })}>
            <span style={{ fontSize: 16, width: 20, textAlign: 'center', flexShrink: 0 }}>{HOME_NAV.icon}</span>
            <span>{HOME_NAV.label}</span>
          </NavLink>
          <NavLink to={RECHARGE_NAV.to} title={RECHARGE_NAV.title} style={({ isActive }) => ({
            ...navLinkStyle(isActive),
            color: isActive ? 'var(--accent-green)' : 'var(--text-secondary)',
            fontWeight: 700,
            boxShadow: isActive ? 'inset 3px 3px 6px #bebebe, inset -3px -3px 6px #ffffff' : '4px 4px 8px #bebebe, -4px -4px 8px #ffffff',
          })}>
            <span style={{ fontSize: 18, width: 20, textAlign: 'center', flexShrink: 0 }}>{RECHARGE_NAV.icon}</span>
            <span>{RECHARGE_NAV.label}</span>
          </NavLink>
        </div>

        {navGroups.map(g => (
          <div key={g.label} style={{ marginBottom: 10 }}>
            <div style={{ fontSize: 10, fontWeight: 700, color: 'var(--text-muted)', padding: '4px 10px 4px', letterSpacing: 1, textTransform: 'uppercase' }}>
              {g.label}
            </div>
            {g.items.map(n => (
              <NavLink key={n.to} to={n.to} end={n.to === '/'} title={n.title} style={({ isActive }) => navLinkStyle(isActive)}>
                <span style={{ width: 20, textAlign: 'center', fontSize: 16, flexShrink: 0 }}>{n.icon}</span>
                <span>{n.label}</span>
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      {/* 使用者 */}
      <div style={{ padding: '14px 16px', boxShadow: 'inset 0 2px 4px rgba(0,0,0,.04)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexShrink: 0 }}>
        <span style={{ color: 'var(--text-secondary)', fontSize: 13 }}>👤 {user}</span>
        <button onClick={logout} className="danger" style={{ fontSize: 12, padding: '8px 14px', borderRadius: 10 }}>
          {S.navLogout}
        </button>
      </div>
    </>
  )

  if (isMobile) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', height: '100dvh', overflow: 'hidden' }}>
        <a href="#main-content" className="skip-link">
          跳到主要內容
        </a>
        {/* 頂部 Header（手機觸控區至少 44px） */}
        <header style={{
          minHeight: 56,
          paddingTop: 'max(6px, env(safe-area-inset-top))',
          paddingLeft: 'max(12px, env(safe-area-inset-left))',
          paddingRight: 'max(12px, env(safe-area-inset-right))',
          background: 'linear-gradient(180deg, var(--bg-sidebar) 0%, rgba(207, 216, 232, 0.98) 100%)',
          boxShadow: '0 4px 16px rgba(15, 23, 42, 0.08), 0 0 0 1px rgba(59, 130, 246, 0.08)',
          display: 'flex', alignItems: 'center', paddingBottom: 8, gap: 8, flexShrink: 0, zIndex: 100,
        }}>
          <button
            type="button"
            onClick={() => setDrawerOpen(true)}
            aria-label="開啟選單"
            aria-expanded={drawerOpen}
            aria-controls="gm-drawer-nav"
            style={{ minWidth: 48, minHeight: 48, width: 48, height: 48, background: 'var(--neu-bg)', boxShadow: '4px 4px 8px #bebebe, -4px -4px 8px #ffffff', borderRadius: 12, fontSize: 20, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, padding: 0 }}>
            ☰
          </button>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 10, fontWeight: 700, color: 'var(--text-muted)', letterSpacing: '0.06em', textTransform: 'uppercase', marginBottom: 2 }}>
              {S.appName}
            </div>
            <div style={{ fontWeight: 800, fontSize: 17, color: 'var(--text-primary)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', letterSpacing: '-0.02em' }}>
              {pageTitle}
            </div>
          </div>
          <NavLink to="/recharge"
            style={({ isActive }) => ({
              display: 'flex', alignItems: 'center', gap: 6, minHeight: 44, padding: '10px 14px', borderRadius: 10, textDecoration: 'none', fontSize: 14, fontWeight: 700, flexShrink: 0,
              background: isActive ? 'rgba(74,222,128,.2)' : 'rgba(74,222,128,.12)',
              border: '1px solid rgba(74,222,128,.35)', color: '#4ade80',
            })}>
            💳 充值
          </NavLink>
        </header>

        {/* Drawer 背景遮罩 */}
        {drawerOpen && (
          <div
            role="presentation"
            aria-hidden
            style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.48)', zIndex: 200, backdropFilter: 'blur(4px)' }}
          />
        )}

        {/* 側拉抽屜 — 關閉時加 pointerEvents:none 防止攔截觸控 */}
        <div
          id="gm-drawer-nav"
          ref={drawerRef}
          data-drawer-nav
          aria-label="主選單導覽"
          aria-hidden={!drawerOpen}
          style={{
          position: 'fixed', top: 0, left: 0, bottom: 0, width: 'min(300px, calc(100vw - 40px))',
          paddingTop: 'env(safe-area-inset-top)',
          background: 'var(--bg-sidebar)',
          display: 'flex', flexDirection: 'column',
          zIndex: 300,
          transform: drawerOpen ? 'translateX(0)' : 'translateX(-100%)',
          transition: 'transform .25s cubic-bezier(.4,0,.2,1)',
          boxShadow: drawerOpen ? '8px 0 24px rgba(0,0,0,.12), -2px 0 8px rgba(255,255,255,.4)' : 'none',
          pointerEvents: drawerOpen ? undefined : 'none',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '14px 16px', boxShadow: 'inset 0 -2px 4px rgba(0,0,0,.04)' }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: 'var(--text-primary)' }}>🍅 {S.appName}</div>
            <button
              type="button"
              onClick={() => setDrawerOpen(false)}
              aria-label="關閉選單"
              style={{ minWidth: 48, minHeight: 48, width: 48, height: 48, background: 'var(--neu-bg)', boxShadow: 'inset 2px 2px 4px #bebebe, inset -2px -2px 4px #ffffff', borderRadius: 12, fontSize: 20, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 0 }}>
              ✕
            </button>
          </div>

          <nav style={{ flex: 1, padding: '10px 8px', overflowY: 'auto' }}>
            {/* 首頁 + 充值 */}
            <div style={{ margin: '4px 0 12px', display: 'flex', flexDirection: 'column', gap: 4 }}>
              <NavLink to={HOME_NAV.to} end style={({ isActive }) => ({
                display: 'flex', alignItems: 'center', gap: 12,
                padding: '13px 14px', borderRadius: 12, fontSize: 15, fontWeight: isActive ? 700 : 500, textDecoration: 'none',
                background: isActive ? 'var(--neu-bg)' : 'transparent',
                color: isActive ? 'var(--accent-blue)' : 'var(--text-secondary)',
                boxShadow: isActive ? 'inset 3px 3px 6px #bebebe, inset -3px -3px 6px #ffffff' : 'none',
              })}>
                <span style={{ fontSize: 20 }}>{HOME_NAV.icon}</span>
                {HOME_NAV.label}
              </NavLink>
              <NavLink to={RECHARGE_NAV.to} style={({ isActive }) => ({
                display: 'flex', alignItems: 'center', gap: 12,
                padding: '13px 14px', borderRadius: 12, fontSize: 15, fontWeight: 700, textDecoration: 'none',
                background: 'var(--neu-bg)',
                color: isActive ? 'var(--accent-green)' : 'var(--text-secondary)',
                boxShadow: isActive ? 'inset 3px 3px 6px #bebebe, inset -3px -3px 6px #ffffff' : '4px 4px 8px #bebebe, -4px -4px 8px #ffffff',
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
                    padding: '13px 14px', borderRadius: 12, marginBottom: 4, fontSize: 15,
                    background: isActive ? 'var(--neu-bg)' : 'transparent',
                    color: isActive ? 'var(--accent-blue)' : 'var(--text-secondary)',
                    fontWeight: isActive ? 700 : 400, textDecoration: 'none',
                    boxShadow: isActive ? 'inset 3px 3px 6px #bebebe, inset -3px -3px 6px #ffffff' : 'none',
                  })}>
                    <span style={{ fontSize: 18 }}>{n.icon}</span>
                    {n.label}
                  </NavLink>
                ))}
              </div>
            ))}
          </nav>

          <div style={{ padding: '14px 16px', boxShadow: 'inset 0 2px 4px rgba(0,0,0,.04)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ color: 'var(--text-secondary)', fontSize: 14 }}>👤 {user}</span>
            <button onClick={logout} className="danger" style={{ fontSize: 13, padding: '8px 14px', borderRadius: 10 }}>
              {S.navLogout}
            </button>
          </div>
        </div>

        {/* 主內容 */}
        <main
          id="main-content"
          tabIndex={-1}
          className="app-main-scroll"
          style={{ flex: 1, overflow: 'auto', overflowX: 'hidden', background: 'var(--bg-page)', WebkitOverflowScrolling: 'touch' as const }}
        >
          <Outlet />
        </main>
      </div>
    )
  }

  // ── 桌機版 ──
  return (
    <div style={{ display: 'flex', height: '100vh', overflow: 'hidden', background: 'var(--bg-page)' }}>
      <a href="#main-content" className="skip-link">
        跳到主要內容
      </a>
      <aside style={{
        width: 240,
        background: 'linear-gradient(180deg, var(--bg-sidebar) 0%, rgba(207, 216, 232, 0.98) 100%)',
        display: 'flex', flexDirection: 'column', flexShrink: 0, overflowY: 'auto',
        boxShadow: '6px 0 20px rgba(15, 23, 42, 0.07), 0 0 0 1px rgba(59, 130, 246, 0.06)',
      }}>
        <SidebarContent />
      </aside>
      <main id="main-content" tabIndex={-1} className="app-main-scroll app-main-desktop" style={{ flex: 1, overflow: 'auto', background: 'var(--bg-page)' }}>
        <Outlet />
      </main>
    </div>
  )
}
