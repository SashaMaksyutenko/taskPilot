import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { projectService } from '../services/projectService'
import type { PublicBoard } from '../types/project'

const COLUMNS = ['Backlog', 'InProgress', 'Review', 'Done']

function PriorityBadge({ priority }: { priority: string }) {
  const cls =
    priority === 'High' ? 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300'
      : priority === 'Low' ? 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
        : 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300'
  return <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${cls}`}>{priority}</span>
}

/**
 * Public, read-only Kanban board reached via a share token — no login. Renders the shared
 * project's tasks grouped into the four columns.
 */
export default function PublicBoardPage() {
  const { t } = useTranslation()
  const { token = '' } = useParams()
  const [board, setBoard] = useState<PublicBoard | null>(null)
  const [notFound, setNotFound] = useState(false)

  useEffect(() => {
    projectService.getPublicBoard(token).then(setBoard).catch(() => setNotFound(true))
  }, [token])

  if (notFound) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-canvas px-6 text-center">
        <p className="text-lg font-semibold">{t('publicBoard.notFound')}</p>
        <Link to="/" className="text-sm font-semibold text-primary hover:underline">TaskPilot →</Link>
      </div>
    )
  }
  if (!board) return <div className="min-h-screen bg-canvas" />

  return (
    <div className="min-h-screen bg-canvas">
      <header className="flex items-center gap-3 border-b border-border bg-surface px-6 py-4">
        <span className="inline-block h-4 w-4 flex-none rounded" style={{ background: board.color ?? '#4F46E5' }} />
        <h1 className="min-w-0 flex-1 truncate text-xl font-bold">{board.name}</h1>
        <span className="hidden text-xs text-muted sm:inline">{t('publicBoard.readOnly')}</span>
        <Link to="/" className="flex items-center gap-2 text-sm font-semibold text-primary hover:underline">
          <img src="/logo-mark.svg" alt="" className="h-6 w-6" />
          TaskPilot
        </Link>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6">
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {COLUMNS.map((status) => {
            const tasks = board.tasks.filter((task) => task.status === status)
            return (
              <div key={status} className="rounded-xl border border-border bg-surface p-3">
                <div className="mb-3 flex items-center justify-between px-1">
                  <h2 className="text-sm font-bold">{t(`board.status.${status}`, status)}</h2>
                  <span className="text-xs font-semibold text-muted tabular-nums">{tasks.length}</span>
                </div>
                <div className="space-y-2">
                  {tasks.map((task, i) => (
                    <div key={i} className="rounded-lg border border-border bg-canvas p-3 shadow-soft">
                      <div className="mb-1.5 flex items-start justify-between gap-2">
                        <span className="min-w-0 text-sm font-medium">{task.title}</span>
                        <PriorityBadge priority={task.priority} />
                      </div>
                      {task.tags.length > 0 && (
                        <div className="mb-1.5 flex flex-wrap gap-1">
                          {task.tags.map((tag) => (
                            <span key={tag} className="rounded bg-primary/10 px-1.5 py-0.5 text-[10px] font-medium text-primary">{tag}</span>
                          ))}
                        </div>
                      )}
                      <div className="flex items-center justify-between text-[11px] text-muted">
                        <span className="truncate">{task.assigneeName ?? ''}</span>
                        {task.deadline && <span className="flex-none">{new Date(task.deadline).toLocaleDateString()}</span>}
                      </div>
                    </div>
                  ))}
                  {tasks.length === 0 && <p className="px-1 py-2 text-xs text-muted">—</p>}
                </div>
              </div>
            )
          })}
        </div>
      </main>
    </div>
  )
}
