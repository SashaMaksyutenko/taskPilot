import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import Avatar from '../Avatar'
import MentionText from '../MentionText'
import MentionField, { type MentionCandidate } from '../MentionField'
import Attachments from '../Attachments'
import { taskAttachments } from '../../services/attachmentSources'
import TaskHistory from '../TaskHistory'
import { apiErrorMessage } from '../../lib/apiError'
import { createTaskConnection } from '../../lib/taskHub'
import { REACTION_EMOJIS } from '../../lib/reactionEmojis'
import { projectService } from '../../services/projectService'
import { taskService, type ExtensionRequest } from '../../services/taskService'
import { userService, type UserSearchResult } from '../../services/userService'
import { RECURRENCE_OPTIONS, type Task, type TaskComment, type CommentReactionsUpdate } from '../../types/project'
import TaskDependenciesSection from '../TaskDependenciesSection'
import TaskWatchersSection from '../TaskWatchersSection'
import TaskCustomFieldsSection from '../TaskCustomFieldsSection'
import { sprintService } from '../../services/sprintService'
import { epicService } from '../../services/epicService'
import type { Sprint, Epic } from '../../types/project'

const PRIORITIES = ['Low', 'Medium', 'High']

/** Converts an ISO date string to the value a <input type="date"> expects (yyyy-MM-dd). */
function toDateInput(iso: string | null): string {
  return iso ? iso.slice(0, 10) : ''
}

/** Formats a duration in seconds as HH:MM:SS. */
function formatDuration(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = totalSeconds % 60
  return [h, m, s].map((n) => String(n).padStart(2, '0')).join(':')
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
  showHistory = false,
  isOwner = false,
  canWrite = false,
}: {
  task: Task
  onClose: () => void
  onSaved: (task: Task) => void
  onDeleted: (taskId: string) => void
  /** Opens the history section straight away (the "View history" menu action). */
  showHistory?: boolean
  /** Whether the current user owns the project — only the owner may change the deadline. */
  isOwner?: boolean
  /** Whether the current user may edit tasks (owner/Editor) — gates managing dependencies. */
  canWrite?: boolean
}) {
  const { t } = useTranslation()
  const [title, setTitle] = useState(task.title)
  const [description, setDescription] = useState(task.description ?? '')
  const [priority, setPriority] = useState(task.priority)
  const [deadline, setDeadline] = useState(toDateInput(task.deadline))
  const [recurrence, setRecurrence] = useState(task.recurrence ?? 'None')
  const [recurrenceInterval, setRecurrenceInterval] = useState(task.recurrenceInterval ?? 1)
  const [estimate, setEstimate] = useState(task.estimate != null ? String(task.estimate) : '')

  // Sprint the task belongs to (null = backlog), plus the project's sprints to pick from.
  const [sprints, setSprints] = useState<Sprint[]>([])
  const [sprintId, setSprintId] = useState<string>(task.sprintId ?? '')
  useEffect(() => {
    sprintService.list(task.projectId).then(setSprints).catch(() => {})
  }, [task.projectId])

  const changeSprint = async (value: string) => {
    setSprintId(value)
    await sprintService.assignTask(task.id, value || null).catch(() => {})
  }

  // Epic the task belongs to (null = ungrouped), plus the project's epics to pick from.
  const [epics, setEpics] = useState<Epic[]>([])
  const [epicId, setEpicId] = useState<string>(task.epicId ?? '')
  useEffect(() => {
    epicService.list(task.projectId).then(setEpics).catch(() => {})
  }, [task.projectId])

  const changeEpic = async (value: string) => {
    setEpicId(value)
    await epicService.assignTask(task.id, value || null).catch(() => {})
  }
  const [tags, setTags] = useState<string[]>(task.tags ?? [])
  const [tagInput, setTagInput] = useState('')

  // Deadline-extension requests for this task.
  const [extensions, setExtensions] = useState<ExtensionRequest[]>([])
  const [extDate, setExtDate] = useState('')
  const [extReason, setExtReason] = useState('')
  const [extError, setExtError] = useState('')
  useEffect(() => {
    taskService.getExtensionRequests(task.id).then(setExtensions).catch(() => {})
  }, [task.id])

  const submitExtension = async () => {
    setExtError('')
    if (!extDate) return
    try {
      // Send an end-of-day UTC time so a plain date still lands in the future.
      const created = await taskService.requestExtension(task.id, `${extDate}T23:59:59Z`, extReason.trim())
      setExtensions((prev) => [created, ...prev])
      setExtDate('')
      setExtReason('')
    } catch (e) {
      setExtError(apiErrorMessage(e))
    }
  }

  const decideExtension = async (requestId: string, approve: boolean) => {
    const updated = await taskService.decideExtension(requestId, approve).catch(() => null)
    if (updated) {
      setExtensions((prev) => prev.map((x) => (x.id === requestId ? updated : x)))
      // An approval moves the task deadline — reflect it in the editor.
      if (approve) setDeadline(toDateInput(updated.requestedDeadline))
    }
  }

  // Subtasks (children of this task).
  const [subtasks, setSubtasks] = useState<Task[]>([])
  const [newSubtask, setNewSubtask] = useState('')

  // Which comment's emoji picker is open (by comment id), if any.
  const [reactionPickerFor, setReactionPickerFor] = useState<string | null>(null)

  // AI subtask suggestions (shown only when the server has an LLM key configured).
  const [aiEnabled, setAiEnabled] = useState(false)
  const [aiLoading, setAiLoading] = useState(false)
  const [aiSuggestions, setAiSuggestions] = useState<string[]>([])
  const [aiSelected, setAiSelected] = useState<Set<string>>(new Set())
  useEffect(() => {
    taskService.aiEnabled().then(setAiEnabled).catch(() => setAiEnabled(false))
  }, [])

  const suggestSubtasks = async () => {
    setAiLoading(true)
    const suggestions = await taskService.suggestSubtasks(task.id).catch(() => null)
    setAiLoading(false)
    if (!suggestions) return
    setAiSuggestions(suggestions)
    setAiSelected(new Set(suggestions)) // pre-select all
  }

  const toggleSuggestion = (s: string) =>
    setAiSelected((prev) => {
      const next = new Set(prev)
      if (next.has(s)) next.delete(s)
      else next.add(s)
      return next
    })

  // Creates the picked suggestions as real subtasks, then clears the suggestion list.
  const addSelectedSuggestions = async () => {
    const chosen = aiSuggestions.filter((s) => aiSelected.has(s))
    const created = await Promise.all(
      chosen.map((title) =>
        taskService.createTask(task.projectId, { title, parentTaskId: task.id }).catch(() => null),
      ),
    )
    const ok = created.filter((t): t is Task => t !== null)
    if (ok.length) setSubtasks((prev) => [...prev, ...ok])
    setAiSuggestions([])
    setAiSelected(new Set())
  }

  // Time tracking: accumulated seconds + the start time of a running timer (if any).
  const [timeSpent, setTimeSpent] = useState(task.timeSpentSeconds)
  const [timerStartedAt, setTimerStartedAt] = useState<string | null>(task.timerStartedAt)
  const [now, setNow] = useState(Date.now())
  // Re-render every second while the timer runs so the display ticks.
  useEffect(() => {
    if (!timerStartedAt) return
    const id = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(id)
  }, [timerStartedAt])

  // Total seconds to show = stored time plus the current run (if running).
  const displaySeconds =
    timeSpent + (timerStartedAt ? Math.max(0, Math.floor((now - new Date(timerStartedAt).getTime()) / 1000)) : 0)

  const toggleTimer = async () => {
    const updated = timerStartedAt
      ? await taskService.stopTimer(task.id).catch(() => null)
      : await taskService.startTimer(task.id).catch(() => null)
    if (updated) {
      setTimeSpent(updated.timeSpentSeconds)
      setTimerStartedAt(updated.timerStartedAt)
      onSaved(updated)
    }
  }

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

  // Load the task's subtasks when the modal opens.
  useEffect(() => {
    taskService.getSubtasks(task.id).then(setSubtasks).catch(() => setSubtasks([]))
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
    connection.on('ReceiveCommentReaction', (update: CommentReactionsUpdate) => {
      setComments((prev) =>
        prev.map((c) => (c.id === update.commentId ? { ...c, reactions: update.reactions } : c)),
      )
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

  // Toggle an emoji reaction on a comment; the response carries the fresh reaction groups.
  const toggleCommentReaction = async (commentId: string, emoji: string) => {
    setReactionPickerFor(null)
    const reactions = await taskService.reactToComment(commentId, emoji).catch(() => null)
    if (reactions)
      setComments((prev) => prev.map((c) => (c.id === commentId ? { ...c, reactions } : c)))
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

  const addTag = () => {
    const tag = tagInput.trim()
    if (!tag) return
    // Case-insensitive dedupe; cap the list at 15.
    if (!tags.some((x) => x.toLowerCase() === tag.toLowerCase()) && tags.length < 15) {
      setTags([...tags, tag.slice(0, 30)])
    }
    setTagInput('')
  }

  const removeTag = (tag: string) => setTags(tags.filter((x) => x !== tag))

  const addSubtask = async () => {
    const title = newSubtask.trim()
    if (!title) return
    setNewSubtask('')
    const created = await taskService
      .createTask(task.projectId, { title, parentTaskId: task.id })
      .catch(() => null)
    if (created) setSubtasks((prev) => [...prev, created])
  }

  // Flip a subtask between Done and Backlog via the status endpoint.
  const toggleSubtask = async (sub: Task) => {
    const nextStatus = sub.status === 'Done' ? 'Backlog' : 'Done'
    const updated = await taskService.changeStatus(sub.id, nextStatus).catch(() => null)
    if (updated) setSubtasks((prev) => prev.map((s) => (s.id === updated.id ? updated : s)))
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
        tags,
        recurrence,
        recurrenceInterval,
        estimate: estimate.trim() === '' ? -1 : Math.max(0, Math.floor(Number(estimate) || 0)),
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
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl bg-surface p-6 shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('taskModal.title')}</h2>
          <button onClick={onClose} className="text-muted hover:text-foreground">
            ✕
          </button>
        </div>

        <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.titleField')}</label>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          className="mb-4 w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
        />

        <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.description')}</label>
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={3}
          className="mb-4 w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
        />

        <div className="mb-4 grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.priority')}</label>
            <select
              value={priority}
              onChange={(e) => setPriority(e.target.value)}
              className="w-full rounded-lg border border-border bg-canvas px-2 py-2 text-sm text-foreground outline-none"
            >
              {PRIORITIES.map((p) => (
                <option key={p} value={p}>
                  {t(`board.priority.${p}`, p)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.deadline')}</label>
            <input
              type="date"
              value={deadline}
              onChange={(e) => setDeadline(e.target.value)}
              disabled={!isOwner}
              title={!isOwner ? t('taskModal.deadlineOwnerOnly') : undefined}
              className="w-full rounded-lg border border-border bg-canvas px-2 py-2 text-sm text-foreground outline-none disabled:cursor-not-allowed disabled:opacity-60"
            />
            {!isOwner && (
              <p className="mt-1 text-xs text-muted">{t('taskModal.deadlineOwnerOnly')}</p>
            )}
          </div>
        </div>

        {/* Recurrence — when set, completing the task spawns the next occurrence. */}
        <div className="mb-4 grid grid-cols-2 gap-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.recurrence')}</label>
            <select
              value={recurrence}
              onChange={(e) => setRecurrence(e.target.value)}
              className="w-full rounded-lg border border-border bg-canvas px-2 py-2 text-sm text-foreground outline-none"
            >
              {RECURRENCE_OPTIONS.map((r) => (
                <option key={r} value={r}>
                  {t(`taskModal.recurrenceOption.${r}`, r)}
                </option>
              ))}
            </select>
          </div>
          {recurrence !== 'None' && (
            <div>
              <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.recurrenceEvery')}</label>
              <input
                type="number"
                min={1}
                value={recurrenceInterval}
                onChange={(e) => setRecurrenceInterval(Math.max(1, Number(e.target.value) || 1))}
                className="w-full rounded-lg border border-border bg-canvas px-2 py-2 text-sm text-foreground outline-none"
              />
            </div>
          )}
        </div>

        {/* Deadline-extension requests */}
        <label className="mb-1 block text-sm font-medium text-foreground">{t('extension.title')}</label>
        <div className="mb-4 space-y-2">
          {extensions.map((x) => (
            <div key={x.id} className="rounded-lg border border-border bg-canvas px-3 py-2 text-sm">
              <div className="flex items-center gap-2">
                <span className="font-medium">{x.requesterName}</span>
                <span className="text-muted">→ {new Date(x.requestedDeadline).toLocaleDateString()}</span>
                <span
                  className={`ml-auto rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                    x.status === 'Approved'
                      ? 'bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300'
                      : x.status === 'Rejected'
                        ? 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300'
                        : 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200'
                  }`}
                >
                  {t(`extension.status.${x.status}`, x.status)}
                </span>
              </div>
              {x.reason && <p className="mt-1 text-muted">{x.reason}</p>}
              {x.canDecide && (
                <div className="mt-2 flex gap-2">
                  <button
                    onClick={() => decideExtension(x.id, true)}
                    className="rounded-lg bg-green-600 px-3 py-1 text-xs font-semibold text-white hover:bg-green-700"
                  >
                    {t('extension.approve')}
                  </button>
                  <button
                    onClick={() => decideExtension(x.id, false)}
                    className="rounded-lg border border-border px-3 py-1 text-xs font-semibold text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20"
                  >
                    {t('extension.reject')}
                  </button>
                </div>
              )}
            </div>
          ))}

          {/* Request form (one open request at a time; backend enforces) */}
          <div className="flex flex-wrap items-end gap-2">
            <input
              type="date"
              value={extDate}
              onChange={(e) => setExtDate(e.target.value)}
              className="rounded-lg border border-border bg-canvas px-2 py-1.5 text-sm text-foreground outline-none"
            />
            <input
              value={extReason}
              onChange={(e) => setExtReason(e.target.value)}
              placeholder={t('extension.reasonPlaceholder')}
              className="min-w-0 flex-1 rounded-lg border border-border bg-canvas px-3 py-1.5 text-sm text-foreground outline-none focus:border-primary"
            />
            <button
              onClick={submitExtension}
              disabled={!extDate}
              className="rounded-lg bg-primary px-3 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
            >
              {t('extension.request')}
            </button>
          </div>
          {extError && <p className="text-xs text-red-600">{extError}</p>}
        </div>

        {/* Attached files */}
        <Attachments source={taskAttachments} ownerId={task.id} />

        {/* Task history (audit trail); loads only when expanded */}
        <TaskHistory taskId={task.id} defaultOpen={showHistory} />

        {/* Time tracking */}
        <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.timeTracking')}</label>
        <div className="mb-4 flex items-center gap-3">
          <span className={`font-mono text-lg font-bold ${timerStartedAt ? 'text-accent' : 'text-foreground'}`}>
            {formatDuration(displaySeconds)}
          </span>
          <button
            onClick={toggleTimer}
            className={`rounded-lg px-4 py-1.5 text-sm font-semibold text-white transition ${
              timerStartedAt ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'
            }`}
          >
            {timerStartedAt ? t('taskModal.stopTimer') : t('taskModal.startTimer')}
          </button>
        </div>

        {/* Tags */}
        <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.tags')}</label>
        <div className="mb-4">
          {tags.length > 0 && (
            <div className="mb-2 flex flex-wrap gap-1.5">
              {tags.map((tag) => (
                <span
                  key={tag}
                  className="flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary"
                >
                  {tag}
                  <button
                    onClick={() => removeTag(tag)}
                    className="text-muted hover:text-red-600"
                    title={t('taskModal.removeTag')}
                  >
                    ✕
                  </button>
                </span>
              ))}
            </div>
          )}
          <input
            value={tagInput}
            onChange={(e) => setTagInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ',') {
                e.preventDefault()
                addTag()
              } else if (e.key === 'Backspace' && !tagInput && tags.length > 0) {
                removeTag(tags[tags.length - 1])
              }
            }}
            onBlur={addTag}
            placeholder={t('taskModal.tagsPlaceholder')}
            className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
          />
        </div>

        {/* Assignee */}
        <label className="mb-1 block text-sm font-medium text-foreground">{t('taskModal.assignee')}</label>
        <div className="mb-4">
          <div className="mb-2 flex items-center gap-2 text-sm">
            {assigneeName ? (
              <>
                <span className="rounded bg-canvas px-2 py-0.5 font-medium">
                  @{assigneeName}
                </span>
                <button onClick={unassign} className="text-xs font-semibold text-red-600 hover:underline">
                  {t('taskModal.unassign')}
                </button>
              </>
            ) : (
              <span className="text-muted">{t('taskModal.unassigned')}</span>
            )}
          </div>

          <div className="relative">
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('taskModal.searchAssignee')}
              className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
            />
            {results.length > 0 && (
              <ul className="absolute left-0 right-0 top-full z-20 mt-1 max-h-56 overflow-y-auto rounded-lg border border-border bg-surface shadow-elevated">
                {results.map((u) => (
                  <li key={u.id}>
                    <button
                      onClick={() => pickAssignee(u)}
                      className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-canvas"
                    >
                      <Avatar name={u.name} src={u.avatarUrl} size={26} />
                      <span className="font-medium">{u.name}</span>
                      {u.title && <span className="ml-1 text-xs text-muted">{u.title}</span>}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* Sprint + story-point estimate */}
        <div className="mb-4 grid gap-3 border-t border-border pt-4 sm:grid-cols-2">
          {(sprints.length > 0 || canWrite) && (
            <div>
              <label className="mb-1 block text-sm font-medium">{t('sprints.field')}</label>
              <select
                value={sprintId}
                onChange={(e) => changeSprint(e.target.value)}
                disabled={!canWrite}
                className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary disabled:opacity-60"
              >
                <option value="">{t('sprints.backlog')}</option>
                {sprints.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium">{t('taskModal.estimate')}</label>
            <input
              type="number"
              min="0"
              value={estimate}
              onChange={(e) => setEstimate(e.target.value)}
              disabled={!canWrite}
              placeholder={t('taskModal.estimatePlaceholder')}
              className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary disabled:opacity-60"
            />
          </div>
          {(epics.length > 0 || canWrite) && (
            <div>
              <label className="mb-1 block text-sm font-medium">{t('epics.field')}</label>
              <select
                value={epicId}
                onChange={(e) => changeEpic(e.target.value)}
                disabled={!canWrite}
                className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary disabled:opacity-60"
              >
                <option value="">{t('epics.none')}</option>
                {epics.map((e) => <option key={e.id} value={e.id}>{e.title}</option>)}
              </select>
            </div>
          )}
        </div>

        {/* Dependencies (blocked-by / blocks) */}
        <div className="mb-4 border-t border-border pt-4">
          <TaskDependenciesSection taskId={task.id} projectId={task.projectId} canWrite={canWrite} />
        </div>

        {/* Watchers (subscribe to this task's notifications) */}
        <div className="mb-4 border-t border-border pt-4">
          <TaskWatchersSection taskId={task.id} />
        </div>

        {/* Custom fields (project-defined) */}
        <div className="mb-4 border-t border-border pt-4 empty:hidden">
          <TaskCustomFieldsSection taskId={task.id} canWrite={canWrite} />
        </div>

        {/* Subtasks */}
        <div className="mb-4 border-t border-border pt-4">
          <div className="mb-2 flex items-center justify-between">
            <label className="block text-sm font-medium text-foreground">
              {t('taskModal.subtasks')}{' '}
              {subtasks.length > 0 && (
                <span className="text-muted">
                  ({subtasks.filter((s) => s.status === 'Done').length}/{subtasks.length})
                </span>
              )}
            </label>
            {/* AI suggestions (only when the assistant is configured on the server) */}
            {aiEnabled && (
              <button
                type="button"
                onClick={suggestSubtasks}
                disabled={aiLoading}
                className="flex items-center gap-1 rounded-lg border border-border px-2 py-1 text-xs font-semibold text-primary hover:bg-canvas disabled:opacity-50"
              >
                ✨ {aiLoading ? t('taskModal.aiThinking') : t('taskModal.aiSuggest')}
              </button>
            )}
          </div>

          {/* AI suggestion picker */}
          {aiSuggestions.length > 0 && (
            <div className="mb-3 rounded-lg border border-primary/30 bg-primary/5 p-2">
              <p className="mb-1 px-1 text-xs text-muted">{t('taskModal.aiPick')}</p>
              <ul className="space-y-0.5">
                {aiSuggestions.map((s) => (
                  <li key={s} className="flex items-center gap-2 px-1 text-sm">
                    <input
                      type="checkbox"
                      checked={aiSelected.has(s)}
                      onChange={() => toggleSuggestion(s)}
                      className="h-4 w-4 accent-primary"
                    />
                    <span className="min-w-0 flex-1">{s}</span>
                  </li>
                ))}
              </ul>
              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  onClick={addSelectedSuggestions}
                  disabled={aiSelected.size === 0}
                  className="rounded-lg bg-primary px-2.5 py-1 text-xs font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
                >
                  {t('taskModal.aiAdd', { count: aiSelected.size })}
                </button>
                <button
                  type="button"
                  onClick={() => setAiSuggestions([])}
                  className="rounded-lg px-2.5 py-1 text-xs font-semibold text-muted hover:bg-canvas"
                >
                  {t('taskModal.cancel')}
                </button>
              </div>
            </div>
          )}

          {subtasks.length > 0 && (
            <ul className="mb-2 space-y-1">
              {subtasks.map((s) => (
                <li key={s.id} className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={s.status === 'Done'}
                    onChange={() => toggleSubtask(s)}
                    className="h-4 w-4 accent-primary"
                  />
                  <span className={s.status === 'Done' ? 'text-muted line-through' : ''}>{s.title}</span>
                </li>
              ))}
            </ul>
          )}

          <input
            value={newSubtask}
            onChange={(e) => setNewSubtask(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                addSubtask()
              }
            }}
            placeholder={t('taskModal.addSubtask')}
            className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
          />
        </div>

        {/* Comments */}
        <div className="mb-4 border-t border-border pt-4">
          <label className="mb-2 block text-sm font-medium text-foreground">
            {t('taskModal.comments')} {comments.length > 0 && `(${comments.length})`}
          </label>

          {comments.length === 0 ? (
            <p className="mb-3 text-sm text-muted">{t('taskModal.noComments')}</p>
          ) : (
            <ul className="mb-3 max-h-48 space-y-2 overflow-y-auto pr-1">
              {comments.map((c) => (
                <li key={c.id} className="group rounded-lg bg-canvas px-3 py-2 text-sm">
                  <div className="flex items-center justify-between">
                    <span className="flex items-center gap-2 font-medium">
                      <Avatar name={c.authorName} src={c.authorAvatarUrl} size={22} />
                      {c.authorName}
                    </span>
                    <div className="flex items-center gap-2">
                      <span className="text-xs text-muted">
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
                  <p className="mt-1 whitespace-pre-wrap break-words text-foreground">
                    <MentionText text={c.body} />
                  </p>

                  {/* Emoji reactions */}
                  <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                    {c.reactions.map((re) => (
                      <button
                        key={re.emoji}
                        onClick={() => toggleCommentReaction(c.id, re.emoji)}
                        className={`flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition ${
                          re.mine ? 'border-primary bg-primary/10 dark:border-border' : 'border-border hover:bg-surface'
                        }`}
                      >
                        <span>{re.emoji}</span>
                        <span className="font-semibold">{re.count}</span>
                      </button>
                    ))}
                    <div className="relative">
                      <button
                        onClick={() => setReactionPickerFor(reactionPickerFor === c.id ? null : c.id)}
                        className="rounded-full border border-border px-2 py-0.5 text-xs text-muted opacity-0 transition hover:bg-surface group-hover:opacity-100"
                        aria-label={t('taskModal.addReaction', 'Add reaction')}
                      >
                        🙂+
                      </button>
                      {reactionPickerFor === c.id && (
                        <div className="absolute z-10 mt-1 grid max-h-64 w-72 grid-cols-8 gap-0.5 overflow-y-auto rounded-lg border border-border bg-surface p-2 shadow-lg">
                          {REACTION_EMOJIS.map((e) => (
                            <button
                              key={e}
                              onClick={() => toggleCommentReaction(c.id, e)}
                              className="flex h-8 w-8 items-center justify-center rounded text-xl leading-none transition hover:scale-125 hover:bg-canvas"
                            >
                              {e}
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          )}

          <div className="flex items-start gap-2">
            <MentionField
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
              className="w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary"
            />
            <button
              onClick={addComment}
              disabled={posting || !newComment.trim()}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
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
            className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
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
