import api from '../lib/api'
import type { Application, MarketTaskDetail, MarketTaskListItem } from '../types/marketplace'

/** REST calls for the marketplace. */
export const marketplaceService = {
  getTasks(): Promise<MarketTaskListItem[]> {
    return api.get<MarketTaskListItem[]>('/api/marketplace/tasks').then((r) => r.data)
  },

  getTask(id: string): Promise<MarketTaskDetail> {
    return api.get<MarketTaskDetail>(`/api/marketplace/tasks/${id}`).then((r) => r.data)
  },

  createTask(data: {
    title: string
    description: string
    budget: number
    requiredSkills?: string
    deadline?: string
  }): Promise<MarketTaskDetail> {
    return api.post<MarketTaskDetail>('/api/marketplace/tasks', data).then((r) => r.data)
  },

  apply(data: { taskId: string; coverLetter: string; proposedRate: number }): Promise<Application> {
    return api.post<Application>('/api/marketplace/applications', data).then((r) => r.data)
  },

  accept(applicationId: string): Promise<void> {
    return api.post(`/api/marketplace/applications/${applicationId}/accept`).then(() => undefined)
  },

  reject(applicationId: string): Promise<void> {
    return api.post(`/api/marketplace/applications/${applicationId}/reject`).then(() => undefined)
  },

  /** Assignee submits finished work. */
  submit(taskId: string): Promise<void> {
    return api.post(`/api/marketplace/tasks/${taskId}/submit`).then(() => undefined)
  },

  /** Poster approves submitted work. */
  approve(taskId: string): Promise<void> {
    return api.post(`/api/marketplace/tasks/${taskId}/approve`).then(() => undefined)
  },
}
