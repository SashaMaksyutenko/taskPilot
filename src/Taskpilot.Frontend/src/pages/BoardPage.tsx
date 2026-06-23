import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import Navbar from '../components/Navbar'
import TaskActionsDropdown from '../components/TaskActionsDropdown'
import TaskContextMenu from '../components/TaskContextMenu'
import TaskDetailModal from '../components/TaskDetailModal'
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
  const [project, setProject] = useState<Project | null>(null)
  const [tasks, setTasks] = useState<Task[]>([])
  const [newTitle, setNewTitle] = useState('')
  const [selectedTask, setSelectedTask] = useState<Task | null>(null)
  const draggingId = useRef<string | null>(null)

  useEffect(() => {
    if (!projectId) return
    projectService.getProject(projectId).then(setProject).catch(() => {})
    taskService.getTasks(projectId).then(setTasks).catch(() => {})
  }, [projectId])

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

  const exportCsv = async () => {
    const blob = await taskService.exportCsv(projectId).catch(() => null)
    if (!blob) return
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${project?.name ?? 'tasks'}.csv`
    a.click()
    URL.revokeObjectURL(url)
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
            onClick={exportCsv}
            className="ml-auto rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold hover:bg-white dark:border-slate-600 dark:hover:bg-slate-800"
          >
            {t('board.exportCsv')}
          </button>
        </div>

        {/* Add task */}
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

        {/* Columns */}
        <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
          {STATUS_COLUMNS.map((col) => {
            const colTasks = tasks.filter((t) => t.status === col.key)
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
    </div>
  )
}
