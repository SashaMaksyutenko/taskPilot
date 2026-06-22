import { useEffect, useRef, useState } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { notificationService } from '../services/notificationService'
import type { AppNotification } from '../types/notification'
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

  const [notesOpen, setNotesOpen] = useState(false)
  const [notes, setNotes] = useState<AppNotification[]>([])
  const bellRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    notificationService.getUnreadCount().then(setUnread).catch(() => {})
  }, [])

  // Close the notifications panel when clicking anywhere outside it.
  useEffect(() => {
    if (!notesOpen) return
    const onClick = (e: MouseEvent) => {
      if (bellRef.current && !bellRef.current.contains(e.target as Node)) setNotesOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [notesOpen])

  const toggleNotes = () => {
    const next = !notesOpen
    setNotesOpen(next)
    if (next) {
      // Load the latest notifications each time the panel opens.
      notificationService.getNotifications().then(setNotes).catch(() => {})
    }
  }

  const openNotification = async (n: AppNotification) => {
    if (!n.isRead) {
      await notificationService.markRead(n.id).catch(() => {})
      setNotes((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
      setUnread((c) => Math.max(0, c - 1))
    }
    setNotesOpen(false)
    if (n.link) navigate(n.link)
  }

  const markAllRead = async () => {
    await notificationService.markAllRead().catch(() => {})
    setNotes((prev) => prev.map((x) => ({ ...x, isRead: true })))
    setUnread(0)
  }

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
