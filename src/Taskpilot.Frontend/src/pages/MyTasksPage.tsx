import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import EmptyState from '../components/feedback/EmptyState'
import { taskService } from '../services/taskService'
import type { MyTask } from '../types/project'

function StatusDot({ status }: { status: string }) {
  const color =
    status === 'Done' ? 'bg-green-500'
      : status === 'Review' ? 'bg-amber-500'
        : status === 'InProgress' ? 'bg-blue-500'
          : 'bg-slate-400'
  return <span className={`h-2.5 w-2.5 flex-none rounded-full ${color}`} />
}

function PriorityBadge({ priority }: { priority: string }) {
  const cls =
    priority === 'High' ? 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300'
      : priority === 'Low' ? 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
        : 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300'
  const { t } = useTranslation()
  return <span className={`flex-none rounded-full px-2 py-0.5 text-[10px] font-semibold ${cls}`}>{t(`board.priority.${priority}`, priority)}</span>
}

/**
 * "My work" — every task assigned to the current user across all their active projects, in one
 * list (soonest deadline first). Done tasks are hidden. Clicking a task opens it on its board.
 */
export default function MyTasksPage() {
  const { t } = useTranslation()
  const [tasks, setTasks] = useState<MyTask[] | null>(null)

  useEffect(() => {
    taskService.getMine().then(setTasks).catch(() => setTasks([]))
  }, [])

  const active = (tasks ?? []).filter((task) => task.status !== 'Done')
  const now = Date.now()

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <h1 className="mb-1 text-2xl font-bold">{t('myTasks.title')}</h1>
      <p className="mb-6 text-sm text-muted">{t('myTasks.subtitle')}</p>

      {tasks === null ? (
        <p className="text-sm text-muted">{t('common.loading', 'Loading…')}</p>
      ) : active.length === 0 ? (
        <EmptyState message={t('myTasks.empty')} />
      ) : (
        <ul className="space-y-2">
          {active.map((task) => {
            const overdue = task.deadline && new Date(task.deadline).getTime() < now
            return (
              <li key={task.id}>
                <Link
                  to={`/projects/${task.projectId}?task=${task.id}`}
                  className="flex items-center gap-3 rounded-lg border border-border bg-surface p-3 transition hover:bg-canvas"
                >
                  <StatusDot status={task.status} />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{task.title}</span>
                    <span className="mt-0.5 flex items-center gap-1.5 text-xs text-muted">
                      <span className="inline-block h-2 w-2 flex-none rounded-full" style={{ background: task.projectColor ?? '#94a3b8' }} />
                      <span className="truncate">{task.projectName}</span>
                      <span>· {t(`board.status.${task.status}`, task.status)}</span>
                    </span>
                  </span>
                  <PriorityBadge priority={task.priority} />
                  {task.deadline && (
                    <span className={`flex-none text-xs ${overdue ? 'font-semibold text-red-600 dark:text-red-400' : 'text-muted'}`}>
                      {new Date(task.deadline).toLocaleDateString()}
                    </span>
                  )}
                </Link>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
