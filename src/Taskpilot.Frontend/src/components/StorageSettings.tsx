import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { adminService, type OrganizationSettings } from '../services/adminService'
import { apiErrorMessage } from '../lib/apiError'

const BYTES_PER_MB = 1024 * 1024

/** Bytes → whole megabytes, for the editable inputs. */
const toMb = (bytes: number) => Math.round(bytes / BYTES_PER_MB)

/** Bytes → megabytes with one decimal, for the read-only usage line. */
const toMb1 = (bytes: number) => (bytes / BYTES_PER_MB).toFixed(1)

/**
 * Admin panel section for the organization's storage limits: shows current usage against
 * the quota and lets the admin edit the per-file cap and the total quota (entered in MB).
 */
export default function StorageSettings() {
  const { t } = useTranslation()
  const [settings, setSettings] = useState<OrganizationSettings | null>(null)
  const [maxUploadMb, setMaxUploadMb] = useState('')
  const [quotaMb, setQuotaMb] = useState('')
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  // Load once. A ref (not state) so React StrictMode's double effect does not fetch twice.
  const loaded = useRef(false)
  useEffect(() => {
    if (loaded.current) return
    loaded.current = true
    adminService
      .getSettings()
      .then((s) => {
        setSettings(s)
        setMaxUploadMb(String(toMb(s.maxUploadBytes)))
        setQuotaMb(String(toMb(s.storageQuotaBytes)))
      })
      .catch(() => setMessage({ ok: false, text: t('storage.loadFailed') }))
  }, [t])

  const save = async () => {
    setSaving(true)
    setMessage(null)
    try {
      const updated = await adminService.updateStorage(
        Number(maxUploadMb) * BYTES_PER_MB,
        Number(quotaMb) * BYTES_PER_MB,
      )
      setSettings(updated)
      setMessage({ ok: true, text: t('storage.saved') })
    } catch (e) {
      setMessage({ ok: false, text: apiErrorMessage(e) })
    } finally {
      setSaving(false)
    }
  }

  if (!settings && !message) return null

  // Usage as a percentage of the quota, clamped so an over-quota state never overflows the bar.
  const usedPct = settings
    ? Math.min(100, Math.round((settings.storageUsedBytes / settings.storageQuotaBytes) * 100))
    : 0

  return (
    <section className="mb-6 rounded-xl border border-border bg-surface p-5">
      <h2 className="mb-3 font-bold">{t('storage.title')}</h2>

      {settings && (
        <>
          <div className="mb-1 flex justify-between text-sm text-muted">
            <span>
              {t('storage.usage', {
                used: toMb1(settings.storageUsedBytes),
                total: toMb(settings.storageQuotaBytes),
              })}
            </span>
            <span>{usedPct}%</span>
          </div>
          <div className="mb-4 h-2 w-full overflow-hidden rounded-full bg-canvas">
            <div
              className={`h-full rounded-full ${usedPct >= 90 ? 'bg-red-500' : 'bg-primary'}`}
              style={{ width: `${usedPct}%` }}
            />
          </div>

          <div className="flex flex-wrap items-end gap-4">
            <label className="text-sm">
              <span className="mb-1 block font-medium text-foreground">{t('storage.maxUpload')}</span>
              <input
                type="number"
                min={1}
                value={maxUploadMb}
                onChange={(e) => setMaxUploadMb(e.target.value)}
                className="w-28 rounded-lg border border-border bg-canvas px-3 py-1.5 text-foreground outline-none focus:border-primary"
              />
            </label>
            <label className="text-sm">
              <span className="mb-1 block font-medium text-foreground">{t('storage.quota')}</span>
              <input
                type="number"
                min={1}
                value={quotaMb}
                onChange={(e) => setQuotaMb(e.target.value)}
                className="w-28 rounded-lg border border-border bg-canvas px-3 py-1.5 text-foreground outline-none focus:border-primary"
              />
            </label>
            <button
              onClick={save}
              disabled={saving || !maxUploadMb || !quotaMb}
              className="rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
            >
              {t('storage.save')}
            </button>
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
