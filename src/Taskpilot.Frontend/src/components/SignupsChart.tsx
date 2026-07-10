import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { DayActivity } from '../types/stats'

/**
 * Bar chart of new user registrations per day over the selected period.
 */
export default function SignupsChart({ activity }: { activity: DayActivity[] }) {
  const { t } = useTranslation()

  // Short "DD.MM" label per day; hide labels on long ranges for readability.
  const data = (activity ?? []).map((d) => ({
    label: new Date(d.day).toLocaleDateString(undefined, { day: '2-digit', month: '2-digit' }),
    count: d.signups,
  }))
  const tickGap = data.length > 45 ? 13 : data.length > 20 ? 4 : 1

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5 text-primary dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
      <h2 className="mb-3 font-bold">{t('admin.signupsTrend')}</h2>
      <div className="h-64">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} margin={{ top: 5, right: 5, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#94A3B833" />
            <XAxis dataKey="label" tick={{ fontSize: 11 }} interval={tickGap} />
            <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
            <Tooltip />
            <Bar dataKey="count" fill="#4F46E5" radius={[3, 3, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
