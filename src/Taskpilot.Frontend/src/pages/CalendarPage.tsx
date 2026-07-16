import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import ActionsContextMenu from '../components/menus/ActionsContextMenu'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import Input from '../components/ui/Input'
import { cn } from '../lib/cn'
import { notify } from '../lib/toast'
import { useDragAndDrop } from '../hooks/useDragAndDrop'
import { calendarService } from '../services/calendarService'
import { taskService } from '../services/taskService'
import type { CalendarTask } from '../types/calendar'

const STATUS_COLORS: Record<string, string> = {
  Backlog: 'bg-border text-foreground',
  InProgress: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-950/50 dark:text-indigo-300',
  Review: 'bg-amber-100 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300',
  Done: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300',
}

type View = 'month' | 'week' | 'day'
const VIEWS: View[] = ['month', 'week', 'day']

const pad = (n: number) => String(n).padStart(2, '0')
/** Local calendar date as YYYY-MM-DD — also the drop-zone key, so it works in every view. */
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
const addDays = (d: Date, n: number) => {
  const x = new Date(d)
  x.setDate(d.getDate() + n)
  return x
}
/** Weeks start on Sunday, matching the weekday labels and Date.getDay(). */
const startOfWeek = (d: Date) => addDays(d, -d.getDay())
const sameDay = (a: Date, b: Date) => iso(a) === iso(b)

/** Calendar with month / week / day views — tasks sit on their deadline, colored by status. */
export default function CalendarPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const months = t('calendar.months', { returnObjects: true }) as string[]
  const weekdays = t('calendar.weekdays', { returnObjects: true }) as string[]

  const today = new Date()
  const [view, setView] = useState<View>('month')
  const [cursor, setCursor] = useState<Date>(today)
  const [tasks, setTasks] = useState<CalendarTask[]>([])
  const [feedUrl, setFeedUrl] = useState<string | null>(null)
  const [showFeed, setShowFeed] = useState(false)

  // The visible span depends on the view; tasks are fetched for exactly that span.
  const range = useMemo(() => {
    if (view === 'month') {
      const first = new Date(cursor.getFullYear(), cursor.getMonth(), 1)
      const last = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0)
      return { from: iso(first), to: iso(last) }
    }
    if (view === 'week') {
      const start = startOfWeek(cursor)
      return { from: iso(start), to: iso(addDays(start, 6)) }
    }
    return { from: iso(cursor), to: iso(cursor) }
  }, [view, cursor])

  useEffect(() => {
    calendarService.getTasks(range.from, range.to).then(setTasks).catch(() => {})
  }, [range.from, range.to])

  const tasksByDay = useMemo(() => {
    const map: Record<string, CalendarTask[]> = {}
    for (const task of tasks) {
      const key = task.deadline.slice(0, 10)
      ;(map[key] ??= []).push(task)
    }
    return map
  }, [tasks])

  // Step one month / week / day, depending on the active view.
  const step = (dir: 1 | -1) =>
    setCursor((c) => {
      if (view === 'month') return new Date(c.getFullYear(), c.getMonth() + dir, 1)
      return addDays(c, (view === 'week' ? 7 : 1) * dir)
    })

  const heading = () => {
    if (view === 'month') return `${months[cursor.getMonth()]} ${cursor.getFullYear()}`
    if (view === 'week') {
      const s = startOfWeek(cursor)
      const e = addDays(s, 6)
      return `${s.getDate()} ${months[s.getMonth()]} – ${e.getDate()} ${months[e.getMonth()]} ${e.getFullYear()}`
    }
    return `${weekdays[cursor.getDay()]}, ${cursor.getDate()} ${months[cursor.getMonth()]} ${cursor.getFullYear()}`
  }

  const exportIcs = async () => {
    const blob = await calendarService.exportIcs().catch(() => null)
    if (!blob) return
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'taskpilot.ics'
    a.click()
    URL.revokeObjectURL(url)
  }

  const openFeed = async () => {
    setShowFeed((s) => !s)
    if (feedUrl) return
    const url = await calendarService.getFeedUrl().catch(() => null)
    if (url) setFeedUrl(url)
  }

  const copyFeed = async () => {
    if (!feedUrl) return
    await navigator.clipboard.writeText(feedUrl).catch(() => {})
    notify.success(t('calendar.feedCopied'))
  }

  const regenerateFeed = async () => {
    const url = await calendarService.regenerateFeedUrl().catch(() => null)
    if (url) {
      setFeedUrl(url)
      notify.success(t('calendar.feedRegenerated'))
    }
  }

  // Drag-and-drop reschedule (pointer-based, so it works on touch too).
  const dropOnDate = async (targetIso: string, taskId: string) => {
    const task = tasks.find((x) => x.id === taskId)
    if (!task) return
    if (task.deadline.slice(0, 10) === targetIso) return // dropped on its own day

    // Keep the original time of day; only the date changes.
    const newDeadline = `${targetIso}T${task.deadline.slice(11) || '00:00:00Z'}`
    // Optimistic move, rolled back if the request fails.
    setTasks((prev) => prev.map((x) => (x.id === taskId ? { ...x, deadline: newDeadline } : x)))

    const updated = await taskService.reschedule(taskId, newDeadline).catch(() => null)
    if (!updated) {
      setTasks((prev) => prev.map((x) => (x.id === taskId ? { ...x, deadline: task.deadline } : x)))
      notify.error(t('calendar.rescheduleFailed'))
    }
  }

  const dnd = useDragAndDrop({
    onDrop: dropOnDate,
    renderGhost: (id) => {
      const task = tasks.find((x) => x.id === id)
      return (
        <div className="truncate rounded bg-primary px-2 py-1 text-[11px] font-medium text-white shadow-elevated">
          {task?.title ?? ''}
        </div>
      )
    },
  })

  /** One draggable task chip — shared by all three views. */
  const taskChip = (task: CalendarTask, big = false) => (
    <ActionsContextMenu
      key={task.id}
      actions={[
        { label: t('menu.openProject'), onSelect: () => navigate(`/projects/${task.projectId}`) },
        {
          label: t('menu.copyLink'),
          onSelect: () =>
            navigator.clipboard?.writeText(`${window.location.origin}/projects/${task.projectId}`).catch(() => {}),
        },
      ]}
    >
      <div
        {...dnd.draggableProps(task.id)}
        title={`${task.title} · ${task.projectName} · ${t(`board.status.${task.status}`, task.status)}`}
        className={cn(
          'truncate rounded font-medium transition hover:opacity-80',
          big ? 'px-3 py-2 text-sm' : 'px-1.5 py-0.5 text-[11px]',
          STATUS_COLORS[task.status] ?? 'bg-border text-foreground',
          dnd.draggingId === task.id && 'opacity-40',
        )}
        onClick={() => {
          if (dnd.justDragged()) return
          navigate(`/projects/${task.projectId}`)
        }}
      >
        {task.title}
        {big && <span className="ml-2 font-normal opacity-70">{task.projectName}</span>}
      </div>
    </ActionsContextMenu>
  )

  // --- views ---

  const monthGrid = () => {
    const year = cursor.getFullYear()
    const month = cursor.getMonth()
    const firstWeekday = new Date(year, month, 1).getDay()
    const daysInMonth = new Date(year, month + 1, 0).getDate()
    const cells: (number | null)[] = []
    for (let i = 0; i < firstWeekday; i++) cells.push(null)
    for (let d = 1; d <= daysInMonth; d++) cells.push(d)
    while (cells.length % 7 !== 0) cells.push(null)

    return (
      <Card className="overflow-hidden p-0">
        <div className="grid grid-cols-7 border-b border-border bg-canvas text-center text-xs font-semibold text-muted">
          {weekdays.map((w) => (
            <div key={w} className="py-3">
              {w}
            </div>
          ))}
        </div>

        <div className="grid grid-cols-7 gap-px bg-border">
          {cells.map((d, i) => {
            const key = d ? iso(new Date(year, month, d)) : null
            const dayTasks = key ? tasksByDay[key] ?? [] : []
            return (
              <div
                key={i}
                {...(key ? dnd.dropZoneProps(key) : {})}
                className={cn(
                  'min-h-28 p-2 transition-colors',
                  d ? 'bg-surface' : 'bg-canvas/50',
                  key !== null && dnd.activeZone === key && 'bg-primary/10 ring-2 ring-inset ring-primary',
                )}
              >
                {d && (
                  <>
                    <div
                      className={cn(
                        'mb-1.5 inline-flex h-7 w-7 items-center justify-center rounded-full text-xs font-medium',
                        sameDay(new Date(year, month, d), today) ? 'bg-primary font-bold text-white' : 'text-muted',
                      )}
                    >
                      {d}
                    </div>
                    <div className="space-y-1">{dayTasks.map((task) => taskChip(task))}</div>
                  </>
                )}
              </div>
            )
          })}
        </div>
      </Card>
    )
  }

  const weekGrid = () => {
    const start = startOfWeek(cursor)
    const days = Array.from({ length: 7 }, (_, i) => addDays(start, i))

    return (
      <Card className="overflow-hidden p-0">
        <div className="grid grid-cols-7 gap-px bg-border">
          {days.map((d) => {
            const key = iso(d)
            const dayTasks = tasksByDay[key] ?? []
            return (
              <div
                key={key}
                {...dnd.dropZoneProps(key)}
                className={cn(
                  'min-h-[22rem] bg-surface p-2 transition-colors',
                  dnd.activeZone === key && 'bg-primary/10 ring-2 ring-inset ring-primary',
                )}
              >
                <div className="mb-2 flex items-center gap-1.5 border-b border-border pb-2">
                  <span className="text-xs font-semibold text-muted">{weekdays[d.getDay()]}</span>
                  <span
                    className={cn(
                      'inline-flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium',
                      sameDay(d, today) ? 'bg-primary font-bold text-white' : 'text-muted',
                    )}
                  >
                    {d.getDate()}
                  </span>
                </div>
                <div className="space-y-1">{dayTasks.map((task) => taskChip(task))}</div>
              </div>
            )
          })}
        </div>
      </Card>
    )
  }

  const dayList = () => {
    const key = iso(cursor)
    const dayTasks = tasksByDay[key] ?? []
    return (
      <Card
        {...dnd.dropZoneProps(key)}
        className={cn('min-h-[22rem] p-4 transition-colors', dnd.activeZone === key && 'bg-primary/10 ring-2 ring-inset ring-primary')}
      >
        {dayTasks.length === 0 ? (
          <p className="py-16 text-center text-sm text-muted">{t('calendar.noTasks')}</p>
        ) : (
          <div className="space-y-2">{dayTasks.map((task) => taskChip(task, true))}</div>
        )}
      </Card>
    )
  }

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <h1 className="page-title">{t('calendar.title')}</h1>
        <div className="flex items-center gap-2">
          <Button variant="secondary" size="sm" onClick={openFeed}>
            {t('calendar.subscribe')}
          </Button>
          <Button variant="secondary" size="sm" onClick={exportIcs}>
            {t('calendar.exportIcs')}
          </Button>
        </div>
      </div>

      {showFeed && (
        <Card className="mb-4 p-4">
          <p className="mb-3 text-sm text-muted">{t('calendar.subscribeHint')}</p>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Input readOnly value={feedUrl ?? t('calendar.loading')} onFocus={(e) => e.currentTarget.select()} className="flex-1" />
            <Button size="sm" onClick={copyFeed} disabled={!feedUrl}>
              {t('calendar.copy')}
            </Button>
            <Button variant="secondary" size="sm" onClick={regenerateFeed}>
              {t('calendar.regenerate')}
            </Button>
          </div>
        </Card>
      )}

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Button variant="secondary" size="sm" onClick={() => step(-1)} aria-label={t('calendar.prev')}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="min-w-[13rem] text-center text-lg font-semibold">{heading()}</span>
        <Button variant="secondary" size="sm" onClick={() => step(1)} aria-label={t('calendar.next')}>
          <ChevronRight className="h-4 w-4" />
        </Button>
        <Button variant="secondary" size="sm" onClick={() => setCursor(new Date())}>
          {t('calendar.today')}
        </Button>

        {/* View switcher */}
        <div className="ml-auto inline-flex overflow-hidden rounded-lg border border-border">
          {VIEWS.map((v) => (
            <button
              key={v}
              onClick={() => setView(v)}
              className={cn(
                'px-3 py-1.5 text-sm font-medium transition',
                view === v ? 'bg-primary text-white' : 'text-foreground hover:bg-canvas',
              )}
            >
              {t(`calendar.view.${v}`)}
            </button>
          ))}
        </div>
      </div>

      {view === 'month' && monthGrid()}
      {view === 'week' && weekGrid()}
      {view === 'day' && dayList()}
      {dnd.overlay}
    </div>
  )
}
