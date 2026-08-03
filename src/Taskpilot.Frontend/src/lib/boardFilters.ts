import type { Task } from '../types/project'

/** Sentinel assignee filters. Otherwise the value is a specific user id. */
export const FILTER_ME = '__me'
export const FILTER_UNASSIGNED = '__unassigned'

/** The active board filters. Empty strings / empty tags mean "no filter". */
export interface BoardFilters {
  tags: string[]
  /** '' (any) | FILTER_ME | FILTER_UNASSIGNED | a user id. */
  assignee: string
  /** '' (any) | 'Low' | 'Medium' | 'High'. */
  priority: string
}

/** True when a task passes every active board filter. */
export function matchesBoardFilters(task: Task, filters: BoardFilters, currentUserId?: string): boolean {
  if (filters.tags.length > 0 && !task.tags.some((tag) => filters.tags.includes(tag))) return false
  if (filters.priority && task.priority !== filters.priority) return false

  switch (filters.assignee) {
    case '':
      return true
    case FILTER_UNASSIGNED:
      return !task.assigneeId
    case FILTER_ME:
      return task.assigneeId === currentUserId
    default:
      return task.assigneeId === filters.assignee
  }
}

/** Whether any filter is active (used to show a "clear" affordance). */
export function hasActiveFilters(filters: BoardFilters): boolean {
  return filters.tags.length > 0 || filters.assignee !== '' || filters.priority !== ''
}
