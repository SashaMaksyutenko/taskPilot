import api from '../lib/api'
import type { Task, TaskStatus } from '../types/project'

/** REST calls for project tasks. */
export const taskService = {
  getTasks(projectId: string, status?: TaskStatus): Promise<Task[]> {
    return api
      .get<Task[]>(`/api/projects/${projectId}/tasks`, { params: status ? { status } : {} })
      .then((r) => r.data)
  },

  createTask(
    projectId: string,
    data: { title: string; description?: string; priority?: string; deadline?: string },
  ): Promise<Task> {
    return api.post<Task>(`/api/projects/${projectId}/tasks`, data).then((r) => r.data)
  },

  updateTask(
    taskId: string,
    data: {
      title: string
      description?: string | null
      priority?: string
      assigneeId?: string | null
      deadline?: string | null
    },
  ): Promise<Task> {
    return api.put<Task>(`/api/tasks/${taskId}`, data).then((r) => r.data)
  },

  changeStatus(taskId: string, status: TaskStatus): Promise<Task> {
    return api.post<Task>(`/api/tasks/${taskId}/status`, { status }).then((r) => r.data)
  },

  deleteTask(taskId: string): Promise<void> {
    return api.delete(`/api/tasks/${taskId}`).then(() => undefined)
  },

  /** Downloads the project's tasks as a CSV blob. */
  exportCsv(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/tasks/export`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },
}
