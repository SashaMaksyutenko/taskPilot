import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { apiErrorMessage } from '../../lib/apiError'
import { customFieldService } from '../../services/customFieldService'
import type { CustomFieldDefinition, CustomFieldType } from '../../types/project'

const TYPES: CustomFieldType[] = ['Text', 'Number', 'Select', 'Date']

/**
 * Manage a project's custom-field definitions (add/remove). Shown from the board; owner/Editors.
 * Deleting a field also removes its values from every task.
 */
export default function CustomFieldsModal({ projectId, onClose }: { projectId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [fields, setFields] = useState<CustomFieldDefinition[]>([])
  const [name, setName] = useState('')
  const [type, setType] = useState<CustomFieldType>('Text')
  const [options, setOptions] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = () => customFieldService.getDefinitions(projectId).then(setFields).catch(() => {})
  useEffect(() => {
    load()
  }, [projectId])

  const add = async () => {
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      await customFieldService.createDefinition(projectId, {
        name: name.trim(),
        type,
        options: type === 'Select' ? options : undefined,
      })
      setName('')
      setOptions('')
      setType('Text')
      load()
    } catch (e) {
      setError(apiErrorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  const remove = async (id: string) => {
    await customFieldService.deleteDefinition(id).catch(() => {})
    load()
  }

  const inputCls =
    'w-full rounded-lg border border-border bg-canvas px-3 py-2 text-sm outline-none focus:border-primary'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl bg-surface p-6 shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-1 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('customFields.title')}</h2>
          <button onClick={onClose} className="text-muted hover:text-foreground">✕</button>
        </div>
        <p className="mb-4 text-xs text-muted">{t('customFields.subtitle')}</p>

        {/* Existing fields */}
        {fields.length === 0 ? (
          <p className="mb-4 text-sm text-muted">{t('customFields.empty')}</p>
        ) : (
          <ul className="mb-4 space-y-1.5">
            {fields.map((f) => (
              <li key={f.id} className="flex items-center gap-2 rounded-lg bg-canvas px-3 py-2 text-sm">
                <span className="flex-1 truncate font-medium">{f.name}</span>
                <span className="rounded-full bg-border/60 px-2 py-0.5 text-xs text-muted">
                  {t(`customFields.type.${f.type}`, f.type)}
                </span>
                <button
                  onClick={() => remove(f.id)}
                  className="text-xs font-semibold text-red-600 hover:underline"
                  aria-label={t('customFields.remove')}
                >
                  ✕
                </button>
              </li>
            ))}
          </ul>
        )}

        {/* Add a field */}
        <div className="space-y-2 border-t border-border pt-4">
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder={t('customFields.namePlaceholder')}
            className={inputCls}
          />
          <select value={type} onChange={(e) => setType(e.target.value as CustomFieldType)} className={inputCls}>
            {TYPES.map((ty) => (
              <option key={ty} value={ty}>{t(`customFields.type.${ty}`, ty)}</option>
            ))}
          </select>
          {type === 'Select' && (
            <textarea
              value={options}
              onChange={(e) => setOptions(e.target.value)}
              rows={3}
              placeholder={t('customFields.optionsPlaceholder')}
              className={inputCls}
            />
          )}
          {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}
          <button
            onClick={add}
            disabled={saving || !name.trim()}
            className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-60"
          >
            {t('customFields.add')}
          </button>
        </div>
      </div>
    </div>
  )
}
