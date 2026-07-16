import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '../lib/cn'
import type { Task } from '../types/project'

/** Bar colour per Kanban status. */
const STATUS_BAR: Record<string, string> = {
  Backlog: 'bg-slate-400 dark:bg-slate-500',
  InProgress: 'bg-indigo-500',
  Review: 'bg-amber-500',
  Done: 'bg-emerald-500',
}

const DAY = 86_400_000
const TICKS = 5

/**
 * Project timeline (Gantt): one bar per task, spanning creation → deadline.
 * Tasks without a deadline have no end, so they are counted but not drawn.
 */
export default function GanttChart({ tasks, onSelect }: { tasks: Task[]; onSelect?: (task: Task) => void }) {
  const { t } = useTranslation()

  const model = useMemo(() => {
    const scheduled = tasks.filter((task) => task.deadline)
    if (scheduled.length === 0) return null

    // A task's bar always runs left→right, even if its deadline predates creation.
    const rows = scheduled.map((task) => {
      const a = new Date(task.createdAt).getTime()
      const b = new Date(task.deadline!).getTime()
      return { task, start: Math.min(a, b), end: Math.max(a, b) }
    })

    // Pad a day either side so bars never sit flush against the edge.
    const min = Math.min(...rows.map((r) => r.start)) - DAY
    const max = Math.max(...rows.map((r) => r.end)) + DAY
    const span = Math.max(max - min, DAY)
    const pct = (ms: number) => ((ms - min) / span) * 100

    return {
      rows: rows
        .sort((x, y) => x.start - y.start)
        .map((r) => ({
          ...r,
          left: pct(r.start),
          // Keep very short tasks visible.
          width: Math.max(pct(r.end) - pct(r.start), 1),
        })),
      ticks: Array.from({ length: TICKS }, (_, i) => ({
        pct: (i / (TICKS - 1)) * 100,
        date: new Date(min + (span * i) / (TICKS - 1)),
      })),
      todayPct: pct(Date.now()),
      unscheduled: tasks.length - scheduled.length,
    }
  }, [tasks])

  if (!model) return <p className="py-16 text-center text-sm text-muted">{t('gantt.empty')}</p>

  const fmt = (d: Date) => d.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })

  return (
    <div className="overflow-x-auto">
      <div className="min-w-[40rem]">
        {/* Date axis */}
        <div className="mb-2 flex">
          <div className="w-48 flex-none" />
          <div className="relative h-5 flex-1">
            {model.ticks.map((tick, i) => (
              <span
                key={i}
                className="absolute -translate-x-1/2 text-[11px] text-muted"
                style={{ left: `${tick.pct}%` }}
              >
                {fmt(tick.date)}
              </span>
            ))}
          </div>
        </div>

        <div className="space-y-1.5">
          {model.rows.map(({ task, left, width, start, end }) => (
            <div key={task.id} className="flex items-center">
              <div className="w-48 flex-none truncate pr-3 text-sm" title={task.title}>
                {task.title}
              </div>

              <div className="relative h-7 flex-1 rounded bg-canvas">
                {/* Today marker */}
                {model.todayPct >= 0 && model.todayPct <= 100 && (
                  <div
                    className="absolute inset-y-0 w-px bg-red-500/70"
                    style={{ left: `${model.todayPct}%` }}
                    aria-hidden
                  />
                )}
                <button
                  onClick={() => onSelect?.(task)}
                  title={`${task.title} · ${fmt(new Date(start))} → ${fmt(new Date(end))}${
                    task.assigneeName ? ` · ${task.assigneeName}` : ''
                  }`}
                  className={cn(
                    'absolute inset-y-1 rounded transition hover:opacity-80',
                    STATUS_BAR[task.status] ?? 'bg-slate-400',
                  )}
                  style={{ left: `${left}%`, width: `${width}%` }}
                />
              </div>
            </div>
          ))}
        </div>

        {model.unscheduled > 0 && (
          <p className="mt-3 text-xs text-muted">{t('gantt.unscheduled', { n: model.unscheduled })}</p>
        )}
      </div>
    </div>
  )
}
