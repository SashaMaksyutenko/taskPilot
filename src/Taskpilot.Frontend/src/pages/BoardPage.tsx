import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
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
  const { projectId = '' } = useParams()
  const [project, setProject] = useState<Project | null>(null)
  const [tasks, setTasks] = useState<Task[]>([])
  const [newTitle, setNewTitle] = useState('')

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
    // Optimistic update, then persist.
    setTasks((prev) => prev.map((t) => (t.id === taskId ? { ...t, status } : t)))
    try {
      await taskService.changeStatus(taskId, status)
    } catch {
      // On failure, reload the true state.
      taskService.getTasks(projectId).then(setTasks).catch(() => {})
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 px-6 py-6 text-[#1E2A44]">
      <div className="mb-5 flex items-center gap-3">
        <Link to="/projects" className="text-sm text-slate-500 hover:underline">
          ← Projects
        </Link>
        <h1 className="text-xl font-bold">{project?.name ?? 'Board'}</h1>
      </div>

      {/* Add task */}
      <div className="mb-5 flex max-w-md gap-2">
        <input
          value={newTitle}
          onChange={(e) => setNewTitle(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && addTask()}
          placeholder="Add a task (goes to Backlog)…"
          className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[#1E2A44]"
        />
        <button onClick={addTask} className="rounded-lg bg-[#1E2A44] px-4 text-sm font-semibold text-white">
          Add
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
              onDrop={(e) => onDrop(col.key, e.dataTransfer.getData('taskId'))}
              className="rounded-xl bg-slate-100 p-3"
            >
              <div className="mb-3 flex items-center justify-between px-1 text-sm font-bold">
                <span>{col.label}</span>
                <span className="text-slate-400">{colTasks.length}</span>
              </div>

              <div className="space-y-2">
                {colTasks.map((t) => (
                  <div
                    key={t.id}
                    draggable
                    onDragStart={(e) => e.dataTransfer.setData('taskId', t.id)}
                    className="cursor-grab rounded-lg border border-slate-200 bg-white p-3 shadow-sm active:cursor-grabbing"
                  >
                    <div className="text-sm font-medium">{t.title}</div>
                    <div className="mt-2 flex items-center gap-2">
                      <span
                        className={`rounded px-2 py-0.5 text-[11px] font-semibold ${
                          priorityClasses[t.priority] ?? 'bg-slate-200 text-slate-600'
                        }`}
                      >
                        {t.priority}
                      </span>
                      {t.assigneeName && (
                        <span className="text-[11px] text-slate-500">@{t.assigneeName}</span>
                      )}
                    </div>
                  </div>
                ))}
                {colTasks.length === 0 && (
                  <p className="px-1 py-4 text-center text-xs text-slate-400">Drop tasks here</p>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
