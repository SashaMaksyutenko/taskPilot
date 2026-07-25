import { useTranslation } from 'react-i18next'
import { Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'

// Stable color per Kanban status.
const STATUS_COLORS: Record<string, string> = {
  Backlog: '#94A3B8', // gray
  InProgress: '#3B82F6', // blue
  Review: '#F59E0B', // amber
  Done: '#10B981', // green
}

/**
 * Donut chart of tasks across the platform grouped by Kanban status (admin analytics).
 */
export default function TasksStatusChart({ tasksByStatus }: { tasksByStatus: Record<string, number> }) {
  const { t } = useTranslation()

  const data = Object.entries(tasksByStatus ?? {})
    .filter(([, value]) => value > 0)
    .map(([status, value]) => ({
      name: t(`board.status.${status}`, status),
      value,
      fill: STATUS_COLORS[status] ?? '#94A3B8',
    }))

  return (
    <div className="rounded-xl border border-border bg-surface p-5 text-primary">
      <h2 className="mb-3 font-bold">{t('admin.tasksByStatus')}</h2>
      <div className="h-64">
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie data={data} dataKey="value" nameKey="name" innerRadius={60} outerRadius={90} paddingAngle={2} />
            <Tooltip />
            <Legend />
          </PieChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
