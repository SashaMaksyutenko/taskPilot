import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router-dom'
import Navbar from '../components/Navbar'
import TaskActionsDropdown from '../components/TaskActionsDropdown'
import TaskContextMenu from '../components/TaskContextMenu'
import TaskDetailModal from '../components/TaskDetailModal'
import ProjectMembersModal from '../components/ProjectMembersModal'
import { useAppSelector } from '../store/hooks'
import { projectService } from '../services/projectService'
import { taskService } from '../services/taskService'
import { STATUS_COLUMNS, type Project, type Task, type TaskStatus } from '../types/project'

const priorityClasses: Record<string, string> = {
  High: 'bg-red-100 text-red-700',
  Medium: 'bg-amber-100 text-amber-700',
  Low: 'bg-slate-200 text-slate-600',
}

/**
 * Kanban board for one project. Tasks are grouped into columns by status and can
 * be dragged between columns (native HTML5 drag & drop), which calls the status API.
 */
export default function BoardPage() {
  const { t } = useTranslation()
  const { projectId = '' } = useParams()
  const navigate = useNavigate()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [project, setProject] = useState<Project | null>(null)
  const [tasks, setTasks] = useState<Task[]>([])
  const [newTitle, setNewTitle] = useState('')
  const [selectedTask, setSelectedTask] = useState<Task | null>(null)
  const [membersOpen, setMembersOpen] = useState(false)
  const [canWrite, setCanWrite] = useState(true)
  // Tags currently used to filter the board (empty = show everything).
  const [activeTags, setActiveTags] = useState<string[]>([])
  const draggingId = useRef<string | null>(null)

  const isOwner = !!project && project.ownerId === currentUserId

  useEffect(() => {
    if (!projectId) return
    projectService.getProject(projectId).then(setProject).catch(() => {})
    taskService.getTasks(projectId).then(setTasks).catch(() => {})
    // Determine my write permission (owner or Editor member; Viewers are read-only).
    projectService
      .getMembers(projectId)
      .then((ms) => {
        const me = ms.find((m) => m.userId === currentUserId)
        setCanWrite(!!me && (me.isOwner || me.role === 'Editor'))
      })
      .catch(() => {})
  }, [projectId, currentUserId])

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
    if (copy) setTasks((prev) => [...prev, copy])
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

  const download = (blob: Blob, ext: string) => {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${project?.name ?? 'tasks'}.${ext}`
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

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="px-6 py-6">
        <div className="mb-5 flex items-center gap-3">
          <Link to="/projects" className="text-sm text-slate-500 hover:underline dark:text-slate-400">
            {t('board.backToProjects')}
          </Link>
          <h1 className="text-xl font-bold">{project?.name ?? t('board.title')}</h1>
          <button
            onClick={() => setMembersOpen(true)}
            className="ml-auto rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
          >
            {t('members.button')}
          </button>
          <button
            onClick={exportCsv}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
          >
            {t('board.exportCsv')}
          </button>
          <button
            onClick={exportXlsx}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
          >
            {t('board.exportXlsx')}
          </button>
          <button
            onClick={exportPdf}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
          >
            {t('board.exportPdf')}
          </button>
        </div>

        {/* Add task (Editors and the owner only) */}
        {canWrite ? (
          <div className="mb-5 flex max-w-md gap-2">
            <input
              value={newTitle}
              onChange={(e) => setNewTitle(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && addTask()}
              placeholder={t('board.addPlaceholder')}
              className="flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-800"
            />
            <button onClick={addTask} className="rounded-lg bg-[#1E2A44] px-4 text-sm font-semibold text-white">
              {t('board.add')}
            </button>
          </div>
        ) : (
          <p className="mb-5 text-sm text-slate-400">👁️ {t('board.readOnly')}</p>
        )}

        {/* Tag filter bar */}
        {allTags.length > 0 && (
          <div className="mb-5 flex flex-wrap items-center gap-1.5">
            <span className="mr-1 text-xs font-semibold text-slate-500 dark:text-slate-400">
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
                      ? 'bg-[#1E2A44] text-white dark:bg-slate-200 dark:text-slate-900'
                      : 'bg-[#1E2A44]/10 text-[#1E2A44] hover:bg-[#1E2A44]/20 dark:bg-slate-700 dark:text-slate-200'
                  }`}
                >
                  {tag}
                </button>
              )
            })}
            {activeTags.length > 0 && (
              <button
                onClick={() => setActiveTags([])}
                className="ml-1 text-xs font-semibold text-slate-400 hover:text-red-600 hover:underline"
              >
                {t('board.clearFilter')}
              </button>
            )}
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
                className="rounded-xl bg-slate-100 p-3 dark:bg-slate-800"
              >
                <div className="mb-3 flex items-center justify-between px-1 text-sm font-bold">
                  <span>{t(`board.status.${col.key}`)}</span>
                  <span className="text-slate-400">{colTasks.length}</span>
                </div>

                <div className="space-y-2">
                  {colTasks.map((task) => (
                    <TaskContextMenu
                      key={task.id}
                      onEdit={() => setSelectedTask(task)}
                      onDuplicate={() => duplicateTask(task)}
                      onChangePriority={(p) => changePriority(task, p)}
                      onDelete={() => removeTask(task)}
                    >
                      <div
                        draggable
                        onDragStart={(e) => {
                          draggingId.current = task.id
                          e.dataTransfer.setData('taskId', task.id)
                        }}
                        onClick={() => setSelectedTask(task)}
                        className="group relative cursor-grab rounded-lg border border-slate-200 bg-white p-3 shadow-sm active:cursor-grabbing dark:border-slate-700 dark:bg-slate-900"
                      >
                        {/* Hover three-dot menu (same actions as right-click) */}
                        <div className="absolute right-1 top-1 opacity-0 transition group-hover:opacity-100">
                          <TaskActionsDropdown
                            onEdit={() => setSelectedTask(task)}
                            onDuplicate={() => duplicateTask(task)}
                            onChangePriority={(p) => changePriority(task, p)}
                            onDelete={() => removeTask(task)}
                          />
                        </div>
                        <div className="pr-5 text-sm font-medium">{task.title}</div>
                        <div className="mt-2 flex items-center gap-2">
                          <span
                            className={`rounded px-2 py-0.5 text-[11px] font-semibold ${
                              priorityClasses[task.priority] ?? 'bg-slate-200 text-slate-600'
                            }`}
                          >
                            {t(`board.priority.${task.priority}`, task.priority)}
                          </span>
                          {task.assigneeName && (
                            <span className="text-[11px] text-slate-500 dark:text-slate-400">@{task.assigneeName}</span>
                          )}
                        </div>
                        {task.tags.length > 0 && (
                          <div className="mt-2 flex flex-wrap gap-1">
                            {task.tags.map((tag) => (
                              <span
                                key={tag}
                                className="rounded-full bg-[#1E2A44]/10 px-1.5 py-0.5 text-[10px] font-medium text-[#1E2A44] dark:bg-slate-700 dark:text-slate-200"
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
                    <p className="px-1 py-4 text-center text-xs text-slate-400">{t('board.dropHere')}</p>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      </main>

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
    </div>
  )
}
