import api from '../lib/api'
import type { Epic, SaveEpic } from '../types/project'

/** REST calls for project epics. */
export const epicService = {
  list(projectId: string): Promise<Epic[]> {
    return api.get<Epic[]>(`/api/projects/${projectId}/epics`).then((r) => r.data)
  },

  create(projectId: string, data: SaveEpic): Promise<Epic> {
    return api.post<Epic>(`/api/projects/${projectId}/epics`, data).then((r) => r.data)
  },

  update(epicId: string, data: SaveEpic): Promise<Epic> {
    return api.put<Epic>(`/api/epics/${epicId}`, data).then((r) => r.data)
  },

  remove(epicId: string): Promise<void> {
    return api.delete(`/api/epics/${epicId}`).then(() => undefined)
  },

  /** Moves a task into an epic, or ungroups it when epicId is null. */
  assignTask(taskId: string, epicId: string | null): Promise<void> {
    return api.post(`/api/tasks/${taskId}/epic`, { epicId }).then(() => undefined)
  },
}
