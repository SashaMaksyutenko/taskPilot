import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { adminService } from '../services/adminService'
import { apiErrorMessage } from '../lib/apiError'

/**
 * Admin panel card for who may self-register, via two email-domain lists:
 * an allowlist ("only these domains") and a denylist ("everyone except these").
 * The state actually in force is spelled out, because an allowlist entry silently locks
 * out every other domain — easy to do by accident. Uses a dedicated endpoint that touches
 * only these settings.
 */
export default function RegistrationSettings() {
  const { t } = useTranslation()
  const [allowed, setAllowed] = useState('')
  const [blocked, setBlocked] = useState('')
  // The values currently stored on the server, used for the summary and the dirty check.
  const [savedAllowed, setSavedAllowed] = useState('')
  const [savedBlocked, setSavedBlocked] = useState('')
  const [ready, setReady] = useState(false)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  // Load once. A ref (not state) so StrictMode's double effect does not fetch twice.
  const loaded = useRef(false)
  useEffect(() => {
    if (loaded.current) return
    loaded.current = true
    adminService
      .getSettings()
      .then((s) => {
        setAllowed(s.allowedEmailDomains)
        setBlocked(s.blockedEmailDomains)
        setSavedAllowed(s.allowedEmailDomains)
        setSavedBlocked(s.blockedEmailDomains)
        setReady(true)
      })
      .catch(() => setMessage({ ok: false, text: t('registration.loadFailed') }))
  }, [t])

  const save = async () => {
    setSaving(true)
    setMessage(null)
    try {
      const updated = await adminService.updateRegistration(allowed.trim(), blocked.trim())
      // Show the normalized values the server stored ("@Acme.COM" -> "acme.com").
      setAllowed(updated.allowedEmailDomains)
      setBlocked(updated.blockedEmailDomains)
      setSavedAllowed(updated.allowedEmailDomains)
      setSavedBlocked(updated.blockedEmailDomains)
      setMessage({ ok: true, text: t('registration.saved') })
    } catch (e) {
      setMessage({ ok: false, text: apiErrorMessage(e) })
    } finally {
      setSaving(false)
    }
  }

  if (!ready && !message) return null

  const dirty = allowed !== savedAllowed || blocked !== savedBlocked

  return (
    <section className="mb-6 rounded-xl border border-border bg-surface p-5">
      <h2 className="mb-1 font-bold">{t('registration.title')}</h2>

      {ready && (
        <>
          {/* Spell out what is in force right now, so the effect is never a surprise. */}
          <p className="mb-3 text-sm text-muted">
            {savedAllowed.trim().length === 0
              ? t('registration.openNow')
              : t('registration.restrictedNow', { domains: savedAllowed })}
            {savedBlocked.trim().length > 0 && ` ${t('registration.blockedNow', { domains: savedBlocked })}`}
          </p>

          <div className="space-y-3">
            <label className="block text-sm">
              <span className="mb-1 block font-medium text-foreground">{t('registration.domains')}</span>
              <input
                value={allowed}
                onChange={(e) => setAllowed(e.target.value)}
                placeholder={t('registration.placeholder')}
                className="w-full max-w-md rounded-lg border border-border bg-canvas px-3 py-1.5 text-foreground outline-none focus:border-primary"
              />
              <span className="mt-1 block text-xs text-muted">{t('registration.hint')}</span>
            </label>

            <label className="block text-sm">
              <span className="mb-1 block font-medium text-foreground">{t('registration.blocked')}</span>
              <input
                value={blocked}
                onChange={(e) => setBlocked(e.target.value)}
                placeholder={t('registration.blockedPlaceholder')}
                className="w-full max-w-md rounded-lg border border-border bg-canvas px-3 py-1.5 text-foreground outline-none focus:border-primary"
              />
              <span className="mt-1 block text-xs text-muted">{t('registration.blockedHint')}</span>
            </label>
          </div>

          <button
            onClick={save}
            disabled={saving || !dirty}
            className="mt-3 rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
          >
            {t('registration.save')}
          </button>
        </>
      )}

      {message && (
        <p className={`mt-3 text-sm ${message.ok ? 'text-green-600 dark:text-green-400' : 'text-red-600'}`}>
          {message.text}
        </p>
      )}
    </section>
  )
}
