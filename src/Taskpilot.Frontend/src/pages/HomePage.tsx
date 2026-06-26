import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import Navbar from '../components/Navbar'
import { calendarService } from '../services/calendarService'
import { notificationService } from '../services/notificationService'
import { projectService } from '../services/projectService'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import type { AppNotification } from '../types/notification'
import type { CalendarTask } from '../types/calendar'

const pad = (n: number) => String(n).padStart(2, '0')
const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

/**
 * Personal dashboard: quick stats, recent notifications and quick actions.
 * Pulls live data from the projects, notifications and calendar APIs.
 */
export default function HomePage() {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { t } = useTranslation()
  const { user, isAuthenticated } = useAppSelector((s) => s.auth)

  const [projectCount, setProjectCount] = useState(0)
  const [unread, setUnread] = useState(0)
  const [upcoming, setUpcoming] = useState(0)
  const [overdue, setOverdue] = useState<CalendarTask[]>([])
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (isAuthenticated && !user) dispatch(fetchMe())
  }, [isAuthenticated, user, dispatch])

  useEffect(() => {
    const today = new Date()
    const in30 = new Date()
    in30.setDate(today.getDate() + 30)

    // Load all dashboard data, then drop the loading state once everything settles.
    Promise.allSettled([
      projectService.getProjects().then((p) => setProjectCount(p.length)),
      notificationService.getUnreadCount().then(setUnread),
      notificationService.getNotifications().then((n) => setNotifications(n.slice(0, 6))),
      calendarService.getTasks(isoDate(today), isoDate(in30)).then((t) => setUpcoming(t.length)),
      calendarService.getOverdue().then(setOverdue),
    ]).finally(() => setLoading(false))
  }, [])

  const markAllRead = async () => {
    await notificationService.markAllRead().catch(() => {})
    setUnread(0)
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })))
  }

  // Open a notification: mark it read locally + on the server and follow its link.
  const openNotification = async (n: AppNotification) => {
    if (!n.isRead) {
      await notificationService.markRead(n.id).catch(() => {})
      setNotifications((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
      setUnread((c) => Math.max(0, c - 1))
    }
    if (n.link) navigate(n.link)
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />

      <main className="mx-auto max-w-5xl px-6 py-8">
        <h1 className="text-2xl font-bold">{t('dashboard.welcome')}{user ? `, ${user.name}` : ''} 👋</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
          {t('dashboard.subtitle')}
        </p>

        <div className="mt-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
          <Stat label={t('dashboard.projects')} value={projectCount} accent="bg-indigo-100 text-indigo-700" loading={loading} />
          <Stat label={t('dashboard.unread')} value={unread} accent="bg-blue-100 text-blue-700" loading={loading} />
          <Stat label={t('dashboard.deadlines')} value={upcoming} accent="bg-amber-100 text-amber-700" loading={loading} />
          <Stat label={t('dashboard.overdue')} value={overdue.length} accent="bg-red-100 text-red-700" loading={loading} />
        </div>

        {/* Overdue tasks */}
        {overdue.length > 0 && (
          <div className="mt-6 rounded-xl border border-red-200 bg-red-50 p-5 dark:border-red-900/50 dark:bg-red-950/20">
            <h2 className="mb-3 font-bold text-red-700 dark:text-red-400">{t('dashboard.overdueTasks')}</h2>
            <ul className="divide-y divide-red-100 dark:divide-red-900/40">
              {overdue.map((task) => (
                <li key={task.id}>
                  <Link to={`/projects/${task.projectId}`} className="flex items-center gap-3 py-2 text-sm hover:opacity-80">
                    <span className="font-medium">{task.title}</span>
                    <span className="text-xs text-slate-500 dark:text-slate-400">{task.projectName}</span>
                    <span className="ml-auto text-xs font-semibold text-red-600 dark:text-red-400">
                      {new Date(task.deadline).toLocaleDateString()}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        )}

        <div className="mt-6 grid gap-4 lg:grid-cols-3">
          <div className="rounded-xl border border-slate-200 bg-white p-5 lg:col-span-2 dark:border-slate-700 dark:bg-slate-800">
            <div className="mb-3 flex items-center">
              <h2 className="font-bold">{t('dashboard.recentActivity')}</h2>
              {unread > 0 && (
                <button onClick={markAllRead} className="ml-auto text-xs font-semibold text-slate-500 hover:underline dark:text-slate-400">
                  {t('dashboard.markAllRead')}
                </button>
              )}
            </div>
            {loading ? (
              <p className="py-6 text-center text-sm text-slate-400">{t('dashboard.loading')}</p>
            ) : notifications.length === 0 ? (
              <p className="py-6 text-center text-sm text-slate-400">{t('dashboard.noActivity')}</p>
            ) : (
              <ul className="divide-y divide-slate-100 dark:divide-slate-700">
                {notifications.map((n) => (
                  <li key={n.id}>
                    <button
                      onClick={() => openNotification(n)}
                      className="flex w-full items-start gap-3 py-3 text-left hover:opacity-80"
                    >
                      <span className={`mt-1.5 h-2 w-2 flex-none rounded-full ${n.isRead ? 'bg-slate-300' : 'bg-[#F6BE2C]'}`} />
                      <div>
                        <p className="text-sm">{n.message}</p>
                        <p className="text-xs text-slate-400">{new Date(n.createdAt).toLocaleString()}</p>
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
            <h2 className="mb-3 font-bold">{t('dashboard.quickActions')}</h2>
            <div className="space-y-2">
              <Action to="/projects" label={t('dashboard.myProjects')} primary />
              <Action to="/calendar" label={t('dashboard.calendar')} />
              <Action to="/chat" label={t('dashboard.openChat')} />
            </div>
          </div>
        </div>
      </main>
    </div>
  )
}

function Stat({
  label,
  value,
  accent,
  loading,
}: {
  label: string
  value: number
  accent: string
  loading: boolean
}) {
  const { t } = useTranslation()
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
      <div className="text-sm text-slate-500 dark:text-slate-400">{label}</div>
      <div className="mt-1 flex items-center gap-2">
        {loading ? (
          <span className="my-1 h-7 w-10 animate-pulse rounded bg-slate-200 dark:bg-slate-700" />
        ) : (
          <>
            <span className="text-3xl font-bold">{value}</span>
            <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${accent}`}>{t('dashboard.total')}</span>
          </>
        )}
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
          : 'border border-slate-300 text-[#1E2A44] hover:bg-slate-50 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-700'
      }`}
    >
      {label}
    </Link>
  )
}
