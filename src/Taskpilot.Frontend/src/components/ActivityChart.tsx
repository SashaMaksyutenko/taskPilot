import { useTranslation } from 'react-i18next'
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { DayActivity } from '../types/stats'

/**
 * Line chart of content created per day (forum topics and tasks) over the period.
 */
export default function ActivityChart({ activity }: { activity: DayActivity[] }) {
  const { t } = useTranslation()

  const data = (activity ?? []).map((d) => ({
    label: new Date(d.day).toLocaleDateString(undefined, { day: '2-digit', month: '2-digit' }),
    topics: d.topics,
    tasks: d.tasks,
  }))
  const tickGap = data.length > 45 ? 13 : data.length > 20 ? 4 : 1

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5 text-primary dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
      <h2 className="mb-3 font-bold">{t('admin.contentTrend')}</h2>
      <div className="h-64">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data} margin={{ top: 5, right: 5, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#94A3B833" />
            <XAxis dataKey="label" tick={{ fontSize: 11 }} interval={tickGap} />
            <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
            <Tooltip />
            <Legend />
            <Line type="monotone" dataKey="topics" name={t('admin.metricTopics')} stroke="#10B981" strokeWidth={2} dot={false} />
            <Line type="monotone" dataKey="tasks" name={t('admin.metricTasks')} stroke="#F59E0B" strokeWidth={2} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
