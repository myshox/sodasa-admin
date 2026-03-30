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

function navClass({ isActive }: { isActive: boolean }) {
  return `gm-nav-link ${isActive ? 'gm-nav-link--active' : ''}`.trim()
}
function navClassRecharge({ isActive }: { isActive: boolean }) {
  return `gm-nav-link gm-nav-link--recharge ${isActive ? 'gm-nav-link--active' : ''}`.trim()
}

/** 側欄／抽屜共用導覽連結 */
function SidebarNavLinks() {
  return (
    <>
      <div style={{ margin: '4px 0 12px', display: 'flex', flexDirection: 'column', gap: 6 }}>
        <NavLink to={HOME_NAV.to} end title={HOME_NAV.title} className={navClass}>
          <span className="gm-nav-link__icon" aria-hidden>{HOME_NAV.icon}</span>
          <span>{HOME_NAV.label}</span>
        </NavLink>
        <NavLink to={RECHARGE_NAV.to} title={RECHARGE_NAV.title} className={navClassRecharge}>
          <span className="gm-nav-link__icon" aria-hidden>{RECHARGE_NAV.icon}</span>
          <span>{RECHARGE_NAV.label}</span>
        </NavLink>
      </div>
      {navGroups.map(g => (
        <div key={g.label} style={{ marginBottom: 10 }}>
          <div className="gm-sidebar__nav-group-label">{g.label}</div>
          {g.items.map(n => (
            <NavLink key={n.to} to={n.to} end={n.to === '/'} title={n.title} className={navClass}>
              <span className="gm-nav-link__icon" aria-hidden>{n.icon}</span>
              <span>{n.label}</span>
            </NavLink>
          ))}
        </div>
      ))}
    </>
  )
}

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

  const SidebarContent = () => (
    <>
      <div className="gm-sidebar__brand">
        <div style={{ fontSize: 24, marginBottom: 4, lineHeight: 1 }} aria-hidden>🍅</div>
        <div style={{ fontWeight: 800, fontSize: 15, color: 'var(--text-primary)', letterSpacing: '-0.03em' }}>{S.appName}</div>
        <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 4, lineHeight: 1.4 }}>{S.appSub}</div>
        <span className="gm-sidebar__brand-badge">GM Console</span>
      </div>

      <nav className="gm-sidebar__nav" aria-label="主要導覽">
        <SidebarNavLinks />
      </nav>

      <div className="gm-sidebar__footer">
        <span style={{ color: 'var(--text-secondary)', fontSize: 13, fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={user}>👤 {user}</span>
        <button type="button" onClick={logout} className="danger" style={{ fontSize: 12, padding: '8px 14px', borderRadius: 10, flexShrink: 0 }}>
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
          className="gm-sidebar gm-sidebar--drawer"
          aria-label="主選單導覽"
          aria-hidden={!drawerOpen}
          style={{
          position: 'fixed', top: 0, left: 0, bottom: 0,
          paddingTop: 'env(safe-area-inset-top)',
          display: 'flex', flexDirection: 'column',
          zIndex: 300,
          transform: drawerOpen ? 'translateX(0)' : 'translateX(-100%)',
          transition: 'transform .25s cubic-bezier(.4,0,.2,1)',
          boxShadow: drawerOpen ? '8px 0 28px rgba(15, 23, 42, 0.12)' : 'none',
          pointerEvents: drawerOpen ? undefined : 'none',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '14px 16px', borderBottom: '1px solid var(--border)', flexShrink: 0, background: 'rgba(255,255,255,0.35)' }}>
            <div style={{ fontWeight: 800, fontSize: 15, color: 'var(--text-primary)', letterSpacing: '-0.02em' }}>🍅 {S.appName}</div>
            <button
              type="button"
              onClick={() => setDrawerOpen(false)}
              aria-label="關閉選單"
              style={{ minWidth: 48, minHeight: 48, width: 48, height: 48, background: 'var(--neu-bg)', boxShadow: 'var(--neu-shadow-raised-sm)', borderRadius: 12, fontSize: 20, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 0, border: '1px solid var(--border)' }}>
              ✕
            </button>
          </div>

          <nav className="gm-sidebar__nav" aria-label="主要導覽">
            <SidebarNavLinks />
          </nav>

          <div className="gm-sidebar__footer">
            <span style={{ color: 'var(--text-secondary)', fontSize: 14, fontWeight: 600 }}>👤 {user}</span>
            <button type="button" onClick={logout} className="danger" style={{ fontSize: 13, padding: '8px 14px', borderRadius: 10 }}>
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
      <aside className="gm-sidebar">
        <SidebarContent />
      </aside>
      <main id="main-content" tabIndex={-1} className="app-main-scroll app-main-desktop gm-main" style={{ flex: 1, overflow: 'auto' }}>
        <Outlet />
      </main>
    </div>
  )
}
