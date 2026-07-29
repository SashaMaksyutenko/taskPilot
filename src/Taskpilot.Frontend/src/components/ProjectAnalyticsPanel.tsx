import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, Pie, PieChart,
  ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import { projectService } from '../services/projectService'
import type { ProjectAnalytics } from '../types/project'

const STATUS_COLORS: Record<string, string> = {
  Backlog: '#94A3B8', InProgress: '#3B82F6', Review: '#F59E0B', Done: '#10B981',
}
const PRIORITY_COLORS: Record<string, string> = { High: '#EF4444', Medium: '#F59E0B', Low: '#94A3B8' }
const PRIORITY_ORDER = ['High', 'Medium', 'Low']

/** A single headline metric. */
function StatTile({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-xl border border-border bg-surface p-5">
      <div className="text-2xl font-bold tabular-nums">{value}</div>
      <div className="mt-1 text-sm text-muted">{label}</div>
      {hint && <div className="mt-0.5 text-xs text-muted">{hint}</div>}
    </div>
  )
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-border bg-surface p-5 text-primary">
      <h3 className="mb-3 font-bold">{title}</h3>
      <div className="h-64">
        <ResponsiveContainer width="100%" height="100%">{children as React.ReactElement}</ResponsiveContainer>
      </div>
    </div>
  )
}

/**
 * Delivery analytics for a project board: headline tiles plus a burn-up trend, status/priority
 * mix and per-assignee workload. Read-only; any member sees it.
 */
export default function ProjectAnalyticsPanel({ projectId }: { projectId: string }) {
  const { t } = useTranslation()
  const [data, setData] = useState<ProjectAnalytics | null>(null)

  useEffect(() => {
    projectService.getAnalytics(projectId).then(setData).catch(() => {})
  }, [projectId])

  if (!data) return <p className="p-6 text-sm text-muted">{t('analytics.loading')}</p>

  const weeks = data.weeks.map((w) => ({
    label: new Date(w.weekStart).toLocaleDateString(undefined, { day: '2-digit', month: '2-digit' }),
    created: w.created,
    completed: w.completed,
  }))
  const statusData = Object.entries(data.byStatus)
    .filter(([, v]) => v > 0)
    .map(([status, value]) => ({ name: t(`board.status.${status}`, status), value, fill: STATUS_COLORS[status] ?? '#94A3B8' }))
  const priorityData = PRIORITY_ORDER.map((p) => ({ name: t(`board.priority.${p}`, p), value: data.byPriority[p] ?? 0, key: p }))
  const workload = data.byAssignee.slice(0, 8).map((a) => ({
    name: a.name === 'Unassigned' ? t('analytics.unassigned') : a.name,
    open: a.open,
    done: a.done,
  }))
  const delta = data.throughputThisWeek - data.throughputPrevWeek

  return (
    <div className="space-y-4">
      {/* Headline tiles */}
      <div className="grid gap-4 sm:grid-cols-3">
        <StatTile label={t('analytics.totalTasks')} value={String(data.totalTasks)} />
        <StatTile
          label={t('analytics.avgCycleTime')}
          value={data.avgCycleTimeDays != null ? t('analytics.days', { count: data.avgCycleTimeDays }) : '—'}
        />
        <StatTile
          label={t('analytics.throughput')}
          value={String(data.throughputThisWeek)}
          hint={delta === 0 ? undefined : t('analytics.vsPrev', { delta: delta > 0 ? `+${delta}` : String(delta) })}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        {/* Burn-up trend — full width */}
        <div className="lg:col-span-2">
          <Card title={t('analytics.burnUp')}>
            <LineChart data={weeks} margin={{ top: 5, right: 5, left: -20, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#94A3B833" />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} />
              <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
              <Tooltip />
              <Legend />
              <Line type="monotone" dataKey="created" name={t('analytics.created')} stroke="#3B82F6" strokeWidth={2} dot={false} />
              <Line type="monotone" dataKey="completed" name={t('analytics.completed')} stroke="#10B981" strokeWidth={2} dot={false} />
            </LineChart>
          </Card>
        </div>

        {/* Status donut */}
        <Card title={t('analytics.byStatus')}>
          <PieChart>
            <Pie data={statusData} dataKey="value" nameKey="name" innerRadius={55} outerRadius={85} paddingAngle={2} />
            <Tooltip />
            <Legend />
          </PieChart>
        </Card>

        {/* Priority bars */}
        <Card title={t('analytics.byPriority')}>
          <BarChart data={priorityData} margin={{ top: 5, right: 5, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#94A3B833" />
            <XAxis dataKey="name" tick={{ fontSize: 11 }} />
            <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
            <Tooltip />
            <Bar dataKey="value" radius={[4, 4, 0, 0]}>
              {priorityData.map((p) => <Cell key={p.key} fill={PRIORITY_COLORS[p.key] ?? '#94A3B8'} />)}
            </Bar>
          </BarChart>
        </Card>

        {/* Workload per assignee — full width */}
        {workload.length > 0 && (
          <div className="lg:col-span-2">
            <Card title={t('analytics.workload')}>
              <BarChart data={workload} margin={{ top: 5, right: 5, left: -20, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#94A3B833" />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} allowDecimals={false} />
                <Tooltip />
                <Legend />
                <Bar dataKey="open" name={t('analytics.open')} stackId="w" fill="#3B82F6" radius={[0, 0, 0, 0]} />
                <Bar dataKey="done" name={t('analytics.done')} stackId="w" fill="#10B981" radius={[4, 4, 0, 0]} />
              </BarChart>
            </Card>
          </div>
        )}
      </div>
    </div>
  )
}
