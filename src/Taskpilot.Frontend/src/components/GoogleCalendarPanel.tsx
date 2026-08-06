import { Calendar as CalendarIcon, RefreshCw } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import Button from './ui/Button'
import { apiErrorMessage } from '../lib/apiError'
import { notify } from '../lib/toast'
import { googleCalendarService, type GoogleCalendarStatus } from '../services/googleCalendarService'

/** Redirect target Google sends the user back to — must be an authorized redirect URI in the OAuth app. */
const redirectUri = () => `${window.location.origin}/calendar`

/**
 * Connect / disconnect the user's Google Calendar and push their deadline tasks to it on demand.
 * Renders nothing when the server has no Google OAuth configured. The OAuth flow redirects back to
 * /calendar with a ?code, which this panel consumes to complete the connection.
 */
export default function GoogleCalendarPanel() {
  const { t } = useTranslation()
  const [status, setStatus] = useState<GoogleCalendarStatus | null>(null)
  const [busy, setBusy] = useState(false)
  const [searchParams, setSearchParams] = useSearchParams()
  // Consume the OAuth code exactly once (StrictMode runs effects twice in dev).
  const consumed = useRef(false)

  useEffect(() => {
    googleCalendarService
      .status()
      .then(setStatus)
      .catch(() => setStatus({ configured: false, connected: false, lastSyncedUtc: null }))
  }, [])

  // Complete the consent flow when Google redirects back with ?code.
  useEffect(() => {
    const code = searchParams.get('code')
    if (!code || consumed.current) return
    consumed.current = true
    setBusy(true)
    googleCalendarService
      .connect(code, redirectUri())
      .then((s) => {
        setStatus(s)
        notify.success(t('gcal.connected'))
      })
      .catch((e) => notify.error(apiErrorMessage(e)))
      .finally(() => {
        setBusy(false)
        // Drop the OAuth params so a refresh doesn't retry the exchange.
        searchParams.delete('code')
        searchParams.delete('state')
        searchParams.delete('scope')
        setSearchParams(searchParams, { replace: true })
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams])

  const connect = async () => {
    setBusy(true)
    try {
      const url = await googleCalendarService.connectUrl(redirectUri())
      window.location.href = url // leave the SPA for Google's consent screen
    } catch (e) {
      notify.error(apiErrorMessage(e))
      setBusy(false)
    }
  }

  const sync = async () => {
    setBusy(true)
    try {
      const pushed = await googleCalendarService.sync() // TaskPilot → Google
      const pulled = await googleCalendarService.pull() // Google → TaskPilot (moved events)
      notify.success(t('gcal.synced', { count: pushed.created + pushed.updated }))
      if (pulled.rescheduled > 0) notify.success(t('gcal.pulled', { count: pulled.rescheduled }))
      setStatus((s) => (s ? { ...s, lastSyncedUtc: new Date().toISOString() } : s))
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const disconnect = async () => {
    setBusy(true)
    try {
      await googleCalendarService.disconnect()
      setStatus((s) => (s ? { ...s, connected: false, lastSyncedUtc: null } : s))
      notify.success(t('gcal.disconnected'))
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  // Hidden entirely when the server has no Google OAuth configured.
  if (!status || !status.configured) return null

  return (
    <div className="mb-4 rounded-[var(--radius-card)] border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center gap-3">
        <span className="inline-flex flex-none rounded-lg bg-primary-muted p-2 text-primary">
          <CalendarIcon className="h-[18px] w-[18px]" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="font-semibold">{t('gcal.title')}</p>
          <p className="text-sm text-muted">
            {status.connected
              ? status.lastSyncedUtc
                ? t('gcal.lastSynced', { date: new Date(status.lastSyncedUtc).toLocaleString() })
                : t('gcal.connectedHint')
              : t('gcal.desc')}
          </p>
        </div>
        {status.connected ? (
          <div className="flex flex-none items-center gap-2">
            <Button size="sm" onClick={sync} disabled={busy}>
              <RefreshCw className="h-4 w-4" />
              {t('gcal.syncNow')}
            </Button>
            <Button variant="secondary" size="sm" onClick={disconnect} disabled={busy}>
              {t('gcal.disconnect')}
            </Button>
          </div>
        ) : (
          <Button size="sm" onClick={connect} disabled={busy}>
            {t('gcal.connect')}
          </Button>
        )}
      </div>
    </div>
  )
}
