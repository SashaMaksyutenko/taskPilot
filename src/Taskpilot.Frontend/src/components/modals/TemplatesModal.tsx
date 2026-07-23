import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { projectTemplateService } from '../../services/projectTemplateService'
import { notify } from '../../lib/toast'
import type { ProjectTemplate, TemplateTask } from '../../types/template'

/**
 * Lists the user's project templates and lets them start a new project from one,
 * preview its tasks, or delete it. Opened from the Templates button on the projects
 * page. Creating from a template is handled here; the parent just reloads its list.
 */
export default function TemplatesModal({
  open,
  onClose,
  onProjectCreated,
}: {
  open: boolean
  onClose: () => void
  /** Called after a project is created so the projects list can refresh. */
  onProjectCreated: () => void
}) {
  const { t } = useTranslation()
  const [templates, setTemplates] = useState<ProjectTemplate[] | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)
  // The template whose task list is expanded, and the tasks once loaded.
  const [previewId, setPreviewId] = useState<string | null>(null)
  const [previewTasks, setPreviewTasks] = useState<TemplateTask[] | null>(null)

  // Load once per open. A ref guards against StrictMode's double-invoke in dev.
  const loaded = useRef(false)
  useEffect(() => {
    if (!open) {
      loaded.current = false
      return
    }
    if (loaded.current) return
    loaded.current = true
    setTemplates(null)
    setPreviewId(null)
    projectTemplateService
      .getTemplates()
      .then(setTemplates)
      .catch(() => setTemplates([]))
  }, [open])

  if (!open) return null

  const applyTemplate = async (template: ProjectTemplate) => {
    setBusyId(template.id)
    try {
      await projectTemplateService.createProjectFromTemplate(template.id)
      notify.success(t('templates.created', { name: template.name }))
      onProjectCreated()
      onClose()
    } catch {
      notify.error(t('templates.createFailed'))
    } finally {
      setBusyId(null)
    }
  }

  const remove = async (template: ProjectTemplate) => {
    // Optimistic: drop the row, restore it if the server refuses.
    setTemplates((prev) => prev?.filter((x) => x.id !== template.id) ?? null)
    if (previewId === template.id) setPreviewId(null)
    try {
      await projectTemplateService.deleteTemplate(template.id)
    } catch {
      setTemplates((prev) => (prev ? [template, ...prev] : prev))
      notify.error(t('templates.deleteFailed'))
    }
  }

  const togglePreview = async (template: ProjectTemplate) => {
    if (previewId === template.id) {
      setPreviewId(null)
      return
    }
    setPreviewId(template.id)
    setPreviewTasks(null)
    const detail = await projectTemplateService.getTemplate(template.id).catch(() => null)
    // Only apply if this is still the open preview (the user may have toggled another).
    if (detail) setPreviewTasks(detail.tasks)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="max-h-[80vh] w-full max-w-lg overflow-y-auto rounded-xl bg-surface p-6 shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="mb-4 text-lg font-bold">{t('templates.title')}</h2>

        {templates === null && <p className="text-sm text-muted">{t('templates.loading')}</p>}
        {templates?.length === 0 && <p className="text-sm text-muted">{t('templates.empty')}</p>}

        {!!templates?.length && (
          <ul className="space-y-2">
            {templates.map((tpl) => (
              <li key={tpl.id} className="rounded-lg border border-border p-3">
                <div className="flex items-center gap-2">
                  <span className="inline-block h-3 w-3 flex-none rounded-full" style={{ background: tpl.color ?? '#94a3b8' }} />
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-semibold">{tpl.name}</div>
                    <button
                      type="button"
                      onClick={() => togglePreview(tpl)}
                      aria-expanded={previewId === tpl.id}
                      className="text-xs text-muted hover:underline"
                    >
                      {t('templates.taskCount', { count: tpl.taskCount })} {previewId === tpl.id ? '▾' : '▸'}
                    </button>
                  </div>
                  <button
                    type="button"
                    onClick={() => applyTemplate(tpl)}
                    disabled={busyId === tpl.id}
                    className="flex-none rounded-lg bg-primary px-3 py-1.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
                  >
                    {busyId === tpl.id ? t('templates.creating') : t('templates.use')}
                  </button>
                  <button
                    type="button"
                    onClick={() => remove(tpl)}
                    title={t('templates.delete')}
                    aria-label={t('templates.delete')}
                    className="flex-none text-muted hover:text-red-600"
                  >
                    ×
                  </button>
                </div>

                {/* Expanded task preview. */}
                {previewId === tpl.id && (
                  <div className="mt-2 border-t border-border pt-2 text-sm">
                    {previewTasks === null && <p className="text-xs text-muted">{t('templates.loading')}</p>}
                    {previewTasks?.length === 0 && <p className="text-xs text-muted">{t('templates.noTasks')}</p>}
                    {previewTasks?.map((task) => (
                      <div key={task.id} className="flex items-baseline gap-2 py-0.5">
                        {/* Subtasks are indented so the structure reads at a glance. */}
                        <span className={`min-w-0 flex-1 truncate ${task.parentTemplateTaskId ? 'pl-4 text-muted' : ''}`}>
                          {task.parentTemplateTaskId ? '↳ ' : ''}{task.title}
                        </span>
                        {task.deadlineOffsetDays !== null && (
                          <span className="flex-none text-xs text-muted">
                            {t('templates.dueInDays', { count: task.deadlineOffsetDays })}
                          </span>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}

        <div className="mt-5 text-right">
          <button onClick={onClose} className="text-sm font-semibold text-muted hover:underline">
            {t('templates.close')}
          </button>
        </div>
      </div>
    </div>
  )
}
