import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { projectService } from '../services/projectService'
import { userService, type UserSearchResult } from '../services/userService'
import type { ProjectMember } from '../types/project'
import Avatar from './Avatar'

/**
 * Manage a project's collaborators. Everyone with access sees the roster; the owner
 * can add members (via user search) and remove them.
 */
export default function ProjectMembersModal({
  projectId,
  isOwner,
  currentUserId,
  onClose,
  onLeft,
}: {
  projectId: string
  isOwner: boolean
  currentUserId?: string
  onClose: () => void
  onLeft: () => void
}) {
  const { t } = useTranslation()
  const [members, setMembers] = useState<ProjectMember[]>([])
  const [search, setSearch] = useState('')
  const [results, setResults] = useState<UserSearchResult[]>([])

  const load = () => projectService.getMembers(projectId).then(setMembers).catch(() => {})
  useEffect(() => {
    load()
  }, [projectId])

  // Debounced user search for adding a collaborator.
  useEffect(() => {
    const term = search.trim()
    if (term.length < 2) {
      setResults([])
      return
    }
    const handle = setTimeout(() => {
      userService.searchUsers(term).then(setResults).catch(() => setResults([]))
    }, 300)
    return () => clearTimeout(handle)
  }, [search])

  const memberIds = new Set(members.map((m) => m.userId))
  const [roleToAdd, setRoleToAdd] = useState('Editor')

  const add = async (userId: string) => {
    await projectService.addMember(projectId, userId, roleToAdd).catch(() => {})
    setSearch('')
    setResults([])
    load()
  }

  const changeRole = async (userId: string, role: string) => {
    await projectService.setMemberRole(projectId, userId, role).catch(() => {})
    load()
  }

  const remove = async (userId: string) => {
    await projectService.removeMember(projectId, userId).catch(() => {})
    load()
  }

  // The current user is a collaborator (can leave) when present in the roster and not the owner.
  const canLeave = !isOwner && members.some((m) => m.userId === currentUserId)

  const leave = async () => {
    await projectService.leaveProject(projectId).catch(() => {})
    onLeft()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="max-h-[90vh] w-full max-w-md overflow-y-auto rounded-xl bg-white p-6 shadow-xl dark:bg-slate-800"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('members.title')}</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
            ✕
          </button>
        </div>

        <ul className="mb-4 space-y-2">
          {members.map((m) => (
            <li key={m.userId} className="flex items-center gap-2 text-sm">
              <Avatar name={m.name} src={m.avatarUrl} size={28} />
              <span className="min-w-0 flex-1 truncate font-medium">{m.name}</span>
              {m.isOwner ? (
                <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600 dark:bg-slate-700 dark:text-slate-300">
                  {t('members.owner')}
                </span>
              ) : isOwner ? (
                <>
                  <select
                    value={m.role}
                    onChange={(e) => changeRole(m.userId, e.target.value)}
                    className="rounded border border-slate-300 bg-white px-1.5 py-0.5 text-xs outline-none dark:border-slate-600 dark:bg-slate-900"
                  >
                    <option value="Editor">{t('members.role.Editor')}</option>
                    <option value="Viewer">{t('members.role.Viewer')}</option>
                  </select>
                  <button
                    onClick={() => remove(m.userId)}
                    className="text-xs font-semibold text-red-600 hover:underline"
                  >
                    {t('members.remove')}
                  </button>
                </>
              ) : (
                <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600 dark:bg-slate-700 dark:text-slate-300">
                  {t(`members.role.${m.role}`)}
                </span>
              )}
            </li>
          ))}
        </ul>

        {isOwner && (
          <div className="relative">
            <div className="mb-2 flex items-center gap-2 text-sm">
              <span className="text-slate-500 dark:text-slate-400">{t('members.addAs')}</span>
              <select
                value={roleToAdd}
                onChange={(e) => setRoleToAdd(e.target.value)}
                className="rounded border border-slate-300 bg-white px-2 py-1 text-xs outline-none dark:border-slate-600 dark:bg-slate-900"
              >
                <option value="Editor">{t('members.role.Editor')}</option>
                <option value="Viewer">{t('members.role.Viewer')}</option>
              </select>
            </div>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('members.searchPlaceholder')}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            {results.length > 0 && (
              <ul className="absolute left-0 right-0 top-full z-20 mt-1 max-h-56 overflow-y-auto rounded-lg border border-slate-300 bg-white shadow-lg dark:border-slate-600 dark:bg-slate-800">
                {results.map((u) => (
                  <li key={u.id}>
                    <button
                      onClick={() => add(u.id)}
                      disabled={memberIds.has(u.id)}
                      className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-slate-50 disabled:opacity-40 dark:hover:bg-slate-700"
                    >
                      <Avatar name={u.name} src={u.avatarUrl} size={24} />
                      <span className="min-w-0 flex-1 truncate">{u.name}</span>
                      {memberIds.has(u.id) && <span className="text-xs text-slate-400">{t('members.added')}</span>}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}

        {canLeave && (
          <button
            onClick={leave}
            className="mt-4 w-full rounded-lg border border-red-300 px-4 py-2 text-sm font-semibold text-red-600 transition hover:bg-red-50 dark:border-red-700 dark:hover:bg-red-950"
          >
            {t('members.leave')}
          </button>
        )}
      </div>
    </div>
  )
}
