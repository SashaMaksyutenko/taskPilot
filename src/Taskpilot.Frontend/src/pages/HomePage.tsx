import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { calendarService } from '../services/calendarService'
import { notificationService } from '../services/notificationService'
import { projectService } from '../services/projectService'
import { fetchMe, logout } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import type { AppNotification } from '../types/notification'

const pad = (n: number) => String(n).padStart(2, '0')
const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

/**
 * Personal dashboard: quick stats, recent notifications and quick actions.
 * Pulls live data from the projects, notifications and calendar APIs.
 */
export default function HomePage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { user, isAuthenticated } = useAppSelector((s) => s.auth)

  const [projectCount, setProjectCount] = useState(0)
  const [unread, setUnread] = useState(0)
  const [upcoming, setUpcoming] = useState(0)
  const [notifications, setNotifications] = useState<AppNotification[]>([])

  useEffect(() => {
    if (isAuthenticated && !user) dispatch(fetchMe())
  }, [isAuthenticated, user, dispatch])

  useEffect(() => {
    projectService.getProjects().then((p) => setProjectCount(p.length)).catch(() => {})
    notificationService.getUnreadCount().then(setUnread).catch(() => {})
    notificationService.getNotifications().then((n) => setNotifications(n.slice(0, 6))).catch(() => {})

    const today = new Date()
    const in30 = new Date()
    in30.setDate(today.getDate() + 30)
    calendarService
      .getTasks(isoDate(today), isoDate(in30))
      .then((t) => setUpcoming(t.length))
      .catch(() => {})
  }, [])

  const handleLogout = () => {
    dispatch(logout())
    navigate('/login')
  }

  const markAllRead = async () => {
    await notificationService.markAllRead().catch(() => {})
    setUnread(0)
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })))
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44]">
      {/* Top bar */}
      <header className="flex items-center gap-3 border-b border-slate-200 bg-white px-6 py-3">
        <img src="/logo-mark.svg" alt="" className="h-7 w-7" />
        <span className="font-bold">TaskPilot</span>
        <div className="ml-auto flex items-center gap-4">
          <span className="text-sm text-slate-500">{user?.name}</span>
          <button onClick={handleLogout} className="text-sm font-semibold text-slate-500 hover:text-[#1E2A44]">
            Log out
          </button>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-6 py-8">
        <h1 className="text-2xl font-bold">Welcome back{user ? `, ${user.name}` : ''} 👋</h1>
        <p className="mt-1 text-sm text-slate-500">Here’s what’s happening across your work.</p>

        {/* Stat cards */}
        <div className="mt-6 grid gap-4 sm:grid-cols-3">
          <Stat label="Projects" value={projectCount} accent="bg-indigo-100 text-indigo-700" />
          <Stat label="Unread notifications" value={unread} accent="bg-blue-100 text-blue-700" />
          <Stat label="Deadlines (30 days)" value={upcoming} accent="bg-amber-100 text-amber-700" />
        </div>

        <div className="mt-6 grid gap-4 lg:grid-cols-3">
          {/* Recent activity */}
          <div className="rounded-xl border border-slate-200 bg-white p-5 lg:col-span-2">
            <div className="mb-3 flex items-center">
              <h2 className="font-bold">Recent activity</h2>
              {unread > 0 && (
                <button onClick={markAllRead} className="ml-auto text-xs font-semibold text-slate-500 hover:underline">
                  Mark all read
                </button>
              )}
            </div>
            {notifications.length === 0 ? (
              <p className="py-6 text-center text-sm text-slate-400">No activity yet.</p>
            ) : (
              <ul className="divide-y divide-slate-100">
                {notifications.map((n) => (
                  <li key={n.id} className="flex items-start gap-3 py-3">
                    <span className={`mt-1.5 h-2 w-2 flex-none rounded-full ${n.isRead ? 'bg-slate-300' : 'bg-[#F6BE2C]'}`} />
                    <div>
                      <p className="text-sm">{n.message}</p>
                      <p className="text-xs text-slate-400">{new Date(n.createdAt).toLocaleString()}</p>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Quick actions */}
          <div className="rounded-xl border border-slate-200 bg-white p-5">
            <h2 className="mb-3 font-bold">Quick actions</h2>
            <div className="space-y-2">
              <Action to="/projects" label="My projects" primary />
              <Action to="/calendar" label="Calendar" />
              <Action to="/chat" label="Open chat" />
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}

function Stat({ label, value, accent }: { label: string; value: number; accent: string }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5">
      <div className="text-sm text-slate-500">{label}</div>
      <div className="mt-1 flex items-center gap-2">
        <span className="text-3xl font-bold">{value}</span>
        <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${accent}`}>total</span>
      </div>
    </div>
  )
}

function Action({ to, label, primary }: { to: string; label: string; primary?: boolean }) {
  return (
    <Link
      to={to}
      className={`block rounded-lg py-2.5 text-center text-sm font-semibold transition ${
        primary
          ? 'bg-[#1E2A44] text-white hover:bg-[#27345a]'
          : 'border border-slate-300 text-[#1E2A44] hover:bg-slate-50'
      }`}
    >
      {label}
    </Link>
  )
}
