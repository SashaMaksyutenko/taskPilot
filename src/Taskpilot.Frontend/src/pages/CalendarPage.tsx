import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import ActionsContextMenu from '../components/ActionsContextMenu'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import Input from '../components/ui/Input'
import { cn } from '../lib/cn'
import { notify } from '../lib/toast'
import { calendarService } from '../services/calendarService'
import { taskService } from '../services/taskService'
import type { CalendarTask } from '../types/calendar'

const STATUS_COLORS: Record<string, string> = {
  Backlog: 'bg-border text-foreground',
  InProgress: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-950/50 dark:text-indigo-300',
  Review: 'bg-amber-100 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300',
  Done: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300',
}

const pad = (n: number) => String(n).padStart(2, '0')
const dateKey = (y: number, m: number, d: number) => `${y}-${pad(m + 1)}-${pad(d)}`

/** Month calendar — tasks on deadline days, colored by status. */
export default function CalendarPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const months = t('calendar.months', { returnObjects: true }) as string[]
  const weekdays = t('calendar.weekdays', { returnObjects: true }) as string[]

  const today = new Date()
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth())
  const [tasks, setTasks] = useState<CalendarTask[]>([])
  const [feedUrl, setFeedUrl] = useState<string | null>(null)
  const [showFeed, setShowFeed] = useState(false)

  useEffect(() => {
    const lastDay = new Date(year, month + 1, 0).getDate()
    calendarService
      .getTasks(dateKey(year, month, 1), dateKey(year, month, lastDay))
      .then(setTasks)
      .catch(() => {})
  }, [year, month])

  const tasksByDay = useMemo(() => {
    const map: Record<string, CalendarTask[]> = {}
    for (const task of tasks) {
      const key = task.deadline.slice(0, 10)
      ;(map[key] ??= []).push(task)
    }
    return map
  }, [tasks])

  const firstWeekday = new Date(year, month, 1).getDay()
  const daysInMonth = new Date(year, month + 1, 0).getDate()
  const cells: (number | null)[] = []
  for (let i = 0; i < firstWeekday; i++) cells.push(null)
  for (let d = 1; d <= daysInMonth; d++) cells.push(d)
  while (cells.length % 7 !== 0) cells.push(null)

  const prevMonth = () => (month === 0 ? (setMonth(11), setYear((y) => y - 1)) : setMonth((m) => m - 1))
  const nextMonth = () => (month === 11 ? (setMonth(0), setYear((y) => y + 1)) : setMonth((m) => m + 1))

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

  const isToday = (d: number) =>
    year === today.getFullYear() && month === today.getMonth() && d === today.getDate()

  // Drag-and-drop reschedule: the day cell being hovered while dragging a task.
  const [dragOverDay, setDragOverDay] = useState<number | null>(null)

  const dropOnDay = async (day: number, taskId: string) => {
    setDragOverDay(null)
    const task = tasks.find((x) => x.id === taskId)
    if (!task) return

    const target = dateKey(year, month, day)
    if (task.deadline.slice(0, 10) === target) return // dropped on its own day

    // Keep the original time of day; only the date changes.
    const newDeadline = `${target}T${task.deadline.slice(11) || '00:00:00Z'}`
    // Optimistic move, rolled back if the request fails.
    setTasks((prev) => prev.map((x) => (x.id === taskId ? { ...x, deadline: newDeadline } : x)))

    const updated = await taskService.reschedule(taskId, newDeadline).catch(() => null)
    if (!updated) {
      setTasks((prev) => prev.map((x) => (x.id === taskId ? { ...x, deadline: task.deadline } : x)))
      notify.error(t('calendar.rescheduleFailed'))
    }
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

      <div className="mb-4 flex items-center gap-3">
        <Button variant="secondary" size="sm" onClick={prevMonth} aria-label="Previous month">
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="min-w-[10rem] text-center text-lg font-semibold">
          {months[month]} {year}
        </span>
        <Button variant="secondary" size="sm" onClick={nextMonth} aria-label="Next month">
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>

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
            const dayTasks = d ? tasksByDay[dateKey(year, month, d)] ?? [] : []
            return (
              <div
                key={i}
                onDragOver={(e) => {
                  if (!d) return
                  e.preventDefault() // allow the drop
                  setDragOverDay(d)
                }}
                onDragLeave={() => d && dragOverDay === d && setDragOverDay(null)}
                onDrop={(e) => {
                  if (!d) return
                  e.preventDefault()
                  const taskId = e.dataTransfer.getData('taskId')
                  if (taskId) dropOnDay(d, taskId)
                }}
                className={cn(
                  'min-h-28 p-2 transition-colors',
                  d ? 'bg-surface' : 'bg-canvas/50',
                  d !== null && dragOverDay === d && 'bg-primary/10 ring-2 ring-inset ring-primary',
                )}
              >
                {d && (
                  <>
                    <div
                      className={cn(
                        'mb-1.5 inline-flex h-7 w-7 items-center justify-center rounded-full text-xs font-medium',
                        isToday(d) ? 'bg-primary font-bold text-white' : 'text-muted',
                      )}
                    >
                      {d}
                    </div>
                    <div className="space-y-1">
                      {dayTasks.map((task) => (
                        <ActionsContextMenu
                          key={task.id}
                          actions={[
                            { label: t('menu.openProject'), onSelect: () => navigate(`/projects/${task.projectId}`) },
                            {
                              label: t('menu.copyLink'),
                              onSelect: () =>
                                navigator.clipboard
                                  ?.writeText(`${window.location.origin}/projects/${task.projectId}`)
                                  .catch(() => {}),
                            },
                          ]}
                        >
                          <div
                            draggable
                            onDragStart={(e) => e.dataTransfer.setData('taskId', task.id)}
                            title={`${task.title} · ${task.projectName} · ${t(`board.status.${task.status}`, task.status)}`}
                            className={cn(
                              'cursor-grab truncate rounded px-1.5 py-0.5 text-[11px] font-medium transition hover:opacity-80 active:cursor-grabbing',
                              STATUS_COLORS[task.status] ?? 'bg-border text-foreground',
                            )}
                            onClick={() => navigate(`/projects/${task.projectId}`)}
                          >
                            {task.title}
                          </div>
                        </ActionsContextMenu>
                      ))}
                    </div>
                  </>
                )}
              </div>
            )
          })}
        </div>
      </Card>
    </div>
  )
}
