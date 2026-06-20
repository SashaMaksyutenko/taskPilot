import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { projectService } from '../services/projectService'
import type { Project } from '../types/project'

/**
 * Lists the current user's projects and lets them create a new one.
 * Each project links to its Kanban board.
 */
export default function ProjectsPage() {
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
    <div className="min-h-screen bg-slate-50 px-6 py-8 text-[#1E2A44]">
      <div className="mx-auto max-w-5xl">
        <div className="mb-6 flex items-center gap-3">
          <img src="/logo-mark.svg" alt="" className="h-8 w-8" />
          <h1 className="text-2xl font-bold">Projects</h1>
          <Link to="/" className="ml-auto text-sm text-slate-500 hover:underline">
            Home
          </Link>
        </div>

        {/* Create project */}
        <div className="mb-6 flex gap-2">
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && create()}
            placeholder="New project name…"
            className="flex-1 rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-[#1E2A44]"
          />
          <button
            onClick={create}
            disabled={loading}
            className="rounded-lg bg-[#1E2A44] px-5 font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
          >
            Create
          </button>
        </div>

        {/* Project cards */}
        {projects.length === 0 ? (
          <p className="text-slate-400">No projects yet. Create your first one above.</p>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {projects.map((p) => (
              <Link
                key={p.id}
                to={`/projects/${p.id}`}
                className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:shadow-md"
              >
                <div className="flex items-center gap-2">
                  <span
                    className="inline-block h-3 w-3 rounded-full"
                    style={{ background: p.color ?? '#94a3b8' }}
                  />
                  <span className="font-semibold">{p.name}</span>
                </div>
                <p className="mt-2 text-sm text-slate-500">{p.taskCount} task(s)</p>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
