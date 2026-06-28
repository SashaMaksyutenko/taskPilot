import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, useNavigate } from 'react-router-dom'
import Avatar from './Avatar'
import { useNotifications } from '../hooks/useNotifications'
import { logout } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'

// `key` points at the i18n translation key (nav.*); `to` is the route.
const LINKS = [
  { to: '/', key: 'nav.dashboard', end: true },
  { to: '/projects', key: 'nav.projects', end: false },
  { to: '/calendar', key: 'nav.calendar', end: false },
  { to: '/forum', key: 'nav.forum', end: false },
  { to: '/marketplace', key: 'nav.market', end: false },
  { to: '/chat', key: 'nav.chat', end: false },
  { to: '/notes', key: 'nav.notes', end: false },
  { to: '/search', key: 'nav.search', end: false },
]

/**
 * Shared top navigation: brand, links, theme toggle, notification bell and logout.
 * Used across the main authenticated pages.
 */
export default function Navbar() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { t, i18n } = useTranslation()
  const user = useAppSelector((s) => s.auth.user)
  const [dark, setDark] = useState(() => document.documentElement.classList.contains('dark'))

  // Toggle between English and Ukrainian (persisted to localStorage by the detector).
  const toggleLang = () => i18n.changeLanguage(i18n.language.startsWith('uk') ? 'en' : 'uk')

  const {
    unread,
    notes,
    toasts,
    open: notesOpen,
    bellRef,
    toggle: toggleNotes,
    openNotification,
    openToast,
    dismissToast,
    markAllRead,
  } = useNotifications()

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
    <>
      <header className="sticky top-0 z-10 flex items-center gap-1 border-b border-slate-200 bg-white px-4 py-2.5 dark:border-slate-700 dark:bg-slate-800">
      <NavLink to="/" className="mr-4 flex items-center gap-2 font-bold text-[#1E2A44] dark:text-white">
        <img src="/logo-mark.svg" className="h-7 w-7" alt="" />
        TaskPilot
      </NavLink>

      <nav className="hidden items-center gap-1 sm:flex">
        {(user?.role === 'Admin' ? [...LINKS, { to: '/admin', key: 'nav.admin', end: false }] : LINKS).map((l) => (
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
            {t(l.key)}
          </NavLink>
        ))}
      </nav>

      <div className="ml-auto flex items-center gap-3">
        <button
          onClick={toggleLang}
          title="Change language"
          className="rounded-lg px-2 py-2 text-xs font-bold text-slate-500 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-700"
        >
          {i18n.language.startsWith('uk') ? 'УКР' : 'EN'}
        </button>

        <button
          onClick={toggleTheme}
          title="Toggle theme"
          className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-700"
        >
          {dark ? '☀️' : '🌙'}
        </button>

        <div className="relative" ref={bellRef}>
          <button
            onClick={toggleNotes}
            title="Notifications"
            className="relative rounded-lg p-2 text-slate-500 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-700"
          >
            🔔
            {unread > 0 && (
              <span className="absolute right-0 top-0 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold text-white">
                {unread}
              </span>
            )}
          </button>

          {notesOpen && (
            <div className="absolute right-0 top-full z-20 mt-2 w-80 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl dark:border-slate-700 dark:bg-slate-800">
              <div className="flex items-center justify-between border-b border-slate-200 px-4 py-2.5 dark:border-slate-700">
                <span className="text-sm font-bold">Notifications</span>
                {unread > 0 && (
                  <button onClick={markAllRead} className="text-xs font-semibold text-[#1E2A44] hover:underline dark:text-slate-300">
                    Mark all read
                  </button>
                )}
              </div>

              <div className="max-h-96 overflow-y-auto">
                {notes.length === 0 ? (
                  <p className="px-4 py-6 text-center text-sm text-slate-400">No notifications.</p>
                ) : (
                  notes.map((n) => (
                    <button
                      key={n.id}
                      onClick={() => openNotification(n)}
                      className={`flex w-full gap-2 border-b border-slate-100 px-4 py-3 text-left text-sm last:border-b-0 hover:bg-slate-50 dark:border-slate-700/60 dark:hover:bg-slate-700/50 ${
                        n.isRead ? '' : 'bg-slate-50/60 dark:bg-slate-700/30'
                      }`}
                    >
                      <span
                        className={`mt-1.5 h-2 w-2 flex-none rounded-full ${
                          n.isRead ? 'bg-transparent' : 'bg-[#F6BE2C]'
                        }`}
                      />
                      <span className="min-w-0">
                        <span className="block text-slate-700 dark:text-slate-200">{n.message}</span>
                        <span className="mt-0.5 block text-xs text-slate-400">
                          {new Date(n.createdAt).toLocaleString()}
                        </span>
                      </span>
                    </button>
                  ))
                )}
              </div>
            </div>
          )}
        </div>

        <NavLink
          to="/settings"
          className="flex items-center gap-2 text-sm text-slate-500 hover:text-[#1E2A44] dark:text-slate-300 dark:hover:text-white"
          title={user?.name}
        >
          <Avatar name={user?.name ?? '?'} src={user?.avatarUrl} size={30} />
          <span className="hidden sm:inline">{user?.name}</span>
        </NavLink>
        <button
          onClick={handleLogout}
          className="text-sm font-semibold text-slate-500 hover:text-[#1E2A44] dark:text-slate-300 dark:hover:text-white"
        >
          {t('nav.logout')}
        </button>
      </div>
      </header>

      {/* Toasts for incoming real-time notifications */}
      {toasts.length > 0 && (
        <div className="fixed bottom-4 right-4 z-50 flex w-80 flex-col gap-2">
          {toasts.map((t) => (
            <div
              key={t.id}
              className="flex items-start gap-2 rounded-xl border border-slate-200 bg-white p-3 shadow-lg dark:border-slate-700 dark:bg-slate-800"
            >
              <span className="mt-1.5 h-2 w-2 flex-none rounded-full bg-[#F6BE2C]" />
              <button onClick={() => openToast(t)} className="min-w-0 flex-1 text-left text-sm">
                <span className="block text-slate-700 dark:text-slate-200">{t.message}</span>
              </button>
              <button
                onClick={() => dismissToast(t.id)}
                className="flex-none text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                ✕
              </button>
            </div>
          ))}
        </div>
      )}
    </>
  )
}
