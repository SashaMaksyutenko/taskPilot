import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { calendarService } from '../services/calendarService'
import type { CalendarTask } from '../types/calendar'

const STATUS_COLORS: Record<string, string> = {
  Backlog: 'bg-slate-200 text-slate-700',
  InProgress: 'bg-blue-100 text-blue-700',
  Review: 'bg-amber-100 text-amber-700',
  Done: 'bg-green-100 text-green-700',
}

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

const pad = (n: number) => String(n).padStart(2, '0')
/** YYYY-MM-DD for a given year, 0-based month and day. */
const dateKey = (y: number, m: number, d: number) => `${y}-${pad(m + 1)}-${pad(d)}`

/**
 * Month calendar. Shows tasks on their deadline day, colored by status.
 * Navigate months with the arrows; data is fetched per visible month.
 */
export default function CalendarPage() {
  const today = new Date()
  const [year, setYear] = useState(today.getFullYear())
  const [month, setMonth] = useState(today.getMonth()) // 0-based
  const [tasks, setTasks] = useState<CalendarTask[]>([])

  useEffect(() => {
    const lastDay = new Date(year, month + 1, 0).getDate()
    calendarService
      .getTasks(dateKey(year, month, 1), dateKey(year, month, lastDay))
      .then(setTasks)
      .catch(() => {})
  }, [year, month])

  // Group tasks by their deadline date (the UTC date part of the ISO string).
  const tasksByDay = useMemo(() => {
    const map: Record<string, CalendarTask[]> = {}
    for (const t of tasks) {
      const key = t.deadline.slice(0, 10)
      ;(map[key] ??= []).push(t)
    }
    return map
  }, [tasks])

  // Build the grid cells: leading blanks + days, padded to full weeks.
  const firstWeekday = new Date(year, month, 1).getDay()
  const daysInMonth = new Date(year, month + 1, 0).getDate()
  const cells: (number | null)[] = []
  for (let i = 0; i < firstWeekday; i++) cells.push(null)
  for (let d = 1; d <= daysInMonth; d++) cells.push(d)
  while (cells.length % 7 !== 0) cells.push(null)

  const prevMonth = () =>
    month === 0 ? (setMonth(11), setYear((y) => y - 1)) : setMonth((m) => m - 1)
  const nextMonth = () =>
    month === 11 ? (setMonth(0), setYear((y) => y + 1)) : setMonth((m) => m + 1)

  const isToday = (d: number) =>
    year === today.getFullYear() && month === today.getMonth() && d === today.getDate()

  return (
    <div className="min-h-screen bg-slate-50 px-6 py-6 text-[#1E2A44]">
      <div className="mx-auto max-w-5xl">
        <div className="mb-5 flex items-center gap-3">
          <img src="/logo-mark.svg" alt="" className="h-8 w-8" />
          <h1 className="text-2xl font-bold">Calendar</h1>
          <Link to="/" className="ml-auto text-sm text-slate-500 hover:underline">
            Home
          </Link>
        </div>

        {/* Month navigation */}
        <div className="mb-4 flex items-center gap-4">
          <button onClick={prevMonth} className="rounded-lg border border-slate-300 px-3 py-1 hover:bg-white">
            ←
          </button>
          <span className="text-lg font-semibold">
            {MONTHS[month]} {year}
          </span>
          <button onClick={nextMonth} className="rounded-lg border border-slate-300 px-3 py-1 hover:bg-white">
            →
          </button>
        </div>

        {/* Weekday header */}
        <div className="grid grid-cols-7 gap-px text-center text-xs font-semibold text-slate-500">
          {WEEKDAYS.map((w) => (
            <div key={w} className="py-2">
              {w}
            </div>
          ))}
        </div>

        {/* Day grid */}
        <div className="grid grid-cols-7 gap-px overflow-hidden rounded-lg bg-slate-200">
          {cells.map((d, i) => {
            const dayTasks = d ? tasksByDay[dateKey(year, month, d)] ?? [] : []
            return (
              <div key={i} className={`min-h-24 bg-white p-1.5 ${d ? '' : 'bg-slate-50'}`}>
                {d && (
                  <>
                    <div
                      className={`mb-1 inline-flex h-6 w-6 items-center justify-center rounded-full text-xs ${
                        isToday(d) ? 'bg-[#1E2A44] font-bold text-white' : 'text-slate-500'
                      }`}
                    >
                      {d}
                    </div>
                    <div className="space-y-1">
                      {dayTasks.map((t) => (
                        <div
                          key={t.id}
                          title={`${t.title} · ${t.projectName} · ${t.status}`}
                          className={`truncate rounded px-1.5 py-0.5 text-[11px] font-medium ${
                            STATUS_COLORS[t.status] ?? 'bg-slate-200 text-slate-700'
                          }`}
                        >
                          {t.title}
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
