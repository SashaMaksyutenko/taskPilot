import { useEffect, useState } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { notificationService } from '../services/notificationService'
import { logout } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

const LINKS = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/projects', label: 'Projects', end: false },
  { to: '/calendar', label: 'Calendar', end: false },
  { to: '/forum', label: 'Forum', end: false },
  { to: '/marketplace', label: 'Market', end: false },
  { to: '/chat', label: 'Chat', end: false },
]

/**
 * Shared top navigation: brand, links, theme toggle, notification bell and logout.
 * Used across the main authenticated pages.
 */
export default function Navbar() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const user = useAppSelector((s) => s.auth.user)
  const [unread, setUnread] = useState(0)
  const [dark, setDark] = useState(() => document.documentElement.classList.contains('dark'))

  useEffect(() => {
    notificationService.getUnreadCount().then(setUnread).catch(() => {})
  }, [])

  const toggleTheme = () => {
    const next = !dark
    setDark(next)
    document.documentElement.classList.toggle('dark', next)
    localStorage.setItem('theme', next ? 'dark' : 'light')
  }

  const handleLogout = () => {
    dispatch(logout())
    navigate('/login')
  }

  return (
    <header className="sticky top-0 z-10 flex items-center gap-1 border-b border-slate-200 bg-white px-4 py-2.5 dark:border-slate-700 dark:bg-slate-800">
      <NavLink to="/" className="mr-4 flex items-center gap-2 font-bold text-[#1E2A44] dark:text-white">
        <img src="/logo-mark.svg" className="h-7 w-7" alt="" />
        TaskPilot
      </NavLink>

      <nav className="hidden items-center gap-1 sm:flex">
        {(user?.role === 'Admin' ? [...LINKS, { to: '/admin', label: 'Admin', end: false }] : LINKS).map((l) => (
          <NavLink
            key={l.to}
            to={l.to}
            end={l.end}
            className={({ isActive }) =>
              `rounded-lg px-3 py-1.5 text-sm font-semibold ${
                isActive
                  ? 'bg-slate-100 text-[#1E2A44] dark:bg-slate-700 dark:text-white'
                  : 'text-slate-500 hover:text-[#1E2A44] dark:text-slate-400 dark:hover:text-white'
              }`
            }
          >
            {l.label}
          </NavLink>
        ))}
      </nav>

      <div className="ml-auto flex items-center gap-3">
        <button
          onClick={toggleTheme}
          title="Toggle theme"
          className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-700"
        >
          {dark ? '☀️' : '🌙'}
        </button>

        <div className="relative text-slate-500 dark:text-slate-300" title="Notifications">
          🔔
          {unread > 0 && (
            <span className="absolute -right-1 -top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold text-white">
              {unread}
            </span>
          )}
        </div>

        <NavLink
          to="/settings"
          className="hidden text-sm text-slate-500 hover:text-[#1E2A44] dark:text-slate-300 dark:hover:text-white sm:inline"
        >
          {user?.name}
        </NavLink>
        <button
          onClick={handleLogout}
          className="text-sm font-semibold text-slate-500 hover:text-[#1E2A44] dark:text-slate-300 dark:hover:text-white"
        >
          Log out
        </button>
      </div>
    </header>
  )
}
