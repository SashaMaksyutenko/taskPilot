import { Eye, EyeOff } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { taskService } from '../services/taskService'
import type { TaskWatchers } from '../types/project'
import Avatar from './Avatar'

/**
 * Task "watchers" inside the task modal: a Watch/Watching toggle (subscribe to the task's
 * notifications without being its assignee) plus the avatars of everyone currently watching.
 * Any project member can watch.
 */
export default function TaskWatchersSection({ taskId }: { taskId: string }) {
  const { t } = useTranslation()
  const [state, setState] = useState<TaskWatchers | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    taskService.getWatchers(taskId).then(setState).catch(() => {})
  }, [taskId])

  const toggle = async () => {
    if (!state || busy) return
    setBusy(true)
    try {
      setState(state.isWatching ? await taskService.unwatch(taskId) : await taskService.watch(taskId))
    } catch {
      // Leave the current state on failure.
    } finally {
      setBusy(false)
    }
  }

  if (!state) return null

  return (
    <div>
      <div className="mb-2 flex items-center gap-2">
        <h3 className="font-bold">{t('watchers.title')}</h3>
        {state.watchers.length > 0 && (
          <span className="text-xs text-muted">{state.watchers.length}</span>
        )}
      </div>

      <button
        type="button"
        onClick={toggle}
        disabled={busy}
        className={`mb-2 inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-sm font-medium transition disabled:opacity-60 ${
          state.isWatching
            ? 'border-primary bg-primary-muted text-primary'
            : 'border-border text-muted hover:bg-canvas hover:text-foreground'
        }`}
      >
        {state.isWatching ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        {state.isWatching ? t('watchers.watching') : t('watchers.watch')}
      </button>

      {state.watchers.length === 0 ? (
        <p className="text-sm text-muted">{t('watchers.none')}</p>
      ) : (
        <ul className="flex flex-wrap gap-2">
          {state.watchers.map((w) => (
            <li key={w.userId} className="flex items-center gap-1.5 rounded-full bg-canvas py-0.5 pl-0.5 pr-2.5 text-xs">
              <Avatar name={w.name} src={w.avatarUrl} size={20} />
              <span className="truncate">{w.name}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
