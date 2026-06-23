import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Navbar from '../components/Navbar'
import { projectService } from '../services/projectService'
import type { Project } from '../types/project'

/**
 * Lists the current user's projects and lets them create a new one.
 * Each project links to its Kanban board.
 */
export default function ProjectsPage() {
  const { t } = useTranslation()
  const [projects, setProjects] = useState<Project[]>([])
  const [name, setName] = useState('')
  const [loading, setLoading] = useState(false)

  const load = () => {
    projectService.getProjects().then(setProjects).catch(() => {})
  }

  useEffect(load, [])

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

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-5xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">{t('projects.title')}</h1>

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
              <Link
                key={p.id}
                to={`/projects/${p.id}`}
                className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:shadow-md dark:border-slate-700 dark:bg-slate-800"
              >
                <div className="flex items-center gap-2">
                  <span className="inline-block h-3 w-3 rounded-full" style={{ background: p.color ?? '#94a3b8' }} />
                  <span className="font-semibold">{p.name}</span>
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
            ))}
          </div>
        )}
      </main>
    </div>
  )
}
