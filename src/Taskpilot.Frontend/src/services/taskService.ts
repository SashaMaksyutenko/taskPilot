import api from '../lib/api'
import type { Task, TaskComment, TaskStatus } from '../types/project'

/** REST calls for project tasks. */
export const taskService = {
  getTasks(projectId: string, status?: TaskStatus): Promise<Task[]> {
    return api
      .get<Task[]>(`/api/projects/${projectId}/tasks`, { params: status ? { status } : {} })
      .then((r) => r.data)
  },

  createTask(
    projectId: string,
    data: {
      title: string
      description?: string
      priority?: string
      deadline?: string
      tags?: string[]
      parentTaskId?: string
    },
  ): Promise<Task> {
    return api.post<Task>(`/api/projects/${projectId}/tasks`, data).then((r) => r.data)
  },

  /** Lists a task's subtasks (children). */
  getSubtasks(taskId: string): Promise<Task[]> {
    return api.get<Task[]>(`/api/tasks/${taskId}/subtasks`).then((r) => r.data)
  },

  updateTask(
    taskId: string,
    data: {
      title: string
      description?: string | null
      priority?: string
      assigneeId?: string | null
      deadline?: string | null
      tags?: string[]
    },
  ): Promise<Task> {
    return api.put<Task>(`/api/tasks/${taskId}`, data).then((r) => r.data)
  },

  changeStatus(taskId: string, status: TaskStatus): Promise<Task> {
    return api.post<Task>(`/api/tasks/${taskId}/status`, { status }).then((r) => r.data)
  },

  /** Changes the status of several tasks at once. */
  bulkChangeStatus(taskIds: string[], status: TaskStatus): Promise<{ changed: number }> {
    return api.post<{ changed: number }>('/api/tasks/bulk/status', { taskIds, status }).then((r) => r.data)
  },

  /** Deletes several tasks at once. */
  bulkDelete(taskIds: string[]): Promise<{ deleted: number }> {
    return api.post<{ deleted: number }>('/api/tasks/bulk/delete', { taskIds }).then((r) => r.data)
  },

  /** Creates a copy of a task in the same project. */
  duplicateTask(taskId: string): Promise<Task> {
    return api.post<Task>(`/api/tasks/${taskId}/duplicate`).then((r) => r.data)
  },

  /** Moves a task to another project. */
  moveTask(taskId: string, projectId: string): Promise<Task> {
    return api.post<Task>(`/api/tasks/${taskId}/move`, { projectId }).then((r) => r.data)
  },

  /** Starts the task's time tracker. */
  startTimer(taskId: string): Promise<Task> {
    return api.post<Task>(`/api/tasks/${taskId}/timer/start`).then((r) => r.data)
  },

  /** Stops the task's time tracker. */
  stopTimer(taskId: string): Promise<Task> {
    return api.post<Task>(`/api/tasks/${taskId}/timer/stop`).then((r) => r.data)
  },

  deleteTask(taskId: string): Promise<void> {
    return api.delete(`/api/tasks/${taskId}`).then(() => undefined)
  },

  /** Lists a task's comments, oldest first. */
  getComments(taskId: string): Promise<TaskComment[]> {
    return api.get<TaskComment[]>(`/api/tasks/${taskId}/comments`).then((r) => r.data)
  },

  /** Adds a comment to a task. */
  addComment(taskId: string, body: string): Promise<TaskComment> {
    return api.post<TaskComment>(`/api/tasks/${taskId}/comments`, { body }).then((r) => r.data)
  },

  /** Deletes a comment authored by the current user. */
  deleteComment(commentId: string): Promise<void> {
    return api.delete(`/api/tasks/comments/${commentId}`).then(() => undefined)
  },

  /** Downloads the project's tasks as a CSV blob. */
  exportCsv(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/tasks/export`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the project's tasks as an Excel (.xlsx) blob. */
  exportXlsx(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/tasks/export/xlsx`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the project's tasks as a PDF blob. */
  exportPdf(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/tasks/export/pdf`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the analytical project report as a PDF blob. */
  reportPdf(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/report/pdf`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the analytical project report as an Excel (.xlsx) blob. */
  reportXlsx(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/report/xlsx`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the team-performance report as a PDF blob. */
  teamReportPdf(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/report/team/pdf`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Downloads the team-performance report as an Excel (.xlsx) blob. */
  teamReportXlsx(projectId: string): Promise<Blob> {
    return api
      .get(`/api/projects/${projectId}/report/team/xlsx`, { responseType: 'blob' })
      .then((r) => r.data as Blob)
  },

  /** Lists the current user's scheduled report emails for a project. */
  getReportSchedules(projectId: string): Promise<ReportSchedule[]> {
    return api.get<ReportSchedule[]>(`/api/projects/${projectId}/report/schedules`).then((r) => r.data)
  },

  /** Schedules a recurring report email for a project. */
  createReportSchedule(
    projectId: string,
    data: { kind: string; format: string; frequency: string },
  ): Promise<ReportSchedule> {
    return api
      .post<ReportSchedule>(`/api/projects/${projectId}/report/schedules`, data)
      .then((r) => r.data)
  },

  /** Deletes a scheduled report email. */
  deleteReportSchedule(projectId: string, scheduleId: string): Promise<void> {
    return api.delete(`/api/projects/${projectId}/report/schedules/${scheduleId}`).then(() => undefined)
  },

  /** Moves only a task's deadline (calendar drag-and-drop); other fields stay. */
  reschedule(taskId: string, deadline: string | null): Promise<Task> {
    return api.post<Task>(`/api/tasks/${taskId}/reschedule`, { deadline }).then((r) => r.data)
  },

  /** Whether AI features (e.g. subtask suggestions) are configured on the server. */
  aiEnabled(): Promise<boolean> {
    return api.get<{ enabled: boolean }>('/api/tasks/ai/status').then((r) => r.data.enabled)
  },

  /** Asks the AI to propose subtasks for a task; returns suggested titles. */
  suggestSubtasks(taskId: string): Promise<string[]> {
    return api
      .post<{ suggestions: string[] }>(`/api/tasks/${taskId}/ai/subtasks`)
      .then((r) => r.data.suggestions)
  },

  /** Lists a task's deadline-extension requests (newest first). */
  getExtensionRequests(taskId: string): Promise<ExtensionRequest[]> {
    return api.get<ExtensionRequest[]>(`/api/tasks/${taskId}/extension-requests`).then((r) => r.data)
  },

  /** Raises a pending extension request for a task. */
  requestExtension(taskId: string, requestedDeadline: string, reason: string): Promise<ExtensionRequest> {
    return api
      .post<ExtensionRequest>(`/api/tasks/${taskId}/extension-requests`, { requestedDeadline, reason })
      .then((r) => r.data)
  },

  /** Approves or rejects an extension request (project owner only). */
  decideExtension(requestId: string, approve: boolean): Promise<ExtensionRequest> {
    return api
      .post<ExtensionRequest>(`/api/extension-requests/${requestId}/decision`, { approve })
      .then((r) => r.data)
  },
}

/** A recurring report email (mirrors the backend ReportScheduleDto). */
export interface ReportSchedule {
  id: string
  projectId: string
  kind: 'Project' | 'Team'
  format: 'Pdf' | 'Xlsx'
  frequency: 'Daily' | 'Weekly' | 'Monthly'
  lastSentAt: string | null
  createdAt: string
}

/** A task deadline-extension request (mirrors the backend ExtensionRequestDto). */
export interface ExtensionRequest {
  id: string
  taskId: string
  requesterId: string
  requesterName: string
  requestedDeadline: string
  reason: string
  status: 'Pending' | 'Approved' | 'Rejected'
  createdAt: string
  decidedAt: string | null
  canDecide: boolean
}
