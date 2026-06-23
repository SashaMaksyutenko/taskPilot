import { useTranslation } from 'react-i18next'
import type { AdminStats, PublicStats } from '../types/stats'

/** Renders one labelled statistic. */
function Stat({ value, label }: { value: string | number; label: string }) {
  return (
    <div>
      <div className="text-2xl font-bold">{value}</div>
      <div className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{label}</div>
    </div>
  )
}

/** Type guard: are these the admin (full) stats? */
function isAdmin(stats: PublicStats | AdminStats): stats is AdminStats {
  return 'activeUsers' in stats
}

/**
 * Forum-style statistics panel. Shows totals, the newest user and who is online.
 * Works with public stats or the richer admin stats (extra analytics shown only
 * when present).
 */
export default function StatsPanel({ stats }: { stats: PublicStats | AdminStats | null }) {
  const { t } = useTranslation()
  if (!stats) return null
  const admin = isAdmin(stats)

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5 text-[#1E2A44] dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
      <h2 className="mb-4 font-bold">{t('stats.title')}</h2>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Stat value={stats.totalUsers} label={t('stats.users')} />
        <Stat value={stats.totalTopics} label={t('stats.topics')} />
        <Stat value={stats.totalForumPosts} label={t('stats.posts')} />
        <Stat value={stats.onlineUsers} label={t('stats.online')} />
      </div>

      {stats.newestUserName && (
        <p className="mt-4 text-sm text-slate-500 dark:text-slate-400">
          {t('stats.newestMember')} <span className="font-semibold text-[#1E2A44] dark:text-slate-200">{stats.newestUserName}</span>
        </p>
      )}

      {/* Who is online */}
      <div className="mt-4 border-t border-slate-100 pt-4 dark:border-slate-700">
        <div className="text-sm font-semibold">
          {t('stats.onlineNow', { count: stats.onlineUsers })}
          {admin && <span className="font-normal text-slate-500 dark:text-slate-400"> · {t('stats.anonymousToday', { count: stats.anonymousVisitorsToday })}</span>}
        </div>
        {stats.onlineUserNames.length > 0 ? (
          <div className="mt-1 flex flex-wrap gap-x-2 gap-y-1 text-sm text-green-600 dark:text-green-400">
            {stats.onlineUserNames.map((name) => (
              <span key={name}>{name}</span>
            ))}
          </div>
        ) : (
          <p className="mt-1 text-sm text-slate-400">{t('stats.noneOnline')}</p>
        )}
      </div>

      {/* Admin-only analytics */}
      {admin && (
        <div className="mt-4 grid grid-cols-2 gap-4 border-t border-slate-100 pt-4 sm:grid-cols-3 dark:border-slate-700">
          <Stat value={stats.activeUsers} label={t('stats.activeUsers')} />
          <Stat value={stats.anonymousVisitorsToday} label={t('stats.visitorsToday')} />
          <Stat value={stats.anonymousVisitsTotal} label={t('stats.anonRequests')} />
        </div>
      )}
    </section>
  )
}
