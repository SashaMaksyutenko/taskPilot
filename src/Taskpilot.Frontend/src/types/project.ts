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
  memberCount: number
  isArchived: boolean
  /** Whether the current user (a member) has muted this project's notifications. */
  muted: boolean
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
  tags: string[]
  timeSpentSeconds: number
  timerStartedAt: string | null
  /** Sprint the task belongs to, or null when in the backlog. */
  sprintId: string | null
  /** Effort estimate in story points, or null when not estimated. */
  estimate: number | null
  /** "None" | "Daily" | "Weekly" | "Monthly". */
  recurrence: string
  recurrenceInterval: number
}

/** Task recurrence options offered in the UI. */
export const RECURRENCE_OPTIONS = ['None', 'Daily', 'Weekly', 'Monthly'] as const

/** A light reference to a task, used in dependency listings (mirrors TaskRefDto). */
export interface TaskRef {
  id: string
  title: string
  status: string
}

/** A task's dependency graph (mirrors TaskDependenciesDto). */
export interface TaskDependencies {
  dependsOn: TaskRef[]
  blocks: TaskRef[]
  isBlocked: boolean
}

/** Public share state of a board (mirrors ShareLinkDto). */
export interface ShareLink {
  token: string | null
  enabled: boolean
}

/** A task on a public read-only board (mirrors PublicTaskDto). */
export interface PublicTask {
  title: string
  status: string
  priority: string
  assigneeName: string | null
  deadline: string | null
  tags: string[]
}

/** A shared project board for anonymous viewers (mirrors PublicBoardDto). */
export interface PublicBoard {
  name: string
  color: string | null
  tasks: PublicTask[]
}

/** Created vs completed counts for one week (mirrors WeekBucketDto). */
export interface WeekBucket {
  weekStart: string
  created: number
  completed: number
}

/** Open/done load for one assignee (mirrors AssigneeLoadDto). */
export interface AssigneeLoad {
  name: string
  open: number
  done: number
}

/** The project's longest dependency chain (mirrors CriticalPathDto). */
export interface CriticalPath {
  length: number
  tasks: TaskRef[]
}

/** Aggregate delivery metrics for a project board (mirrors ProjectAnalyticsDto). */
export interface ProjectAnalytics {
  totalTasks: number
  byStatus: Record<string, number>
  byPriority: Record<string, number>
  weeks: WeekBucket[]
  avgCycleTimeDays: number | null
  throughputThisWeek: number
  throughputPrevWeek: number
  byAssignee: AssigneeLoad[]
}

/** A sprint / iteration with its task tallies (mirrors SprintDto). */
export interface Sprint {
  id: string
  projectId: string
  name: string
  goal: string | null
  startDate: string | null
  endDate: string | null
  status: string
  taskCount: number
  doneCount: number
  plannedPoints: number
  completedPoints: number
}

/** Input for creating/updating a sprint (mirrors SaveSprintDto). */
export interface SaveSprint {
  name: string
  goal?: string | null
  startDate?: string | null
  endDate?: string | null
  status?: string
}

export type ProjectMemberRole = 'Editor' | 'Viewer'

export interface ProjectMember {
  userId: string
  name: string
  avatarUrl: string | null
  role: ProjectMemberRole
  isOwner: boolean
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
