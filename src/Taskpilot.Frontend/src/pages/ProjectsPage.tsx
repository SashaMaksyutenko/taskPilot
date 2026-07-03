import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Navbar from '../components/Navbar'
import ProjectContextMenu from '../components/ProjectContextMenu'
import { projectService } from '../services/projectService'
import { taskService } from '../services/taskService'
import { useAppSelector } from '../store/hooks'
import type { Project } from '../types/project'

/**
 * Lists the current user's projects and lets them create a new one.
 * Each project links to its Kanban board.
 */
export default function ProjectsPage() {
  const { t } = useTranslation()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [projects, setProjects] = useState<Project[]>([])
  const [name, setName] = useState('')
  const [loading, setLoading] = useState(false)
  // When on, archived projects are listed too (and can be restored).
  const [showArchived, setShowArchived] = useState(false)

  const load = () => {
    projectService.getProjects(showArchived).then(setProjects).catch(() => {})
  }

  useEffect(load, [showArchived])

  const create = async () => {
    const trimmed = name.trim()
    if (!trimmed) return
    setLoading(true)
    try {
      await projectService.createProject({ name: trimmed })
      setName('')
      load()
    } finally {
      setLoading(false)
    }
  }

  // Context-menu actions.
  const exportTasks = async (project: Project) => {
    const blob = await taskService.exportCsv(project.id).catch(() => null)
    if (!blob) return
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${project.name}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  const archive = async (project: Project) => {
    await projectService.archive(project.id).catch(() => {})
    load()
  }

  const restore = async (project: Project) => {
    await projectService.restore(project.id).catch(() => {})
    load()
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-6 py-8">
        <div className="mb-6 flex items-center justify-between">
          <h1 className="text-2xl font-bold">{t('projects.title')}</h1>
          <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
            <input
              type="checkbox"
              checked={showArchived}
              onChange={(e) => setShowArchived(e.target.checked)}
              className="h-4 w-4 accent-[#1E2A44]"
            />
            {t('projects.showArchived')}
          </label>
        </div>

        {/* Create project */}
        <div className="mb-6 flex gap-2">
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && create()}
            placeholder={t('projects.newPlaceholder')}
            className="flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-800"
          />
          <button
            onClick={create}
            disabled={loading}
            className="rounded-lg bg-[#1E2A44] px-5 font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
          >
            {t('projects.create')}
          </button>
        </div>

        {projects.length === 0 ? (
          <p className="text-slate-400">{t('projects.empty')}</p>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {projects.map((p) => (
              <ProjectContextMenu
                key={p.id}
                archived={p.isArchived}
                onExport={() => exportTasks(p)}
                onArchive={() => archive(p)}
                onRestore={() => restore(p)}
              >
                <Link
                  to={`/projects/${p.id}`}
                  className={`block rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:shadow-md dark:border-slate-700 dark:bg-slate-800 ${
                    p.isArchived ? 'opacity-60' : ''
                  }`}
                >
                <div className="flex items-center gap-2">
                  <span className="inline-block h-3 w-3 rounded-full" style={{ background: p.color ?? '#94a3b8' }} />
                  <span className="font-semibold">{p.name}</span>
                  {p.isArchived && (
                    <span className="rounded-full bg-slate-200 px-2 py-0.5 text-[11px] font-semibold text-slate-600 dark:bg-slate-700 dark:text-slate-300">
                      {t('projects.archived')}
                    </span>
                  )}
                  {!!currentUserId && p.ownerId !== currentUserId && (
                    <span className="rounded-full bg-sky-100 px-2 py-0.5 text-[11px] font-semibold text-sky-700 dark:bg-sky-900/40 dark:text-sky-300">
                      {t('projects.shared')}
                    </span>
                  )}
                  {p.memberCount > 0 && (
                    <span className="ml-auto text-xs text-slate-400" title={t('projects.members', { count: p.memberCount })}>
                      👥 {p.memberCount + 1}
                    </span>
                  )}
                </div>
                <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">{t('projects.tasks', { count: p.taskCount })}</p>

                {/* Progress: share of tasks in the Done status. */}
                {(() => {
                  const pct = p.taskCount > 0 ? Math.round((p.completedTaskCount / p.taskCount) * 100) : 0
                  return (
                    <div className="mt-3">
                      <div className="mb-1 flex justify-between text-xs text-slate-500 dark:text-slate-400">
                        <span>{t('projects.progress', { done: p.completedTaskCount, total: p.taskCount })}</span>
                        <span>{pct}%</span>
                      </div>
                      <div className="h-2 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700">
                        <div className="h-full rounded-full bg-green-500 transition-all" style={{ width: `${pct}%` }} />
                      </div>
                    </div>
                  )
                })()}
                </Link>
              </ProjectContextMenu>
            ))}
          </div>
        )}
      </main>
    </div>
  )
}
