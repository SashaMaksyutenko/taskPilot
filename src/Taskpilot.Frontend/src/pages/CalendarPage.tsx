import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import Navbar from '../components/Navbar'
import { calendarService } from '../services/calendarService'
import type { CalendarTask } from '../types/calendar'
import { notify } from '../lib/toast'

const STATUS_COLORS: Record<string, string> = {
  Backlog: 'bg-slate-200 text-slate-700',
  InProgress: 'bg-blue-100 text-blue-700',
  Review: 'bg-amber-100 text-amber-700',
  Done: 'bg-green-100 text-green-700',
}

const pad = (n: number) => String(n).padStart(2, '0')
const dateKey = (y: number, m: number, d: number) => `${y}-${pad(m + 1)}-${pad(d)}`

/**
 * Month calendar. Shows tasks on their deadline day, colored by status.
 */
export default function CalendarPage() {
  const { t } = useTranslation()
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
    for (const t of tasks) {
      const key = t.deadline.slice(0, 10)
      ;(map[key] ??= []).push(t)
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

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-6 py-6">
        <div className="mb-4 flex items-center justify-between">
          <h1 className="text-2xl font-bold">{t('calendar.title')}</h1>
          <div className="flex items-center gap-2">
            <button
              onClick={openFeed}
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
            >
              {t('calendar.subscribe')}
            </button>
            <button
              onClick={exportIcs}
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
            >
              {t('calendar.exportIcs')}
            </button>
          </div>
        </div>

        {showFeed && (
          <div className="mb-4 rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-800">
            <p className="mb-2 text-sm text-slate-600 dark:text-slate-300">{t('calendar.subscribeHint')}</p>
            <div className="flex flex-col gap-2 sm:flex-row">
              <input
                readOnly
                value={feedUrl ?? t('calendar.loading')}
                onFocus={(e) => e.currentTarget.select()}
                className="flex-1 rounded-lg border border-slate-300 bg-slate-50 px-3 py-1.5 text-sm dark:border-slate-600 dark:bg-slate-900"
              />
              <button
                onClick={copyFeed}
                disabled={!feedUrl}
                className="rounded-lg bg-[#1E2A44] px-3 py-1.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
              >
                {t('calendar.copy')}
              </button>
              <button
                onClick={regenerateFeed}
                className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-slate-50 dark:border-slate-600 dark:hover:bg-slate-700"
              >
                {t('calendar.regenerate')}
              </button>
            </div>
          </div>
        )}

        <div className="mb-4 flex items-center gap-4">
          <button onClick={prevMonth} className="rounded-lg border border-slate-300 px-3 py-1 hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800">
            ←
          </button>
          <span className="text-lg font-semibold">
            {months[month]} {year}
          </span>
          <button onClick={nextMonth} className="rounded-lg border border-slate-300 px-3 py-1 hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800">
            →
          </button>
        </div>

        <div className="grid grid-cols-7 gap-px text-center text-xs font-semibold text-slate-500 dark:text-slate-400">
          {weekdays.map((w) => (
            <div key={w} className="py-2">
              {w}
            </div>
          ))}
        </div>

        <div className="grid grid-cols-7 gap-px overflow-hidden rounded-lg bg-slate-200 dark:bg-slate-700">
          {cells.map((d, i) => {
            const dayTasks = d ? tasksByDay[dateKey(year, month, d)] ?? [] : []
            return (
              <div
                key={i}
                className={`min-h-24 p-1.5 ${d ? 'bg-white dark:bg-slate-800' : 'bg-slate-50 dark:bg-slate-900'}`}
              >
                {d && (
                  <>
                    <div
                      className={`mb-1 inline-flex h-6 w-6 items-center justify-center rounded-full text-xs ${
                        isToday(d) ? 'bg-[#1E2A44] font-bold text-white' : 'text-slate-500 dark:text-slate-400'
                      }`}
                    >
                      {d}
                    </div>
                    <div className="space-y-1">
                      {dayTasks.map((task) => (
                        <div
                          key={task.id}
                          title={`${task.title} · ${task.projectName} · ${t(`board.status.${task.status}`, task.status)}`}
                          className={`truncate rounded px-1.5 py-0.5 text-[11px] font-medium ${
                            STATUS_COLORS[task.status] ?? 'bg-slate-200 text-slate-700'
                          }`}
                        >
                          {task.title}
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </div>
            )
          })}
        </div>
      </main>
    </div>
  )
}
