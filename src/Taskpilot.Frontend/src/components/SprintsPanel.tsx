import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { apiErrorMessage } from '../lib/apiError'
import { sprintService } from '../services/sprintService'
import type { Sprint } from '../types/project'

const STATUSES = ['Planned', 'Active', 'Completed']

function StatusBadge({ status }: { status: string }) {
  const cls =
    status === 'Active' ? 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300'
      : status === 'Completed' ? 'bg-green-100 text-green-700 dark:bg-green-950/40 dark:text-green-300'
        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300'
  const { t } = useTranslation()
  return <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${cls}`}>{t(`sprints.status.${status}`, status)}</span>
}

/**
 * Sprints/iterations view for a project: create sprints, track each one's progress (done/total),
 * change its status and delete it. Tasks are moved in/out from the task modal's sprint picker.
 */
export default function SprintsPanel({ projectId, canWrite }: { projectId: string; canWrite: boolean }) {
  const { t } = useTranslation()
  const [sprints, setSprints] = useState<Sprint[]>([])
  const [name, setName] = useState('')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [error, setError] = useState('')

  const load = () => sprintService.list(projectId).then(setSprints).catch(() => {})
  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId])

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
  }

  const fmt = (d: string | null) => (d ? new Date(d).toLocaleDateString() : null)

  return (
    <div className="mx-auto max-w-3xl space-y-4">
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
