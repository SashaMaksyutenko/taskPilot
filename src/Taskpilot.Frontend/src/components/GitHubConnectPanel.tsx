import { GitBranch, Lock } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import Button from './ui/Button'
import { apiErrorMessage } from '../lib/apiError'
import { notify } from '../lib/toast'
import { githubConnectionService, type GitHubConnectionStatus, type GitHubRepo } from '../services/githubConnectionService'

/** Redirect target GitHub sends the user back to — the OAuth app's callback host must match. */
const redirectUri = () => `${window.location.origin}/settings`

/**
 * Connect / disconnect the user's personal GitHub account and browse its repositories.
 * Renders nothing when the server has no GitHub integration OAuth app configured. The OAuth flow
 * redirects back to /settings with ?code&state, which this panel consumes to complete the link.
 */
export default function GitHubConnectPanel() {
  const { t } = useTranslation()
  const [status, setStatus] = useState<GitHubConnectionStatus | null>(null)
  const [busy, setBusy] = useState(false)
  const [repos, setRepos] = useState<GitHubRepo[] | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  // Consume the OAuth code exactly once (StrictMode runs effects twice in dev).
  const consumed = useRef(false)

  useEffect(() => {
    githubConnectionService
      .status()
      .then(setStatus)
      .catch(() => setStatus({ configured: false, connected: false, login: null, connectedAt: null }))
  }, [])

  // Complete the link when GitHub redirects back with ?code (and only ours: state must be present).
  useEffect(() => {
    const code = searchParams.get('code')
    const state = searchParams.get('state')
    if (!code || !state || consumed.current) return
    consumed.current = true
    setBusy(true)
    githubConnectionService
      .connect(code, redirectUri(), state)
      .then((s) => {
        setStatus(s)
        notify.success(t('ghlink.connected'))
      })
      .catch((e) => notify.error(apiErrorMessage(e)))
      .finally(() => {
        setBusy(false)
        // Drop the OAuth params so a refresh doesn't retry the exchange.
        searchParams.delete('code')
        searchParams.delete('state')
        setSearchParams(searchParams, { replace: true })
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams])

  const connect = async () => {
    setBusy(true)
    try {
      const url = await githubConnectionService.connectUrl(redirectUri())
      window.location.href = url // leave the SPA for GitHub's authorize screen
    } catch (e) {
      notify.error(apiErrorMessage(e))
      setBusy(false)
    }
  }

  const disconnect = async () => {
    setBusy(true)
    try {
      await githubConnectionService.disconnect()
      setStatus((s) => (s ? { ...s, connected: false, login: null, connectedAt: null } : s))
      setRepos(null)
      notify.success(t('ghlink.disconnected'))
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const loadRepos = async () => {
    if (repos) {
      setRepos(null) // toggle closed
      return
    }
    setBusy(true)
    try {
      setRepos(await githubConnectionService.repos())
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  // Hidden entirely when the server has no GitHub integration configured.
  if (!status || !status.configured) return null

  return (
    <section className="mt-8 rounded-[var(--radius-card)] border border-border bg-surface p-6">
      <h2 className="mb-1 font-bold">{t('ghlink.title')}</h2>
      <p className="mb-4 text-sm text-muted">{t('ghlink.desc')}</p>

      {status.connected ? (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-3 text-sm">
            <span className="inline-flex items-center gap-2 rounded-full bg-emerald-500/10 px-3 py-1 font-semibold text-emerald-600 dark:text-emerald-400">
              <GitBranch className="h-4 w-4" />
              {t('ghlink.connectedAs', { login: status.login })}
            </span>
            <Button variant="secondary" size="sm" onClick={loadRepos} disabled={busy}>
              {repos ? t('ghlink.hideRepos') : t('ghlink.viewRepos')}
            </Button>
            <button onClick={disconnect} disabled={busy} className="text-xs font-semibold text-red-600 hover:underline disabled:opacity-60">
              {t('ghlink.disconnect')}
            </button>
          </div>

          {repos && (
            repos.length === 0 ? (
              <p className="text-sm text-muted">{t('ghlink.noRepos')}</p>
            ) : (
              <ul className="max-h-64 space-y-1 overflow-y-auto rounded-lg border border-border p-2">
                {repos.map((r) => (
                  <li key={r.fullName} className="flex items-center gap-2 rounded px-2 py-1 text-sm hover:bg-canvas">
                    <GitBranch className="h-3.5 w-3.5 flex-none text-muted" />
                    <span className="min-w-0 truncate font-mono text-xs">{r.fullName}</span>
                    {r.private && (
                      <span className="ml-auto inline-flex flex-none items-center gap-1 text-[11px] text-muted">
                        <Lock className="h-3 w-3" />
                        {t('ghlink.private')}
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            )
          )}
        </div>
      ) : (
        <button
          onClick={connect}
          disabled={busy}
          className="inline-flex items-center gap-2 rounded-lg bg-[#24292f] px-5 py-2 text-sm font-semibold text-white transition hover:brightness-110 disabled:opacity-60 dark:bg-[#333b42]"
        >
          <GitBranch className="h-4 w-4" />
          {t('ghlink.connect')}
        </button>
      )}
    </section>
  )
}
