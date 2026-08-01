import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../lib/apiError'
import { notify } from '../lib/toast'
import { customFieldService } from '../services/customFieldService'
import type { TaskField } from '../types/project'

/**
 * The task's custom fields inside the task modal: each project-defined field (text/number/
 * select/date) with an input bound to this task's value. Editable by owner/Editors; the
 * backend validates the value against the field type. Renders nothing when the project has
 * no custom fields.
 */
export default function TaskCustomFieldsSection({ taskId, canWrite }: { taskId: string; canWrite: boolean }) {
  const { t } = useTranslation()
  const [fields, setFields] = useState<TaskField[]>([])
  const [drafts, setDrafts] = useState<Record<string, string>>({})

  useEffect(() => {
    customFieldService
      .getTaskFields(taskId)
      .then((list) => {
        setFields(list)
        setDrafts(Object.fromEntries(list.map((f) => [f.fieldId, f.value])))
      })
      .catch(() => {})
  }, [taskId])

  // Persist a value only when it actually changed; revert the draft on failure.
  const commit = async (field: TaskField, value: string) => {
    if (value === field.value) return
    try {
      const updated = await customFieldService.setTaskValue(taskId, field.fieldId, value)
      setFields(updated)
      setDrafts(Object.fromEntries(updated.map((f) => [f.fieldId, f.value])))
    } catch (e) {
      notify.error(apiErrorMessage(e))
      setDrafts((prev) => ({ ...prev, [field.fieldId]: field.value }))
    }
  }

  if (fields.length === 0) return null

  const inputCls =
    'w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm text-foreground outline-none focus:border-primary disabled:opacity-60'

  return (
    <div>
      <h3 className="mb-2 font-bold">{t('customFields.title')}</h3>
      <div className="space-y-2">
        {fields.map((field) => {
          const value = drafts[field.fieldId] ?? ''
          const set = (v: string) => setDrafts((prev) => ({ ...prev, [field.fieldId]: v }))
          return (
            <div key={field.fieldId}>
              <label className="mb-0.5 block text-xs font-medium text-muted">{field.name}</label>
              {field.type === 'Select' ? (
                <select
                  value={value}
                  disabled={!canWrite}
                  onChange={(e) => { set(e.target.value); commit(field, e.target.value) }}
                  className={inputCls}
                >
                  <option value="">—</option>
                  {field.options.map((opt) => (
                    <option key={opt} value={opt}>{opt}</option>
                  ))}
                </select>
              ) : field.type === 'Date' ? (
                <input
                  type="date"
                  value={value}
                  disabled={!canWrite}
                  onChange={(e) => { set(e.target.value); commit(field, e.target.value) }}
                  className={inputCls}
                />
              ) : (
                <input
                  type={field.type === 'Number' ? 'number' : 'text'}
                  value={value}
                  disabled={!canWrite}
                  onChange={(e) => set(e.target.value)}
                  onBlur={() => commit(field, value)}
                  className={inputCls}
                />
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
