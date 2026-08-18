import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import TaskActionsDropdown from '../components/menus/TaskActionsDropdown'
import TaskContextMenu from '../components/menus/TaskContextMenu'
import TaskDetailModal from '../components/modals/TaskDetailModal'
import ProjectMembersModal from '../components/modals/ProjectMembersModal'
import AutomationsModal from '../components/modals/AutomationsModal'
import CustomFieldsModal from '../components/modals/CustomFieldsModal'
import EpicsModal from '../components/modals/EpicsModal'
import BoardActionsMenu from '../components/menus/BoardActionsMenu'
import ShareBoardModal from '../components/modals/ShareBoardModal'
import GitHubModal from '../components/modals/GitHubModal'
import ProjectAnalyticsPanel from '../components/ProjectAnalyticsPanel'
import SprintsPanel from '../components/SprintsPanel'
import ActivityPanel from '../components/ActivityPanel'
import ConfirmDialog from '../components/modals/ConfirmDialog'
import Button from '../components/ui/Button'
import Input from '../components/ui/Input'
import Card from '../components/ui/Card'
import GanttChart from '../components/GanttChart'
import TeamWorkload from '../components/TeamWorkload'
import Confetti from '../components/feedback/Confetti'
import ResultState from '../components/feedback/ResultState'
import { bookmarkService } from '../services/bookmarkService'
import { gitHubService } from '../services/gitHubService'
import { taskRef } from '../lib/taskRef'
import { useAppSelector } from '../store/hooks'
import { projectService } from '../services/projectService'
import { useDragAndDrop } from '../hooks/useDragAndDrop'
import { useFeatures } from '../hooks/useFeatures'
import { apiErrorMessage } from '../lib/apiError'
import { taskService, type ReportSchedule } from '../services/taskService'
import { epicService } from '../services/epicService'
import { notify } from '../lib/toast'
import { STATUS_COLUMNS, type Epic, type Project, type Task, type TaskStatus } from '../types/project'
import { FILTER_ME, FILTER_UNASSIGNED, hasActiveFilters, matchesBoardFilters } from '../lib/boardFilters'

const priorityClasses: Record<string, string> = {
  High: 'bg-red-500/10 text-red-600 dark:text-red-400',
  Medium: 'bg-amber-500/10 text-amber-600 dark:text-amber-400',
  Low: 'bg-border/60 text-muted',
}

// How tasks are ordered inside each board column (labels come from i18n).
const TASK_SORT_KEYS = ['manual', 'priority', 'deadline', 'title'] as const
type TaskSortKey = (typeof TASK_SORT_KEYS)[number]
// High first, then Medium, then Low.
const priorityRank: Record<string, number> = { High: 0, Medium: 1, Low: 2 }

// Status dot color for each column header.
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
  const features = useFeatures()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [project, setProject] = useState<Project | null>(null)
  const [notFound, setNotFound] = useState(false)
  const [tasks, setTasks] = useState<Task[]>([])
  const [newTitle, setNewTitle] = useState('')
  const [selectedTask, setSelectedTask] = useState<Task | null>(null)
  // Whether the detail modal should open straight onto the task's history
  // ("View history" in the menus) rather than the edit form.
  const [openOnHistory, setOpenOnHistory] = useState(false)

  /** Opens the task's detail modal, optionally jumping to its history. */
  const openTask = (task: Task, history = false) => {
    setOpenOnHistory(history)
    setSelectedTask(task)
  }
  const [membersOpen, setMembersOpen] = useState(false)
  const [automationsOpen, setAutomationsOpen] = useState(false)
  const [customFieldsOpen, setCustomFieldsOpen] = useState(false)
  const [epicsOpen, setEpicsOpen] = useState(false)
  // The project's epics, for the coloured chip shown on each task card.
  const [epics, setEpics] = useState<Epic[]>([])
  const [shareOpen, setShareOpen] = useState(false)
  const [githubOpen, setGithubOpen] = useState(false)
  // The connected GitHub repo ("owner/name"), shown read-only to every project member.
  const [githubRepo, setGithubRepo] = useState<string | null>(null)
  const [criticalIds, setCriticalIds] = useState<Set<string>>(new Set())
  const [canWrite, setCanWrite] = useState(true)
  // Other projects this task can be moved to (owned/active, excluding the current one).
  const [moveTargets, setMoveTargets] = useState<{ id: string; name: string }[]>([])
  // Project members a task can be assigned to (owner + collaborators).
  const [members, setMembers] = useState<{ id: string; name: string }[]>([])
  // Tags currently used to filter the board (empty = show everything).
  const [activeTags, setActiveTags] = useState<string[]>([])
  // Assignee/priority board filters ('' = any).
  const [filterAssignee, setFilterAssignee] = useState('')
  const [filterPriority, setFilterPriority] = useState('')
  // Ids of tasks selected for a bulk action.
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  // Task awaiting delete confirmation.
  const [deletingTask, setDeletingTask] = useState<Task | null>(null)
  // Celebratory confetti shown briefly when a task is moved to Done.
  const [showConfetti, setShowConfetti] = useState(false)
  // Per-column WIP limits (status → max tasks); the column being edited, if any.
  const [wipLimits, setWipLimits] = useState<Record<string, number>>({})
  const [editingWip, setEditingWip] = useState<string | null>(null)
  const [wipDraft, setWipDraft] = useState('')

  const isOwner = !!project && project.ownerId === currentUserId
  // A member may move only tasks assigned to them; the owner may move any.
  const canMoveTask = (task: Task) => isOwner || task.assigneeId === currentUserId

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
    // Critical-path task ids — highlighted on the timeline (only a real chain of >1).
    projectService
      .getCriticalPath(projectId)
      .then((cp) => setCriticalIds(cp.length > 1 ? new Set(cp.tasks.map((task) => task.id)) : new Set()))
      .catch(() => {})
    // Per-column WIP limits.
    projectService
      .getWipLimits(projectId)
      .then((ls) => setWipLimits(Object.fromEntries(ls.map((l) => [l.status, l.maxTasks]))))
      .catch(() => {})
    // Project epics — for the coloured chip on cards and the modal.
    epicService.list(projectId).then(setEpics).catch(() => {})
    // Linked GitHub repo — a read-only indicator any member can see.
    gitHubService.status(projectId).then((s) => setGithubRepo(s.connected ? s.repo : null)).catch(() => {})
  }, [projectId, currentUserId])

  // Quick lookup of an epic by id for the card chip.
  const epicById = new Map(epics.map((e) => [e.id, e]))

  // Save (or clear, when blank/0) the WIP limit for a column from the inline editor.
  const saveWip = async (status: string) => {
    setEditingWip(null)
    const parsed = parseInt(wipDraft, 10)
    const value = Number.isFinite(parsed) && parsed > 0 ? parsed : null
    if ((wipLimits[status] ?? null) === value) return
    try {
      const list = await projectService.setWipLimit(projectId, status, value)
      setWipLimits(Object.fromEntries(list.map((l) => [l.status, l.maxTasks])))
    } catch (e) {
      notify.error(apiErrorMessage(e))
    }
  }

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

  const onDrop = async (status: string, taskId: string) => {
    const task = tasks.find((t) => t.id === taskId)
    if (!task || task.status === status) return
    // You may only move tasks assigned to you (the owner may move any).
    if (!canMoveTask(task)) return
    // Only the owner approves a task into Done.
    if (status === 'Done' && !isOwner) {
      notify.error(t('board.onlyOwnerDone'))
      return
    }
    setTasks((prev) => prev.map((t) => (t.id === taskId ? { ...t, status: status as TaskStatus } : t)))
    // Celebrate finishing a task.
    if (status === 'Done') setShowConfetti(true)
    try {
      await taskService.changeStatus(taskId, status as TaskStatus)
    } catch (e) {
      // Surface the reason (e.g. a WIP limit) and roll the board back to the server truth.
      notify.error(apiErrorMessage(e))
      taskService.getTasks(projectId).then(setTasks).catch(() => {})
    }
  }

  // Pointer-based drag-and-drop (works on touch, unlike the native HTML5 API).
  const dnd = useDragAndDrop({
    onDrop,
    renderGhost: (id) => {
      const task = tasks.find((t) => t.id === id)
      return (
        <div className="rounded-lg border border-primary bg-surface px-3 py-2 text-sm font-medium shadow-elevated">
          {task?.title ?? ''}
        </div>
      )
    },
  })

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

  const bulkAssign = async (assigneeId: string | null) => {
    const ids = [...selectedIds]
    if (ids.length === 0) return
    const res = await taskService.bulkAssign(ids, assigneeId).catch(() => null)
    clearSelection()
    taskService.getTasks(projectId).then(setTasks).catch(() => {})
    if (res) notify.success(t('toast.tasksUpdated', { count: res.changed }))
  }

  const bulkPriority = async (priority: string) => {
    const ids = [...selectedIds]
    if (ids.length === 0) return
    const res = await taskService.bulkPriority(ids, priority).catch(() => null)
    clearSelection()
    taskService.getTasks(projectId).then(setTasks).catch(() => {})
    if (res) notify.success(t('toast.tasksUpdated', { count: res.changed }))
  }

  // The board shows only top-level tasks; subtasks are managed inside their parent.
  const topLevelTasks = tasks.filter((t) => !t.parentTaskId)

  // All distinct tags across the project's tasks, alphabetical, for the filter bar.
  const allTags = Array.from(new Set(topLevelTasks.flatMap((t) => t.tags))).sort((a, b) => a.localeCompare(b))

  const toggleTag = (tag: string) =>
    setActiveTags((prev) => (prev.includes(tag) ? prev.filter((x) => x !== tag) : [...prev, tag]))

  // A task passes when it satisfies every active filter (tags / assignee / priority).
  const boardFilters = { tags: activeTags, assignee: filterAssignee, priority: filterPriority }
  const visibleTasks = hasActiveFilters(boardFilters)
    ? topLevelTasks.filter((t) => matchesBoardFilters(t, boardFilters, currentUserId))
    : topLevelTasks
  const clearFilters = () => {
    setActiveTags([])
    setFilterAssignee('')
    setFilterPriority('')
  }

  // How to order tasks within each column ('manual' keeps board/creation order).
  const [taskSort, setTaskSort] = useState<TaskSortKey>('manual')

  // Order within each column. 'manual' keeps the natural (creation) order; tasks with no
  // deadline sort last when ordering by deadline.
  const sortedVisibleTasks =
    taskSort === 'manual'
      ? visibleTasks
      : visibleTasks.slice().sort((a, b) => {
          switch (taskSort) {
            case 'priority':
              return (priorityRank[a.priority] ?? 9) - (priorityRank[b.priority] ?? 9)
            case 'deadline':
              return (
                (a.deadline ? new Date(a.deadline).getTime() : Infinity) -
                (b.deadline ? new Date(b.deadline).getTime() : Infinity)
              )
            case 'title':
              return a.title.localeCompare(b.title)
            default:
              return 0
          }
        })

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

  const importInputRef = useRef<HTMLInputElement>(null)
  const importCsv = async (file: File | undefined) => {
    if (!file) return
    try {
      const res = await taskService.importCsv(projectId, await file.text())
      notify.success(t('board.importResult', { created: res.created, skipped: res.skipped }))
      if (res.errors.length > 0) notify.error(res.errors[0])
      taskService.getTasks(projectId).then(setTasks).catch(() => {})
    } catch (e) {
      notify.error(apiErrorMessage(e))
    }
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

  const teamReportPdf = async () => {
    const blob = await taskService.teamReportPdf(projectId).catch(() => null)
    if (blob) download(blob, 'pdf', `${project?.name ?? 'project'}-team`)
  }

  const teamReportXlsx = async () => {
    const blob = await taskService.teamReportXlsx(projectId).catch(() => null)
    if (blob) download(blob, 'xlsx', `${project?.name ?? 'project'}-team`)
  }

  // The toolbar would be too crowded with four report buttons, so they live in a menu.
  const [reportsOpen, setReportsOpen] = useState(false)
  const [view, setView] = useState<'board' | 'gantt' | 'team' | 'analytics' | 'sprints' | 'activity'>('board')
  const runReport = (fn: () => Promise<void>) => {
    setReportsOpen(false)
    void fn()
  }

  // Recurring report emails for this project (loaded when the menu opens).
  const [schedules, setSchedules] = useState<ReportSchedule[]>([])
  const [schedKind, setSchedKind] = useState('Project')
  const [schedFormat, setSchedFormat] = useState('Pdf')
  const [schedFreq, setSchedFreq] = useState('Weekly')
  const [schedError, setSchedError] = useState('')

  useEffect(() => {
    if (!reportsOpen) return
    taskService.getReportSchedules(projectId).then(setSchedules).catch(() => {})
  }, [reportsOpen, projectId])

  const addSchedule = async () => {
    setSchedError('')
    try {
      const created = await taskService.createReportSchedule(projectId, {
        kind: schedKind,
        format: schedFormat,
        frequency: schedFreq,
      })
      setSchedules((prev) => [created, ...prev])
    } catch (e) {
      setSchedError(apiErrorMessage(e))
    }
  }

  const removeSchedule = async (id: string) => {
    await taskService.deleteReportSchedule(projectId, id).catch(() => {})
    setSchedules((prev) => prev.filter((s) => s.id !== id))
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
        {dnd.overlay}
        <div className="mb-4 flex items-center gap-3">
          <Link to="/projects" className="text-sm text-muted hover:text-foreground hover:underline">
            {t('board.backToProjects')}
          </Link>
          <h1 className="min-w-0 truncate text-xl font-bold">{project?.name ?? t('board.title')}</h1>
          {githubRepo && (
            <a
              href={`https://github.com/${githubRepo}`}
              target="_blank"
              rel="noreferrer"
              title={t('github.linkedRepo', { repo: githubRepo })}
              className="inline-flex flex-none items-center gap-1 rounded-full border border-border bg-surface px-2.5 py-1 text-xs font-medium text-muted transition hover:bg-canvas hover:text-foreground"
            >
              🔗 <span className="max-w-[12rem] truncate">{githubRepo}</span>
            </a>
          )}

        </div>

        {/* View switcher + object-level actions share one row below the title. */}
        <div className="mb-5 flex items-center justify-between gap-3 border-b border-border">
          <div className="flex gap-1 overflow-x-auto">
            {(['board', 'gantt', 'team', 'analytics', 'sprints', 'activity'] as const).map((v) => (
              <button
                key={v}
                onClick={() => setView(v)}
                className={`-mb-px shrink-0 whitespace-nowrap border-b-2 px-3 py-2 text-[13px] font-medium transition ${
                  view === v ? 'border-primary text-primary' : 'border-transparent text-muted hover:text-foreground'
                }`}
              >
                {t(`board.view.${v}`)}
              </button>
            ))}
            {canWrite && features.whiteboard && (
              <button
                onClick={() => navigate(`/projects/${projectId}/whiteboard`)}
                className="-mb-px shrink-0 whitespace-nowrap border-b-2 border-transparent px-3 py-2 text-[13px] font-medium text-muted transition hover:text-foreground"
              >
                {t('whiteboard.button')}
              </button>
            )}
          </div>

          <div className="flex flex-none items-center gap-2 pb-1.5">
          {/* Hidden CSV import picker, triggered from the "More" menu. */}
          {canWrite && (
            <input
              ref={importInputRef}
              type="file"
              accept=".csv,text/csv"
              className="hidden"
              onChange={(e) => {
                importCsv(e.target.files?.[0])
                e.target.value = ''
              }}
            />
          )}
          {/* Secondary actions collapsed into one menu so the toolbar stays clean. */}
          <BoardActionsMenu
            isOwner={isOwner}
            canWrite={canWrite}
            onMembers={() => setMembersOpen(true)}
            onAutomations={() => setAutomationsOpen(true)}
            onFields={() => setCustomFieldsOpen(true)}
            onEpics={() => setEpicsOpen(true)}
            onWhiteboard={() => navigate(`/projects/${projectId}/whiteboard`)}
            onShare={() => setShareOpen(true)}
            onGitHub={() => setGithubOpen(true)}
            onImport={() => importInputRef.current?.click()}
            onExportCsv={exportCsv}
            onExportXlsx={exportXlsx}
            onExportPdf={exportPdf}
          />
          {/* Reports menu (project health + team performance, PDF/Excel each) */}
          <div className="relative">
            <Button variant="secondary" size="sm" onClick={() => setReportsOpen((o) => !o)}>
              {t('board.reports')} ▾
            </Button>
            {reportsOpen && (
              <>
                {/* Click anywhere else to close. */}
                <div className="fixed inset-0 z-10" onClick={() => setReportsOpen(false)} />
                <div className="absolute right-0 z-20 mt-1 w-80 overflow-hidden rounded-lg border border-border bg-surface shadow-elevated">
                  {/* Download now */}
                  {[
                    { label: t('board.reportPdf'), run: reportPdf },
                    { label: t('board.reportXlsx'), run: reportXlsx },
                    { label: t('board.teamReportPdf'), run: teamReportPdf },
                    { label: t('board.teamReportXlsx'), run: teamReportXlsx },
                  ].map((item) => (
                    <button
                      key={item.label}
                      onClick={() => runReport(item.run)}
                      className="block w-full px-3 py-2 text-left text-sm hover:bg-canvas"
                    >
                      {item.label}
                    </button>
                  ))}

                  {/* Recurring report emails */}
                  <div className="border-t border-border p-3">
                    <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">
                      {t('board.schedule')}
                    </p>

                    {schedules.length > 0 && (
                      <ul className="mb-2 space-y-1">
                        {schedules.map((s) => (
                          <li key={s.id} className="flex items-center gap-2 text-xs">
                            <span className="min-w-0 flex-1 truncate">
                              {t(`board.kind.${s.kind}`, s.kind)} · {s.format} ·{' '}
                              {t(`board.freq.${s.frequency}`, s.frequency)}
                            </span>
                            <button
                              onClick={() => removeSchedule(s.id)}
                              className="flex-none text-muted hover:text-red-600"
                              title={t('board.scheduleRemove')}
                            >
                              ✕
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}

                    <div className="flex gap-1">
                      <select
                        value={schedKind}
                        onChange={(e) => setSchedKind(e.target.value)}
                        className="min-w-0 flex-1 rounded border border-border bg-canvas px-1 py-1 text-xs"
                      >
                        <option value="Project">{t('board.kind.Project')}</option>
                        <option value="Team">{t('board.kind.Team')}</option>
                      </select>
                      <select
                        value={schedFormat}
                        onChange={(e) => setSchedFormat(e.target.value)}
                        className="rounded border border-border bg-canvas px-1 py-1 text-xs"
                      >
                        <option value="Pdf">PDF</option>
                        <option value="Xlsx">Excel</option>
                      </select>
                      <select
                        value={schedFreq}
                        onChange={(e) => setSchedFreq(e.target.value)}
                        className="min-w-0 flex-1 rounded border border-border bg-canvas px-1 py-1 text-xs"
                      >
                        <option value="Daily">{t('board.freq.Daily')}</option>
                        <option value="Weekly">{t('board.freq.Weekly')}</option>
                        <option value="Monthly">{t('board.freq.Monthly')}</option>
                      </select>
                      <button
                        onClick={addSchedule}
                        className="gradient-primary flex-none rounded px-2 py-1 text-xs font-semibold text-white transition hover:brightness-[1.06]"
                      >
                        +
                      </button>
                    </div>
                    {schedError && <p className="mt-1 text-xs text-red-600">{schedError}</p>}
                  </div>
                </div>
              </>
            )}
          </div>
          </div>
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

        {/* Filter bar: assignee + priority selects, plus tag pills when the project has tags. */}
        {view === 'board' && (
          <div className="mb-5 flex flex-wrap items-center gap-1.5">
            <select
              value={filterAssignee}
              onChange={(e) => setFilterAssignee(e.target.value)}
              className="rounded-lg border border-border bg-surface px-2 py-1 text-xs text-foreground outline-none"
            >
              <option value="">{t('board.filter.anyAssignee')}</option>
              <option value={FILTER_ME}>{t('board.filter.me')}</option>
              <option value={FILTER_UNASSIGNED}>{t('board.filter.unassigned')}</option>
              {members.map((m) => (
                <option key={m.id} value={m.id}>{m.name}</option>
              ))}
            </select>
            <select
              value={filterPriority}
              onChange={(e) => setFilterPriority(e.target.value)}
              className="rounded-lg border border-border bg-surface px-2 py-1 text-xs text-foreground outline-none"
            >
              <option value="">{t('board.filter.anyPriority')}</option>
              {['High', 'Medium', 'Low'].map((p) => (
                <option key={p} value={p}>{t(`board.priority.${p}`)}</option>
              ))}
            </select>

            {allTags.length > 0 && <span className="ml-1 mr-0.5 text-xs font-semibold text-muted">{t('board.filterByTag')}</span>}
            {allTags.map((tag) => {
              const active = activeTags.includes(tag)
              return (
                <button
                  key={tag}
                  onClick={() => toggleTag(tag)}
                  className={`rounded-full px-2 py-0.5 text-xs font-medium transition ${
                    active ? 'bg-primary text-white' : 'bg-primary/10 text-primary hover:bg-primary/20'
                  }`}
                >
                  {tag}
                </button>
              )
            })}

            {hasActiveFilters(boardFilters) && (
              <button
                onClick={clearFilters}
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
            <select
              defaultValue=""
              onChange={(e) => {
                if (e.target.value) bulkAssign(e.target.value === '__unassign' ? null : e.target.value)
                e.target.value = ''
              }}
              className="rounded-lg border border-border bg-surface px-2 py-1 text-sm text-foreground outline-none"
            >
              <option value="" disabled>
                {t('board.bulkAssignTo')}
              </option>
              <option value="__unassign">{t('board.unassign')}</option>
              {members.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
            <select
              defaultValue=""
              onChange={(e) => {
                if (e.target.value) bulkPriority(e.target.value)
                e.target.value = ''
              }}
              className="rounded-lg border border-border bg-surface px-2 py-1 text-sm text-foreground outline-none"
            >
              <option value="" disabled>
                {t('board.bulkPriority')}
              </option>
              {['Low', 'Medium', 'High'].map((p) => (
                <option key={p} value={p}>
                  {t(`board.priority.${p}`)}
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

        {/* Timeline (Gantt) — same filtered tasks as the board */}
        {view === 'gantt' && (
          <Card className="p-4">
            <GanttChart tasks={visibleTasks} onSelect={setSelectedTask} criticalTaskIds={criticalIds} />
          </Card>
        )}

        {/* Team availability — who is busy with what over the next month */}
        {view === 'team' && <TeamWorkload projectId={projectId} />}

        {/* Delivery analytics — status/priority mix, weekly trend, cycle time, workload */}
        {view === 'analytics' && <ProjectAnalyticsPanel projectId={projectId} />}

        {/* Sprints / iterations */}
        {view === 'sprints' && <SprintsPanel projectId={projectId} canWrite={canWrite} />}

        {view === 'activity' && <ActivityPanel projectId={projectId} />}

        {/* Columns */}
        {view === 'board' && (
        <>
        {tasks.length > 1 && (
          <div className="mb-4 flex items-center gap-2">
            <span className="text-xs font-semibold text-muted">{t('board.sortTasks')}</span>
            <select
              value={taskSort}
              onChange={(e) => setTaskSort(e.target.value as TaskSortKey)}
              aria-label={t('board.sortTasks')}
              className="rounded-lg border border-border bg-surface px-2.5 py-1.5 text-sm text-foreground outline-none focus:border-primary"
            >
              {TASK_SORT_KEYS.map((k) => (
                <option key={k} value={k}>
                  {t(`board.taskSort.${k}`)}
                </option>
              ))}
            </select>
          </div>
        )}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
          {STATUS_COLUMNS.map((col) => {
            const colTasks = sortedVisibleTasks.filter((t) => t.status === col.key)
            return (
              <div
                key={col.key}
                {...dnd.dropZoneProps(col.key)}
                className={`rounded-[var(--radius-card)] border border-border bg-canvas/40 p-2.5 transition-colors ${
                  dnd.activeZone === col.key ? 'bg-primary/5 ring-1 ring-inset ring-primary/50' : ''
                }`}
              >
                <div className="mb-2.5 flex items-center justify-between px-1 text-[13px] font-semibold">
                  <span className="flex items-center gap-2">
                    <span className={`h-2 w-2 rounded-full ${columnDot[col.key] ?? 'bg-border'}`} />
                    {t(`board.status.${col.key}`)}
                  </span>
                  {(() => {
                    const limit = wipLimits[col.key]
                    const over = limit != null && colTasks.length >= limit
                    if (editingWip === col.key) {
                      return (
                        <input
                          type="number"
                          min={0}
                          autoFocus
                          value={wipDraft}
                          onChange={(e) => setWipDraft(e.target.value)}
                          onBlur={() => saveWip(col.key)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') saveWip(col.key)
                            if (e.key === 'Escape') setEditingWip(null)
                          }}
                          placeholder={t('board.wip.placeholder')}
                          className="w-16 rounded-full border border-primary bg-surface px-2 py-0.5 text-xs font-semibold outline-none"
                        />
                      )
                    }
                    const label = limit != null ? `${colTasks.length} / ${limit}` : `${colTasks.length}`
                    const cls = over
                      ? 'bg-red-500/10 text-red-600 dark:text-red-400'
                      : 'bg-border/60 text-muted'
                    return canWrite ? (
                      <button
                        type="button"
                        onClick={() => { setEditingWip(col.key); setWipDraft(limit != null ? String(limit) : '') }}
                        title={t('board.wip.set')}
                        className={`rounded-full px-2 py-0.5 text-xs font-semibold transition hover:ring-1 hover:ring-primary/40 ${cls}`}
                      >
                        {label}
                      </button>
                    ) : (
                      <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${cls}`}>{label}</span>
                    )
                  })()}
                </div>

                <div className="space-y-2">
                  {colTasks.map((task) => (
                    <TaskContextMenu
                      key={task.id}
                      onEdit={() => openTask(task)}
                      onDuplicate={() => duplicateTask(task)}
                      onCopyLink={() => copyLink(task)}
                      onChangePriority={(p) => changePriority(task, p)}
                      assignTargets={members}
                      onAssign={(uid) => assign(task, uid)}
                      moveTargets={moveTargets}
                      onMove={(pid) => moveTask(task, pid)}
                      onViewHistory={() => openTask(task, true)}
                      onDelete={() => setDeletingTask(task)}
                      bookmarked={bookmarkedTaskIds.has(task.id)}
                      onBookmark={() => toggleBookmark(task)}
                    >
                      <div
                        {...(canMoveTask(task) ? dnd.draggableProps(task.id) : {})}
                        onClick={() => {
                          // Ignore the click that fires right after a drag.
                          if (dnd.justDragged()) return
                          openTask(task)
                        }}
                        className={`group relative rounded-lg border border-border bg-surface p-2.5 transition hover:border-muted/50 ${
                          canMoveTask(task) ? 'cursor-grab' : ''
                        } ${dnd.draggingId === task.id ? 'opacity-40' : ''}`}
                      >
                        {/* Hover three-dot menu (same actions as right-click) */}
                        <div className="absolute right-1 top-1 opacity-0 transition group-hover:opacity-100">
                          <TaskActionsDropdown
                            onEdit={() => openTask(task)}
                            onDuplicate={() => duplicateTask(task)}
                            onCopyLink={() => copyLink(task)}
                            onChangePriority={(p) => changePriority(task, p)}
                            assignTargets={members}
                            onAssign={(uid) => assign(task, uid)}
                            moveTargets={moveTargets}
                            onMove={(pid) => moveTask(task, pid)}
                            onViewHistory={() => openTask(task, true)}
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
                        {task.epicId && epicById.has(task.epicId) && (
                          <span
                            className={`mb-1 inline-block max-w-full truncate rounded px-1.5 py-0.5 text-[10px] font-semibold ${canWrite ? 'ml-5' : ''}`}
                            style={{
                              background: `${epicById.get(task.epicId)!.color ?? '#64748b'}22`,
                              color: epicById.get(task.epicId)!.color ?? '#64748b',
                            }}
                          >
                            {epicById.get(task.epicId)!.title}
                          </span>
                        )}
                        <div className={`pr-5 text-[13px] font-medium ${canWrite ? 'pl-5' : ''}`}>{task.title}</div>
                        <div className="mt-2 flex items-center gap-2">
                          {project && (
                            <span className="font-mono text-[10px] text-muted" title={t('github.taskRef')}>
                              {taskRef(project.name, task.number)}
                            </span>
                          )}
                          <span
                            className={`rounded px-2 py-0.5 text-[11px] font-semibold ${
                              priorityClasses[task.priority] ?? 'bg-border text-muted'
                            }`}
                          >
                            {t(`board.priority.${task.priority}`, task.priority)}
                          </span>
                          {task.recurrence && task.recurrence !== 'None' && (
                            <span
                              className="text-[11px] text-muted"
                              title={t(`taskModal.recurrenceOption.${task.recurrence}`, task.recurrence)}
                            >
                              🔁
                            </span>
                          )}
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
        </>
        )}

      {selectedTask && (
        <TaskDetailModal
          task={selectedTask}
          taskReference={project ? taskRef(project.name, selectedTask.number) : undefined}
          showHistory={openOnHistory}
          isOwner={isOwner}
          canWrite={canWrite}
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

      {automationsOpen && (
        <AutomationsModal
          projectId={projectId}
          members={members}
          onClose={() => setAutomationsOpen(false)}
        />
      )}

      {customFieldsOpen && (
        <CustomFieldsModal projectId={projectId} onClose={() => setCustomFieldsOpen(false)} />
      )}

      {epicsOpen && (
        <EpicsModal
          projectId={projectId}
          onClose={() => {
            setEpicsOpen(false)
            epicService.list(projectId).then(setEpics).catch(() => {})
          }}
        />
      )}

      {shareOpen && <ShareBoardModal projectId={projectId} onClose={() => setShareOpen(false)} />}

      {githubOpen && <GitHubModal projectId={projectId} onClose={() => setGithubOpen(false)} />}

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
