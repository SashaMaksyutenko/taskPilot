import { useEffect, useState } from 'react'
import { Calendar, FolderKanban, Bell, AlertTriangle, Sparkles, ArrowRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import FadeIn from '../components/FadeIn'
import Card from '../components/ui/Card'
import Skeleton from '../components/ui/Skeleton'
import { calendarService } from '../services/calendarService'
import { chatbotService } from '../services/chatbotService'
import { notificationService } from '../services/notificationService'
import { projectService } from '../services/projectService'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import type { AppNotification } from '../types/notification'
import type { CalendarTask } from '../types/calendar'
import { cn } from '../lib/cn'

const pad = (n: number) => String(n).padStart(2, '0')
const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

/** Personal dashboard — stats, overdue tasks, recent activity. */
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
  const [aiEnabled, setAiEnabled] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (isAuthenticated && !user) dispatch(fetchMe())
  }, [isAuthenticated, user, dispatch])

  useEffect(() => {
    const today = new Date()
    const in30 = new Date()
    in30.setDate(today.getDate() + 30)

    Promise.allSettled([
      projectService.getProjects().then((p) => setProjectCount(p.length)),
      notificationService.getUnreadCount().then(setUnread),
      notificationService.getNotifications().then((n) => setNotifications(n.slice(0, 6))),
      calendarService.getTasks(isoDate(today), isoDate(in30)).then((t) => setUpcoming(t.length)),
      calendarService.getOverdue().then(setOverdue),
      chatbotService.status().then((s) => setAiEnabled(s.enabled)),
    ]).finally(() => setLoading(false))
  }, [])

  const markAllRead = async () => {
    await notificationService.markAllRead().catch(() => {})
    setUnread(0)
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })))
  }

  const openNotification = async (n: AppNotification) => {
    if (!n.isRead) {
      await notificationService.markRead(n.id).catch(() => {})
      setNotifications((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
      setUnread((c) => Math.max(0, c - 1))
    }
    if (n.link) navigate(n.link)
  }

  const stats = [
    { label: t('dashboard.projects'), value: projectCount, icon: FolderKanban, tone: 'from-indigo-500/10 to-indigo-600/5 text-indigo-600' },
    { label: t('dashboard.unread'), value: unread, icon: Bell, tone: 'from-sky-500/10 to-sky-600/5 text-sky-600' },
    { label: t('dashboard.deadlines'), value: upcoming, icon: Calendar, tone: 'from-amber-500/10 to-amber-600/5 text-amber-600' },
    { label: t('dashboard.overdue'), value: overdue.length, icon: AlertTriangle, tone: 'from-red-500/10 to-red-600/5 text-red-600' },
  ]

  return (
    <div className="mx-auto max-w-5xl">
      <FadeIn>
        <h1 className="page-title">
          {t('dashboard.welcome')}
          {user ? `, ${user.name}` : ''} 👋
        </h1>
        <p className="page-subtitle mt-1">{t('dashboard.subtitle')}</p>

        <div className="mt-8 grid grid-cols-2 gap-4 lg:grid-cols-4">
          {stats.map((s, i) => (
            <FadeIn key={s.label} delay={i * 0.05}>
              <Card className="p-5">
                <div className={cn('mb-3 inline-flex rounded-xl bg-gradient-to-br p-2.5', s.tone)}>
                  <s.icon className="h-5 w-5" strokeWidth={2} />
                </div>
                {loading ? (
                  <div className="h-8 w-12 animate-pulse rounded bg-canvas" />
                ) : (
                  <div className="text-3xl font-bold tabular-nums">{s.value}</div>
                )}
                <div className="mt-1 text-sm text-muted">{s.label}</div>
              </Card>
            </FadeIn>
          ))}
        </div>

        {aiEnabled && <AssistantCard onAsk={(prompt) => navigate('/assistant', { state: { prompt } })} />}

        {overdue.length > 0 && (
          <Card className="mt-6 border-red-200 bg-red-50/50 p-5 dark:border-red-900/40 dark:bg-red-950/20">
            <h2 className="mb-3 flex items-center gap-2 font-bold text-red-700 dark:text-red-400">
              <AlertTriangle className="h-5 w-5" />
              {t('dashboard.overdueTasks')}
            </h2>
            <ul className="divide-y divide-red-100 dark:divide-red-900/40">
              {overdue.map((task) => (
                <li key={task.id}>
                  <Link to={`/projects/${task.projectId}`} className="flex items-center gap-3 py-2.5 text-sm hover:opacity-80">
                    <span className="font-medium">{task.title}</span>
                    <span className="text-xs text-muted">{task.projectName}</span>
                    <span className="ml-auto text-xs font-semibold text-red-600 dark:text-red-400">
                      {new Date(task.deadline).toLocaleDateString()}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        )}

        <div className="mt-6 grid gap-4 lg:grid-cols-3">
          <Card className="p-5 lg:col-span-2">
            <div className="mb-4 flex items-center">
              <h2 className="font-bold">{t('dashboard.recentActivity')}</h2>
              {unread > 0 && (
                <button onClick={markAllRead} className="ml-auto text-xs font-semibold text-primary hover:underline">
                  {t('dashboard.markAllRead')}
                </button>
              )}
            </div>
            {loading ? (
              <div className="space-y-3 py-2">
                {Array.from({ length: 4 }).map((_, i) => (
                  <div key={i} className="flex items-center gap-3">
                    <Skeleton className="h-9 w-9 rounded-full" />
                    <div className="flex-1 space-y-1.5">
                      <Skeleton className="h-3 w-2/3" />
                      <Skeleton className="h-2.5 w-1/4" />
                    </div>
                  </div>
                ))}
              </div>
            ) : notifications.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted">{t('dashboard.noActivity')}</p>
            ) : (
              <ul className="divide-y divide-border">
                {notifications.map((n) => (
                  <li key={n.id}>
                    <button
                      onClick={() => openNotification(n)}
                      className="flex w-full items-start gap-3 py-3 text-left hover:opacity-80"
                    >
                      <span className={cn('mt-1.5 h-2 w-2 flex-none rounded-full', n.isRead ? 'bg-border' : 'bg-accent')} />
                      <div>
                        <p className="text-sm">{n.message}</p>
                        <p className="text-xs text-muted">{new Date(n.createdAt).toLocaleString()}</p>
                      </div>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </Card>

          <Card className="p-5">
            <h2 className="mb-4 font-bold">{t('dashboard.quickActions')}</h2>
            <div className="space-y-2">
              <QuickLink to="/projects" label={t('dashboard.myProjects')} primary />
              <QuickLink to="/calendar" label={t('dashboard.calendar')} />
              <QuickLink to="/chat" label={t('dashboard.openChat')} />
            </div>
          </Card>
        </div>
      </FadeIn>
    </div>
  )
}

/** Prominent entry point to the data-aware AI assistant, with one-tap example prompts. */
function AssistantCard({ onAsk }: { onAsk: (prompt: string) => void }) {
  const { t } = useTranslation()
  const examples = [t('dashboard.aiExample1'), t('dashboard.aiExample2'), t('dashboard.aiExample3')]

  return (
    <Card className="mt-6 overflow-hidden border-primary/20 bg-gradient-to-br from-primary/10 via-surface to-surface p-5">
      <div className="flex flex-wrap items-start gap-4">
        <div className="inline-flex flex-none rounded-xl bg-gradient-to-br from-primary/20 to-primary/5 p-2.5 text-primary">
          <Sparkles className="h-5 w-5" strokeWidth={2} />
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="font-bold">{t('dashboard.aiTitle')}</h2>
          <p className="mt-0.5 text-sm text-muted">{t('dashboard.aiSubtitle')}</p>

          <div className="mt-3 flex flex-wrap gap-2">
            {examples.map((ex) => (
              <button
                key={ex}
                onClick={() => onAsk(ex)}
                className="rounded-full border border-border bg-surface px-3 py-1.5 text-xs font-medium text-foreground transition hover:border-primary hover:text-primary"
              >
                {ex}
              </button>
            ))}
          </div>
        </div>
        <button
          onClick={() => onAsk('')}
          className="inline-flex flex-none items-center gap-1.5 rounded-lg bg-primary px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-primary-hover"
        >
          {t('dashboard.aiOpen')}
          <ArrowRight className="h-4 w-4" />
        </button>
      </div>
    </Card>
  )
}

function QuickLink({ to, label, primary }: { to: string; label: string; primary?: boolean }) {
  return (
    <Link
      to={to}
      className={cn(
        'block rounded-lg py-2.5 text-center text-sm font-semibold transition',
        primary
          ? 'bg-primary text-white hover:bg-primary-hover'
          : 'border border-border text-foreground hover:bg-canvas',
      )}
    >
      {label}
    </Link>
  )
}
