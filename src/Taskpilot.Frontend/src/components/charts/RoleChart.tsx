import { useTranslation } from 'react-i18next'
import { Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'

// Stable color per role.
const ROLE_COLORS: Record<string, string> = {
  Developer: '#4F46E5', // indigo
  Manager: '#10B981', // green
  Admin: '#F59E0B', // amber
  Viewer: '#94A3B8', // slate
}

/**
 * Donut chart of how many users have each role (admin dashboard).
 */
export default function RoleChart({ usersByRole }: { usersByRole: Record<string, number> }) {
  const { t } = useTranslation()

  // Build chart data, translating role names and assigning a per-slice color via
  // the `fill` field (the modern Recharts way — the deprecated <Cell> is avoided).
  const data = Object.entries(usersByRole).map(([role, value]) => ({
    name: t(`admin.roles.${role}`, role),
    value,
    fill: ROLE_COLORS[role] ?? '#94A3B8',
  }))

  return (
    <div className="rounded-xl border border-border bg-surface p-5 text-primary">
      <h2 className="mb-3 font-bold">{t('admin.usersByRole')}</h2>
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
