import { useEffect, useState } from 'react'
import { Calendar, FolderKanban, Bell, AlertTriangle, Sparkles, ArrowRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import FadeIn from '../components/feedback/FadeIn'
import WeeklyDigest from '../components/WeeklyDigest'
import Card from '../components/ui/Card'
import Skeleton from '../components/ui/Skeleton'
import { calendarService } from '../services/calendarService'
import { chatbotService } from '../services/chatbotService'
import { notificationService } from '../services/notificationService'
import { projectService } from '../services/projectService'
import { taskService } from '../services/taskService'
import type { MyTask } from '../types/project'
import { fetchMe } from '../store/authSlice'
import { useAppDispatch, useAppSelector } from '../store/hooks'
import type { AppNotification } from '../types/notification'
import type { CalendarTask } from '../types/calendar'
import { cn } from '../lib/cn'

const pad = (n: number) => String(n).padStart(2, '0')
const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

/** Coloured dot for a task's Kanban status. */
const statusDot = (status: string) =>
  status === 'Done' ? 'bg-green-500'
    : status === 'Review' ? 'bg-amber-500'
      : status === 'InProgress' ? 'bg-blue-500'
        : 'bg-slate-400'

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
  const [myTasks, setMyTasks] = useState<MyTask[]>([])
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
      taskService.getMine().then((ts) => setMyTasks(ts.filter((x) => x.status !== 'Done').slice(0, 6))),
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
    { label: t('dashboard.projects'), value: projectCount, icon: FolderKanban, tile: 'bg-primary-muted text-primary' },
    { label: t('dashboard.unread'), value: unread, icon: Bell, tile: 'bg-canvas text-muted' },
    { label: t('dashboard.deadlines'), value: upcoming, icon: Calendar, tile: 'bg-amber-500/10 text-amber-600 dark:text-amber-400' },
    {
      label: t('dashboard.overdue'),
      value: overdue.length,
      icon: AlertTriangle,
      tile: overdue.length > 0 ? 'bg-red-500/10 text-red-500' : 'bg-canvas text-muted',
    },
  ]

  return (
    <div className="mx-auto max-w-5xl">
      <FadeIn>
        {/* Greeting */}
        <div className="mb-5">
          <h1 className="text-2xl font-bold tracking-tight sm:text-[26px]">
            {t('dashboard.welcome')}
            {user ? (
              <>
                , <span className="text-primary">{user.name}</span>
              </>
            ) : (
              ''
            )}{' '}
            👋
          </h1>
          <p className="page-subtitle mt-1">{t('dashboard.subtitle')}</p>
        </div>

        {/* KPIs — flat, hairline stat cards */}
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {stats.map((s, i) => (
            <FadeIn key={s.label} delay={i * 0.05}>
              <div className="rounded-[var(--radius-card)] border border-border bg-surface p-4 transition hover:border-muted/40">
                <span className={cn('inline-flex rounded-lg p-2', s.tile)}>
                  <s.icon className="h-[18px] w-[18px]" strokeWidth={2} />
                </span>
                {loading ? (
                  <div className="mt-3 h-7 w-12 animate-pulse rounded bg-canvas" />
                ) : (
                  <div className="mt-3 text-2xl font-bold tabular-nums">{s.value}</div>
                )}
                <div className="mt-0.5 text-[13px] text-muted">{s.label}</div>
              </div>
            </FadeIn>
          ))}
        </div>

        <WeeklyDigest aiEnabled={aiEnabled} />

        {aiEnabled && <AssistantCard onAsk={(prompt) => navigate('/assistant', { state: { prompt } })} />}

        {overdue.length > 0 && (
          <Card className="mt-6 border-red-500/30 bg-red-500/5 p-5">
            <h2 className="mb-3 flex items-center gap-2 font-bold text-red-600 dark:text-red-400">
              <AlertTriangle className="h-5 w-5" />
              {t('dashboard.overdueTasks')}
            </h2>
            <ul className="divide-y divide-red-500/15">
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

        {/* My work + quick actions */}
        <div className="mt-6 grid gap-4 lg:grid-cols-3">
          <Card className="p-5 lg:col-span-2">
            <div className="mb-3 flex items-center">
              <h2 className="font-bold">{t('nav.myTasks')}</h2>
              <Link to="/my-tasks" className="ml-auto text-xs font-semibold text-primary hover:underline">
                {t('dashboard.seeAll')} →
              </Link>
            </div>
            {loading ? (
              <div className="space-y-3 py-2">
                {Array.from({ length: 4 }).map((_, i) => (
                  <div key={i} className="flex items-center gap-3">
                    <Skeleton className="h-2.5 w-2.5 rounded-full" />
                    <Skeleton className="h-3 flex-1" />
                  </div>
                ))}
              </div>
            ) : myTasks.length === 0 ? (
              <p className="py-8 text-center text-sm text-muted">{t('myTasks.empty')}</p>
            ) : (
              <ul className="space-y-1">
                {myTasks.map((task) => {
                  const overdueTask = task.deadline && new Date(task.deadline).getTime() < Date.now()
                  return (
                    <li key={task.id}>
                      <Link
                        to={`/projects/${task.projectId}?task=${task.id}`}
                        className="flex items-center gap-2.5 rounded-lg px-2 py-2 text-sm transition hover:bg-canvas"
                      >
                        <span className={cn('h-2 w-2 flex-none rounded-full', statusDot(task.status))} />
                        <span className="min-w-0 flex-1 truncate font-medium">{task.title}</span>
                        <span className="hidden items-center gap-1.5 text-xs text-muted sm:flex">
                          <span className="inline-block h-2 w-2 rounded-full" style={{ background: task.projectColor ?? '#94a3b8' }} />
                          <span className="max-w-[8rem] truncate">{task.projectName}</span>
                        </span>
                        {task.deadline && (
                          <span className={cn('flex-none text-xs', overdueTask ? 'font-semibold text-red-600 dark:text-red-400' : 'text-muted')}>
                            {new Date(task.deadline).toLocaleDateString()}
                          </span>
                        )}
                      </Link>
                    </li>
                  )
                })}
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

        {/* Recent activity — full width */}
        <Card className="mt-6 p-5">
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
      </FadeIn>
    </div>
  )
}

/** Prominent entry point to the data-aware AI assistant, with one-tap example prompts. */
function AssistantCard({ onAsk }: { onAsk: (prompt: string) => void }) {
  const { t } = useTranslation()
  const examples = [t('dashboard.aiExample1'), t('dashboard.aiExample2'), t('dashboard.aiExample3')]

  return (
    <Card className="mt-6 overflow-hidden border-primary/25 p-5">
      <div className="flex flex-wrap items-start gap-4">
        <div className="inline-flex flex-none rounded-lg bg-primary-muted p-2.5 text-primary">
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
          className="gradient-primary inline-flex flex-none items-center gap-1.5 rounded-lg px-4 py-2.5 text-sm font-semibold text-white shadow-sm shadow-primary/25 transition hover:brightness-[1.06]"
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
          ? 'gradient-primary text-white shadow-sm shadow-primary/25 hover:brightness-[1.06]'
          : 'border border-border text-foreground hover:bg-canvas',
      )}
    >
      {label}
    </Link>
  )
}
