import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import TaskActionsDropdown from '../components/TaskActionsDropdown'
import TaskContextMenu from '../components/TaskContextMenu'
import TaskDetailModal from '../components/TaskDetailModal'
import ProjectMembersModal from '../components/ProjectMembersModal'
import ConfirmDialog from '../components/ConfirmDialog'
import Button from '../components/ui/Button'
import Input from '../components/ui/Input'
import Confetti from '../components/Confetti'
import ResultState from '../components/ResultState'
import { bookmarkService } from '../services/bookmarkService'
import { useAppSelector } from '../store/hooks'
import { projectService } from '../services/projectService'
import { taskService } from '../services/taskService'
import { notify } from '../lib/toast'
import { STATUS_COLUMNS, type Project, type Task, type TaskStatus } from '../types/project'

const priorityClasses: Record<string, string> = {
  High: 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300',
  Medium: 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300',
  Low: 'bg-border text-muted',
}

const columnAccent: Record<string, string> = {
  Backlog: 'border-t-slate-400',
  InProgress: 'border-t-primary',
  Review: 'border-t-amber-500',
  Done: 'border-t-emerald-500',
}

// Matching dot color for each column header.
const columnDot: Record<string, string> = {
  Backlog: 'bg-slate-400',
  InProgress: 'bg-primary',
  Review: 'bg-amber-500',
  Done: 'bg-emerald-500',
}

/**
 * Kanban board for one project. Tasks are grouped into columns by status and can
 * be dragged between columns (native HTML5 drag & drop), which calls the status API.
 */
export default function BoardPage() {
  const { t } = useTranslation()
  const { projectId = '' } = useParams()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [project, setProject] = useState<Project | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [tasks, setTasks] = useState<Task[]>([])
  const [newTitle, setNewTitle] = useState('')
  const [selectedTask, setSelectedTask] = useState<Task | null>(null)
  const [membersOpen, setMembersOpen] = useState(false)
  const [canWrite, setCanWrite] = useState(true)
  // Other projects this task can be moved to (owned/active, excluding the current one).
  const [moveTargets, setMoveTargets] = useState<{ id: string; name: string }[]>([])
  // Project members a task can be assigned to (owner + collaborators).
  const [members, setMembers] = useState<{ id: string; name: string }[]>([])
  // Tags currently used to filter the board (empty = show everything).
  const [activeTags, setActiveTags] = useState<string[]>([])
  // Ids of tasks selected for a bulk action.
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  // Task awaiting delete confirmation.
  const [deletingTask, setDeletingTask] = useState<Task | null>(null)
  const draggingId = useRef<string | null>(null)
  // Celebratory confetti shown briefly when a task is moved to Done.
  const [showConfetti, setShowConfetti] = useState(false)

  const isOwner = !!project && project.ownerId === currentUserId

  useEffect(() => {
    if (!projectId) return
    // A deleted/inaccessible project 404s — show a clean "not found" instead of a blank board.
    projectService.getProject(projectId).then(setProject).catch(() => setNotFound(true))
    taskService.getTasks(projectId).then(setTasks).catch(() => {})
    // Other active projects the user owns can receive moved tasks.
    projectService
      .getProjects()
      .then((ps) =>
        setMoveTargets(ps.filter((p) => p.id !== projectId).map((p) => ({ id: p.id, name: p.name }))),
      )
      .catch(() => {})
    // Determine my write permission (owner or Editor member; Viewers are read-only).
    projectService
      .getMembers(projectId)
      .then((ms) => {
        const me = ms.find((m) => m.userId === currentUserId)
        setCanWrite(!!me && (me.isOwner || me.role === 'Editor'))
        setMembers(ms.map((m) => ({ id: m.userId, name: m.name })))
      })
      .catch(() => {})
  }, [projectId, currentUserId])

  // Open a task's details when arriving via a shared "?task=" deep link.
  useEffect(() => {
    const taskId = searchParams.get('task')
    if (!taskId || tasks.length === 0) return
    const target = tasks.find((t) => t.id === taskId)
    if (target) {
      setSelectedTask(target)
      // Drop the query param so refresh/close doesn't re-open it.
      searchParams.delete('task')
      setSearchParams(searchParams, { replace: true })
    }
  }, [tasks, searchParams, setSearchParams])

  const addTask = async () => {
    const title = newTitle.trim()
    if (!title) return
    setNewTitle('')
    const created = await taskService.createTask(projectId, { title })
    setTasks((prev) => [...prev, created])
  }

  const onDrop = async (status: TaskStatus, taskId: string) => {
    const task = tasks.find((t) => t.id === taskId)
    if (!task || task.status === status) return
    setTasks((prev) => prev.map((t) => (t.id === taskId ? { ...t, status } : t)))
    // Celebrate finishing a task.
    if (status === 'Done') setShowConfetti(true)
    try {
      await taskService.changeStatus(taskId, status)
    } catch {
      taskService.getTasks(projectId).then(setTasks).catch(() => {})
    }
  }

  // Context-menu actions.
  const changePriority = async (task: Task, priority: string) => {
    const updated = await taskService
      .updateTask(task.id, {
        title: task.title,
        description: task.description,
        priority,
        assigneeId: task.assigneeId,
        deadline: task.deadline,
      })
      .catch(() => null)
    if (updated) setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)))
  }

  const removeTask = async (task: Task) => {
    await taskService.deleteTask(task.id).catch(() => {})
    setTasks((prev) => prev.filter((t) => t.id !== task.id))
  }

  const duplicateTask = async (task: Task) => {
    const copy = await taskService.duplicateTask(task.id).catch(() => null)
    if (copy) {
      setTasks((prev) => [...prev, copy])
      notify.success(t('toast.taskDuplicated'))
    }
  }

  const moveTask = async (task: Task, targetProjectId: string) => {
    const moved = await taskService.moveTask(task.id, targetProjectId).catch(() => null)
    // The task (and its subtasks) left this board.
    if (moved) {
      setTasks((prev) => prev.filter((t) => t.id !== task.id && t.parentTaskId !== task.id))
      notify.success(t('toast.taskMoved'))
    }
  }

  const copyLink = (task: Task) => {
    // A shareable deep link that opens this task's details on the board.
    const url = `${window.location.origin}/projects/${projectId}?task=${task.id}`
    navigator.clipboard?.writeText(url).catch(() => {})
    notify.success(t('toast.linkCopied'))
  }

  // Bookmarked task ids (for the context-menu label).
  const [bookmarkedTaskIds, setBookmarkedTaskIds] = useState<Set<string>>(new Set())
  useEffect(() => {
    bookmarkService
      .getMine()
      .then((bs) => setBookmarkedTaskIds(new Set(bs.filter((b) => b.type === 'Task').map((b) => b.entityId))))
      .catch(() => {})
  }, [])

  const toggleBookmark = async (task: Task) => {
    const now = await bookmarkService
      .toggle({ type: 'Task', entityId: task.id, title: task.title, link: `/projects/${projectId}?task=${task.id}` })
      .catch(() => null)
    if (now === null) return
    setBookmarkedTaskIds((prev) => {
      const next = new Set(prev)
      if (now) next.add(task.id)
      else next.delete(task.id)
      return next
    })
    notify.success(now ? t('bookmarks.added') : t('bookmarks.removed'))
  }

  const assign = async (task: Task, assigneeId: string | null) => {
    const updated = await taskService
      .updateTask(task.id, {
        title: task.title,
        description: task.description,
        priority: task.priority,
        assigneeId,
        deadline: task.deadline,
        tags: task.tags,
      })
      .catch(() => null)
    if (updated) {
      setTasks((prev) => prev.map((x) => (x.id === updated.id ? updated : x)))
      notify.success(t('toast.taskAssigned'))
    }
  }

  // Bulk selection helpers.
  const toggleSelected = (id: string) =>
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const clearSelection = () => setSelectedIds(new Set())

  const bulkStatus = async (status: TaskStatus) => {
    const ids = [...selectedIds]
    if (ids.length === 0) return
    const res = await taskService.bulkChangeStatus(ids, status).catch(() => null)
    clearSelection()
    taskService.getTasks(projectId).then(setTasks).catch(() => {})
    if (res) notify.success(t('toast.tasksUpdated', { count: res.changed }))
  }

  const bulkDelete = async () => {
    const ids = [...selectedIds]
    if (ids.length === 0) return
    const res = await taskService.bulkDelete(ids).catch(() => null)
    setTasks((prev) => prev.filter((t) => !selectedIds.has(t.id)))
    clearSelection()
    if (res) notify.success(t('toast.tasksDeleted', { count: res.deleted }))
  }

  // The board shows only top-level tasks; subtasks are managed inside their parent.
  const topLevelTasks = tasks.filter((t) => !t.parentTaskId)

  // All distinct tags across the project's tasks, alphabetical, for the filter bar.
  const allTags = Array.from(new Set(topLevelTasks.flatMap((t) => t.tags))).sort((a, b) => a.localeCompare(b))

  const toggleTag = (tag: string) =>
    setActiveTags((prev) => (prev.includes(tag) ? prev.filter((x) => x !== tag) : [...prev, tag]))

  // A task passes the filter when no tag is selected or it carries any selected tag.
  const visibleTasks =
    activeTags.length === 0
      ? topLevelTasks
      : topLevelTasks.filter((t) => t.tags.some((tag) => activeTags.includes(tag)))

  const download = (blob: Blob, ext: string, prefix = project?.name ?? 'tasks') => {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${prefix}.${ext}`
    a.click()
    URL.revokeObjectURL(url)
  }

  const exportCsv = async () => {
    const blob = await taskService.exportCsv(projectId).catch(() => null)
    if (blob) download(blob, 'csv')
  }

  const exportXlsx = async () => {
    const blob = await taskService.exportXlsx(projectId).catch(() => null)
    if (blob) download(blob, 'xlsx')
  }

  const exportPdf = async () => {
    const blob = await taskService.exportPdf(projectId).catch(() => null)
    if (blob) download(blob, 'pdf')
  }

  const reportPdf = async () => {
    const blob = await taskService.reportPdf(projectId).catch(() => null)
    if (blob) download(blob, 'pdf', `${project?.name ?? 'project'}-report`)
  }

  const reportXlsx = async () => {
    const blob = await taskService.reportXlsx(projectId).catch(() => null)
    if (blob) download(blob, 'xlsx', `${project?.name ?? 'project'}-report`)
  }

  if (notFound) {
    return (
      <div className="mx-auto max-w-lg px-6 py-16">
        <ResultState variant="error" message={t('board.notFound')}>
          <Link to="/projects" className="text-sm font-semibold text-primary hover:underline">
            ← {t('board.backToProjects')}
          </Link>
        </ResultState>
      </div>
    )
  }

  return (
    <div className="-mx-4 sm:-mx-6 lg:-mx-8">
        {showConfetti && <Confetti onDone={() => setShowConfetti(false)} />}
        <div className="mb-5 flex items-center gap-3">
          <Link to="/projects" className="text-sm text-muted hover:text-foreground hover:underline">
            {t('board.backToProjects')}
          </Link>
          <h1 className="text-xl font-bold">{project?.name ?? t('board.title')}</h1>
          <Button variant="secondary" size="sm" className="ml-auto" onClick={() => setMembersOpen(true)}>
            {t('members.button')}
          </Button>
          <Button variant="secondary" size="sm" onClick={exportCsv}>
            {t('board.exportCsv')}
          </Button>
          <Button variant="secondary" size="sm" onClick={exportXlsx}>
            {t('board.exportXlsx')}
          </Button>
          <Button variant="secondary" size="sm" onClick={exportPdf}>
            {t('board.exportPdf')}
          </Button>
          <Button variant="accent" size="sm" onClick={reportPdf}>
            {t('board.reportPdf')}
          </Button>
          <Button variant="accent" size="sm" onClick={reportXlsx}>
            {t('board.reportXlsx')}
          </Button>
        </div>

        {/* Add task (Editors and the owner only) */}
        {canWrite ? (
          <div className="mb-5 flex max-w-md gap-2">
            <Input
              value={newTitle}
              onChange={(e) => setNewTitle(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && addTask()}
              placeholder={t('board.addPlaceholder')}
            />
            <Button onClick={addTask}>{t('board.add')}</Button>
          </div>
        ) : (
          <p className="mb-5 text-sm text-muted">👁️ {t('board.readOnly')}</p>
        )}

        {/* Tag filter bar */}
        {allTags.length > 0 && (
          <div className="mb-5 flex flex-wrap items-center gap-1.5">
            <span className="mr-1 text-xs font-semibold text-muted">
              {t('board.filterByTag')}
            </span>
            {allTags.map((tag) => {
              const active = activeTags.includes(tag)
              return (
                <button
                  key={tag}
                  onClick={() => toggleTag(tag)}
                  className={`rounded-full px-2 py-0.5 text-xs font-medium transition ${
                    active
                      ? 'bg-primary text-white'
                      : 'bg-primary/10 text-primary hover:bg-primary/20'
                  }`}
                >
                  {tag}
                </button>
              )
            })}
            {activeTags.length > 0 && (
              <button
                onClick={() => setActiveTags([])}
                className="ml-1 text-xs font-semibold text-muted hover:text-red-600 hover:underline"
              >
                {t('board.clearFilter')}
              </button>
            )}
          </div>
        )}

        {/* Bulk action bar (visible when tasks are selected) */}
        {canWrite && selectedIds.size > 0 && (
          <div className="mb-5 flex flex-wrap items-center gap-3 rounded-lg border border-primary/20 bg-primary/5 px-4 py-2">
            <span className="text-sm font-semibold">{t('board.selected', { count: selectedIds.size })}</span>
            <select
              defaultValue=""
              onChange={(e) => {
                if (e.target.value) bulkStatus(e.target.value as TaskStatus)
                e.target.value = ''
              }}
              className="rounded-lg border border-border bg-surface px-2 py-1 text-sm text-foreground outline-none"
            >
              <option value="" disabled>
                {t('board.bulkMoveTo')}
              </option>
              {STATUS_COLUMNS.map((col) => (
                <option key={col.key} value={col.key}>
                  {t(`board.status.${col.key}`)}
                </option>
              ))}
            </select>
            <button onClick={bulkDelete} className="text-sm font-semibold text-red-600 hover:underline">
              {t('board.bulkDelete')}
            </button>
            <button onClick={clearSelection} className="text-sm font-semibold text-muted hover:text-foreground hover:underline">
              {t('board.clearSelection')}
            </button>
          </div>
        )}

        {/* Columns */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
          {STATUS_COLUMNS.map((col) => {
            const colTasks = visibleTasks.filter((t) => t.status === col.key)
            return (
              <div
                key={col.key}
                onDragOver={(e) => e.preventDefault()}
                onDrop={(e) => onDrop(col.key, e.dataTransfer.getData('taskId') || draggingId.current || '')}
                className={`rounded-xl border-t-4 bg-canvas p-3 ${columnAccent[col.key] ?? 'border-t-border'}`}
              >
                <div className="mb-3 flex items-center justify-between px-1 text-sm font-bold">
                  <span className="flex items-center gap-2">
                    <span className={`h-2 w-2 rounded-full ${columnDot[col.key] ?? 'bg-border'}`} />
                    {t(`board.status.${col.key}`)}
                  </span>
                  <span className="rounded-full bg-border/60 px-2 py-0.5 text-xs font-semibold text-muted">
                    {colTasks.length}
                  </span>
                </div>

                <div className="space-y-2">
                  {colTasks.map((task) => (
                    <TaskContextMenu
                      key={task.id}
                      onEdit={() => setSelectedTask(task)}
                      onDuplicate={() => duplicateTask(task)}
                      onCopyLink={() => copyLink(task)}
                      onChangePriority={(p) => changePriority(task, p)}
                      assignTargets={members}
                      onAssign={(uid) => assign(task, uid)}
                      moveTargets={moveTargets}
                      onMove={(pid) => moveTask(task, pid)}
                      onDelete={() => setDeletingTask(task)}
                      bookmarked={bookmarkedTaskIds.has(task.id)}
                      onBookmark={() => toggleBookmark(task)}
                    >
                      <div
                        draggable
                        onDragStart={(e) => {
                          draggingId.current = task.id
                          e.dataTransfer.setData('taskId', task.id)
                        }}
                        onClick={() => setSelectedTask(task)}
                        className="group relative cursor-grab rounded-lg border border-border bg-surface p-3 shadow-soft transition hover:border-primary/30 hover:shadow-card active:cursor-grabbing"
                      >
                        {/* Hover three-dot menu (same actions as right-click) */}
                        <div className="absolute right-1 top-1 opacity-0 transition group-hover:opacity-100">
                          <TaskActionsDropdown
                            onEdit={() => setSelectedTask(task)}
                            onDuplicate={() => duplicateTask(task)}
                            onCopyLink={() => copyLink(task)}
                            onChangePriority={(p) => changePriority(task, p)}
                            assignTargets={members}
                            onAssign={(uid) => assign(task, uid)}
                            moveTargets={moveTargets}
                            onMove={(pid) => moveTask(task, pid)}
                            onDelete={() => setDeletingTask(task)}
                          />
                        </div>
                        {/* Bulk-select checkbox (Editors/owner) — appears on hover or when selected */}
                        {canWrite && (
                          <input
                            type="checkbox"
                            checked={selectedIds.has(task.id)}
                            onClick={(e) => e.stopPropagation()}
                            onChange={() => toggleSelected(task.id)}
                            className={`absolute left-1 top-1 h-4 w-4 accent-primary transition ${
                              selectedIds.has(task.id) ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'
                            }`}
                          />
                        )}
                        <div className={`pr-5 text-sm font-medium ${canWrite ? 'pl-5' : ''}`}>{task.title}</div>
                        <div className="mt-2 flex items-center gap-2">
                          <span
                            className={`rounded px-2 py-0.5 text-[11px] font-semibold ${
                              priorityClasses[task.priority] ?? 'bg-border text-muted'
                            }`}
                          >
                            {t(`board.priority.${task.priority}`, task.priority)}
                          </span>
                          {task.assigneeName && (
                            <span className="text-[11px] text-muted">@{task.assigneeName}</span>
                          )}
                        </div>
                        {task.tags.length > 0 && (
                          <div className="mt-2 flex flex-wrap gap-1">
                            {task.tags.map((tag) => (
                              <span
                                key={tag}
                                className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-medium text-primary"
                              >
                                {tag}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    </TaskContextMenu>
                  ))}
                  {colTasks.length === 0 && (
                    <p className="px-1 py-4 text-center text-xs text-muted">{t('board.dropHere')}</p>
                  )}
                </div>
              </div>
            )
          })}
        </div>

      {selectedTask && (
        <TaskDetailModal
          task={selectedTask}
          onClose={() => setSelectedTask(null)}
          onSaved={(updated) => setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)))}
          onDeleted={(taskId) => setTasks((prev) => prev.filter((t) => t.id !== taskId))}
        />
      )}

      {membersOpen && (
        <ProjectMembersModal
          projectId={projectId}
          isOwner={isOwner}
          currentUserId={currentUserId}
          onClose={() => setMembersOpen(false)}
          onLeft={() => navigate('/projects')}
        />
      )}

      {/* Task delete confirmation */}
      <ConfirmDialog
        open={!!deletingTask}
        title={t('board.deleteTitle')}
        message={t('board.deleteConfirm', { title: deletingTask?.title ?? '' })}
        onConfirm={() => {
          if (deletingTask) removeTask(deletingTask)
          setDeletingTask(null)
        }}
        onCancel={() => setDeletingTask(null)}
      />
    </div>
  )
}
