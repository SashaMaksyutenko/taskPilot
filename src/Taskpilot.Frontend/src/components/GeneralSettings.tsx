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
  const [logoUrl, setLogoUrl] = useState<string | null>(null)
  const [logoBusy, setLogoBusy] = useState(false)
  const [ready, setReady] = useState(false)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const logoInputRef = useRef<HTMLInputElement>(null)

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
        setLogoUrl(s.logoUrl)
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
      setBranding({ name: updated.name })
      setMessage({ ok: true, text: t('general.saved') })
    } catch (e) {
      setMessage({ ok: false, text: apiErrorMessage(e) })
    } finally {
      setSaving(false)
    }
  }

  const uploadLogo = async (file: File) => {
    setLogoBusy(true)
    setMessage(null)
    try {
      const updated = await adminService.updateLogo(file)
      setLogoUrl(updated.logoUrl)
      // Update the shell logo without a reload.
      setBranding({ logoUrl: updated.logoUrl })
      setMessage({ ok: true, text: t('general.logoSaved') })
    } catch (e) {
      // Size limit (2 MB) and the image-only rule are refused here.
      setMessage({ ok: false, text: apiErrorMessage(e) })
    } finally {
      setLogoBusy(false)
      if (logoInputRef.current) logoInputRef.current.value = ''
    }
  }

  const removeLogo = async () => {
    setLogoBusy(true)
    setMessage(null)
    try {
      const updated = await adminService.removeLogo()
      setLogoUrl(updated.logoUrl)
      setBranding({ logoUrl: updated.logoUrl })
      setMessage({ ok: true, text: t('general.logoRemoved') })
    } catch (e) {
      setMessage({ ok: false, text: apiErrorMessage(e) })
    } finally {
      setLogoBusy(false)
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

          {/* Logo: preview (custom or the built-in mark) + upload/remove */}
          <div className="mt-5">
            <span className="mb-1 block text-sm font-medium text-foreground">{t('general.logo')}</span>
            <div className="flex items-center gap-3">
              <img
                src={logoUrl ?? '/logo-mark.svg'}
                alt=""
                className="h-12 w-12 rounded border border-border bg-canvas object-contain"
              />
              <button
                type="button"
                onClick={() => logoInputRef.current?.click()}
                disabled={logoBusy}
                className="rounded-lg border border-border px-3 py-1.5 text-sm font-medium text-foreground hover:bg-canvas disabled:opacity-50"
              >
                {logoBusy ? t('general.logoUploading') : t('general.logoUpload')}
              </button>
              {logoUrl && (
                <button
                  type="button"
                  onClick={removeLogo}
                  disabled={logoBusy}
                  className="text-sm font-medium text-muted hover:text-red-600 disabled:opacity-50"
                >
                  {t('general.logoRemove')}
                </button>
              )}
              <input
                ref={logoInputRef}
                type="file"
                accept="image/*"
                className="hidden"
                aria-label={t('general.logoUpload')}
                onChange={(e) => {
                  const file = e.target.files?.[0]
                  if (file) uploadLogo(file)
                }}
              />
            </div>
            <span className="mt-1 block text-xs text-muted">{t('general.logoHint')}</span>
          </div>
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
