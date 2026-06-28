// Types mirroring the backend project/task DTOs.

export interface Project {
  id: string
  name: string
  description: string | null
  color: string | null
  ownerId: string
  ownerName: string
  taskCount: number
  completedTaskCount: number
  isArchived: boolean
  createdAt: string
  archivedAt: string | null
}

export interface Task {
  id: string
  projectId: string
  title: string
  description: string | null
  status: TaskStatus
  priority: string
  assigneeId: string | null
  assigneeName: string | null
  creatorId: string
  creatorName: string
  parentTaskId: string | null
  deadline: string | null
  createdAt: string
  updatedAt: string | null
  completedAt: string | null
}

export interface TaskComment {
  id: string
  taskId: string
  authorId: string
  authorName: string
  authorAvatarUrl: string | null
  body: string
  createdAt: string
  updatedAt: string | null
}

/** The four Kanban columns, in order. */
export type TaskStatus = 'Backlog' | 'InProgress' | 'Review' | 'Done'

export const STATUS_COLUMNS: { key: TaskStatus; label: string }[] = [
  { key: 'Backlog', label: 'Backlog' },
  { key: 'InProgress', label: 'In Progress' },
  { key: 'Review', label: 'Review' },
  { key: 'Done', label: 'Done' },
]
