import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { adminService } from '../services/adminService'
import { apiErrorMessage } from '../lib/apiError'
import { useSetBranding } from '../hooks/useBranding'

/**
 * Admin panel card for the organization's general details — currently its name, which is
 * shown in the sidebar, the top bar and on the sign-in page. Saving pushes the new name
 * into the shared branding state, so the shell updates immediately without a reload. Uses a
 * dedicated endpoint that touches only this setting.
 */
export default function GeneralSettings() {
  const { t } = useTranslation()
  const setBranding = useSetBranding()
  const [name, setName] = useState('')
  // The name currently stored on the server, for the dirty check.
  const [savedName, setSavedName] = useState('')
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
        setName(s.name)
        setSavedName(s.name)
        setReady(true)
      })
      .catch(() => setMessage({ ok: false, text: t('general.loadFailed') }))
  }, [t])

  const save = async () => {
    setSaving(true)
    setMessage(null)
    try {
      const updated = await adminService.updateGeneral(name.trim())
      setName(updated.name)
      setSavedName(updated.name)
      // Reflect the change in the shell straight away.
      setBranding(updated.name)
      setMessage({ ok: true, text: t('general.saved') })
    } catch (e) {
      setMessage({ ok: false, text: apiErrorMessage(e) })
    } finally {
      setSaving(false)
    }
  }

  if (!ready && !message) return null

  // Disable save on a blank or unchanged name — the server rejects blank anyway.
  const dirty = name.trim() !== savedName && name.trim().length > 0

  return (
    <section className="mb-6 rounded-xl border border-border bg-surface p-5">
      <h2 className="mb-1 font-bold">{t('general.title')}</h2>

      {ready && (
        <>
          <p className="mb-3 text-sm text-muted">{t('general.subtitle')}</p>

          <label className="block text-sm">
            <span className="mb-1 block font-medium text-foreground">{t('general.name')}</span>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              maxLength={100}
              placeholder={t('general.namePlaceholder')}
              className="w-full max-w-md rounded-lg border border-border bg-canvas px-3 py-1.5 text-foreground outline-none focus:border-primary"
            />
          </label>

          <button
            onClick={save}
            disabled={saving || !dirty}
            className="mt-3 rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
          >
            {t('general.save')}
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
