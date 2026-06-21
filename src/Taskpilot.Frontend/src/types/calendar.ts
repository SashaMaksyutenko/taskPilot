/** A task shown on the calendar (mirrors the backend CalendarTaskDto). */
export interface CalendarTask {
  id: string
  title: string
  projectId: string
  projectName: string
  status: string
  priority: string
  deadline: string
}
