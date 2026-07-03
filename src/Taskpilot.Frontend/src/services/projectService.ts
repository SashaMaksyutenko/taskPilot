import api from '../lib/api'
import type { Project, ProjectMember } from '../types/project'

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

  updateProject(
    id: string,
    data: { name: string; description?: string | null; color?: string | null },
  ): Promise<Project> {
    return api.put<Project>(`/api/projects/${id}`, data).then((r) => r.data)
  },

  /** Creates a copy of a project (cloning its tasks). */
  duplicate(id: string): Promise<Project> {
    return api.post<Project>(`/api/projects/${id}/duplicate`).then((r) => r.data)
  },

  archive(id: string): Promise<void> {
    return api.post(`/api/projects/${id}/archive`).then(() => undefined)
  },

  restore(id: string): Promise<void> {
    return api.post(`/api/projects/${id}/restore`).then(() => undefined)
  },

  getMembers(projectId: string): Promise<ProjectMember[]> {
    return api.get<ProjectMember[]>(`/api/projects/${projectId}/members`).then((r) => r.data)
  },

  addMember(projectId: string, userId: string, role: string): Promise<ProjectMember> {
    return api
      .post<ProjectMember>(`/api/projects/${projectId}/members`, { userId, role })
      .then((r) => r.data)
  },

  setMemberRole(projectId: string, userId: string, role: string): Promise<ProjectMember> {
    return api
      .put<ProjectMember>(`/api/projects/${projectId}/members/${userId}/role`, { role })
      .then((r) => r.data)
  },

  removeMember(projectId: string, userId: string): Promise<void> {
    return api.delete(`/api/projects/${projectId}/members/${userId}`).then(() => undefined)
  },

  leaveProject(projectId: string): Promise<void> {
    return api.post(`/api/projects/${projectId}/leave`).then(() => undefined)
  },
}
