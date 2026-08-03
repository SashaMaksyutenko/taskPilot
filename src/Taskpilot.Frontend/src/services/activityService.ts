import api from '../lib/api'

/** One entry in a project's activity feed (mirrors ActivityEntryDto). */
export interface ActivityEntry {
  id: string
  action: string
  details: string | null
  createdAt: string
  taskId: string | null
  actorId: string | null
  actorName: string
  actorAvatarUrl: string | null
}

export const activityService = {
  /** Recent task actions in a project, newest first. */
  get(projectId: string, limit = 30): Promise<ActivityEntry[]> {
    return api.get<ActivityEntry[]>(`/api/projects/${projectId}/activity`, { params: { limit } }).then((r) => r.data)
  },
}
