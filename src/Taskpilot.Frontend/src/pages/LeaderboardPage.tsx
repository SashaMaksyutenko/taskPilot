import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Avatar from '../components/Avatar'
import EmptyState from '../components/feedback/EmptyState'
import { leaderboardService, type Leaderboard, type LeaderboardEntry } from '../services/leaderboardService'

/** Medal for the top three, otherwise the numeric rank. */
function Rank({ rank }: { rank: number }) {
  const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : null
  return (
    <span className="w-8 flex-none text-center text-sm font-bold tabular-nums">
      {medal ?? <span className="text-muted">#{rank}</span>}
    </span>
  )
}

function Row({ entry, isMe }: { entry: LeaderboardEntry; isMe: boolean }) {
  const { t } = useTranslation()
  return (
    <Link
      to={`/users/${entry.userId}`}
      className={`flex items-center gap-3 px-3 py-2.5 transition ${
        isMe ? 'border-l-2 border-primary bg-primary/5' : 'hover:bg-canvas'
      }`}
    >
      <Rank rank={entry.rank} />
      <Avatar name={entry.name} src={entry.avatarUrl} size={32} />
      <span className="min-w-0 flex-1 truncate font-medium">{entry.name}</span>
      <span className="flex-none text-xs text-muted">{t('leaderboard.tasks', { count: entry.tasksCompleted })}</span>
      <span className="flex-none w-16 text-right font-bold text-primary tabular-nums">{entry.score}</span>
    </Link>
  )
}

/** Community leaderboard ranked by reputation points, with the current user's own standing. */
export default function LeaderboardPage() {
  const { t } = useTranslation()
  const [board, setBoard] = useState<Leaderboard | null>(null)

  useEffect(() => {
    leaderboardService.get().then(setBoard).catch(() => setBoard({ entries: [], me: null }))
  }, [])

  const meId = board?.me?.userId
  const meShown = !!board?.me && board.entries.some((e) => e.userId === meId)

  return (
    <div className="mx-auto max-w-2xl px-6 py-8">
      <h1 className="mb-1 text-2xl font-bold">{t('leaderboard.title')}</h1>
      <p className="mb-6 text-sm text-muted">{t('leaderboard.subtitle')}</p>

      {board === null ? (
        <p className="text-sm text-muted">{t('common.loading', 'Loading…')}</p>
      ) : board.entries.length === 0 ? (
        <EmptyState message={t('leaderboard.empty')} />
      ) : (
        <>
          <div className="mb-2 flex items-center gap-3 px-3 text-xs font-medium uppercase tracking-wide text-muted">
            <span className="w-8" />
            <span className="flex-1">{t('leaderboard.user')}</span>
            <span className="w-16 text-right">{t('leaderboard.points')}</span>
          </div>
          <ul className="divide-y divide-border overflow-hidden rounded-[var(--radius-card)] border border-border bg-surface">
            {board.entries.map((e) => (
              <li key={e.userId}>
                <Row entry={e} isMe={e.userId === meId} />
              </li>
            ))}
          </ul>

          {/* The caller's own standing, when they're ranked below the shown list. */}
          {board.me && !meShown && (
            <>
              <p className="mt-4 mb-2 px-3 text-xs font-medium uppercase tracking-wide text-muted">{t('leaderboard.you')}</p>
              <div className="overflow-hidden rounded-[var(--radius-card)] border border-primary bg-surface">
                <Row entry={board.me} isMe />
              </div>
            </>
          )}
        </>
      )}
    </div>
  )
}
