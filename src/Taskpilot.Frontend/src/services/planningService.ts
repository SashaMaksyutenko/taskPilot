import api from '../lib/api'

/** One task in the "what to do next" plan (mirrors NextActionItemDto). */
export interface NextActionItem {
  taskId: string
  projectId: string
  number: number
  title: string
  projectName: string
  projectColor: string | null
  priority: string
  deadline: string | null
  isOverdue: boolean
  isBlocked: boolean
  reason: string | null
}

/** A prioritized plan across the user's open tasks (mirrors NextActionsDto). */
export interface NextActions {
  enabled: boolean
  rankedByAi: boolean
  items: NextActionItem[]
}

export const planningService = {
  /** The next tasks to work on, best first (AI-ranked when configured). */
  next(limit = 6): Promise<NextActions> {
    return api.get<NextActions>('/api/planning/next', { params: { limit } }).then((r) => r.data)
  },
}
