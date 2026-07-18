import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { adminService } from '../services/adminService'
import { apiErrorMessage } from '../lib/apiError'

/**
 * Admin panel card for who may self-register: a comma-separated email-domain allowlist.
 * Empty means registration is open to any domain, so the current state is spelled out —
 * typing a domain here silently locks out every other domain, which is easy to do by
 * accident. Uses a dedicated endpoint that touches only this setting.
 */
export default function RegistrationSettings() {
  const { t } = useTranslation()
  const [domains, setDomains] = useState('')
  const [saved, setSavedValue] = useState('')
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
        setDomains(s.allowedEmailDomains)
        setSavedValue(s.allowedEmailDomains)
        setReady(true)
      })
      .catch(() => setMessage({ ok: false, text: t('registration.loadFailed') }))
  }, [t])

  const save = async () => {
    setSaving(true)
    setMessage(null)
    try {
      const updated = await adminService.updateRegistration(domains.trim())
      // The server returns the normalized value ("@Acme.com" -> "acme.com"), so show that.
      setDomains(updated.allowedEmailDomains)
      setSavedValue(updated.allowedEmailDomains)
      setMessage({ ok: true, text: t('registration.saved') })
    } catch (e) {
      setMessage({ ok: false, text: apiErrorMessage(e) })
    } finally {
      setSaving(false)
    }
  }

  if (!ready && !message) return null

  return (
    <section className="mb-6 rounded-xl border border-border bg-surface p-5">
      <h2 className="mb-1 font-bold">{t('registration.title')}</h2>

      {ready && (
        <>
          {/* Spell out what is in force right now, so the effect is never a surprise. */}
          <p className="mb-3 text-sm text-muted">
            {saved.trim().length === 0
              ? t('registration.openNow')
              : t('registration.restrictedNow', { domains: saved })}
          </p>

          <label className="block text-sm">
            <span className="mb-1 block font-medium text-foreground">{t('registration.domains')}</span>
            <input
              value={domains}
              onChange={(e) => setDomains(e.target.value)}
              placeholder={t('registration.placeholder')}
              className="w-full max-w-md rounded-lg border border-border bg-canvas px-3 py-1.5 text-foreground outline-none focus:border-primary"
            />
          </label>
          <p className="mt-1 text-xs text-muted">{t('registration.hint')}</p>

          <button
            onClick={save}
            disabled={saving || domains === saved}
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
