import { useTranslation } from 'react-i18next'
import { Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'

// Stable color per moderation status.
const STATUS_COLORS: Record<string, string> = {
  Active: '#10B981', // green
  Muted: '#F59E0B', // amber
  Banned: '#EF4444', // red
}

/**
 * Donut chart of how many users are active vs muted vs banned (admin dashboard).
 */
export default function StatusChart({ usersByStatus }: { usersByStatus: Record<string, number> }) {
  const { t } = useTranslation()

  const data = Object.entries(usersByStatus ?? {})
    .filter(([, value]) => value > 0)
    .map(([status, value]) => ({
      name: t(`admin.status${status}`, status),
      value,
      fill: STATUS_COLORS[status] ?? '#94A3B8',
    }))

  return (
    <div className="rounded-xl border border-border bg-surface p-5 text-primary">
      <h2 className="mb-3 font-bold">{t('admin.usersByStatus')}</h2>
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
