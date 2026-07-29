import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../lib/apiError'
import { taskService } from '../services/taskService'
import type { Task, TaskDependencies } from '../types/project'

/** Coloured dot for a task's Kanban status. */
function StatusDot({ status }: { status: string }) {
  const color =
    status === 'Done' ? 'bg-green-500'
      : status === 'Review' ? 'bg-amber-500'
        : status === 'InProgress' ? 'bg-blue-500'
          : 'bg-slate-400'
  return <span className={`h-2 w-2 flex-none rounded-full ${color}`} />
}

/**
 * The task's dependency graph inside the task modal: what it's blocked by (with a "Blocked"
 * badge while any blocker is unfinished) and what it blocks. Editors/owner can add or remove
 * blockers; the backend rejects self/cross-project/duplicate/cyclic dependencies.
 */
export default function TaskDependenciesSection({
  taskId,
  projectId,
  canWrite,
}: {
  taskId: string
  projectId: string
  canWrite: boolean
}) {
  const { t } = useTranslation()
  const [graph, setGraph] = useState<TaskDependencies | null>(null)
  const [projectTasks, setProjectTasks] = useState<Task[]>([])
  const [error, setError] = useState('')

  const load = () => taskService.getDependencies(taskId).then(setGraph).catch(() => {})
  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [taskId])
  useEffect(() => {
    if (canWrite) taskService.getTasks(projectId).then(setProjectTasks).catch(() => {})
  }, [projectId, canWrite])

  const add = async (dependsOnId: string) => {
    setError('')
    try {
      setGraph(await taskService.addDependency(taskId, dependsOnId))
    } catch (e) {
      setError(apiErrorMessage(e))
    }
  }

  const remove = async (dependsOnId: string) => {
    await taskService.removeDependency(taskId, dependsOnId).catch(() => {})
    load()
  }

  if (!graph) return null
  const existing = new Set([taskId, ...graph.dependsOn.map((d) => d.id)])
  const candidates = projectTasks.filter((pt) => !existing.has(pt.id))

  return (
    <div>
      <div className="mb-2 flex items-center gap-2">
        <h3 className="font-bold">{t('deps.title')}</h3>
        {graph.isBlocked && (
          <span className="rounded-full bg-red-100 px-2 py-0.5 text-[11px] font-semibold text-red-700 dark:bg-red-950/40 dark:text-red-300">
            {t('deps.blocked')}
          </span>
        )}
      </div>

      {/* Blocked by (this task's dependencies) */}
      <p className="mb-1 text-xs font-medium text-muted">{t('deps.blockedBy')}</p>
      {graph.dependsOn.length === 0 ? (
        <p className="mb-2 text-sm text-muted">{t('deps.none')}</p>
      ) : (
        <ul className="mb-2 space-y-1">
          {graph.dependsOn.map((d) => (
            <li key={d.id} className="flex items-center gap-2 text-sm">
              <StatusDot status={d.status} />
              <span className={`min-w-0 flex-1 truncate ${d.status === 'Done' ? 'text-muted line-through' : ''}`}>{d.title}</span>
              {canWrite && (
                <button
                  type="button"
                  onClick={() => remove(d.id)}
                  className="flex-none text-xs text-red-600 hover:underline"
                  aria-label={t('deps.remove')}
                >
                  ✕
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {canWrite && candidates.length > 0 && (
        <select
          value=""
          onChange={(e) => { if (e.target.value) add(e.target.value) }}
          className="mb-1 w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary"
        >
          <option value="">{t('deps.addBlocker')}</option>
          {candidates.map((pt) => (
            <option key={pt.id} value={pt.id}>{pt.title}</option>
          ))}
        </select>
      )}
      {error && <p className="mb-1 text-sm text-red-600 dark:text-red-400">{error}</p>}

      {/* Blocks (tasks that depend on this one) */}
      {graph.blocks.length > 0 && (
        <>
          <p className="mb-1 mt-3 text-xs font-medium text-muted">{t('deps.blocksLabel')}</p>
          <ul className="space-y-1">
            {graph.blocks.map((b) => (
              <li key={b.id} className="flex items-center gap-2 text-sm">
                <StatusDot status={b.status} />
                <span className="min-w-0 flex-1 truncate">{b.title}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
