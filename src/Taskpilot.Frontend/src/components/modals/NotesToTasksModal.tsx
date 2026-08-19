import { Sparkles, X } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../../lib/apiError'
import { notify } from '../../lib/toast'
import { taskService } from '../../services/taskService'

interface Item {
  id: string
  title: string
  checked: boolean
}

/**
 * Paste meeting notes → the AI extracts action items → the user picks which to create as tasks in
 * the project's Backlog. A Pro feature (the extract endpoint is plan-gated). See {@link taskService}.
 */
export default function NotesToTasksModal({
  projectId,
  onClose,
  onCreated,
}: {
  projectId: string
  onClose: () => void
  onCreated: () => void
}) {
  const { t } = useTranslation()
  const [notes, setNotes] = useState('')
  const [items, setItems] = useState<Item[] | null>(null)
  const [extracting, setExtracting] = useState(false)
  const [creating, setCreating] = useState(false)

  const extract = async () => {
    if (!notes.trim()) return
    setExtracting(true)
    try {
      const titles = await taskService.extractTasksFromNotes(projectId, notes)
      setItems(titles.map((title) => ({ id: crypto.randomUUID(), title, checked: true })))
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setExtracting(false)
    }
  }

  const selected = items?.filter((i) => i.checked && i.title.trim()) ?? []

  const create = async () => {
    if (selected.length === 0) return
    setCreating(true)
    try {
      for (const item of selected) await taskService.createTask(projectId, { title: item.title.trim() })
      notify.success(t('notesToTasks.created', { count: selected.length }))
      onCreated()
      onClose()
    } catch (e) {
      notify.error(apiErrorMessage(e))
    } finally {
      setCreating(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="flex max-h-[85vh] w-full max-w-lg flex-col rounded-[var(--radius-card)] border border-border bg-surface shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-border px-5 py-3">
          <h2 className="flex items-center gap-2 font-bold">
            <Sparkles className="h-4 w-4 text-primary" />
            {t('notesToTasks.title')}
          </h2>
          <button onClick={onClose} className="text-muted hover:text-foreground" aria-label={t('notesToTasks.close')}>
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          <p className="mb-2 text-sm text-muted">{t('notesToTasks.hint')}</p>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={6}
            placeholder={t('notesToTasks.placeholder')}
            className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary"
          />
          <button
            onClick={extract}
            disabled={extracting || !notes.trim()}
            className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
          >
            <Sparkles className="h-4 w-4" />
            {extracting ? t('notesToTasks.extracting') : t('notesToTasks.extract')}
          </button>

          {items && (
            <div className="mt-4">
              {items.length === 0 ? (
                <p className="text-sm text-muted">{t('notesToTasks.none')}</p>
              ) : (
                <>
                  <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">
                    {t('notesToTasks.review')}
                  </p>
                  <ul className="space-y-1.5">
                    {items.map((item) => (
                      <li key={item.id} className="flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={item.checked}
                          onChange={(e) =>
                            setItems((prev) => prev!.map((x) => (x.id === item.id ? { ...x, checked: e.target.checked } : x)))
                          }
                          className="h-4 w-4 flex-none accent-primary"
                        />
                        <input
                          value={item.title}
                          onChange={(e) =>
                            setItems((prev) => prev!.map((x) => (x.id === item.id ? { ...x, title: e.target.value } : x)))
                          }
                          className="min-w-0 flex-1 rounded border border-transparent bg-canvas px-2 py-1 text-sm outline-none focus:border-primary"
                        />
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </div>
          )}
        </div>

        <div className="flex items-center justify-end gap-2 border-t border-border px-5 py-3">
          <button onClick={onClose} className="text-sm font-semibold text-muted hover:text-foreground">
            {t('notesToTasks.cancel')}
          </button>
          <button
            onClick={create}
            disabled={creating || selected.length === 0}
            className="rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
          >
            {t('notesToTasks.create', { count: selected.length })}
          </button>
        </div>
      </div>
    </div>
  )
}
