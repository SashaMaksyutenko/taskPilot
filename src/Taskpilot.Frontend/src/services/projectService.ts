import api from '../lib/api'
import type { Project } from '../types/project'

/** REST calls for projects. */
export const projectService = {
  getProjects(includeArchived = false): Promise<Project[]> {
    return api
      .get<Project[]>('/api/projects', { params: { includeArchived } })
      .then((r) => r.data)
  },

  getProject(id: string): Promise<Project> {
    return api.get<Project>(`/api/projects/${id}`).then((r) => r.data)
  },

  createProject(data: { name: string; description?: string; color?: string }): Promise<Project> {
    return api.post<Project>('/api/projects', data).then((r) => r.data)
  },

  archive(id: string): Promise<void> {
    return api.post(`/api/projects/${id}/archive`).then(() => undefined)
  },

  restore(id: string): Promise<void> {
    return api.post(`/api/projects/${id}/restore`).then(() => undefined)
  },
}
