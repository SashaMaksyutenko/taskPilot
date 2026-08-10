import { RefreshCw, Sparkles } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { planningService, type NextActions } from '../services/planningService'
import Card from './ui/Card'

/**
 * "What to do next" — an AI-prioritized shortlist of the user's open tasks, each with a one-line
 * reason. Shown on the dashboard when the assistant LLM is configured; falls back to a plain
 * urgency order if the model is unavailable. Renders nothing when the user has no open tasks.
 */
export default function NextActionsCard() {
  const { t } = useTranslation()
  const [plan, setPlan] = useState<NextActions | null>(null)
  const [loading, setLoading] = useState(true)

  const fetchPlan = () =>
    planningService
      .next()
      .then(setPlan)
      .catch(() => setPlan(null))
      .finally(() => setLoading(false))
  // Starts in the loading state, so the mount fetch doesn't toggle state synchronously.
  useEffect(() => {
    fetchPlan()
  }, [])
  const refresh = () => {
    setLoading(true)
    fetchPlan()
  }

  // Nothing to plan (no open assigned tasks) — stay out of the way.
  if (!loading && (!plan || plan.items.length === 0)) return null

  return (
    <Card className="mt-6 p-5">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 font-bold">
          <Sparkles className="h-4 w-4 text-primary" />
          {t('planning.title')}
        </h2>
        <button
          onClick={refresh}
          disabled={loading}
          title={t('planning.refresh')}
          className="inline-flex items-center gap-1.5 rounded-lg border border-border px-2.5 py-1.5 text-xs font-medium transition hover:bg-canvas disabled:opacity-60"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${loading ? 'animate-spin' : ''}`} />
          {t('planning.refresh')}
        </button>
      </div>

      {loading && !plan ? (
        <div className="space-y-3 py-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-8 animate-pulse rounded bg-canvas" />
          ))}
        </div>
      ) : (
        <ol className="space-y-1">
          {plan!.items.map((item, i) => {
            const due = item.deadline ? new Date(item.deadline).toLocaleDateString() : null
            return (
              <li key={item.taskId}>
                <Link
                  to={`/projects/${item.projectId}?task=${item.taskId}`}
                  className="flex items-start gap-3 rounded-lg px-2 py-2 transition hover:bg-canvas"
                >
                  <span className="mt-0.5 flex h-5 w-5 flex-none items-center justify-center rounded-full bg-primary/10 text-[11px] font-bold text-primary">
                    {i + 1}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center gap-2">
                      <span className="min-w-0 truncate text-sm font-medium">{item.title}</span>
                      {item.isBlocked && (
                        <span className="flex-none rounded-full bg-amber-500/10 px-1.5 py-0.5 text-[10px] font-semibold text-amber-600 dark:text-amber-400">
                          {t('planning.blocked')}
                        </span>
                      )}
                    </span>
                    <span className="mt-0.5 flex items-center gap-1.5 text-xs text-muted">
                      <span
                        className="inline-block h-2 w-2 flex-none rounded-full"
                        style={{ background: item.projectColor ?? '#94a3b8' }}
                      />
                      <span className="max-w-[8rem] flex-none truncate">{item.projectName}</span>
                      {item.reason && <span className="truncate">· {item.reason}</span>}
                    </span>
                  </span>
                  {due && (
                    <span className={`flex-none text-xs ${item.isOverdue ? 'font-semibold text-red-600 dark:text-red-400' : 'text-muted'}`}>
                      {due}
                    </span>
                  )}
                </Link>
              </li>
            )
          })}
        </ol>
      )}

      {plan && !plan.rankedByAi && plan.items.length > 0 && (
        <p className="mt-2 px-2 text-[11px] text-muted">{t('planning.fallback')}</p>
      )}
    </Card>
  )
}
