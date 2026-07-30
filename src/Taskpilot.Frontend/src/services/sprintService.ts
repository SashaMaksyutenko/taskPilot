import api from '../lib/api'
import type { Sprint, SaveSprint } from '../types/project'

/** REST calls for project sprints / iterations. */
export const sprintService = {
  list(projectId: string): Promise<Sprint[]> {
    return api.get<Sprint[]>(`/api/projects/${projectId}/sprints`).then((r) => r.data)
  },

  create(projectId: string, data: SaveSprint): Promise<Sprint> {
    return api.post<Sprint>(`/api/projects/${projectId}/sprints`, data).then((r) => r.data)
  },

  update(sprintId: string, data: SaveSprint): Promise<Sprint> {
    return api.put<Sprint>(`/api/sprints/${sprintId}`, data).then((r) => r.data)
  },

  remove(sprintId: string): Promise<void> {
    return api.delete(`/api/sprints/${sprintId}`).then(() => undefined)
  },

  /** Moves a task into a sprint, or to the backlog when sprintId is null. */
  assignTask(taskId: string, sprintId: string | null): Promise<void> {
    return api.post(`/api/tasks/${taskId}/sprint`, { sprintId }).then(() => undefined)
  },
}
