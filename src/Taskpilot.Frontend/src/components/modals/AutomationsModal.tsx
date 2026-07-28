import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  automationService,
  AUTOMATION_ACTIONS,
  AUTOMATION_TRIGGERS,
  type AutomationRule,
} from '../../services/automationService'

const STATUSES = ['Backlog', 'InProgress', 'Review', 'Done']
const PRIORITIES = ['Low', 'Medium', 'High']

/**
 * Project automations ("robots"): the owner builds rules of the form
 * "when {trigger} → {action}". Shown from the board; owner-only.
 */
export default function AutomationsModal({
  projectId,
  members,
  onClose,
}: {
  projectId: string
  members: { id: string; name: string }[]
  onClose: () => void
}) {
  const { t } = useTranslation()
  const [rules, setRules] = useState<AutomationRule[]>([])
  const [name, setName] = useState('')
  const [trigger, setTrigger] = useState('OnTaskStatusChanged')
  const [triggerStatus, setTriggerStatus] = useState('Done')
  const [action, setAction] = useState('NotifyOwner')
  const [actionValue, setActionValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = () => automationService.list(projectId).then(setRules).catch(() => {})
  useEffect(() => {
    load()
  }, [projectId])

  // The value field's meaning depends on the chosen action.
  const needsValue = action === 'SetPriority' || action === 'AssignToUser' || action === 'AddComment'

  const add = async () => {
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      await automationService.create(projectId, {
        name: name.trim(),
        isEnabled: true,
        trigger,
        triggerStatus: trigger === 'OnTaskStatusChanged' ? triggerStatus || null : null,
        action,
        actionValue: needsValue ? actionValue || null : null,
      })
      setName('')
      setActionValue('')
      load()
    } catch (e) {
      setError((e as { response?: { data?: { error?: string } } })?.response?.data?.error ?? t('automation.failed'))
    } finally {
      setSaving(false)
    }
  }

  const toggle = async (rule: AutomationRule) => {
    await automationService
      .update(rule.id, {
        name: rule.name,
        isEnabled: !rule.isEnabled,
        trigger: rule.trigger,
        triggerStatus: rule.triggerStatus,
        action: rule.action,
        actionValue: rule.actionValue,
      })
      .catch(() => {})
    load()
  }

  const remove = async (id: string) => {
    await automationService.remove(id).catch(() => {})
    load()
  }

  const memberName = (id: string | null) => members.find((m) => m.id === id)?.name ?? id ?? ''

  // A readable "When … → …" summary for a saved rule.
  const summary = (r: AutomationRule) => {
    const when =
      r.trigger === 'OnTaskCreated'
        ? t('automation.trigger.OnTaskCreated')
        : `${t('automation.trigger.OnTaskStatusChanged')} ${r.triggerStatus ? t(`board.status.${r.triggerStatus}`, r.triggerStatus) : t('automation.anyStatus')}`
    let then = t(`automation.action.${r.action}`, r.action)
    if (r.action === 'SetPriority' && r.actionValue) then += `: ${t(`board.priority.${r.actionValue}`, r.actionValue)}`
    else if (r.action === 'AssignToUser') then += `: ${memberName(r.actionValue)}`
    else if (r.action === 'AddComment' && r.actionValue) then += `: “${r.actionValue}”`
    return `${when} → ${then}`
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl bg-surface p-6 shadow-elevated"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-1 flex items-center justify-between">
          <h2 className="text-lg font-bold">{t('automation.title')}</h2>
          <button onClick={onClose} className="text-muted hover:text-foreground">✕</button>
        </div>
        <p className="mb-4 text-xs text-muted">{t('automation.subtitle')}</p>

        {/* Existing rules */}
        {rules.length === 0 ? (
          <p className="mb-4 text-sm text-muted">{t('automation.empty')}</p>
        ) : (
          <ul className="mb-5 space-y-2">
            {rules.map((r) => (
              <li key={r.id} className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm">
                <span className={`min-w-0 flex-1 ${r.isEnabled ? '' : 'opacity-50'}`}>
                  <span className="font-medium">{r.name}</span>
                  <span className="block text-xs text-muted">{summary(r)}</span>
                </span>
                <button
                  onClick={() => toggle(r)}
                  className="flex-none rounded-full border border-border px-2 py-0.5 text-[11px] font-semibold hover:bg-canvas"
                >
                  {r.isEnabled ? t('automation.on') : t('automation.off')}
                </button>
                <button onClick={() => remove(r.id)} className="flex-none text-xs font-semibold text-red-600 hover:underline">
                  {t('automation.delete')}
                </button>
              </li>
            ))}
          </ul>
        )}

        {/* New rule builder */}
        <div className="rounded-lg border border-border bg-canvas/50 p-4">
          <h3 className="mb-3 text-sm font-semibold">{t('automation.newRule')}</h3>

          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder={t('automation.namePlaceholder')}
            className="mb-3 w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-primary"
          />

          <div className="mb-2 grid grid-cols-2 gap-2">
            <div>
              <label className="mb-1 block text-xs font-medium text-muted">{t('automation.when')}</label>
              <select
                value={trigger}
                onChange={(e) => setTrigger(e.target.value)}
                className="w-full rounded-lg border border-border bg-surface px-2 py-2 text-sm outline-none"
              >
                {AUTOMATION_TRIGGERS.map((tr) => (
                  <option key={tr} value={tr}>{t(`automation.trigger.${tr}`)}</option>
                ))}
              </select>
            </div>
            {trigger === 'OnTaskStatusChanged' && (
              <div>
                <label className="mb-1 block text-xs font-medium text-muted">{t('automation.status')}</label>
                <select
                  value={triggerStatus}
                  onChange={(e) => setTriggerStatus(e.target.value)}
                  className="w-full rounded-lg border border-border bg-surface px-2 py-2 text-sm outline-none"
                >
                  <option value="">{t('automation.anyStatus')}</option>
                  {STATUSES.map((s) => (
                    <option key={s} value={s}>{t(`board.status.${s}`, s)}</option>
                  ))}
                </select>
              </div>
            )}
          </div>

          <div className="mb-2 grid grid-cols-2 gap-2">
            <div>
              <label className="mb-1 block text-xs font-medium text-muted">{t('automation.then')}</label>
              <select
                value={action}
                onChange={(e) => { setAction(e.target.value); setActionValue('') }}
                className="w-full rounded-lg border border-border bg-surface px-2 py-2 text-sm outline-none"
              >
                {AUTOMATION_ACTIONS.map((a) => (
                  <option key={a} value={a}>{t(`automation.action.${a}`)}</option>
                ))}
              </select>
            </div>
            {needsValue && (
              <div>
                <label className="mb-1 block text-xs font-medium text-muted">{t('automation.value')}</label>
                {action === 'SetPriority' ? (
                  <select
                    value={actionValue}
                    onChange={(e) => setActionValue(e.target.value)}
                    className="w-full rounded-lg border border-border bg-surface px-2 py-2 text-sm outline-none"
                  >
                    <option value="">—</option>
                    {PRIORITIES.map((p) => (
                      <option key={p} value={p}>{t(`board.priority.${p}`, p)}</option>
                    ))}
                  </select>
                ) : action === 'AssignToUser' ? (
                  <select
                    value={actionValue}
                    onChange={(e) => setActionValue(e.target.value)}
                    className="w-full rounded-lg border border-border bg-surface px-2 py-2 text-sm outline-none"
                  >
                    <option value="">{t('automation.selectMember')}</option>
                    {members.map((m) => (
                      <option key={m.id} value={m.id}>{m.name}</option>
                    ))}
                  </select>
                ) : (
                  <input
                    value={actionValue}
                    onChange={(e) => setActionValue(e.target.value)}
                    placeholder={t('automation.commentPlaceholder')}
                    className="w-full rounded-lg border border-border bg-surface px-2 py-2 text-sm outline-none focus:border-primary"
                  />
                )}
              </div>
            )}
          </div>

          {error && <p className="mb-2 text-sm text-red-600 dark:text-red-400">{error}</p>}
          <button
            onClick={add}
            disabled={saving}
            className="rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-50"
          >
            {t('automation.add')}
          </button>
        </div>
      </div>
    </div>
  )
}
