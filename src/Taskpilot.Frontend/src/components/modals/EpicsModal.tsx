import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../../lib/apiError'
import { epicService } from '../../services/epicService'
import type { Epic } from '../../types/project'

const PRESET_COLORS = ['#8b5cf6', '#3b82f6', '#10b981', '#f59e0b', '#ef4444', '#ec4899', '#14b8a6', '#64748b']

/**
 * Manage a project's epics (add/remove). Shown from the board; owner/Editors. Deleting an epic
 * ungroups its tasks. Epics are a theme-based grouping that can span several sprints.
 */
export default function EpicsModal({ projectId, onClose }: { projectId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [epics, setEpics] = useState<Epic[]>([])
  const [title, setTitle] = useState('')
  const [color, setColor] = useState(PRESET_COLORS[0])
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = () => epicService.list(projectId).then(setEpics).catch(() => {})
  useEffect(() => {
    load()
  }, [projectId])

  const add = async () => {
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      await epicService.create(projectId, { title: title.trim(), color })
      setTitle('')
      load()
    } catch (e) {
      setError(apiErrorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  const remove = async (id: string) => {
    await epicService.remove(id).catch(() => {})
    load()
  }

  const inputCls = 'w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl bg-surface p-6 shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-1 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('epics.title')}</h2>
          <button onClick={onClose} className="text-muted hover:text-foreground">✕</button>
        </div>
        <p className="mb-4 text-xs text-muted">{t('epics.subtitle')}</p>

        {/* Existing epics */}
        {epics.length === 0 ? (
          <p className="mb-4 text-sm text-muted">{t('epics.empty')}</p>
        ) : (
          <ul className="mb-4 space-y-1.5">
            {epics.map((e) => (
              <li key={e.id} className="flex items-center gap-2 rounded-lg bg-canvas px-3 py-2 text-sm">
                <span className="h-3 w-3 flex-none rounded-full" style={{ background: e.color ?? '#64748b' }} />
                <span className="flex-1 truncate font-medium">{e.title}</span>
                <span className="rounded-full bg-border/60 px-2 py-0.5 text-xs text-muted">{e.doneCount}/{e.taskCount}</span>
                <button
                  onClick={() => remove(e.id)}
                  className="text-xs font-semibold text-red-600 hover:underline"
                  aria-label={t('epics.remove')}
                >
                  ✕
                </button>
              </li>
            ))}
          </ul>
        )}

        {/* Add an epic */}
        <div className="space-y-2 border-t border-border pt-4">
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t('epics.namePlaceholder')}
            className={inputCls}
          />
          <div className="flex flex-wrap items-center gap-1.5">
            {PRESET_COLORS.map((c) => (
              <button
                key={c}
                type="button"
                onClick={() => setColor(c)}
                aria-label={c}
                className={`h-6 w-6 rounded-full transition ${color === c ? 'ring-2 ring-offset-2 ring-offset-surface ring-foreground' : ''}`}
                style={{ background: c }}
              />
            ))}
          </div>
          {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
          <button
            onClick={add}
            disabled={saving || !title.trim()}
            className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
          >
            {t('epics.add')}
          </button>
        </div>
      </div>
    </div>
  )
}
