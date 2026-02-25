import { Outlet, NavLink, useNavigate } from 'react-router-dom'
import { S } from '../strings'

const navItems = [
  { to: '/',        icon: '&#128202;', label: S.navDashboard },
  { to: '/players', icon: '&#128101;', label: S.navPlayers },
  { to: '/online',  icon: '&#128994;', label: S.navOnline },
  { to: '/petcmd',  icon: '&#128062;', label: S.navPetCmd },
]

export default function Layout() {
  const nav = useNavigate()
  const logout = () => { localStorage.clear(); nav('/login') }
  const user = localStorage.getItem('gm_user') ?? 'GM'

  return (
    <div style={{ display: 'flex', height: '100vh', overflow: 'hidden' }}>
      <aside style={{
        width: 220, background: 'var(--bg-sidebar)',
        borderRight: '1px solid var(--border)',
        display: 'flex', flexDirection: 'column', flexShrink: 0
      }}>
        <div style={{ padding: '20px 16px 14px', borderBottom: '1px solid var(--border)' }}>
          <div style={{ fontSize: 22, marginBottom: 4 }}>&#127813;</div>
          <div style={{ fontWeight: 700, fontSize: 15, color: 'var(--text-primary)' }}>{S.appName}</div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{S.appSub}</div>
        </div>

        <nav style={{ flex: 1, padding: '10px 8px', overflowY: 'auto' }}>
          {navItems.map(n => (
            <NavLink key={n.to} to={n.to} end={n.to === '/'} style={({ isActive }) => ({
              display: 'flex', alignItems: 'center', gap: 10,
              padding: '9px 12px', borderRadius: 'var(--radius)',
              marginBottom: 2, fontSize: 14,
              background: isActive ? 'rgba(74,158,255,.15)' : 'transparent',
              color: isActive ? 'var(--accent-blue)' : 'var(--text-secondary)',
              fontWeight: isActive ? 600 : 400,
              textDecoration: 'none', transition: 'all .15s'
            })}>
              <span dangerouslySetInnerHTML={{ __html: n.icon }} />
              <span>{n.label}</span>
            </NavLink>
          ))}
        </nav>

        <div style={{
          padding: '12px 16px', borderTop: '1px solid var(--border)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between'
        }}>
          <span style={{ color: 'var(--text-secondary)', fontSize: 13 }}>&#128100; {user}</span>
          <button onClick={logout} style={{
            background: 'transparent', color: 'var(--text-muted)',
            fontSize: 12, padding: '4px 8px',
            border: '1px solid var(--border)', borderRadius: 6
          }}>{S.navLogout}</button>
        </div>
      </aside>

      <main style={{ flex: 1, overflow: 'auto', background: 'var(--bg-page)' }}>
        <Outlet />
      </main>
    </div>
  )
}
