import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { statsService } from '../../services/statsService'
import type { PublicStats } from '../../types/stats'

/**
 * Compact site-statistics strip shown at the bottom of every signed-in page (the numbers
 * from the stats panel, without the charts). Lives in AppShell, so it never appears on the
 * public login/register pages.
 */
export default function StatsFooter() {
  const { t } = useTranslation()
  const [stats, setStats] = useState<PublicStats | null>(null)

  useEffect(() => {
    let active = true
    const load = () => statsService.getPublic().then((s) => active && setStats(s)).catch(() => {})
    load()
    // Refresh occasionally so the "online" figure stays roughly current.
    const id = setInterval(load, 60_000)
    return () => {
      active = false
      clearInterval(id)
    }
  }, [])

  if (!stats) return null

  const items = [
    { value: stats.totalUsers, label: t('stats.users') },
    { value: stats.totalTopics, label: t('stats.topics') },
    { value: stats.totalForumPosts, label: t('stats.posts') },
    { value: stats.onlineUsers, label: t('stats.online') },
  ]

  return (
    <footer className="border-t border-border px-4 py-3 sm:px-6 lg:px-8">
      <div className="flex flex-wrap items-center justify-center gap-x-6 gap-y-1 text-xs text-muted">
        {items.map((it) => (
          <span key={it.label}>
            <span className="font-semibold tabular-nums text-foreground">{it.value}</span> {it.label}
          </span>
        ))}
        <Link to="/pricing" className="font-semibold text-primary hover:underline">
          {t('pricing.nav')}
        </Link>
      </div>
    </footer>
  )
}
