import api from '../lib/api'
import type { CustomFieldDefinition, CustomFieldType, TaskField } from '../types/project'

/** Custom fields: project-level definitions and per-task values. */
export const customFieldService = {
  /** A project's custom-field definitions, in order. */
  getDefinitions(projectId: string): Promise<CustomFieldDefinition[]> {
    return api.get<CustomFieldDefinition[]>(`/api/projects/${projectId}/custom-fields`).then((r) => r.data)
  },

  /** Adds a custom-field definition to a project. `options` is newline-separated (Select only). */
  createDefinition(
    projectId: string,
    field: { name: string; type: CustomFieldType; options?: string },
  ): Promise<CustomFieldDefinition> {
    return api.post<CustomFieldDefinition>(`/api/projects/${projectId}/custom-fields`, field).then((r) => r.data)
  },

  /** Deletes a definition and every task value for it. */
  deleteDefinition(fieldId: string): Promise<void> {
    return api.delete(`/api/custom-fields/${fieldId}`).then(() => undefined)
  },

  /** A task's custom fields with their current values. */
  getTaskFields(taskId: string): Promise<TaskField[]> {
    return api.get<TaskField[]>(`/api/tasks/${taskId}/fields`).then((r) => r.data)
  },

  /** Sets or clears (empty value) a task's value for a field; returns the task's fields. */
  setTaskValue(taskId: string, fieldId: string, value: string): Promise<TaskField[]> {
    return api.put<TaskField[]>(`/api/tasks/${taskId}/fields/${fieldId}`, { value }).then((r) => r.data)
  },
}
