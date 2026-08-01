import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { apiErrorMessage } from '../lib/apiError'
import { useDragAndDrop } from '../hooks/useDragAndDrop'
import { sprintService } from '../services/sprintService'
import { taskService } from '../services/taskService'
import type { Sprint, Task } from '../types/project'

const STATUSES = ['Planned', 'Active', 'Completed']
const BACKLOG = 'backlog'

function StatusBadge({ status }: { status: string }) {
  const cls =
    status === 'Active' ? 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300'
      : status === 'Completed' ? 'bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-300'
        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
  const { t } = useTranslation()
  return <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${cls}`}>{t(`sprints.status.${status}`, status)}</span>
}

function StatusDot({ status }: { status: string }) {
  const color =
    status === 'Done' ? 'bg-green-500'
      : status === 'Review' ? 'bg-amber-500'
        : status === 'InProgress' ? 'bg-blue-500'
          : 'bg-slate-400'
  return <span className={`h-2 w-2 flex-none rounded-full ${color}`} />
}

/**
 * Sprints/iterations view for a project: create sprints, track each one's progress (done/total)
 * and velocity, and **plan by dragging tasks** between the backlog and each sprint. Assigning
 * reuses the task→sprint endpoint; the task modal's sprint picker still works too.
 */
export default function SprintsPanel({ projectId, canWrite }: { projectId: string; canWrite: boolean }) {
  const { t } = useTranslation()
  const [sprints, setSprints] = useState<Sprint[]>([])
  const [tasks, setTasks] = useState<Task[]>([])
  const [name, setName] = useState('')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [error, setError] = useState('')

  const load = () => sprintService.list(projectId).then(setSprints).catch(() => {})
  const loadTasks = () => taskService.getTasks(projectId).then(setTasks).catch(() => {})
  useEffect(() => {
    load()
    loadTasks()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId])

  // Move a task into a sprint (or the backlog); optimistic, then refresh the sprint tallies.
  const dnd = useDragAndDrop({
    onDrop: (zoneKey, taskId) => {
      const target = zoneKey === BACKLOG ? null : zoneKey
      const task = tasks.find((t) => t.id === taskId)
      if (!task || (task.sprintId ?? null) === target) return
      setTasks((prev) => prev.map((x) => (x.id === taskId ? { ...x, sprintId: target } : x)))
      sprintService
        .assignTask(taskId, target)
        .then(load)
        .catch(() => loadTasks())
    },
    renderGhost: (id) => {
      const task = tasks.find((x) => x.id === id)
      return task ? (
        <div className="rounded-lg border border-primary bg-surface px-2.5 py-1.5 text-xs font-medium shadow-card">
          {task.title}
        </div>
      ) : null
    },
  })

  const create = async () => {
    if (!name.trim()) return
    setError('')
    try {
      await sprintService.create(projectId, {
        name: name.trim(),
        startDate: start ? `${start}T00:00:00Z` : null,
        endDate: end ? `${end}T23:59:59Z` : null,
      })
      setName('')
      setStart('')
      setEnd('')
      load()
    } catch (e) {
      setError(apiErrorMessage(e))
    }
  }

  const setStatus = async (s: Sprint, status: string) => {
    await sprintService
      .update(s.id, { name: s.name, goal: s.goal, startDate: s.startDate, endDate: s.endDate, status })
      .catch(() => {})
    load()
  }

  const remove = async (id: string) => {
    await sprintService.remove(id).catch(() => {})
    load()
    loadTasks()
  }

  const fmt = (d: string | null) => (d ? new Date(d).toLocaleDateString() : null)

  // Tasks to show in a drop zone: a sprint's tasks, or the not-done backlog for the null zone.
  const tasksIn = (sprintId: string | null) =>
    sprintId === null
      ? tasks.filter((x) => !x.sprintId && x.status !== 'Done')
      : tasks.filter((x) => x.sprintId === sprintId)

  const TaskList = ({ sprintId }: { sprintId: string | null }) => {
    const zoneKey = sprintId ?? BACKLOG
    const list = tasksIn(sprintId)
    return (
      <div
        {...dnd.dropZoneProps(zoneKey)}
        className={`mt-2 min-h-[2.5rem] space-y-1.5 rounded-lg border border-dashed p-1.5 transition-colors ${
          dnd.activeZone === zoneKey ? 'border-primary bg-primary/5' : 'border-border'
        }`}
      >
        {list.length === 0 ? (
          <p className="px-1.5 py-1 text-xs text-muted">{t('board.dropHere')}</p>
        ) : (
          list.map((task) => (
            <div
              key={task.id}
              {...(canWrite ? dnd.draggableProps(task.id) : {})}
              className="flex items-center gap-2 rounded-lg border border-border bg-canvas px-2.5 py-1.5 text-xs"
            >
              <StatusDot status={task.status} />
              <span className="min-w-0 flex-1 truncate">{task.title}</span>
            </div>
          ))
        )}
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      {dnd.overlay}
      {canWrite && (
        <div className="rounded-xl border border-border bg-surface p-4">
          <h3 className="mb-3 font-semibold">{t('sprints.new')}</h3>
          <div className="flex flex-wrap items-end gap-2">
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('sprints.namePlaceholder')}
              className="min-w-[12rem] flex-1 rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary"
            />
            <input type="date" value={start} onChange={(e) => setStart(e.target.value)} className="rounded-lg border border-border bg-canvas px-2 py-2 text-sm outline-none" />
            <input type="date" value={end} onChange={(e) => setEnd(e.target.value)} className="rounded-lg border border-border bg-canvas px-2 py-2 text-sm outline-none" />
            <button
              onClick={create}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover"
            >
              {t('sprints.add')}
            </button>
          </div>
          {error && <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>}
        </div>
      )}

      {sprints.length === 0 ? (
        <p className="text-sm text-muted">{t('sprints.empty')}</p>
      ) : (
        <div className="space-y-3">
          {/* Backlog — unassigned, not-done tasks available to pull into a sprint. */}
          <div className="rounded-xl border border-border bg-surface p-4">
            <div className="flex items-center gap-2">
              <span className="font-semibold">{t('sprints.backlog')}</span>
              <span className="text-xs text-muted">{tasksIn(null).length}</span>
            </div>
            <TaskList sprintId={null} />
          </div>

          {sprints.map((s) => {
            const pct = s.taskCount ? Math.round((s.doneCount / s.taskCount) * 100) : 0
            const range = [fmt(s.startDate), fmt(s.endDate)].filter(Boolean).join(' – ')
            return (
              <div key={s.id} className="rounded-xl border border-border bg-surface p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-semibold">{s.name}</span>
                  <StatusBadge status={s.status} />
                  {range && <span className="text-xs text-muted">{range}</span>}
                  <div className="ml-auto flex items-center gap-2">
                    {canWrite && (
                      <select
                        value={s.status}
                        onChange={(e) => setStatus(s, e.target.value)}
                        className="rounded border border-border bg-canvas px-1.5 py-0.5 text-xs outline-none"
                      >
                        {STATUSES.map((st) => <option key={st} value={st}>{t(`sprints.status.${st}`, st)}</option>)}
                      </select>
                    )}
                    {canWrite && (
                      <button onClick={() => remove(s.id)} className="text-xs font-semibold text-red-600 hover:underline">
                        {t('sprints.delete')}
                      </button>
                    )}
                  </div>
                </div>
                {s.goal && <p className="mt-1 text-sm text-muted">{s.goal}</p>}
                <div className="mt-3 flex items-center gap-3">
                  <div className="h-2 flex-1 overflow-hidden rounded-full bg-border">
                    <div className="h-full rounded-full bg-primary transition-all" style={{ width: `${pct}%` }} />
                  </div>
                  <span className="flex-none text-xs text-muted tabular-nums">
                    {s.doneCount}/{s.taskCount}
                    {s.plannedPoints > 0 && <span> · {t('sprints.points', { done: s.completedPoints, total: s.plannedPoints })}</span>}
                  </span>
                </div>
                <TaskList sprintId={s.id} />
              </div>
            )
          })}
        </div>
      )}

      {/* Velocity — committed vs completed story points per sprint */}
      {sprints.some((s) => s.plannedPoints > 0) && (
        <div className="rounded-xl border border-border bg-surface p-5 text-primary">
          <h3 className="mb-3 font-bold">{t('sprints.velocity')}</h3>
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={sprints.map((s) => ({ name: s.name, planned: s.plannedPoints, completed: s.completedPoints }))}
                margin={{ top: 5, right: 5, left: -20, bottom: 0 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#94A3B833" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <Tooltip />
                <Legend />
                <Bar dataKey="planned" name={t('sprints.planned')} fill="#94A3B8" radius={[4, 4, 0, 0]} />
                <Bar dataKey="completed" name={t('sprints.completed')} fill="#10B981" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}
    </div>
  )
}
