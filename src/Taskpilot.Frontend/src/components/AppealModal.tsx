import { useState } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * Modal for a user to appeal a moderation warning. Collects a message and delegates
 * submission to the parent.
 */
export default function AppealModal({
  warningReason,
  onClose,
  onSubmit,
}: {
  warningReason: string
  onClose: () => void
  onSubmit: (message: string) => Promise<void>
}) {
  const { t } = useTranslation()
  const [message, setMessage] = useState('')
  const [saving, setSaving] = useState(false)

  const submit = async () => {
    const trimmed = message.trim()
    if (trimmed.length < 10 || saving) return
    setSaving(true)
    try {
      await onSubmit(trimmed)
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="w-full max-w-md rounded-xl bg-surface p-6 shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('appeal.title')}</h2>
          <button onClick={onClose} className="text-muted hover:text-foreground dark:hover:text-foreground">
            ✕
          </button>
        </div>

        <p className="mb-3 rounded-lg bg-canvas px-3 py-2 text-sm text-muted">
          {t('appeal.warningLabel')}: {warningReason}
        </p>

        <textarea
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          rows={4}
          placeholder={t('appeal.messagePlaceholder')}
          className="mb-4 w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary"
        />

        <div className="flex items-center justify-end gap-3">
          <button onClick={onClose} className="text-sm font-semibold text-muted hover:text-primary dark:hover:text-white">
            {t('appeal.cancel')}
          </button>
          <button
            onClick={submit}
            disabled={saving || message.trim().length < 10}
            className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
          >
            {t('appeal.submit')}
          </button>
        </div>
      </div>
    </div>
  )
}
