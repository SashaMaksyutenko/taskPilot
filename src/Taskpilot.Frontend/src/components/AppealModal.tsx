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
        className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl dark:bg-slate-800"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('appeal.title')}</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
            ✕
          </button>
        </div>

        <p className="mb-3 rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-600 dark:bg-slate-900 dark:text-slate-300">
          {t('appeal.warningLabel')}: {warningReason}
        </p>

        <textarea
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          rows={4}
          placeholder={t('appeal.messagePlaceholder')}
          className="mb-4 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
        />

        <div className="flex items-center justify-end gap-3">
          <button onClick={onClose} className="text-sm font-semibold text-slate-500 hover:text-[#1E2A44] dark:text-slate-300 dark:hover:text-white">
            {t('appeal.cancel')}
          </button>
          <button
            onClick={submit}
            disabled={saving || message.trim().length < 10}
            className="rounded-lg bg-[#1E2A44] px-5 py-2 text-sm font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
          >
            {t('appeal.submit')}
          </button>
        </div>
      </div>
    </div>
  )
}
