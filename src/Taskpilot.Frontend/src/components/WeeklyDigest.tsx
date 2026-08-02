import { Sparkles } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../lib/apiError'
import { notify } from '../lib/toast'
import { digestService, type Digest } from '../services/digestService'
import Card from './ui/Card'

/**
 * Week-in-review card on the dashboard: four activity numbers (completed / created / overdue /
 * due soon) with an optional AI-written summary generated on demand (only when the assistant
 * LLM is configured — the summary button is hidden otherwise).
 */
export default function WeeklyDigest({ aiEnabled }: { aiEnabled: boolean }) {
  const { t } = useTranslation()
  const [digest, setDigest] = useState<Digest | null>(null)
  const [summary, setSummary] = useState('')
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    digestService.weekly().then(setDigest).catch(() => {})
  }, [])

  const generate = async () => {
    setLoading(true)
    try {
      const res = await digestService.summary()
      if (res.enabled) setSummary(res.summary)
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setLoading(false)
    }
  }

  if (!digest) return null

  const cells = [
    { label: t('digest.completed'), value: digest.completed, tone: 'text-green-600 dark:text-green-400' },
    { label: t('digest.created'), value: digest.created, tone: 'text-foreground' },
    { label: t('digest.overdue'), value: digest.overdue, tone: 'text-red-600 dark:text-red-400' },
    { label: t('digest.dueSoon'), value: digest.dueSoon, tone: 'text-amber-600 dark:text-amber-400' },
  ]

  return (
    <Card className="mt-6 p-5">
      <div className="mb-4 flex items-center justify-between gap-2">
        <h2 className="font-bold">{t('digest.title')}</h2>
        {aiEnabled && (
          <button
            onClick={generate}
            disabled={loading}
            className="inline-flex items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 text-sm font-medium transition hover:bg-canvas disabled:opacity-60"
          >
            <Sparkles className="h-4 w-4 text-primary" />
            {loading ? t('digest.generating') : t('digest.summarize')}
          </button>
        )}
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        {cells.map((c) => (
          <div key={c.label}>
            <div className={`text-2xl font-bold tabular-nums ${c.tone}`}>{c.value}</div>
            <div className="mt-0.5 text-xs text-muted">{c.label}</div>
          </div>
        ))}
      </div>

      {summary && (
        <p className="mt-4 rounded-lg bg-primary/5 p-3 text-sm leading-relaxed text-foreground">{summary}</p>
      )}
    </Card>
  )
}
