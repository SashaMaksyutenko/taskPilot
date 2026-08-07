import { Copy, GitCommit, GitPullRequest } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { gitHubService, type GitHubTaskLink } from '../services/gitHubService'
import { notify } from '../lib/toast'

/**
 * Shows the commits and pull requests (from the project's linked GitHub repo) that reference this
 * task, plus a "copy reference" helper. Renders nothing unless the project is connected to GitHub.
 */
export default function GitHubTaskLinksSection({ taskId, projectId }: { taskId: string; projectId: string }) {
  const { t } = useTranslation()
  const [connected, setConnected] = useState(false)
  const [links, setLinks] = useState<GitHubTaskLink[]>([])
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    let alive = true
    Promise.all([
      gitHubService.status(projectId).catch(() => ({ connected: false, repo: null, webhookUrl: null })),
      gitHubService.getTaskLinks(taskId).catch(() => [] as GitHubTaskLink[]),
    ]).then(([status, ls]) => {
      if (!alive) return
      setConnected(status.connected)
      setLinks(ls)
      setLoaded(true)
    })
    return () => {
      alive = false
    }
  }, [taskId, projectId])

  const copyRef = () => {
    // A merged PR whose body contains this line moves the task to Done.
    navigator.clipboard?.writeText(`Closes ${taskId}`).catch(() => {})
    notify.success(t('github.refCopied'))
  }

  // Hidden entirely unless the project is linked to a GitHub repo.
  if (!loaded || !connected) return null

  return (
    <div>
      <div className="mb-2 flex items-center justify-between gap-2">
        <span className="text-sm font-medium text-foreground">{t('github.linksTitle')}</span>
        <button
          type="button"
          onClick={copyRef}
          title={t('github.copyRefHint')}
          className="inline-flex flex-none items-center gap-1 text-xs font-semibold text-primary hover:underline"
        >
          <Copy className="h-3.5 w-3.5" />
          {t('github.copyRef')}
        </button>
      </div>

      {links.length === 0 ? (
        <p className="text-xs text-muted">{t('github.noLinks')}</p>
      ) : (
        <ul className="space-y-1.5">
          {links.map((l) => (
            <li key={`${l.kind}-${l.externalId}`}>
              <a
                href={l.url}
                target="_blank"
                rel="noreferrer"
                className="flex items-center gap-2 rounded-lg border border-border px-2.5 py-1.5 text-sm transition hover:bg-canvas"
              >
                {l.kind === 'PullRequest' ? (
                  <GitPullRequest className="h-4 w-4 flex-none text-primary" />
                ) : (
                  <GitCommit className="h-4 w-4 flex-none text-muted" />
                )}
                <span className="min-w-0 flex-1 truncate">{l.title}</span>
                <span className="flex-none rounded-full bg-canvas px-2 py-0.5 text-[10px] font-semibold text-muted">
                  {l.state}
                </span>
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
