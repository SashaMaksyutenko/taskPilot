import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { taskService, type TaskHistoryEntry } from '../services/taskService'

/**
 * A task's history (audit trail): who created, edited, moved, rescheduled or
 * deleted it, newest first.
 *
 * Collapsed by default and loaded on the first expand, so opening a task does not
 * pay for a request nobody looked at. Lives in its own component rather than inside
 * TaskDetailModal, which is already long enough.
 */
export default function TaskHistory({
  taskId,
  defaultOpen = false,
}: {
  taskId: string
  /** Open (and therefore load) immediately — used by the "View history" menu action. */
  defaultOpen?: boolean
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(defaultOpen)
  const [entries, setEntries] = useState<TaskHistoryEntry[] | null>(null)
  const [failed, setFailed] = useState(false)

  // Which task we have already requested. A ref, not state: React StrictMode
  // double-invokes effects in dev and the second pass would still see the old
  // state value, firing the request twice.
  const loadedFor = useRef<string | null>(null)

  useEffect(() => {
    // Nothing to do until the section is opened; refetch if the modal is reused
    // for a different task.
    if (!open || loadedFor.current === taskId) return
    loadedFor.current = taskId

    setEntries(null)
    setFailed(false)
    taskService
      .getHistory(taskId)
      .then((list) => {
        if (loadedFor.current === taskId) setEntries(list)
      })
      .catch(() => {
        if (loadedFor.current === taskId) setFailed(true)
      })

    // A stale response is discarded by comparing the ref above rather than by a
    // cleanup flag: under StrictMode the first pass's cleanup would flip that flag
    // while the second pass skips re-fetching (the ref already matches), so the only
    // in-flight response would be thrown away and the section would load forever.
  }, [open, taskId])

  return (
    <div className="mb-4">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="flex w-full items-center gap-2 text-sm font-medium text-foreground"
      >
        <span className="text-muted">{open ? '▾' : '▸'}</span>
        {t('history.title')}
      </button>

      {open && (
        <div className="mt-2 rounded-lg border border-border bg-canvas px-3 py-2 text-sm">
          {failed && <p className="text-muted">{t('history.failed')}</p>}
          {!failed && entries === null && <p className="text-muted">{t('history.loading')}</p>}
          {!failed && entries?.length === 0 && <p className="text-muted">{t('history.empty')}</p>}

          {!!entries?.length && (
            <ul className="space-y-1">
              {entries.map((e) => (
                <li key={e.id} className="flex items-baseline gap-2 py-0.5">
                  <span className="flex-none font-medium">
                    {/* Action codes nest under history.action (e.g. "task.status.changed"
                        resolves to history.action.task.status.changed); an unknown code
                        falls back to the raw string rather than rendering the key. */}
                    {t(`history.action.${e.action}`, e.action)}
                  </span>
                  <span className="min-w-0 flex-1 truncate text-muted">
                    {/* The server renders details in English (e.g. "Status: Backlog → Done");
                        it is data, not a translatable label. */}
                    {e.details}
                  </span>
                  <span className="flex-none text-xs text-muted">
                    {e.actorName ?? t('history.system')} · {new Date(e.createdAt).toLocaleString()}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}
