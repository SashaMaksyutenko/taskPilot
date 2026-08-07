import { Copy, GitBranch, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import Button from '../ui/Button'
import Input from '../ui/Input'
import { apiErrorMessage } from '../../lib/apiError'
import { notify } from '../../lib/toast'
import { gitHubService, type GitHubConnectResult, type GitHubStatus } from '../../services/gitHubService'

/**
 * Board owner tool: link a GitHub repo to the project and show the webhook URL + secret to
 * configure in the repo's settings. Commits/PRs that mention a task id then link to it, and a
 * merged PR that "closes" a task moves it to Done.
 */
export default function GitHubModal({ projectId, onClose }: { projectId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [status, setStatus] = useState<GitHubStatus | null>(null)
  const [repo, setRepo] = useState('')
  const [result, setResult] = useState<GitHubConnectResult | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    gitHubService
      .status(projectId)
      .then((s) => {
        setStatus(s)
        setRepo(s.repo ?? '')
      })
      .catch(() => setStatus({ connected: false, repo: null, webhookUrl: null }))
  }, [projectId])

  const connect = async () => {
    setBusy(true)
    try {
      const res = await gitHubService.connect(projectId, repo.trim())
      setResult(res)
      setStatus({ connected: true, repo: res.repo, webhookUrl: res.webhookUrl })
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const disconnect = async () => {
    setBusy(true)
    try {
      await gitHubService.disconnect(projectId)
      setStatus({ connected: false, repo: null, webhookUrl: null })
      setResult(null)
      setRepo('')
      notify.success(t('github.disconnected'))
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const copy = (value: string) => {
    navigator.clipboard?.writeText(value).catch(() => {})
    notify.success(t('github.copied'))
  }

  const webhookUrl = result?.webhookUrl ?? status?.webhookUrl ?? ''

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="w-full max-w-lg rounded-[var(--radius-card)] border border-border bg-surface p-6 shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-1 flex items-center gap-2">
          <GitBranch className="h-5 w-5" />
          <h2 className="text-lg font-bold">{t('github.title')}</h2>
          <button onClick={onClose} className="ml-auto rounded-lg p-1.5 text-muted hover:bg-canvas" aria-label={t('github.close')}>
            <X className="h-5 w-5" />
          </button>
        </div>
        <p className="mb-4 text-sm text-muted">{t('github.desc')}</p>

        {status?.connected ? (
          <div className="mb-4 flex items-center gap-3 rounded-lg border border-border bg-canvas px-3 py-2 text-sm">
            <span className="font-semibold">{status.repo}</span>
            <button onClick={disconnect} disabled={busy} className="ml-auto text-xs font-semibold text-red-600 hover:underline disabled:opacity-50">
              {t('github.disconnect')}
            </button>
          </div>
        ) : (
          <div className="mb-4 flex gap-2">
            <Input value={repo} onChange={(e) => setRepo(e.target.value)} placeholder={t('github.repoPlaceholder')} className="flex-1" />
            <Button onClick={connect} disabled={busy || !repo.trim()}>
              {t('github.connect')}
            </Button>
          </div>
        )}

        {status?.connected && (
          <div className="space-y-3 rounded-lg border border-border p-4 text-sm">
            <p className="font-semibold">{t('github.setupTitle')}</p>

            <Field label={t('github.webhookUrl')} value={webhookUrl} onCopy={() => copy(webhookUrl)} copyLabel={t('github.copy')} />

            {result?.webhookSecret ? (
              <Field label={t('github.secret')} value={result.webhookSecret} onCopy={() => copy(result.webhookSecret)} copyLabel={t('github.copy')} mono />
            ) : (
              <p className="text-xs text-muted">{t('github.secretHidden')}</p>
            )}

            <ul className="ml-4 list-disc space-y-1 text-xs text-muted">
              <li>{t('github.stepContentType')}</li>
              <li>{t('github.stepEvents')}</li>
              <li>{t('github.stepRef')}</li>
            </ul>
          </div>
        )}
      </div>
    </div>
  )
}

/** A read-only value row with a copy button. */
function Field({ label, value, onCopy, copyLabel, mono }: { label: string; value: string; onCopy: () => void; copyLabel: string; mono?: boolean }) {
  return (
    <div>
      <label className="mb-1 block text-xs font-semibold text-muted">{label}</label>
      <div className="flex items-center gap-2">
        <code className={`min-w-0 flex-1 overflow-x-auto rounded bg-canvas px-2 py-1 text-xs ${mono ? 'font-mono' : ''}`}>{value}</code>
        <button onClick={onCopy} className="inline-flex flex-none items-center gap-1 text-xs font-semibold text-primary hover:underline">
          <Copy className="h-3.5 w-3.5" />
          {copyLabel}
        </button>
      </div>
    </div>
  )
}
