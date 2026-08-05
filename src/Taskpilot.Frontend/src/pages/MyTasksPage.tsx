import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import EmptyState from '../components/feedback/EmptyState'
import { cn } from '../lib/cn'
import { taskService } from '../services/taskService'
import type { MyTask } from '../types/project'

/** Ordering + icon kind for each status column (soonest-actionable first). */
const STATUS_META: Record<string, { rank: number; kind: 'backlog' | 'todo' | 'prog' | 'review' | 'done' }> = {
  InProgress: { rank: 0, kind: 'prog' },
  Review: { rank: 1, kind: 'review' },
  Todo: { rank: 2, kind: 'todo' },
  Backlog: { rank: 3, kind: 'backlog' },
  Done: { rank: 4, kind: 'done' },
}

/** Linear-style status "donut": outline for open work, a filling wedge for in-progress/review. */
function StatusIcon({ status, className }: { status: string; className?: string }) {
  const kind = STATUS_META[status]?.kind ?? 'todo'
  const base = 'h-3.5 w-3.5 flex-none rounded-full'
  if (kind === 'backlog') return <span className={cn(base, 'border-[1.5px] border-dashed border-muted', className)} />
  if (kind === 'todo') return <span className={cn(base, 'border-[1.5px] border-muted', className)} />
  if (kind === 'done')
    return <span className={cn(base, 'grid place-items-center bg-primary text-[9px] font-bold text-white', className)}>✓</span>
  // prog / review — partial conic fill in the accent colour.
  const pct = kind === 'review' ? 85 : 55
  return (
    <span
      className={cn(base, 'border-[1.5px] border-primary', className)}
      style={{ background: `conic-gradient(var(--color-primary) 0 ${pct}%, transparent 0)` }}
    />
  )
}

const PRIORITY_BARS: Record<string, number> = { High: 3, Medium: 2, Low: 1 }

/** Priority signal bars (▁▃▅) — filled to match priority; Urgent shows a red flag. */
function PriorityBars({ priority }: { priority: string }) {
  if (priority === 'Urgent')
    return <span className="grid h-3.5 w-3.5 flex-none place-items-center rounded bg-red-500 text-[9px] font-bold text-white">!</span>
  const filled = PRIORITY_BARS[priority] ?? 0
  const heights = ['h-1', 'h-[7px]', 'h-2.5']
  return (
    <span className="flex h-3 w-3.5 flex-none items-end gap-[2px]" aria-hidden>
      {heights.map((h, i) => (
        <span key={i} className={cn('w-[3px] rounded-[1px]', h, i < filled ? 'bg-muted' : 'bg-border')} />
      ))}
    </span>
  )
}

const deadlineTime = (t: MyTask) => (t.deadline ? Date.parse(t.deadline) : Number.POSITIVE_INFINITY)

/**
 * "My work" — every task assigned to the current user across their active projects, as a dense
 * list grouped by status (soonest deadline first within each group). Done tasks are hidden.
 * Clicking a task opens it on its board.
 */
export default function MyTasksPage() {
  const { t } = useTranslation()
  const [tasks, setTasks] = useState<MyTask[] | null>(null)

  useEffect(() => {
    taskService.getMine().then(setTasks).catch(() => setTasks([]))
  }, [])

  const active = (tasks ?? []).filter((task) => task.status !== 'Done')
  const now = Date.now()

  // Group by status, order the groups, and sort each group by soonest deadline.
  const byStatus = new Map<string, MyTask[]>()
  for (const task of active) {
    const list = byStatus.get(task.status) ?? []
    list.push(task)
    byStatus.set(task.status, list)
  }
  const groups = [...byStatus.entries()]
    .sort((a, b) => (STATUS_META[a[0]]?.rank ?? 9) - (STATUS_META[b[0]]?.rank ?? 9))
    .map(([status, list]) => [status, [...list].sort((a, b) => deadlineTime(a) - deadlineTime(b))] as const)

  return (
    <div className="mx-auto max-w-4xl">
      <div className="mb-4 flex items-end justify-between gap-4">
        <div>
          <h1 className="page-title">{t('myTasks.title')}</h1>
          <p className="page-subtitle">{t('myTasks.subtitle')}</p>
        </div>
        {active.length > 0 && (
          <span className="flex-none rounded-md border border-border bg-surface px-2.5 py-1 text-xs font-medium text-muted">
            {active.length}
          </span>
        )}
      </div>

      {tasks === null ? (
        <p className="text-sm text-muted">{t('common.loading', 'Loading…')}</p>
      ) : active.length === 0 ? (
        <EmptyState message={t('myTasks.empty')} />
      ) : (
        <div className="overflow-hidden rounded-[var(--radius-card)] border border-border bg-surface">
          {groups.map(([status, list]) => (
            <section key={status}>
              <div className="flex items-center gap-2 border-b border-border bg-canvas/60 px-3 py-1.5">
                <StatusIcon status={status} />
                <span className="text-[13px] font-semibold">{t(`board.status.${status}`, status)}</span>
                <span className="text-xs text-muted">{list.length}</span>
              </div>
              {list.map((task) => {
                const overdue = task.deadline && new Date(task.deadline).getTime() < now
                return (
                  <Link
                    key={task.id}
                    to={`/projects/${task.projectId}?task=${task.id}`}
                    className="flex h-[38px] items-center gap-2.5 border-b border-border/70 px-3 text-[13px] transition last:border-b-0 hover:bg-canvas"
                  >
                    <PriorityBars priority={task.priority} />
                    <StatusIcon status={task.status} />
                    <span className="min-w-0 flex-1 truncate">{task.title}</span>
                    <span className="hidden max-w-[160px] flex-none items-center gap-1.5 text-xs text-muted sm:flex">
                      <span
                        className="h-1.5 w-1.5 flex-none rounded-full"
                        style={{ background: task.projectColor ?? '#94a3b8' }}
                      />
                      <span className="truncate">{task.projectName}</span>
                    </span>
                    {task.deadline && (
                      <span
                        className={cn(
                          'w-16 flex-none text-right text-xs',
                          overdue ? 'font-semibold text-red-500' : 'text-muted',
                        )}
                      >
                        {new Date(task.deadline).toLocaleDateString()}
                      </span>
                    )}
                  </Link>
                )
              })}
            </section>
          ))}
        </div>
      )}
    </div>
  )
}
