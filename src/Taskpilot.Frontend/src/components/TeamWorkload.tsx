import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import Avatar from './Avatar'
import { taskService, type TeamMemberWorkload } from '../services/taskService'

// Same status palette the calendar uses, so a task reads the same everywhere.
const STATUS_COLORS: Record<string, string> = {
  Backlog: 'bg-border text-foreground',
  InProgress: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-950/50 dark:text-indigo-300',
  Review: 'bg-amber-100 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300',
  Done: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300',
}

/**
 * Team availability for a project: each participant with the tasks assigned to them that
 * fall due soon, so you see who is busy and who is free. Read-only; anyone with access to
 * the project can view it.
 */
export default function TeamWorkload({ projectId }: { projectId: string }) {
  const { t } = useTranslation()
  const [team, setTeam] = useState<TeamMemberWorkload[] | null>(null)
  const [failed, setFailed] = useState(false)

  // A ref (not state) so React StrictMode's double effect does not fetch twice; refetch
  // if the component is reused for a different project.
  const loadedFor = useRef<string | null>(null)
  useEffect(() => {
    if (loadedFor.current === projectId) return
    loadedFor.current = projectId
    setTeam(null)
    setFailed(false)
    taskService
      .getTeamWorkload(projectId)
      .then((rows) => {
        if (loadedFor.current === projectId) setTeam(rows)
      })
      .catch(() => {
        if (loadedFor.current === projectId) setFailed(true)
      })
  }, [projectId])

  if (failed) return <p className="text-sm text-muted">{t('team.failed')}</p>
  if (team === null) return <p className="text-sm text-muted">{t('team.loading')}</p>

  return (
    <div className="space-y-3">
      {team.map((member) => (
        <div key={member.userId} className="rounded-xl border border-border bg-surface p-4">
          <div className="mb-3 flex items-center gap-3">
            <Avatar name={member.name} src={member.avatarUrl} size={32} />
            <span className="font-semibold text-foreground">{member.name}</span>
            {member.isOwner && (
              <span className="rounded-full bg-primary-muted px-2 py-0.5 text-xs font-medium text-primary">
                {t('team.owner')}
              </span>
            )}
            <span className="ml-auto text-xs text-muted">
              {t('team.taskCount', { count: member.tasks.length })}
            </span>
          </div>

          {member.tasks.length === 0 ? (
            <p className="text-sm text-muted">{t('team.free')}</p>
          ) : (
            <ul className="space-y-1.5">
              {member.tasks.map((task) => (
                <li key={task.id} className="flex items-center gap-3 text-sm">
                  <span
                    className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                      STATUS_COLORS[task.status] ?? 'bg-border text-foreground'
                    }`}
                  >
                    {t(`board.status.${task.status}`, task.status)}
                  </span>
                  <span className="min-w-0 flex-1 truncate text-foreground">{task.title}</span>
                  <span className="flex-none text-xs text-muted">
                    {new Date(task.deadline).toLocaleDateString()}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      ))}
    </div>
  )
}
