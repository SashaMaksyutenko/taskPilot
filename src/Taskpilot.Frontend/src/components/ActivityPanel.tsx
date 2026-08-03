import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Avatar from './Avatar'
import EmptyState from './feedback/EmptyState'
import { activityService, type ActivityEntry } from '../services/activityService'

/** Dotted audit action → an i18n verb key (dots aren't valid in nested keys). */
const ACTION_KEY: Record<string, string> = {
  'task.created': 'created',
  'task.updated': 'updated',
  'task.status.changed': 'statusChanged',
  'task.rescheduled': 'rescheduled',
  'task.moved': 'moved',
  'task.deleted': 'deleted',
}

/** A coloured dot hinting at the kind of action. */
function ActionDot({ action }: { action: string }) {
  const color =
    action === 'task.created' ? 'bg-green-500'
      : action === 'task.deleted' ? 'bg-red-500'
        : action === 'task.status.changed' ? 'bg-blue-500'
          : 'bg-slate-400'
  return <span className={`mt-1.5 h-2 w-2 flex-none rounded-full ${color}`} />
}

/**
 * Project activity feed (a board view): a timeline of recent task actions from the audit
 * trail — who did what and when — newest first. Distinct from personal notifications.
 */
export default function ActivityPanel({ projectId }: { projectId: string }) {
  const { t } = useTranslation()
  const [entries, setEntries] = useState<ActivityEntry[] | null>(null)

  useEffect(() => {
    activityService.get(projectId).then(setEntries).catch(() => setEntries([]))
  }, [projectId])

  if (entries === null) return <p className="mx-auto max-w-2xl text-sm text-muted">{t('common.loading', 'Loading…')}</p>
  if (entries.length === 0) return <div className="mx-auto max-w-2xl"><EmptyState message={t('activity.empty')} /></div>

  return (
    <div className="mx-auto max-w-2xl">
      <ul className="space-y-1">
        {entries.map((e) => {
          const verb = t(`activity.action.${ACTION_KEY[e.action] ?? 'updated'}`)
          const row = (
            <div className="flex items-start gap-3 rounded-lg px-3 py-2.5 transition hover:bg-canvas">
              <ActionDot action={e.action} />
              <Avatar name={e.actorName} src={e.actorAvatarUrl} size={26} />
              <div className="min-w-0 flex-1">
                <p className="text-sm">
                  <span className="font-medium">{e.actorName}</span> <span className="text-muted">{verb}</span>
                </p>
                {e.details && <p className="truncate text-xs text-muted">{e.details}</p>}
              </div>
              <span className="flex-none text-xs text-muted">{new Date(e.createdAt).toLocaleString()}</span>
            </div>
          )
          return (
            <li key={e.id}>
              {e.taskId ? <Link to={`/projects/${projectId}?task=${e.taskId}`}>{row}</Link> : row}
            </li>
          )
        })}
      </ul>
    </div>
  )
}
