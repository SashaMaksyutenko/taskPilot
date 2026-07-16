import { useTranslation } from 'react-i18next'
import Card from '../ui/Card'
import type { AdminStats, PublicStats } from '../../types/stats'

function Stat({ value, label }: { value: string | number; label: string }) {
  return (
    <div>
      <div className="text-2xl font-bold tabular-nums text-foreground">{value}</div>
      <div className="text-xs font-medium uppercase tracking-wide text-muted">{label}</div>
    </div>
  )
}

function isAdmin(stats: PublicStats | AdminStats): stats is AdminStats {
  return 'activeUsers' in stats
}

/** Live statistics panel for landing and admin pages. */
export default function StatsPanel({ stats }: { stats: PublicStats | AdminStats | null }) {
  const { t } = useTranslation()
  if (!stats) return null
  const admin = isAdmin(stats)

  return (
    <Card className="p-6">
      <h2 className="mb-5 font-bold">{t('stats.title')}</h2>

      <div className="grid grid-cols-2 gap-6 sm:grid-cols-4">
        <Stat value={stats.totalUsers} label={t('stats.users')} />
        <Stat value={stats.totalTopics} label={t('stats.topics')} />
        <Stat value={stats.totalForumPosts} label={t('stats.posts')} />
        <Stat value={stats.onlineUsers} label={t('stats.online')} />
      </div>

      {stats.newestUserName && (
        <p className="mt-5 text-sm text-muted">
          {t('stats.newestMember')}{' '}
          <span className="font-semibold text-foreground">{stats.newestUserName}</span>
        </p>
      )}

      <div className="mt-5 border-t border-border pt-5">
        <div className="text-sm font-semibold">
          {t('stats.onlineNow', { count: stats.onlineUsers })}
          {admin && (
            <span className="font-normal text-muted">
              {' '}
              · {t('stats.anonymousToday', { count: stats.anonymousVisitorsToday })}
            </span>
          )}
        </div>
        {stats.onlineUserNames.length > 0 ? (
          <div className="mt-2 flex flex-wrap gap-x-2 gap-y-1 text-sm text-emerald-600 dark:text-emerald-400">
            {stats.onlineUserNames.map((name) => (
              <span key={name}>{name}</span>
            ))}
          </div>
        ) : (
          <p className="mt-2 text-sm text-muted">{t('stats.noneOnline')}</p>
        )}
      </div>

      {admin && (
        <div className="mt-5 grid grid-cols-2 gap-6 border-t border-border pt-5 sm:grid-cols-3">
          <Stat value={stats.activeUsers} label={t('stats.activeUsers')} />
          <Stat value={stats.anonymousVisitorsToday} label={t('stats.visitorsToday')} />
          <Stat value={stats.anonymousVisitsTotal} label={t('stats.anonRequests')} />
        </div>
      )}
    </Card>
  )
}
