import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { adminService } from '../services/adminService'
import { settingsService } from '../services/settingsService'
import { apiErrorMessage } from '../lib/apiError'
import { useSetFeatures } from '../hooks/useFeatures'

/**
 * Admin panel card for the org-wide feature toggles (Marketplace, Forum). A concern of its
 * own, separate from storage. Each toggle applies immediately (a switch that needs a
 * separate "Save" is confusing), optimistically updating the box and the shared nav, and
 * rolling back if the save fails. Uses a dedicated endpoint that touches only the flags,
 * so it can never clobber the storage limits.
 */
export default function FeatureSettings() {
  const { t } = useTranslation()
  const setFeatures = useSetFeatures()
  const [marketplaceEnabled, setMarketplaceEnabled] = useState(true)
  const [forumEnabled, setForumEnabled] = useState(true)
  const [ready, setReady] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  // Load the current flags once. A ref (not state) so StrictMode's double effect
  // does not fetch twice.
  const loaded = useRef(false)
  useEffect(() => {
    if (loaded.current) return
    loaded.current = true
    settingsService
      .getFeatures()
      .then((f) => {
        setMarketplaceEnabled(f.marketplaceEnabled)
        setForumEnabled(f.forumEnabled)
        setReady(true)
      })
      .catch(() => setMessage({ ok: false, text: t('storage.loadFailed') }))
  }, [t])

  const toggle = async (marketplace: boolean, forum: boolean) => {
    const previous = { marketplace: marketplaceEnabled, forum: forumEnabled }

    // Optimistic: update the checkbox and the sidebar right away.
    setMarketplaceEnabled(marketplace)
    setForumEnabled(forum)
    setFeatures(marketplace, forum)
    setMessage(null)

    try {
      const updated = await adminService.updateFeatures(marketplace, forum)
      setFeatures(updated.marketplaceEnabled, updated.forumEnabled)
      setMessage({ ok: true, text: t('storage.saved') })
    } catch (e) {
      // Roll the box and the sidebar back to what the server still has.
      setMarketplaceEnabled(previous.marketplace)
      setForumEnabled(previous.forum)
      setFeatures(previous.marketplace, previous.forum)
      setMessage({ ok: false, text: apiErrorMessage(e) })
    }
  }

  if (!ready && !message) return null

  return (
    <section className="mb-6 rounded-xl border border-border bg-surface p-5">
      <h2 className="mb-1 font-bold">{t('features.title')}</h2>
      <p className="mb-3 text-xs text-muted">{t('features.hint')}</p>

      {ready && (
        <div className="flex flex-wrap gap-6">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={marketplaceEnabled}
              onChange={(e) => toggle(e.target.checked, forumEnabled)}
              className="h-4 w-4 accent-primary"
            />
            {t('features.marketplace')}
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={forumEnabled}
              onChange={(e) => toggle(marketplaceEnabled, e.target.checked)}
              className="h-4 w-4 accent-primary"
            />
            {t('features.forum')}
          </label>
        </div>
      )}

      {message && (
        <p className={`mt-3 text-sm ${message.ok ? 'text-green-600 dark:text-green-400' : 'text-red-600'}`}>
          {message.text}
        </p>
      )}
    </section>
  )
}
