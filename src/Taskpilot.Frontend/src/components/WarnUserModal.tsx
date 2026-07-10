import { useState } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * Modal for an admin to issue a moderation warning to a user. Collects a reason and
 * delegates submission to the parent (which calls the API and handles escalation).
 */
export default function WarnUserModal({
  userName,
  onClose,
  onSubmit,
}: {
  userName: string
  onClose: () => void
  onSubmit: (reason: string) => Promise<void>
}) {
  const { t } = useTranslation()
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)

  const submit = async () => {
    const trimmed = reason.trim()
    if (trimmed.length < 3 || saving) return
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
          <h2 className="text-lg font-bold">{t('warn.title', { name: userName })}</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
            ✕
          </button>
        </div>

        <p className="mb-3 text-sm text-slate-500 dark:text-slate-400">{t('warn.hint')}</p>

        <textarea
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          rows={4}
          placeholder={t('warn.reasonPlaceholder')}
          className="mb-4 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
        />

        <div className="flex items-center justify-end gap-3">
          <button onClick={onClose} className="text-sm font-semibold text-slate-500 hover:text-primary dark:text-slate-300 dark:hover:text-white">
            {t('warn.cancel')}
          </button>
          <button
            onClick={submit}
            disabled={saving || reason.trim().length < 3}
            className="rounded-lg bg-amber-600 px-5 py-2 text-sm font-semibold text-white transition hover:bg-amber-700 disabled:opacity-60"
          >
            {t('warn.submit')}
          </button>
        </div>
      </div>
    </div>
  )
}
