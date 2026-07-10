import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import EmptyState from '../components/EmptyState'
import ConfirmDialog from '../components/ConfirmDialog'
import ProjectContextMenu from '../components/ProjectContextMenu'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import Input from '../components/ui/Input'
import { projectService } from '../services/projectService'
import { taskService } from '../services/taskService'
import { notify } from '../lib/toast'
import { useAppSelector } from '../store/hooks'
import type { Project } from '../types/project'

// Palette for colour-tagging projects.
const COLORS = ['#4F46E5', '#2563EB', '#0891B2', '#059669', '#D97706', '#DC2626', '#7C3AED', '#94A3B8']

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
  // Project awaiting delete confirmation.
  const [deleting, setDeleting] = useState<Project | null>(null)
  // Project currently being edited (rename / recolour) in the modal.
  const [editing, setEditing] = useState<Project | null>(null)
  const [editName, setEditName] = useState('')
  const [editColor, setEditColor] = useState<string | null>(null)

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
      notify.success(t('toast.projectCreated'))
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

  const duplicate = async (project: Project) => {
    await projectService.duplicate(project.id).catch(() => {})
    load()
    notify.success(t('toast.projectDuplicated'))
  }

  const archive = async (project: Project) => {
    await projectService.archive(project.id).catch(() => {})
    load()
  }

  const restore = async (project: Project) => {
    await projectService.restore(project.id).catch(() => {})
    load()
  }

  const confirmDelete = async () => {
    if (!deleting) return
    await projectService.remove(deleting.id).catch(() => {})
    setDeleting(null)
    load()
    notify.success(t('toast.projectDeleted'))
  }

  const openEdit = (project: Project) => {
    setEditing(project)
    setEditName(project.name)
    setEditColor(project.color)
  }

  const saveEdit = async () => {
    if (!editing) return
    const trimmed = editName.trim()
    if (!trimmed) return
    await projectService
      .updateProject(editing.id, { name: trimmed, description: editing.description, color: editColor })
      .catch(() => {})
    setEditing(null)
    load()
  }

  return (
    <div className="mx-auto max-w-5xl">
        <div className="mb-6 flex items-center justify-between">
          <h1 className="page-title">{t('projects.title')}</h1>
          <label className="flex cursor-pointer items-center gap-2 text-sm text-muted">
            <input
              type="checkbox"
              checked={showArchived}
              onChange={(e) => setShowArchived(e.target.checked)}
              className="h-4 w-4 accent-primary"
            />
            {t('projects.showArchived')}
          </label>
        </div>

        <div className="mb-6 flex gap-2">
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && create()}
            placeholder={t('projects.newPlaceholder')}
            className="flex-1"
          />
          <Button onClick={create} disabled={loading}>
            {t('projects.create')}
          </Button>
        </div>

        {projects.length === 0 ? (
          <EmptyState message={t('projects.empty')} />
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {projects.map((p) => (
              <ProjectContextMenu
                key={p.id}
                archived={p.isArchived}
                onEdit={() => openEdit(p)}
                onDuplicate={() => duplicate(p)}
                onExport={() => exportTasks(p)}
                onArchive={() => archive(p)}
                onRestore={() => restore(p)}
                onDelete={() => setDeleting(p)}
              >
                <Link
                  to={`/projects/${p.id}`}
                  className={`block ${p.isArchived ? 'opacity-60' : ''}`}
                >
                <Card hover className="p-5">
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
                </Card>
                </Link>
              </ProjectContextMenu>
            ))}
          </div>
        )}

        {/* Edit project modal */}
        {editing && (
          <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
            onClick={() => setEditing(null)}
          >
            <div
              className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl dark:bg-slate-800"
              onClick={(e) => e.stopPropagation()}
            >
              <h2 className="mb-4 text-lg font-bold">{t('projects.editTitle')}</h2>
              <input
                value={editName}
                onChange={(e) => setEditName(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && saveEdit()}
                placeholder={t('projects.newPlaceholder')}
                className="mb-4 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
              />
              <div className="mb-5 flex flex-wrap gap-2">
                {COLORS.map((c) => (
                  <button
                    key={c}
                    onClick={() => setEditColor(c)}
                    className={`h-7 w-7 rounded-full border-2 ${editColor === c ? 'border-primary dark:border-white' : 'border-transparent'}`}
                    style={{ background: c }}
                    aria-label={c}
                  />
                ))}
              </div>
              <div className="flex items-center gap-3">
                <button
                  onClick={saveEdit}
                  className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white hover:bg-primary-hover"
                >
                  {t('projects.save')}
                </button>
                <button onClick={() => setEditing(null)} className="text-sm font-semibold text-slate-500 hover:underline">
                  {t('projects.cancel')}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Delete confirmation */}
        <ConfirmDialog
          open={!!deleting}
          title={t('projects.deleteTitle')}
          message={t('projects.deleteConfirm', { name: deleting?.name ?? '' })}
          onConfirm={confirmDelete}
          onCancel={() => setDeleting(null)}
        />
      </div>
  )
}
