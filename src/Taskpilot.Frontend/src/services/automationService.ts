import api from '../lib/api'

/** Triggers and actions offered when building a rule (mirror the backend enums). */
export const AUTOMATION_TRIGGERS = ['OnTaskCreated', 'OnTaskStatusChanged'] as const
export const AUTOMATION_ACTIONS = ['SetPriority', 'AssignToUser', 'NotifyOwner', 'AddComment'] as const

/** A project automation rule (mirrors AutomationRuleDto). */
export interface AutomationRule {
  id: string
  projectId: string
  name: string
  isEnabled: boolean
  trigger: string
  triggerStatus: string | null
  action: string
  actionValue: string | null
  createdAt: string
}

/** Input for creating/updating a rule (mirrors SaveAutomationRuleDto). */
export interface SaveAutomationRule {
  name: string
  isEnabled: boolean
  trigger: string
  triggerStatus?: string | null
  action: string
  actionValue?: string | null
}

export const automationService = {
  list(projectId: string): Promise<AutomationRule[]> {
    return api.get<AutomationRule[]>(`/api/projects/${projectId}/automations`).then((r) => r.data)
  },

  create(projectId: string, data: SaveAutomationRule): Promise<AutomationRule> {
    return api.post<AutomationRule>(`/api/projects/${projectId}/automations`, data).then((r) => r.data)
  },

  update(ruleId: string, data: SaveAutomationRule): Promise<AutomationRule> {
    return api.put<AutomationRule>(`/api/automations/${ruleId}`, data).then((r) => r.data)
  },

  remove(ruleId: string): Promise<void> {
    return api.delete(`/api/automations/${ruleId}`).then(() => undefined)
  },
}
