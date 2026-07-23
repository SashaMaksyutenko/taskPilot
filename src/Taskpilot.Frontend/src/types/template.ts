/** A reusable project template (mirrors the backend ProjectTemplateDto). */
export interface ProjectTemplate {
  id: string
  name: string
  description: string | null
  color: string | null
  /** How many tasks the template will stamp out. */
  taskCount: number
  createdAt: string
}

/** One task inside a template (mirrors the backend TemplateTaskDto). */
export interface TemplateTask {
  id: string
  title: string
  description: string | null
  priority: string
  /** Deadline as days from the project's start; null if the task has none. */
  deadlineOffsetDays: number | null
  parentTemplateTaskId: string | null
  tags: string[]
}

/** A template with its tasks, for previewing (mirrors ProjectTemplateDetailDto). */
export interface ProjectTemplateDetail extends ProjectTemplate {
  tasks: TemplateTask[]
}
