import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { taskService } from '../services/taskService'
import { userService, type UserSearchResult } from '../services/userService'
import type { Task } from '../types/project'

const PRIORITIES = ['Low', 'Medium', 'High']

/** Converts an ISO date string to the value a <input type="date"> expects (yyyy-MM-dd). */
function toDateInput(iso: string | null): string {
  return iso ? iso.slice(0, 10) : ''
}

/**
 * Modal to view and edit a single task: title, description, priority, assignee
 * (picked via user search) and deadline. Also supports deleting the task.
 */
export default function TaskDetailModal({
  task,
  onClose,
  onSaved,
  onDeleted,
}: {
  task: Task
  onClose: () => void
  onSaved: (task: Task) => void
  onDeleted: (taskId: string) => void
}) {
  const { t } = useTranslation()
  const [title, setTitle] = useState(task.title)
  const [description, setDescription] = useState(task.description ?? '')
  const [priority, setPriority] = useState(task.priority)
  const [deadline, setDeadline] = useState(toDateInput(task.deadline))

  // Assignee: track id + display name; null when unassigned.
  const [assigneeId, setAssigneeId] = useState<string | null>(task.assigneeId)
  const [assigneeName, setAssigneeName] = useState<string | null>(task.assigneeName)

  const [search, setSearch] = useState('')
  const [results, setResults] = useState<UserSearchResult[]>([])
  const [saving, setSaving] = useState(false)

  // Debounced user search for picking an assignee.
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

  const pickAssignee = (u: UserSearchResult) => {
    setAssigneeId(u.id)
    setAssigneeName(u.name)
    setSearch('')
    setResults([])
  }

  const unassign = () => {
    setAssigneeId(null)
    setAssigneeName(null)
  }

  const save = async () => {
    const trimmed = title.trim()
    if (!trimmed) return
    setSaving(true)
    try {
      const updated = await taskService.updateTask(task.id, {
        title: trimmed,
        description: description.trim() || null,
        priority,
        assigneeId,
        deadline: deadline ? new Date(deadline).toISOString() : null,
      })
      onSaved(updated)
      onClose()
    } finally {
      setSaving(false)
    }
  }

  const remove = async () => {
    await taskService.deleteTask(task.id).catch(() => {})
    onDeleted(task.id)
    onClose()
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-lg rounded-xl bg-white p-6 shadow-xl dark:bg-slate-800"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('taskModal.title')}</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
            ✕
          </button>
        </div>

        <label className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{t('taskModal.titleField')}</label>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          className="mb-4 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
        />

        <label className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{t('taskModal.description')}</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={3}
          className="mb-4 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
        />

        <div className="mb-4 grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{t('taskModal.priority')}</label>
            <select
              value={priority}
              onChange={(e) => setPriority(e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-2 py-2 text-sm outline-none dark:border-slate-600 dark:bg-slate-900"
            >
              {PRIORITIES.map((p) => (
                <option key={p} value={p}>
                  {t(`board.priority.${p}`, p)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{t('taskModal.deadline')}</label>
            <input
              type="date"
              value={deadline}
              onChange={(e) => setDeadline(e.target.value)}
              className="w-full rounded-lg border border-slate-300 bg-white px-2 py-2 text-sm outline-none dark:border-slate-600 dark:bg-slate-900"
            />
          </div>
        </div>

        {/* Assignee */}
        <label className="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300">{t('taskModal.assignee')}</label>
        <div className="mb-4">
          <div className="mb-2 flex items-center gap-2 text-sm">
            {assigneeName ? (
              <>
                <span className="rounded bg-slate-100 px-2 py-0.5 font-medium dark:bg-slate-700">
                  @{assigneeName}
                </span>
                <button onClick={unassign} className="text-xs font-semibold text-red-600 hover:underline">
                  {t('taskModal.unassign')}
                </button>
              </>
            ) : (
              <span className="text-slate-400">{t('taskModal.unassigned')}</span>
            )}
          </div>

          <div className="relative">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('taskModal.searchAssignee')}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            {results.length > 0 && (
              <ul className="absolute left-0 right-0 top-full z-20 mt-1 max-h-56 overflow-y-auto rounded-lg border border-slate-300 bg-white shadow-lg dark:border-slate-600 dark:bg-slate-800">
                {results.map((u) => (
                  <li key={u.id}>
                    <button
                      onClick={() => pickAssignee(u)}
                      className="block w-full px-3 py-2 text-left text-sm hover:bg-slate-50 dark:hover:bg-slate-700"
                    >
                      <span className="font-medium">{u.name}</span>
                      {u.title && <span className="ml-2 text-xs text-slate-400">{u.title}</span>}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={save}
            disabled={saving}
            className="rounded-lg bg-[#1E2A44] px-5 py-2 text-sm font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
          >
            {t('taskModal.save')}
          </button>
          <button
            onClick={remove}
            className="text-sm font-semibold text-red-600 hover:underline"
          >
            {t('taskModal.deleteTask')}
          </button>
        </div>
      </div>
    </div>
  )
}
