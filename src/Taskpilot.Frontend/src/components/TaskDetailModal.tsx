import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import Avatar from './Avatar'
import MentionText from './MentionText'
import MentionTextarea, { type MentionCandidate } from './MentionTextarea'
import { apiErrorMessage } from '../lib/apiError'
import { createTaskConnection } from '../lib/taskHub'
import { projectService } from '../services/projectService'
import { taskService } from '../services/taskService'
import { userService, type UserSearchResult } from '../services/userService'
import type { Task, TaskComment } from '../types/project'

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

  // Comments thread.
  const [comments, setComments] = useState<TaskComment[]>([])
  const [newComment, setNewComment] = useState('')
  const [posting, setPosting] = useState(false)
  const [commentError, setCommentError] = useState('')

  // Project members — used as @mention candidates.
  const [mentionCandidates, setMentionCandidates] = useState<MentionCandidate[]>([])
  useEffect(() => {
    projectService
      .getMembers(task.projectId)
      .then((ms) => setMentionCandidates(ms.map((m) => ({ id: m.userId, name: m.name, avatarUrl: m.avatarUrl }))))
      .catch(() => {})
  }, [task.projectId])

  // Load the task's comments when the modal opens.
  useEffect(() => {
    taskService.getComments(task.id).then(setComments).catch(() => setComments([]))
  }, [task.id])

  // Subscribe to real-time comment updates for this task (other collaborators).
  useEffect(() => {
    const connection = createTaskConnection()

    connection.on('ReceiveComment', (comment: TaskComment) => {
      // Append only if we don't already have it (the author appended locally).
      setComments((prev) => (prev.some((c) => c.id === comment.id) ? prev : [...prev, comment]))
    })
    connection.on('RemoveComment', (commentId: string) => {
      setComments((prev) => prev.filter((c) => c.id !== commentId))
    })

    connection
      .start()
      .then(() => connection.invoke('JoinTask', task.id))
      .catch(() => {})

    return () => {
      connection.stop().catch(() => {})
    }
  }, [task.id])

  const addComment = async () => {
    const body = newComment.trim()
    if (!body || posting) return
    setPosting(true)
    setCommentError('')
    try {
      const created = await taskService.addComment(task.id, body)
      // The real-time broadcast may have already appended this comment (race), so dedupe by id.
      setComments((prev) => (prev.some((c) => c.id === created.id) ? prev : [...prev, created]))
      setNewComment('')
    } catch (e) {
      setCommentError(apiErrorMessage(e))
    } finally {
      setPosting(false)
    }
  }

  const removeComment = async (id: string) => {
    await taskService.deleteComment(id).catch(() => {})
    setComments((prev) => prev.filter((c) => c.id !== id))
  }

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
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl bg-white p-6 shadow-xl dark:bg-slate-800"
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
                      className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-slate-50 dark:hover:bg-slate-700"
                    >
                      <Avatar name={u.name} src={u.avatarUrl} size={26} />
                      <span className="font-medium">{u.name}</span>
                      {u.title && <span className="ml-1 text-xs text-slate-400">{u.title}</span>}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* Comments */}
        <div className="mb-4 border-t border-slate-200 pt-4 dark:border-slate-700">
          <label className="mb-2 block text-sm font-medium text-slate-700 dark:text-slate-300">
            {t('taskModal.comments')} {comments.length > 0 && `(${comments.length})`}
          </label>

          {comments.length === 0 ? (
            <p className="mb-3 text-sm text-slate-400">{t('taskModal.noComments')}</p>
          ) : (
            <ul className="mb-3 max-h-48 space-y-2 overflow-y-auto pr-1">
              {comments.map((c) => (
                <li key={c.id} className="group rounded-lg bg-slate-50 px-3 py-2 text-sm dark:bg-slate-900">
                  <div className="flex items-center justify-between">
                    <span className="flex items-center gap-2 font-medium">
                      <Avatar name={c.authorName} src={c.authorAvatarUrl} size={22} />
                      {c.authorName}
                    </span>
                    <div className="flex items-center gap-2">
                      <span className="text-xs text-slate-400">
                        {new Date(c.createdAt).toLocaleString()}
                      </span>
                      <button
                        onClick={() => removeComment(c.id)}
                        className="text-xs font-semibold text-red-600 opacity-0 transition group-hover:opacity-100 hover:underline"
                        title={t('taskModal.deleteComment')}
                      >
                        ✕
                      </button>
                    </div>
                  </div>
                  <p className="mt-1 whitespace-pre-wrap break-words text-slate-600 dark:text-slate-300">
                    <MentionText text={c.body} />
                  </p>
                </li>
              ))}
            </ul>
          )}

          <div className="flex items-start gap-2">
            <MentionTextarea
              value={newComment}
              onChange={setNewComment}
              candidates={mentionCandidates}
              onKeyDown={(e) => {
                // Ctrl/Cmd+Enter posts the comment.
                if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
                  e.preventDefault()
                  addComment()
                }
              }}
              rows={2}
              placeholder={t('taskModal.commentPlaceholder')}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            <button
              onClick={addComment}
              disabled={posting || !newComment.trim()}
              className="rounded-lg bg-[#1E2A44] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
            >
              {t('taskModal.addComment')}
            </button>
          </div>
          {commentError && <p className="mt-2 text-sm font-medium text-red-600">{commentError}</p>}
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
